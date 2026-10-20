using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("APPROVAL_SUBMISSION_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Set a disposable rehearsal connection.");
var target = new NpgsqlConnectionStringBuilder(connection);
if (target.Database != "approval_submission_rehearsal" || !target.Host!.StartsWith("/", StringComparison.Ordinal))
    throw new InvalidOperationException("This synthetic probe requires its named rehearsal database over a local Unix socket.");
var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
    p => p.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options;
var scope = new Scope("synthetic-approval-tenant", Guid.NewGuid());
ProcurementDbContext Context(Scope? actor = null) => new(options, actor ?? scope);
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

await using (var db = Context())
{
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 18, "P0-P21 migration count must be 18 after twice-applied SQL.");
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Rehearsal schema has pending migrations.");
}
// Deliberately retain seven 100ns ticks below PostgreSQL's microsecond precision.
var now = new DateTimeOffset(DateTimeOffset.UtcNow.UtcTicks / 10 * 10 + 7, TimeSpan.Zero);
var request = new PurchaseRequest
{
    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CreatedBy = scope.SubjectId,
    Number = "SYNTHETIC-PR-1", RequesterWorkerId = Guid.NewGuid(), Status = "Submitted",
    Version = 1, SubmittedVersion = 1, SubmittedAt = now, Purpose = "Synthetic evidence only",
    CurrencyCode = "ZMW", ApprovalState = "PendingPolicyAndBudget",
};
request.Lines.Add(new PurchaseRequestLine
{
    PurchaseRequestId = request.Id, LineNumber = 1, Description = "Synthetic precision item", UnitCode = "EA",
    RequestedQuantity = 1.2345m, EstimatedUnitPrice = 1.2345m, EstimatedCurrencyCode = "ZMW", CategoryCode = "TEST",
});
var policy = new PolicyVersion
{
    PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1, State = "Published",
    EffectiveFrom = now.AddDays(-1), RulesJson = "{\"z\":[1e2,1.0000,{\"b\":true,\"a\":null}],\"amount\":12345678901234.1234}",
};
var evaluation = new PolicyEvaluation
{
    PurchaseRequestId = request.Id, RequestVersion = 1, PolicyVersionId = policy.Id,
    PolicyVersionNumber = 1, AmountBasis = "EstimatedLineBase", Outcome = "Matched", EvaluatedAt = now,
    DecisionJson = JsonSerializer.Serialize(new
    {
        requestId = request.Id, requestVersion = 1, policyId = policy.Id,
        lines = request.Lines.Select(x => new { lineId = x.Id, decision = new
            { status = "Match", approvalStages = new[] { new { code = "TEST", assignee = "LineManager", mode = "One" } } } }),
    }),
};
var finance = new BudgetCheckSnapshot
{
    PurchaseRequestId = request.Id, RequestVersion = 1, FinanceCheckRef = "SYNTHETIC-CHECK-1",
    Status = "Available", CheckedAmount = 2m, CurrencyCode = "ZMW", CheckedAt = now.AddSeconds(1),
    CheckedBySubjectId = "synthetic-finance", EvidenceRef = "SYNTHETIC-NO-REAL-RESERVATION",
    ReservationRef = "SYNTHETIC-RESERVATION", ReservationStatus = "Confirmed",
};
PurchaseRequestApprovalSubmission pin;
await using (var db = Context())
{
    db.AddScoped(request); db.AddScoped(policy); db.AddScoped(evaluation); db.AddScoped(finance);
    await db.SaveChangesAsync();
    // Pin tracked values without reloading first: this catches sub-microsecond digest drift.
    pin = PurchaseRequestApprovalPinning.Pin(request, evaluation, policy, finance, scope.TenantId,
        scope.LegalEntityId, Guid.NewGuid(), Guid.NewGuid(), PurchaseRequestApprovalPinning.ApprovedManualEvidence,
        "SYNTHETIC-FIXTURE-ONLY-NOT-AUTHORITY", now.AddMinutes(1));
    db.AddScoped(pin);
    request.ApprovalInstanceRef = pin.CoreApprovalCaseId.ToString();
    request.ApprovalState = "Pending";
    await db.SaveChangesAsync();
}
await using (var db = Context())
{
    var stored = await db.ApprovalSubmissions.SingleAsync();
    var source = await db.PurchaseRequests.Include(x => x.Lines).SingleAsync();
    var storedFinance = await db.BudgetCheckSnapshots.SingleAsync();
    Check(now.UtcTicks % 10 == 7 && source.SubmittedAt!.Value.UtcTicks % 10 == 0,
        "Timestamp fixture did not exercise actual database microsecond truncation.");
    var result = PurchaseRequestApprovalPinning.Verify(stored, source,
        await db.PolicyEvaluations.SingleAsync(), await db.PolicyVersions.SingleAsync(), storedFinance,
        scope.TenantId, scope.LegalEntityId);
    Check(result.IsValid, "Database roundtrip invalidated pin: " + string.Join(",", result.Blockers));
    Check(source.Version == 2 && stored.SubmittedBusinessVersion == 1, "Operational revision changed submitted authority.");
    Check(stored.EstimatedAmount == 1.52399025m, "Eight-decimal calculation was rounded in storage.");
    Console.WriteLine("PASS roundtrip: source revision 2/submitted version 1, numeric 1.52399025, timestamp ticks 7 -> 0.");
}

