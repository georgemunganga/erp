using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record OrderResolvedAssignee(string PrincipalId, Guid WorkerId, ProcurementSpendingAuthorityEvidence Authority);
internal sealed record OrderResolvedStage(PolicyPreviewStage Route, IReadOnlyList<OrderResolvedAssignee> Assignees);

/// <summary>PO domain chooses eligibility; shared Core executes the resulting tasks. No inferred
/// budget owner, implicit administrative spending limit, or account-alias maker/checker bypass.</summary>
internal static class ProcurementOrderAuthorityResolver
{
    internal static async Task<IReadOnlyList<OrderResolvedStage>> ResolveAsync(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, IReadOnlyList<PolicyPreviewStage> stages, Guid buyerWorker,
        HrmDbContext core, ProcurementDbContext db, ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        if (stages.Count == 0 || stages.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != stages.Count)
            throw new InvalidOperationException("OrderApprovalRouteInvalid");
        var workers = PurchaseOrderWorkerIdentity.Participants(lines, buyerWorker);
        var ids = await core.LocalUsers.AsNoTracking().Where(x => x.IsActive && !x.IsArchived
            && x.RolesCsv.Contains("procurement_manager")).Select(x => x.Id).ToArrayAsync(ct);
        var accounts = new List<ProcurementAuthorityIdentity.CanonicalAccount>();
        foreach (var id in ids)
        {
            var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core,
                scope.TenantId, scope.LegalEntityId, id.ToString("D"), ct);
            if (account is not null && LocalIdentityRoutes.ParseRoles(account.RolesCsv)
                .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase)
                && PurchaseOrderWorkerIdentity.IsIndependent(workers, account.WorkerId)
                && await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId,
                    scope.LegalEntityId, account.WorkerId, ct) is not null) accounts.Add(account);
        }
        var eligible = new List<OrderResolvedAssignee>();
        foreach (var account in accounts.DistinctBy(x => x.WorkerId))
        {
            var authority = await AuthorityAsync(account, order, lines, core, db, scope, now, ct);
            if (authority is not null) eligible.Add(new(account.PrincipalId, account.WorkerId, authority));
        }
        var output = new List<OrderResolvedStage>();
        foreach (var stage in stages)
        {
            // Multi-department manager and budget-owner routing needs explicit business mapping.
            if (stage.Assignee != "ProcurementManager" || stage.Mode is not ("One" or "All"))
                throw new InvalidOperationException("OrderApprovalAssigneeMappingRequired");
            if (eligible.Count == 0) throw new InvalidOperationException("OrderApprovalAuthorityUnavailable");
            if (stage.Mode == "One" && eligible.Count != 1) throw new InvalidOperationException("OrderApprovalAssigneeAmbiguous");
            output.Add(new(stage, eligible.OrderBy(x => x.PrincipalId, StringComparer.Ordinal).ToArray()));
        }
        return output;
    }

    internal static async Task<ProcurementAuthorityIdentity.CanonicalAccount> ValidateAsync(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, PurchaseOrderSubmission submission,
        PurchaseOrderAssignmentAuthorityEvidence assignment, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        var retained = PurchaseOrderAssignmentIdentity.ReadVerified(assignment);
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core,
            scope.TenantId, scope.LegalEntityId, assignment.PrincipalId, ct);
        if (retained is null || account is null || assignment.SubmissionId != submission.Id
            || assignment.CoreApprovalCaseId != submission.CoreApprovalCaseId || account.WorkerId != retained.AssigneeWorkerId
            || !PurchaseOrderWorkerIdentity.Verify(submission, order, lines)
            || !PurchaseOrderWorkerIdentity.IsIndependent(submission.ParticipantWorkersJson, account.WorkerId)
            || assignment.AssigneeKind != "ProcurementManager" || !LocalIdentityRoutes.ParseRoles(account.RolesCsv)
                .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("OrderApprovalIdentityChanged");
        var current = await AuthorityAsync(account, order, lines, core, db, scope, now, ct, submission.LineBaseAmount);
        if (current is null || current.GrantId != assignment.GrantId || current.GrantRevision != assignment.GrantRevision
            || current.AuthorityVersion != assignment.AuthorityVersion)
            throw new InvalidOperationException("OrderApprovalSpendingAuthorityChanged");
        return account;
    }

    private static async Task<ProcurementSpendingAuthorityEvidence?> AuthorityAsync(
        ProcurementAuthorityIdentity.CanonicalAccount account, PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct, decimal? pinnedAmount = null)
    {
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, account.RolesCsv, ct);
        var admin = LocalIdentityRoutes.ParseRoles(account.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase);
        if (!admin && (!actions.Contains("procurement-access") || !actions.Contains("procurement-po-approve"))) return null;
        var id = Guid.Parse(account.PrincipalId);
        var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == id && !x.IsArchived)
            .Select(x => x.LocationId).ToArrayAsync(ct);
        if (branches.Length != 0 && (order.BranchRef is null || !branches.Contains(order.BranchRef.Value))) return null;
        var grants = await db.SpendingAuthorityGrants.Include(x => x.Categories).Where(x => x.PrincipalId == account.PrincipalId
            && x.Permission == "procurement-po-approve" && x.Action == "PurchaseOrder.Approve").ToListAsync(ct);
        var context = PurchaseOrderCommercialWorkflow.SpendingContext(order, lines);
        if (pinnedAmount is { } originalAmount) context = context with { Amount = originalAmount };
        var result = ProcurementSpendingAuthority.Evaluate(context,
            account.PrincipalId, "procurement-po-approve", "PurchaseOrder.Approve", now, grants);
        return result.Eligible ? result.Evidence : null;
    }
}
