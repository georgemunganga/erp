using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task RequireUnitReviewPolicySchema(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        var schema = http.RequestServices.GetService<IProcurementInvoiceUnitReviewSchemaReadiness>();
        if (schema is null || !await schema.IsReadyAsync(db, ct))
            throw new OperationalIdentityProblem("invoice-unit-review-schema-unavailable", "Original unit-review controls are not ready. No change was saved.", 503);
    }
    private static async Task<IResult?> CheckUnitReviewCompanyRules(PolicyVersion policy, HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (!await ReceivingConfigurationActor(core, scope, permission, ct))
            return Problem("unit-review-configuration-denied", "Your current account cannot configure original-unit review.", 403);
        var issues = InvoiceUnitReviewPolicyRules.Validate(policy.RulesJson);
        if (issues.Count != 0) return Results.Json(new { code = "invalid-unit-review-rules", issues }, statusCode: 422);
        var rules = InvoiceUnitReviewPolicyRules.Parse(policy.RulesJson);
        foreach (var principal in rules.Stages.SelectMany(x => x.Principals).Distinct(StringComparer.Ordinal))
        {
            var person = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, principal, ct);
            if (person is null || !person.Permissions.Contains("procurement-access") || !person.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission))
                return Problem("unit-reviewer-unavailable", "Choose currently eligible named original-unit reviewers for this company.", 422);
        }
        return null;
    }
    private static async Task<IResult> UnitReviewers(string? search, int? offset, int? limit, HttpContext http,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct)) return Results.Forbid();
        var text = (search ?? "").Trim().ToLowerInvariant();
        if (text.Length > 100 || (offset ?? 0) is < 0 or > 100000 || (limit ?? 25) is < 1 or > 50)
            return Problem("unit-reviewer-query-invalid", "Use a bounded name search and page.", 422);
        var query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, DateTimeOffset.UtcNow)
            .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
        var candidateTotal = await query.CountAsync(ct);
        var page = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(offset ?? 0).Take(limit ?? 25)
            .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(ct);
        var items = new List<object>();
        foreach (var row in page)
        {
            var person = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, row.Id.ToString("D"), ct);
            items.Add(new { principalId = row.Id, row.DisplayName, canReviewOriginalUnits = person is not null
                && person.Permissions.Contains("procurement-access") && person.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission) });
        }
        return Results.Ok(new { items, candidateTotal, offset = offset ?? 0, limit = limit ?? 25, isAuthority = false });
    }
}
