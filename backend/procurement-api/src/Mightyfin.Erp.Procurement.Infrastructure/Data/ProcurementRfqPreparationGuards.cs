using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;
public sealed partial class ProcurementDbContext
{
    private DocumentDbContext? rfqDocumentOwner;
    private readonly HashSet<Guid> rfqInsertedUseSets=[];
    internal void BindRfqDocumentOwner(DocumentDbContext documents)
    {
        if(rfqDocumentOwner is not null||Database.CurrentTransaction is null||documents.Database.CurrentTransaction is null
            ||!ReferenceEquals(Database.GetDbConnection(),documents.Database.GetDbConnection())
            ||!ReferenceEquals(Database.CurrentTransaction.GetDbTransaction(),documents.Database.CurrentTransaction.GetDbTransaction()))
            throw new InvalidOperationException("RfqPreparationDocumentOwnerMismatch");
        rfqDocumentOwner=documents;Guid[] pending=[];
        documents.SavingChanges+=(_,_)=>pending=documents.ChangeTracker.Entries<DocumentUseSetRow>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity.Id).ToArray();
        documents.SavedChanges+=(_,_)=>{rfqInsertedUseSets.UnionWith(pending);pending=[];};
        documents.SaveChangesFailed+=(_,_)=>pending=[];
    }
    private void GuardRfqPreparation()
    {
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is PrincipalRfqPreparedPlan or PrincipalRfqPreparationReceipt))
        {
            if(entry.State is EntityState.Unchanged or EntityState.Detached)continue;
            if(entry.Entity.TenantId!=scope.TenantId||entry.Entity.LegalEntityId!=scope.LegalEntityId)throw new InvalidOperationException("RfqPreparationScopeMismatch");
            if(entry.State!=EntityState.Added)throw new InvalidOperationException("RfqPreparationAppendOnly");
        }
        var plans=ChangeTracker.Entries<PrincipalRfqPreparedPlan>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var receipts=ChangeTracker.Entries<PrincipalRfqPreparationReceipt>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        foreach(var plan in plans)
        {
            var head=PrincipalRfqCommercialDrafts.Local.SingleOrDefault(x=>x.Id==plan.DraftId)??throw new InvalidOperationException("RfqPreparationOwnedDraftRequired");
            var row=PrincipalRfqCommercialDraftRevisions.Local.SingleOrDefault(x=>x.Id==plan.DraftRevisionId)??throw new InvalidOperationException("RfqPreparationOwnedRevisionRequired");
            var command=PrincipalRfqCommercialDraftReceipts.Local.SingleOrDefault(x=>x.DraftRevisionId==row.Id)??throw new InvalidOperationException("RfqPreparationOriginalCommandRequired");
            if(Entry(row).State!=EntityState.Added||Entry(command).State!=EntityState.Added||plan.ActorPrincipalId!=scope.SubjectId
                ||plan.ActorWorkerAliasId!=scope.CoreLinkedWorkerId||receipts.Count(x=>x.PreparedPlanId==plan.Id)!=1)
                throw new InvalidOperationException("RfqPreparationSameCommandRequired");
            PrincipalRfqPreparedPlanEvidence.Verify(plan,head,row);
            PrincipalRfqPreparedPlanEvidence.VerifyReceipt(plan,receipts.Single(x=>x.PreparedPlanId==plan.Id),command);
            var ev=SourcingEvents.Local.Single(x=>x.Id==plan.SourcingEventId);
            if(ev.Status!="Draft"||ev.Version!=plan.EventVersion)throw new InvalidOperationException("RfqPreparationEventVersionChanged");
            var policy=PolicyVersions.Local.Single(x=>x.Id==plan.PolicyVersionId);
            var identity=SourcingPolicyIdentityEvidenceRecords.Local.Where(x=>x.PolicyVersionId==policy.Id).ToArray();
            var published=identity.Single(x=>x.Id==plan.PublishedPolicyIdentityId);
            SourcingPolicyIdentity.VerifyPublication(policy,identity.Single(x=>x.Id==published.CreatedEvidenceId),identity.Single(x=>x.Id==published.SubmittedEvidenceId),published);
            if(published.EvidenceSha256!=plan.PublishedIdentitySha256||published.RulesSha256!=plan.PolicyRulesSha256
                ||policy.EffectiveFrom>plan.PreparedAt||policy.EffectiveTo is {} end&&plan.PreparedAt>=end)
                throw new InvalidOperationException("RfqPreparationPublishedPolicyChanged");
            var input=PrincipalRfqCommercialDraft.Restore(head,row);
            if(input.Requirements.Count!=plan.DocumentCount)throw new InvalidOperationException("RfqPreparationDocumentSetChanged");
            if(plan.DocumentCount>0)
            {
                var documents=rfqDocumentOwner;
                if(documents is null||Database.CurrentTransaction is null||documents.Database.CurrentTransaction is null
                    ||!ReferenceEquals(Database.GetDbConnection(),documents.Database.GetDbConnection())
                    ||!ReferenceEquals(Database.CurrentTransaction.GetDbTransaction(),documents.Database.CurrentTransaction.GetDbTransaction())
                    ||!rfqInsertedUseSets.Contains(plan.DocumentUseSetId!.Value))
                    throw new InvalidOperationException("RfqPreparationSavedOwningDocumentUseRequired");
                var set=documents.UseSets.Local.Single(x=>x.Id==plan.DocumentUseSetId);
                var pins=documents.UsePins.Local.Where(x=>x.UseSetId==set.Id).ToArray();
                if(set.TenantId!=scope.TenantId||set.LegalEntityId!=scope.LegalEntityId||set.UseKey!=row.Id
                    ||set.SourceModule!="procurement"||set.SourceRecordType!=PrincipalRfqPrepublicationPlan.DocumentSourceType
                    ||set.SourceRecordId!=plan.SourcingEventId||set.SourceRevision!=row.Revision||set.Kind!=Mightyfin.Erp.Core.Documents.Domain.DocumentUseKind.Review
                    ||set.OwnerModule!="procurement"||set.OwnerType!=PrincipalRfqPrepublicationPlan.DocumentOwnerType||set.OwnerId!=plan.SourcingEventId
                    ||set.CreatedBy!=scope.SubjectId||set.SelectionDigest!=plan.DocumentSelectionDigest||set.SelectionCount!=plan.DocumentCount||pins.Length!=plan.DocumentCount
                    ||input.Requirements.Any(selection=>pins.Count(pin=>pin.VersionId==selection.VersionId&&pin.DocumentId==selection.DocumentId
                        &&pin.Sha256==selection.Sha256&&pin.DocumentRevision==selection.DocumentRevision&&pin.VersionRevision==selection.VersionRevision)!=1))
                    throw new InvalidOperationException("RfqPreparationExactOwningDocumentSetRequired");
                using var snapshot=System.Text.Json.JsonDocument.Parse(plan.SnapshotJson);
                var retained=snapshot.RootElement.GetProperty("candidateSnapshot").GetProperty("requirements").EnumerateArray().ToArray();
                if(retained.Length!=pins.Length||retained.Any(expected=>pins.Count(pin=>pin.Id==expected.GetProperty("Id").GetGuid()
                    &&pin.DocumentId==expected.GetProperty("DocumentId").GetGuid()&&pin.VersionId==expected.GetProperty("VersionId").GetGuid()
                    &&pin.Sha256==expected.GetProperty("Sha256").GetString()&&pin.DocumentRevision==expected.GetProperty("DocumentRevision").GetInt64()
                    &&pin.VersionRevision==expected.GetProperty("VersionRevision").GetInt64()
                    &&pin.ScanEvidenceId==expected.GetProperty("scan").GetProperty("Id").GetGuid())!=1))
                    throw new InvalidOperationException("RfqPreparationStoredScanSnapshotChanged");
            }
        }
        if(receipts.Length!=plans.Length)throw new InvalidOperationException("RfqPreparationOrphanReceiptDenied");
    }
}
