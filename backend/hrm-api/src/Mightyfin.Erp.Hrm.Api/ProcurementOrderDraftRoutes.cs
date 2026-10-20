using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Internal PO drafts only; approval, issue and commitments remain separate gates.</summary>
public static class ProcurementOrderDraftRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/orders");
        group.MapGet("", List).RequireAuthorization("procurement-po-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-po-read");
        group.MapPost("", Create).RequireAuthorization("procurement-po-create");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-po-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var own = db.PurchaseOrders.AsNoTracking().Where(x => x.BuyerSubjectId == scope.SubjectId);
        var total = await own.CountAsync(ct);
        var rows = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.SupplierId,
                SupplierName = db.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.DisplayName).FirstOrDefault(),
                x.CurrencyCode, x.Status, x.ExpectedDeliveryOn, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.BuyerSubjectId == scope.SubjectId, ct);
        if (order is null) return Problem("not-found", "Purchase order not found.", 404);
        var lines = await db.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == id && x.IsActive).OrderBy(x => x.LineNumber).ToListAsync(ct);
        var supplierName = await db.Suppliers.AsNoTracking().Where(x => x.Id == order.SupplierId)
            .Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        return Results.Ok(Detail(order, lines, supplierName));
    }

    private static async Task<IResult> Create(OrderDraftInput input, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, input.SupplierId, ct))
            return Problem("supplier-unavailable", "Choose an active vendor in this company.", 422);
        var order = new PurchaseOrder
        {
            Number = "PO-" + Guid.CreateVersion7().ToString("N"), SupplierId = input.SupplierId,
            CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(), BuyerSubjectId = scope.SubjectId,
            ShipToRef = Clean(input.ShipToRef), BillToRef = Clean(input.BillToRef),
            PaymentTermRef = Clean(input.PaymentTermRef), ExpectedDeliveryOn = input.ExpectedDeliveryOn,
            Status = "Draft",
        };
        db.AddScoped(order);
        var lines = new List<PurchaseOrderLine>();
        for (var i = 0; i < input.Lines!.Count; i++)
        {
            var line = NewLine(order.Id, i + 1, input.Lines[i]);
            db.AddScoped(line);
            lines.Add(line);
        }
        ProcurementDraftAudit.Add(db, scope, "PurchaseOrder", order.Id,
            "DraftCreated", null, Detail(order, lines, null));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/orders/{order.Id}", Detail(order, lines, null));
    }

    private static async Task<IResult> Update(Guid id, VersionedOrderDraft input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var order = await db.PurchaseOrders
            .SingleOrDefaultAsync(x => x.Id == id && x.BuyerSubjectId == scope.SubjectId, ct);
        if (order is null) return Problem("not-found", "Purchase order not found.", 404);
        if (order.Status != "Draft") return Problem("order-locked", "Only a draft order can be changed.", 409);
        if (order.Version != input.Version) return Problem("version-conflict", "Refresh this order before saving.", 409);
        if (input.Draft is null) return Problem("validation-failed", "Enter order details.", 422);
        var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            ? order.CurrencyCode : input.Draft.CurrencyCode };
        var issue = Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, draft.SupplierId, ct))
            return Problem("supplier-unavailable", "Choose an active vendor in this company.", 422);
        var allLines = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == id)
            .OrderBy(x => x.LineNumber).ToListAsync(ct);
        var active = allLines.Where(x => x.IsActive).ToList();
        var suppliedIds = draft.Lines!.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (suppliedIds.Count != suppliedIds.Distinct().Count()
            || suppliedIds.Any(lineId => active.All(x => x.Id != lineId)))
            return Problem("invalid-line", "Refresh this order before editing its lines.", 409);
        var removed = active.Where(x => !suppliedIds.Contains(x.Id)).ToList();
        if (removed.Count > 0 && (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000))
            return Problem("validation-failed", "Give a reason of up to 1,000 characters when removing a line.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(order, active, null));
        order.SupplierId = draft.SupplierId;
        order.CurrencyCode = draft.CurrencyCode!.Trim().ToUpperInvariant();
        order.ShipToRef = Clean(draft.ShipToRef);
        order.BillToRef = Clean(draft.BillToRef);
        order.PaymentTermRef = Clean(draft.PaymentTermRef);
        order.ExpectedDeliveryOn = draft.ExpectedDeliveryOn;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var line in removed) line.IsActive = false;
        var next = allLines.Count == 0 ? 1 : allLines.Max(x => x.LineNumber) + 1;
        foreach (var revised in draft.Lines!)
        {
            if (revised.Id is { } lineId)
                ApplyLine(active.Single(x => x.Id == lineId), revised);
            else
            {
                var line = NewLine(id, next++, revised);
                db.AddScoped(line);
                allLines.Add(line);
            }
        }
        var current = allLines.Where(x => x.IsActive).OrderBy(x => x.LineNumber).ToList();
        ProcurementDraftAudit.Add(db, scope, "PurchaseOrder", id, "DraftUpdated", before,
            new { order = Detail(order, current, null), reason = Clean(input.Reason) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this order before saving.", 409); }
        return Results.Ok(Detail(order, current, null));
    }

    private static async Task<IResult> Cancel(Guid id, OrderDraftCancel input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var order = await db.PurchaseOrders
            .SingleOrDefaultAsync(x => x.Id == id && x.BuyerSubjectId == scope.SubjectId, ct);
        if (order is null) return Problem("not-found", "Purchase order not found.", 404);
        if (order.Status != "Draft") return Problem("order-locked", "Only a draft order can be cancelled.", 409);
        if (order.Version != input.Version) return Problem("version-conflict", "Refresh this order before cancelling.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a cancellation reason of up to 1,000 characters.", 422);
        var lines = await db.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == id && x.IsActive).OrderBy(x => x.LineNumber).ToListAsync(ct);
        var before = ProcurementDraftAudit.Snapshot(Detail(order, lines, null));
        order.Status = "Cancelled";
        order.ClosureReason = input.Reason.Trim();
        order.ClosedAt = DateTimeOffset.UtcNow;
        ProcurementDraftAudit.Add(db, scope, "PurchaseOrder", id, "DraftCancelled", before,
            Detail(order, lines, null));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this order before cancelling.", 409); }
        return Results.Ok(Detail(order, lines, null));
    }

    private static Task<bool> SupplierAvailable(ProcurementDbContext db, Guid supplierId,
        CancellationToken ct) => db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == supplierId
            && (x.Status == "Active" || x.Status == "Approved") && x.IsOrderable, ct);

    private static string? Validate(OrderDraftInput input)
    {
        if (input.SupplierId == Guid.Empty) return "Choose a vendor.";
        if (input.CurrencyCode is null || input.CurrencyCode.Trim().Length != 3
            || !input.CurrencyCode.Trim().All(char.IsLetter)) return "Choose a three-letter currency.";
        if (input.Lines is null || input.Lines.Count is < 1 or > 100)
            return "Enter between 1 and 100 order lines.";
        if (new[] { input.ShipToRef, input.BillToRef, input.PaymentTermRef }
            .Any(x => x?.Length > 500)) return "Address and payment references must be under 500 characters.";
        foreach (var line in input.Lines)
        {
            if (line is null) return "Each order line needs details.";
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 500)
                return "Each line needs a description of up to 500 characters.";
            if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode.Trim().Length > 20)
                return "Each line needs a unit of up to 20 characters.";
            if (line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxAmount < 0
                || line.Quantity > 1_000_000m || line.UnitPrice > 1_000_000_000m
                || line.TaxAmount > 1_000_000_000_000m)
                return "Line quantity, price or tax is outside the allowed range.";
        }
        return null;
    }

    private static PurchaseOrderLine NewLine(Guid orderId, int number, OrderDraftLineInput input)
    {
        var line = new PurchaseOrderLine { PurchaseOrderId = orderId, LineNumber = number };
        ApplyLine(line, input);
        return line;
    }

    private static void ApplyLine(PurchaseOrderLine line, OrderDraftLineInput input)
    {
        line.Description = input.Description!.Trim();
        line.UnitCode = input.UnitCode!.Trim().ToUpperInvariant();
        line.Quantity = input.Quantity;
        line.UnitPrice = input.UnitPrice;
        line.TaxAmount = input.TaxAmount;
        line.CostCenterRef = Clean(input.CostCenterRef);
        line.ProjectRef = Clean(input.ProjectRef);
        line.ExpectedDeliveryOn = input.ExpectedDeliveryOn;
    }

    private static object Detail(PurchaseOrder order, IReadOnlyCollection<PurchaseOrderLine> lines,
        string? supplierName) => new
    {
        order.Id, order.Number, order.SupplierId, supplierName, order.CurrencyCode,
        order.Status, order.BuyerSubjectId, order.ShipToRef, order.BillToRef,
        order.PaymentTermRef, order.ExpectedDeliveryOn, order.ClosureReason,
        order.Version, order.CreatedAt,
        total = lines.Sum(x => x.Quantity * x.UnitPrice + x.TaxAmount),
        lines = lines.Select(x => new { x.Id, x.LineNumber, x.Description, x.UnitCode,
            x.Quantity, x.UnitPrice, x.TaxAmount, x.CostCenterRef, x.ProjectRef,
            x.ExpectedDeliveryOn }),
        nextStep = "Approval and issue require an approved source, Finance commitment and delegated authority.",
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record VersionedOrderDraft(long Version, string? Reason, OrderDraftInput Draft);
    public sealed record OrderDraftCancel(long Version, string? Reason);
}

public sealed record OrderDraftInput(Guid SupplierId, string? CurrencyCode, string? ShipToRef,
    string? BillToRef, string? PaymentTermRef, DateOnly? ExpectedDeliveryOn,
    List<OrderDraftLineInput>? Lines);
public sealed record OrderDraftLineInput(Guid? Id, string? Description, string? UnitCode,
    decimal Quantity, decimal UnitPrice, decimal TaxAmount, string? CostCenterRef,
    string? ProjectRef, DateOnly? ExpectedDeliveryOn);
