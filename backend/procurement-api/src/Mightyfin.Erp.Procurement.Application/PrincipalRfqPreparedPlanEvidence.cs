using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Structural stored-evidence validation, not a current policy, Core or
/// document owner authority. Only actual owner composition stages these records.</summary>
public static class PrincipalRfqPreparedPlanEvidence
{
    public static PrincipalRfqPreparedPlan Create(PrincipalRfqPrepublicationPlan candidate,PrincipalRfqPublishedPlanPolicy policy,
        PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,RfqPrincipalActor actor,
        DateTimeOffset at,int documentCount,Guid? useSetId,string? selectionDigest)
    {
        ArgumentNullException.ThrowIfNull(candidate);ArgumentNullException.ThrowIfNull(policy);ArgumentNullException.ThrowIfNull(actor);
        _=PrincipalRfqCommercialDraft.Restore(head,row);
        if(candidate.CanPublish||candidate.EventId!=head.SourcingEventId||candidate.Revision!=head.Revision
            ||candidate.OriginalSourceFingerprint!=head.OriginalFingerprint||actor.PrincipalId!=head.OwnerPrincipalId
            ||actor.TenantId!=head.TenantId||actor.CompanyId!=head.LegalEntityId||policy.OriginalFingerprint!=head.OriginalFingerprint
            ||policy.ObservedAt!=at||documentCount is <0 or >100||policy.RequirementsPackRequired&&documentCount==0
            ||(documentCount==0?useSetId is not null||selectionDigest is not null:useSetId is null||useSetId==Guid.Empty||!Hash(selectionDigest)))
            throw new InvalidOperationException("RfqPreparedPlanEvidenceInvalid");
        using var sourceJson=JsonDocument.Parse(candidate.SnapshotJson);
        var envelope=JsonSerializer.Serialize(new{schemaVersion=1,candidateSnapshotSha256=candidate.SnapshotSha256,
            candidateSnapshot=sourceJson.RootElement,documentUseSetId=useSetId,documentSelectionDigest=selectionDigest,documentCount});
        var result=new PrincipalRfqPreparedPlan{TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            SourcingEventId=head.SourcingEventId,DraftId=head.Id,DraftRevisionId=row.Id,SourceEvidenceId=head.SourceEvidenceId,
            BranchId=head.BranchId,DraftRevision=row.Revision,EventVersion=candidate.EventVersion,
            ActorPrincipalId=actor.PrincipalId,ActorWorkerAliasId=actor.LinkedWorkerId,DraftSnapshotSha256=row.SnapshotSha256,
            PolicyVersionId=policy.PolicyId,PublishedPolicyIdentityId=policy.PublishedIdentityId,PolicyRulesSha256=policy.RulesSha256,
            PublishedIdentitySha256=policy.PublishedIdentitySha256,RequirementsPackRequired=policy.RequirementsPackRequired,
            DocumentCount=documentCount,DocumentUseKey=row.Id,DocumentUseSetId=useSetId,DocumentSelectionDigest=selectionDigest,
            EvaluationRulesSha256=candidate.EvaluationRulesSha256,SnapshotJson=envelope,SnapshotSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(envelope),PreparedAt=at};
        Verify(result,head,row);return result;
    }
    public static void Verify(PrincipalRfqPreparedPlan plan,PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(head);ArgumentNullException.ThrowIfNull(row);
        // No current policy/availability is inferred from stored self-consistency.
        if(plan.SchemaVersion!=1||plan.SourcingEventId!=head.SourcingEventId||plan.DraftId!=head.Id||plan.DraftRevisionId!=row.Id
            ||plan.SourceEvidenceId!=head.SourceEvidenceId||plan.TenantId!=head.TenantId||plan.LegalEntityId!=head.LegalEntityId
            ||plan.BranchId!=head.BranchId||plan.DraftRevision!=row.Revision||plan.DraftSnapshotSha256!=row.SnapshotSha256
            ||plan.ActorPrincipalId!=head.OwnerPrincipalId||plan.DocumentUseKey!=row.Id||plan.EventVersion<1
            ||plan.PolicyVersionId==Guid.Empty||plan.PublishedPolicyIdentityId==Guid.Empty||plan.PublishedStage!="Published"
            ||!Hash(plan.PolicyRulesSha256)||!Hash(plan.PublishedIdentitySha256)||!Hash(plan.EvaluationRulesSha256)
            ||plan.DocumentCount is <0 or >100||plan.RequirementsPackRequired&&plan.DocumentCount==0
            ||(plan.DocumentCount==0?plan.DocumentUseSetId is not null||plan.DocumentSelectionDigest is not null:
                plan.DocumentUseSetId is null||plan.DocumentUseSetId==Guid.Empty||!Hash(plan.DocumentSelectionDigest))
            ||plan.PreparedAt==default||plan.PreparedAt.Offset!=TimeSpan.Zero||plan.PreparedAt.UtcTicks%10!=0
            ||plan.SnapshotJson is not{Length:>0 and <=1000000}||plan.SnapshotSha256!=ProcurementEvidenceEncoding.PolicyJsonDigest(plan.SnapshotJson))
            throw new InvalidOperationException("RfqPreparedPlanEvidenceChanged");
        using var json=JsonDocument.Parse(plan.SnapshotJson);var envelope=json.RootElement;
        var root=envelope.GetProperty("candidateSnapshot");
        if(envelope.GetProperty("schemaVersion").GetInt32()!=1
            ||envelope.GetProperty("candidateSnapshotSha256").GetString()!=ProcurementEvidenceEncoding.PolicyJsonDigest(root.GetRawText())
            ||(envelope.GetProperty("documentUseSetId").ValueKind==JsonValueKind.Null?(Guid?)null:envelope.GetProperty("documentUseSetId").GetGuid())!=plan.DocumentUseSetId
            ||envelope.GetProperty("documentSelectionDigest").GetString()!=plan.DocumentSelectionDigest
            ||envelope.GetProperty("documentCount").GetInt32()!=plan.DocumentCount)
            throw new InvalidOperationException("RfqPreparedPlanDocumentBindingChanged");
        var policy=root.GetProperty("publishedPolicy");var use=root.GetProperty("documentReviewUse");
        if(root.GetProperty("eventId").GetGuid()!=plan.SourcingEventId||root.GetProperty("TenantId").GetString()!=plan.TenantId
            ||root.GetProperty("CompanyId").GetGuid()!=plan.LegalEntityId||root.GetProperty("BranchId").GetGuid()!=plan.BranchId
            ||root.GetProperty("eventVersion").GetInt64()!=plan.EventVersion||root.GetProperty("revision").GetInt64()!=plan.DraftRevision
            ||root.GetProperty("originalFingerprint").GetString()!=head.OriginalFingerprint||root.GetProperty("sourceEvidenceId").GetGuid()!=head.SourceEvidenceId
            ||root.GetProperty("sourceEnvelopeSha256").GetString()!=head.SourceEnvelopeSha256||root.GetProperty("MakerPrincipalId").GetString()!=plan.ActorPrincipalId
            ||root.GetProperty("preparedAt").GetDateTimeOffset()!=plan.PreparedAt||root.GetProperty("requirements").GetArrayLength()!=plan.DocumentCount
            ||root.GetProperty("evaluation").GetProperty("rulesSha256").GetString()!=plan.EvaluationRulesSha256
            ||policy.GetProperty("PolicyId").GetGuid()!=plan.PolicyVersionId||policy.GetProperty("PublishedIdentityId").GetGuid()!=plan.PublishedPolicyIdentityId
            ||policy.GetProperty("RulesSha256").GetString()!=plan.PolicyRulesSha256||policy.GetProperty("PublishedIdentitySha256").GetString()!=plan.PublishedIdentitySha256
            ||policy.GetProperty("RequirementsPackRequired").GetBoolean()!=plan.RequirementsPackRequired
            ||use.GetProperty("Module").GetString()!="procurement"||use.GetProperty("RecordType").GetString()!=PrincipalRfqPrepublicationPlan.DocumentSourceType
            ||use.GetProperty("Key").GetGuid()!=row.Id||use.GetProperty("RecordId").GetGuid()!=head.SourcingEventId
            ||use.GetProperty("Revision").GetInt64()!=row.Revision||use.GetProperty("Kind").GetInt32()!=1)
            throw new InvalidOperationException("RfqPreparedPlanSnapshotBindingChanged");
    }
    public static string Response(PrincipalRfqPreparedPlan plan,Guid key)=>JsonSerializer.Serialize(new{preparedPlanId=plan.Id,
        eventId=plan.SourcingEventId,draftId=plan.DraftId,draftRevision=plan.DraftRevision,eventVersion=plan.EventVersion,
        status="PreparedCandidate",canPublish=false,snapshotSha256=plan.SnapshotSha256,commandKey=key});
    public static void VerifyReceipt(PrincipalRfqPreparedPlan plan,PrincipalRfqPreparationReceipt receipt,PrincipalRfqCommercialDraftReceipt draftReceipt)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(receipt);ArgumentNullException.ThrowIfNull(draftReceipt);
        if(receipt.SchemaVersion!=1||receipt.PreparedPlanId!=plan.Id||receipt.DraftRevisionId!=plan.DraftRevisionId
            ||receipt.DraftCommandReceiptId!=draftReceipt.Id||receipt.TenantId!=plan.TenantId||receipt.LegalEntityId!=plan.LegalEntityId
            ||receipt.SourcingEventId!=plan.SourcingEventId||receipt.BranchId!=plan.BranchId||receipt.ActorPrincipalId!=plan.ActorPrincipalId
            ||receipt.SnapshotSha256!=plan.SnapshotSha256||receipt.CommandKey!=draftReceipt.CommandKey||receipt.CommandKey==Guid.Empty
            ||receipt.RequestSha256!=draftReceipt.RequestSha256||draftReceipt.DraftRevisionId!=plan.DraftRevisionId||draftReceipt.Operation!="Revise"
            ||receipt.ResponseJson!=Response(plan,receipt.CommandKey)||receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("RfqPreparationReceiptChanged");
    }
    private static bool Hash(string? value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
