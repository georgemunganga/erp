using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    internal static ApprovalAssignment[] ActivePublicationAssignments(ApprovalCase approval, string principal) =>
        approval.Stages.Where(stage => stage.State == "Active")
            .SelectMany(stage => stage.Assignments)
            .Where(assignment => assignment.AssigneeSubjectId == principal
                && assignment.State == "Pending"
                && assignment.PermissionKey == PrincipalRfqPublicationCoreWorkflow.Permission
                && assignment.DelegatedFromSubjectId is null)
            .ToArray();

    /// <summary>Current actor's own active Core assignments, joined to RFQ scope
    /// in SQL BEFORE counting and paging. No company-wide case scan or worker lookup.</summary>
    private static Task<IResult> PrincipalPublicationAssigned(int? page, int? pageSize,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return PrincipalPublicationDisabled(http);
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var paging = ProcurementSourcingDiscoveryQueries.Paging(page, pageSize, null);
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqPublicationCoreWorkflow.Permission, null, false, token);
            var allowedBranches = actor.Branches.ToArray();
            var selectedBranch = actor.SelectedBranchId;
            var workflow = cases.Cases.FromSqlInterpolated($"""
                SELECT c.* FROM core_approval.cases c
                JOIN procurement.sourcing_events e ON e.id=c."SourceId"
                    AND e.tenant_id=c."TenantId" AND e.legal_entity_id=c."LegalEntityId"
                WHERE c."TenantId"={scope.TenantId} AND c."LegalEntityId"={scope.LegalEntityId}
                    AND c."SourceModule"='procurement' AND c."SourceType"='sourcing_publication'
                    AND c."State"='Pending' AND e.execution_version=2
                    AND e.status='PublicationReview' AND e.approval_instance_ref=c."Id"::text
                    AND ({selectedBranch}::uuid IS NULL OR e.branch_ref={selectedBranch}::uuid)
                    AND ({actor.OrganisationWide} OR e.branch_ref=ANY({allowedBranches}))
                    AND EXISTS (
                        SELECT 1 FROM core_approval.stages stage
                        JOIN core_approval.assignments assignment
                            ON assignment."StageId"=stage."Id"
                            AND assignment."TenantId"=stage."TenantId"
                            AND assignment."LegalEntityId"=stage."LegalEntityId"
                        WHERE stage."CaseId"=c."Id" AND stage."TenantId"=c."TenantId"
                            AND stage."LegalEntityId"=c."LegalEntityId"
                            AND stage."State"='Active' AND assignment."State"='Pending'
                            AND assignment."CaseId"=c."Id"
                            AND assignment."AssigneeSubjectId"={actor.PrincipalId}
                            AND assignment."PermissionKey"='procurement-sourcing-publish'
                            AND assignment."DelegatedFromSubjectId" IS NULL)
                """).AsNoTracking();
            var total = await workflow.CountAsync(token);
            var rows = await workflow.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items = new List<object>();
            foreach (var approval in rows)
            {
                var ev = await db.SourcingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == approval.SourceId && x.ExecutionVersion == 2, token);
                if (ev is null) throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
                try
                {
                    var verified = await PrincipalPublicationEvidence(db, cases, ev, token);
                    if (verified.Case.Id != approval.Id)
                        throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
                    var pending = ActivePublicationAssignments(verified.Case, actor.PrincipalId);
                    if (pending.Length == 0)
                        throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
                    items.Add(new { eventId = ev.Id, ev.Number, ev.Title, ev.CurrencyCode,
                        ev.Status, eventVersion = ev.Version, caseId = approval.Id,
                        caseVersion = verified.Case.Version,
                        assignments = pending.Select(x => new { x.Id, x.Version, x.State }).ToArray(),
                        state = "VerifiedAssignedReview" });
                }
                catch (Exception error) when (IsDiscoveryEvidenceFailure(error))
                {
                    items.Add(new { eventId = ev.Id, caseId = approval.Id,
                        state = "Unavailable", blocker = "SourcingPublicationEvidenceNeedsReview" });
                }
            }
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new { items, total,
                page = paging.Number, pageSize = paging.Size, scope = "CurrentCoreAssignment" }, Json));
        }, ct);
    }

    /// <summary>Minimal supplier selector for the original RFQ Bidding step. It
    /// requires a currently owned Draft; no vendor-admin read grant is inferred.</summary>
    private static Task<IResult> PrincipalPublicationSuppliers(Guid id, string? q, int? page,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return PrincipalPublicationDisabled(http);
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var search = q?.Trim() ?? "";
            var number = page ?? 1;
            if (search.Length is < 2 or > 100 || search.Any(char.IsControl)
                || number is < 1 or > 20)
                throw new Problem("SourcingSupplierSearchInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", id, token);
            if (ev.Status != "Draft") throw new Problem("SourcingDraftChanged", 409);
            var now = DateTimeOffset.UtcNow;
            var escaped = search.Replace("\\", "\\\\").Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            var candidates = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                .Where(x => x.Status == "Active" && x.IsOrderable
                    && (x.EligibleFrom == null || x.EligibleFrom <= now)
                    && (x.EligibleUntil == null || x.EligibleUntil >= now)
                    && (x.PaymentVerificationToken == null || x.PaymentVerificationStatus == "Verified")
                    && (EF.Functions.ILike(x.DisplayName, pattern, "\\")
                        || EF.Functions.ILike(x.Number, pattern, "\\")))
                .OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Take(501).ToArrayAsync(token);
            if (candidates.Length > 500)
                throw new Problem("SourcingSupplierSearchTooBroad", 422);
            var eligible = PrincipalPublicationSupplierSelection.Page(candidates, now, number);
            var items = eligible.Items
                .Select(x => new { supplierId = x.Id, x.DisplayName, x.Number }).ToArray();
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new { items, total = eligible.Total,
                page = number, pageSize = 25, eligibilityMeaning = "CurrentApprovedPreviewOnly",
                eventId = id }, Json));
        }, ct);
    }
}
