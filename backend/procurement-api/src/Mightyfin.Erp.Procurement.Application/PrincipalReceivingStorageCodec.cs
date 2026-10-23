using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Owned text envelopes, not HTTP trust. Row bindings and full ledger validation
/// are mandatory after decode. Text storage preserves original digest bytes.</summary>
public static class PrincipalReceivingStorageCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict, MaxDepth = 24
    };
    public static string Encode<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, Options);
        Check(json); return json;
    }
    public static T Decode<T>(string json, string sha256) where T : class
    {
        Check(json);
        if (ProcurementEvidenceEncoding.Digest(json) != sha256)
            throw new InvalidOperationException("PrincipalReceivingStoredEvidenceDigestInvalid");
        try { return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("PrincipalReceivingStoredEvidenceMissing"); }
        catch (JsonException error) { throw new InvalidOperationException("PrincipalReceivingStoredEvidenceMalformed", error); }
    }
    private static void Check(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1_048_576 || Encoding.UTF8.GetByteCount(json) > 1_048_576)
            throw new InvalidOperationException("PrincipalReceivingStoredEvidenceSizeInvalid");
        // Reuse the strict duplicate-property/numeric/depth checker; do not invent a
        // second last-property-wins JSON interpretation at the persistence boundary.
        if (ProcurementEvidenceEncoding.PolicyJsonDigest(json).Length != 64)
            throw new InvalidOperationException("PrincipalReceivingStoredEvidenceMalformed");
    }
    public static PrincipalReceivingSource Source(PrincipalReceivingSourceState state)
    {
        var source=Decode<PrincipalReceivingSource>(state.SourceJson,state.SourceSha256);
        PrincipalControlledReceiving.VerifySource(source);var pin=source.Commercial;
        if(state.SchemaVersion!=2 || state.TenantId!=pin.TenantId || state.LegalEntityId!=pin.LegalEntityId
            || state.BranchId!=pin.BranchId || state.PurchaseOrderId!=pin.PurchaseOrderId
            || state.PurchaseOrderLineId!=pin.PurchaseOrderLineId || state.PurchaseRequestLineId!=pin.PurchaseRequestLineId
            || state.IssuedVersionId!=pin.IssuedVersionId || state.SubmissionId!=source.SubmissionId
            || state.FundingEvidenceId!=source.FundingEvidenceId || state.AcknowledgmentId!=source.SupplierAcknowledgmentId
            || state.SourceBindingId!=source.SourceBindingId || state.OriginalSourceSha256!=source.OriginalDirectSourceSha256
            || state.IssuedSnapshotSha256!=pin.IssuedSnapshotSha256 || state.CategoryCode!=pin.CategoryCode
            || state.PurchaseType!=pin.PurchaseType || state.Kind!=pin.Kind.ToString() || state.CurrencyCode!=pin.CurrencyCode)
            throw new InvalidOperationException("PrincipalReceivingSourceBindingInvalid");
        return source;
    }
    public static PrincipalReceivingLedger Ledger(PrincipalReceivingSourceState state,
        IReadOnlyList<PrincipalReceivingFact> facts,IReadOnlyList<PolicyVersion> policies)
    {
        var source=Source(state);var observations=new List<PrincipalGoodsObservation>();
        var inspections=new List<PrincipalGoodsInspection>();var acceptances=new List<PrincipalReceivingAcceptance>();
        foreach(var row in facts.OrderBy(x=>x.Sequence))
        {
            if(row.SchemaVersion!=2 || row.TenantId!=state.TenantId || row.LegalEntityId!=state.LegalEntityId
                || row.SourceStateId!=state.Id || row.BranchId!=state.BranchId)
                throw new InvalidOperationException("PrincipalReceivingFactScopeInvalid");
            switch(row.Kind)
            {
                case "Observation":
                    var observed=Decode<PrincipalGoodsObservation>(row.PayloadJson,row.PayloadSha256);
                    Bind(row,observed.Id,observed.ReceiptId,observed.Source,observed.Sequence,observed.CommandKey,observed.Receiver,observed.PolicyVersionId,observed.ReceivedQuantity,0,source);
                    observations.Add(observed);break;
                case "Inspection":
                    var inspected=Decode<PrincipalGoodsInspection>(row.PayloadJson,row.PayloadSha256);
                    // Inspection DraftId must be the owning observation's receipt, not
                    // an unrelated visible draft chosen by the caller.
                    var owner=observations.SingleOrDefault(x=>x.Id==inspected.ObservationId)
                        ??throw new InvalidOperationException("PrincipalReceivingObservationRequired");
                    Bind(row,inspected.Id,owner.ReceiptId,inspected.Source,inspected.Sequence,inspected.CommandKey,inspected.Inspector,inspected.PolicyVersionId,inspected.QualifiedQuantity,0,source);
                    inspections.Add(inspected);break;
                case "Acceptance":
                    var accepted=Decode<PrincipalReceivingAcceptance>(row.PayloadJson,row.PayloadSha256);
                    var acceptedDraft=accepted.ObservationId is {} observedId
                        ?observations.SingleOrDefault(x=>x.Id==observedId)?.ReceiptId??throw new InvalidOperationException("PrincipalReceivingObservationRequired")
                        :accepted.AcceptanceRecordId;
                    Bind(row,accepted.Id,acceptedDraft,accepted.Source,accepted.Sequence,accepted.CommandKey,accepted.Actor,accepted.PolicyVersionId,accepted.Quantity,accepted.NetAmount,source);
                    acceptances.Add(accepted);break;
                default:throw new InvalidOperationException("PrincipalReceivingFactKindInvalid");
            }
        }
        var ledger=new PrincipalReceivingLedger(source,state.Sequence,observations,inspections,acceptances,policies);
        var totals=PrincipalControlledReceiving.Balance(ledger);
        if(state.AcceptedQuantity!=totals.AcceptedQuantity || state.AcceptedNetAmount!=totals.AcceptedNetAmount)
            throw new InvalidOperationException("PrincipalReceivingStoredBalanceUnknown");
        return ledger;
    }
    private static void Bind(PrincipalReceivingFact row,Guid id,Guid draft,PrincipalReceivingSource source,
        long sequence,Guid key,PrincipalReceivingPerson actor,Guid policy,decimal quantity,decimal amount,PrincipalReceivingSource expected)
    {
        if(row.Id!=id || row.DraftId!=draft || source!=expected || row.Sequence!=sequence || row.CommandKey!=key
            || row.ActorPrincipalId!=actor.PrincipalId || row.ActorWorkerAliasId!=actor.WorkerAliasId
            || row.PolicyVersionId!=policy || row.Quantity!=quantity || row.NetAmount!=amount)
            throw new InvalidOperationException("PrincipalReceivingFactBindingInvalid");
    }
}
