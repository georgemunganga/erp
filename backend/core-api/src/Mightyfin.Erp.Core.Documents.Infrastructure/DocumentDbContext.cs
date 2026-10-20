using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>Core owns file metadata; business modules authorize the owning record.</summary>
public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options, IDocumentScope scope) : DbContext(options)
{
    public DbSet<CoreDocument> Documents => Set<CoreDocument>();
    public DbSet<CoreDocumentVersion> Versions => Set<CoreDocumentVersion>();
    public DbSet<DocumentRecordEvent> RecordEvents => Set<DocumentRecordEvent>();
    public DbSet<DocumentScanEvidence> ScanEvidence => Set<DocumentScanEvidence>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("core_document");
        model.Entity<CoreDocument>(b =>
        {
            b.ToTable("documents");
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id });
            b.Property(x => x.TenantId).HasMaxLength(150);
            b.Property(x => x.UploadKey).HasMaxLength(32);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.UploadKey }).IsUnique();
            b.Property(x => x.OwnerModule).HasMaxLength(60);
            b.Property(x => x.OwnerType).HasMaxLength(80);
            b.Property(x => x.Classification).HasMaxLength(20);
            b.Property(x => x.CreatedBy).HasMaxLength(150);
            b.Property(x => x.RetiredBy).HasMaxLength(150);
            b.Property(x => x.RetirementReason).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId });
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
        model.Entity<CoreDocumentVersion>(b =>
        {
            b.ToTable("versions");
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id });
            b.Property(x => x.TenantId).HasMaxLength(150);
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.Id });
            b.Property(x => x.ScanFailureCode).HasMaxLength(80);
            b.Property(x => x.FileName).HasMaxLength(255);
            b.Property(x => x.MediaType).HasMaxLength(100);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.Property(x => x.StorageKey).HasMaxLength(100);
            b.Property(x => x.CreatedBy).HasMaxLength(150);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.Number }).IsUnique();
            b.HasIndex(x => x.StorageKey).IsUnique();
            b.HasOne<CoreDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
        model.Entity<DocumentScanEvidence>(b =>
        {
            b.ToTable("scan_evidence");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).HasMaxLength(150);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.Property(x => x.FailureCode).HasMaxLength(80);
            b.Property(x => x.Engine).HasMaxLength(120);
            b.Property(x => x.Definitions).HasMaxLength(120);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.VersionId, x.Attempt }).IsUnique();
            b.HasOne<CoreDocumentVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.VersionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
        model.Entity<DocumentRecordEvent>(b =>
        {
            b.ToTable("record_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.TenantId).HasMaxLength(150);
            b.Property(x => x.Action).HasMaxLength(60);
            b.Property(x => x.ActorSubjectId).HasMaxLength(150);
            b.Property(x => x.FailureCode).HasMaxLength(80);
            b.HasOne<CoreDocumentVersion>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.VersionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.At });
            b.HasQueryFilter(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId);
        });
    }

    public override int SaveChanges() { ValidateChanges(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    { ValidateChanges(); return base.SaveChangesAsync(cancellationToken); }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { ValidateChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { ValidateChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }

    private void ValidateChanges()
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.SubjectId))
            throw new UnauthorizedAccessException("An authenticated document scope is required.");
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified && entry.Entity is DocumentScanEvidence or DocumentRecordEvent)
                throw new InvalidOperationException("Document evidence must be retained.");
            if ((string)entry.Property("TenantId").CurrentValue! != scope.TenantId
                || (Guid)entry.Property("LegalEntityId").CurrentValue! != scope.LegalEntityId)
                throw new UnauthorizedAccessException("Document is outside the actor scope.");
            if (entry.State == EntityState.Added && entry.Entity is DocumentRecordEvent audit && audit.ActorSubjectId != scope.SubjectId)
                throw new UnauthorizedAccessException("Document evidence must identify the acting subject.");
            if (entry.State == EntityState.Modified)
            {
                var mutable = entry.Entity is CoreDocument
                    ? new[] { "IsRetired", "RetiredBy", "RetirementReason", "RetiredAt", "Version" }
                    : new[] { "ScanFailureCode", "ScanState", "ScanAttempts", "ScanLeaseId", "ScanStartedAt", "ScanLeaseExpiresAt", "Version" };
                if (entry.Properties.Any(x => x.IsModified && !mutable.Contains(x.Metadata.Name)))
                    throw new InvalidOperationException("Document identity and content metadata are immutable.");
            }
        }
    }
}
