using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Draft commercial rates belong to revision one; they grant no purchasing authority.</summary>
public static partial class ProcurementContractDraftRoutes
{
    private static void MapRateRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/rates", ListRates).RequireAuthorization("procurement-contract-read");
        group.MapPost("/{id:guid}/rates", AddRate).RequireAuthorization("procurement-contract-edit");
        group.MapPut("/{id:guid}/rates/{rateId:guid}", UpdateRate).RequireAuthorization("procurement-contract-edit");
        group.MapPost("/{id:guid}/rates/{rateId:guid}/remove", RemoveRate).RequireAuthorization("procurement-contract-edit");
    }

    private static async Task<IResult> ListRates(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var owner = await db.SupplierContracts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (owner is null) return Problem("not-found", "Contract not found.", 404);
        var version = await db.SupplierContractVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SupplierContractId == id && x.RevisionNumber == 1, ct);
        if (version is null) return Problem("contract-version-unavailable", "The draft version is unavailable.", 409);
        var rows = await db.ContractRates.AsNoTracking()
            .Where(x => x.SupplierContractVersionId == version.Id)
            .OrderBy(x => x.LineNumber).ThenBy(x => x.Id).ToListAsync(ct);
        return Results.Ok(new { contractId = id, contractVersion = owner.Version,
            draftVersionId = version.Id, items = rows.Select(x => RateDetail(x)).ToArray() });
    }

    private static async Task<IResult> AddRate(Guid id, JsonElement body, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed = ProcurementOriginalValueInput.Read<VersionedContractRateDraft>(body);
        if (parsed.Error is not null) return parsed.Error;
        var input = parsed.Value!;
        if (input.Rate is null) return Problem("validation-failed", "Enter the rate details.", 422);
        var issue = ProcurementContractRateRules.Validate(input.Rate);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        try
        {
            await using var transaction = await ProcurementRecurringContractTransactions.BeginAsync(db, ct);
            var loaded = await EditableRateParent(id, input.ContractVersion, db, scope, ct);
            if (loaded.Error is not null) return loaded.Error;
            var (contract, version) = loaded.Value!.Value;
            var itemError = await CatalogRateIssue(db, input.Rate, ct);
            if (itemError is not null) return itemError;
            var last = await db.ContractRates.Where(x => x.SupplierContractVersionId == version.Id)
                .Select(x => (int?)x.LineNumber).MaxAsync(ct) ?? 0;
            if (last == int.MaxValue) return Problem("rate-limit", "No more rates can be added to this draft.", 409);
            var rate = new ContractRate { SupplierContractVersionId = version.Id, LineNumber = last + 1 };
            SetRate(rate, input.Rate);
            db.AddScoped(rate);
            TouchContract(db, contract);
            ProcurementDraftAudit.Add(db, scope, "SupplierContract", id, "DraftRateAdded", null,
                new { contractVersion = checked(contract.Version + 1), rate = RateDetail(rate) });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Created($"/api/procurement/v1/contracts/{id}/rates/{rate.Id}",
                new { contractId = id, contractVersion = contract.Version, rate = RateDetail(rate) });
        }
        catch (Exception error) when (ProcurementRecurringContractTransactions.IsConflict(error))
        { return Problem("version-conflict", "The contract changed. Refresh its rates before saving.", 409); }
    }

    private static async Task<IResult> UpdateRate(Guid id, Guid rateId, JsonElement body,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed = ProcurementOriginalValueInput.Read<VersionedContractRateDraft>(body);
        if (parsed.Error is not null) return parsed.Error;
        var input = parsed.Value!;
        if (input.Rate is null) return Problem("validation-failed", "Enter the rate details.", 422);
        var issue = ProcurementContractRateRules.Validate(input.Rate);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        try
        {
            await using var transaction = await ProcurementRecurringContractTransactions.BeginAsync(db, ct);
            var loaded = await EditableRateParent(id, input.ContractVersion, db, scope, ct);
            if (loaded.Error is not null) return loaded.Error;
            var (contract, version) = loaded.Value!.Value;
            var rate = await db.ContractRates.SingleOrDefaultAsync(x => x.Id == rateId
                && x.SupplierContractVersionId == version.Id, ct);
            if (rate is null) return Problem("not-found", "Rate not found.", 404);
            if (rate.Status != "Active") return Problem("rate-removed", "A removed rate cannot be changed.", 409);
            if (rate.Version != input.RateVersion) return Problem("version-conflict", "Refresh this rate before saving.", 409);
            var itemError = await CatalogRateIssue(db, input.Rate, ct);
            if (itemError is not null) return itemError;
            var before = ProcurementDraftAudit.Snapshot(RateDetail(rate));
            SetRate(rate, input.Rate);
            TouchContract(db, contract);
            ProcurementDraftAudit.Add(db, scope, "SupplierContract", id, "DraftRateUpdated", before,
                new { contractVersion = checked(contract.Version + 1), rate = RateDetail(rate, checked(rate.Version + 1)) });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(new { contractId = id, contractVersion = contract.Version, rate = RateDetail(rate) });
        }
        catch (Exception error) when (ProcurementRecurringContractTransactions.IsConflict(error))
        { return Problem("version-conflict", "The contract changed. Refresh its rates before saving.", 409); }
    }

    private static async Task<IResult> RemoveRate(Guid id, Guid rateId, ContractRateRemoval input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a removal reason of up to 1,000 characters.", 422);
        try
        {
            await using var transaction = await ProcurementRecurringContractTransactions.BeginAsync(db, ct);
            var loaded = await EditableRateParent(id, input.ContractVersion, db, scope, ct);
            if (loaded.Error is not null) return loaded.Error;
            var (contract, version) = loaded.Value!.Value;
            var rate = await db.ContractRates.SingleOrDefaultAsync(x => x.Id == rateId
                && x.SupplierContractVersionId == version.Id, ct);
            if (rate is null) return Problem("not-found", "Rate not found.", 404);
            if (rate.Status != "Active") return Problem("rate-removed", "This rate has already been removed.", 409);
            if (rate.Version != input.RateVersion) return Problem("version-conflict", "Refresh this rate before removing it.", 409);
            var before = ProcurementDraftAudit.Snapshot(RateDetail(rate));
            rate.Status = "Removed";
            rate.StatusReason = input.Reason.Trim();
            TouchContract(db, contract);
            ProcurementDraftAudit.Add(db, scope, "SupplierContract", id, "DraftRateRemoved", before,
                new { contractVersion = checked(contract.Version + 1), rate = RateDetail(rate, checked(rate.Version + 1)) });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(new { contractId = id, contractVersion = contract.Version, rate = RateDetail(rate) });
        }
        catch (Exception error) when (ProcurementRecurringContractTransactions.IsConflict(error))
        { return Problem("version-conflict", "The contract changed. Refresh its rates before saving.", 409); }
    }

    private static async Task<((SupplierContract Contract, SupplierContractVersion Version)? Value, IResult? Error)>
        EditableRateParent(Guid id, long expectedVersion, ProcurementDbContext db,
            ProcurementRequestScope scope, CancellationToken ct)
    {
        var contract = await db.SupplierContracts.SingleOrDefaultAsync(x => x.Id == id
            && x.OwnerSubjectId == scope.SubjectId, ct);
        if (contract is null) return (null, Problem("not-found", "Contract not found.", 404));
        if (contract.Status != "Draft") return (null, Problem("contract-locked", "Only a draft contract can be changed.", 409));
        if (contract.Version != expectedVersion) return (null, Problem("version-conflict", "Refresh this contract before saving.", 409));
        var version = await db.SupplierContractVersions.SingleOrDefaultAsync(x => x.SupplierContractId == id
            && x.RevisionNumber == 1, ct);
        if (version is null || version.Status != "Draft")
            return (null, Problem("contract-version-unavailable", "The draft version is unavailable.", 409));
        return ((contract, version), null);
    }

    private static async Task<IResult?> CatalogRateIssue(ProcurementDbContext db,
        ContractRateDraftInput input, CancellationToken ct)
    {
        if (input.CatalogItemId is not { } itemId) return null;
        var unit = input.UnitCode!.Trim();
        var valid = await db.CatalogItems.AsNoTracking().AnyAsync(x => x.Id == itemId
            && x.Status == "Active" && x.IsPurchasable && x.UnitCode == unit, ct);
        return valid ? null : Problem("item-unavailable", "Choose an active purchasable item with the same unit.", 422);
    }

    private static void SetRate(ContractRate rate, ContractRateDraftInput input)
    {
        rate.CatalogItemId = input.CatalogItemId;
        rate.Description = input.Description!.Trim();
        rate.UnitCode = input.UnitCode!.Trim();
        rate.UnitPrice = input.UnitPrice;
        rate.MaximumQuantity = input.MaximumQuantity;
    }

    private static void TouchContract(ProcurementDbContext db, SupplierContract contract)
    {
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        db.Entry(contract).Property(x => x.UpdatedAt).IsModified = true;
    }

    private static object RateDetail(ContractRate rate, long? resultingVersion = null) => new
    {
        rate.Id, rate.LineNumber, rate.CatalogItemId, rate.Description, rate.UnitCode,
        unitPriceExact = rate.UnitPrice.ToString("0.############################", CultureInfo.InvariantCulture),
        maximumQuantityExact = rate.MaximumQuantity?.ToString("0.############################", CultureInfo.InvariantCulture),
        rate.Status, rate.StatusReason, version = resultingVersion ?? rate.Version,
    };

    public sealed record VersionedContractRateDraft(long ContractVersion, long? RateVersion, ContractRateDraftInput? Rate);
    public sealed record ContractRateRemoval(long ContractVersion, long RateVersion, string? Reason);
}
