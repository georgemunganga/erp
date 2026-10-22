using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static Task<IResult> Tasks(int? page, int? pageSize, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var p = page ?? 1; var size = pageSize ?? 20;
            if (p < 1 || p > 100000 || size < 1 || size > 50) throw new ReviewProblem("InvoiceUnitReviewPageInvalid", 400);
            var actor = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token) ?? throw new UnauthorizedAccessException();
            if (scope.WorkerId != actor.WorkerId || !actor.Permissions.Contains("procurement-access") || !actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                || !(await auth.AuthorizeAsync(http.User, null, InvoiceRetainedUnitReview.ReviewPermission)).Succeeded) throw new UnauthorizedAccessException();
            var branches = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, token);
            // Immutable source branch and current invoice must agree BEFORE count/paging.
            // Current actual Core assignment and its retained canonical worker must both match.
            var query = db.InvoiceUnitReviewSources.FromSqlInterpolated($@"
                SELECT s.* FROM procurement.invoice_unit_review_sources s
                JOIN procurement.supplier_invoices i ON i.tenant_id=s.tenant_id AND i.legal_entity_id=s.legal_entity_id AND i.id=s.supplier_invoice_id
                    AND i.branch_ref IS NOT DISTINCT FROM s.branch_ref
                WHERE s.tenant_id={scope.TenantId} AND s.legal_entity_id={scope.LegalEntityId}
                AND EXISTS (SELECT 1 FROM core_approval.cases c
                    JOIN core_approval.stages g ON g.""CaseId""=c.""Id"" AND g.""TenantId""=c.""TenantId"" AND g.""LegalEntityId""=c.""LegalEntityId""
                    JOIN core_approval.assignments a ON a.""StageId""=g.""Id"" AND a.""CaseId""=c.""Id"" AND a.""TenantId""=c.""TenantId"" AND a.""LegalEntityId""=c.""LegalEntityId""
                    JOIN procurement.invoice_unit_review_assignments w ON w.tenant_id=s.tenant_id AND w.legal_entity_id=s.legal_entity_id AND w.review_source_id=s.id AND w.core_case_id=c.""Id"" AND w.core_assignment_id=a.""Id""
                    WHERE c.""Id""=s.core_case_id AND c.""TenantId""=s.tenant_id AND c.""LegalEntityId""=s.legal_entity_id
                    AND c.""SourceModule""='procurement' AND c.""SourceType""='invoice_unit_review' AND c.""SourceId""=s.id AND c.""SourceVersion""=1
                    AND c.""State""='Pending' AND g.""State""='Active' AND a.""State""='Pending'
                    AND a.""PermissionKey""={InvoiceRetainedUnitReview.ReviewPermission} AND a.""AssigneeSubjectId""={scope.SubjectId}
                    AND w.principal_id={scope.SubjectId} AND w.worker_id={actor.WorkerId})").AsNoTracking();
            if (scope.WorkLocationId is { } selected) query = query.Where(x => x.BranchRef == selected);
            if (branches.Length != 0) query = query.Where(x => x.BranchRef != null && branches.Contains(x.BranchRef.Value));
            var total = await query.CountAsync(token);
            var rows = await query.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id).Skip((p - 1) * size).Take(size)
                .Select(x => new { reviewId = x.Id, invoiceId = x.SupplierInvoiceId, mappingVersionId = x.MappingVersionId, x.MappingRevision,
                    branchId = x.BranchRef, x.OpenedAt, canPostToFinance = false, isInvoiceApproval = false }).ToArrayAsync(token);
            return InvoiceUnitReviewCommands.Serialize(new { items = rows, total, page = p, pageSize = size, canPostToFinance = false });
        }, ct);

    private static Task<IResult> Detail(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (source, invoice) = await Source(db, scope, id, false, token);
            var actor = await Actor(core, scope, http, auth, source.BranchRef,
                [InvoiceRetainedUnitReview.ReviewPermission, InvoiceUnitReviewTransitions.RequestPermission, InvoiceUnitReviewTransitions.ReadPermission], token);
            var workflow = await Workflow(cases, scope, source, false, token);
            var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            var owned = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
            var maker = actor.PrincipalId == source.MakerPrincipalId && actor.WorkerId == source.MakerWorkerId
                && actor.Permissions.Contains(InvoiceUnitReviewTransitions.RequestPermission) && actor.Permissions.Contains("procurement-invoice-read");
            var assigned = actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                && pins.Any(x => x.PrincipalId == actor.PrincipalId && x.WorkerId == actor.WorkerId);
            var reader = actor.Permissions.Contains(InvoiceUnitReviewTransitions.ReadPermission) && actor.Permissions.Contains("procurement-invoice-read");
            if (!maker && !assigned && !reader) throw new UnauthorizedAccessException();
            var outcome = await db.InvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x => x.ReviewSourceId == id, token);
            var currentMapping = invoice.CurrentMappingVersionId == source.MappingVersionId && invoice.MappingRevision == source.MappingRevision;
            var assignments = workflow.Stages.Where(x => currentMapping && workflow.State == "Pending" && x.State == "Active").SelectMany(x => x.Assignments)
                .Where(x => x.State == "Pending" && x.AssigneeSubjectId == actor.PrincipalId && pins.Any(w => w.CoreAssignmentId == x.Id && w.WorkerId == actor.WorkerId))
                .Select(x => new { assignmentId = x.Id, assignmentVersion = x.Version }).ToArray();
            return InvoiceUnitReviewCommands.Serialize(new { reviewId = id, invoiceId = invoice.Id, invoiceVersion = invoice.Version,
                mappingVersionId = source.MappingVersionId, source.MappingRevision, coreCaseId = workflow.Id, caseVersion = workflow.Version,
                caseState = workflow.State, reviewState = outcome?.State ?? "Pending", currentMapping, assignments,
                originalDocumentLines = owned.Source.Lines, attestation = InvoiceUnitReviewCommands.OriginalUnitAttestation,
                actionCandidates = assignments.Length == 0 ? Array.Empty<string>() : new[] { "Approve", "Return", "Reject" },
                actionCandidateMeaning = "CurrentAssignmentOnlyRevalidatedByCommand", isInvoiceApproval = false, canPostToFinance = false });
        }, ct);

    private static Task<IResult> Recover(Guid id, Guid key, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            if (key == Guid.Empty) throw new ReviewProblem("InvoiceUnitReviewCommandKeyRequired", 400);
            var (source, invoice) = await Source(db, scope, id, false, token);
            var receipt = await db.InvoiceUnitReviewReceipts.SingleOrDefaultAsync(x => x.ReviewSourceId == id && x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, token) ?? throw new KeyNotFoundException();
            var action = receipt.Operation == "Open" ? InvoiceUnitReviewTransitions.RequestPermission : InvoiceRetainedUnitReview.ReviewPermission;
            var actor = await Actor(core, scope, http, auth, source.BranchRef, [action], token);
            if (receipt.ActorWorkerId != actor.WorkerId) throw new UnauthorizedAccessException();
            if (receipt.Operation == "Open" && (!actor.Permissions.Contains("procurement-invoice-read") || !(await auth.AuthorizeAsync(http.User, null, "procurement-invoice-read")).Succeeded)) throw new UnauthorizedAccessException();
            var request = InvoiceUnitReviewCommands.Read<InvoiceUnitReviewRequest>(receipt.RequestJson);
            if (request.InvoiceId != invoice.Id || request.Operation == "Decision" && request.ReviewId != id
                || receipt.Fingerprint != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.RequestJson)) throw new ReviewProblem("InvoiceUnitReviewReceiptInvalid", 409);
            // Actor-owned historical acknowledgement only; no fresh decision or authorization is inferred.
            var response = InvoiceUnitReviewCommands.Read<InvoiceUnitReviewResponse>(receipt.ResponseJson);
            if (response.ReviewId != id || response.InvoiceId != invoice.Id || response.CommandKey != key || !response.RecordedHistoricalResult || response.CanPostToFinance || response.IsInvoiceApproval)
                throw new ReviewProblem("InvoiceUnitReviewReceiptInvalid", 409);
            return receipt.ResponseJson;
        }, ct);
}
