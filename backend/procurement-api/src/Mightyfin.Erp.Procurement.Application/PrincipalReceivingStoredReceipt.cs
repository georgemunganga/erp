using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Historical producer evidence, not current authorization. A fact may only
/// consume its exact original same-command scoped receipt and original result bytes.</summary>
public static class PrincipalReceivingStoredReceipt
{
    public static void Verify(PrincipalReceivingFact fact,PrincipalReceivingCommandReceipt? receipt,PrincipalReceivingSource source)
    {
        var operation=fact.Kind switch{"Observation"=>"Record","Inspection"=>"Inspect","Acceptance"=>source.Commercial.Kind==ReceivingKind.Service?"Certify":"Accept",_=>""};
        if(receipt is null||operation.Length==0||fact.Operation!=operation||receipt.SchemaVersion!=2||fact.SchemaVersion!=2
            ||receipt.TenantId!=fact.TenantId||receipt.LegalEntityId!=fact.LegalEntityId||receipt.DraftId!=fact.DraftId
            ||receipt.BranchId!=fact.BranchId||receipt.ActorPrincipalId!=fact.ActorPrincipalId||receipt.CommandKey!=fact.CommandKey
            ||receipt.Operation!=operation||receipt.ResponseStatus!=200||receipt.ResultRevision<=0||fact.CommandKey==Guid.Empty
            ||receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)||!Sha(receipt.RequestSha256)
            ||fact.TenantId!=source.Commercial.TenantId||fact.LegalEntityId!=source.Commercial.LegalEntityId||fact.BranchId!=source.Commercial.BranchId
            ||string.IsNullOrWhiteSpace(receipt.ResponseJson)||receipt.ResponseJson.Length>1_048_576
            ||System.Text.Encoding.UTF8.GetByteCount(receipt.ResponseJson)>1_048_576||ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson).Length!=64)
            throw new InvalidOperationException("PrincipalReceivingOriginalCommandReceiptRequired");
        try
        {
            using var document=JsonDocument.Parse(receipt.ResponseJson);var result=document.RootElement;
            if(result.GetProperty("id").GetGuid()!=fact.DraftId||result.GetProperty("version").GetInt64()!=receipt.ResultRevision
                ||result.GetProperty("executionVersion").GetInt32()!=2||result.GetProperty("kind").GetString()!=source.Commercial.Kind.ToString()
                ||result.GetProperty("purchaseOrderId").GetGuid()!=source.Commercial.PurchaseOrderId
                ||result.GetProperty("issuedVersionId").GetGuid()!=source.Commercial.IssuedVersionId
                ||result.GetProperty("status").GetString()!=(operation=="Certify"?"Certified":"Recorded"))
                throw new InvalidOperationException("PrincipalReceivingOriginalCommandResultMismatch");
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or FormatException)
        {throw new InvalidOperationException("PrincipalReceivingOriginalCommandResultMalformed",error);}
    }
    private static bool Sha(string value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
