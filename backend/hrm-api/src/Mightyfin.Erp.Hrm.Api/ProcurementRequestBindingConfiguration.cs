using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult> RequestDepartments(HttpContext http, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
            return Problem("request-configuration-denied", "You cannot configure request approvals for this company.", 403);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await core.OrgUnits.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived && x.Status == "active"
            && x.EffectiveFrom <= today && (x.EffectiveTo == null || x.EffectiveTo >= today))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new { id = x.Id, label = x.Name }).Take(501).ToArrayAsync(ct);
        if (rows.Length > 500) return Problem("request-departments-too-many", "The department catalogue is too large for this selector.", 422);
        return Results.Ok(new { items = rows, isAuthority = false });
    }

    private static async Task<IResult> RequestRoles(HttpContext http, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
            return Problem("request-configuration-denied", "You cannot configure request approvals for this company.", 403);
        var rows = await core.TenantRoleAssignments.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.Active && !x.IsArchived).OrderBy(x => x.RoleName).ThenBy(x => x.RoleKey)
            .Select(x => new { key = x.RoleKey, label = x.RoleName }).Take(501).ToArrayAsync(ct);
        if (rows.Length > 500) return Problem("request-roles-too-many", "Narrow the configured Core role catalogue before selecting request roles.", 422);
        return Results.Ok(new { items = rows, isAuthority = false });
    }

    private static async Task<IResult> RequestApprovers(string? search, int? offset, int? limit, string? principalIds,
        HttpContext http, HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
            return Problem("request-configuration-denied", "You cannot configure request approvals for this company.", 403);
        var text = (search ?? "").Trim().ToLowerInvariant();
        if (text.Length > 100) return Problem("request-search-invalid", "Search by a name of up to 100 characters.", 422);
        var skip = Math.Clamp(offset ?? 0, 0, 100000); var take = Math.Clamp(limit ?? 25, 1, 50);
        Guid[]? selected = null;
        if (http.Request.Query.ContainsKey("principalIds"))
        {
            var values = (principalIds ?? "").Split(',');
            if (values.Length is < 1 or > 25 || text.Length != 0 || skip != 0
                || values.Any(x => !new PurchaseRequestApproverBinding("CoreUser", x).IsValid)
                || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                return Problem("request-selection-invalid", "Choose up to 25 distinct saved approvers without another search.", 422);
            selected = values.Select(Guid.Parse).ToArray();
        }
        var query = ProcurementPrincipalPeople.Candidates(core, scope, DateTimeOffset.UtcNow)
            .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
        if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
        var candidates = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.DisplayName }).Take(501).ToArrayAsync(ct);
        if (candidates.Length > 500) return Problem("request-search-too-broad", "Search by a more specific name.", 422);
        var eligible = new List<object>();
        foreach (var row in candidates)
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, row.Id.ToString("D"), ct);
            if (person is not null && person.Permissions.Contains(SpendingAuthorityConfiguration.ApprovalPermission))
                eligible.Add(new { principalId = person.PrincipalId, row.DisplayName });
        }
        return Results.Ok(new { items = eligible.Skip(skip).Take(take), total = eligible.Count,
            nextOffset = skip + take < eligible.Count ? (int?)(skip + take) : null, isAuthority = false });
    }

    private static async Task<IResult?> CheckRequestBindingCompanyRules(JsonElement root,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        var editor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (editor is null || !editor.OrganisationWide)
            return Problem("request-policy-scope", "Company-wide request rules require company-wide configuration scope.", 403);
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in root.GetProperty("rules").EnumerateArray())
        {
            if (rule.GetProperty("when").TryGetProperty("departmentIds", out var departments))
                foreach (var department in departments.EnumerateArray())
                {
                    var id = department.GetGuid(); var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    if (!await core.OrgUnits.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                        && x.Id == id && !x.IsArchived && x.Status == "active" && x.EffectiveFrom <= today && (x.EffectiveTo == null || x.EffectiveTo >= today), ct))
                        return Problem("request-department-unavailable", "Choose active departments in this company.", 422);
                }
            foreach (var role in rule.GetProperty("when").GetProperty("requesterRoleKeys").EnumerateArray())
                roles.Add(role.GetString()!);
            foreach (var stage in rule.GetProperty("result").GetProperty("approvalStages").EnumerateArray())
            {
                var binding = PurchaseRequestApproverBinding.ReadStage(stage, required: true)!;
                if (binding.Kind == "CoreRole") roles.Add(binding.Key);
                else
                {
                    var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, binding.Key, ct);
                    if (person is null || !person.Permissions.Contains(SpendingAuthorityConfiguration.ApprovalPermission))
                        return Problem("request-approver-unavailable", "Choose an active Core user with purchase approval access in this company.", 422);
                    var branches = rule.GetProperty("when").GetProperty("branchIds").EnumerateArray().Select(x => x.GetGuid()).ToArray();
                    if (!person.OrganisationWide && (branches.Length == 0 || branches.Any(x => !person.AllowedBranches.Contains(x))))
                        return Problem("request-approver-scope", "This approver cannot cover all branches selected by the rule.", 422);
                }
            }
        }
        var active = await core.TenantRoleAssignments.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.Active && !x.IsArchived && roles.Contains(x.RoleKey)).Select(x => x.RoleKey).ToArrayAsync(ct);
        if (roles.Except(active, StringComparer.Ordinal).Any())
            return Problem("request-role-unavailable", "Choose active Core roles before reviewing these rules.", 422);
        // Role membership, current action/scope and finite monetary authority are checked
        // again at execution. A registered role or configuration preview is never a grant.
        return null;
    }
}
