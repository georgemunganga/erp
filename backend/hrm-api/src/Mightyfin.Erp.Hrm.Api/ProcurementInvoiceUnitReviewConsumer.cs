using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    // Same owning matching transaction, current canonical caller, immutable original signer.
    // This supplies only units; historical Finance exposure keeps its separate unavailable owner.
    internal static async Task<InvoiceMappingReview?> RetainedReview(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, HttpContext http, SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals,
        InvoiceMatchMappingVersion mapping, InvoiceMappingReviewContext context, CancellationToken ct)
    {
        var options = http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>();
        if (!options.Enabled || !options.ExecutionDatabaseConfigured) return null;
        if (!await http.RequestServices.GetRequiredService<IProcurementInvoiceUnitReviewSchemaReadiness>().IsReadyAsync(db, ct))
            return InvoiceMappingReview.Unavailable("InvoiceUnitReviewSchemaUnavailable");
        var source = await db.InvoiceUnitReviewSources.SingleOrDefaultAsync(x => x.SupplierInvoiceId == invoice.Id && x.MappingVersionId == mapping.Id, ct);
        if (source is null) return InvoiceMappingReview.Unavailable("OriginalInvoiceUnitReviewRequired");
        if (source.BranchRef != invoice.BranchRef) throw new UnauthorizedAccessException();
        var outcome = await db.InvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x => x.ReviewSourceId == source.Id, ct);
        if (outcome is null || outcome.State != "Verified") return InvoiceMappingReview.Unavailable(outcome?.State ?? "OriginalInvoiceUnitReviewPending");
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct) ?? throw new UnauthorizedAccessException();
        if (actor.PrincipalId != scope.PrincipalId?.ToString("D")) throw new UnauthorizedAccessException();
        _ = await ProcurementExecutionRecordScope.Branches(core, scope, invoice.BranchRef, true, ct);
        var workflow = await Workflow(cases, scope, source, false, ct);
        var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == source.Id).ToArrayAsync(ct);
        var owned = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
        var matching = await db.PolicyVersions.SingleAsync(x => x.Id == source.MatchingPolicyId, ct);
        var review = await db.PolicyVersions.SingleAsync(x => x.Id == source.ReviewPolicyId, ct);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, matching, ct);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, review, ct);
        var votes = await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToArrayAsync(ct);
        var authority = new InvoiceRetainedUnitReviewAuthority(source.Id, 1, outcome.EvidenceJson, outcome.EvidenceSha256,
            workflow, votes, owned.AssignmentWorkers, matching, review);
        var consumer = new InvoiceUnitReviewConsumer(scope.TenantId, scope.LegalEntityId, invoice.BranchRef, scope.SubjectId,
            actor.WorkerId, actor.Permissions, context.At, context.DocumentVersionHashes, scope.WorkLocationId, actor.AllowedBranches, actor.OrganisationWide);
        var units = InvoiceRetainedUnitReview.Verify(authority, invoice, originals, mapping, consumer);
        return new InvoiceMappingReview(true, "Verified", outcome.EvidenceJson, units) { RetainedAuthority = authority, CurrentConsumer = consumer };
    }
}
