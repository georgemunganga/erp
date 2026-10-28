using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Stage the exact response inside the caller's existing Core and
    /// Procurement transaction. The caller first validates current Core authority
    /// and compares the original key/body; this primitive never makes a decision.</summary>
    public async Task<InvoiceProcurementOwnerCommandReceipt> StageInvoiceOwnerReceiptAsync(
        InvoiceProcurementOwnerReceiptEvidence.Response response,string requestSha256,
        DateTimeOffset at,CancellationToken ct)
    {
        if(!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable
            || scope.IsConfined || scope.IsExplicitAllBranches
            || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            || !ProcurementPrincipalEvidence.OptionalWorker(scope.CoreLinkedWorkerId))
            throw new InvalidOperationException("InvoiceOwnerReceiptSerializableScopeRequired");
        var baseline=InvoiceProcurementOwnerBaselines.Local.SingleOrDefault(x=>x.Id==response.BaselineId)
            ??await InvoiceProcurementOwnerBaselines.SingleAsync(x=>x.Id==response.BaselineId,ct);
        if(stagedInvoiceOwnerReceipts.Any(x=>x.CommandKey==response.CommandKey)
            || await InvoiceProcurementOwnerCommandReceipts.AnyAsync(x=>x.CommandKey==response.CommandKey,ct))
            throw new InvalidOperationException("InvoiceOwnerReceiptCommandKeyAlreadyUsed");
        var json=InvoiceProcurementOwnerReceiptEvidence.Encode(response);
        var row=new InvoiceProcurementOwnerCommandReceipt
        {
            TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            BaselineId=baseline.Id,CoreCaseId=response.CoreCaseId,CoreDecisionId=response.CoreDecisionId,
            Operation=response.Operation,CommandKey=response.CommandKey,ActorPrincipalId=scope.SubjectId,
            ActorWorkerAliasId=scope.CoreLinkedWorkerId,RequestSha256=requestSha256,
            ResponseJson=json,ResponseSha256=ProcurementEvidenceEncoding.Digest(json),
            ResponseStatus=response.Operation=="Open"?201:200,CoreCaseVersion=response.CoreCaseVersion,
            CreatedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(at)
                ??throw new InvalidOperationException("InvoiceOwnerReceiptTimeInvalid"),
            CreatedBy=scope.SubjectId
        };
        _=InvoiceProcurementOwnerReceiptEvidence.Verify(row,baseline);
        stagedInvoiceOwnerReceipts.Add(row);
        AddScoped(row);
        return row;
    }

    /// <summary>The route authorizes a current read permission before calling.
    /// Historical recovery does not require the old policy window to remain open.</summary>
    public async Task<InvoiceProcurementOwnerCommandReceipt?> RecoverInvoiceOwnerReceiptAsync(
        Guid commandKey,string operation,string? originalRequestSha256,CancellationToken ct)
    {
        if(commandKey==Guid.Empty || operation is not ("Open" or "Decide")
            || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            || scope.IsConfined)
            throw new InvalidOperationException("InvoiceOwnerReceiptReadScopeInvalid");
        var receipt=await InvoiceProcurementOwnerCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.CommandKey==commandKey
                && x.Operation==operation && x.ActorPrincipalId==scope.SubjectId,ct);
        if(receipt is null)return null;
        if(originalRequestSha256 is not null && originalRequestSha256!=receipt.RequestSha256)
            throw new InvalidOperationException("InvoiceOwnerReceiptCommandKeyReused");
        var baseline=await InvoiceProcurementOwnerBaselines.AsNoTracking()
            .SingleAsync(x=>x.Id==receipt.BaselineId,ct);
        _=InvoiceProcurementOwnerReceiptEvidence.Verify(receipt,baseline);
        return receipt;
    }
}
