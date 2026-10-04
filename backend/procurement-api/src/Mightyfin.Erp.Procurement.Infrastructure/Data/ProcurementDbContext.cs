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
    public DbSet<SupplierSite> SupplierSites => Set<SupplierSite>();
    public DbSet<SupplierQualification> SupplierQualifications => Set<SupplierQualification>();
    public DbSet<SupplierAttribute> SupplierAttributes => Set<SupplierAttribute>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierDecision> SupplierDecisions => Set<SupplierDecision>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogEntry> CatalogEntries => Set<CatalogEntry>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();
    public DbSet<PurchaseRequestAllocation> PurchaseRequestAllocations => Set<PurchaseRequestAllocation>();
    public DbSet<PolicyEvaluation> PolicyEvaluations => Set<PolicyEvaluation>();
    public DbSet<BudgetCheckSnapshot> BudgetCheckSnapshots => Set<BudgetCheckSnapshot>();
    public DbSet<ProcurementAttachmentRef> AttachmentRefs => Set<ProcurementAttachmentRef>();
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
        {
            foreach (var contact in supplier.Contacts) Stamp(contact);
            foreach (var site in supplier.Sites) Stamp(site);
            foreach (var qualification in supplier.Qualifications) Stamp(qualification);
            foreach (var attribute in supplier.Attributes) Stamp(attribute);
            foreach (var category in supplier.Categories) Stamp(category);
            foreach (var decision in supplier.Decisions) Stamp(decision);
        }
        if (record is CatalogItem item)
            foreach (var entry in item.Entries) Stamp(entry);
        if (record is PurchaseRequest request)
        {
            foreach (var line in request.Lines) Stamp(line);
            foreach (var allocation in request.Allocations) Stamp(allocation);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("procurement");
        ConfigureBase<Supplier>(modelBuilder, "suppliers");
        ConfigureBase<SupplierContact>(modelBuilder, "supplier_contacts");
        ConfigureBase<SupplierSite>(modelBuilder, "supplier_sites");
        ConfigureBase<SupplierQualification>(modelBuilder, "supplier_qualifications");
        ConfigureBase<SupplierAttribute>(modelBuilder, "supplier_attributes");
        ConfigureBase<SupplierCategory>(modelBuilder, "supplier_categories");
        ConfigureBase<SupplierDecision>(modelBuilder, "supplier_decisions");
        ConfigureBase<CatalogItem>(modelBuilder, "catalog_items");
        ConfigureBase<CatalogEntry>(modelBuilder, "catalog_entries");
        ConfigureBase<PurchaseRequest>(modelBuilder, "purchase_requests");
        ConfigureBase<PurchaseRequestLine>(modelBuilder, "purchase_request_lines");
        ConfigureBase<PurchaseRequestAllocation>(modelBuilder, "purchase_request_allocations");
        ConfigureBase<PolicyEvaluation>(modelBuilder, "policy_evaluations");
        ConfigureBase<BudgetCheckSnapshot>(modelBuilder, "budget_check_snapshots");
        ConfigureBase<ProcurementAttachmentRef>(modelBuilder, "attachment_refs");
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
            b.Property(x => x.DefaultCurrencyCode).HasMaxLength(3);
            b.Property(x => x.LanguageCode).HasMaxLength(12);
            b.Property(x => x.SupplierType).HasMaxLength(60);
            b.Property(x => x.RiskTier).HasMaxLength(40);
            b.Property(x => x.PortalAccessState).HasMaxLength(40);
            b.Property(x => x.PaymentVerificationStatus).HasMaxLength(40);
            b.Property(x => x.PaymentVerificationToken).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IsOrderable, x.Status });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CountryCode, x.NormalizedRegistration })
                .IsUnique().HasFilter("normalized_registration IS NOT NULL");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Contacts).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Sites).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Qualifications).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Attributes).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Categories).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Decisions).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SupplierContact>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.LanguageCode).HasMaxLength(12);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.IsPrimary })
                .IsUnique().HasFilter("is_primary = true");
        });
        modelBuilder.Entity<SupplierSite>(b =>
        {
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.CountryCode).HasMaxLength(2);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.Kind });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.Kind, x.IsPrimary })
                .IsUnique().HasFilter("is_primary = true");
        });
        modelBuilder.Entity<SupplierQualification>(b =>
        {
            b.Property(x => x.QualificationType).HasMaxLength(80);
            b.Property(x => x.ReviewStatus).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.ExpiresOn });
        });
        modelBuilder.Entity<SupplierAttribute>(b =>
        {
            b.Property(x => x.Kind).HasMaxLength(30);
            b.Property(x => x.FieldKey).HasMaxLength(120);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.Kind, x.FieldKey }).IsUnique();
        });
        modelBuilder.Entity<SupplierCategory>(b =>
        {
            b.Property(x => x.CategoryCode).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.CategoryCode }).IsUnique();
        });
        modelBuilder.Entity<SupplierDecision>(b =>
        {
            b.Property(x => x.FromStatus).HasMaxLength(40);
            b.Property(x => x.ToStatus).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.DecidedAt });
        });
        modelBuilder.Entity<CatalogItem>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(300);
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.UnitCode).HasMaxLength(30);
            b.Property(x => x.PurchaseUnitCode).HasMaxLength(30);
            b.Property(x => x.PurchaseUnitConversion).HasPrecision(18, 6);
            b.Property(x => x.MinimumOrderQuantity).HasPrecision(18, 4);
            b.ToTable(t => t.HasCheckConstraint("ck_catalog_item_buying_values",
                "(purchase_unit_conversion IS NULL OR purchase_unit_conversion > 0) AND (minimum_order_quantity IS NULL OR minimum_order_quantity >= 0) AND (lead_time_days IS NULL OR lead_time_days >= 0)"));
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.CategoryCode });
            b.HasMany(x => x.Entries).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CatalogItemId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CatalogEntry>(b =>
        {
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.UnitCode).HasMaxLength(30);
            b.Property(x => x.UnitPrice).HasPrecision(18, 4);
            b.Property(x => x.MinimumQuantity).HasPrecision(18, 4);
            b.Property(x => x.MaximumQuantity).HasPrecision(18, 4);
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.AudienceCode).HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CatalogItemId, x.Status });
            b.HasOne<Supplier>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("ck_catalog_entry_values",
                "unit_price >= 0 AND (minimum_quantity IS NULL OR minimum_quantity >= 0) AND (maximum_quantity IS NULL OR maximum_quantity >= 0) AND (minimum_quantity IS NULL OR maximum_quantity IS NULL OR maximum_quantity >= minimum_quantity)"));
        });
        modelBuilder.Entity<PurchaseRequest>(b =>
        {
            b.Property(x => x.Number).HasMaxLength(40);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.SourceChannel).HasMaxLength(40);
            b.Property(x => x.ApprovalState).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RequesterWorkerId, x.Status });
            b.HasMany(x => x.Lines).WithOne()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Allocations).WithOne()
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
            b.Property(x => x.PurchaseType).HasMaxLength(40);
            b.Property(x => x.EstimatedCurrencyCode).HasMaxLength(3);
            b.Property(x => x.DiscountPercent).HasPrecision(7, 4);
            b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.Id });
            b.HasOne<CatalogItem>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CatalogItemId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Supplier>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PreferredSupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.LineNumber }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("ck_pr_line_nonnegative",
                "requested_quantity > 0 AND estimated_unit_price >= 0 AND approved_quantity >= 0 AND allocated_quantity >= 0 AND ordered_quantity >= 0 AND accepted_quantity >= 0 AND cancelled_quantity >= 0 AND (discount_percent IS NULL OR (discount_percent >= 0 AND discount_percent <= 100))"));
        });
        modelBuilder.Entity<PurchaseRequestAllocation>(b =>
        {
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.EstimatedValue).HasPrecision(18, 4);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.Method).HasMaxLength(40);
            b.Property(x => x.Status).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IdempotencyKey }).IsUnique();
            b.HasOne<PurchaseRequestLine>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.PurchaseRequestLineId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("ck_pr_allocation_nonnegative",
                "quantity > 0 AND estimated_value >= 0"));
        });
        modelBuilder.Entity<PolicyEvaluation>(b =>
        {
            b.Property(x => x.Outcome).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.EvaluatedAt });
            b.HasOne<PurchaseRequest>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PolicyVersion>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BudgetCheckSnapshot>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(40);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.CheckedAmount).HasPrecision(18, 4);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.FinanceCheckRef }).IsUnique();
            b.HasOne<PurchaseRequest>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProcurementAttachmentRef>(b =>
        {
            b.Property(x => x.OwnerType).HasMaxLength(80);
            b.Property(x => x.ScanStatus).HasMaxLength(40);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OwnerType, x.OwnerId });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DocumentServiceId, x.DocumentVersion }).IsUnique();
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
            if (entry.State == EntityState.Modified && entry.Entity is AuditEvent or SupplierDecision or PolicyEvaluation or BudgetCheckSnapshot)
                throw new InvalidOperationException("Decision, evaluation, budget and audit records are append-only.");
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
