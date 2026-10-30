using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SourcingManualContact> SourcingManualContacts => Set<SourcingManualContact>();
    public DbSet<SourcingManualQuoteVersion> SourcingManualQuoteVersions => Set<SourcingManualQuoteVersion>();

    private void ConfigureManualQuotations(ModelBuilder model)
    {
        ConfigureBase<SourcingManualContact>(model, "sourcing_manual_contacts");
        ConfigureBase<SourcingManualQuoteVersion>(model, "sourcing_manual_quote_versions");
        var contact = model.Entity<SourcingManualContact>();
        var version = model.Entity<SourcingManualQuoteVersion>();
        var bid = model.Entity<SupplierBid>();
        var line = model.Entity<SupplierBidLine>();
        bid.Property(x => x.PrincipalCaptureVersion).HasDefaultValue(0);
        bid.Property(x => x.SupplierQuoteReference).HasMaxLength(200);
        bid.Property(x => x.OfferValidityKind).HasMaxLength(20);
        bid.Property(x => x.SupplierOfferedPaymentCode).HasMaxLength(80);
        bid.Property(x => x.SupplierOfferedPaymentText).HasMaxLength(2000);
        bid.Property(x => x.WarrantyText).HasMaxLength(4000);
        bid.Property(x => x.DeliveryTermsText).HasMaxLength(2000);
        line.Property(x => x.PrincipalCaptureVersion).HasDefaultValue(0);
        line.Property(x => x.IsActive).HasDefaultValue(true);
        line.Property(x => x.DiscountKind).HasMaxLength(20);
        line.Property(x => x.DiscountValue).HasPrecision(18, 4).HasDefaultValue(0m);
        line.Property(x => x.TaxTreatment).HasMaxLength(20);
        line.Property(x => x.SupplierOfferedTaxCode).HasMaxLength(80);
        foreach (var entity in new[] { contact.Metadata, version.Metadata })
        {
            entity.FindProperty(nameof(SourcingManualContact.ActorPrincipalId))!.SetMaxLength(36);
            entity.FindProperty(nameof(SourcingManualContact.CommandSha256))!.SetMaxLength(64);
            entity.FindProperty(nameof(SourcingManualContact.ResponseSha256))!.SetMaxLength(64);
            entity.FindProperty(nameof(SourcingManualContact.ResponseJson))!.SetColumnType("text");
        }
        contact.Property(x => x.Channel).HasMaxLength(30);
        contact.Property(x => x.Reference).HasMaxLength(200);
        version.Property(x => x.Operation).HasMaxLength(20);
        version.Property(x => x.SnapshotJson).HasColumnType("text");
        version.Property(x => x.RequestJson).HasColumnType("text");
        version.Property(x => x.SnapshotSha256).HasMaxLength(64);
        version.Property(x => x.RequestSha256).HasMaxLength(64);
        version.Property(x => x.DocumentPinSetSha256).HasMaxLength(64);
        // Historical versions reference the stable bid ID/protocol, never the
        // mutable current bid.Version. Existing version uniqueness is separate.
        bid.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PrincipalCaptureVersion })
            .HasName("ak_quote_bid_protocol");
        line.HasOne<SupplierBid>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.PrincipalCaptureVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PrincipalCaptureVersion })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_line_protocol");
        version.HasOne<SupplierBid>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.SchemaVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PrincipalCaptureVersion })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_version_protocol");
        model.Entity<SourcingInvitation>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SupplierId, x.Id }).HasName("ak_quote_invitation_source");
        contact.HasOne<SourcingInvitation>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SupplierId, x.InvitationId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SupplierId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_contact_invitation");
        model.Entity<SourcingPublicationOpening>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.Id }).HasName("ak_quote_publication_opening");
        contact.HasOne<SourcingPublicationOpening>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.PublicationOpeningId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_contact_opening");
        model.Entity<SourcingPublicationDecision>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.OpeningId, x.Id }).HasName("ak_quote_publication_decision");
        contact.HasOne<SourcingPublicationDecision>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.PublicationOpeningId, x.PublicationDecisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.OpeningId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_contact_decision");
        Link<SourcingManualContact, SourcingEvent>(model, nameof(SourcingManualContact.SourcingEventId));
        Link<SourcingManualContact, Supplier>(model, nameof(SourcingManualContact.SupplierId));
        contact.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId,
            x.InvitationId, x.SupplierId, x.BranchId, x.Id }).HasName("ak_quote_contact_source");
        version.HasOne<SourcingManualContact>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.InvitationId, x.SupplierId, x.BranchId, x.ManualContactId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId,
                x.InvitationId, x.SupplierId, x.BranchId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_version_contact");
        version.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.Id })
            .HasName("ak_quote_version_bid");
        version.HasOne<SourcingManualQuoteVersion>().WithMany().HasForeignKey(x => new
            { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.PreviousVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_quote_version_previous");
        contact.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_quote_contact_actor_command");
        contact.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.InvitationId })
            .IsUnique().HasDatabaseName("ux_quote_contact_invitation");
        version.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_quote_version_actor_command");
        version.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.BidVersion })
            .IsUnique().HasDatabaseName("ux_quote_version_bid_version");
        const string principal = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string hash = " ~ '^[0-9a-f]{64}$'";
        contact.ToTable(t => t.HasCheckConstraint("ck_manual_contact_v2",
            "schema_version=2 AND event_version_at_capture>0 AND invitation_version_at_capture>1"
            + " AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND actor_principal_id" + principal + " AND command_sha256" + hash + " AND response_sha256" + hash
            + " AND channel IN ('EmailOutsideSystem','Physical','Telephone','Other')"
            + " AND char_length(btrim(reference)) BETWEEN 1 AND 200 AND reference=btrim(reference)"
            + " AND isfinite(reported_contact_at) AND isfinite(recorded_at) AND reported_contact_at<=recorded_at AND response_status_code=201 AND octet_length(response_json) BETWEEN 2 AND 65536"
            + " AND " + QuoteNonEmpty("id", "sourcing_event_id", "invitation_id", "supplier_id", "branch_id",
                "publication_opening_id", "publication_decision_id", "command_key")
            + " AND (actor_core_linked_worker_id IS NULL OR " + QuoteNonEmpty("actor_core_linked_worker_id") + ")"));
        var emptyPins = ProcurementEvidenceEncoding.Digest("[]");
        version.ToTable(t => t.HasCheckConstraint("ck_manual_quote_version_v3",
            "schema_version=2 AND snapshot_schema_version IN (2,3) AND bid_version>0"
            + " AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND actor_principal_id" + principal + " AND command_sha256" + hash
            + " AND response_sha256" + hash + " AND snapshot_sha256" + hash
            + " AND request_sha256" + hash + " AND document_pin_set_sha256" + hash
            + " AND event_version_at_capture>0 AND invitation_version_at_capture>0 AND isfinite(recorded_at)"
            + " AND octet_length(snapshot_json) BETWEEN 2 AND 8388608 AND octet_length(request_json) BETWEEN 2 AND 8388608"
            + " AND octet_length(response_json) BETWEEN 2 AND 65536"
            + " AND ((operation='Create' AND bid_version=1 AND previous_version_id IS NULL AND response_status_code=201)"
            + " OR (operation IN ('Revise','Cancel') AND bid_version>1 AND previous_version_id IS NOT NULL AND response_status_code=200))"
            + " AND (previous_version_id IS NULL OR previous_version_id<>id)"
            + " AND ((document_pin_count=0 AND document_use_key IS NULL AND document_pin_set_sha256='" + emptyPins + "')"
            + " OR (document_pin_count BETWEEN 1 AND 100 AND document_use_key IS NOT NULL AND " + QuoteNonEmpty("document_use_key") + "))"
            + " AND " + QuoteNonEmpty("id", "sourcing_event_id", "invitation_id", "supplier_id", "supplier_bid_id", "branch_id", "manual_contact_id", "command_key")
            + " AND (actor_core_linked_worker_id IS NULL OR " + QuoteNonEmpty("actor_core_linked_worker_id") + ")"));
        bid.ToTable(t => t.HasCheckConstraint("ck_supplier_bid_capture_v2",
            "principal_capture_version IN (0,2) AND (principal_capture_version=0 OR (source_channel='Manual'"
            + " AND status IN ('Draft','Cancelled') AND submission_version=1 AND submitted_at IS NULL"
            + " AND surrogate_entered_by IS NOT NULL AND surrogate_entered_by<>'00000000-0000-0000-0000-000000000000' AND surrogate_entered_by" + principal
            + " AND offer_validity_kind IS NOT NULL AND offer_validity_kind IN ('Unspecified','Fixed','OpenEnded')"
            + " AND ((offer_validity_kind='Fixed' AND valid_until IS NOT NULL AND (quote_date IS NULL OR valid_until>=quote_date))"
            + " OR (offer_validity_kind IN ('Unspecified','OpenEnded') AND valid_until IS NULL))))"));
        line.ToTable(t => t.HasCheckConstraint("ck_supplier_bid_line_capture_v2",
            "principal_capture_version IN (0,2) AND (principal_capture_version=0 OR (quantity>0 AND unit_price>=0 AND tax_amount>=0 AND delivery_amount>=0"
            + " AND discount_kind IS NOT NULL AND discount_kind IN ('None','Amount','Percent') AND discount_value>=0"
            + " AND ((discount_kind='None' AND discount_value=0) OR discount_kind='Amount' OR (discount_kind='Percent' AND discount_value<=100))"
            + " AND tax_treatment IS NOT NULL AND tax_treatment IN ('Exclusive','Inclusive')"
            + " AND (CASE WHEN discount_kind='Percent' THEN quantity*unit_price*discount_value/100 ELSE discount_value END)<=quantity*unit_price"
            + " AND (tax_treatment='Exclusive' OR tax_amount<=quantity*unit_price-(CASE WHEN discount_kind='Percent' THEN quantity*unit_price*discount_value/100 ELSE discount_value END))))"));
    }
    private static string QuoteNonEmpty(params string[] columns) => string.Join(" AND ", columns.Select(x =>
        x + " <> '00000000-0000-0000-0000-000000000000'::uuid"));
}
