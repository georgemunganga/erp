using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using NpgsqlTypes;

// Private owner-level persistence evidence only. No HTTP, Core identity, finite-role or business-policy claim.
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Procurement")
    ?? throw new InvalidOperationException("An explicit fresh private probe database is required.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (args.Length != 0 || settings.Host is not ("localhost" or "127.0.0.1" or "::1" or "/authority-identity-private-socket")
    || settings.Database is null || !settings.Database.StartsWith("operational_policy_identity_", StringComparison.Ordinal)
    || settings.Database.Length > 63 || !settings.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
    throw new InvalidOperationException("Only a fresh operational_policy_identity_ private socket/loopback database is accepted.");
const string Previous = "P30SpendingAuthorityWorkerIdentity";
const string Current = "P31OperationalPolicyWorkerIdentity";
const string Table = "procurement.operational_policy_identity_evidence";
const string Maker = "11111111-1111-4111-8111-111111111111";
const string Reviewer = "22222222-2222-4222-8222-222222222222";
var makerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
var reviewerWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
var company = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
var snake = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
ProcurementDbContext Context(string actor = Maker, Guid? worker = null) => new(
    new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
        p => p.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
    new ProbeScope(actor, worker ?? (actor == Reviewer ? reviewerWorker : makerWorker), company));
PolicyVersion Draft(string key, int number) => new() { TenantId = "operational-policy-probe", LegalEntityId = company,
    PolicyKey = key, PolicyVersionNumber = number, State = "Draft", Version = 1,
    RulesJson = "{\"schemaVersion\":1,\"persistenceOnly\":true}", EffectiveFrom = now.AddDays(1),
    CreatedAt = now, CreatedBy = Maker };

await using (var raw = new NpgsqlConnection(connection))
{
    await raw.OpenAsync();
    await using var fresh = new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%' AND c.relkind IN ('r','p'))", raw);
    Check(await fresh.ExecuteScalarAsync() is true, "Refusing nonempty database; no reset, resume or cleanup is supported.");
}
string downScript;
await using (var db = Context())
{
    var migrator = db.GetService<IMigrator>();
    downScript = migrator.GenerateScript(Current, Previous, MigrationsSqlGenerationOptions.NoTransactions);
    Check(downScript.Contains("LOCK TABLE", StringComparison.Ordinal)
        && downScript.Contains("OperationalPolicyIdentityRecord", StringComparison.Ordinal)
        && downScript.Contains("Retained operational policy identity prevents destructive downgrade.", StringComparison.Ordinal),
        "Compiled P31 Down lacks the required lock/evidence/audit retention contract.");
    await migrator.MigrateAsync(Previous);
    await migrator.MigrateAsync(Current); await migrator.MigrateAsync(Previous);
    await migrator.MigrateAsync(Current); await migrator.MigrateAsync(Previous);
}
// Raw legacy intake is intentional: current runtime guards must never adopt unknown historical people.
var legacy = new[] { Draft("RECEIVING", 1), Draft("INVOICE_MATCHING", 1) };
await using (var raw = new NpgsqlConnection(connection))
{
    await raw.OpenAsync();
    foreach (var policy in legacy)
    {
        await using var insert = new NpgsqlCommand("INSERT INTO procurement.policy_versions SELECT * FROM jsonb_populate_record(NULL::procurement.policy_versions,@row)", raw);
        insert.Parameters.Add(new NpgsqlParameter("row", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(policy, snake) });
        Check(await insert.ExecuteNonQueryAsync() == 1, "Raw pre-P31 legacy fixture did not persist exactly one policy.");
    }
}
var legacyBefore = await LegacyRows();
await using (var db = Context())
{
    await db.GetService<IMigrator>().MigrateAsync(Current);
    Check(!await db.OperationalPolicyIdentityRecords.AnyAsync(), "P31 fabricated historical worker evidence.");
}
Check(legacyBefore == await LegacyRows(), "Upgrade changed original legacy columns.");
var all = new List<(Guid Policy, OperationalPolicyWorkerIdentityEvidence Created, OperationalPolicyWorkerIdentityEvidence Submitted, OperationalPolicyWorkerIdentityEvidence Published)>();
foreach (var key in new[] { "RECEIVING", "INVOICE_MATCHING" })
{
    var policy = Draft(key, 2);
    var created = OperationalPolicyWorkerIdentity.Capture(policy, "Created", Maker, makerWorker, 1, now);
    await using (var db = Context())
    {
        db.PolicyVersions.Add(policy); db.OperationalPolicyIdentityRecords.Add(OperationalPolicyIdentityStorage.ToRecord(created));
        await db.SaveChangesAsync();
    }
    OperationalPolicyWorkerIdentityEvidence submitted;
    await using (var db = Context())
    {
        var owner = await db.PolicyVersions.SingleAsync(x => x.Id == policy.Id);
        owner.State = "PendingReview"; owner.ReviewRequestedBy = Maker; owner.ReviewRequestedAt = now.AddSeconds(1);
        submitted = OperationalPolicyWorkerIdentity.Capture(owner, "Submitted", Maker, makerWorker, checked(owner.Version + 1), owner.ReviewRequestedAt.Value, created);
        db.OperationalPolicyIdentityRecords.Add(OperationalPolicyIdentityStorage.ToRecord(submitted)); await db.SaveChangesAsync();
    }
    OperationalPolicyWorkerIdentityEvidence published;
    await using (var db = Context(Reviewer))
    {
        var owner = await db.PolicyVersions.SingleAsync(x => x.Id == policy.Id);
        OperationalPolicyWorkerIdentity.RequireIndependent(owner, created, submitted, Reviewer, reviewerWorker);
        owner.State = "Published"; owner.PublishedBy = Reviewer; owner.PublishedAt = now.AddSeconds(2);
        published = OperationalPolicyWorkerIdentity.Capture(owner, "Published", Reviewer, reviewerWorker, checked(owner.Version + 1), owner.PublishedAt.Value, created, submitted);
        db.OperationalPolicyIdentityRecords.Add(OperationalPolicyIdentityStorage.ToRecord(published)); await db.SaveChangesAsync();
    }
    await using (var db = Context(Reviewer))
    {
        var owner = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == policy.Id);
        var rows = await db.OperationalPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policy.Id).ToArrayAsync();
        Check(rows.Length == 3 && owner.Version == 3, "Expected three exact committed stages.");
        OperationalPolicyWorkerIdentity.VerifyPublication(owner,
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Created")),
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted")),
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Published")));
        Check(rows.All(x => x.CreatedAt == x.ActionAt && x.CreatedBy == x.ActorPrincipalId && x.Version == 1
            && x.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(x.ActionAt)), "Persisted immutable metadata or PostgreSQL microseconds drifted.");
    }
    all.Add((policy.Id, created, submitted, published));
}
var baseline = await Footprint();
foreach (var old in legacy)
{
    await using var db = Context();
    var policy = await db.PolicyVersions.SingleAsync(x => x.Id == old.Id);
    policy.State = "PendingReview"; policy.ReviewRequestedAt = now.AddSeconds(3); policy.ReviewRequestedBy = Maker;
    try { await db.SaveChangesAsync(); throw new InvalidOperationException("Legacy policy was silently adopted as a new governed round."); }
    catch (InvalidOperationException error) when (error.Message == "OperationalPolicyHistoricalIdentityMissing") { }
    Check(await Footprint() == baseline, "Denied legacy adoption changed persisted footprint.");
}
foreach (var delete in new[] { false, true })
{
    await using var db = Context();
    var row = await db.OperationalPolicyIdentityRecords.SingleAsync(x => x.Id == all[0].Created.Id);
    if (delete) db.Remove(row); else db.Entry(row).Property(x => x.ActorWorkerId).CurrentValue = reviewerWorker;
    try { await db.SaveChangesAsync(); throw new InvalidOperationException("Append-only mutation unexpectedly succeeded."); }
    catch (InvalidOperationException error) when (error.Message == "OperationalPolicyIdentityAppendOnly") { }
    Check(await Footprint() == baseline, "Denied EF evidence mutation changed persisted footprint.");
}
var first = all[0];
JsonObject Raw(OperationalPolicyWorkerIdentityEvidence evidence) => JsonSerializer.SerializeToNode(OperationalPolicyIdentityStorage.ToRecord(evidence), snake)!.AsObject();
var wrongOwner = Raw(first.Created); wrongOwner["id"] = Guid.NewGuid(); wrongOwner["policy_version_id"] = Guid.NewGuid();
await DeniedInsert(wrongOwner, PostgresErrorCodes.ForeignKeyViolation, ["fk_operational_policy_identity_owner"]);
var wrongKey = Raw(first.Created); wrongKey["id"] = Guid.NewGuid(); wrongKey["policy_key"] = "INVOICE_MATCHING";
await DeniedInsert(wrongKey, PostgresErrorCodes.ForeignKeyViolation, ["fk_operational_policy_identity_owner"]);
var foreignOrigin = Raw(first.Submitted); foreignOrigin["id"] = Guid.NewGuid(); foreignOrigin["policy_record_version_at_action"] = 899;
foreignOrigin["created_evidence_id"] = all[1].Created.Id;
await DeniedInsert(foreignOrigin, PostgresErrorCodes.ForeignKeyViolation, ["fk_operational_policy_identity_created"]);
var wrongStage = Raw(first.Submitted); wrongStage["id"] = Guid.NewGuid(); wrongStage["policy_record_version_at_action"] = 900;
wrongStage["created_evidence_id"] = first.Submitted.Id;
await DeniedInsert(wrongStage, PostgresErrorCodes.ForeignKeyViolation, ["fk_operational_policy_identity_created"]);
var wrongSubmission = Raw(first.Published); wrongSubmission["id"] = Guid.NewGuid(); wrongSubmission["submitted_evidence_id"] = all[1].Created.Id;
await DeniedInsert(wrongSubmission, PostgresErrorCodes.ForeignKeyViolation, ["fk_operational_policy_identity_submitted"], removePublished: first.Published.Id);
var duplicateCreated = Raw(first.Created); duplicateCreated["id"] = Guid.NewGuid();
await DeniedInsert(duplicateCreated, PostgresErrorCodes.UniqueViolation, ["ux_operational_policy_identity_created", "ux_operational_policy_identity_revision"]);
var duplicatePublished = Raw(first.Published); duplicatePublished["id"] = Guid.NewGuid(); duplicatePublished["policy_record_version_at_action"] = 901;
await DeniedInsert(duplicatePublished, PostgresErrorCodes.UniqueViolation, ["ux_operational_policy_identity_published"]);
var duplicateRevision = Raw(first.Submitted); duplicateRevision["id"] = Guid.NewGuid();
await DeniedInsert(duplicateRevision, PostgresErrorCodes.UniqueViolation, ["ux_operational_policy_identity_revision"]);
var noWorker = Raw(first.Submitted); noWorker["id"] = Guid.NewGuid(); noWorker["policy_record_version_at_action"] = 902; noWorker["actor_worker_id"] = Guid.Empty;
await DeniedInsert(noWorker, PostgresErrorCodes.CheckViolation, ["ck_operational_policy_identity"]);
Check(await Footprint() == baseline, "A raw SQL denial changed retained data/history.");
await using (var db = Context())
{
    try { await db.GetService<IMigrator>().MigrateAsync(Previous); throw new InvalidOperationException("Used P31 evidence was destructively downgraded."); }
    catch (Exception error) when (RetainedDown(error)) { }
}
Check(await Footprint() == baseline, "Rejected used Down changed original footprint.");
// Private owner rollback simulation, never a supported runtime delete or recovery instruction.
await using (var raw = new NpgsqlConnection(connection))
{
    await raw.OpenAsync(); await using var tx = await raw.BeginTransactionAsync();
    foreach (var stage in new[] { "Published", "Submitted", "Created" })
    {
        await using var remove = new NpgsqlCommand("DELETE FROM " + Table + " WHERE stage=@stage", raw, tx);
        remove.Parameters.AddWithValue("stage", stage); await remove.ExecuteNonQueryAsync();
    }
    await using var down = new NpgsqlCommand(downScript, raw, tx);
    try { await down.ExecuteNonQueryAsync(); throw new InvalidOperationException("Retained audit alone allowed destructive P31 Down."); }
    catch (Exception error) when (RetainedDown(error)) { }
    finally { await tx.RollbackAsync(); }
}
Check(await Footprint() == baseline && legacyBefore == await LegacyRows(), "Audit-only Down rehearsal changed retained evidence.");
await using (var db = Context())
{
    Check(!db.Database.HasPendingModelChanges(), "Compiled generated snapshot differs from actual model.");
    Check((await db.Database.GetAppliedMigrationsAsync()).Last().EndsWith("_" + Current, StringComparison.Ordinal), "P31 history was not retained.");
    Check(await db.OperationalPolicyIdentityRecords.CountAsync() == 6, "Unexpected action rows.");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, actualPostgres = true, actualCoreIdentityOrFiniteRoleHttp = false,
    legacyPoliciesUnchanged = 2, noIdentityBackfill = true, legacyAdoptionDenials = 2, emptyDownUp = true, persistedActions = 6,
    scopedForeignKeyDenials = 5, uniquenessDenials = 3, emptyWorkerCheckDenial = true, appendOnlyUpdateAndDeleteDenied = true,
    usedDownBlocked = true, auditOnlyDownBlocked = true, denialFootprintUnchanged = true, production = false }));