// A second valid scoped source/evaluation/Finance chain isolates uniqueness from FK failures.
var otherRequest = new PurchaseRequest { Number = "SYNTHETIC-PR-2", RequesterWorkerId = Guid.NewGuid(),
    Status = "Submitted", SubmittedVersion = 1, SubmittedAt = now, Purpose = "Synthetic constraint source", CurrencyCode = "ZMW" };
var otherPolicy = new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 2,
    State = "Published", EffectiveFrom = now.AddDays(-1), RulesJson = "{}" };
var otherEvaluation = new PolicyEvaluation { PurchaseRequestId = otherRequest.Id, RequestVersion = 1,
    PolicyVersionId = otherPolicy.Id, PolicyVersionNumber = 2, AmountBasis = "EstimatedLineBase",
    Outcome = "Matched", DecisionJson = "{}", EvaluatedAt = now };
var otherFinance = new BudgetCheckSnapshot { PurchaseRequestId = otherRequest.Id, RequestVersion = 1,
    FinanceCheckRef = "SYNTHETIC-CHECK-2", Status = "Available", CheckedAmount = 2, CurrencyCode = "ZMW",
    CheckedAt = now, CheckedBySubjectId = "synthetic-finance", EvidenceRef = "SYNTHETIC-2",
    ReservationRef = "SYNTHETIC-RESERVATION-2", ReservationStatus = "Confirmed" };
await using (var db = Context())
{
    db.AddScoped(otherRequest); db.AddScoped(otherPolicy); db.AddScoped(otherEvaluation); db.AddScoped(otherFinance);
    await db.SaveChangesAsync();
}

// Execute copies through direct SQL to test actual PostgreSQL constraints rather than the helper.
await using var sql = new NpgsqlConnection(connection);
await sql.OpenAsync();
await using (var command = new NpgsqlCommand("CREATE TEMP TABLE approval_probe_template AS SELECT * FROM procurement.purchase_request_approval_submissions", sql))
    await command.ExecuteNonQueryAsync();
async Task Denied(string label, string expectedSqlState, string setup)
{
    await using var tx = await sql.BeginTransactionAsync();
    try
    {
        await using var command = new NpgsqlCommand("UPDATE approval_probe_template SET id = gen_random_uuid(); " + setup
            + "; INSERT INTO procurement.purchase_request_approval_submissions SELECT * FROM approval_probe_template", sql, tx);
        await command.ExecuteNonQueryAsync();
        throw new InvalidOperationException(label + " was accepted.");
    }
    catch (PostgresException error) when (error.SqlState == expectedSqlState)
    { Console.WriteLine("PASS denied " + label + ": " + error.SqlState); }
    finally { await tx.RollbackAsync(); }
}
var otherSource = $"purchase_request_id='{otherRequest.Id:D}', policy_evaluation_id='{otherEvaluation.Id:D}', "
    + $"policy_version_id='{otherPolicy.Id:D}', policy_version_number=2, finance_snapshot_id='{otherFinance.Id:D}'";
