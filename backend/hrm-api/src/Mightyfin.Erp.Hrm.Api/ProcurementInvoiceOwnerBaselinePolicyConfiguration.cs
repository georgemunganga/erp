using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult?> CheckInvoiceOwnerBaselineCompanyRules(PolicyVersion policy,
        HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (scope.IsConfined || scope.IsExplicitAllBranches
            || !await ReceivingConfigurationActor(core, scope, permission, ct))
            return Problem("invoice-owner-configuration-denied",
                "Your current account cannot configure invoice history review.", 403);
        var issues = InvoiceProcurementBaselineReviewPolicyRules.Validate(policy.RulesJson);
        if (issues.Count != 0)
            return Results.Json(new { code = "invalid-invoice-owner-rules", issues }, statusCode: 422);
        var rules = InvoiceProcurementBaselineReviewPolicyRules.Parse(policy.RulesJson);
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, rules.ReviewerPrincipalId, ct);
        if (person is null || !person.OrganisationWide
            || !person.Permissions.Contains("procurement-access")
            || !person.Permissions.Contains(InvoiceProcurementBaselineEvidence.ReviewPermission))
            return Problem("invoice-owner-reviewer-unavailable",
                "Choose a current company-wide invoice history reviewer.", 422);
        return null;
    }

    private static async Task<IResult> InvoiceOwnerBaselineReviewers(string? search, int? offset,
        int? limit, HttpContext http, HrmDbContext core, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (scope.IsConfined || scope.IsExplicitAllBranches
            || !await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
            return Results.Forbid();
        var text = (search ?? "").Trim().ToLowerInvariant();
        if (text.Length > 100 || (offset ?? 0) is < 0 or > 100000
            || (limit ?? 25) is < 1 or > 50)
            return Problem("invoice-owner-reviewer-query-invalid",
                "Use a bounded name search and page.", 422);
        var query = ProcurementPrincipalPeople.Candidates(core, scope, DateTimeOffset.UtcNow)
            .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
        var candidateTotal = await query.CountAsync(ct);
        var page = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Skip(offset ?? 0).Take(limit ?? 25)
            .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(ct);
        var items = new List<object>();
        foreach (var row in page)
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, row.Id.ToString("D"), ct);
            items.Add(new { principalId = row.Id, row.DisplayName,
                canReviewInvoiceHistory = person is not null && person.OrganisationWide
                    && person.Permissions.Contains("procurement-access")
                    && person.Permissions.Contains(InvoiceProcurementBaselineEvidence.ReviewPermission) });
        }
        return Results.Ok(new { items, candidateTotal, offset = offset ?? 0,
            limit = limit ?? 25, isAuthority = false });
    }
}
