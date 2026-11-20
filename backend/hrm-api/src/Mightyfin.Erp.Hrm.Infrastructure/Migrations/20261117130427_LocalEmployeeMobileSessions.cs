using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocalEmployeeMobileSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_mobile_sessions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    password_version = table.Column<string>(type: "text", nullable: false),
                    access_hash = table.Column<string>(type: "text", nullable: false),
                    refresh_hash = table.Column<string>(type: "text", nullable: false),
                    access_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_mobile_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_local_mobile_sessions_legal_entities_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_local_mobile_sessions_local_users_tenant_id_local_user_id",
                        columns: x => new { x.tenant_id, x.local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_local_mobile_sessions_access_hash",
                schema: "hrm",
                table: "local_mobile_sessions",
                column: "access_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_local_mobile_sessions_refresh_hash",
                schema: "hrm",
                table: "local_mobile_sessions",
                column: "refresh_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_local_mobile_sessions_tenant_id_legal_entity_id",
                schema: "hrm",
                table: "local_mobile_sessions",
                columns: new[] { "tenant_id", "legal_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_local_mobile_sessions_tenant_id_local_user_id_expires_at",
                schema: "hrm",
                table: "local_mobile_sessions",
                columns: new[] { "tenant_id", "local_user_id", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "local_mobile_sessions",
                schema: "hrm");
        }
    }
}
