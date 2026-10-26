using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>The M5 envelope is separate from retained M3b Create/Cancel bytes.
/// A self-consistent hash alone does not bind a receipt to an order or operation.</summary>
public static class PrincipalOrderGovernanceReceiptEncoding
{
    public sealed record Result(int SchemaVersion,string Operation,Guid OrderId,string Number,string Status,
        long Version,string OriginalDirectSourceSha256,Guid CommandKey,Guid EvidenceId,
        bool RecordedHistoricalResult,bool ManualFundingOnly,bool FinanceReservationChanged,bool SupplierSendPerformed);
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false};
    private static readonly HashSet<string> Fields=new(StringComparer.Ordinal)
    {
        "schemaVersion","operation","orderId","number","status","version","originalDirectSourceSha256",
        "commandKey","evidenceId","recordedHistoricalResult","manualFundingOnly","financeReservationChanged","supplierSendPerformed"
    };
    public static int StatusCode(string operation)=>operation switch
    {
        "Submit" or "Issue"=>201,
        "Funding" or "Decide" or "Ack" or "ReleaseRequest" or "ReleaseConfirm"=>200,
        _=>throw Invalid()
    };
    public static string Encode(PurchaseOrder order,string operation,Guid key,long version,Guid evidenceId)
    {
        var result=new Result(2,operation,order.Id,order.Number,order.Status,version,
            order.OriginalDirectSourceSha256??"",key,evidenceId,true,true,false,false);
        RequireResult(result,order,operation,key,version);
        return JsonSerializer.Serialize(result,Json);
    }
    public static Result Verify(PrincipalOrderDraftReceipt receipt,PurchaseOrder order,string operation,Guid key)
    {
        if(receipt.SchemaVersion!=2 || receipt.Operation!=operation || receipt.CommandKey!=key
            || receipt.PurchaseOrderId!=order.Id || receipt.TenantId!=order.TenantId
            || receipt.LegalEntityId!=order.LegalEntityId || receipt.ResponseStatus!=StatusCode(operation)
            || receipt.ResultVersion<1 || receipt.ResponseJson.Length>65536
            || receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))throw Invalid();
        try
        {
            using var document=JsonDocument.Parse(receipt.ResponseJson,new JsonDocumentOptions{MaxDepth=8});
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw Invalid();
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var field in document.RootElement.EnumerateObject())
                if(!Fields.Contains(field.Name)||!seen.Add(field.Name))throw Invalid();
            if(seen.Count!=Fields.Count)throw Invalid();
            var result=JsonSerializer.Deserialize<Result>(receipt.ResponseJson,Json)??throw Invalid();
            RequireResult(result,order,operation,key,receipt.ResultVersion);
            return result;
        }
        catch(Exception error) when(error is JsonException or ArgumentException or FormatException)
        {throw Invalid();}
    }
    private static void RequireResult(Result value,PurchaseOrder order,string operation,Guid key,long version)
    {
        _=StatusCode(operation);
        var stateMatches=operation switch
        {
            "Funding"=>value.Status=="Draft", "Submit"=>value.Status=="PendingApproval",
            "Decide"=>value.Status is "PendingApproval" or "Approved" or "Returned" or "Rejected",
            "Issue"=>value.Status=="IssuePending", "Ack"=>value.Status is "Issued" or "Disputed",
            "ReleaseRequest"=>value.Status=="ReleasePending", "ReleaseConfirm"=>value.Status=="Cancelled",
            _=>false
        };
        if(value.SchemaVersion!=2 || value.Operation!=operation || !stateMatches || order.PrincipalDraftSchemaVersion!=2
            || value.OrderId!=order.Id || order.Id==Guid.Empty || value.Number!=order.Number
            || value.Version!=version || version<1 || value.CommandKey!=key || key==Guid.Empty
            || value.EvidenceId==Guid.Empty || value.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || value.OriginalDirectSourceSha256 is not {Length:64} || !value.RecordedHistoricalResult
            || !value.ManualFundingOnly || value.FinanceReservationChanged || value.SupplierSendPerformed)throw Invalid();
    }
    private static InvalidOperationException Invalid()=>new("PrincipalOrderReceiptBindingInvalid");
}
