using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Commercial contract drafts only. Review, signature and activation need governed workflows.</summary>
public static class ProcurementContractDraftRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/contracts");
        group.MapGet("", List).RequireAuthorization("procurement-contract-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-contract-read");
        group.MapPost("", Create).RequireAuthorization("procurement-contract-create");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-contract-edit");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-contract-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var own = db.SupplierContracts.AsNoTracking().Where(x => x.OwnerSubjectId == scope.SubjectId);
        var total = await own.CountAsync(ct);
        var items = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.SupplierId, x.Title, x.ContractType,
                x.CurrencyCode, x.CeilingAmount, x.Status, x.ExpiresOn, x.Version })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var contract = await db.SupplierContracts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (contract is null) return Problem("not-found", "Contract not found.", 404);
        var version = await db.SupplierContractVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SupplierContractId == id && x.RevisionNumber == 1, ct);
        return Results.Ok(Detail(contract, version));
    }

    private static async Task<IResult> Create(ContractDraftInput input, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = ProcurementContractDraftRules.Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, input.SupplierId, scope.SubjectId, ct))
            return Problem("supplier-unavailable", "Choose an available supplier in this company.", 422);

        var contract = new SupplierContract
        {
            Number = "CON-" + Guid.CreateVersion7().ToString("N"),
            SupplierId = input.SupplierId, Title = input.Title!.Trim(),
            ContractType = input.ContractType!.Trim(), CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(),
            CeilingAmount = input.CeilingAmount, EffectiveFrom = input.EffectiveFrom,
            ExpiresOn = input.ExpiresOn, RenewalNoticeOn = input.RenewalNoticeOn,
            OwnerSubjectId = scope.SubjectId, Status = "Draft",
        };
        var version = NewVersion(contract);
        db.AddScoped(contract);
        db.AddScoped(version);
        ProcurementDraftAudit.Add(db, scope, "SupplierContract", contract.Id,
            "DraftCreated", null, Detail(contract, version));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/contracts/{contract.Id}", Detail(contract, version));
    }

    private static async Task<IResult> Update(Guid id, VersionedContractDraft input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var contract = await db.SupplierContracts
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (contract is null) return Problem("not-found", "Contract not found.", 404);
        if (contract.Status != "Draft") return Problem("contract-locked", "Only a draft contract can be changed.", 409);
        if (contract.Version != input.Version) return Problem("version-conflict", "Refresh this contract before saving.", 409);
        var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            ? contract.CurrencyCode : input.Draft.CurrencyCode };
        var issue = ProcurementContractDraftRules.Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!await SupplierAvailable(db, draft.SupplierId, scope.SubjectId, ct))
            return Problem("supplier-unavailable", "Choose an available supplier in this company.", 422);
        var version = await db.SupplierContractVersions
            .SingleOrDefaultAsync(x => x.SupplierContractId == id && x.RevisionNumber == 1, ct);
        if (version is null || version.Status != "Draft")
            return Problem("contract-version-unavailable", "The draft version is unavailable.", 409);
        var before = ProcurementDraftAudit.Snapshot(Detail(contract, version));

        contract.SupplierId = draft.SupplierId;
        contract.Title = draft.Title!.Trim();
        contract.ContractType = draft.ContractType!.Trim();
        contract.CurrencyCode = draft.CurrencyCode!.Trim().ToUpperInvariant();
        contract.CeilingAmount = draft.CeilingAmount;
        contract.EffectiveFrom = draft.EffectiveFrom;
        contract.ExpiresOn = draft.ExpiresOn;
        contract.RenewalNoticeOn = draft.RenewalNoticeOn;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        version.CeilingAmount = draft.CeilingAmount;
        version.EffectiveFrom = draft.EffectiveFrom;
        version.ExpiresOn = draft.ExpiresOn;
        ProcurementDraftAudit.Add(db, scope, "SupplierContract", contract.Id,
            "DraftUpdated", before, Detail(contract, version));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this contract before saving.", 409); }
        return Results.Ok(Detail(contract, version));
    }

    private static async Task<IResult> Cancel(Guid id, ContractCancel input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var contract = await db.SupplierContracts
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (contract is null) return Problem("not-found", "Contract not found.", 404);
        if (contract.Status != "Draft") return Problem("contract-locked", "Only a draft contract can be cancelled.", 409);
        if (contract.Version != input.Version) return Problem("version-conflict", "Refresh this contract before cancelling.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a cancellation reason of up to 1,000 characters.", 422);
        var version = await db.SupplierContractVersions
            .SingleOrDefaultAsync(x => x.SupplierContractId == id && x.RevisionNumber == 1, ct);
        if (version is null) return Problem("contract-version-unavailable", "The draft version is unavailable.", 409);
        var before = ProcurementDraftAudit.Snapshot(Detail(contract, version));
        contract.Status = "Cancelled";
        contract.TerminationReason = input.Reason.Trim();
        version.Status = "Cancelled";
        version.AmendmentReason = input.Reason.Trim();
        ProcurementDraftAudit.Add(db, scope, "SupplierContract", contract.Id,
            "DraftCancelled", before, Detail(contract, version));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this contract before cancelling.", 409); }
        return Results.Ok(Detail(contract, version));
    }

    private static Task<bool> SupplierAvailable(ProcurementDbContext db, Guid supplierId,
        string subjectId, CancellationToken ct) => db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == supplierId
            && ((x.Status == "Draft" && x.CreatedBy == subjectId)
                || ((x.Status == "Active" || x.Status == "Approved") && x.IsOrderable)), ct);

    private static SupplierContractVersion NewVersion(SupplierContract contract) => new()
    {
        SupplierContractId = contract.Id, RevisionNumber = 1, Status = "Draft",
        CeilingAmount = contract.CeilingAmount, EffectiveFrom = contract.EffectiveFrom,
        ExpiresOn = contract.ExpiresOn,
    };

    private static object Detail(SupplierContract contract, SupplierContractVersion? version) => new
    {
        contract.Id, contract.Number, contract.SupplierId, contract.Title, contract.ContractType,
        contract.CurrencyCode, contract.CeilingAmount, contract.EffectiveFrom, contract.ExpiresOn,
        contract.RenewalNoticeOn, contract.Status, contract.TerminationReason,
        contract.Version, contract.CreatedAt,
        draftVersion = version is null ? null : new { version.Id, version.RevisionNumber, version.Status, version.Version },
    };

    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record VersionedContractDraft(long Version, ContractDraftInput Draft);
    public sealed record ContractCancel(long Version, string? Reason);
}
