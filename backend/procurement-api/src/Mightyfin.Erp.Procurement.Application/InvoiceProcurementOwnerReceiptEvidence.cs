using System.Text.Json;
using System.Text;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed response contract for the baseline's own command journal.
/// Recovery returns ResponseJson verbatim after reauthorizing the actor and
/// scoped baseline; a self-consistent SHA alone is not a valid receipt.</summary>
public static class InvoiceProcurementOwnerReceiptEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Fields =
    ["schemaVersion","baselineId","coreCaseId","coreDecisionId","operation","commandKey",
        "coreCaseVersion","coreCaseState","inventorySetSha256"];

    public sealed record Response(int SchemaVersion,Guid BaselineId,Guid CoreCaseId,
        Guid? CoreDecisionId,string Operation,Guid CommandKey,long CoreCaseVersion,
        string CoreCaseState,string InventorySetSha256);

    public static string Encode(Response response)
    {
        Need(response);
        return JsonSerializer.Serialize(response,Json);
    }

    public static Response Verify(InvoiceProcurementOwnerCommandReceipt receipt,
        InvoiceProcurementOwnerBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(baseline);
        if(receipt.SchemaVersion!=2 || receipt.TenantId!=baseline.TenantId
            || receipt.LegalEntityId!=baseline.LegalEntityId || receipt.BaselineId!=baseline.Id
            || receipt.CoreCaseId!=baseline.CoreCaseId || receipt.CommandKey==Guid.Empty
            || !ProcurementPrincipalEvidence.Canonical(receipt.ActorPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(receipt.ActorWorkerAliasId)
            || !Sha(receipt.RequestSha256) || !Sha(receipt.ResponseSha256)
            || Encoding.UTF8.GetByteCount(receipt.ResponseJson) is < 2 or > 65_536
            || ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)!=receipt.ResponseSha256)
            throw new InvalidOperationException("InvoiceOwnerReceiptChanged");
        try
        {
            using var document=JsonDocument.Parse(receipt.ResponseJson);
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object
                || !root.EnumerateObject().Select(x=>x.Name).Order(StringComparer.Ordinal)
                    .SequenceEqual(Fields.Order(StringComparer.Ordinal)))
                throw new InvalidOperationException("InvoiceOwnerReceiptChanged");
            var response=JsonSerializer.Deserialize<Response>(receipt.ResponseJson,Json)
                ?? throw new InvalidOperationException("InvoiceOwnerReceiptChanged");
            Need(response);
            if(response.BaselineId!=receipt.BaselineId || response.CoreCaseId!=receipt.CoreCaseId
                || response.CoreDecisionId!=receipt.CoreDecisionId || response.Operation!=receipt.Operation
                || response.CommandKey!=receipt.CommandKey || response.CoreCaseVersion!=receipt.CoreCaseVersion
                || response.InventorySetSha256!=baseline.InventorySetSha256
                || receipt.Operation=="Open" && (receipt.ResponseStatus!=201
                    || receipt.ActorPrincipalId!=baseline.MakerPrincipalId)
                || receipt.Operation=="Decide" && (receipt.ResponseStatus!=200
                    || !ProcurementPrincipalEvidence.Independent(baseline.MakerPrincipalId,
                        baseline.MakerWorkerAliasId,receipt.ActorPrincipalId,receipt.ActorWorkerAliasId)))
                throw new InvalidOperationException("InvoiceOwnerReceiptChanged");
            return response;
        }
        catch(Exception error) when(error is JsonException or FormatException or InvalidOperationException)
        { throw new InvalidOperationException("InvoiceOwnerReceiptChanged",error); }
    }

    private static void Need(Response response)
    {
        if(response.SchemaVersion!=1 || response.BaselineId==Guid.Empty
            || response.CoreCaseId==Guid.Empty || response.CommandKey==Guid.Empty
            || response.CoreCaseVersion<1 || !Sha(response.InventorySetSha256)
            || response.Operation is not ("Open" or "Decide")
            || response.Operation=="Open" && (response.CoreDecisionId is not null
                || response.CoreCaseState!="Pending")
            || response.Operation=="Decide" && ((response.CoreDecisionId is null || response.CoreDecisionId==Guid.Empty)
                || response.CoreCaseState is not ("Approved" or "Rejected" or "Returned")))
            throw new InvalidOperationException("InvoiceOwnerReceiptChanged");
    }

    private static bool Sha(string? value)=>value is {Length:64}
        && value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
