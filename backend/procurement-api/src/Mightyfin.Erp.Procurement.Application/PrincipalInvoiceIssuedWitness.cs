using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Host projection of the existing retained-issued/Core owner loader,
/// obtained inside the same shared approval transaction. This record is not an
/// HTTP input and its construction alone does not prove Core approval.</summary>
public sealed record PrincipalInvoiceIssuedWitness(
    string TenantId, Guid LegalEntityId, Guid BranchId,
    Guid PurchaseOrderId, Guid IssuedVersionId, Guid CoreApprovalCaseId,
    Guid SupplierId, string CurrencyCode,
    IReadOnlyDictionary<Guid, PrincipalReceivingSource> Sources);
