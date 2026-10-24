using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P35ContractInternalReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_p35_contract_version_owner",
                schema: "procurement",
                table: "supplier_contract_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_id", "id" });

            migrationBuilder.CreateTable(
                name: "contract_review_policy_identity",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_contract_review_policy_identity", x => x.id);
                    table.UniqueConstraint("AK_contract_review_policy_identity_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p35_contract_policy_lineage", x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.id, x.stage });
                    table.CheckConstraint("ck_p35_contract_policy_identity", "schema_version=2 AND policy_key='SUPPLIER_CONTRACT_REVIEW'\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_p35_contract_policy_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "contract_review_policy_identity",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_contract_policy_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_contract_policy_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "contract_review_policy_identity",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_review_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_business_version = table.Column<long>(type: "bigint", nullable: false),
                    contract_version_business_version = table.Column<long>(type: "bigint", nullable: false),
                    submission_number = table.Column<int>(type: "integer", nullable: false),
                    review_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_pins_json = table.Column<string>(type: "text", nullable: false),
                    assignment_pins_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    terms_json = table.Column<string>(type: "text", nullable: false),
                    terms_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_contract_review_submissions", x => x.id);
                    table.UniqueConstraint("AK_contract_review_submissions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p35_contract_submission_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.contract_id, x.review_branch_id, x.core_case_id });
                    table.UniqueConstraint("ak_p35_contract_submission_scope", x => new { x.tenant_id, x.legal_entity_id, x.id, x.contract_id, x.review_branch_id });
                    table.CheckConstraint("ck_p35_submission", "schema_version=1 AND id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND contract_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND contract_version_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND review_branch_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND supplier_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND policy_key='SUPPLIER_CONTRACT_REVIEW'\nAND contract_business_version>0 AND contract_version_business_version>0\nAND submission_number>0 AND policy_version_number>0\nAND maker_principal_id IS NOT NULL\nAND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND maker_principal_id<>'00000000-0000-0000-0000-000000000000'\nAND (maker_worker_alias_id IS NULL OR maker_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid)\nAND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'\nAND assignment_pins_sha256 IS NOT NULL AND assignment_pins_sha256 ~ '^[0-9a-f]{64}$'\nAND assignment_pins_json IS NOT NULL AND length(assignment_pins_json)>2\nAND terms_sha256 IS NOT NULL AND terms_sha256 ~ '^[0-9a-f]{64}$'\nAND terms_json IS NOT NULL AND length(terms_json)>2\nAND isfinite(submitted_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_p35_submission_contract",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.contract_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contracts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_submission_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_submission_version",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.contract_id, x.contract_version_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contract_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_review_corrections",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_contract_version = table.Column<long>(type: "bigint", nullable: false),
                    to_contract_version = table.Column<long>(type: "bigint", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    corrected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_contract_review_corrections", x => x.id);
                    table.UniqueConstraint("AK_contract_review_corrections_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p35_correction_owner", x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.id, x.contract_id, x.review_branch_id });
                    table.CheckConstraint("ck_p35_correction", "id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND contract_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND contract_version_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND submission_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND review_branch_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND from_contract_version>0 AND to_contract_version=from_contract_version+1\nAND actor_principal_id IS NOT NULL\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id<>'00000000-0000-0000-0000-000000000000'\nAND (actor_worker_alias_id IS NULL OR actor_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid)\nAND reason IS NOT NULL AND length(trim(reason))>0 AND length(reason)<=2000\nAND isfinite(corrected_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_p35_correction_submission",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.contract_id, x.review_branch_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "contract_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "contract_id", "review_branch_id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_correction_version",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.contract_id, x.contract_version_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contract_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_review_votes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_contract_review_votes", x => x.id);
                    table.UniqueConstraint("AK_contract_review_votes_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p35_contract_vote_owner", x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.id, x.contract_id, x.review_branch_id });
                    table.CheckConstraint("ck_p35_vote", "id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_stage_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_assignment_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND core_decision_id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND actor_principal_id IS NOT NULL\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id<>'00000000-0000-0000-0000-000000000000'\nAND (actor_worker_alias_id IS NULL OR actor_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid)\nAND action IN ('Approve','Return','Reject')\nAND isfinite(decided_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_p35_vote_submission",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.contract_id, x.review_branch_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "contract_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "contract_id", "review_branch_id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_review_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    result_contract_version = table.Column<long>(type: "bigint", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_contract_review_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_contract_review_command_receipts_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p35_receipt", "id<>'00000000-0000-0000-0000-000000000000'::uuid\nAND command_key<>'00000000-0000-0000-0000-000000000000'::uuid\nAND actor_principal_id IS NOT NULL\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id<>'00000000-0000-0000-0000-000000000000'\nAND command_sha256 IS NOT NULL AND command_sha256 ~ '^[0-9a-f]{64}$'\nAND response_sha256 IS NOT NULL AND response_sha256 ~ '^[0-9a-f]{64}$'\nAND response_json IS NOT NULL AND length(response_json)>2\nAND ((operation='Submit' AND vote_id IS NULL AND correction_id IS NULL AND response_status=201)\n    OR (operation='Decide' AND vote_id IS NOT NULL AND correction_id IS NULL AND response_status=200)\n    OR (operation='Correction' AND vote_id IS NULL AND correction_id IS NOT NULL AND response_status=200))\nAND result_contract_version>0 AND isfinite(completed_at)\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_p35_receipt_correction",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.correction_id, x.contract_id, x.review_branch_id },
                        principalSchema: "procurement",
                        principalTable: "contract_review_corrections",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "submission_id", "id", "contract_id", "review_branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_receipt_submission",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.contract_id, x.review_branch_id },
                        principalSchema: "procurement",
                        principalTable: "contract_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "contract_id", "review_branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p35_receipt_vote",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.vote_id, x.contract_id, x.review_branch_id },
                        principalSchema: "procurement",
                        principalTable: "contract_review_votes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "submission_id", "id", "contract_id", "review_branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_command_receipts_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "contract_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "contract_id", "review_branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_command_receipts_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "contract_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "correction_id", "contract_id", "review_branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_command_receipts_tenant_id_legal_entity_id~2",
                schema: "procurement",
                table: "contract_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "vote_id", "contract_id", "review_branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_actor_key",
                schema: "procurement",
                table: "contract_review_command_receipts",
                columns: new[] { "tenant_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_corrections_tenant_id_legal_entity_id_contr~",
                schema: "procurement",
                table: "contract_review_corrections",
                columns: new[] { "tenant_id", "legal_entity_id", "contract_id", "contract_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_corrections_tenant_id_legal_entity_id_submi~",
                schema: "procurement",
                table: "contract_review_corrections",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "contract_id", "review_branch_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p35_submission_correction",
                schema: "procurement",
                table: "contract_review_corrections",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_policy_identity_tenant_id_legal_entity_id_~1",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_policy_identity_tenant_id_legal_entity_id_~2",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_policy_identity_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_policy_created",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_policy_published",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_policy_revision",
                schema: "procurement",
                table: "contract_review_policy_identity",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "stage", "policy_record_version_at_action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_submissions_tenant_id_legal_entity_id_contr~",
                schema: "procurement",
                table: "contract_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "contract_id", "contract_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_submissions_tenant_id_legal_entity_id_polic~",
                schema: "procurement",
                table: "contract_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_core_case",
                schema: "procurement",
                table: "contract_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_submission_number",
                schema: "procurement",
                table: "contract_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "contract_id", "submission_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_review_votes_tenant_id_legal_entity_id_submission_~",
                schema: "procurement",
                table: "contract_review_votes",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "contract_id", "review_branch_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p35_contract_core_decision",
                schema: "procurement",
                table: "contract_review_votes",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_review_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "contract_review_policy_identity",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "contract_review_corrections",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "contract_review_votes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "contract_review_submissions",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p35_contract_version_owner",
                schema: "procurement",
                table: "supplier_contract_versions");
        }
    }
}
