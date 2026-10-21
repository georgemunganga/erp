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

// Private synthetic persistence probe. Does not establish Core identity or finite-role HTTP authority.
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Procurement")
    ?? throw new InvalidOperationException("An explicit disposable probe database is required.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host is not ("localhost" or "127.0.0.1" or "/authority-identity-private-socket")
    || settings.Database is null || !settings.Database.StartsWith("authority_identity_", StringComparison.Ordinal))
    throw new InvalidOperationException("Probe requires its private socket or loopback and an authority_identity_ disposable database.");
var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
var maker = "11111111-1111-4111-8111-111111111111";
var recipient = "22222222-2222-4222-8222-222222222222";
var reviewer = "33333333-3333-4333-8333-333333333333";
var makerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
var recipientWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
var reviewerWorker = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
var company = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
var snake = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
ProcurementDbContext Context(string actor, Guid? worker) => new(
    new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
        p => p.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
    new ProbeScope(actor, worker, company));
SpendingAuthorityConfigurationActor Actor(string actor, string permission)
    => new("authority-identity-probe", company, actor, true, new HashSet<string> { permission });
SpendingAuthoritySubject Subject() => new("authority-identity-probe", company, recipient, true, true);
ProcurementSpendingAuthorityGrant Draft(string reference)
    => SpendingAuthorityConfiguration.CreateDraft(reference, recipient,
        new("ZMW", "EstimatedLineBase", 12.3456m, now.AddDays(-1), now.AddDays(1),
            AllBranches: true, AllDepartments: true, AllCategories: true),
        Actor(maker, SpendingAuthorityConfiguration.ConfigurePermission), now, []);

