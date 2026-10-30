using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<InvoiceIntakeCommandReceipt> InvoiceIntakeCommandReceipts => Set<InvoiceIntakeCommandReceipt>();

    // Root registers this before the shared snake-case convention.
    private void ConfigureInvoiceIntakeCommandReceipts(ModelBuilder model)
    {
        ConfigureBase<InvoiceIntakeCommandReceipt>(model, "invoice_intake_command_receipts");
        var row = model.Entity<InvoiceIntakeCommandReceipt>();
        row.Property(x => x.Operation).HasMaxLength(32);
        row.Property(x => x.ActorPrincipalId).HasMaxLength(200);
        row.Property(x => x.RequestSha256).HasMaxLength(64);
        row.Property(x => x.ResponseSha256).HasMaxLength(64);
        row.Property(x => x.ResponseJson).HasColumnType("text");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_invoice_intake_receipt_actor_key");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CalculationId })
            .IsUnique().HasDatabaseName("ux_invoice_intake_receipt_calculation");
        row.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        row.HasOne<InvoiceAmountCalculation>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CalculationId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        if (Database.IsNpgsql()) row.ToTable(t => t.HasCheckConstraint("ck_invoice_intake_receipt_v2",
            "operation='InvoiceCaptureV2' AND version=1 AND response_status=201 "
            + "AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND calculation_id <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' "
            + "AND octet_length(response_json) BETWEEN 1 AND 262144 "
            + "AND isfinite(completed_at_utc) AND created_at>=completed_at_utc "
            + "AND updated_at IS NULL AND updated_by IS NULL"));
    }

    // Root registers after scope/audit stamping and before the owning SaveChanges.
    private void GuardInvoiceIntakeCommandReceipts()
    {
        var entries = ChangeTracker.Entries<InvoiceIntakeCommandReceipt>().ToArray();
        if (entries.Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("InvoiceIntakeReceiptAppendOnly");
        var receipts = entries.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var calculations = ChangeTracker.Entries<InvoiceAmountCalculation>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        if (receipts.Length != calculations.Length
            || receipts.Select(x => (x.TenantId,x.LegalEntityId,x.ActorPrincipalId,x.CommandKey))
                .Distinct().Count() != receipts.Length
            || receipts.Select(x => (x.TenantId,x.LegalEntityId,x.CalculationId))
                .Distinct().Count() != receipts.Length)
            throw new InvalidOperationException("InvoiceIntakeReceiptAtomicSetChanged");
        foreach (var receipt in receipts)
        {
            if (receipt.TenantId != scope.TenantId || receipt.LegalEntityId != scope.LegalEntityId
                || receipt.ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("InvoiceIntakeReceiptScopeChanged");
            var calculation = calculations.SingleOrDefault(x => x.Id == receipt.CalculationId
                && x.SupplierInvoiceId == receipt.SupplierInvoiceId && x.TenantId == receipt.TenantId
                && x.LegalEntityId == receipt.LegalEntityId)
                ?? throw new InvalidOperationException("InvoiceIntakeReceiptCalculationChanged");
            var invoice = ChangeTracker.Entries<SupplierInvoice>().SingleOrDefault(x =>
                x.State == EntityState.Added && x.Entity.Id == receipt.SupplierInvoiceId
                && x.Entity.TenantId == receipt.TenantId && x.Entity.LegalEntityId == receipt.LegalEntityId)?.Entity
                ?? throw new InvalidOperationException("InvoiceIntakeReceiptNewInvoiceRequired");
            var lines = ChangeTracker.Entries<SupplierInvoiceLine>().Where(x =>
                x.State == EntityState.Added && x.Entity.SupplierInvoiceId == invoice.Id)
                .Select(x => x.Entity).ToArray();
            InvoiceIntakeCommandReceiptEvidence.Verify(receipt, invoice, calculation, lines);
        }
    }
}
