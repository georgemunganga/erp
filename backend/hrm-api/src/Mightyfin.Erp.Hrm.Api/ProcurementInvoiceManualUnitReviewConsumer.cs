using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static async Task<InvoiceMappingReview> RetainedManualReview(ProcurementDbContext db,
        ApprovalDbContext cases,ProcurementRequestScope scope,HttpContext http,SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals,InvoiceMatchMappingVersion mapping,
        InvoiceUnitReviewSource source,InvoiceMappingReviewContext context,CancellationToken ct)
    {
        if(!http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>().ManualAttestationEnabled)
            return InvoiceMappingReview.Unavailable("InvoiceManualUnitReviewExecutionDisabled");
        if(!await ProcurementInvoiceManualUnitReviewSchemaCapture.IsInstalledAsync(db,ct))
            return InvoiceMappingReview.Unavailable("InvoiceManualUnitReviewSchemaUnavailable");
        if(context.DocumentVersionHashes.Count!=0 || source.BranchRef!=invoice.BranchRef
            || invoice.CurrentMappingVersionId!=mapping.Id || invoice.MappingRevision!=mapping.MappingRevision
            || source.MappingVersionId!=mapping.Id || source.MappingRevision!=mapping.MappingRevision)
            return InvoiceMappingReview.Unavailable("InvoiceManualUnitReviewCurrentMappingRequired");
        RequireManualMapping(mapping,invoice,originals);
        var outcome=await db.InvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x=>x.ReviewSourceId==source.Id,ct);
        if(outcome is null || outcome.State!="Verified")
            return InvoiceMappingReview.Unavailable(outcome?.State??"InvoiceManualUnitReviewPending");
        await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,ct);
        var actor=await ProcurementInvoiceUnitReviewPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct)
            ?? throw new UnauthorizedAccessException();
        if(actor.PrincipalId!=scope.PrincipalId?.ToString("D"))throw new UnauthorizedAccessException();
        _=await ProcurementExecutionRecordScope.Branches(core,scope,invoice.BranchRef,true,ct);
        var workflow=await Workflow(cases,scope,source,false,ct);
        var pins=await db.InvoiceUnitReviewAssignments.Where(x=>x.ReviewSourceId==source.Id).ToArrayAsync(ct);
        var owned=InvoiceUnitReviewStorage.Restore(source,pins,workflow);
        _=RequireManualSource(source);
        var terminal=JsonSerializer.Deserialize<InvoiceManualUnitReviewEnvelope>(outcome.EvidenceJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new InvalidOperationException("InvoiceManualUnitReviewOutcomeInvalid");
        if(outcome.EvidenceSchemaVersion!=4 || terminal.SchemaVersion!=4 || terminal.EvidenceMode!="ManualAttestation"
            || outcome.ReviewSourceId!=source.Id || outcome.CoreCaseId!=workflow.Id || terminal.ReviewId!=source.Id
            || terminal.FinalDecisionId!=outcome.FinalDecisionId || terminal.ReviewerPrincipalId!=outcome.ReviewerPrincipalId
            || terminal.ReviewerWorkerId!=outcome.ReviewerWorkerId || terminal.ReviewedAt!=outcome.CompletedAt)
            throw new InvalidOperationException("InvoiceManualUnitReviewOutcomeInvalid");
        var matching=await db.PolicyVersions.SingleAsync(x=>x.Id==source.MatchingPolicyId,ct);
        var review=await db.PolicyVersions.SingleAsync(x=>x.Id==source.ReviewPolicyId,ct);
        RequireManualPolicy(review);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,http.RequestServices,matching,ct);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,http.RequestServices,review,ct);
        var votes=await cases.Decisions.Where(x=>x.CaseId==workflow.Id).ToArrayAsync(ct);
        var authority=new InvoiceRetainedUnitReviewAuthority(source.Id,1,outcome.EvidenceJson,outcome.EvidenceSha256,
            workflow,votes,owned.AssignmentWorkers,matching,review);
        var consumer=new InvoiceUnitReviewConsumer(scope.TenantId,scope.LegalEntityId,invoice.BranchRef,scope.SubjectId,
            actor.WorkerId,actor.Permissions,context.At,EmptyManualDocumentHashes,scope.WorkLocationId,actor.AllowedBranches,actor.OrganisationWide);
        var units=InvoiceManualUnitReviewEvidence.Verify(authority,owned.Source,invoice,originals,mapping,consumer);
        return new(true,"Verified",outcome.EvidenceJson,units)
            {RetainedAuthority=authority,CurrentConsumer=consumer,ManualSource=owned.Source};
    }
}
