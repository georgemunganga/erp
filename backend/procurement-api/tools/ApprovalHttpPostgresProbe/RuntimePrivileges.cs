using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

/// <summary>Disposable-database rehearsal only. Never provision production roles with this tool.</summary>
internal static partial class RuntimePrivileges
{
    public static void ValidateConnections(string runtime, string bootstrap)
    {
        var app = new NpgsqlConnectionStringBuilder(runtime); var owner = new NpgsqlConnectionStringBuilder(bootstrap);
        if (app.Database is null || !app.Database.StartsWith("proc_approval_http_", StringComparison.Ordinal)
            || app.Database != owner.Database || app.Host != owner.Host || app.Port != owner.Port
            || string.IsNullOrWhiteSpace(app.Username) || app.Username == owner.Username
            || !app.Username.StartsWith("proc_approval_runtime_", StringComparison.Ordinal))
            throw new InvalidOperationException("Distinct bootstrap/runtime identities on the same guarded disposable database are required; runtime role must use proc_approval_runtime_ prefix.");
    }

    public static async Task ProvisionAndVerifyAsync(string bootstrap, string runtime, IServiceProvider fixture, bool migrations, bool orders = false, bool receiving = false, bool matching = false, bool matchingDocuments = false, bool rfq = false)
    {
        ValidateConnections(runtime, bootstrap);
        var options = new NpgsqlConnectionStringBuilder(runtime); var role = options.Username!;
        await using var owner = new NpgsqlConnection(bootstrap); await owner.OpenAsync();
        await using (var check = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role AND rolcanlogin
              AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls)
              AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.member WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_namespace n JOIN pg_roles r ON r.oid=n.nspowner WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_roles r ON r.oid=c.relowner WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_database d JOIN pg_roles r ON r.oid=d.datdba WHERE r.rolname=@role)
            """, owner))
        {
            check.Parameters.AddWithValue("role", role);
            if (await check.ExecuteScalarAsync() is not true)
                throw new InvalidOperationException("Runtime must be an existing unprivileged login with no role memberships or object ownership.");
        }
        var quotedRole = Quote(role);
        await Sql(owner, $"REVOKE CREATE,TEMPORARY ON DATABASE {Quote(options.Database!)} FROM PUBLIC");
        await Sql(owner, $"REVOKE ALL ON DATABASE {Quote(options.Database!)} FROM {quotedRole}");
        await Sql(owner, $"GRANT CONNECT ON DATABASE {Quote(options.Database!)} TO {quotedRole}");
        foreach (var schema in new[] { "hrm", "procurement", "core_approval", "public" })
        {
            await Sql(owner, $"REVOKE CREATE ON SCHEMA {Quote(schema)} FROM PUBLIC");
            await Sql(owner, $"REVOKE ALL ON SCHEMA {Quote(schema)} FROM {quotedRole}");
            await Sql(owner, $"REVOKE ALL ON ALL TABLES IN SCHEMA {Quote(schema)} FROM {quotedRole}");
            await Sql(owner, $"REVOKE ALL ON ALL SEQUENCES IN SCHEMA {Quote(schema)} FROM {quotedRole}");
            await Sql(owner, $"GRANT USAGE ON SCHEMA {Quote(schema)} TO {quotedRole}");
        }
        // Fresh fixture schemas must not have PUBLIC table grants which defeat this role boundary.
        foreach (var schema in new[] { "hrm", "procurement", "core_approval" })
            await Sql(owner, $"REVOKE ALL ON ALL TABLES IN SCHEMA {Quote(schema)} FROM PUBLIC");
        using var scope = fixture.CreateScope();
        var hrm = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        var procurement = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var approvals = scope.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        async Task Columns<T>(params string[] names)
        {
            var entity = hrm.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Missing canonical fixture model.");
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = names.Select(n => Quote(entity.FindProperty(n)?.GetColumnName(table)
                ?? throw new InvalidOperationException("Unmapped canonical read column.")));
            await Sql(owner, $"GRANT SELECT ({string.Join(',', columns)}) ON {Table(entity)} TO {quotedRole}");
        }
        await Columns<LocalUser>("Id", "TenantId", "WorkerId", "RolesCsv", "IsActive", "IsArchived", "LockedUntil", "DisplayName");
        await Columns<Worker>("Id", "TenantId", "SubjectId", "LocationId", "OrgUnitId", "ManagerId", "Status", "IsArchived");
        await Columns<LegalEntity>("Id", "TenantId", "IsArchived", "IsDefault", "RegisteredName", "Code", "Currency");
        await Columns<WorkLocation>("Id", "TenantId", "IsArchived", "LegalEntityId", "Code", "Name");
        await Columns<OrgUnit>("Id", "TenantId", "IsArchived", "LegalEntityId", "Code", "Name", "Status", "EffectiveFrom", "EffectiveTo");
        await Columns<HrUserBranchAssignment>("Id", "TenantId", "IsArchived", "UserId", "LocationId");
        await Columns<TenantRoleAssignment>("TenantId", "IsArchived", "Active", "RoleKey", "PermissionsCsv");
        async Task Grant(DbContext db, Type type, string privileges)
        {
            var entity = db.Model.FindEntityType(type) ?? throw new InvalidOperationException("Missing fixture model.");
            await Sql(owner, $"GRANT {privileges} ON {Table(entity)} TO {quotedRole}");
        }
        foreach (var type in new[] { typeof(ApprovalCase), typeof(ApprovalStage), typeof(ApprovalAssignment) })
            await Grant(approvals, type, "SELECT,INSERT,UPDATE");
        await Grant(approvals, typeof(ApprovalDecision), "SELECT,INSERT");
        await Grant(approvals, typeof(Mightyfin.Erp.Core.Approvals.Domain.ApprovalDelegation), "SELECT");
        foreach (var type in new[] { typeof(PurchaseRequest), typeof(PurchaseRequestLine), typeof(ProcurementSpendingAuthorityGrant) })
            await Grant(procurement, type, "SELECT,INSERT,UPDATE");
        // Draft-only replacement children require DELETE; parent-state guards remain enforced by the application.
        await Grant(procurement, typeof(ProcurementSpendingAuthorityCategory), "SELECT,INSERT,DELETE");
        foreach (var type in new[] { typeof(PolicyEvaluation), typeof(BudgetCheckSnapshot), typeof(PurchaseRequestApprovalSubmission),
            typeof(PurchaseRequestAssignmentAuthorityEvidence), typeof(ProcurementApprovalCommandReceipt), typeof(PurchaseRequestApprovalOutcome),
            typeof(PurchaseRequestFinanceReleaseRequest), typeof(PurchaseRequestFinanceReleaseConfirmation), typeof(AuditEvent), typeof(OutboxEvent) })
            await Grant(procurement, type, "SELECT,INSERT");
        foreach (var type in new[] { typeof(ProcurementCategory), typeof(PolicyVersion), typeof(CatalogItem), typeof(CatalogEntry),
            typeof(Supplier), typeof(SupplierQualification), typeof(PurchaseRequestAllocation) })
            await Grant(procurement, type, "SELECT");
        if (orders)
        {
            foreach (var type in new[] { typeof(PurchaseOrder), typeof(PurchaseOrderLine), typeof(PurchaseRequestAllocation) })
                await Grant(procurement, type, "SELECT,INSERT,UPDATE");
            await Grant(procurement, typeof(ProcurementMethod), "SELECT");
            foreach (var type in new[] { typeof(PurchaseOrderFinanceEvidence), typeof(PurchaseOrderCommitmentOwnership),
                typeof(PurchaseOrderSubmission), typeof(PurchaseOrderAssignmentAuthorityEvidence), typeof(PurchaseOrderIssuedVersion),
                typeof(PurchaseOrderDeliveryEvidence), typeof(PurchaseOrderFinanceResolution), typeof(ProcurementCommercialCommandReceipt) })
                await Grant(procurement, type, "SELECT,INSERT");
        }
        if (rfq)
        {
            foreach (var type in new[] { typeof(SourcingEvent), typeof(SourcingLine), typeof(PolicyVersion) })
                await Grant(procurement, type, "SELECT,INSERT,UPDATE");
            foreach (var type in new[] { typeof(SourcingDraftEvidence), typeof(SourcingDemandBinding), typeof(SourcingCommandReceipt) })
                await Grant(procurement, type, "SELECT,INSERT");
        }
        if (receiving)
        {
            await Grant(procurement, typeof(ProcurementAttachmentRef), "SELECT");
            foreach (var type in new[] { typeof(ReceivingSourceState), typeof(GoodsReceipt), typeof(GoodsReceiptLine), typeof(ServiceAcceptance), typeof(PolicyVersion) })
                await Grant(procurement, type, "SELECT,INSERT,UPDATE");
            foreach (var type in new[] { typeof(ReceivingAcceptanceEffect), typeof(ReceiptInspection) })
                await Grant(procurement, type, "SELECT,INSERT");
        }
        if (matching)
        {
            foreach (var type in new[] { typeof(SupplierInvoice), typeof(InvoiceMatchExposureState), typeof(InvoiceException) })
                await Grant(procurement,type,"SELECT,INSERT,UPDATE");
            foreach (var type in new[] { typeof(SupplierInvoiceLine), typeof(InvoiceMatchMappingVersion), typeof(InvoiceMatchAllocation), typeof(InvoiceMatchResult) })
                await Grant(procurement,type,"SELECT,INSERT");
        }
        if (migrations)
        {
            await Sql(owner, $"GRANT SELECT ON procurement.__procurement_migrations TO {quotedRole}");
            await Sql(owner, $"GRANT SELECT ON core_approval.\"__EFMigrationsHistory\" TO {quotedRole}");
        }
        if(matchingDocuments) await GrantMatchingDocumentsAsync(owner,quotedRole,fixture);
        await AssertDeniedAsync(runtime, fixture, matching, rfq);
        if(matchingDocuments) await AssertMatchingDocumentWritesDeniedAsync(runtime,fixture);
    }

    public static async Task AssertDeniedAsync(string runtime, IServiceProvider fixture, bool matching = false, bool rfq = false)
    {
        using var services = fixture.CreateScope();
        var hrm = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        var local = hrm.Model.FindEntityType(typeof(LocalUser))!;
        var localTable = StoreObjectIdentifier.Table(local.GetTableName()!, local.GetSchema());
        var id = Quote(local.FindProperty("Id")!.GetColumnName(localTable)!);
        var password = Quote(local.FindProperty("PasswordHash")!.GetColumnName(localTable)!);
        var procurement = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var immutable = new[] { typeof(PurchaseRequestApprovalSubmission), typeof(PolicyEvaluation), typeof(BudgetCheckSnapshot),
            typeof(PurchaseRequestAssignmentAuthorityEvidence), typeof(ProcurementApprovalCommandReceipt), typeof(PurchaseRequestApprovalOutcome),
            typeof(PurchaseRequestFinanceReleaseRequest), typeof(PurchaseRequestFinanceReleaseConfirmation), typeof(AuditEvent), typeof(OutboxEvent) };
        var forbidden = new List<string>
        {
            "CREATE SCHEMA probe_runtime_forbidden", "CREATE TABLE core_approval.probe_runtime_forbidden(id integer)",
            "CREATE TABLE procurement.probe_runtime_forbidden(id integer)",
            "CREATE TABLE hrm.probe_runtime_forbidden(id integer)", "CREATE TEMP TABLE probe_runtime_forbidden(id integer)",
            $"UPDATE {Table(local)} SET {id}={id} WHERE FALSE", $"DELETE FROM {Table(local)} WHERE FALSE",
            $"SELECT {password} FROM {Table(local)} LIMIT 0",
            "UPDATE core_approval.decisions SET \"Id\"=\"Id\" WHERE FALSE", "DELETE FROM core_approval.decisions WHERE FALSE",
        };
        foreach (var type in immutable.Concat(matching ? new[] { typeof(InvoiceMatchMappingVersion), typeof(InvoiceMatchAllocation), typeof(InvoiceMatchResult) } : []).Concat(rfq ? new[] { typeof(SourcingDraftEvidence), typeof(SourcingDemandBinding), typeof(SourcingCommandReceipt) } : []))
        {
            var entity = procurement.Model.FindEntityType(type)!;
            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var key = Quote(entity.FindProperty("Id")!.GetColumnName(store)!);
            forbidden.Add($"UPDATE {Table(entity)} SET {key}={key} WHERE FALSE");
            forbidden.Add($"DELETE FROM {Table(entity)} WHERE FALSE");
        }
        foreach (var type in new[] { typeof(Worker), typeof(LegalEntity), typeof(TenantRoleAssignment) })
        {
            var entity = hrm.Model.FindEntityType(type)!;
            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var key = Quote(entity.FindProperty("Id")!.GetColumnName(store)!);
            forbidden.Add($"UPDATE {Table(entity)} SET {key}={key} WHERE FALSE");
            forbidden.Add($"DELETE FROM {Table(entity)} WHERE FALSE");
        }
        await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync();
        foreach (var statement in forbidden)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(statement, connection, transaction);
                await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Runtime role unexpectedly accepted a forbidden database operation.");
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InsufficientPrivilege) { }
            finally { await transaction.RollbackAsync(); }
        }
    }

    private static string Table(IEntityType entity) => $"{Quote(entity.GetSchema() ?? "public")}.{Quote(entity.GetTableName()!)}";
    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    private static async Task Sql(NpgsqlConnection connection, string sql)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
}
