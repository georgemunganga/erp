using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProcurementResolvedApprovalStage(PolicyPreviewStage Route,
    IReadOnlyList<ProcurementResolvedApprovalAssignee> Assignees);
internal sealed record ProcurementResolvedApprovalAssignee(string PrincipalId, Guid WorkerId,
    PurchaseRequestAuthorityEvidence Authority);

internal static class ProcurementApprovalAssigneeResolver
{
    internal static async Task<IReadOnlyList<ProcurementResolvedApprovalStage>> ResolveAsync(
        PurchaseRequest request, PolicyEvaluation evaluation, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        var route = ApprovalRoutePreview.FromEvaluation(evaluation,
            request.Lines.Where(x => x.ApprovalStatus != "Cancelled").Select(x => x.Id).ToArray());
        if (!route.HasSingleRoute || route.Lines.Count == 0)
            throw new InvalidOperationException("ApprovalSingleRouteRequired");
        var stages = route.Lines[0].Stages;
        if (stages.Count == 0 || stages.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != stages.Count)
            throw new InvalidOperationException("ApprovalRouteInvalid");
        var maker = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, request.CreatedBy, ct);
        var requester = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, request.RequesterWorkerId, ct);
        if (maker is null || requester is null) throw new InvalidOperationException("ApprovalMakerIdentityUnavailable");
        var requesterManagerId = await core.Workers.AsNoTracking().Where(x => x.Id == request.RequesterWorkerId)
            .Select(x => x.ManagerId).SingleAsync(ct);
        var output = new List<ProcurementResolvedApprovalStage>();
        foreach (var stage in stages)
        {
            if (stage.Mode is not ("One" or "All")) throw new InvalidOperationException("ApprovalModeUnsupported");
            var accounts = new List<ProcurementAuthorityIdentity.CanonicalAccount>();
            if (stage.Assignee == "BudgetOwner") throw new InvalidOperationException("BudgetOwnerMappingRequired");
            if (stage.Assignee == "LineManager" && requesterManagerId is { } managerId)
            {
                var account = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core,
                    scope.TenantId, scope.LegalEntityId, managerId, ct);
                if (account is not null) accounts.Add(account);
            }
            else if (stage.Assignee == "ProcurementManager")
            {
                var candidates = await core.LocalUsers.AsNoTracking().Where(x => x.IsActive && !x.IsArchived
                    && x.RolesCsv.Contains("procurement_manager")).Select(x => x.Id).ToArrayAsync(ct);
                foreach (var candidate in candidates)
                {
                    var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core,
                        scope.TenantId, scope.LegalEntityId, candidate.ToString("D"), ct);
                    if (account is not null && LocalIdentityRoutes.ParseRoles(account.RolesCsv)
                        .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase)) accounts.Add(account);
                }
            }
            else if (stage.Assignee != "LineManager") throw new InvalidOperationException("ApprovalAssigneeKindUnsupported");
            var eligible = new List<ProcurementResolvedApprovalAssignee>();
            foreach (var account in accounts.DistinctBy(x => x.PrincipalId))
            {
                if (account.PrincipalId == maker.PrincipalId || account.PrincipalId == requester.PrincipalId
                    || account.WorkerId == maker.WorkerId || account.WorkerId == requester.WorkerId) continue;
                var authority = await AuthorityAsync(account, request, core, db, scope, now, ct);
                if (authority is not null) eligible.Add(new(account.PrincipalId, account.WorkerId, authority));
            }
            if (eligible.Count == 0) throw new InvalidOperationException("ApprovalAssigneeAuthorityUnavailable");
            if (stage.Mode == "One" && eligible.Count != 1) throw new InvalidOperationException("ApprovalAssigneeAmbiguous");
            output.Add(new(stage, eligible.OrderBy(x => x.PrincipalId, StringComparer.Ordinal).ToArray()));
        }
        return output;
    }

    internal static async Task ValidateAssignmentAsync(PurchaseRequest request,
        PurchaseRequestAssignmentAuthorityEvidence pin, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, pin.PrincipalId, ct);
        var requester = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core,
            scope.TenantId, scope.LegalEntityId, request.RequesterWorkerId, ct);
        var sourceIdentity = await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pin.SubmissionId, ct);
        if (sourceIdentity is null || !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(sourceIdentity))
            throw new InvalidOperationException("ApprovalHistoricalWorkerEvidenceMissing");
        if (account is null || requester is null || pin.AssigneeWorkerId != account.WorkerId
            || account.WorkerId == sourceIdentity.MakerWorkerId || account.WorkerId == sourceIdentity.RequesterWorkerId || account.PrincipalId == request.CreatedBy
            || account.PrincipalId == requester.PrincipalId) throw new InvalidOperationException("ApprovalIdentityChanged");
        if (pin.AssigneeKind == "LineManager")
        {
            var managerId = await core.Workers.AsNoTracking().Where(x => x.Id == request.RequesterWorkerId)
                .Select(x => x.ManagerId).SingleAsync(ct);
            if (managerId != account.WorkerId) throw new InvalidOperationException("ApprovalManagerChanged");
        }
        else if (pin.AssigneeKind != "ProcurementManager" || !LocalIdentityRoutes.ParseRoles(account.RolesCsv)
            .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("ApprovalAssignmentRoleChanged");
        var current = await AuthorityAsync(account, request, core, db, scope, now, ct);
        if (current is null || current.GrantId != pin.GrantId || current.GrantRevision != pin.GrantRevision
            || current.AuthorityVersion != pin.AuthorityVersion || Hash(pin.EvidenceJson) != pin.EvidenceSha256)
            throw new InvalidOperationException("ApprovalSpendingAuthorityChanged");
        var retained = PurchaseRequestAssignmentIdentity.ReadVerified(pin);
        var evidence = retained?.Authority;
        if (retained is null || retained.AssigneeWorkerId == Guid.Empty || retained.AssigneeWorkerId != pin.AssigneeWorkerId
            || evidence is null || evidence.GrantId != pin.GrantId || evidence.GrantRevision != pin.GrantRevision
            || evidence.AuthorityVersion != pin.AuthorityVersion || evidence.PrincipalId != pin.PrincipalId)
            throw new InvalidOperationException("ApprovalSpendingAuthorityEvidenceInvalid");
    }

    private static async Task<PurchaseRequestAuthorityEvidence?> AuthorityAsync(
        ProcurementAuthorityIdentity.CanonicalAccount account, PurchaseRequest request, HrmDbContext core,
        ProcurementDbContext db, ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, account.RolesCsv, ct);
        var admin = LocalIdentityRoutes.ParseRoles(account.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase);
        if (!admin && (!actions.Contains("procurement-access") || !actions.Contains(SpendingAuthorityConfiguration.ApprovalPermission)))
            return null;
        var accountId = Guid.Parse(account.PrincipalId);
        var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == accountId && !x.IsArchived)
            .Select(x => x.LocationId).ToArrayAsync(ct);
        if (branches.Length != 0 && (request.BranchRef is null || !branches.Contains(request.BranchRef.Value))) return null;
        var grants = await db.SpendingAuthorityGrants.Include(x => x.Categories)
            .Where(x => x.PrincipalId == account.PrincipalId
                && x.Permission == SpendingAuthorityConfiguration.ApprovalPermission
                && x.Action == SpendingAuthorityConfiguration.ApprovalAction).ToListAsync(ct);
        var result = PurchaseRequestSpendingAuthority.Evaluate(request, account.PrincipalId,
            SpendingAuthorityConfiguration.ApprovalPermission, SpendingAuthorityConfiguration.ApprovalAction,
            PurchaseRequestSpendingAuthority.SupportedAmountBasis, now, grants);
        return result.Eligible ? result.Evidence : null;
    }
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