string downScript;
await using (var db = Context(maker, makerWorker))
{
    var migrations = db.GetService<IMigrator>();
    downScript = migrations.GenerateScript("P30SpendingAuthorityWorkerIdentity", "P29SourcingPolicyIdentity", MigrationsSqlGenerationOptions.NoTransactions);
    Check(downScript.Contains("LOCK TABLE procurement.spending_authority_grants", StringComparison.Ordinal)
        && downScript.Contains("SpendingAuthorityIdentityRecord", StringComparison.Ordinal), "Compiled Down lacks the concurrency/audit retention guards.");
    await migrations.MigrateAsync("P29SourcingPolicyIdentity");
    await migrations.MigrateAsync("P30SpendingAuthorityWorkerIdentity");
    await migrations.MigrateAsync("P29SourcingPolicyIdentity"); // Empty Down must be reversible.
    await migrations.MigrateAsync("P30SpendingAuthorityWorkerIdentity");
    await migrations.MigrateAsync("P29SourcingPolicyIdentity");
}
Guid legacyId; string legacyBefore;
await using (var db = Context(maker, null))
{
    var legacy = Draft("LEGACY-UNKNOWN"); legacyId = legacy.Id;
    db.SpendingAuthorityGrants.Add(legacy); await db.SaveChangesAsync();
    legacyBefore = JsonSerializer.Serialize(await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x => x.Id == legacyId));
    await db.GetService<IMigrator>().MigrateAsync("P30SpendingAuthorityWorkerIdentity");
}
await using (var db = Context(maker, makerWorker))
{
    Check(JsonSerializer.Serialize(await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x => x.Id == legacyId))
        == legacyBefore, "Upgrade modified the legacy grant.");
    Check(!await db.SpendingAuthorityIdentityRecords.AnyAsync(), "Upgrade backfilled unknown worker identity.");
}
var grant = Draft("NEW-RETAINED");
var created = SpendingAuthorityIdentity.CaptureCreation(grant, makerWorker, recipientWorker);
var creationFingerprint = SpendingAuthorityConfiguration.Fingerprint(grant);
await using (var db = Context(maker, makerWorker))
{
    db.SpendingAuthorityGrants.Add(grant);
    db.SpendingAuthorityIdentityRecords.Add(SpendingAuthorityIdentityStorage.ToRecord(created));
    await db.SaveChangesAsync();
}
SpendingAuthorityIdentityEvidence submitted;
await using (var db = Context(maker, makerWorker))
{
    var stored = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == grant.Id);
    Check(SpendingAuthorityConfiguration.Fingerprint(stored) == creationFingerprint, "Creation fingerprint drifted.");
    SpendingAuthorityConfiguration.Submit(stored, stored.Version,
        Actor(maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), now.AddSeconds(1));
    submitted = SpendingAuthorityIdentity.CaptureSubmission(stored, created, maker, makerWorker, recipientWorker);
    db.SpendingAuthorityIdentityRecords.Add(SpendingAuthorityIdentityStorage.ToRecord(submitted));
    await db.SaveChangesAsync();
}
SpendingAuthorityIdentityEvidence activated;
await using (var db = Context(reviewer, reviewerWorker))
{
    var stored = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == grant.Id);
    SpendingAuthorityIdentity.RequireIndependentReviewer(stored, created, submitted, reviewer, reviewerWorker);
    SpendingAuthorityConfiguration.Activate(stored, stored.Version,
        Actor(reviewer, SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Synthetic independent review",
        now.AddSeconds(2), [stored]);
    activated = SpendingAuthorityIdentity.CaptureActivation(stored, created, submitted, reviewer, reviewerWorker, recipientWorker);
    db.SpendingAuthorityIdentityRecords.Add(SpendingAuthorityIdentityStorage.ToRecord(activated));
    await db.SaveChangesAsync();
}
await using (var db = Context(reviewer, reviewerWorker))
{
    var stored = await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x => x.Id == grant.Id);
    var rows = await db.SpendingAuthorityIdentityRecords.AsNoTracking().Where(x => x.GrantId == grant.Id).ToArrayAsync();
    Check(rows.Length == 3, "Expected exactly three retained action rows.");
    SpendingAuthorityIdentity.VerifyActivation(stored,
        SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Created")),
        SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted")),
        SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Activated")));
}
var beforeDenials = await Footprint();
await using (var db = Context(reviewer, reviewerWorker))
{
    var row = await db.SpendingAuthorityIdentityRecords.SingleAsync(x => x.Id == created.Id);
    db.Entry(row).Property(x => x.ActorWorkerId).CurrentValue = reviewerWorker;
    try { await db.SaveChangesAsync(); throw new InvalidOperationException("Evidence mutation unexpectedly succeeded."); }
    catch (InvalidOperationException error) when (error.Message == "AuthorityIdentityAppendOnly") { }
}
var invalidOwner = JsonSerializer.SerializeToNode(SpendingAuthorityIdentityStorage.ToRecord(created), snake)!.AsObject();
invalidOwner["id"] = Guid.NewGuid(); invalidOwner["grant_id"] = Guid.NewGuid();
await ConstraintFailure(invalidOwner, PostgresErrorCodes.ForeignKeyViolation, ["fk_authority_identity_grant"]);
var invalidLineage = JsonSerializer.SerializeToNode(SpendingAuthorityIdentityStorage.ToRecord(submitted), snake)!.AsObject();
invalidLineage["id"] = Guid.NewGuid(); invalidLineage["grant_record_version_at_action"] = 999;
invalidLineage["created_evidence_id"] = Guid.NewGuid();
await ConstraintFailure(invalidLineage, PostgresErrorCodes.ForeignKeyViolation, ["fk_authority_identity_created"]);
var invalidSubmission = JsonSerializer.SerializeToNode(SpendingAuthorityIdentityStorage.ToRecord(activated), snake)!.AsObject();
invalidSubmission["id"] = Guid.NewGuid(); invalidSubmission["submitted_evidence_id"] = Guid.NewGuid();
await ConstraintFailure(invalidSubmission, PostgresErrorCodes.ForeignKeyViolation,
    ["fk_authority_identity_submitted"], temporarilyRemoveActivation: activated.Id);
