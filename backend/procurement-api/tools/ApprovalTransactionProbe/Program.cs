using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

// This executable creates synthetic records only in an explicitly named rehearsal database.
var connection = Environment.GetEnvironmentVariable("APPROVAL_REHEARSAL_CONNECTION")
    ?? throw new InvalidOperationException("APPROVAL_REHEARSAL_CONNECTION is required.");
if (!(new NpgsqlConnectionStringBuilder(connection).Database?.Contains("rehearsal", StringComparison.OrdinalIgnoreCase) ?? false))
    throw new InvalidOperationException("Refusing a database without rehearsal in its name.");
var scope = new ProbeScope();
var procOptions = new DbContextOptionsBuilder<ProcurementDbContext>()
    .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options;
var coreOptions = new DbContextOptionsBuilder<ApprovalDbContext>()
    .UseNpgsql(connection, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval")).Options;
await using (var proc = new ProcurementDbContext(procOptions, scope)) await proc.Database.MigrateAsync();
await using (var core = new ApprovalDbContext(coreOptions, scope)) await core.Database.MigrateAsync();
var runner = new ProcurementApprovalTransaction(connection, scope, scope);

foreach (var checkpoint in new[] { "before-save", "after-procurement-save", "after-core-save", "after-both-saves" })
{
    var id = Guid.NewGuid();
    var calls = 0;
    await Expect<ProbeFailure>(async () => await runner.ExecuteAsync<int>(async (proc, core, ct) =>
    {
        calls++;
        AddRecords(proc, core, id);
        if (checkpoint == "before-save") throw new ProbeFailure();
        if (checkpoint != "after-core-save") await proc.SaveChangesAsync(ct);
        if (checkpoint == "after-procurement-save") throw new ProbeFailure();
        await core.SaveChangesAsync(ct);
        throw new ProbeFailure();
    }));
    Assert(calls == 1, "Unexpected retry of a non-transient error.");
    await AssertRecordCounts(id, 0);
    Console.WriteLine($"PASS rollback {checkpoint}");
}

var committed = Guid.NewGuid();
await runner.ExecuteAsync(async (proc, core, ct) =>
{
    Assert(ReferenceEquals(proc.Database.GetDbConnection(), core.Database.GetDbConnection()), "Different physical connections.");
    Assert(ReferenceEquals(proc.Database.CurrentTransaction!.GetDbTransaction(), core.Database.CurrentTransaction!.GetDbTransaction()),
        "Different database transactions.");
    AddRecords(proc, core, committed);
    await Task.CompletedTask;
    return committed;
});
await AssertRecordCounts(committed, 1);
Console.WriteLine("PASS one transaction commits source, Core case, audit and outbox");

// Two different assignees read the same case revision. The first intermediate vote
// MUST change the case token even though Pending stays Pending. The stale second
// vote must roll back its decision/assignment writes, then succeed after a fresh read.
var reviewerA = scope with { SubjectId = "synthetic-independent-reviewer-a" };
var reviewerB = scope with { SubjectId = "synthetic-independent-reviewer-b" };
await using (var first = new ApprovalDbContext(coreOptions, reviewerA))
await using (var stale = new ApprovalDbContext(coreOptions, reviewerB))
{
    var firstCase = await LoadCase(first, committed);
    var staleCase = await LoadCase(stale, committed);
    var firstAssignment = firstCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == reviewerA.SubjectId);
    var staleAssignment = staleCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == reviewerB.SubjectId);
    var originalVersion = firstCase.Version;
    Decide(first, firstCase, firstAssignment, reviewerA, "vote-a");
    await first.SaveChangesAsync();
    Assert(firstCase.State == "Pending" && firstCase.Version == originalVersion + 1,
        "Intermediate vote did not advance case version.");
    Decide(stale, staleCase, staleAssignment, reviewerB, "vote-b");
    await Expect<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    await using var fresh = new ApprovalDbContext(coreOptions, reviewerB);
    var latest = await LoadCase(fresh, committed);
    var beforeDecisions = await fresh.Decisions.Where(x => x.CaseId == latest.Id).ToListAsync();
    Assert(beforeDecisions.Count == 1 && latest.State == "Pending", "Stale vote leaked a decision or state.");
    var secondAssignment = latest.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == reviewerB.SubjectId);
    Assert(secondAssignment.State == "Pending", "Stale vote leaked an assignment update.");
    Decide(fresh, latest, secondAssignment, reviewerB, "vote-b", beforeDecisions);
    await fresh.SaveChangesAsync();
    Assert(latest.State == "Approved" && latest.Version == originalVersion + 2, "Fresh quorum did not complete once.");
    var replay = ApprovalEngine.Decide(latest, secondAssignment, Actor(reviewerB), "Approve", "vote-b", null,
        beforeDecisions, DateTimeOffset.UtcNow);
    Assert(!replay.Recorded, "Terminal replay recorded another vote.");
    Assert(await fresh.SaveChangesAsync() == 0 && latest.Version == originalVersion + 2,
        "Replay advanced case version or changed rows.");
}
Console.WriteLine("PASS real PostgreSQL stale intermediate vote denial, fresh quorum completion and terminal replay");

