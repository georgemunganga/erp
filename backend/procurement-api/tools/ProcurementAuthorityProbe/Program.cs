using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("PROCUREMENT_AUTHORITY_PROBE_CONNECTION")
    ?? throw new InvalidOperationException("Set a private disposable PostgreSQL probe connection.");
if (!(new NpgsqlConnectionStringBuilder(connection).Database?.StartsWith("proc_authority_probe_", StringComparison.Ordinal) ?? false))
    throw new InvalidOperationException("Probe database name must start proc_authority_probe_. Production databases are forbidden.");
var company = Guid.NewGuid();
var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection).Options;
ProcurementDbContext Db(string actor) => new(options, new Scope(company, actor));
SpendingAuthorityConfigurationActor Actor(string actor, string permission)
    => new("probe", company, actor, true, new HashSet<string>([permission]));
SpendingAuthoritySubject Subject() => new("probe", company, "subject", true, true);
SpendingAuthorityDraft Draft(decimal amount) => new("ZMW", "EstimatedLineBase", amount,
    DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), true, null, true, null, true);
async Task<List<ProcurementSpendingAuthorityGrant>> Family(ProcurementDbContext db)
    => await SpendingAuthorityFamilyTransaction.LoadAsync(db, "subject", SpendingAuthorityConfiguration.ApprovalPermission,
        SpendingAuthorityConfiguration.ApprovalAction, "AUTH-PROBE", CancellationToken.None);
async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Begin(ProcurementDbContext db)
    => await SpendingAuthorityFamilyTransaction.BeginAsync(db, "probe", company, "subject",
        SpendingAuthorityConfiguration.ApprovalPermission, SpendingAuthorityConfiguration.ApprovalAction,
        "AUTH-PROBE", CancellationToken.None);
async Task<Guid> Create(decimal amount)
{
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            await using var db = Db("owner"); await using var tx = await Begin(db);
            var family = await Family(db);
            var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH-PROBE", "subject", Draft(amount),
                Actor("owner", SpendingAuthorityConfiguration.ConfigurePermission), DateTimeOffset.UtcNow, family);
            db.AddScoped(grant); await db.SaveChangesAsync(); await tx.CommitAsync(); return grant.Id;
        }
        catch (Exception ex) when (attempt < 5 && Retryable(ex)) { }
    }
}
async Task Submit(Guid id)
{
    await using var db = Db("owner"); await using var tx = await Begin(db);
    var family = await Family(db); var grant = family.Single(x => x.Id == id);
    SpendingAuthorityConfiguration.Submit(grant, grant.Version,
        Actor("owner", SpendingAuthorityConfiguration.ConfigurePermission), Subject(), DateTimeOffset.UtcNow);
    await db.SaveChangesAsync(); await tx.CommitAsync();
}
async Task Supersede(Guid previousId, Guid nextId, bool failBeforeReplacement)
{
    await using var db = Db("reviewer"); await using var tx = await Begin(db);
    var family = await Family(db); var old = family.Single(x => x.Id == previousId); var next = family.Single(x => x.Id == nextId);
    SpendingAuthorityConfiguration.Supersede(next, next.Version, old, old.Version,
        Actor("reviewer", SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Lower verified authority", DateTimeOffset.UtcNow, family);
    db.AddScoped(new AuditEvent { RecordType = nameof(ProcurementSpendingAuthorityGrant), RecordId = old.Id,
        Action = "authority.superseded", ActorSubjectId = "reviewer" });
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    db.Entry(old).State = EntityState.Modified;
    await db.SaveChangesAsync();
    if (failBeforeReplacement) throw new InjectedFailure();
    db.Entry(next).State = EntityState.Modified;
    await db.SaveChangesAsync(); await tx.CommitAsync();
}

await using (var schema = Db("owner"))
{
    // Fresh disposable model rehearsal. P22 migration/idempotency rehearsal is a separate
    // gate once generated on top of finalized P21; this does not claim migration coverage.
    await schema.Database.EnsureCreatedAsync();
}
var first = await Create(1000); await Submit(first);
await using (var db = Db("reviewer"))
{
    await using var tx = await Begin(db); var family = await Family(db); var grant = family.Single();
    SpendingAuthorityConfiguration.Activate(grant, grant.Version,
        Actor("reviewer", SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Independent finite authority", DateTimeOffset.UtcNow, family);
    await db.SaveChangesAsync(); await tx.CommitAsync();
}
var second = await Create(100); await Submit(second);
long beforeAudit;
await using (var db = Db("reviewer")) beforeAudit = await db.AuditEvents.LongCountAsync();
try { await Supersede(first, second, true); throw new Exception("Failure injection was not reached."); }
catch (InjectedFailure) { }
await using (var db = Db("reviewer"))
{
    var family = await Family(db);
    Require(family.Single(x => x.Id == first).State == "Active", "Predecessor changed after rollback.");
    Require(family.Single(x => x.Id == second).State == "PendingReview", "Replacement changed after rollback.");
    Require(await db.AuditEvents.LongCountAsync() == beforeAudit, "Audit survived rolled-back supersession.");
}
await Supersede(first, second, false);
await using (var db = Db("revoker"))
{
    await using var tx = await Begin(db); var family = await Family(db); var grant = family.Single(x => x.Id == second);
    Require(family.Single(x => x.Id == first).State == "Superseded", "Old authority was not retired.");
    SpendingAuthorityConfiguration.Revoke(grant, grant.Version,
        Actor("revoker", SpendingAuthorityConfiguration.RevokePermission), "Authority withdrawn", DateTimeOffset.UtcNow);
    await db.SaveChangesAsync(); await tx.CommitAsync();
}
await using (var db = Db("reviewer"))
{
    var family = await Family(db);
    Require(family.All(x => x.State != "Active"), "Revocation revived old authority.");
    var grant = family.Single(x => x.Id == second); grant.MaximumAmount = 10000;
    try { await db.SaveChangesAsync(); throw new Exception("Published mutation was accepted."); }
    catch (InvalidOperationException ex) when (ex.Message == "PublishedAuthorityIsImmutable") { }
}
await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Create(50)));
await using (var db = Db("reviewer"))
{
    var family = await Family(db);
    Require(family.Count == 6 && family.Select(x => x.AuthorityVersion).Distinct().Count() == 6,
        "Concurrent creation duplicated a logical authority version.");
}
Console.WriteLine("PASS: scoped PostgreSQL authority lifecycle, publication, rollback, terminal revocation, immutable fields and concurrent version allocation.");

static bool Retryable(Exception ex)
    => (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState
        is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation;
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed class InjectedFailure : Exception { }
sealed class Scope(Guid company, string actor) : IProcurementScope
{
    public string TenantId => "probe";
    public Guid LegalEntityId => company;
    public string SubjectId => actor;
    public Guid? WorkLocationId => null;
    public Guid? OrgUnitId => null;
    public bool IsConfined => false;
    public string? CorrelationId => "authority-probe";
}
