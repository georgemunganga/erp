using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardInvoiceOwnerBaselineAdmission()
    {
        var addedBaselines=ChangeTracker.Entries<InvoiceProcurementOwnerBaseline>()
            .Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var addedRows=ChangeTracker.Entries<InvoiceProcurementOwnerBaselineRow>()
            .Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        if(addedBaselines.Length>0 || addedRows.Length>0)
        {
            if(addedBaselines.Length!=1 || !ReferenceEquals(addedBaselines[0],stagedInvoiceOwnerBaseline)
                || stagedInvoiceOwnerCapture is null || stagedInvoiceOwnerRows is null
                || stagedInvoiceOwnerCase is null || stagedInvoiceOwnerReviewers is null
                || addedRows.Length!=stagedInvoiceOwnerRows.Count
                || addedRows.Any(x=>!stagedInvoiceOwnerRows.Any(y=>ReferenceEquals(x,y))))
                throw new InvalidOperationException("InvoiceOwnerBaselineAdmissionUnavailable");
            InvoiceProcurementBaselineEvidence.VerifyCapture(stagedInvoiceOwnerBaseline!,
                stagedInvoiceOwnerRows,stagedInvoiceOwnerCapture);
            InvoiceProcurementBaselineEvidence.VerifyPendingCore(stagedInvoiceOwnerBaseline!,
                stagedInvoiceOwnerCase,stagedInvoiceOwnerReviewers);
        }
        foreach(var entry in ChangeTracker.Entries<InvoiceProcurementOwnerBaseline>())
        {
            if(entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("InvoiceOwnerBaselineAppendOnly");
        }
        foreach(var entry in ChangeTracker.Entries<InvoiceProcurementOwnerBaselineRow>())
        {
            if(entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("InvoiceOwnerBaselineInventoryAppendOnly");
        }
    }
}