var retryId = Guid.NewGuid();
var attempts = 0;
ProcurementDbContext? failedContext = null;
await runner.ExecuteAsync(async (proc, core, ct) =>
{
    attempts++;
    Assert(!ReferenceEquals(failedContext, proc), "Retry reused a failed context.");
    AddRecords(proc, core, retryId);
    await proc.SaveChangesAsync(ct);
    await core.SaveChangesAsync(ct);
    if (attempts == 1)
    {
        failedContext = proc;
        await proc.Database.ExecuteSqlRawAsync("DO $$ BEGIN RAISE EXCEPTION 'synthetic serialization abort' USING ERRCODE = '40001'; END $$;", ct);
    }
    return retryId;
});
Assert(attempts == 2, "Confirmed transaction abort was not retried exactly once.");
await AssertRecordCounts(retryId, 1);
Console.WriteLine("PASS serialization abort retries whole command with fresh contexts and one final effect");

var exhausted = Guid.NewGuid();
attempts = 0;
await Expect<PostgresException>(async () => await runner.ExecuteAsync(async (proc, core, ct) =>
{
    attempts++;
    AddRecords(proc, core, exhausted);
    await proc.SaveChangesAsync(ct);
    await core.SaveChangesAsync(ct);
    await proc.Database.ExecuteSqlRawAsync("DO $$ BEGIN RAISE EXCEPTION 'synthetic deadlock abort' USING ERRCODE = '40P01'; END $$;", ct);
    return exhausted;
}));
Assert(attempts == 3, "Transaction retry bound was not three attempts.");
await AssertRecordCounts(exhausted, 0);
Console.WriteLine("PASS bounded transaction retries leave no partial effect");

var unknown = Guid.NewGuid();
attempts = 0;
await Expect<IOException>(async () => await runner.ExecuteAsync<int>(async (proc, core, ct) =>
{
    attempts++;
    AddRecords(proc, core, unknown);
    await proc.SaveChangesAsync(ct);
    throw new IOException("Synthetic unknown network outcome; must not blindly retry.");
}));
Assert(attempts == 1, "An unknown network outcome was automatically retried.");
await AssertRecordCounts(unknown, 0);
Console.WriteLine("PASS pre-commit IO error rolls back and is not automatically retried (not an unknown-commit recovery test)");

var cancelled = Guid.NewGuid();
using (var cancellation = new CancellationTokenSource())
{
    await Expect<OperationCanceledException>(async () => await runner.ExecuteAsync<int>(async (proc, core, ct) =>
    {
        AddRecords(proc, core, cancelled);
        await proc.SaveChangesAsync(ct);
        await core.SaveChangesAsync(ct);
        cancellation.Cancel();
        ct.ThrowIfCancellationRequested();
        return 0;
    }, cancellation.Token));
}
await AssertRecordCounts(cancelled, 0);
Console.WriteLine("PASS cancellation after both saves rolls back");

await Expect<UnauthorizedAccessException>(() => Task.FromResult(new ProcurementApprovalTransaction(connection,
    scope, scope with { SubjectId = "different-identity" })));
