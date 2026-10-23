using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact schema 2 envelope and source pins. Schema 1 is restored by GovernedRfqRestoration.</summary>
public static class GovernedPrincipalRfqRestoration
{
    private sealed record Envelope(int SchemaVersion,Guid CreateCommandKey,RfqPrincipalDraft Draft);
    private static readonly JsonSerializerOptions Json=new()
    {
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
    };
    public static (string Payload,string Sha256) Capture(RfqPrincipalDraft draft,Guid key)
    {
        if(draft.State!="Draft" || key==Guid.Empty)throw new InvalidOperationException("RfqPrincipalCaptureInvalid");
        var payload=JsonSerializer.Serialize(new Envelope(2,key,draft),Json);
        return(payload,ProcurementEvidenceEncoding.Digest(payload));
    }
    public static RfqPrincipalDraft RestoreVerified(SourcingDraftEvidence evidence,SourcingEvent ev,
        IReadOnlyList<SourcingDemandBinding> bindings)
    {
        if(evidence.SchemaVersion!=2 || ev.ExecutionVersion!=2 || bindings.Count is <1 or >200
            || evidence.PayloadJson is not {Length: >0 and <=1000000}
            || evidence.PayloadSha256!=ProcurementEvidenceEncoding.Digest(evidence.PayloadJson))
            throw new InvalidOperationException("RfqPrincipalEnvelopeInvalid");
        using var document=JsonDocument.Parse(evidence.PayloadJson);
        NoDuplicates(document.RootElement);
        var envelope=JsonSerializer.Deserialize<Envelope>(evidence.PayloadJson,Json)
            ?? throw new InvalidOperationException("RfqPrincipalEnvelopeInvalid");
        var draft=envelope.Draft;
        if(envelope.SchemaVersion!=2 || envelope.CreateCommandKey!=evidence.CreateCommandKey
            || envelope.CreateCommandKey==Guid.Empty || draft.State!="Draft"
            || draft.Id!=ev.Id || draft.Id!=evidence.SourcingEventId || draft.TenantId!=ev.TenantId
            || draft.TenantId!=evidence.TenantId || draft.CompanyId!=ev.LegalEntityId
            || draft.CompanyId!=evidence.LegalEntityId || draft.BranchId!=ev.BranchRef
            || draft.BranchId!=evidence.BranchRef || draft.CurrencyCode!=ev.CurrencyCode
            || draft.Title!=ev.Title || draft.MakerPrincipalId!=ev.OwnerSubjectId
            || draft.MakerPrincipalId!=evidence.MakerPrincipalId || draft.MakerPrincipalId!=evidence.MakerSubjectId
            || draft.MakerWorkerAlias!=ev.MakerWorkerId || draft.MakerWorkerAlias!=evidence.MakerWorkerId
            || draft.OriginalFingerprint!=ev.OriginalDraftFingerprint
            || draft.OriginalFingerprint!=evidence.OriginalDraftFingerprint
            || draft.Lines.Count!=bindings.Count || draft.Lines.Select(x=>x.LineId).Distinct().Count()!=bindings.Count
            || !draft.Lines.Select(x=>x.LineId).SequenceEqual(draft.Lines.OrderBy(x=>x.LineId).Select(x=>x.LineId)))
            throw new InvalidOperationException("RfqPrincipalEnvelopeInvalid");
        var recomputed=GovernedPrincipalRfqDraft.NewDraft(draft.Id,draft.TenantId,draft.CompanyId,draft.BranchId,
            draft.CurrencyCode,draft.Title,draft.MakerPrincipalId,draft.MakerWorkerAlias,"Draft",draft.Lines);
        if(recomputed.OriginalFingerprint!=draft.OriginalFingerprint)
            throw new InvalidOperationException("RfqPrincipalFingerprintChanged");
        foreach(var pin in draft.Lines)
        {
            var matches=bindings.Where(x=>x.SourcingLineId==pin.EventLineId && x.PurchaseRequestLineId==pin.LineId).ToArray();
            if(matches.Length!=1 || ev.MethodId!=pin.MethodId || ev.MethodRevision!=pin.MethodRevision
                || ev.Method!=pin.MethodCode || !Matches(matches[0],pin,ev,evidence))
                throw new InvalidOperationException("RfqPrincipalBindingChanged");
        }
        return draft;
    }
    public static void VerifyHistorical(RfqPrincipalDraft draft,IReadOnlyList<ApprovedPurchaseSource> sources)
    {
        if(sources.Count!=draft.Lines.Count)throw new InvalidOperationException("RfqPrincipalHistoryChanged");
        foreach(var pin in draft.Lines)
        {
            var history=sources.Where(x=>x.Line.Id==pin.LineId).ToArray();
            if(history.Length!=1)throw new InvalidOperationException("RfqPrincipalHistoryChanged");
            var source=history[0];var p=source.Submission;
            if(!PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(p)
                || p.FinanceIdentitySchemaVersion!=2 || source.Request.Id!=pin.RequestId
                || p.Id!=pin.SubmissionId || p.SubmittedBusinessVersion!=pin.SubmittedVersion
                || p.CoreApprovalCaseId!=pin.CaseId || source.Outcome.Id!=pin.OutcomeId
                || source.FinalDecision.Id!=pin.DecisionId || p.RequestSnapshotSha256!=pin.RequestSha
                || p.PolicyDecisionSha256!=pin.PolicyDecisionSha || p.PolicyRulesSha256!=pin.PolicyRulesSha
                || p.FinanceSnapshotId!=pin.FinanceId || p.FinanceSnapshotSha256!=pin.FinanceSha
                || p.RequestSnapshotSha256!=ProcurementEvidenceEncoding.Digest(p.RequestSnapshotJson)
                || p.PolicyDecisionSha256!=ProcurementEvidenceEncoding.Digest(source.Evaluation.DecisionJson)
                || p.PolicyRulesSha256!=ProcurementEvidenceEncoding.PolicyJsonDigest(source.Policy.RulesJson)
                || p.FinanceSnapshotSha256!=ProcurementEvidenceEncoding.Digest(p.FinanceSnapshotJson)
                || !FinanceSnapshotCodec.Matches(source.Finance,p.FinanceIdentitySchemaVersion,
                    p.FinanceSnapshotJson,p.FinanceSnapshotSha256)
                || source.Method.Id!=pin.MethodId || source.Method.Version!=pin.MethodRevision
                || source.Method.Code!=pin.MethodCode || p.MakerSubjectId!=pin.MakerPrincipalId
                || p.RequesterPrincipalId?.ToString("D")!=pin.RequesterPrincipalId
                || p.MakerWorkerId!=pin.MakerWorkerAlias || p.RequesterWorkerId!=pin.RequesterWorkerAlias
                || p.PrincipalIdentitySha256!=pin.PrincipalIdentitySha256
                || source.Outcome.Outcome!="Approved" || source.Outcome.SubmissionId!=p.Id
                || source.Outcome.CoreApprovalCaseId!=pin.CaseId || source.Outcome.CoreDecisionId!=pin.DecisionId
                || source.CoreCase.State!="Approved" || source.CoreCase.Id!=pin.CaseId
                || source.CoreCase.SourceModule!="procurement" || source.CoreCase.SourceType!="purchase_request"
                || source.CoreCase.SourceId!=pin.RequestId || source.CoreCase.SourceVersion!=pin.SubmittedVersion
                || source.CoreCase.MakerSubjectId!=pin.MakerPrincipalId || source.CoreCase.CompletedAt is null
                || source.FinalDecision.Action!="Approve" || source.FinalDecision.CaseId!=pin.CaseId
                || source.FinalDecision.ActorSubjectId==pin.MakerPrincipalId
                || source.CoreCase.CompletionKey!=source.FinalDecision.IdempotencyKey
                || source.Outcome.DecisionCommandKey!=source.FinalDecision.IdempotencyKey
                || source.Line.UnitCode!=pin.UnitCode
                || source.Line.CategoryCode!=pin.CategoryCode || source.Line.EstimatedUnitPrice!=pin.UnitEstimate)
                throw new InvalidOperationException("RfqPrincipalHistoryChanged");
        }
    }
    private static bool Matches(SourcingDemandBinding row,RfqPrincipalDemandPin pin,SourcingEvent ev,SourcingDraftEvidence evidence)
        => row.IdentitySchemaVersion==2 && row.SourcingEventId==ev.Id && row.SourceEvidenceId==evidence.Id
            && row.PurchaseRequestId==pin.RequestId && row.RequestVersion==pin.RequestVersion
            && row.SubmissionId==pin.SubmissionId && row.SubmittedVersion==pin.SubmittedVersion
            && row.CoreApprovalCaseId==pin.CaseId && row.ApprovalOutcomeId==pin.OutcomeId
            && row.CoreDecisionId==pin.DecisionId && row.RequestSha==pin.RequestSha
            && row.PolicyDecisionSha==pin.PolicyDecisionSha && row.PolicyRulesSha==pin.PolicyRulesSha
            && row.FinanceSnapshotId==pin.FinanceId && row.FinanceSha==pin.FinanceSha
            && row.MethodId==pin.MethodId && row.MethodRevision==pin.MethodRevision
            && row.MethodCode==pin.MethodCode && row.MinimumQuoteCount==pin.MinimumQuoteCount
            && row.MakerPrincipalId==pin.MakerPrincipalId && row.RequesterPrincipalId==pin.RequesterPrincipalId
            && row.MakerWorkerId==pin.MakerWorkerAlias && row.RequesterWorkerId==pin.RequesterWorkerAlias
            && row.PrincipalIdentitySha256==pin.PrincipalIdentitySha256 && row.AllocationId==pin.AllocationId
            && row.Quantity==pin.Quantity && row.UnitEstimate==pin.UnitEstimate
            && row.EstimatedValue==pin.EstimatedValue && row.UnitCode==pin.UnitCode
            && row.CategoryCode==pin.CategoryCode && row.DepartmentRef==pin.DepartmentId;
    private static void NoDuplicates(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in value.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new InvalidOperationException("RfqPrincipalDuplicateField");
                NoDuplicates(property.Value);
            }
        }
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())NoDuplicates(item);
    }
}
