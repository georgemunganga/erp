using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed partial class DocumentDbContext
{
    public DbSet<DocumentUseSetRow> UseSets => Set<DocumentUseSetRow>();
    public DbSet<DocumentUsePinRow> UsePins => Set<DocumentUsePinRow>();

    private void ConfigureDocumentUses(ModelBuilder model)
    {
        // Stable immutable ownership keys only; keep names short to avoid Npgsql truncation/retained-key renames.
        model.Entity<CoreDocument>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.Id }).HasName("ak_doc_owner_id");
        model.Entity<DocumentScanEvidence>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.VersionId, x.Id }).HasName("ak_doc_scan_version_id");
        model.Entity<DocumentUseSetRow>(b =>
        {
            b.ToTable("use_sets", t => t.HasCheckConstraint("ck_doc_use_set_shape",
                "\"SourceRevision\" > 0 AND \"Kind\" IN (1,2) AND \"SelectionCount\" BETWEEN 1 AND 100 AND \"SelectionDigest\" ~ '^[0-9a-f]{64}$'"));
            b.HasKey(x => x.Id).HasName("pk_doc_use_sets");
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.Id })
                .HasName("ak_doc_use_set_owner_id");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UseKey }).IsUnique().HasDatabaseName("ux_doc_use_key");
            b.Property(x => x.TenantId).HasMaxLength(150).IsRequired();
            b.Property(x => x.SourceModule).HasMaxLength(60).IsRequired();
            b.Property(x => x.SourceRecordType).HasMaxLength(80).IsRequired();
            b.Property(x => x.OwnerModule).HasMaxLength(60).IsRequired();
            b.Property(x => x.OwnerType).HasMaxLength(80).IsRequired();
            b.Property(x => x.CreatedBy).HasMaxLength(150).IsRequired();
            b.Property(x => x.SelectionDigest).HasMaxLength(64).IsRequired();
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
        model.Entity<DocumentUsePinRow>(b =>
        {
            b.ToTable("use_pins", t => t.HasCheckConstraint("ck_doc_use_pin_shape",
                "\"DocumentRevision\" > 0 AND \"VersionRevision\" > 0 AND \"VersionNumber\" > 0 AND \"SizeBytes\" BETWEEN 1 AND 104857600 AND \"ScanAttempt\" > 0 AND \"ScanVerdict\" = 1 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"ScanFinishedAt\" >= \"ScanStartedAt\" AND \"CreatedAt\" >= \"ScanFinishedAt\""));
            b.HasKey(x => x.Id).HasName("pk_doc_use_pins");
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).HasMaxLength(150).IsRequired();
            b.Property(x => x.OwnerModule).HasMaxLength(60).IsRequired();
            b.Property(x => x.OwnerType).HasMaxLength(80).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.MediaType).HasMaxLength(100).IsRequired();
            b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            b.Property(x => x.ScanEngine).HasMaxLength(120).IsRequired();
            b.Property(x => x.ScanDefinitions).HasMaxLength(120).IsRequired();
            b.Property(x => x.CreatedBy).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UseSetId, x.VersionId }).IsUnique().HasDatabaseName("ux_doc_use_set_version");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DocumentId }).HasDatabaseName("ix_doc_use_document");
            b.HasOne<DocumentUseSetRow>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.UseSetId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_doc_pin_set_owner");
            b.HasOne<CoreDocument>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.DocumentId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_doc_pin_document_owner");
            b.HasOne<CoreDocumentVersion>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.VersionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_doc_pin_document_version");
            b.HasOne<DocumentScanEvidence>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.VersionId, x.ScanEvidenceId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.VersionId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_doc_pin_version_scan");
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
    }
}