await Denied("same source/version", "23505", "UPDATE approval_probe_template SET core_approval_case_id=gen_random_uuid(), opening_command_key=gen_random_uuid()");
await Denied("same Core case", "23505", "UPDATE approval_probe_template SET opening_command_key=gen_random_uuid(), " + otherSource);
await Denied("same opening key", "23505", "UPDATE approval_probe_template SET core_approval_case_id=gen_random_uuid(), " + otherSource);
// Removing the original within the rolled-back attempt prevents unrelated unique indexes masking FK checks.
const string noOriginal = "DELETE FROM procurement.purchase_request_approval_submissions; ";
await Denied("foreign company references", "23503", noOriginal + "UPDATE approval_probe_template SET legal_entity_id=gen_random_uuid()");
await Denied("foreign tenant references", "23503", noOriginal + "UPDATE approval_probe_template SET tenant_id='foreign-synthetic-tenant'");
await Denied("different existing same-company source", "23503", noOriginal + $"UPDATE approval_probe_template SET purchase_request_id='{otherRequest.Id:D}'");
await Denied("wrong submitted version", "23503", noOriginal + "UPDATE approval_probe_template SET submitted_business_version=2");
await Denied("different existing same-company evaluation", "23503", noOriginal + $"UPDATE approval_probe_template SET policy_evaluation_id='{otherEvaluation.Id:D}'");
await Denied("different existing same-company policy", "23503", noOriginal + $"UPDATE approval_probe_template SET policy_version_id='{otherPolicy.Id:D}', policy_version_number=2");
await Denied("different existing same-company Finance snapshot", "23503", noOriginal + $"UPDATE approval_probe_template SET finance_snapshot_id='{otherFinance.Id:D}'");

await using (var db = Context(scope with { TenantId = "foreign-synthetic-tenant" }))
    Check(!await db.ApprovalSubmissions.AnyAsync(), "Foreign tenant could read evidence.");
await using (var db = Context(scope with { LegalEntityId = Guid.NewGuid() }))
    Check(!await db.ApprovalSubmissions.AnyAsync(), "Foreign company could read evidence.");
await using (var db = Context())
{
    var stored = await db.ApprovalSubmissions.SingleAsync();
    db.Entry(stored).Property(x => x.RequestSnapshotJson).CurrentValue = "{}";
    try { await db.SaveChangesAsync(); throw new Exception("Context rewrite accepted."); }
    catch (InvalidOperationException error) when (error.Message.Contains("append-only", StringComparison.Ordinal)) { }
}
await using (var db = Context())
{
    db.Remove(await db.ApprovalSubmissions.SingleAsync());
    try { await db.SaveChangesAsync(); throw new Exception("Context deletion accepted."); }
    catch (InvalidOperationException error) when (error.Message.Contains("physical deletion", StringComparison.Ordinal)) { }
}
await using (var db = Context()) Check(await db.ApprovalSubmissions.CountAsync() == 1, "Denied attempts changed the ledger.");
Console.WriteLine("PASS: isolated submission evidence/constraints only; NO real Core case, approval decision, spending authorization or atomic Core+Procurement execution proven.");
return 0;

internal sealed record Scope(string TenantId, Guid LegalEntityId) : IProcurementScope
{
    public string SubjectId => "synthetic-requester";
    public Guid? WorkLocationId => null;
    public Guid? OrgUnitId => null;
    public bool IsConfined => false;
    public string? CorrelationId => "synthetic-approval-submission-probe";
}
