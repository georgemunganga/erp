using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<InvoiceReplacementLink> InvoiceReplacementLinks => Set<InvoiceReplacementLink>();
    public DbSet<InvoiceReplacementCommandReceipt> InvoiceReplacementCommandReceipts => Set<InvoiceReplacementCommandReceipt>();

    private void ConfigureInvoiceReplacement(ModelBuilder model)
    {
        ConfigureBase<InvoiceReplacementLink>(model, "invoice_replacement_links");
        var link = model.Entity<InvoiceReplacementLink>();
        link.Property(x => x.SourceOriginalSha256).HasMaxLength(64);
        link.Property(x => x.NoClaimProofSha256).HasMaxLength(64);
        link.Property(x => x.CurrencyCode).HasMaxLength(3);
        link.Property(x => x.MakerPrincipalId).HasMaxLength(200);
        link.Property(x => x.Reason).HasMaxLength(1000);
        link.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceInvoiceId })
            .IsUnique().HasDatabaseName("ux_invoice_replacement_source");
        link.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SuccessorInvoiceId })
            .IsUnique().HasDatabaseName("ux_invoice_replacement_successor");
        link.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SuccessorInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceMatchMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceMappingVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceMatchResult>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.BlockedResultId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceUnitReviewSource>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceUnitReviewOutcome>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewOutcomeId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceAmountCalculation>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CalculationId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        link.HasOne<InvoiceIntakeCommandReceipt>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CaptureReceiptId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        if (Database.IsNpgsql()) link.ToTable(t => t.HasCheckConstraint("ck_invoice_replacement_link_v1",
            "version=1 AND source_invoice_id<>successor_invoice_id "
            + "AND source_invoice_version>0 AND source_mapping_revision>0 "
            + "AND source_original_sha256 ~ '^[0-9a-f]{64}$' AND no_claim_proof_sha256 ~ '^[0-9a-f]{64}$' "
            + "AND currency_code ~ '^[A-Z]{3}$' AND octet_length(reason) BETWEEN 1 AND 1000 "
            + "AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND capture_command_key <> command_key AND isfinite(recorded_at_utc) "
            + "AND created_at>=recorded_at_utc AND updated_at IS NULL AND updated_by IS NULL"));

        ConfigureBase<InvoiceReplacementCommandReceipt>(model, "invoice_replacement_command_receipts");
        var receipt = model.Entity<InvoiceReplacementCommandReceipt>();
        receipt.Property(x => x.Operation).HasMaxLength(32);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(200);
        receipt.Property(x => x.RequestSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.LinkId })
            .IsUnique().HasDatabaseName("ux_invoice_replacement_receipt_link");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_invoice_replacement_receipt_actor_key");
        receipt.HasOne<InvoiceReplacementLink>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.LinkId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SuccessorInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        if (Database.IsNpgsql()) receipt.ToTable(t => t.HasCheckConstraint("ck_invoice_replacement_receipt_v1",
            "operation='InvoiceReplaceV2' AND version=1 AND response_status=201 "
            + "AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' "
            + "AND octet_length(response_json) BETWEEN 1 AND 262144 "
            + "AND isfinite(completed_at_utc) AND created_at>=completed_at_utc "
            + "AND updated_at IS NULL AND updated_by IS NULL"));
    }

    private void GuardInvoiceReplacement()
    {
        var links = ChangeTracker.Entries<InvoiceReplacementLink>().ToArray();
        var receipts = ChangeTracker.Entries<InvoiceReplacementCommandReceipt>().ToArray();
        if (links.Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || receipts.Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("InvoiceReplacementAppendOnly");
        var newLinks = links.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var newReceipts = receipts.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        if (newLinks.Length != newReceipts.Length
            || newLinks.Select(x => (x.TenantId,x.LegalEntityId,x.SourceInvoiceId)).Distinct().Count() != newLinks.Length
            || newLinks.Select(x => (x.TenantId,x.LegalEntityId,x.SuccessorInvoiceId)).Distinct().Count() != newLinks.Length)
            throw new InvalidOperationException("InvoiceReplacementAtomicSetChanged");
        foreach (var linkRow in newLinks)
        {
            var receiptRow = newReceipts.SingleOrDefault(x => x.LinkId == linkRow.Id)
                ?? throw new InvalidOperationException("InvoiceReplacementReceiptRequired");
            var source = SupplierInvoices.AsNoTracking().SingleOrDefault(x => x.Id == linkRow.SourceInvoiceId)
                ?? throw new InvalidOperationException("InvoiceReplacementSourceMissing");
            var successor = ChangeTracker.Entries<SupplierInvoice>().SingleOrDefault(x =>
                x.State == EntityState.Added && x.Entity.Id == linkRow.SuccessorInvoiceId)?.Entity
                ?? throw new InvalidOperationException("InvoiceReplacementNewSuccessorRequired");
            if (ChangeTracker.Entries<SupplierInvoice>().Any(x => x.Entity.Id == source.Id && x.State != EntityState.Unchanged)
                || ChangeTracker.Entries<SupplierInvoiceLine>().Any(x =>
                    x.Entity.SupplierInvoiceId == source.Id && x.State != EntityState.Unchanged))
                throw new InvalidOperationException("InvoiceReplacementSourceImmutable");
            var mapping = InvoiceMatchMappingVersions.AsNoTracking().Single(x => x.Id == linkRow.SourceMappingVersionId);
            var blocked = InvoiceMatchResults.AsNoTracking().Single(x => x.Id == linkRow.BlockedResultId);
            var reviewSource = InvoiceUnitReviewSources.AsNoTracking().Single(x => x.Id == linkRow.ReviewSourceId);
            var reviewOutcome = InvoiceUnitReviewOutcomes.AsNoTracking().Single(x => x.Id == linkRow.ReviewOutcomeId);
            var calculation = ChangeTracker.Entries<InvoiceAmountCalculation>().SingleOrDefault(x =>
                x.State == EntityState.Added && x.Entity.Id == linkRow.CalculationId)?.Entity
                ?? throw new InvalidOperationException("InvoiceReplacementNewCalculationRequired");
            var capture = ChangeTracker.Entries<InvoiceIntakeCommandReceipt>().SingleOrDefault(x =>
                x.State == EntityState.Added && x.Entity.Id == linkRow.CaptureReceiptId)?.Entity
                ?? throw new InvalidOperationException("InvoiceReplacementNewCaptureReceiptRequired");
            InvoiceReplacementEvidence.Verify(linkRow, receiptRow, source, successor, mapping,
                blocked, reviewSource, reviewOutcome, calculation, capture);
        }
    }
}
