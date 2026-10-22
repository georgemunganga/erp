using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<RecurringPurchaseCommitment> RecurringPurchaseCommitments => Set<RecurringPurchaseCommitment>();

    private void ConfigureRecurringPurchaseCommitments(ModelBuilder model)
    {
        ConfigureLifecycle<RecurringPurchaseCommitment>(model, "recurring_purchase_commitments");
        var record = model.Entity<RecurringPurchaseCommitment>();
        record.Property(x => x.Title).HasMaxLength(300);
        record.Property(x => x.Frequency).HasMaxLength(20);
        record.Property(x => x.OwnerSubjectId).HasMaxLength(200);
        record.Property(x => x.ClosedReason).HasMaxLength(1000);
        record.Property(x => x.ExpectedAmount).HasPrecision(18, 4);
        record.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        record.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OwnerPrincipalId, x.NextCycleOn });
        record.ToTable(t => t.HasCheckConstraint("ck_recurring_purchase_schedule",
            "status IN ('Draft','Closed') AND frequency IN ('Weekly','Monthly','Quarterly','Yearly') " +
            "AND expected_amount > 0 AND next_cycle_on >= first_cycle_on " +
            "AND (last_cycle_on IS NULL OR (last_cycle_on >= first_cycle_on AND next_cycle_on <= last_cycle_on)) " +
            "AND owner_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND (owner_worker_id IS NULL OR owner_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) " +
            "AND owner_subject_id = owner_principal_id::text " +
            "AND ((status = 'Draft' AND closed_at IS NULL AND closed_reason IS NULL) " +
            "OR (status = 'Closed' AND closed_at IS NOT NULL AND closed_reason IS NOT NULL))"));
        Link<RecurringPurchaseCommitment, Supplier>(model, nameof(RecurringPurchaseCommitment.SupplierId));
        Link<RecurringPurchaseCommitment, SupplierContract>(model, nameof(RecurringPurchaseCommitment.SupplierContractId));
    }
}
