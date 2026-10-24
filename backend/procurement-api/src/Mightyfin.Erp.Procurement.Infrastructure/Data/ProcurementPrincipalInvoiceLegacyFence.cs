using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Current owning-store fence, after the caller has authorized this invoice.
    /// A partial P31 installation throws instead of treating an absent table as no evidence.</summary>
    public bool HasPrincipalInvoiceWorkflow(Guid invoiceId)
    {
        if (PrincipalInvoiceStates.Local.Any(x => x.SupplierInvoiceId == invoiceId
            && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)) return true;
        if (!ProcurementPrincipalInvoiceSchema.Installed(this)) return false;
        return PrincipalInvoiceStates.IgnoreQueryFilters().Any(x => x.SupplierInvoiceId == invoiceId
            && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
    }

    private void GuardLegacyInvoiceAgainstPrincipalWorkflow()
    {
        var changed = ChangeTracker.Entries<ProcurementRecord>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var invoiceIds = changed.Select(x => x.Entity switch
        {
            SupplierInvoice invoice when x.State != EntityState.Added => invoice.Id,
            SupplierInvoiceLine line => line.SupplierInvoiceId,
            InvoiceMatchMappingVersion mapping => mapping.SupplierInvoiceId,
            InvoiceMatchResult result => result.SupplierInvoiceId,
            InvoiceMatchAllocation allocation => allocation.SupplierInvoiceId,
            InvoiceException issue => issue.SupplierInvoiceId,
            FinanceInvoiceHandoff handoff => handoff.SupplierInvoiceId,
            InvoiceUnitReviewSource review => review.SupplierInvoiceId,
            _ => Guid.Empty,
        }).Where(x => x != Guid.Empty).ToHashSet();
        var reviewIds = changed.Select(x => x.Entity switch
        {
            InvoiceUnitReviewAssignmentEvidence assignment => assignment.ReviewSourceId,
            InvoiceUnitReviewOutcome outcome => outcome.ReviewSourceId,
            InvoiceUnitReviewCommandReceipt receipt => receipt.ReviewSourceId,
            _ => Guid.Empty,
        }).Where(x => x != Guid.Empty).Distinct().ToArray();
        foreach (var reviewId in reviewIds)
        {
            var source = InvoiceUnitReviewSources.Local.SingleOrDefault(x => x.Id == reviewId)
                ?? InvoiceUnitReviewSources.SingleOrDefault(x => x.Id == reviewId);
            if (source is not null) invoiceIds.Add(source.SupplierInvoiceId);
        }
        foreach (var invoiceId in invoiceIds)
            if (HasPrincipalInvoiceWorkflow(invoiceId))
                throw new InvalidOperationException("PrincipalInvoiceLegacyWorkflowUnavailable");
    }
}
