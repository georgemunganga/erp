using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Only the exact M5b supporting unique indexes may be excluded from
/// predecessor index-count fences. Every admitted index is still inspected physically.</summary>
internal static class ProcurementPrincipalReceivingParentIndexes
{
    private sealed record Contract(Type Type, string Name, string[] Properties);
    private static readonly Contract[] Contracts =
    [
        new(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_parent", ["TenantId","LegalEntityId","Id","PurchaseOrderId","SubmissionId","FundingEvidenceId","CommercialSnapshotSha256","OriginalDirectSourceSha256"]),
        new(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_order", ["TenantId","LegalEntityId","Id","PurchaseOrderId"]),
        new(typeof(PrincipalOrderAcknowledgment), "ux_prcv_ack_parent", ["TenantId","LegalEntityId","Id","IssuedVersionId","PurchaseOrderId"]),
        new(typeof(PrincipalOrderSourceBinding), "ux_prcv_original_parent", ["TenantId","LegalEntityId","Id","PurchaseOrderId","PurchaseOrderLineId","PurchaseRequestLineId","BranchId","OriginalDirectSourceSha256"]),
        new(typeof(PurchaseOrderLine), "ux_prcv_po_line_parent", ["TenantId","LegalEntityId","Id","PurchaseOrderId","PurchaseRequestLineId"]),
    ];
    internal static IEnumerable<Type> ParentTypes => Contracts.Select(x => x.Type).Distinct();
    internal static int ExpectedCount(Type type) => Contracts.Count(x => x.Type == type);
    internal static bool Matches(Type type, string? name, IEnumerable<string> properties, bool unique, string? filter) =>
        unique && filter is null && Contracts.Any(x => x.Type == type && x.Name == name
            && x.Properties.SequenceEqual(properties, StringComparer.Ordinal));
    internal static bool IsOwned(IIndex index) => Matches(index.DeclaringEntityType.ClrType,
        index.GetDatabaseName(), index.Properties.Select(x => x.Name), index.IsUnique, index.GetFilter());
}