Console.WriteLine("PASS mismatched canonical scope denied before database work");
Console.WriteLine("Approval transaction rehearsal passed. This does not enable approval HTTP routes or prove business authority guards.");

async Task AssertRecordCounts(Guid id, int count)
{
    await using var proc = new ProcurementDbContext(procOptions, scope);
    await using var core = new ApprovalDbContext(coreOptions, scope);
    Assert(await proc.PurchaseRequests.CountAsync(x => x.Id == id) == count, "Unexpected source count.");
    Assert(await core.Cases.CountAsync(x => x.SourceId == id) == count, "Unexpected Core case count.");
    Assert(await proc.AuditEvents.CountAsync(x => x.RecordId == id && x.Action == "rehearsal") == count, "Unexpected business audit count.");
    Assert(await proc.AuditEvents.CountAsync(x => x.RecordId == id && x.Action == "create") == count, "Unexpected automatic create audit count.");
    Assert(await proc.OutboxEvents.CountAsync(x => x.AggregateId == id) == count, "Unexpected outbox count.");
}

void AddRecords(ProcurementDbContext proc, ApprovalDbContext core, Guid id)
{
    proc.PurchaseRequests.Add(new PurchaseRequest
    {
        Id = id, TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        Number = "TEST-" + id.ToString("N"), RequesterWorkerId = Guid.NewGuid(),
        CurrencyCode = "ZMW", Purpose = "Synthetic atomic transaction evidence", CreatedBy = scope.SubjectId,
    });
    var approval = new ApprovalCase
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, SourceModule = "procurement",
        SourceType = "purchase_request", SourceId = id, SourceVersion = 1,
        PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1, MakerSubjectId = scope.SubjectId,
    };
    var stage = new ApprovalStage
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        CaseId = approval.Id, Sequence = 1, Code = "synthetic", Mode = "All", State = "Active", RequiredApprovals = 2,
    };
    stage.Assignments.Add(new ApprovalAssignment
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CaseId = approval.Id,
        StageId = stage.Id, AssigneeSubjectId = "synthetic-independent-reviewer-a", PermissionKey = "synthetic-approve",
    });
    stage.Assignments.Add(new ApprovalAssignment
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CaseId = approval.Id,
        StageId = stage.Id, AssigneeSubjectId = "synthetic-independent-reviewer-b", PermissionKey = "synthetic-approve",
    });
    approval.Stages.Add(stage);
    core.AddCase(approval);
    proc.AuditEvents.Add(new AuditEvent
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        RecordType = "purchase_request", RecordId = id, Action = "rehearsal", ActorSubjectId = scope.SubjectId,
    });
    proc.OutboxEvents.Add(new OutboxEvent
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        EventType = "rehearsal.approval.opened.v1", AggregateType = "purchase_request",
        AggregateId = id, IdempotencyKey = "rehearsal-" + id.ToString("N"),
    });
}

static Task<ApprovalCase> LoadCase(ApprovalDbContext db, Guid sourceId) => db.Cases
    .Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleAsync(x => x.SourceId == sourceId);
static ApprovalActor Actor(ProbeScope scope) => new(scope.SubjectId, scope.TenantId, scope.LegalEntityId,
    new HashSet<string> { "synthetic-approve" });
static void Decide(ApprovalDbContext db, ApprovalCase parent, ApprovalAssignment assignment,
    ProbeScope actor, string key, List<ApprovalDecision>? decisions = null)
{
    decisions ??= [];
    ApprovalEngine.Decide(parent, assignment, Actor(actor), "Approve", key, null, decisions, DateTimeOffset.UtcNow);
    db.Decisions.AddRange(decisions.Where(x => db.Entry(x).State == EntityState.Detached));
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
sealed class ProbeFailure : Exception { }
sealed record ProbeScope : IProcurementScope, IApprovalScope
{
    public string TenantId { get; init; } = "approval-transaction-rehearsal";
    public Guid LegalEntityId { get; init; } = Guid.NewGuid();
    public string SubjectId { get; init; } = "synthetic-requester";
    public Guid? WorkLocationId => null;
    public Guid? OrgUnitId => null;
    public bool IsConfined => false;
    public string? CorrelationId => "synthetic-transaction-probe";
}
