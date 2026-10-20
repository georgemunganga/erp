using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Procurement owns the `procurement` schema and migration history.</summary>
public sealed partial class ProcurementDbContext(DbContextOptions<ProcurementDbContext> options, IProcurementScope scope) : DbContext(options)
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
    public DbSet<PurchaseRequestApprovalSubmission> ApprovalSubmissions => Set<PurchaseRequestApprovalSubmission>();
    public DbSet<BudgetCheckSnapshot> BudgetCheckSnapshots => Set<BudgetCheckSnapshot>();
    public DbSet<ProcurementAttachmentRef> AttachmentRefs => Set<ProcurementAttachmentRef>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<SourcingEvent> SourcingEvents => Set<SourcingEvent>();
    public DbSet<SourcingLine> SourcingLines => Set<SourcingLine>();
    public DbSet<SourcingInvitation> SourcingInvitations => Set<SourcingInvitation>();
    public DbSet<SupplierBid> SupplierBids => Set<SupplierBid>();
    public DbSet<SupplierBidLine> SupplierBidLines => Set<SupplierBidLine>();
    public DbSet<BidEvaluation> BidEvaluations => Set<BidEvaluation>();
    public DbSet<SourcingAward> SourcingAwards => Set<SourcingAward>();
    public DbSet<SourcingAwardLine> SourcingAwardLines => Set<SourcingAwardLine>();
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
    public DbSet<SupplierContractVersion> SupplierContractVersions => Set<SupplierContractVersion>();
    public DbSet<ContractRate> ContractRates => Set<ContractRate>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderRevision> PurchaseOrderRevisions => Set<PurchaseOrderRevision>();
    public DbSet<SupplierOrderAcknowledgement> SupplierOrderAcknowledgements => Set<SupplierOrderAcknowledgement>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<ReceiptInspection> ReceiptInspections => Set<ReceiptInspection>();
    public DbSet<ServiceAcceptance> ServiceAcceptances => Set<ServiceAcceptance>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<InvoiceMatchResult> InvoiceMatchResults => Set<InvoiceMatchResult>();
    public DbSet<InvoiceException> InvoiceExceptions => Set<InvoiceException>();
    public DbSet<FinanceInvoiceHandoff> FinanceInvoiceHandoffs => Set<FinanceInvoiceHandoff>();
    public DbSet<FinancePaymentStatusRef> FinancePaymentStatusRefs => Set<FinancePaymentStatusRef>();
    public DbSet<SupplierCreditNoteRef> SupplierCreditNoteRefs => Set<SupplierCreditNoteRef>();
    public DbSet<ProcurementCategory> ProcurementCategories => Set<ProcurementCategory>();
    public DbSet<ProcurementMethod> ProcurementMethods => Set<ProcurementMethod>();
    public DbSet<ProcurementPlan> ProcurementPlans => Set<ProcurementPlan>();
    public DbSet<ProcurementPlanLine> ProcurementPlanLines => Set<ProcurementPlanLine>();
    public DbSet<SupplierPortalMembership> SupplierPortalMemberships => Set<SupplierPortalMembership>();
    public DbSet<SupplierPerformanceReview> SupplierPerformanceReviews => Set<SupplierPerformanceReview>();
    public DbSet<SupplierPerformanceCriterion> SupplierPerformanceCriteria => Set<SupplierPerformanceCriterion>();
    public DbSet<ProcurementComment> ProcurementComments => Set<ProcurementComment>();
    public DbSet<ProcurementSetupAssessment> ProcurementSetupAssessments => Set<ProcurementSetupAssessment>();

    /// <summary>Stamp scope before EF tracks alternate keys used by child foreign keys.</summary>
    public void AddScoped<T>(T record) where T : ProcurementRecord
    {
        Stamp(record);
        Set<T>().Add(record);
    }

    private void Stamp(ProcurementRecord record)
    {
        // Evidence was constructed for a specific source scope. Never relabel it
        // as belonging to another company when attaching it to this context.
        if ((record is PurchaseRequestApprovalSubmission or PurchaseRequestApprovalOutcome
                or PurchaseRequestFinanceReleaseRequest or PurchaseRequestFinanceReleaseConfirmation) &&
            (record.TenantId != scope.TenantId || record.LegalEntityId != scope.LegalEntityId))
            throw new InvalidOperationException("Pinned approval evidence is outside the authenticated scope.");
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
        if (record is ProcurementPlan plan)
            foreach (var line in plan.Lines) Stamp(line);
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
        ConfigureBase<PurchaseRequestApprovalSubmission>(modelBuilder, "purchase_request_approval_submissions");
        ConfigureBase<BudgetCheckSnapshot>(modelBuilder, "budget_check_snapshots");
        ConfigureBase<ProcurementAttachmentRef>(modelBuilder, "attachment_refs");
        ConfigureBase<PolicyVersion>(modelBuilder, "policy_versions");
        ConfigureBase<ImportBatch>(modelBuilder, "import_batches");
        ConfigureBase<AuditEvent>(modelBuilder, "audit_events");
        ConfigureBase<OutboxEvent>(modelBuilder, "outbox_events");
        ConfigureLifecycle<SourcingEvent>(modelBuilder, "sourcing_events");
        ConfigureLifecycle<SourcingLine>(modelBuilder, "sourcing_lines");
        ConfigureLifecycle<SourcingInvitation>(modelBuilder, "sourcing_invitations");
        ConfigureLifecycle<SupplierBid>(modelBuilder, "supplier_bids");
        ConfigureLifecycle<SupplierBidLine>(modelBuilder, "supplier_bid_lines");
        ConfigureLifecycle<BidEvaluation>(modelBuilder, "bid_evaluations");
        ConfigureLifecycle<SourcingAward>(modelBuilder, "sourcing_awards");
        ConfigureLifecycle<SourcingAwardLine>(modelBuilder, "sourcing_award_lines");
        ConfigureLifecycle<SupplierContract>(modelBuilder, "supplier_contracts");
        ConfigureLifecycle<SupplierContractVersion>(modelBuilder, "supplier_contract_versions");
        ConfigureLifecycle<ContractRate>(modelBuilder, "contract_rates");
        ConfigureLifecycle<PurchaseOrder>(modelBuilder, "purchase_orders");
        ConfigureLifecycle<PurchaseOrderLine>(modelBuilder, "purchase_order_lines");
        ConfigureLifecycle<PurchaseOrderRevision>(modelBuilder, "purchase_order_revisions");
        ConfigureLifecycle<SupplierOrderAcknowledgement>(modelBuilder, "supplier_order_acknowledgements");
        ConfigureLifecycle<GoodsReceipt>(modelBuilder, "goods_receipts");
        ConfigureLifecycle<GoodsReceiptLine>(modelBuilder, "goods_receipt_lines");
        ConfigureLifecycle<ReceiptInspection>(modelBuilder, "receipt_inspections");
        ConfigureLifecycle<ServiceAcceptance>(modelBuilder, "service_acceptances");
        ConfigureLifecycle<SupplierReturn>(modelBuilder, "supplier_returns");
        ConfigureLifecycle<SupplierInvoice>(modelBuilder, "supplier_invoices");
        ConfigureLifecycle<SupplierInvoiceLine>(modelBuilder, "supplier_invoice_lines");
        ConfigureLifecycle<InvoiceMatchResult>(modelBuilder, "invoice_match_results");
        ConfigureLifecycle<InvoiceException>(modelBuilder, "invoice_exceptions");
        ConfigureLifecycle<FinanceInvoiceHandoff>(modelBuilder, "finance_invoice_handoffs");
        ConfigureLifecycle<FinancePaymentStatusRef>(modelBuilder, "finance_payment_status_refs");
        ConfigureLifecycle<SupplierCreditNoteRef>(modelBuilder, "supplier_credit_note_refs");
        ConfigureLifecycle<ProcurementCategory>(modelBuilder, "procurement_categories");
        ConfigureLifecycle<ProcurementMethod>(modelBuilder, "procurement_methods");
        ConfigureLifecycle<ProcurementPlan>(modelBuilder, "procurement_plans");
        ConfigureLifecycle<ProcurementPlanLine>(modelBuilder, "procurement_plan_lines");
        ConfigureLifecycle<SupplierPortalMembership>(modelBuilder, "supplier_portal_memberships");
        ConfigureLifecycle<SupplierPerformanceReview>(modelBuilder, "supplier_performance_reviews");
        ConfigureLifecycle<SupplierPerformanceCriterion>(modelBuilder, "supplier_performance_criteria");
        ConfigureLifecycle<ProcurementComment>(modelBuilder, "procurement_comments");
        ConfigureLifecycle<ProcurementSetupAssessment>(modelBuilder, "procurement_setup_assessments");

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
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.LanguageCode).HasMaxLength(12);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.IsPrimary })
                .IsUnique().HasFilter("is_primary = true");
        });
        modelBuilder.Entity<SupplierSite>(b =>
        {
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.CountryCode).HasMaxLength(2);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.Kind });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.Kind, x.IsPrimary })
                .IsUnique().HasFilter("is_primary = true");
        });
        modelBuilder.Entity<SupplierQualification>(b =>
        {
            b.Property(x => x.IsActive).HasDefaultValue(true);
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
            b.Property(x => x.ReturnedBySubjectId).HasMaxLength(200);
            b.Property(x => x.ReturnReason).HasMaxLength(1000);
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
            b.HasOne<CatalogEntry>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CatalogEntryId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Supplier>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PreferredSupplierId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.LineNumber }).IsUnique();
            b.ToTable(t =>
            {
                t.HasCheckConstraint("ck_pr_line_nonnegative",
                    "requested_quantity > 0 AND estimated_unit_price >= 0 AND approved_quantity >= 0 AND allocated_quantity >= 0 AND ordered_quantity >= 0 AND accepted_quantity >= 0 AND cancelled_quantity >= 0 AND (discount_percent IS NULL OR (discount_percent >= 0 AND discount_percent <= 100))");
                t.HasCheckConstraint("ck_pr_line_catalog_ref",
                    "(catalog_entry_id IS NULL AND catalog_published_version IS NULL) OR (catalog_entry_id IS NOT NULL AND catalog_published_version > 0)");
            });
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
            b.Property(x => x.AmountBasis).HasMaxLength(40);
            b.Property(x => x.DecisionJson).HasMaxLength(65536);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.EvaluatedAt });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.RequestVersion }).IsUnique();
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
            b.Property(x => x.CheckedBySubjectId).HasMaxLength(200);
            b.Property(x => x.FinanceCheckRef).HasMaxLength(200);
            b.Property(x => x.EvidenceRef).HasMaxLength(500);
            b.Property(x => x.DecisionReason).HasMaxLength(1000);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.FinanceCheckRef }).IsUnique();
            b.HasOne<PurchaseRequest>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PurchaseRequestApprovalSubmission>(b =>
        {
            b.Property(x => x.MakerSubjectId).HasMaxLength(200);
            // Keep exact canonical bytes: jsonb would reformat JSON and invalidate the stored digest.
            b.Property(x => x.RequestSnapshotJson).HasColumnType("text");
            b.Property(x => x.FinanceSnapshotJson).HasColumnType("text");
            b.Property(x => x.RequestSnapshotSha256).HasMaxLength(64);
            b.Property(x => x.FinanceSnapshotSha256).HasMaxLength(64);
            b.Property(x => x.PolicyDecisionSha256).HasMaxLength(64);
            b.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
            b.Property(x => x.FinanceEvidenceMode).HasMaxLength(40);
            b.Property(x => x.ManualEvidenceAuthorityRef).HasMaxLength(500);
            b.Property(x => x.AmountBasis).HasMaxLength(40);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            // Four-decimal quantities multiplied by four-decimal prices need eight decimals.
            b.Property(x => x.EstimatedAmount).HasPrecision(38, 8);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.SubmittedBusinessVersion }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningCommandKey }).IsUnique();
            b.HasOne<PurchaseRequest>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            // Bind evidence to the same source, submitted version and policy, not just an arbitrary scoped ID.
            b.HasOne<PolicyEvaluation>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyEvaluationId,
                    x.PurchaseRequestId, x.SubmittedBusinessVersion, x.PolicyVersionId, x.PolicyVersionNumber })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                    x.PurchaseRequestId, x.RequestVersion, x.PolicyVersionId, x.PolicyVersionNumber })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PolicyVersion>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyVersionNumber })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyVersionNumber })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BudgetCheckSnapshot>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.FinanceSnapshotId,
                    x.PurchaseRequestId, x.SubmittedBusinessVersion })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                    x.PurchaseRequestId, x.RequestVersion })
                .OnDelete(DeleteBehavior.Restrict);
            // Core owns execution in its separate schema. CoreApprovalCaseId is an explicit reference, not a Core FK.
            b.ToTable(t =>
            {
                t.HasCheckConstraint("ck_pr_approval_submission_identity",
                    "snapshot_schema_version = 1 AND submitted_business_version > 0 AND policy_version_number > 0 " +
                    "AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
                    "AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                t.HasCheckConstraint("ck_pr_approval_submission_amount",
                    "estimated_amount >= 0 AND amount_basis = 'EstimatedLineBase' AND currency_code ~ '^[A-Z]{3}$'");
                t.HasCheckConstraint("ck_pr_approval_submission_digest",
                    "request_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND finance_snapshot_sha256 ~ '^[0-9a-f]{64}$' " +
                    "AND policy_decision_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_pr_approval_submission_finance_mode",
                    "(finance_evidence_mode = 'AutomatedFinance' AND manual_evidence_authority_ref IS NULL) OR " +
                    "(finance_evidence_mode = 'ApprovedManualEvidence' AND manual_evidence_authority_ref IS NOT NULL " +
                    "AND length(btrim(manual_evidence_authority_ref)) > 0)");
            });
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
            b.Property(x => x.ReviewRequestedBy).HasMaxLength(200);
            b.Property(x => x.ReviewReason).HasMaxLength(1000);
            b.Property(x => x.PublishedBy).HasMaxLength(200);
            b.Property(x => x.PublicationReason).HasMaxLength(1000);
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

        ConfigureCommercialRelationships(modelBuilder);
        ConfigureSpendingAuthority(modelBuilder);
        ConfigureApprovalOutcomes(modelBuilder);
        ConfigureApprovalExecution(modelBuilder);
        ConfigurePurchaseOrderExecution(modelBuilder);

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

    private void ConfigureLifecycle<T>(ModelBuilder modelBuilder, string table) where T : ProcurementRecord
    {
        ConfigureBase<T>(modelBuilder, table);
        var b = modelBuilder.Entity<T>();
        foreach (var property in b.Metadata.GetProperties())
        {
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                b.Property(property.Name).HasPrecision(18, 4);
            if (property.Name is "CurrencyCode") b.Property(property.Name).HasMaxLength(3);
            if (property.Name is "Status" or "Decision" or "Outcome" or "Method" or "MatchType")
                b.Property(property.Name).HasMaxLength(40);
            if (property.Name is "Number" or "InvoiceNumber" or "CreditNumber")
                b.Property(property.Name).HasMaxLength(80);
        }
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CreatedAt });
    }

    private static void Link<TChild, TParent>(ModelBuilder modelBuilder, string foreignId)
        where TChild : ProcurementRecord where TParent : ProcurementRecord =>
        modelBuilder.Entity<TChild>().HasOne<TParent>().WithMany()
            .HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), foreignId)
            .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id))
            .OnDelete(DeleteBehavior.Restrict);

    private static void ConfigureCommercialRelationships(ModelBuilder modelBuilder)
    {
        Link<SourcingLine, SourcingEvent>(modelBuilder, nameof(SourcingLine.SourcingEventId));
        Link<SourcingLine, PurchaseRequestLine>(modelBuilder, nameof(SourcingLine.PurchaseRequestLineId));
        Link<SourcingInvitation, SourcingEvent>(modelBuilder, nameof(SourcingInvitation.SourcingEventId));
        Link<SourcingInvitation, Supplier>(modelBuilder, nameof(SourcingInvitation.SupplierId));
        Link<SupplierBid, SourcingEvent>(modelBuilder, nameof(SupplierBid.SourcingEventId));
        Link<SupplierBid, Supplier>(modelBuilder, nameof(SupplierBid.SupplierId));
        Link<SupplierBid, SourcingInvitation>(modelBuilder, nameof(SupplierBid.InvitationId));
        Link<SupplierBidLine, SupplierBid>(modelBuilder, nameof(SupplierBidLine.SupplierBidId));
        Link<SupplierBidLine, SourcingLine>(modelBuilder, nameof(SupplierBidLine.SourcingLineId));
        Link<BidEvaluation, SupplierBid>(modelBuilder, nameof(BidEvaluation.SupplierBidId));
        Link<SourcingAward, SourcingEvent>(modelBuilder, nameof(SourcingAward.SourcingEventId));
        Link<SourcingAward, Supplier>(modelBuilder, nameof(SourcingAward.SupplierId));
        Link<SourcingAwardLine, SourcingAward>(modelBuilder, nameof(SourcingAwardLine.SourcingAwardId));
        Link<SourcingAwardLine, SourcingLine>(modelBuilder, nameof(SourcingAwardLine.SourcingLineId));
        Link<SupplierContract, Supplier>(modelBuilder, nameof(SupplierContract.SupplierId));
        Link<SupplierContract, SourcingAward>(modelBuilder, nameof(SupplierContract.SourcingAwardId));
        Link<SupplierContractVersion, SupplierContract>(modelBuilder, nameof(SupplierContractVersion.SupplierContractId));
        Link<ContractRate, SupplierContractVersion>(modelBuilder, nameof(ContractRate.SupplierContractVersionId));
        Link<ContractRate, CatalogItem>(modelBuilder, nameof(ContractRate.CatalogItemId));
        Link<PurchaseOrder, Supplier>(modelBuilder, nameof(PurchaseOrder.SupplierId));
        Link<PurchaseOrder, SourcingAward>(modelBuilder, nameof(PurchaseOrder.SourcingAwardId));
        Link<PurchaseOrder, SupplierContractVersion>(modelBuilder, nameof(PurchaseOrder.SupplierContractVersionId));
        Link<PurchaseOrderLine, PurchaseOrder>(modelBuilder, nameof(PurchaseOrderLine.PurchaseOrderId));
        Link<PurchaseOrderLine, PurchaseRequestLine>(modelBuilder, nameof(PurchaseOrderLine.PurchaseRequestLineId));
        Link<PurchaseOrderLine, CatalogItem>(modelBuilder, nameof(PurchaseOrderLine.CatalogItemId));
        Link<PurchaseOrderRevision, PurchaseOrder>(modelBuilder, nameof(PurchaseOrderRevision.PurchaseOrderId));
        Link<SupplierOrderAcknowledgement, PurchaseOrder>(modelBuilder, nameof(SupplierOrderAcknowledgement.PurchaseOrderId));
        Link<GoodsReceipt, PurchaseOrder>(modelBuilder, nameof(GoodsReceipt.PurchaseOrderId));
        Link<GoodsReceiptLine, GoodsReceipt>(modelBuilder, nameof(GoodsReceiptLine.GoodsReceiptId));
        Link<GoodsReceiptLine, PurchaseOrderLine>(modelBuilder, nameof(GoodsReceiptLine.PurchaseOrderLineId));
        Link<ReceiptInspection, GoodsReceiptLine>(modelBuilder, nameof(ReceiptInspection.GoodsReceiptLineId));
        Link<ServiceAcceptance, PurchaseOrderLine>(modelBuilder, nameof(ServiceAcceptance.PurchaseOrderLineId));
        Link<SupplierReturn, GoodsReceiptLine>(modelBuilder, nameof(SupplierReturn.GoodsReceiptLineId));
        Link<SupplierInvoice, Supplier>(modelBuilder, nameof(SupplierInvoice.SupplierId));
        Link<SupplierInvoice, PurchaseOrder>(modelBuilder, nameof(SupplierInvoice.PurchaseOrderId));
        Link<SupplierInvoiceLine, SupplierInvoice>(modelBuilder, nameof(SupplierInvoiceLine.SupplierInvoiceId));
        Link<SupplierInvoiceLine, PurchaseOrderLine>(modelBuilder, nameof(SupplierInvoiceLine.PurchaseOrderLineId));
        Link<SupplierInvoiceLine, GoodsReceiptLine>(modelBuilder, nameof(SupplierInvoiceLine.GoodsReceiptLineId));
        Link<SupplierInvoiceLine, ServiceAcceptance>(modelBuilder, nameof(SupplierInvoiceLine.ServiceAcceptanceId));
        Link<InvoiceMatchResult, SupplierInvoice>(modelBuilder, nameof(InvoiceMatchResult.SupplierInvoiceId));
        Link<InvoiceMatchResult, PolicyVersion>(modelBuilder, nameof(InvoiceMatchResult.PolicyVersionId));
        Link<InvoiceException, SupplierInvoice>(modelBuilder, nameof(InvoiceException.SupplierInvoiceId));
        Link<FinanceInvoiceHandoff, SupplierInvoice>(modelBuilder, nameof(FinanceInvoiceHandoff.SupplierInvoiceId));
        Link<FinancePaymentStatusRef, SupplierInvoice>(modelBuilder, nameof(FinancePaymentStatusRef.SupplierInvoiceId));
        Link<SupplierCreditNoteRef, Supplier>(modelBuilder, nameof(SupplierCreditNoteRef.SupplierId));
        Link<SupplierCreditNoteRef, SupplierInvoice>(modelBuilder, nameof(SupplierCreditNoteRef.SupplierInvoiceId));
        Link<SupplierCreditNoteRef, SupplierReturn>(modelBuilder, nameof(SupplierCreditNoteRef.SupplierReturnId));

        modelBuilder.Entity<SourcingLine>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(30).HasDefaultValue("Active");
            b.Property(x => x.StatusReason).HasMaxLength(1000);
        });
        modelBuilder.Entity<ContractRate>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(30).HasDefaultValue("Active");
            b.Property(x => x.StatusReason).HasMaxLength(1000);
        });
        modelBuilder.Entity<SourcingEvent>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        modelBuilder.Entity<SourcingLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.LineNumber }).IsUnique();
        modelBuilder.Entity<SourcingInvitation>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SupplierId }).IsUnique();
        modelBuilder.Entity<SupplierBid>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SupplierId, x.SubmissionVersion }).IsUnique();
        modelBuilder.Entity<SupplierBidLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.SourcingLineId }).IsUnique();
        modelBuilder.Entity<BidEvaluation>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.EvaluatorSubjectId, x.Stage, x.CriterionCode }).IsUnique();
        modelBuilder.Entity<SourcingAwardLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingAwardId, x.SourcingLineId }).IsUnique();
        modelBuilder.Entity<SupplierContract>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        modelBuilder.Entity<SupplierContractVersion>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierContractId, x.RevisionNumber }).IsUnique();
        modelBuilder.Entity<ContractRate>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierContractVersionId, x.LineNumber }).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        modelBuilder.Entity<PurchaseOrderLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.LineNumber }).IsUnique();
        modelBuilder.Entity<PurchaseOrderLine>().Property(x => x.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<PurchaseOrderRevision>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.RevisionNumber }).IsUnique();
        modelBuilder.Entity<SupplierOrderAcknowledgement>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.RevisionNumber }).IsUnique();
        modelBuilder.Entity<GoodsReceipt>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        modelBuilder.Entity<SupplierInvoice>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.InvoiceNumber, x.CurrencyCode }).IsUnique();
        modelBuilder.Entity<SupplierInvoice>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.NormalizedInvoiceNumber, x.CurrencyCode }).IsUnique();
        modelBuilder.Entity<SupplierInvoiceLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId, x.LineNumber }).IsUnique();
        modelBuilder.Entity<FinanceInvoiceHandoff>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<FinancePaymentStatusRef>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.FinancePaymentRef }).IsUnique();
        modelBuilder.Entity<SupplierCreditNoteRef>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.CreditNumber }).IsUnique();
        modelBuilder.Entity<PurchaseOrderRevision>().Property(x => x.BeforeSnapshotJson).HasColumnType("jsonb");
        modelBuilder.Entity<PurchaseOrderRevision>().Property(x => x.AfterSnapshotJson).HasColumnType("jsonb");

        Link<ProcurementMethod, PolicyVersion>(modelBuilder, nameof(ProcurementMethod.PolicyVersionId));
        modelBuilder.Entity<ProcurementPlanLine>().HasOne<ProcurementPlan>().WithMany(x => x.Lines)
            .HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementPlanLine.ProcurementPlanId))
            .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id))
            .OnDelete(DeleteBehavior.Restrict);
        Link<ProcurementPlanLine, CatalogItem>(modelBuilder, nameof(ProcurementPlanLine.CatalogItemId));
        Link<SupplierPortalMembership, Supplier>(modelBuilder, nameof(SupplierPortalMembership.SupplierId));
        Link<SupplierPerformanceReview, Supplier>(modelBuilder, nameof(SupplierPerformanceReview.SupplierId));
        Link<SupplierPerformanceCriterion, SupplierPerformanceReview>(modelBuilder, nameof(SupplierPerformanceCriterion.SupplierPerformanceReviewId));
        modelBuilder.Entity<ProcurementCategory>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique();
        modelBuilder.Entity<ProcurementMethod>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique();
        modelBuilder.Entity<ProcurementPlan>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Number }).IsUnique();
        modelBuilder.Entity<ProcurementPlanLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ProcurementPlanId, x.LineNumber }).IsUnique();
        modelBuilder.Entity<SupplierPortalMembership>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.IdentitySubjectId }).IsUnique();
        modelBuilder.Entity<SupplierPerformanceReview>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierId, x.PeriodFrom, x.PeriodTo });
        modelBuilder.Entity<SupplierPerformanceCriterion>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierPerformanceReviewId, x.CriterionCode }).IsUnique();
        modelBuilder.Entity<ProcurementComment>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OwnerType, x.OwnerId, x.CreatedAt });
        modelBuilder.Entity<ProcurementSetupAssessment>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CapabilityKey, x.EvaluatedAt });
    }

    private string CurrentTenantId => scope.TenantId;
    private Guid CurrentLegalEntityId => scope.LegalEntityId;
    private static string ToSnake(string name) => string.Concat(name.Select((letter, index) =>
        index > 0 && char.IsUpper(letter) ? "_" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.SubjectId))
            throw new InvalidOperationException("An authenticated tenant, legal entity and actor are required to write Procurement data.");
        GuardSpendingAuthorityChanges();
        GuardApprovalOutcomeChanges();
        GuardApprovalExecution();
        GuardPurchaseOrderExecution();
        var now = DateTimeOffset.UtcNow;
        var changes = ChangeTracker.Entries<ProcurementRecord>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changes)
        {
            if (entry.State == EntityState.Deleted && entry.Entity is not ProcurementSpendingAuthorityCategory)
                throw new InvalidOperationException("Procurement records must be closed, cancelled or reversed; physical deletion is not allowed.");
            if (entry.State == EntityState.Modified && entry.Entity is AuditEvent or SupplierDecision or PolicyEvaluation or BudgetCheckSnapshot or PurchaseRequestApprovalSubmission
                or BidEvaluation or InvoiceMatchResult or FinancePaymentStatusRef or SupplierOrderAcknowledgement
                or ProcurementComment or ProcurementSetupAssessment or PurchaseRequestApprovalOutcome
                or PurchaseRequestFinanceReleaseRequest or PurchaseRequestFinanceReleaseConfirmation)
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
                entry.Entity.Version = checked(entry.OriginalValues.GetValue<long>(nameof(ProcurementRecord.Version)) + 1);
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
