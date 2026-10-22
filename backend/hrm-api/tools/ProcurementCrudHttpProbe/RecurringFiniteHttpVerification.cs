using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Npgsql;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    static async Task VerifyRecurringPlannerContext(HttpClient client)
    {
        var context = await RecurringGet(client, "/context");
        Check(context.GetProperty("principalId").GetGuid() == RecurringPlanner
            && context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
            "Recurring planner must be a real employee-free Core user.");
        Check(context.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!)
            .ToHashSet(StringComparer.Ordinal).SetEquals(RecurringPlannerActions.Split(',')), "Recurring planner's actual stored action set differs.");
        var admin = await Send(client, HttpMethod.Get, "/context", null, actor: SystemActor);
        Check(admin.GetProperty("permissions").EnumerateArray().Any(x => x.GetString() == "procurement-recurring-read")
            && admin.GetProperty("permissions").EnumerateArray().Any(x => x.GetString() == "procurement-recurring-edit"),
            "Independent other owner must have actual recurring actions, not an unrelated permission denial.");
        await Send(client, HttpMethod.Get, "/recurring-purchases", null, HttpStatusCode.Forbidden, actor: SystemReviewer);
    }

    static async Task VerifyFiniteRecurringHttp(HttpClient ownerClient)
    {
        stage = "distinct-finite-Procurement-database-login-actual-recurring-HTTP";
        var ownerSettings = new NpgsqlConnectionStringBuilder(connection);
        var runtime = new NpgsqlConnectionStringBuilder(recurringRuntimeConnection);
        Check(runtime.Database == ownerSettings.Database && runtime.Host == ownerSettings.Host && runtime.Port == ownerSettings.Port
            && runtime.Username != ownerSettings.Username && runtime.Username is { } runtimeRoleName && runtimeRoleName.StartsWith("procurement_plan_rt_", StringComparison.Ordinal),
            "Finite recurring connection must be a separate private login on the same exact store.");
        await using var owner = new NpgsqlConnection(connection); await owner.OpenAsync();
        await using (var finite = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role AND rolcanlogin AND NOT rolsuper
                AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls)
              AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.member WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_roles r ON r.oid=c.relowner WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_namespace n JOIN pg_roles r ON r.oid=n.nspowner WHERE r.rolname=@role)
              AND NOT EXISTS(SELECT 1 FROM pg_database d JOIN pg_roles r ON r.oid=d.datdba WHERE r.rolname=@role)
            """, owner))
        {
            finite.Parameters.AddWithValue("role", runtime.Username!);
            Check(await finite.ExecuteScalarAsync() is true, "Recurring runtime must have no ownership, memberships or elevated role flags.");
        }
        static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var quoted = Q(runtime.Username!);
        var grants = $"""
            REVOKE CREATE,TEMPORARY ON DATABASE {Q(runtime.Database!)} FROM PUBLIC;
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            GRANT CONNECT ON DATABASE {Q(runtime.Database!)} TO {quoted};
            GRANT USAGE ON SCHEMA procurement TO {quoted};
            GRANT SELECT ON procurement.__procurement_migrations, procurement.suppliers, procurement.supplier_contracts TO {quoted};
            GRANT SELECT,INSERT,UPDATE ON procurement.recurring_purchase_commitments TO {quoted};
            GRANT SELECT,INSERT ON procurement.audit_events TO {quoted};
            """;
        await using (var command = new NpgsqlCommand(grants, owner)) await command.ExecuteNonQueryAsync();
        await VerifyRecurringFiniteDenials();
        var contract = await RecurringSend(ownerClient, HttpMethod.Post, "/contracts", new {
            supplierId = approvedSupplier, title = "Finite runtime prerequisite", contractType = "Services", currencyCode = "ZMW",
            ceilingAmount = "12345678901234.5678", effectiveFrom = "2026-10-22", expiresOn = "2027-10-22"
        }, HttpStatusCode.Created);
        object Draft(string title, string amount) => new {
            supplierId = approvedSupplier, supplierContractId = Id(contract), title, frequency = "Monthly",
            firstCycleOn = "2026-10-22", nextCycleOn = "2026-11-22", expectedAmount = amount,
            ownerPrincipalId = SystemActor, ownerWorkerId = Worker // Ignored, never trusted ownership.
        };
        var protectedBefore = await RecurringProtectedOwners();
        await using var app = await Host(recurringRuntimeConnection, finiteProbe: true);
        using var client = app.GetTestClient();
        await VerifyRecurringPlannerContext(client);
        var saved = await RecurringSend(client, HttpMethod.Post, "/recurring-purchases", Draft("Finite recurring purchase", "12345678901234.5678"), HttpStatusCode.Created);
        var id = Id(saved); var path = "/recurring-purchases/" + id;
        Check(saved.GetProperty("ownerPrincipalId").GetGuid() == RecurringPlanner
            && saved.GetProperty("ownerWorkerId").ValueKind == JsonValueKind.Null && Version(saved) == 1
            && saved.GetProperty("exactExpectedAmount").GetString() == "12345678901234.5678"
            && saved.GetProperty("currencyCode").GetString() == "ZMW", "Finite create lost exact values, real Core owner or company currency.");
        Check(Id(await RecurringGet(client, path)) == id, "Finite GET failed owning reload.");
        Check((await RecurringGet(client, "/recurring-purchases")).GetProperty("items").EnumerateArray().Any(x => Id(x) == id), "Finite list omitted actual own plan.");
        saved = await RecurringSend(client, HttpMethod.Put, path, new {version = 1, draft = Draft("Finite changed", "0.0001")});
        Check(Version(saved) == 2, "Finite update did not consume exactly one version.");
        saved = await RecurringSend(client, HttpMethod.Put, path, new {version = 2, draft = Draft("Finite changed", "0.0001")});
        Check(Version(saved) == 3, "Finite unchanged update did not consume exactly one version.");
        var beforeDenied = await RecurringFootprint();
        await RecurringSend(client, HttpMethod.Put, path, new {version = 2, draft = Draft("Stale", "1")}, HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Get, path, null, HttpStatusCode.NotFound, actor: SystemActor);
        await Send(client, HttpMethod.Put, path, new {version = 3, draft = Draft("Other owner", "1")}, HttpStatusCode.NotFound, actor: SystemActor);
        await Send(client, HttpMethod.Post, path + "/close", new {version = 3, reason = "Other owner"}, HttpStatusCode.NotFound, actor: SystemActor);
        await Send(client, HttpMethod.Get, path, null, HttpStatusCode.Forbidden, actor: RecurringPlanner, selectedBranch: RecurringOtherBranch);
        await RecurringSend(client, HttpMethod.Get, path, null, HttpStatusCode.Forbidden, foreign: true);
        Check(beforeDenied == await RecurringFootprint(), "Finite denied scope/ownership/stale operations changed records or evidence.");
        await using (var core = Core())
        {
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == RecurringPlannerRole);
            role.PermissionsCsv = string.Join(',', RecurringPlannerActions.Split(',').Where(x => x != "procurement-recurring-edit"));
            await core.SaveChangesAsync();
        }
        try
        {
            beforeDenied = await RecurringFootprint();
            await RecurringSend(client, HttpMethod.Put, path, new {version = 3, draft = Draft("Revoked", "1")}, HttpStatusCode.Forbidden);
            await RecurringSend(client, HttpMethod.Post, path + "/close", new {version = 3, reason = "Revoked"}, HttpStatusCode.Forbidden);
            Check(beforeDenied == await RecurringFootprint(), "Revoked finite Core action changed Procurement facts.");
        }
        finally
        {
            await using var core = Core();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == RecurringPlannerRole)).PermissionsCsv = RecurringPlannerActions;
            await core.SaveChangesAsync();
        }
        saved = await RecurringSend(client, HttpMethod.Post, path + "/close", new {version = 3, reason = "Finite planning completed"});
        Check(Version(saved) == 4 && (await RecurringGet(client, path)).GetProperty("status").GetString() == "Closed", "Finite close/history lost CAS/state.");
        beforeDenied = await RecurringFootprint();
        await RecurringSend(client, HttpMethod.Put, path, new {version = 4, draft = Draft("Closed overwrite", "1")}, HttpStatusCode.Conflict);
        Check(beforeDenied == await RecurringFootprint(), "Finite closed denial changed evidence.");
        await using (var proof = Db())
        {
            var row = await proof.RecurringPurchaseCommitments.AsNoTracking().SingleAsync(x => x.Id == id);
            Check(row.OwnerPrincipalId == RecurringPlanner && row.OwnerWorkerId is null && row.Status == "Closed" && row.Version == 4 && row.ExpectedAmount == 0.0001m,
                "Independent owner reload disagrees with finite HTTP.");
            var updates = await proof.AuditEvents.AsNoTracking().Where(x => x.RecordId == id && x.Action == "PlanUpdated").Select(x => x.ChangedFieldsJson).ToArrayAsync();
            var pairs = new HashSet<(long,long)>();
            foreach(var audit in updates)
            {
                using var body = JsonDocument.Parse(audit ?? throw new ProbeFailure("Finite audit JSON missing."));
                pairs.Add((body.RootElement.GetProperty("before").GetProperty("Version").GetInt64(),body.RootElement.GetProperty("after").GetProperty("Version").GetInt64()));
            }
            Check(pairs.SetEquals(new[] {(1L,2L),(2L,3L)}), "Finite no-op audit does not reflect persisted CAS versions.");
        }
        Check(protectedBefore == await RecurringProtectedOwners(), "Finite planning created or changed another business owner's transactions.");
        await app.StopAsync();
        Console.WriteLine("PASS distinct finite Procurement database login actual recurring CRUD/CAS/no-op/close, real workerless Core actions/scope, unchanged downstream owners; Core projection remains separate owner connection.");
    }

    static async Task VerifyRecurringFiniteDenials()
    {
        await using var runtime = new NpgsqlConnection(recurringRuntimeConnection); await runtime.OpenAsync();
        await using (var who = new NpgsqlCommand("SELECT current_user", runtime))
            Check(await who.ExecuteScalarAsync() as string == new NpgsqlConnectionStringBuilder(recurringRuntimeConnection).Username, "Finite SQL must use the real distinct login, not SET ROLE/owner.");
        await using var core = Core(); var user = core.Model.FindEntityType(typeof(LocalUser))!;
        var table = StoreObjectIdentifier.Table(user.GetTableName()!, user.GetSchema());
        static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var userTable = Q(user.GetSchema() ?? "public") + "." + Q(user.GetTableName()!);
        var key = Q(user.FindProperty("Id")!.GetColumnName(table)!);
        var password = Q(user.FindProperty("PasswordHash")!.GetColumnName(table)!);
        var statements = new[] {
            "CREATE SCHEMA recurring_runtime_forbidden", "CREATE TABLE procurement.recurring_runtime_forbidden(id integer)", "CREATE TEMP TABLE recurring_runtime_forbidden(id integer)",
            $"UPDATE {userTable} SET {key}={key} WHERE FALSE", $"SELECT {password} FROM {userTable} LIMIT 0",
            "DELETE FROM procurement.recurring_purchase_commitments WHERE FALSE", "TRUNCATE procurement.recurring_purchase_commitments",
            "UPDATE procurement.audit_events SET version=version WHERE FALSE", "DELETE FROM procurement.audit_events WHERE FALSE", "TRUNCATE procurement.audit_events",
            "UPDATE procurement.spending_authority_identity_evidence SET version=version WHERE FALSE",
            "INSERT INTO procurement.finance_invoice_handoffs(id) SELECT gen_random_uuid() WHERE FALSE",
            "UPDATE procurement.finance_payment_status_refs SET version=version WHERE FALSE",
            "INSERT INTO procurement.outbox_events(id) SELECT gen_random_uuid() WHERE FALSE"
        };
        foreach (var sql in statements)
        {
            await using var tx = await runtime.BeginTransactionAsync();
            try { await using var command = new NpgsqlCommand(sql, runtime, tx); await command.ExecuteNonQueryAsync(); throw new ProbeFailure("Finite recurring runtime accepted a forbidden operation."); }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InsufficientPrivilege) { }
            finally { await tx.RollbackAsync(); }
        }
        Console.WriteLine($"PASS real finite recurring login identity and {statements.Length} independent rollback-only DDL/Core/Finance/append-evidence privilege denials.");
    }

    static async Task<string> RecurringProtectedOwners()
    {
        await using var db = Db();
        return JsonSerializer.Serialize(new { baseline = await CreditProtectedState(),
            orders = await db.PurchaseOrders.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            orderLines = await db.PurchaseOrderLines.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            budgets = await db.BudgetCheckSnapshots.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            requests = await db.PurchaseRequests.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync() });
    }
}
