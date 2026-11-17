using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MobileWorkdayEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mobile_workday_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state_after = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    blockers = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_workday_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_mobile_workday_events_attendance_records_attendance_record_~",
                        column: x => x.attendance_record_id,
                        principalSchema: "hrm",
                        principalTable: "attendance_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mobile_workday_events_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mobile_workday_events_work_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "hrm",
                        principalTable: "work_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mobile_workday_events_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_attendance_record_id",
                schema: "hrm",
                table: "mobile_workday_events",
                column: "attendance_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_legal_entity_id",
                schema: "hrm",
                table: "mobile_workday_events",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_location_id",
                schema: "hrm",
                table: "mobile_workday_events",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_tenant_id_attendance_record_id_revisi~",
                schema: "hrm",
                table: "mobile_workday_events",
                columns: new[] { "tenant_id", "attendance_record_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_tenant_id_legal_entity_id_worker_id_r~",
                schema: "hrm",
                table: "mobile_workday_events",
                columns: new[] { "tenant_id", "legal_entity_id", "worker_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_tenant_id_worker_id_request_id",
                schema: "hrm",
                table: "mobile_workday_events",
                columns: new[] { "tenant_id", "worker_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mobile_workday_events_worker_id",
                schema: "hrm",
                table: "mobile_workday_events",
                column: "worker_id");
            if(ActiveProvider=="Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql(Data.MobileWorkdaySchema.GuardSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if(ActiveProvider=="Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("DO $guard$ BEGIN IF EXISTS(SELECT 1 FROM hrm.mobile_workday_events) THEN RAISE EXCEPTION 'Retain workday history; disable the feature instead of dropping evidence' USING ERRCODE='23514'; END IF; END $guard$;");
            migrationBuilder.DropTable(
                name: "mobile_workday_events",
                schema: "hrm");
            if(ActiveProvider=="Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("DROP FUNCTION IF EXISTS hrm.mobile_workday_evidence_guard();");
        }
    }
}