var duplicateOrigin = JsonSerializer.SerializeToNode(SpendingAuthorityIdentityStorage.ToRecord(created), snake)!.AsObject();
duplicateOrigin["id"] = Guid.NewGuid();
await ConstraintFailure(duplicateOrigin, PostgresErrorCodes.UniqueViolation, ["ux_authority_identity_origin", "ux_authority_identity_action_revision"]);
var invalidWorker = JsonSerializer.SerializeToNode(SpendingAuthorityIdentityStorage.ToRecord(created), snake)!.AsObject();
invalidWorker["id"] = Guid.NewGuid(); invalidWorker["actor_worker_id"] = Guid.Empty;
await ConstraintFailure(invalidWorker, PostgresErrorCodes.CheckViolation, ["ck_authority_identity_evidence"]);
await using (var db = Context(maker, makerWorker))
{
    try
    {
        await db.GetService<IMigrator>().MigrateAsync("P29SourcingPolicyIdentity");
        throw new InvalidOperationException("Used identity evidence was destructively downgraded.");
    }
    catch (Exception error) when (RetainedDowngrade(error)) { }
}
// Owner-only recovery simulation stays inside a rolled-back transaction. Even if
// action rows are selectively removed, their retained audit must veto the actual Down script.
await using (var db = new NpgsqlConnection(connection))
{
    await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
    foreach (var stage in new[] { "Activated", "Submitted", "Created" })
    {
        await using var remove = new NpgsqlCommand("DELETE FROM procurement.spending_authority_identity_evidence WHERE grant_id=@grant AND stage=@stage", db, tx);
        remove.Parameters.AddWithValue("grant", grant.Id); remove.Parameters.AddWithValue("stage", stage);
        await remove.ExecuteNonQueryAsync();
    }
    await using var command = new NpgsqlCommand(downScript, db, tx);
    try { await command.ExecuteNonQueryAsync(); throw new InvalidOperationException("Audit-only retained identity allowed destructive Down."); }
    catch (Exception error) when (RetainedDowngrade(error)) { }
    finally { await tx.RollbackAsync(); }
}
Check(await Footprint() == beforeDenials, "A rejected command/constraint/downgrade changed the retained footprint.");
await using (var db = Context(maker, makerWorker))
{
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "The generated migration snapshot does not match the model.");
    Check((await db.Database.GetAppliedMigrationsAsync()).Last().EndsWith("_P30SpendingAuthorityWorkerIdentity"), "P30 was not retained.");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, legacyUnchanged = true, noIdentityBackfill = true,
    emptyDownUp = true, persistedActions = 3, foreignKeyDenials = 3, uniquenessDenial = true, workerCheckDenial = true,
    appendOnlyDenial = true, usedDownBlocked = true, auditOnlyDownBlocked = true, deniedFootprintUnchanged = true,
    actualPostgres = true, actualCoreIdentityOrFiniteRoleHttp = false }));

async Task ConstraintFailure(JsonObject row, string expected, string[] constraintNames, Guid? temporarilyRemoveActivation = null)
{
    await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
    await using var tx = await db.BeginTransactionAsync();
    if (temporarilyRemoveActivation is { } oldActivation)
    {
        await using var remove = new NpgsqlCommand("DELETE FROM procurement.spending_authority_identity_evidence WHERE id=@id", db, tx);
        remove.Parameters.AddWithValue("id", oldActivation); await remove.ExecuteNonQueryAsync();
    }
    await using var command = new NpgsqlCommand("INSERT INTO procurement.spending_authority_identity_evidence SELECT * FROM jsonb_populate_record(NULL::procurement.spending_authority_identity_evidence, @row)", db, tx);
    command.Parameters.Add(new NpgsqlParameter("row", NpgsqlDbType.Jsonb) { Value = row.ToJsonString() });
    try { await command.ExecuteNonQueryAsync(); throw new InvalidOperationException("Invalid evidence unexpectedly passed its database constraint."); }
    catch (PostgresException error) when (error.SqlState == expected && constraintNames.Contains(error.ConstraintName, StringComparer.Ordinal)) { }
    finally { await tx.RollbackAsync(); }
}
async Task<string> Footprint()
{
    await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT md5(
          coalesce((SELECT string_agg(md5(row_to_json(t)::text),'' ORDER BY id) FROM procurement.spending_authority_grants t),'') || '|' ||
          coalesce((SELECT string_agg(md5(row_to_json(t)::text),'' ORDER BY id) FROM procurement.spending_authority_identity_evidence t),'') || '|' ||
          coalesce((SELECT string_agg(md5(row_to_json(t)::text),'' ORDER BY id) FROM procurement.audit_events t),'') || '|' ||
          coalesce((SELECT string_agg(md5(row_to_json(t)::text),'' ORDER BY id) FROM procurement.outbox_events t),'') || '|' ||
          coalesce((SELECT string_agg("MigrationId",'' ORDER BY "MigrationId") FROM procurement.__procurement_migrations),'')
        )
        """, db);
    return (string)(await command.ExecuteScalarAsync())!;
}
static bool RetainedDowngrade(Exception error)
{
    for (Exception? current = error; current is not null; current = current.InnerException)
        if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.ObjectNotInPrerequisiteState
            && pg.MessageText == "Retained spending authority identity prevents destructive downgrade.") return true;
    return false;
}
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
sealed class ProbeScope(string actor, Guid? worker, Guid company) : IProcurementScope
{
    public string TenantId => "authority-identity-probe";
    public Guid LegalEntityId => company;
    public string SubjectId => actor;
    public Guid? WorkerId => worker;
    public Guid? WorkLocationId => null;
    public Guid? OrgUnitId => null;
    public bool IsConfined => false;
    public string? CorrelationId => "synthetic-authority-identity-probe";
}
