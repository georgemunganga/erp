using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    // No registration/default document owner grants. Each operation reauthorizes
    // actual Core actor + record/site, then binds the exact same-save owned revision.
    private sealed class CommercialDocumentUseAccess(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,
        PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision revision):IDocumentUseAccess
    {
        public async Task RequireAsync(DocumentOwner owner,DocumentUseReference reference,DocumentUseOperation operation,
            IReadOnlyList<Guid> versionIds,CancellationToken ct)
        {
            RequireCommercialTransaction(db,core);
            var actor=await PrincipalActor(core,scope,http,auth,PrincipalRfqCommercialDraft.Permission,head.BranchId,true,ct);
            var ev=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,PrincipalRfqCommercialDraft.Permission,head.SourcingEventId,ct);
            if(ev.Status!="Draft"||head.OwnerPrincipalId!=actor.PrincipalId||operation!=DocumentUseOperation.Acquire
                ||owner!=new DocumentOwner("procurement",PrincipalRfqPrepublicationPlan.DocumentOwnerType,head.SourcingEventId)
                ||reference!=new DocumentUseReference(revision.Id,"procurement",PrincipalRfqPrepublicationPlan.DocumentSourceType,
                    head.SourcingEventId,revision.Revision,DocumentUseKind.Review)
                ||db.Entry(revision).State!=EntityState.Added
                ||db.Entry(head).State is not(EntityState.Added or EntityState.Modified))throw new UnauthorizedAccessException();
            var input=PrincipalRfqCommercialDraft.Restore(head,revision);
            var expected=input.Requirements.Select(x=>x.VersionId).Order().ToArray();
            if(expected.Length==0||versionIds.Distinct().Count()!=versionIds.Count||!versionIds.Order().SequenceEqual(expected))
                throw new UnauthorizedAccessException();
            var receipt=db.PrincipalRfqCommercialDraftReceipts.Local.SingleOrDefault(x=>x.DraftRevisionId==revision.Id);
            if(receipt is null||db.Entry(receipt).State!=EntityState.Added)throw new UnauthorizedAccessException();
            PrincipalRfqCommercialDraft.VerifyReceipt(head,revision,receipt);
        }
    }

    /// <summary>Future Prepare composition only, NOT called by ordinary Draft Save.
    /// Caller must use ExecuteWithSourcingDocumentsAsync, load source/current people,
    /// stage a successor owned revision+receipt and verify actual published controls.
    /// It stages/flushes real Core use evidence in that SAME transaction; later
    /// Procurement failure rolls it back. It neither publishes nor returns authority.</summary>
    private static async Task<IReadOnlyList<DocumentUsePin>> StageCommercialRequirementsForReview(
        ProcurementDbContext db,HrmDbContext core,DocumentDbContext documents,DocumentOwnerLifecycleFence fence,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,
        PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision revision,CancellationToken ct)
    {
        RequireCommercialTransaction(db,core);
        var input=PrincipalRfqCommercialDraft.Restore(head,revision);
        if(input.Requirements.Count==0)return Array.Empty<DocumentUsePin>();
        if(documents.Database.CurrentTransaction is null
            ||!ReferenceEquals(documents.Database.GetDbConnection(),db.Database.GetDbConnection())
            ||!ReferenceEquals(documents.Database.CurrentTransaction.GetDbTransaction(),db.Database.CurrentTransaction!.GetDbTransaction()))
            throw new InvalidOperationException("RfqCommercialDocumentOwnerTransactionRequired");
        var owner=new DocumentOwner("procurement",PrincipalRfqPrepublicationPlan.DocumentOwnerType,head.SourcingEventId);
        var reference=new DocumentUseReference(revision.Id,"procurement",PrincipalRfqPrepublicationPlan.DocumentSourceType,
            head.SourcingEventId,revision.Revision,DocumentUseKind.Review);
        var store=await EfDocumentUsePinStore.CreateAsync(documents,scope,
            new CommercialDocumentUseAccess(db,core,scope,http,auth,head,revision),fence,[owner],ct);
        var pins=await store.AcquireAsync(owner,reference,input.Requirements,ct);
        await documents.SaveChangesAsync(ct);
        return pins;
    }
}
