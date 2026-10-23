using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>One owning-store predicate for correction of structured manual intake before
    /// any document, matching or downstream evidence. It is not a Finance eligibility check.
    /// Callers must separately enforce current permissions, maker/manager, branch scope and CAS.</summary>
    public IQueryable<SupplierInvoice> ManualInvoiceDrafts()
    {
        var query = SupplierInvoices.AsNoTracking().Where(invoice =>
        invoice.Status == "Received" && invoice.IntakeChannel == "Manual"
        && invoice.FinancePostingRef == null && invoice.EvidenceDocumentRefId == null && invoice.CreditForInvoiceRef == null
        && invoice.MappingRevision == 0 && invoice.CurrentMappingVersionId == null && invoice.LastMatchResultId == null
        && invoice.ActiveMatchClaimResultId == null && invoice.MatchRevision == 0 && invoice.MatchingState == "NotCompared"
        && !SupplierInvoiceLines.Any(line => line.SupplierInvoiceId == invoice.Id
            && (line.PurchaseOrderLineId != null || line.GoodsReceiptLineId != null || line.ServiceAcceptanceId != null))
        && !AttachmentRefs.Any(row => row.OwnerId == invoice.Id)
        && !InvoiceMatchMappingVersions.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !InvoiceMatchAllocations.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !InvoiceMatchResults.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !InvoiceExceptions.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !FinanceInvoiceHandoffs.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !FinancePaymentStatusRefs.Any(row => row.SupplierInvoiceId == invoice.Id)
        && !SupplierCreditNoteRefs.Any(row => row.SupplierInvoiceId == invoice.Id));
        return ProcurementManualPaymentSchema.Installed(this)
            ? query.Where(invoice => !ManualPaymentRequests.Any(row => row.SupplierInvoiceId == invoice.Id
                && row.Status != "Cancelled" && row.Status != "Rejected")) : query;
    }

    private bool MayMaintainManualInvoice(Guid id)
    {
        // Persisted evidence is authoritative. Never adopt an attached invoice or cleared
        // navigation/pointer as proof that an original was not already sealed.
        var original = ManualInvoiceDrafts().SingleOrDefault(row => row.Id == id);
        if (original is null) return false;
        if (ProcurementManualPaymentSchema.Installed(this) && ManualPaymentRequests.IgnoreQueryFilters().Any(row =>
            row.TenantId == original.TenantId && row.LegalEntityId == original.LegalEntityId
            && row.SupplierInvoiceId == id && row.Status != "Cancelled" && row.Status != "Rejected")) return false;
        var parent = ChangeTracker.Entries<SupplierInvoice>().SingleOrDefault(row => row.Entity.Id == id);
        // A line-only save could race a first mapping under implicit ReadCommitted. Every
        // correction must participate in the owning invoice's real optimistic CAS write:
        // PrepareChanges advances Version/UpdatedAt for Modified parents, and the Version
        // concurrency token makes a concurrent mapping and this correction mutually exclusive.
        if (parent is null || parent.State != EntityState.Modified
            || parent.Property(row => row.Version).OriginalValue != original.Version
            || parent.Entity.Version != original.Version || parent.Property(row => row.Version).IsModified) return false;
        var current = parent.Entity;
        if (current.Status is not ("Received" or "Cancelled") || current.IntakeChannel != "Manual"
            || current.BranchRef != original.BranchRef || current.CreatedBy != original.CreatedBy
            || ProcurementEvidenceEncoding.DatabaseTimestamp(current.CreatedAt) != ProcurementEvidenceEncoding.DatabaseTimestamp(original.CreatedAt)
            || current.FinancePostingRef != null || current.EvidenceDocumentRefId != null || current.CreditForInvoiceRef != null
            || current.MappingRevision != 0 || current.CurrentMappingVersionId != null || current.LastMatchResultId != null
            || current.ActiveMatchClaimResultId != null || current.MatchRevision != 0 || current.MatchingState != "NotCompared") return false;
        // Evidence created in this same SaveChanges also closes correction. A caller cannot
        // rewrite an original and insert its first mapping/document/credit in one batch.
        return !ChangeTracker.Entries<ProcurementRecord>().Any(entry => (entry.State is EntityState.Added or EntityState.Modified) && (entry.Entity switch
        {
            InvoiceMatchMappingVersion row => row.SupplierInvoiceId == id,
            InvoiceMatchAllocation row => row.SupplierInvoiceId == id,
            InvoiceMatchResult row => row.SupplierInvoiceId == id,
            InvoiceException row => row.SupplierInvoiceId == id,
            FinanceInvoiceHandoff row => row.SupplierInvoiceId == id,
            FinancePaymentStatusRef row => row.SupplierInvoiceId == id,
            SupplierCreditNoteRef row => row.SupplierInvoiceId == id,
            ManualPaymentRequest row => row.SupplierInvoiceId == id && row.Status != "Cancelled" && row.Status != "Rejected",
            ProcurementAttachmentRef row => row.OwnerId == id,
            SupplierInvoiceLine row => row.SupplierInvoiceId == id
                && (row.PurchaseOrderLineId != null || row.GoodsReceiptLineId != null || row.ServiceAcceptanceId != null),
            _ => false,
        }));
    }
}
