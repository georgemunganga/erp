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

    private sealed class RetainedRequirementsAccess(ProcurementRequestScope scope,
        PrincipalRfqPreparedPlan plan, PrincipalRfqCommercialDraftHead head,
        PrincipalRfqCommercialDraftRevision revision,
        IReadOnlyDictionary<Guid, string> expected,
        bool currentPurposeAuthorized) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference reference,
            DocumentUseOperation operation, IReadOnlyList<Guid> versionIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.TenantId != plan.TenantId || scope.LegalEntityId != plan.LegalEntityId
                || scope.SubjectId != plan.ActorPrincipalId && !currentPurposeAuthorized
                || plan.SourcingEventId != head.SourcingEventId
                || plan.DraftRevisionId != revision.Id || plan.DocumentUseKey != revision.Id
                || owner != new DocumentOwner("procurement", PrincipalRfqPrepublicationPlan.DocumentOwnerType,
                    plan.SourcingEventId)
                || reference != new DocumentUseReference(revision.Id, "procurement",
                    PrincipalRfqPrepublicationPlan.DocumentSourceType, plan.SourcingEventId,
                    revision.Revision, DocumentUseKind.Review)
                || operation != DocumentUseOperation.Read
                || versionIds.Count > 0 && !versionIds.Order().SequenceEqual(expected.Keys.Order()))
                throw new UnauthorizedAccessException("RfqCommercialRetainedDocumentUseDenied");
            return Task.CompletedTask;
        }
    }

    private static async Task RequireRetainedCommercialRequirementsAsync(
        ProcurementDbContext db, DocumentDbContext documents, ProcurementRequestScope scope,
        PrincipalRfqPreparedPlan plan, PrincipalRfqCommercialDraftHead head,
        PrincipalRfqCommercialDraftRevision revision, CancellationToken ct,
        bool currentPurposeAuthorized = false)
    {
        PrincipalRfqPreparedPlanEvidence.Verify(plan, head, revision);
        var draft = PrincipalRfqCommercialDraft.Restore(head, revision);
        var expected = draft.Requirements.ToDictionary(x => x.VersionId, x => x.Sha256);
        if (expected.Count != plan.DocumentCount || expected.Count > 100
            || plan.DocumentCount == 0 && plan.DocumentUseSetId is not null)
            throw new InvalidOperationException("RfqCommercialRetainedDocumentUseChanged");
        if (expected.Count == 0) return;
        if (documents.Database.CurrentTransaction is null || db.Database.CurrentTransaction is null
            || !ReferenceEquals(documents.Database.GetDbConnection(), db.Database.GetDbConnection())
            || !ReferenceEquals(documents.Database.CurrentTransaction.GetDbTransaction(),
                db.Database.CurrentTransaction.GetDbTransaction()))
            throw new InvalidOperationException("RfqCommercialDocumentOwnerTransactionRequired");
        var owner = new DocumentOwner("procurement", PrincipalRfqPrepublicationPlan.DocumentOwnerType,
            plan.SourcingEventId);
        var source = new DocumentUseReference(revision.Id, "procurement",
            PrincipalRfqPrepublicationPlan.DocumentSourceType, plan.SourcingEventId,
            revision.Revision, DocumentUseKind.Review);
        var pins = await EfDocumentUsePinStore.ReadRetainedAsync(documents, scope,
            new RetainedRequirementsAccess(scope, plan, head, revision, expected,
                currentPurposeAuthorized),
            owner, source, ct);
        var set = await documents.UseSets.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == plan.DocumentUseSetId && x.OwnerId == plan.SourcingEventId
            && x.TenantId == plan.TenantId && x.LegalEntityId == plan.LegalEntityId, ct);
        if (set is null || set.SelectionDigest != plan.DocumentSelectionDigest
            || set.SelectionCount != plan.DocumentCount || pins.Count != plan.DocumentCount
            || pins.Any(x => !expected.TryGetValue(x.VersionId, out var sha)
                || x.Sha256 != sha || x.OwnerId != plan.SourcingEventId))
            throw new InvalidOperationException("RfqCommercialRetainedDocumentUseChanged");
    }

    private sealed class OfferOriginalUseAccess(ProcurementRequestScope scope,
        SourcingEvent ev, SupplierBid bid, RfqPrincipalActor actor,
        Guid commandKey, Guid versionId) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference source,
            DocumentUseOperation operation, IReadOnlyList<Guid> versionIds,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.SubjectId != actor.PrincipalId || scope.TenantId != ev.TenantId
                || scope.LegalEntityId != ev.LegalEntityId
                || actor.PrincipalId != bid.SurrogateEnteredBy
                || !actor.Permissions.Contains(PrincipalRfqSealedManualOffers.SubmitAction)
                || ev.Status != "Published" || bid.Status != "Draft"
                || bid.SourcingEventId != ev.Id
                || owner != new DocumentOwner("procurement", "sourcing_event", ev.Id)
                || source != new DocumentUseReference(commandKey, "procurement",
                    "sourcing_commercial_offer", bid.Id, bid.DraftRevision,
                    DocumentUseKind.Publication)
                || operation != DocumentUseOperation.Acquire
                || versionIds.Count != 1 || versionIds[0] != versionId)
                throw new UnauthorizedAccessException("RfqCommercialOfferDocumentUseDenied");
            return Task.CompletedTask;
        }
    }

    private static async Task<(Guid UseSetId, string SelectionDigest, string Sha256)>
        StageCommercialOfferOriginalUseAsync(ProcurementDbContext db,
            DocumentDbContext documents, DocumentOwnerLifecycleFence fence,
            ProcurementRequestScope scope, SourcingEvent ev, SupplierBid bid,
            RfqPrincipalActor actor, Guid commandKey, Guid versionId,
            CancellationToken ct)
    {
        if (versionId == Guid.Empty || documents.Database.CurrentTransaction is null
            || db.Database.CurrentTransaction is null
            || !ReferenceEquals(documents.Database.GetDbConnection(), db.Database.GetDbConnection())
            || !ReferenceEquals(documents.Database.CurrentTransaction.GetDbTransaction(),
                db.Database.CurrentTransaction.GetDbTransaction()))
            throw new InvalidOperationException("RfqCommercialOfferDocumentOwnerRequired");
        var owner = new DocumentOwner("procurement", "sourcing_event", ev.Id);
        var row = await (from version in documents.Versions.AsNoTracking()
            join document in documents.Documents.AsNoTracking()
                on version.DocumentId equals document.Id
            where version.Id == versionId && version.TenantId == scope.TenantId
                && version.LegalEntityId == scope.LegalEntityId
                && document.TenantId == scope.TenantId
                && document.LegalEntityId == scope.LegalEntityId
                && document.OwnerModule == owner.Module
                && document.OwnerType == owner.Type && document.OwnerId == owner.Id
                && !document.IsRetired && version.ScanState == DocumentScanState.Available
            select new { document, version }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("RfqCommercialOfferOriginalDocumentUnavailable");
        var selection = new DocumentUseSelection(owner.Module, owner.Type, owner.Id,
            row.document.Id, row.version.Id, row.document.Version,
            row.version.Version, row.version.Sha256);
        var reference = new DocumentUseReference(commandKey, "procurement",
            "sourcing_commercial_offer", bid.Id, bid.DraftRevision,
            DocumentUseKind.Publication);
        var store = await EfDocumentUsePinStore.CreateAsync(documents, scope,
            new OfferOriginalUseAccess(scope, ev, bid, actor, commandKey, versionId),
            fence, [owner], ct);
        var pins = await store.AcquireAsync(owner, reference, [selection], ct);
        if (pins.Count != 1 || pins[0].VersionId != versionId
            || pins[0].Sha256 != row.version.Sha256)
            throw new InvalidOperationException("RfqCommercialOfferOriginalDocumentChanged");
        await documents.SaveChangesAsync(ct);
        var set = documents.UseSets.Local.SingleOrDefault(x =>
            x.UseKey == commandKey && x.SourceRecordId == bid.Id);
        if (set is null || set.SelectionCount != 1
            || set.OwnerId != ev.Id || set.CreatedBy != actor.PrincipalId)
            throw new InvalidOperationException("RfqCommercialOfferOriginalDocumentUseChanged");
        return (set.Id, set.SelectionDigest, row.version.Sha256);
    }

    internal sealed class RetainedOfferUseAccess(ProcurementRequestScope scope,
        PrincipalRfqCommercialOfferSubmissionRecord offer, RfqPrincipalActor actor,
        Guid useKey, string requiredAction = PrincipalRfqSealedManualOffers.CloseAction) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference source,
            DocumentUseOperation operation, IReadOnlyList<Guid> versionIds,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.SubjectId != actor.PrincipalId || scope.TenantId != offer.TenantId
                || scope.LegalEntityId != offer.LegalEntityId
                || requiredAction is not (PrincipalRfqSealedManualOffers.CloseAction
                    or PrincipalRfqOfferOpeningControls.OpenAction or PrincipalRfqOfferOpeningControls.ReadAction)
                || !actor.Permissions.Contains(requiredAction)
                || (actor.SelectedBranchId is { } selected && selected != offer.BranchId)
                || (!actor.OrganisationWide && !actor.Branches.Contains(offer.BranchId))
                || owner != new DocumentOwner("procurement", "sourcing_event",
                    offer.SourcingEventId)
                || source != new DocumentUseReference(useKey, "procurement",
                    "sourcing_commercial_offer", offer.SupplierBidId,
                    offer.DraftRevision, DocumentUseKind.Publication)
                || operation != DocumentUseOperation.Read
                // Core ReadRetained authorizes the owner/reference before its
                // lookup with an empty set, then authorizes the restored set.
                || versionIds.Count != 0 && (versionIds.Count != 1
                    || versionIds[0] != offer.OriginalDocumentVersionId))
                throw new UnauthorizedAccessException("RfqCommercialOfferDocumentReadDenied");
            return Task.CompletedTask;
        }
    }

    private static async Task RequireRetainedOfferOriginalUseAsync(
        DocumentDbContext documents, ProcurementRequestScope scope,
        PrincipalRfqCommercialOfferSubmissionRecord offer,
        PrincipalRfqCommercialCommandReceipt originalReceipt,
        RfqPrincipalActor actor, CancellationToken ct,
        string requiredAction = PrincipalRfqSealedManualOffers.CloseAction)
    {
        var owner = new DocumentOwner("procurement", "sourcing_event",
            offer.SourcingEventId);
        var source = new DocumentUseReference(originalReceipt.CommandKey, "procurement",
            "sourcing_commercial_offer", offer.SupplierBidId,
            offer.DraftRevision, DocumentUseKind.Publication);
        var set = await documents.UseSets.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == offer.OriginalDocumentUseSetId && x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId, ct);
        if (set is null || set.OwnerModule != owner.Module || set.OwnerType != owner.Type
            || set.OwnerId != owner.Id || set.SourceModule != source.Module
            || set.SourceRecordType != source.RecordType
            || set.SourceRecordId != source.RecordId
            || set.SourceRevision != source.Revision || set.UseKey != source.Key
            || set.Kind != source.Kind || set.SelectionCount != 1
            || set.SelectionDigest != offer.OriginalDocumentSelectionDigest
            || set.CreatedBy != offer.RecorderPrincipalId)
            throw new InvalidOperationException("RfqCommercialOfferOriginalUseChanged");
        var pins = await EfDocumentUsePinStore.ReadRetainedAsync(documents, scope,
            new RetainedOfferUseAccess(scope, offer, actor, originalReceipt.CommandKey, requiredAction),
            owner, source, ct);
        if (pins.Count != 1 || pins[0].OwnerId != offer.SourcingEventId
            || pins[0].VersionId != offer.OriginalDocumentVersionId
            || pins[0].Sha256 != offer.OriginalDocumentSha256)
            throw new InvalidOperationException("RfqCommercialOfferOriginalUseChanged");
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
