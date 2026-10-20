using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Manual, structured invoice intake. Matching and AP posting are separate governed workflows.</summary>
public static class ProcurementInvoiceIntakeRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoices");
        group.MapGet("", List).RequireAuthorization("procurement-invoice-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-invoice-read");
        group.MapPost("", Capture).RequireAuthorization("procurement-invoice-capture");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope, int? page, int? pageSize,
        CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.SupplierInvoices.AsNoTracking();
        if (scope.IsConfined) query = query.Where(x => x.BranchRef == scope.WorkLocationId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.SupplierId, x.BranchRef,
                SupplierName = db.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.DisplayName).FirstOrDefault(),
                x.InvoiceNumber, x.InvoiceDate,
                x.CurrencyCode, x.GrossAmount, x.Status, x.IntakeChannel, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var invoice = await db.SupplierInvoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && (!scope.IsConfined || x.BranchRef == scope.WorkLocationId), ct);
        if (invoice is null) return Results.NotFound(new { code = "not-found", message = "Invoice not found." });
        var lines = await db.SupplierInvoiceLines.AsNoTracking()
            .Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.LineNumber)
            .Select(x => new { x.Id, x.LineNumber, x.Description, x.Quantity,
                x.UnitPrice, x.TaxAmount, x.TaxCodeRef, x.CostCenterRef })
            .ToListAsync(ct);
        var supplierName = await db.Suppliers.AsNoTracking().Where(x => x.Id == invoice.SupplierId)
            .Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        return Results.Ok(new { invoice.Id, invoice.SupplierId, invoice.BranchRef, supplierName, invoice.PurchaseOrderId,
            invoice.InvoiceNumber, invoice.InvoiceDate, invoice.DueDate, invoice.CurrencyCode,
            invoice.NetAmount, invoice.TaxAmount, invoice.GrossAmount, invoice.IntakeChannel,
            invoice.Status, invoice.Version, invoice.CreatedAt, lines,
            nextStep = "Confirm source evidence and resolve duplicate or PO exceptions before matching." });
    }

    private static async Task<IResult> Capture(InvoiceIntakeInput input, ProcurementDbContext db,
        ProcurementRequestScope scope, HrmDbContext core,
        CancellationToken ct)
    {
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = Validate(input);
        if (issue is not null) return Invalid(issue);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.SupplierId, ct);
        if (supplier is null || supplier.Status is not ("Active" or "Suspended" or "Inactive"))
            return Invalid("Choose an existing approved vendor. Invoices for a vendor on hold can still be recorded for review.");
        if (input.PurchaseOrderId is { } poId)
        {
            var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == poId, ct);
            if (order is null || order.SupplierId != supplier.Id
                || scope.IsConfined && order.BranchRef != scope.WorkLocationId)
                return Invalid("Choose a purchase order available to you for this vendor and company.");
        }
        var normalized = input.InvoiceNumber!.Trim().ToUpperInvariant();
        var currency = input.CurrencyCode!.Trim().ToUpperInvariant();
        if (await db.SupplierInvoices.AnyAsync(x => x.SupplierId == supplier.Id
            && x.NormalizedInvoiceNumber == normalized && x.CurrencyCode == currency, ct))
            return Results.Conflict(new { code = "duplicate-invoice",
                message = "This vendor invoice number is already recorded in this currency." });

        var invoice = new SupplierInvoice
        {
            SupplierId = supplier.Id, PurchaseOrderId = input.PurchaseOrderId, BranchRef = scope.WorkLocationId,
            InvoiceNumber = input.InvoiceNumber.Trim(), NormalizedInvoiceNumber = normalized,
            InvoiceDate = input.InvoiceDate, DueDate = input.DueDate,
            CurrencyCode = currency, IntakeChannel = "Manual", Status = "Received",
            NetAmount = input.Lines!.Sum(x => x.Quantity * x.UnitPrice),
            TaxAmount = input.Lines!.Sum(x => x.TaxAmount),
        };
        invoice.GrossAmount = invoice.NetAmount + invoice.TaxAmount;
        db.AddScoped(invoice);
        for (var i = 0; i < input.Lines!.Count; i++)
        {
            var line = input.Lines[i];
            db.AddScoped(new SupplierInvoiceLine
            {
                SupplierInvoiceId = invoice.Id, LineNumber = i + 1,
                Description = line.Description!.Trim(), Quantity = line.Quantity,
                UnitPrice = line.UnitPrice, TaxAmount = line.TaxAmount,
                TaxCodeRef = Clean(line.TaxCodeRef), CostCenterRef = Clean(line.CostCenterRef),
            });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { code = "duplicate-invoice",
                message = "This vendor invoice number is already recorded or conflicts with another invoice." });
        }
        return Results.Created($"/api/procurement/v1/invoices/{invoice.Id}", new
        {
            invoice.Id, invoice.InvoiceNumber, invoice.Status, invoice.GrossAmount,
            invoice.CurrencyCode, invoice.BranchRef, invoice.Version,
        });
    }

    private static string? Validate(InvoiceIntakeInput input)
    {
        if (input.SupplierId == Guid.Empty) return "Choose a vendor.";
        if (string.IsNullOrWhiteSpace(input.InvoiceNumber) || input.InvoiceNumber.Trim().Length > 120)
            return "Enter an invoice number of up to 120 characters.";
        if (input.CurrencyCode is null || input.CurrencyCode.Trim().Length != 3 ||
            !input.CurrencyCode.Trim().All(char.IsLetter))
            return "Choose a three-letter currency code.";
        if (input.InvoiceDate == default) return "Enter an invoice date.";
        if (input.DueDate.HasValue && input.DueDate < input.InvoiceDate)
            return "Due date cannot be before invoice date.";
        if (input.Lines is null || input.Lines.Count is < 1 or > 200)
            return "Enter between 1 and 200 invoice lines.";
        foreach (var line in input.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 500)
                return "Each line needs a description of up to 500 characters.";
            if (line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxAmount < 0)
                return "Each line needs a positive quantity and non-negative price and tax.";
            if (line.Quantity * line.UnitPrice > 999_999_999_999m || line.TaxAmount > 999_999_999_999m)
                return "An invoice line amount is too large.";
        }
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Invalid(string message) => Results.UnprocessableEntity(new { code = "validation-failed", message });
}

public sealed record InvoiceIntakeInput(Guid SupplierId, Guid? PurchaseOrderId,
    string? InvoiceNumber, DateOnly InvoiceDate, DateOnly? DueDate, string? CurrencyCode,
    List<InvoiceIntakeLineInput>? Lines);
public sealed record InvoiceIntakeLineInput(string? Description, decimal Quantity,
    decimal UnitPrice, decimal TaxAmount, string? TaxCodeRef, string? CostCenterRef);
