using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owner-scoped recurring purchase plans. No invoice or payment command is exposed.</summary>
public static class ProcurementRecurringPurchaseRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/recurring-purchases");
        group.MapGet("", List).RequireAuthorization("procurement-recurring-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-recurring-read");
        group.MapPost("", Create).RequireAuthorization("procurement-recurring-create");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-recurring-edit");
        group.MapPost("/{id:guid}/close", Close).RequireAuthorization("procurement-recurring-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var own = Own(db, scope).AsNoTracking();
        var total = await own.CountAsync(ct);
        var rows = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size).ToListAsync(ct);
        var items = rows.Select(Detail).ToArray();
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var record = await Own(db, scope).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return record is null ? Problem("not-found", "Recurring purchase not found.", 404) : Results.Ok(Detail(record));
    }

    private static async Task<IResult> Create(JsonElement body, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (scope.AdminViewOnly || !scope.WorkerId.HasValue)
            return Problem("worker-required", "An active employee account is required.", 403);
        if (scope.OrgUnitId.HasValue || scope.IsConfined && !scope.WorkLocationId.HasValue)
            return Problem("scope-unavailable", "Choose an authorized branch before creating a recurring purchase.", 403);
        var parsed = ProcurementOriginalValueInput.Read<RecurringPurchaseCommitmentInput>(body);
        if (parsed.Error is not null) return parsed.Error;
        var input = parsed.Value! with { CurrencyCode = string.IsNullOrWhiteSpace(parsed.Value!.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : parsed.Value.CurrencyCode };
        var issue = RecurringPurchaseCommitmentRules.Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, input.SupplierId, scope.SubjectId, ct))
            return Problem("supplier-unavailable", "Choose an available supplier in this company.", 422);
        if (!await ContractAvailable(db, input.SupplierContractId, input.SupplierId, scope.SubjectId, ct))
            return Problem("contract-unavailable", "Choose a contract for this supplier and company.", 422);
        var record = new RecurringPurchaseCommitment
        {
            Number = "RPC-" + Guid.CreateVersion7().ToString("N"),
            OwnerWorkerId = scope.WorkerId.Value, OwnerSubjectId = scope.SubjectId,
            WorkLocationId = scope.WorkLocationId,
        };
        Assign(record, input);
        db.AddScoped(record);
        ProcurementDraftAudit.Add(db, scope, nameof(RecurringPurchaseCommitment), record.Id,
            "PlanCreated", null, Detail(record));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/recurring-purchases/{record.Id}", Detail(record));
    }

    private static async Task<IResult> Update(Guid id, JsonElement body, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (scope.AdminViewOnly || !scope.WorkerId.HasValue)
            return Problem("worker-required", "An active employee account is required.", 403);
        var parsed = ProcurementOriginalValueInput.Read<VersionedRecurringPurchase>(body);
        if (parsed.Error is not null) return parsed.Error;
        var input = parsed.Value!;
        if (input.Draft is null) return Problem("validation-failed", "Enter the recurring purchase details.", 422);
        var record = await Own(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (record is null) return Problem("not-found", "Recurring purchase not found.", 404);
        if (record.Status != "Draft") return Problem("record-closed", "A closed recurring purchase cannot be changed.", 409);
        if (record.Version != input.Version) return Problem("version-conflict", "Refresh and compare before saving.", 409);
        var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            ? record.CurrencyCode : input.Draft.CurrencyCode };
        var issue = RecurringPurchaseCommitmentRules.Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, draft.SupplierId, scope.SubjectId, ct))
            return Problem("supplier-unavailable", "Choose an available supplier in this company.", 422);
        if (!await ContractAvailable(db, draft.SupplierContractId, draft.SupplierId, scope.SubjectId, ct))
            return Problem("contract-unavailable", "Choose a contract for this supplier and company.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(record));
        Assign(record, draft);
        ProcurementDraftAudit.Add(db, scope, nameof(RecurringPurchaseCommitment), record.Id,
            "PlanUpdated", before, Detail(record));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh and compare before saving.", 409); }
        return Results.Ok(Detail(record));
    }

    private static async Task<IResult> Close(Guid id, CloseRecurringPurchase input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (scope.AdminViewOnly || !scope.WorkerId.HasValue)
            return Problem("worker-required", "An active employee account is required.", 403);
        var record = await Own(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (record is null) return Problem("not-found", "Recurring purchase not found.", 404);
        if (record.Status != "Draft") return Problem("record-closed", "This recurring purchase is already closed.", 409);
        if (record.Version != input.Version) return Problem("version-conflict", "Refresh and compare before closing.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a reason of up to 1,000 characters.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(record));
        record.Status = "Closed";
        record.ClosedAt = DateTimeOffset.UtcNow;
        record.ClosedReason = input.Reason.Trim();
        ProcurementDraftAudit.Add(db, scope, nameof(RecurringPurchaseCommitment), record.Id,
            "PlanClosed", before, Detail(record));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh and compare before closing.", 409); }
        return Results.Ok(Detail(record));
    }

    private static IQueryable<RecurringPurchaseCommitment> Own(ProcurementDbContext db, ProcurementRequestScope scope)
    {
        if (!scope.WorkerId.HasValue || scope.OrgUnitId.HasValue
            || scope.IsConfined && !scope.WorkLocationId.HasValue)
            return db.RecurringPurchaseCommitments.Where(x => false);
        var workerId = scope.WorkerId.Value;
        var own = db.RecurringPurchaseCommitments.Where(x => x.OwnerSubjectId == scope.SubjectId
            && x.OwnerWorkerId == workerId);
        if (scope.WorkLocationId.HasValue) own = own.Where(x => x.WorkLocationId == scope.WorkLocationId);
        return own;
    }

    private static Task<bool> SupplierAvailable(ProcurementDbContext db, Guid id, string subject, CancellationToken ct) =>
        db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == id &&
            ((x.Status == "Draft" && x.CreatedBy == subject)
                || ((x.Status == "Active" || x.Status == "Approved") && x.IsOrderable)), ct);

    private static Task<bool> ContractAvailable(ProcurementDbContext db, Guid? id, Guid supplierId,
        string subject, CancellationToken ct) => id is null ? Task.FromResult(true) :
        db.SupplierContracts.AsNoTracking().AnyAsync(x => x.Id == id && x.SupplierId == supplierId
            && x.OwnerSubjectId == subject && x.Status != "Cancelled", ct);

    private static void Assign(RecurringPurchaseCommitment record, RecurringPurchaseCommitmentInput input)
    {
        record.SupplierId = input.SupplierId;
        record.SupplierContractId = input.SupplierContractId;
        record.Title = input.Title!.Trim();
        record.Frequency = input.Frequency!;
        record.FirstCycleOn = input.FirstCycleOn;
        record.LastCycleOn = input.LastCycleOn;
        record.NextCycleOn = input.NextCycleOn;
        record.ExpectedAmount = input.ExpectedAmount;
        record.CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant();
    }

    private static object Detail(RecurringPurchaseCommitment x) => new
    {
        x.Id, x.Number, x.Title, x.SupplierId, x.SupplierContractId, x.OwnerWorkerId,
        x.WorkLocationId, x.Frequency, x.FirstCycleOn, x.LastCycleOn, x.NextCycleOn,
        x.ExpectedAmount, exactExpectedAmount = x.ExpectedAmount.ToString(CultureInfo.InvariantCulture),
        x.CurrencyCode, x.Status, x.ClosedAt, x.ClosedReason,
        x.Version, x.CreatedAt
    };

    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record VersionedRecurringPurchase(long Version, RecurringPurchaseCommitmentInput? Draft);
    public sealed record CloseRecurringPurchase(long Version, string? Reason);
}
