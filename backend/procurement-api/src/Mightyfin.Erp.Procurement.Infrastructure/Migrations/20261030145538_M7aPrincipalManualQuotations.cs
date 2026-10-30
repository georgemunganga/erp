using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7aPrincipalManualQuotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_publication_decisions");

            migrationBuilder.RenameIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_i~",
                schema: "procurement",
                table: "supplier_bid_lines",
                newName: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_~1");

            migrationBuilder.AddColumn<DateOnly>(
                name: "delivery_date",
                schema: "procurement",
                table: "supplier_bids",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_terms_text",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "offer_validity_kind",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "principal_capture_version",
                schema: "procurement",
                table: "supplier_bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "quote_date",
                schema: "procurement",
                table: "supplier_bids",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_offered_payment_code",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_offered_payment_text",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_quote_reference",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "valid_until",
                schema: "procurement",
                table: "supplier_bids",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warranty_text",
                schema: "procurement",
                table: "supplier_bids",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_kind",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_value",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "principal_capture_version",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "supplier_offered_tax_code",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_treatment",
                schema: "procurement",
                table: "supplier_bid_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_quote_bid_protocol",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "principal_capture_version" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_quote_publication_opening",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_quote_publication_decision",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "opening_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_quote_invitation_source",
                schema: "procurement",
                table: "sourcing_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "supplier_id", "id" });

            migrationBuilder.CreateTable(
                name: "sourcing_manual_contacts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_version_at_capture = table.Column<long>(type: "bigint", nullable: false),
                    invitation_version_at_capture = table.Column<long>(type: "bigint", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reported_contact_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sourcing_manual_contacts", x => x.id);
                    table.UniqueConstraint("ak_quote_contact_source", x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.invitation_id, x.supplier_id, x.branch_id, x.id });
                    table.UniqueConstraint("AK_sourcing_manual_contacts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_contact_v2", "schema_version=2 AND event_version_at_capture>0 AND invitation_version_at_capture>1 AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND channel IN ('EmailOutsideSystem','Physical','Telephone','Other') AND char_length(btrim(reference)) BETWEEN 1 AND 200 AND reference=btrim(reference) AND isfinite(reported_contact_at) AND isfinite(recorded_at) AND reported_contact_at<=recorded_at AND response_status_code=201 AND octet_length(response_json) BETWEEN 2 AND 65536 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invitation_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND publication_opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND publication_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_sourcing_manual_contacts_sourcing_events_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_manual_contacts_suppliers_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_contact_decision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.publication_opening_id, x.publication_decision_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "opening_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_contact_invitation",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.supplier_id, x.invitation_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_invitations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "supplier_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_contact_opening",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.publication_opening_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_manual_quote_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bid_version = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_schema_version = table.Column<int>(type: "integer", nullable: false),
                    event_version_at_capture = table.Column<long>(type: "bigint", nullable: false),
                    invitation_version_at_capture = table.Column<long>(type: "bigint", nullable: false),
                    request_json = table.Column<string>(type: "text", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_use_key = table.Column<Guid>(type: "uuid", nullable: true),
                    document_pin_count = table.Column<int>(type: "integer", nullable: false),
                    document_pin_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sourcing_manual_quote_versions", x => x.id);
                    table.UniqueConstraint("ak_quote_version_bid", x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id, x.id });
                    table.UniqueConstraint("AK_sourcing_manual_quote_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_quote_version_v3", "schema_version=2 AND snapshot_schema_version IN (2,3) AND bid_version>0 AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND request_sha256 ~ '^[0-9a-f]{64}$' AND document_pin_set_sha256 ~ '^[0-9a-f]{64}$' AND event_version_at_capture>0 AND invitation_version_at_capture>0 AND isfinite(recorded_at) AND octet_length(snapshot_json) BETWEEN 2 AND 8388608 AND octet_length(request_json) BETWEEN 2 AND 8388608 AND octet_length(response_json) BETWEEN 2 AND 65536 AND ((operation='Create' AND bid_version=1 AND previous_version_id IS NULL AND response_status_code=201) OR (operation IN ('Revise','Cancel') AND bid_version>1 AND previous_version_id IS NOT NULL AND response_status_code=200)) AND (previous_version_id IS NULL OR previous_version_id<>id) AND ((document_pin_count=0 AND document_use_key IS NULL AND document_pin_set_sha256='4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945') OR (document_pin_count BETWEEN 1 AND 100 AND document_use_key IS NOT NULL AND document_use_key <> '00000000-0000-0000-0000-000000000000'::uuid)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invitation_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_bid_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND manual_contact_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "fk_quote_version_contact",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.invitation_id, x.supplier_id, x.branch_id, x.manual_contact_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_manual_contacts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "invitation_id", "supplier_id", "branch_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_version_previous",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id, x.previous_version_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_manual_quote_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_version_protocol",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id, x.schema_version },
                        principalSchema: "procurement",
                        principalTable: "supplier_bids",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "principal_capture_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplier_bid_capture_v2",
                schema: "procurement",
                table: "supplier_bids",
                sql: "principal_capture_version IN (0,2) AND (principal_capture_version=0 OR (source_channel='Manual' AND status IN ('Draft','Cancelled') AND submission_version=1 AND submitted_at IS NULL AND surrogate_entered_by IS NOT NULL AND surrogate_entered_by<>'00000000-0000-0000-0000-000000000000' AND surrogate_entered_by ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND offer_validity_kind IS NOT NULL AND offer_validity_kind IN ('Unspecified','Fixed','OpenEnded') AND ((offer_validity_kind='Fixed' AND valid_until IS NOT NULL AND (quote_date IS NULL OR valid_until>=quote_date)) OR (offer_validity_kind IN ('Unspecified','OpenEnded') AND valid_until IS NULL))))");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_i~",
                schema: "procurement",
                table: "supplier_bid_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "principal_capture_version" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplier_bid_line_capture_v2",
                schema: "procurement",
                table: "supplier_bid_lines",
                sql: "principal_capture_version IN (0,2) AND (principal_capture_version=0 OR (quantity>0 AND unit_price>=0 AND tax_amount>=0 AND delivery_amount>=0 AND discount_kind IS NOT NULL AND discount_kind IN ('None','Amount','Percent') AND discount_value>=0 AND ((discount_kind='None' AND discount_value=0) OR discount_kind='Amount' OR (discount_kind='Percent' AND discount_value<=100)) AND tax_treatment IS NOT NULL AND tax_treatment IN ('Exclusive','Inclusive') AND (CASE WHEN discount_kind='Percent' THEN quantity*unit_price*discount_value/100 ELSE discount_value END)<=quantity*unit_price AND (tax_treatment='Exclusive' OR tax_amount<=quantity*unit_price-(CASE WHEN discount_kind='Percent' THEN quantity*unit_price*discount_value/100 ELSE discount_value END))))");

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_sourcin~1",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "supplier_id", "invitation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_sourcing~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "publication_opening_id", "publication_decision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_supplier~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ux_quote_contact_actor_command",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quote_contact_invitation",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "invitation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_s~1",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "schema_version" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "invitation_id", "supplier_id", "branch_id", "manual_contact_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_su~",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "previous_version_id" });

            migrationBuilder.CreateIndex(
                name: "ux_quote_version_actor_command",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quote_version_bid_version",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "bid_version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_quote_line_protocol",
                schema: "procurement",
                table: "supplier_bid_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "principal_capture_version" },
                principalSchema: "procurement",
                principalTable: "supplier_bids",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "principal_capture_version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Exact new-column defaults are rollback-safe. Once any quotation
            // data or retained evidence exists, retain the schema and correct
            // forward. Locks prevent a writer racing the admission check.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.sourcing_manual_contacts,
                  procurement.sourcing_manual_quote_versions,
                  procurement.supplier_bids,
                  procurement.supplier_bid_lines,
                  procurement.sourcing_invitations,
                  procurement.audit_events,
                  procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $m7a_quotation_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.sourcing_manual_contacts)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_manual_quote_versions)
                    OR EXISTS (SELECT 1 FROM procurement.supplier_bids
                      WHERE principal_capture_version<>0 OR supplier_quote_reference IS NOT NULL
                        OR quote_date IS NOT NULL OR offer_validity_kind IS NOT NULL OR valid_until IS NOT NULL
                        OR supplier_offered_payment_code IS NOT NULL OR supplier_offered_payment_text IS NOT NULL
                        OR warranty_text IS NOT NULL OR delivery_terms_text IS NOT NULL OR delivery_date IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM procurement.supplier_bid_lines
                      WHERE principal_capture_version<>0 OR is_active IS DISTINCT FROM TRUE
                        OR discount_kind IS NOT NULL OR discount_value<>0
                        OR tax_treatment IS NOT NULL OR supplier_offered_tax_code IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_invitations WHERE status='ManualContactRecorded')
                    OR EXISTS (SELECT 1 FROM procurement.audit_events
                      WHERE record_type IN ('SourcingManualContact','SourcingManualQuoteVersion')
                        OR (record_type='SupplierQuotation' AND action IN
                          ('OutsideContactRecorded','QuotationDraftCreate','QuotationDraftRevise','QuotationDraftCancel')))
                    OR EXISTS (SELECT 1 FROM procurement.outbox_events
                      WHERE event_type IN ('sourcing.OutsideContactRecorded.v3','sourcing.QuotationDraftCreate.v3',
                        'sourcing.QuotationDraftRevise.v3','sourcing.QuotationDraftCancel.v3'))
                  THEN
                    RAISE EXCEPTION 'M7A quotation data or evidence exists; retain this schema and use a forward correction.'
                      USING ERRCODE='55000';
                  END IF;
                END
                $m7a_quotation_down$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_quote_line_protocol",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropTable(
                name: "sourcing_manual_quote_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_manual_contacts",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_quote_bid_protocol",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplier_bid_capture_v2",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_i~",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplier_bid_line_capture_v2",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_quote_publication_opening",
                schema: "procurement",
                table: "sourcing_publication_openings");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_quote_publication_decision",
                schema: "procurement",
                table: "sourcing_publication_decisions");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_quote_invitation_source",
                schema: "procurement",
                table: "sourcing_invitations");

            migrationBuilder.DropColumn(
                name: "delivery_date",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "delivery_terms_text",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "offer_validity_kind",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "principal_capture_version",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "quote_date",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "supplier_offered_payment_code",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "supplier_offered_payment_text",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "supplier_quote_reference",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "valid_until",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "warranty_text",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "discount_kind",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropColumn(
                name: "discount_value",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropColumn(
                name: "principal_capture_version",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropColumn(
                name: "supplier_offered_tax_code",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.DropColumn(
                name: "tax_treatment",
                schema: "procurement",
                table: "supplier_bid_lines");

            migrationBuilder.RenameIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_~1",
                schema: "procurement",
                table: "supplier_bid_lines",
                newName: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_i~");

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });
        }
    }
}