async Task DeniedInsert(JsonObject row, string sqlState, string[] constraints, Guid? removePublished = null)
{
    await using var raw = new NpgsqlConnection(connection); await raw.OpenAsync(); await using var tx = await raw.BeginTransactionAsync();
    if (removePublished is { } published)
    {
        await using var remove = new NpgsqlCommand("DELETE FROM " + Table + " WHERE id=@id", raw, tx);
        remove.Parameters.AddWithValue("id", published); await remove.ExecuteNonQueryAsync();
    }
    await using var insert = new NpgsqlCommand("INSERT INTO " + Table + " SELECT * FROM jsonb_populate_record(NULL::" + Table + ",@row)", raw, tx);
    insert.Parameters.Add(new NpgsqlParameter("row", NpgsqlDbType.Jsonb) { Value = row.ToJsonString() });
    try { await insert.ExecuteNonQueryAsync(); throw new InvalidOperationException("Invalid evidence passed a named PostgreSQL constraint."); }
    catch (PostgresException error) when (error.SqlState == sqlState && constraints.Contains(error.ConstraintName, StringComparer.Ordinal)) { }
    finally { await tx.RollbackAsync(); }
}
async Task<string> LegacyRows()
{
    await using var raw = new NpgsqlConnection(connection); await raw.OpenAsync();
    await using var command = new NpgsqlCommand("SELECT coalesce(jsonb_agg(to_jsonb(p) ORDER BY id),'[]'::jsonb)::text FROM procurement.policy_versions p WHERE policy_version_number=1 AND policy_key IN ('RECEIVING','INVOICE_MATCHING')", raw);
    return (string)(await command.ExecuteScalarAsync())!;
}
async Task<string> Footprint()
{
    await using var raw = new NpgsqlConnection(connection); await raw.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT jsonb_build_object(
          'policies',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY id),'[]'::jsonb) FROM procurement.policy_versions t),
          'identity',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY id),'[]'::jsonb) FROM procurement.operational_policy_identity_evidence t),
          'audit',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY id),'[]'::jsonb) FROM procurement.audit_events t),
          'outbox',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY id),'[]'::jsonb) FROM procurement.outbox_events t),
          'history',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "MigrationId"),'[]'::jsonb) FROM procurement.__procurement_migrations t),
          'catalog',(SELECT coalesce(jsonb_agg(jsonb_build_array(c.relname,c.relkind,c.relowner,c.relacl) ORDER BY c.relname),'[]'::jsonb) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement'))::text
        """, raw);
    return ProcurementEvidenceEncoding.Digest((string)(await command.ExecuteScalarAsync())!);
}
static bool RetainedDown(Exception error)
{
    for (Exception? e = error; e is not null; e = e.InnerException)
        if (e is PostgresException pg && pg.SqlState == PostgresErrorCodes.ObjectNotInPrerequisiteState
            && pg.MessageText == "Retained operational policy identity prevents destructive downgrade.") return true;
    return false;
}
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed class ProbeScope(string actor, Guid? worker, Guid company) : IProcurementScope
{
    public string TenantId => "operational-policy-probe"; public Guid LegalEntityId => company; public string SubjectId => actor;
    public Guid? WorkerId => worker; public Guid? WorkLocationId => null; public Guid? OrgUnitId => null;
    public bool IsConfined => false; public string? CorrelationId => "private-operational-policy-identity-probe";
}
