using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreFederatedPrincipals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "credential_mode",
                schema: "hrm",
                table: "local_users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "local");

            migrationBuilder.AddCheckConstraint(
                name: "CK_local_users_credential_state",
                schema: "hrm",
                table: "local_users",
                sql: "(credential_mode = 'local' AND email IS NOT NULL AND length(trim(email)) > 0 AND normalized_email IS NOT NULL AND length(trim(normalized_email)) > 0 AND password_hash IS NOT NULL AND length(trim(password_hash)) > 0) OR (credential_mode = 'federated' AND email IS NULL AND normalized_email IS NULL AND password_hash IS NULL AND must_change_password = FALSE AND password_changed_at IS NULL)");

            migrationBuilder.Sql("""
                CREATE FUNCTION hrm.guard_local_user_credential_mode()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.credential_mode IS DISTINCT FROM OLD.credential_mode THEN
                        RAISE EXCEPTION 'Credential mode cannot be changed through ordinary account updates'
                            USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER trg_local_user_credential_mode
                    BEFORE UPDATE ON hrm.local_users
                    FOR EACH ROW EXECUTE FUNCTION hrm.guard_local_user_credential_mode();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never manufacture login addresses/passwords to force old NOT NULL
            // columns back onto real federated principals. Retain data on rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.local_users WHERE credential_mode = 'federated') THEN
                        RAISE EXCEPTION 'Federated principals are retained; roll back code without dropping credential mode'
                            USING ERRCODE = '23514';
                    END IF;
                END; $$;
                DROP TRIGGER trg_local_user_credential_mode ON hrm.local_users;
                DROP FUNCTION hrm.guard_local_user_credential_mode();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_local_users_credential_state",
                schema: "hrm",
                table: "local_users");

            migrationBuilder.DropColumn(
                name: "credential_mode",
                schema: "hrm",
                table: "local_users");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "hrm",
                table: "local_users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
