using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    /// <summary>Real HTTP/runtime connection; only synthetic Core fixture and deliberately
    /// UNBOUND legacy setup use bootstrap. No external Finance truth is manufactured.</summary>
    public async Task VerifyFinanceCheckerIdentity()
    {
        var request = await NewFundedRequest("Retained Finance checker identity actual HTTP proof");
        var id = request.GetProperty("id").GetGuid();
        var version = request.GetProperty("version").GetInt64();
        Guid originalWorker;
        Mightyfin.Erp.Procurement.Domain.BudgetCheckSnapshot original;
        using (var fixture = Scope(finance))
        {
            var db = fixture.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            original = await db.BudgetCheckSnapshots.AsNoTracking().SingleAsync(x => x.PurchaseRequestId == id);
            originalWorker = (await fixture.ServiceProvider.GetRequiredService<HrmDbContext>().LocalUsers
                .Where(x => x.Id == finance).Select(x => x.WorkerId).SingleAsync())!.Value;
        }
        Require(original.CheckerIdentitySchemaVersion == 1 && original.CheckedByWorkerId == originalWorker
            && FinanceCheckerIdentityEvidence.HasValidBinding(original)
            && original.CheckedAt.Ticks % 10 == 0,
            "Actual capture/reload did not retain canonical worker/hash and PostgreSQL microsecond timestamp.");
        var originalBytes = FinanceSnapshotCodec.Encode(original, 1);
        await VerifyFinanceCheckerBudgetChecks(original.Id);
        object Body(string reference) => new { requestVersion = version, financeCheckRef = reference,
            status = original.Status, checkedAmount = original.CheckedAmount, currencyCode = original.CurrencyCode,
            evidenceRef = original.EvidenceRef, decisionReason = original.DecisionReason,
            reservationRef = original.ReservationRef, reservationStatus = original.ReservationStatus };
        var path = $"{Root}/requests/{id}/budget-checks";
        var before = await FinanceIdentityFootprint();
        using var replay = await Send(HttpMethod.Post, path, finance, Body(original.FinanceCheckRef));
        await Check(replay, HttpStatusCode.OK);
        var replayJson = await replay.Content.ReadAsStringAsync();
        using var replayAgain = await Send(HttpMethod.Post, path, finance, Body(original.FinanceCheckRef));
        await Check(replayAgain, HttpStatusCode.OK);
        Require(replayJson == await replayAgain.Content.ReadAsStringAsync() && before == await FinanceIdentityFootprint(),
            "Exact Finance reference replay changed original response or persisted effects.");

        // A genuinely separate active fixture worker, not a copied identity master.
        Guid reboundWorker;
        using (var fixture = Scope(finance))
        {
            var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            var worker = new Worker { EmployeeNo = "HTTP-P27-REBIND", FirstName = "Rebound", LastName = "Fixture",
                Status = "active", LocationId = branch };
            core.Workers.Add(worker);
            var account = await core.LocalUsers.SingleAsync(x => x.Id == finance);
            account.WorkerId = worker.Id; reboundWorker = worker.Id;
            await core.SaveChangesAsync();
        }
        try
        {
            before = await FinanceIdentityFootprint();
            await Expect(HttpStatusCode.Conflict, HttpMethod.Post, path, finance, Body(original.FinanceCheckRef));
            Require(before == await FinanceIdentityFootprint(), "Subject rebind rewrote retained Finance evidence or audited a denied replay.");
        }
        finally
        {
            using var fixture = Scope(finance); var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            var account = await core.LocalUsers.SingleAsync(x => x.Id == finance);
            Require(account.WorkerId == reboundWorker, "Synthetic account changed unexpectedly during rebind proof.");
            account.WorkerId = originalWorker; await core.SaveChangesAsync();
        }
        string roleKey;
        string originalPermissions;
        using (var fixture = Scope(finance))
        {
            var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "finance_approver");
            roleKey = role.RoleKey; originalPermissions = role.PermissionsCsv;
            role.PermissionsCsv = string.Join(',', originalPermissions.Split(',').Where(x => x.Trim() != "procurement-budget-check"));
            await core.SaveChangesAsync();
        }
        try
        {
            before = await FinanceIdentityFootprint();
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, path, finance, Body(original.FinanceCheckRef));
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, path, finance, Body("SYNTHETIC-P27-REVOKED-NEW"));
            Require(before == await FinanceIdentityFootprint(), "Revoked Finance grant persisted capture/replay effects.");
        }
        finally
        {
            using var fixture = Scope(finance); var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == roleKey);
            role.PermissionsCsv = originalPermissions; await core.SaveChangesAsync();
        }

        // Owner-only synthetic legacy0 row: not backfilled, not authoritative new capture.
        var legacyId = Guid.NewGuid(); var legacyRef = "SYNTHETIC-P27-UNBOUND-" + legacyId.ToString("N");
        await using (var owner = new NpgsqlConnection(databaseConnection))
        {
            await owner.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO procurement.budget_check_snapshots SELECT (jsonb_populate_record(NULL::procurement.budget_check_snapshots,to_jsonb(b)||jsonb_build_object('id',@id::text,'finance_check_ref',@reference::text,'checker_identity_schema_version',0,'checked_by_worker_id',NULL,'checker_identity_sha256',NULL))).* FROM procurement.budget_check_snapshots b WHERE b.id=@original", owner);
            insert.Parameters.AddWithValue("id", legacyId); insert.Parameters.AddWithValue("reference", legacyRef);
            insert.Parameters.AddWithValue("original", original.Id);
            Require(await insert.ExecuteNonQueryAsync() == 1, "Synthetic legacy fixture did not insert exactly one row.");
        }
        before = await FinanceIdentityFootprint();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, path, finance, Body(legacyRef));
        Require(before == await FinanceIdentityFootprint(), "Legacy reference replay silently backfilled Finance checker identity.");
        using (var fixture = Scope(finance))
        {
            var db = fixture.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var retained = await db.BudgetCheckSnapshots.AsNoTracking().SingleAsync(x => x.Id == original.Id);
            var legacy = await db.BudgetCheckSnapshots.AsNoTracking().SingleAsync(x => x.Id == legacyId);
            Require(FinanceSnapshotCodec.Encode(retained, 1) == originalBytes
                && legacy.CheckerIdentitySchemaVersion == 0 && legacy.CheckedByWorkerId is null && legacy.CheckerIdentitySha256 is null,
                "Denied identity actions modified retained original or synthetic legacy bytes.");
        }
        Console.WriteLine("PASS Finance checker actual HTTP/Postgres capture/reload/microseconds/exact-ref replay; current worker rebind409, grant revocation403, unbound legacy409; no denied/replay effects. External Finance completeness is not claimed.");
    }

    private async Task VerifyFinanceCheckerBudgetChecks(Guid actualSnapshotId)
    {
        var cases = new (string Label, string Set)[]
        {
            ("v1-null-worker", "checked_by_worker_id=NULL"),
            ("v1-zero-worker", "checked_by_worker_id='00000000-0000-0000-0000-000000000000'::uuid"),
            ("v1-null-hash", "checker_identity_sha256=NULL"),
            ("v1-uppercase-hash", "checker_identity_sha256=repeat('A',64)"),
            ("v1-short-hash", "checker_identity_sha256=repeat('a',63)"),
            ("v1-invalid-hash", "checker_identity_sha256=repeat('z',64)"),
            ("v0-partial-metadata", "checker_identity_schema_version=0"),
            ("unsupported-version", "checker_identity_schema_version=2"),
        };
        foreach (var item in cases)
            await DenyFinanceIdentitySql("budget_check_snapshots", actualSnapshotId, item.Set,
                "ck_budget_checker_identity", item.Label);
        Console.WriteLine("PASS P27 raw required-NULL/zero/hash/version CHECK denials on actual HTTP-captured bound1 row; each owner-only attempted mutation rolled back.");
    }

    public async Task VerifyFinanceCheckerSubmissionCheck()
    {
        Guid? actualSubmission;
        await using (var owner = new NpgsqlConnection(databaseConnection))
        {
            await owner.OpenAsync();
            await using var select = new NpgsqlCommand("SELECT id FROM procurement.purchase_request_approval_submissions WHERE snapshot_schema_version=2 ORDER BY id LIMIT 1", owner);
            var result = await select.ExecuteScalarAsync();
            actualSubmission = result is Guid id ? id : null;
        }
        if (actualSubmission is null)
        {
            Console.WriteLine("NOT EXERCISED P27 submission discriminator CHECK: this selected probe mode produced no genuine schema2 submission; no approval evidence fabricated.");
            return;
        }
        await DenyFinanceIdentitySql("purchase_request_approval_submissions", actualSubmission.Value,
            "finance_identity_schema_version=2", "ck_pr_finance_identity_version", "submission-unsupported-discriminator");
        Console.WriteLine("PASS P27 raw submission unsupported-discriminator CHECK on genuine retained schema2 submission; rollback preserved exact history.");
    }

    private async Task DenyFinanceIdentitySql(string table, Guid target, string set, string constraint, string label)
    {
        // All identifiers/SET clauses are closed constants above. Only an actual retained row ID
        // is parameterized. This bootstrap-only negative never changes runtime grants or authority.
        var before = await FinanceIdentityFootprint();
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using var transaction = await owner.BeginTransactionAsync();
        var rejected = false;
        try
        {
            await using var update = new NpgsqlCommand($"UPDATE procurement.{table} SET {set} WHERE id=@id", owner, transaction);
            update.Parameters.AddWithValue("id", target);
            await update.ExecuteNonQueryAsync();
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.CheckViolation
            && error.ConstraintName == constraint) { rejected = true; }
        finally { await transaction.RollbackAsync(); }
        Require(rejected, "Expected exact PostgreSQL23514/" + constraint + " for " + label);
        Require(before == await FinanceIdentityFootprint(), "Raw CHECK negative changed retained data/evidence after rollback: " + label);
    }

    private async Task<string> FinanceIdentityFootprint()
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT jsonb_build_object('requests',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.purchase_requests r),'finance',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.budget_check_snapshots r),'submissions',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.purchase_request_approval_submissions r),'audit',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.audit_events r),'outbox',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.outbox_events r))::text", owner);
        return (string)(await query.ExecuteScalarAsync())!;
    }
}
