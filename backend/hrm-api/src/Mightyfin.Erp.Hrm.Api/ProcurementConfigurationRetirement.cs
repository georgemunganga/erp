using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementConfigurationRoutes
{
    private static async Task<IResult> RetireCategory(Guid id, ActivationInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var category = await db.ProcurementCategories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (category is null) return Problem("not-found", "Category not found.", 404);
        var issue = RetirementIssue(category.Status, category.Version, input);
        if (issue is not null) return issue;
        if (await db.ProcurementCategories.AsNoTracking().AnyAsync(x => x.ParentCode == category.Code && x.Status != "Retired", ct))
            return Problem("category-in-use", "Move or retire this category's child categories first.", 409);
        if (await db.CatalogItems.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct)
            || await db.PurchaseRequestLines.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct)
            || await db.ProcurementPlanLines.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct)
            || await db.Suppliers.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct)
            || await db.SupplierCategories.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct)
            || await db.SourcingLines.AsNoTracking().AnyAsync(x => x.CategoryCode == category.Code, ct))
            return Problem("category-in-use", "This category is already used by procurement records. Keep its history and use a controlled change.", 409);
        category.Status = "Retired";
        AddRetirementAudit(db, scope, nameof(ProcurementCategory), id, input);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this category before removing it.", 409); }
        return Results.Ok(CategoryDetail(category));
    }

    private static async Task<IResult> RetireMethod(Guid id, ActivationInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var method = await db.ProcurementMethods.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (method is null) return Problem("not-found", "Buying method not found.", 404);
        var issue = RetirementIssue(method.Status, method.Version, input);
        if (issue is not null) return issue;
        method.Status = "Retired";
        AddRetirementAudit(db, scope, nameof(ProcurementMethod), id, input);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this buying method before removing it.", 409); }
        return Results.Ok(MethodDetail(method));
    }

    private static IResult? RetirementIssue(string status, long version, ActivationInput input)
    {
        if (input.Version != version) return Problem("version-conflict", "Refresh this setting before removing it.", 409);
        if (status != "Draft") return Problem("reference-locked", "Only an unused draft can be removed here. Active settings need a controlled change.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Enter a removal reason of up to 1,000 characters.", 422);
        return null;
    }

    private static void AddRetirementAudit(ProcurementDbContext db, ProcurementRequestScope scope,
        string recordType, Guid id, ActivationInput input) => db.AddScoped(new AuditEvent
    {
        RecordType = recordType, RecordId = id, Action = "DraftRetired", ActorSubjectId = scope.SubjectId,
        ChangedFieldsJson = JsonSerializer.Serialize(new { from = "Draft", to = "Retired", reason = input.Reason!.Trim(), version = input.Version }),
    });
}
