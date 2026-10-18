using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Draft reference configuration. No route here activates a policy or procurement method.</summary>
public static class ProcurementConfigurationRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var categories = app.MapGroup("/api/procurement/v1/configuration/categories");
        categories.MapGet("", ListCategories).RequireAuthorization("procurement-configuration-read");
        categories.MapGet("/{id:guid}", GetCategory).RequireAuthorization("procurement-configuration-read");
        categories.MapPost("", CreateCategory).RequireAuthorization("procurement-configuration-edit");
        categories.MapPut("/{id:guid}", UpdateCategory).RequireAuthorization("procurement-configuration-edit");

        var methods = app.MapGroup("/api/procurement/v1/configuration/methods");
        methods.MapGet("", ListMethods).RequireAuthorization("procurement-configuration-read");
        methods.MapGet("/{id:guid}", GetMethod).RequireAuthorization("procurement-configuration-read");
        methods.MapPost("", CreateMethod).RequireAuthorization("procurement-configuration-edit");
        methods.MapPut("/{id:guid}", UpdateMethod).RequireAuthorization("procurement-configuration-edit");
    }

    private static async Task<IResult> ListCategories(ProcurementDbContext db, string? status,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.ProcurementCategories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Code).Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Code, x.Name, x.ParentCode, x.ExpenseAccountRef,
                x.Status, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> GetCategory(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        var item = await db.ProcurementCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? Problem("not-found", "Category not found.", 404) : Results.Ok(CategoryDetail(item));
    }

    private static async Task<IResult> CreateCategory(CategoryInput input, ProcurementDbContext db, CancellationToken ct)
    {
        var issue = CategoryIssue(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var code = input.Code!.Trim().ToUpperInvariant();
        var parent = Clean(input.ParentCode)?.ToUpperInvariant();
        var parentIssue = await ParentIssue(db, code, parent, ct);
        if (parentIssue is not null) return Problem("validation-failed", parentIssue, 422);
        if (await db.ProcurementCategories.AnyAsync(x => x.Code == code, ct))
            return Problem("duplicate-code", "This category code already exists in the company.", 409);
        var category = new ProcurementCategory
        {
            Code = code, Name = input.Name!.Trim(), ParentCode = parent,
            ExpenseAccountRef = Clean(input.ExpenseAccountRef), Status = "Draft",
        };
        db.AddScoped(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Problem("duplicate-code", "This category code already exists in the company.", 409); }
        return Results.Created($"/api/procurement/v1/configuration/categories/{category.Id}", CategoryDetail(category));
    }

    private static async Task<IResult> UpdateCategory(Guid id, VersionedCategory input,
        ProcurementDbContext db, CancellationToken ct)
    {
        var category = await db.ProcurementCategories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (category is null) return Problem("not-found", "Category not found.", 404);
        if (category.Status != "Draft") return Problem("category-locked", "Only a draft category can be changed.", 409);
        if (category.Version != input.Version) return Problem("version-conflict", "Refresh this category before saving.", 409);
        var issue = CategoryIssue(input.Draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!string.Equals(category.Code, input.Draft.Code!.Trim(), StringComparison.OrdinalIgnoreCase))
            return Problem("code-locked", "Category codes cannot be changed; create a new category.", 409);
        var parent = Clean(input.Draft.ParentCode)?.ToUpperInvariant();
        var parentIssue = await ParentIssue(db, category.Code, parent, ct);
        if (parentIssue is not null) return Problem("validation-failed", parentIssue, 422);
        category.Name = input.Draft.Name!.Trim();
        category.ParentCode = parent;
        category.ExpenseAccountRef = Clean(input.Draft.ExpenseAccountRef);
        category.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this category before saving.", 409); }
        return Results.Ok(CategoryDetail(category));
    }

    private static async Task<string?> ParentIssue(ProcurementDbContext db, string code,
        string? parentCode, CancellationToken ct)
    {
        if (parentCode is null) return null;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
        var next = parentCode;
        for (var depth = 0; depth < 20; depth++)
        {
            if (!visited.Add(next)) return "A category cannot be its own parent or create a cycle.";
            var parent = await db.ProcurementCategories.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Code == next && x.Status != "Retired", ct);
            if (parent is null) return "Choose an existing category in this company as the parent.";
            if (string.IsNullOrWhiteSpace(parent.ParentCode)) return null;
            next = parent.ParentCode;
        }
        return "Category hierarchy is too deep.";
    }

    private static string? CategoryIssue(CategoryInput input) =>
        ProcurementReferenceRules.ValidateCode(input.Code)
        ?? ProcurementReferenceRules.ValidateName(input.Name)
        ?? (string.IsNullOrWhiteSpace(input.ParentCode) ? null : ProcurementReferenceRules.ValidateCode(input.ParentCode))
        ?? ProcurementReferenceRules.ValidateOptional(input.ExpenseAccountRef, "Expense account reference", 100);

    private static async Task<IResult> ListMethods(ProcurementDbContext db, string? status,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.ProcurementMethods.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Code).Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description, x.PolicyVersionId,
                x.Status, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> GetMethod(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        var item = await db.ProcurementMethods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? Problem("not-found", "Procurement method not found.", 404) : Results.Ok(MethodDetail(item));
    }

    private static async Task<IResult> CreateMethod(MethodInput input, ProcurementDbContext db, CancellationToken ct)
    {
        var issue = MethodIssue(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (input.PolicyVersionId is Guid policyId &&
            !await db.PolicyVersions.AnyAsync(x => x.Id == policyId, ct))
            return Problem("validation-failed", "Policy version is unavailable in this company.", 422);
        var code = input.Code!.Trim().ToUpperInvariant();
        if (await db.ProcurementMethods.AnyAsync(x => x.Code == code, ct))
            return Problem("duplicate-code", "This procurement method code already exists.", 409);
        var method = new ProcurementMethod
        {
            Code = code, Name = input.Name!.Trim(), Description = Clean(input.Description),
            PolicyVersionId = input.PolicyVersionId, Status = "Draft",
        };
        db.AddScoped(method);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Problem("duplicate-code", "This procurement method code already exists.", 409); }
        return Results.Created($"/api/procurement/v1/configuration/methods/{method.Id}", MethodDetail(method));
    }

    private static async Task<IResult> UpdateMethod(Guid id, VersionedMethod input,
        ProcurementDbContext db, CancellationToken ct)
    {
        var method = await db.ProcurementMethods.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (method is null) return Problem("not-found", "Procurement method not found.", 404);
        if (method.Status != "Draft") return Problem("method-locked", "Only a draft procurement method can be changed.", 409);
        if (method.Version != input.Version) return Problem("version-conflict", "Refresh this method before saving.", 409);
        var issue = MethodIssue(input.Draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!string.Equals(method.Code, input.Draft.Code!.Trim(), StringComparison.OrdinalIgnoreCase))
            return Problem("code-locked", "Method codes cannot be changed; create a new method.", 409);
        if (input.Draft.PolicyVersionId is Guid policyId &&
            !await db.PolicyVersions.AnyAsync(x => x.Id == policyId, ct))
            return Problem("validation-failed", "Policy version is unavailable in this company.", 422);
        method.Name = input.Draft.Name!.Trim();
        method.Description = Clean(input.Draft.Description);
        method.PolicyVersionId = input.Draft.PolicyVersionId;
        method.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this method before saving.", 409); }
        return Results.Ok(MethodDetail(method));
    }

    private static string? MethodIssue(MethodInput input) =>
        ProcurementReferenceRules.ValidateCode(input.Code)
        ?? ProcurementReferenceRules.ValidateName(input.Name)
        ?? ProcurementReferenceRules.ValidateOptional(input.Description, "Description", 2000);

    private static object CategoryDetail(ProcurementCategory x) => new
    {
        x.Id, x.Code, x.Name, x.ParentCode, x.ExpenseAccountRef,
        x.Status, x.Version, x.CreatedAt, x.UpdatedAt,
    };

    private static object MethodDetail(ProcurementMethod x) => new
    {
        x.Id, x.Code, x.Name, x.Description, x.PolicyVersionId,
        x.Status, x.Version, x.CreatedAt, x.UpdatedAt,
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record CategoryInput(string? Code, string? Name, string? ParentCode, string? ExpenseAccountRef);
    public sealed record VersionedCategory(long Version, CategoryInput Draft);
    public sealed record MethodInput(string? Code, string? Name, string? Description, Guid? PolicyVersionId);
    public sealed record VersionedMethod(long Version, MethodInput Draft);
}
