using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Procurement owns the `procurement` schema and migration history.</summary>
public sealed class ProcurementDbContext(DbContextOptions<ProcurementDbContext> options, IProcurementScope scope) : DbContext(options)
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierContact> SupplierContacts => Set<SupplierContact>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>Stamp scope before EF tracks alternate keys used by child foreign keys.</summary>
    public void AddScoped<T>(T record) where T : ProcurementRecord
    {
        Stamp(record);
        Set<T>().Add(record);
    }

    private void Stamp(ProcurementRecord record)
    {
        record.TenantId = scope.TenantId;
        record.LegalEntityId = scope.LegalEntityId;
        if (record is Supplier supplier)
            foreach (var contact in supplier.Contacts) Stamp(contact);
        if (record is PurchaseRequest request)
            foreach (var line in request.Lines) Stamp(line);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("procurement");
        ConfigureBase<Supplier>(modelBuilder, "suppliers");
        ConfigureBase<SupplierContact>(modelBuilder, "supplier_contacts");
        ConfigureBase<CatalogItem>(modelBuilder, "catalog_items");
        ConfigureBase<PurchaseRequest>(modelBuilder, "purchase_requests");
        ConfigureBase<PurchaseRequestLine>(modelBuilder, "purchase_request_lines");
        ConfigureBase<PolicyVersion>(modelBuilder, "policy_versions");
        ConfigureBase<ImportBatch>(modelBuilder, "import_batches");
        ConfigureBase<AuditEvent>(modelBuilder, "audit_events");
        ConfigureBase<OutboxEvent>(modelBuilder, "outbox_events");

        modelBuilder.Entity<Supplier>(b =>
        {
            b.Property(x => x.Number).HasMaxLength(40);
            b.Property(x => x.LegalName).HasMaxLength(300);
            b.Property(x => x.DisplayName).HasMaxLength(300);
            b.Property(x => x.CountryCode).HasMaxLength(2);
            b.Property(x => x.NormalizedRegistration).HasMaxLength(120);
            b.Property(x => x.Status).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CountryCode, x.NormalizedRegistration })
                .IsUnique().HasFilter("normalized_registration IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Contacts).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SupplierContact>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(320);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId });
        });
        modelBuilder.Entity<CatalogItem>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(300);
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.UnitCode).HasMaxLength(30);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.CategoryCode });
        });
        modelBuilder.Entity<PurchaseRequest>(b =>
        {
            b.Property(x => x.Number).HasMaxLength(40);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RequesterWorkerId, x.Status });
            b.HasMany(x => x.Lines).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PurchaseRequestLine>(b =>
        {
            b.Property(x => x.RequestedQuantity).HasPrecision(18, 4);
            b.Property(x => x.EstimatedUnitPrice).HasPrecision(18, 4);
            b.Property(x => x.ApprovedQuantity).HasPrecision(18, 4);
            b.Property(x => x.AllocatedQuantity).HasPrecision(18, 4);
            b.Property(x => x.OrderedQuantity).HasPrecision(18, 4);
            b.Property(x => x.AcceptedQuantity).HasPrecision(18, 4);
            b.Property(x => x.CancelledQuantity).HasPrecision(18, 4);
            b.Property(x => x.ApprovalStatus).HasMaxLength(40);
            b.Property(x => x.Route).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.LineNumber }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("ck_pr_line_nonnegative",
                "requested_quantity > 0 AND estimated_unit_price >= 0 AND approved_quantity >= 0 AND allocated_quantity >= 0 AND ordered_quantity >= 0 AND accepted_quantity >= 0 AND cancelled_quantity >= 0"));
        });
        modelBuilder.Entity<PolicyVersion>(b =>
        {
            b.Property(x => x.PolicyKey).HasMaxLength(100);
            b.Property(x => x.State).HasMaxLength(30);
            b.Property(x => x.RulesJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyKey, x.PolicyVersionNumber }).IsUnique();
        });
        modelBuilder.Entity<ImportBatch>(b =>
        {
            b.Property(x => x.RecordType).HasMaxLength(60);
            b.Property(x => x.FileSha256).HasMaxLength(64);
            b.Property(x => x.State).HasMaxLength(30);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.State, x.ExpiresAt });
        });
        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.Property(x => x.ChangedFieldsJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RecordType, x.RecordId, x.CreatedAt });
        });
        modelBuilder.Entity<OutboxEvent>(b =>
        {
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.State).HasMaxLength(30);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.State, x.NextAttemptAt });
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnake(property.Name));
    }

    private void ConfigureBase<T>(ModelBuilder modelBuilder, string table) where T : ProcurementRecord
    {
        var b = modelBuilder.Entity<T>();
        b.ToTable(table, "procurement");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id });
        b.Property(x => x.TenantId).HasMaxLength(128);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId);
    }

    private string CurrentTenantId => scope.TenantId;
    private Guid CurrentLegalEntityId => scope.LegalEntityId;
    private static string ToSnake(string name) => string.Concat(name.Select((letter, index) =>
        index > 0 && char.IsUpper(letter) ? "_" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));

    public override int SaveChanges()
    {
        PrepareChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PrepareChanges()
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.SubjectId))
            throw new InvalidOperationException("An authenticated tenant, legal entity and actor are required to write Procurement data.");
        var now = DateTimeOffset.UtcNow;
        var changes = ChangeTracker.Entries<ProcurementRecord>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changes)
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Procurement records must be closed, cancelled or reversed; physical deletion is not allowed.");
            if (entry.State == EntityState.Modified && entry.Entity is AuditEvent)
                throw new InvalidOperationException("Audit events are append-only.");
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                    throw new InvalidOperationException("A new record must be stamped with the authenticated scope before tracking.");
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = scope.SubjectId;
                entry.Entity.Version = 1;
            }
            else
            {
                if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                    throw new InvalidOperationException("A record outside the authenticated scope cannot be changed.");
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = scope.SubjectId;
                entry.Entity.Version++;
            }
            if (entry.Entity is AuditEvent or OutboxEvent) continue;
            AuditEvents.Add(new AuditEvent
            {
                RecordType = entry.Entity.GetType().Name,
                RecordId = entry.Entity.Id,
                Action = entry.State == EntityState.Added ? "create" : "update",
                ActorSubjectId = scope.SubjectId,
                ChangedFieldsJson = JsonSerializer.Serialize(entry.State == EntityState.Added
                    ? entry.Properties.Select(p => p.Metadata.Name).OrderBy(name => name)
                    : entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).OrderBy(name => name)),
                CorrelationId = scope.CorrelationId,
                TenantId = scope.TenantId,
                LegalEntityId = scope.LegalEntityId,
                CreatedAt = now,
                CreatedBy = scope.SubjectId,
            });
        }
    }
}
