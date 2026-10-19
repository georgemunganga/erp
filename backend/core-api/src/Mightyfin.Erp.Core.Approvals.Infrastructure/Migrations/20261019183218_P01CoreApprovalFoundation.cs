using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Core.Approvals.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P01CoreApprovalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core_approval");

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "core_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MakerSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletionKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => x.Id);
                    table.UniqueConstraint("AK_cases_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "core_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatorSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DelegateSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegations", x => x.Id);
                    table.UniqueConstraint("AK_delegations_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "stages",
                schema: "core_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.Id);
                    table.UniqueConstraint("AK_stages_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                    table.ForeignKey(
                        name: "FK_stages_cases_TenantId_LegalEntityId_CaseId",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.CaseId },
                        principalSchema: "core_approval",
                        principalTable: "cases",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "core_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DelegatedFromSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PermissionKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                    table.UniqueConstraint("AK_assignments_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                    table.ForeignKey(
                        name: "FK_assignments_stages_TenantId_LegalEntityId_StageId",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.StageId },
                        principalSchema: "core_approval",
                        principalTable: "stages",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisions",
                schema: "core_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisions", x => x.Id);
                    table.UniqueConstraint("AK_decisions_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                    table.ForeignKey(
                        name: "FK_decisions_assignments_TenantId_LegalEntityId_AssignmentId",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.AssignmentId },
                        principalSchema: "core_approval",
                        principalTable: "assignments",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_TenantId_LegalEntityId_CaseId_AssigneeSubjectId~",
                schema: "core_approval",
                table: "assignments",
                columns: new[] { "TenantId", "LegalEntityId", "CaseId", "AssigneeSubjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_TenantId_LegalEntityId_StageId",
                schema: "core_approval",
                table: "assignments",
                columns: new[] { "TenantId", "LegalEntityId", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_cases_TenantId_LegalEntityId_SourceModule_SourceType_Source~",
                schema: "core_approval",
                table: "cases",
                columns: new[] { "TenantId", "LegalEntityId", "SourceModule", "SourceType", "SourceId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decisions_TenantId_LegalEntityId_AssignmentId",
                schema: "core_approval",
                table: "decisions",
                columns: new[] { "TenantId", "LegalEntityId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_decisions_TenantId_LegalEntityId_CaseId_IdempotencyKey",
                schema: "core_approval",
                table: "decisions",
                columns: new[] { "TenantId", "LegalEntityId", "CaseId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delegations_TenantId_LegalEntityId_DelegatorSubjectId_Permi~",
                schema: "core_approval",
                table: "delegations",
                columns: new[] { "TenantId", "LegalEntityId", "DelegatorSubjectId", "PermissionKey", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_stages_TenantId_LegalEntityId_CaseId_Sequence",
                schema: "core_approval",
                table: "stages",
                columns: new[] { "TenantId", "LegalEntityId", "CaseId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "decisions",
                schema: "core_approval");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "core_approval");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "core_approval");

            migrationBuilder.DropTable(
                name: "stages",
                schema: "core_approval");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "core_approval");
        }
    }
}
