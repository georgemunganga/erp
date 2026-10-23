using System.Text.Json;
using System.Text.RegularExpressions;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("MANUAL_PAYMENT_PROBE_CONNECTION")
    ?? throw new InvalidOperationException("A new private synthetic database is required.");
var builder = new NpgsqlConnectionStringBuilder(connection);
if (builder.Host != "/private-pg" || !Regex.IsMatch(builder.Database ?? "", @"\Aproc_manual_payment_20261023_[a-z0-9_]{1,24}\z"))
    throw new InvalidOperationException("Only the private Unix socket and named synthetic database are allowed.");
PaymentProbe? probe = null;
try {
    probe = new PaymentProbe(connection); await probe.Run();
    Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_MANUAL_PAYMENT_POSTGRES_STORAGE", checks = probe.Checks,
        synthetic = true, httpVerified = false, financeOrBankExecution = false, syntheticFinanceProjectionFixture = true }));
} catch (Exception error) {
    Console.WriteLine(JsonSerializer.Serialize(new { status = "FAIL_MANUAL_PAYMENT_POSTGRES_STORAGE", errorType = error.GetType().Name, phase = probe?.Phase,
        sqlState = (error as PostgresException ?? error.InnerException as PostgresException)?.SqlState,
        completedChecks = probe?.Checks,
        errorCode = Regex.IsMatch(error.Message, "\\A(?:ManualPayment|OriginalInvoice)[A-Za-z0-9]+\\z") ? error.Message : null,
        failureMethods = new System.Diagnostics.StackTrace(error, false).GetFrames().Take(12)
            .Select(x => x.GetMethod()?.DeclaringType?.FullName + "." + x.GetMethod()?.Name).ToArray() }));
    Environment.ExitCode = 1;
}

sealed class PaymentProbe(string connection)
{
    private readonly Guid company = Guid.NewGuid(), branch = Guid.NewGuid();
    private readonly string creator = Guid.NewGuid().ToString("D"), approver = Guid.NewGuid().ToString("D"),
        recorder = Guid.NewGuid().ToString("D"), confirmer = Guid.NewGuid().ToString("D");
    private SupplierInvoice invoice = null!;
    private const string Tenant = "synthetic-manual-payment";
    public int Checks { get; private set; }
    public string Phase { get; private set; } = "empty-admission";
    private sealed record Scope(string TenantId, Guid LegalEntityId, Guid? WorkLocationId, string SubjectId) : IProcurementScope
    { public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "synthetic-pg-payment"; }
    private ProcurementDbContext Db(string? actor = null, Guid? selectedBranch = null) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
            options => options.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
        new Scope(Tenant, company, selectedBranch ?? branch, actor ?? creator));
    private void Check(bool condition) { if (!condition) throw new InvalidOperationException("Synthetic PostgreSQL invariant failed."); Checks++; }
    private static async Task Exec(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, params (string Name, object Value)[] values)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        foreach (var value in values) cmd.Parameters.AddWithValue(value.Name, value.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    private async Task<string> Counts()
    {
        await using var c = new NpgsqlConnection(connection); await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT (SELECT count(*) FROM procurement.manual_payment_requests)::text || ':' || "
            + "(SELECT count(*) FROM procurement.manual_payment_decisions)::text || ':' || (SELECT count(*) FROM procurement.manual_payment_receipts)::text || ':' || "
            + "(SELECT count(*) FROM procurement.audit_events)::text", c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
    private async Task Deny(string[] states, Func<NpgsqlConnection, NpgsqlTransaction, Task> attempt)
    {
        var before = await Counts(); var denied = false;
        await using (var c = new NpgsqlConnection(connection)) {
            await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
            try { await attempt(c, tx); await tx.CommitAsync(); }
            catch (PostgresException error) when (states.Contains(error.SqlState)) { denied = true; }
        }
        Check(denied); Check(before == await Counts());
    }
    public async Task Run()
    {
        await using (var c = new NpgsqlConnection(connection)) {
            await c.OpenAsync(); await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')", c);
            Check((long)(await cmd.ExecuteScalarAsync())! == 0); // Refuse retained/nonempty inputs.
        }
        Phase = "owning-migration-empty-down-up";
        await using (var db = Db()) {
            Check(!db.Database.HasPendingModelChanges()); await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Check(applied.Last().EndsWith("P27ManualPaymentRecords", StringComparison.Ordinal));
            var previous = applied.Single(x => x.EndsWith("P24CorePrincipalRecurringPurchasePlanning", StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(previous);
            Phase = "p24-credit-invoice-store-compatibility"; await LegacyP24Compatibility();
            Phase = "owning-migration-empty-down-up"; await db.Database.MigrateAsync();
            Check((await db.Database.GetAppliedMigrationsAsync()).Last().EndsWith("P27ManualPaymentRecords", StringComparison.Ordinal));
        }
        Phase = "audit-only-downgrade-veto"; await AuditOnlyDowngradeVeto();
        Phase = "synthetic-supplier-invoice-seed";
        await using (var db = Db()) {
            var supplier = new Supplier { Number = "SYN-PAY-1", LegalName = "Synthetic supplier", DisplayName = "Synthetic supplier", CountryCode = "ZM" };
            db.AddScoped(supplier);
            invoice = new SupplierInvoice { SupplierId = supplier.Id, BranchRef = branch, CurrencyCode = "ZMW", InvoiceNumber = "SYN-PAY-INV-1",
                NormalizedInvoiceNumber = "SYN-PAY-INV-1", IntakeChannel = "Manual", InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), NetAmount = 100m, GrossAmount = 100m };
            db.AddScoped(invoice); await db.SaveChangesAsync();
        }
        Phase = "mapped-create";
        var made = ManualPaymentWorkflow.Create(Draft(), Actor(creator), Assignee(approver, "approve"), Assignee(recorder, "record"), Assignee(confirmer, "confirm"), Source(), DateTimeOffset.UtcNow);
        await Save(made, creator, true);
        var id = made.Request.Id;
        var current = await Read(id, creator);
        Phase = "mapped-revise";
        await Save(ManualPaymentWorkflow.Revise(current, 1, Draft() with { Amount = 12.3456m }, Actor(creator), Assignee(approver, "approve"),
            Assignee(recorder, "record"), Assignee(confirmer, "confirm"), Source(), DateTimeOffset.UtcNow), creator);
        current = await Read(id, creator); Check(current.Amount == 12.3456m && current.Version == 2);
        Phase = "mapped-submit";
        await Save(ManualPaymentWorkflow.Submit(current, 2, Actor(creator), Source(), DateTimeOffset.UtcNow), creator);
        current = await Read(id, approver);
        Phase = "mapped-approve";
        await Save(ManualPaymentWorkflow.Approve(current, 3, Actor(approver), Source(), DateTimeOffset.UtcNow), approver);
        current = await Read(id, recorder);
        var date = DateOnly.FromDateTime(DateTime.UtcNow); // Explicit synthetic clock, not production company-date evidence.
        Phase = "mapped-record";
        await Save(ManualPaymentWorkflow.Record(current, 4, Actor(recorder), Source(), new(date, "External transfer", "SYN-EXTERNAL-1", 12.3456m, null), date, DateTimeOffset.UtcNow), recorder);
        current = await Read(id, confirmer);
        Phase = "mapped-confirm";
        await Save(ManualPaymentWorkflow.Confirm(current, 5, Actor(confirmer), date, DateTimeOffset.UtcNow), confirmer);
        current = await Read(id, creator); Check(current.Status == "ConfirmedManually" && current.Version == 6 && current.RecordedAmount == 12.3456m);
        await using (var db = Db()) {
            Check(await db.ManualPaymentDecisions.CountAsync(x => x.ManualPaymentRequestId == id) == 6);
            Check(await db.ManualPaymentReceipts.CountAsync(x => x.ManualPaymentRequestId == id) == 6);
            Check(await db.FinancePaymentStatusRefs.CountAsync() == 0 && (await db.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id)).FinancePostingRef is null);
        }
        await using (var foreign = Db(selectedBranch: Guid.NewGuid())) Check(!await foreign.ManualPaymentRequests.AnyAsync());
        var draft = ManualPaymentWorkflow.Create(Draft(), Actor(creator), Assignee(approver, "approve"), Assignee(recorder, "record"), Assignee(confirmer, "confirm"), Source(), DateTimeOffset.UtcNow);
        await Save(draft, creator, true);
        Phase = "negative-postgres-sql"; await NegativeSql(draft.Request.Id);
        Phase = "locked-invoice-capacity"; await CapacityConcurrency();
        Phase = "finance-only-credit-capacity"; await FinanceOnlyCreditVeto();
        Phase = "retained-downgrade-veto";
        await using (var db = Db()) {
            var before = await Counts(); var rejected = false;
            try { await db.GetService<IMigrator>().MigrateAsync("20261022144102_P24CorePrincipalRecurringPurchasePlanning"); }
            catch (PostgresException error) when (error.SqlState == "23514") { rejected = true; }
            Check(rejected && before == await Counts());
        }
    }
    private ManualPaymentDraft Draft() => new(branch, invoice.Id, invoice.SupplierId, "ZMW", 25.1250m, approver, recorder, confirmer, "Synthetic manual record");
    private ManualPaymentInvoiceSource Source() => new(invoice.Id, Tenant, company, branch, invoice.SupplierId, "ZMW", invoice.InvoiceDate, 100m, 100m, true);
    private ManualPaymentActor Actor(string id) => new(id, Tenant, company, branch, new HashSet<Guid> { branch },
        new[] { "create", "edit", "submit", "approve", "record", "confirm", "cancel" }.Select(x => "procurement-payment-" + x).ToHashSet(), true);
    private ManualPaymentAssignee Assignee(string id, string action) => new(id, Tenant, company, new HashSet<Guid> { branch }, new HashSet<string> { "procurement-payment-" + action }, true);
    private async Task<ManualPaymentRequest> Read(Guid id, string actor) { await using var db = Db(actor); return await db.ManualPaymentRequests.SingleAsync(x => x.Id == id); }
    private async Task Save(ManualPaymentChange change, string actor, bool create = false)
    {
        await using var db = Db(actor);
        await Persist(db, change, actor, create); Checks++;
    }
    private async Task Persist(ProcurementDbContext db, ManualPaymentChange change, string actor, bool create = false)
    {
        if (create) db.AddScoped(change.Request);
        else { var stored = await db.ManualPaymentRequests.SingleAsync(x => x.Id == change.Request.Id); db.Entry(stored).CurrentValues.SetValues(change.Request); }
        db.AddScoped(change.Decision);
        db.AddScoped(new ManualPaymentCommandReceipt { TenantId = Tenant, LegalEntityId = company, BranchId = branch, ManualPaymentRequestId = change.Request.Id,
            ActorPrincipalId = actor, CommandKey = Guid.NewGuid(), Action = change.Decision.Action, RequestSha256 = new string('a', 64), ResultVersion = change.Decision.ToVersion,
            ResponseJson = JsonSerializer.Serialize(new { id = change.Request.Id, version = change.Decision.ToVersion, status = change.Request.Status }) });
        await db.SaveChangesAsync();
    }
    private async Task CapacityConcurrency()
    {
        var scope = new Scope(Tenant, company, branch, creator); var allowed = new HashSet<Guid> { branch };
        await using (var db = Db()) {
            var refused = false;
            try { await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, invoice.Id, branch, allowed); }
            catch (InvalidOperationException e) when (e.Message == "ManualPaymentSerializableTransactionRequired") { refused = true; }
            Check(refused);
        }
        var reached = 0; var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Compete()
        {
            await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Establish both snapshots before either can reserve capacity. This is a real
            // overlapping PostgreSQL race, not two sequential reads dressed as concurrency.
            _ = await db.SupplierInvoices.AsNoTracking().Where(x => x.Id == invoice.Id).Select(x => x.Id).SingleAsync();
            if (Interlocked.Increment(ref reached) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            try {
                var locked = await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, invoice.Id, branch, allowed);
                var made = ManualPaymentWorkflow.Create(Draft() with { Amount = 60m }, Actor(creator),
                    Assignee(approver, "approve"), Assignee(recorder, "record"), Assignee(confirmer, "confirm"), locked.Source, DateTimeOffset.UtcNow);
                await Persist(db, made, creator, true);
                await Persist(db, ManualPaymentWorkflow.Submit(made.Request, 1, Actor(creator), locked.Source, DateTimeOffset.UtcNow), creator);
                await tx.CommitAsync(); return "Committed";
            }
            catch (Exception e) when (e.GetBaseException() is PostgresException { SqlState: "40001" }) { return "ConfirmedRollback"; }
            catch (InvalidOperationException e) when (e.Message == "ManualPaymentInsufficientOutstanding") { return "CapacityDenied"; }
        }
        var outcomes = await Task.WhenAll(Compete(), Compete());
        Check(reached == 2 && outcomes.Count(x => x == "Committed") == 1);
        Check(outcomes.Count(x => x is "ConfirmedRollback" or "CapacityDenied") == 1);
        await using (var db = Db()) {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var locked = await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, invoice.Id, branch, allowed);
            Check(locked.Source.IsEligible && locked.Capacity.OtherManualCommitments == 72.3456m
                && locked.Capacity.RemainingExcludingThisRequest == 27.6544m);
            Check(await db.ManualPaymentRequests.CountAsync(x => x.Status == "PendingApproval") == 1);
            var denied = false;
            try { await ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, invoice, null, 30m); }
            catch (InvalidOperationException e) when (e.Message == "ManualPaymentRegisteredAmountExceedsInvoice") { denied = true; }
            Check(denied); // Sequential credit-after-payment cannot overcommit the invoice.
            var reserved = await db.ManualPaymentRequests.AsNoTracking().FirstAsync(x => x.Status == "PendingApproval");
            var excluding = await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, invoice.Id, branch, allowed, reserved.Id);
            denied = false;
            try { _ = ManualPaymentWorkflow.Create(Draft(), Actor(creator), Assignee(approver, "approve"),
                Assignee(recorder, "record"), Assignee(confirmer, "confirm"), excluding.Source, DateTimeOffset.UtcNow); }
            catch (InvalidOperationException e) when (e.Message == "ManualPaymentCapacityExclusionInvalid") { denied = true; }
            Check(denied);
            await tx.RollbackAsync();
        }
        await CreditAgainstManualRace(scope, allowed);
        await using (var db = Db()) {
            var original = await db.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id); var version = original.Version;
            Check(!await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
            original.GrossAmount = original.NetAmount = 101m; var denied = false;
            try { await db.SaveChangesAsync(); }
            catch (InvalidOperationException e) when (e.Message == "OriginalInvoiceImmutable") { denied = true; }
            Check(denied); db.ChangeTracker.Clear(); original = await db.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id);
            Check(original.GrossAmount == 100m && original.Version == version);
        }
    }
    private async Task LegacyP24Compatibility()
    {
        await using var db = Db();
        var supplier = new Supplier { Number = "SYN-P24-PAY", LegalName = "Synthetic P24 compatibility", DisplayName = "Synthetic P24 compatibility", CountryCode = "ZM" };
        db.AddScoped(supplier);
        var header = new SupplierInvoice { SupplierId = supplier.Id, BranchRef = branch, CurrencyCode = "ZMW",
            InvoiceNumber = "SYN-P24-PAY", NormalizedInvoiceNumber = "SYN-P24-PAY", IntakeChannel = "Manual",
            InvoiceDate = new(2026,10,22), NetAmount = 100m, GrossAmount = 100m };
        var line = new SupplierInvoiceLine { SupplierInvoiceId = header.Id, LineNumber = 1, Description = "Original P24 line", Quantity = 1m, UnitPrice = 100m };
        db.AddScoped(header); db.AddScoped(line); await db.SaveChangesAsync();
        Check(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == header.Id));
        header.NetAmount = header.GrossAmount = 99m; line.UnitPrice = 99m; await db.SaveChangesAsync();
        Check(header.Version == 2 && header.GrossAmount == 99m);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE id={header.Id} AND tenant_id={Tenant} AND legal_entity_id={company} FOR UPDATE");
        await ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, header, null, 10m);
        var credit = new SupplierCreditNoteRef { SupplierId = supplier.Id, SupplierInvoiceId = header.Id,
            CreditNumber = "SYN-P24-CREDIT", CurrencyCode = "ZMW", Amount = 10m, Status = "Draft", Reason = "Synthetic P24 commercial claim" };
        db.AddScoped(credit); await db.SaveChangesAsync(); await tx.CommitAsync();
        Check(await db.SupplierCreditNoteRefs.AnyAsync(x => x.Id == credit.Id && x.Amount == 10m));
        Check(!await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == header.Id));
    }
    private async Task FinanceOnlyCreditVeto()
    {
        await using var db = Db();
        var header = new SupplierInvoice { SupplierId = invoice.SupplierId, BranchRef = branch, CurrencyCode = "ZMW",
            InvoiceNumber = "SYN-FINANCE-ONLY", NormalizedInvoiceNumber = "SYN-FINANCE-ONLY", IntakeChannel = "Manual",
            InvoiceDate = new(2026,10,22), NetAmount = 100m, GrossAmount = 100m };
        db.AddScoped(header);
        // Explicit synthetic imported-outcome fixture only; no real Finance adapter,
        // AP posting, account grant, external callback or payment rail executes here.
        db.AddScoped(new FinancePaymentStatusRef { SupplierInvoiceId = header.Id, CurrencyCode = "ZMW",
            FinancePaymentRef = "SYN-FINANCE-ONLY", Status = "Paid", PaidAmount = 100m, SyncedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE id={header.Id} AND tenant_id={Tenant} AND legal_entity_id={company} FOR UPDATE");
        var denied = false;
        try { await ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, header, null, 1m); }
        catch (InvalidOperationException e) when (e.Message == "ManualPaymentRegisteredAmountExceedsInvoice") { denied = true; }
        Check(denied && !await db.SupplierCreditNoteRefs.AnyAsync(x => x.SupplierInvoiceId == header.Id));
        await tx.RollbackAsync();
    }
    private async Task CreditAgainstManualRace(Scope scope, IReadOnlySet<Guid> allowed)
    {
        var reached = 0; var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Compete(bool credit)
        {
            await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            _ = await db.SupplierInvoices.AsNoTracking().Where(x => x.Id == invoice.Id).Select(x => x.Id).SingleAsync();
            if (Interlocked.Increment(ref reached) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            try {
                if (credit) {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE id={invoice.Id} AND tenant_id={Tenant} AND legal_entity_id={company} FOR UPDATE");
                    await ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, invoice, null, 20m);
                    db.AddScoped(new SupplierCreditNoteRef { SupplierId = invoice.SupplierId, SupplierInvoiceId = invoice.Id,
                        CreditNumber = "SYN-CREDIT-RACE", CurrencyCode = "ZMW", Amount = 20m, Status = "Draft", Reason = "Synthetic commercial claim" });
                    await db.SaveChangesAsync();
                } else {
                    var locked = await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, invoice.Id, branch, allowed);
                    var made = ManualPaymentWorkflow.Create(Draft() with { Amount = 20m }, Actor(creator), Assignee(approver, "approve"),
                        Assignee(recorder, "record"), Assignee(confirmer, "confirm"), locked.Source, DateTimeOffset.UtcNow);
                    await Persist(db, made, creator, true);
                    await Persist(db, ManualPaymentWorkflow.Submit(made.Request, 1, Actor(creator), locked.Source, DateTimeOffset.UtcNow), creator);
                }
                await tx.CommitAsync(); return "Committed";
            }
            catch (Exception e) when (e.GetBaseException() is PostgresException { SqlState: "40001" }) { return "ConfirmedRollback"; }
            catch (InvalidOperationException e) when (e.Message is "ManualPaymentInsufficientOutstanding" or "ManualPaymentRegisteredAmountExceedsInvoice") { return "CapacityDenied"; }
        }
        var outcomes = await Task.WhenAll(Compete(true), Compete(false));
        Check(reached == 2 && outcomes.Count(x => x == "Committed") == 1);
        Check(outcomes.Count(x => x is "ConfirmedRollback" or "CapacityDenied") == 1);
        await using var read = Db(); await using var readTx = await read.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var actual = await ManualPaymentCapacityReader.ReadLockedAsync(read, scope, invoice.Id, branch, allowed);
        Check(actual.Capacity.OtherManualCommitments + actual.Capacity.RegisteredCredits == 92.3456m
            && actual.Capacity.RemainingExcludingThisRequest == 7.6544m);
        await readTx.RollbackAsync();
    }
    private async Task AuditOnlyDowngradeVeto()
    {
        await using (var c = new NpgsqlConnection(connection)) {
            await c.OpenAsync(); await Exec(c, null, "INSERT INTO procurement.audit_events (id,tenant_id,legal_entity_id,created_at,created_by,version,record_type,record_id,action,actor_subject_id,changed_fields_json) "
                + "VALUES (@id,@tenant,@company,@at,@actor,1,'OtherEvidence',@record,'synthetic-retention',@actor,@body::jsonb)",
                ("id", Guid.NewGuid()), ("tenant", Tenant), ("company", company), ("at", DateTime.UtcNow), ("actor", creator), ("record", Guid.NewGuid()),
                ("body", "{\"principalEvidenceKind\": \"ManualPayment\"}"));
        }
        await using var db = Db(); var denied = false; var before = await Counts();
        try { await db.GetService<IMigrator>().MigrateAsync("20261022144102_P24CorePrincipalRecurringPurchasePlanning"); }
        catch (PostgresException error) when (error.SqlState == "23514") { denied = true; }
        Check(denied && before == await Counts());
    }
    private async Task NegativeSql(Guid draft)
    {
        await Deny(["23514"], (c,t) => Exec(c,t,"UPDATE procurement.manual_payment_decisions SET reason = 'Attempted rewrite' WHERE manual_payment_request_id = @id", ("id",draft)));
        await Deny(["23514"], (c,t) => Exec(c,t,"DELETE FROM procurement.manual_payment_receipts WHERE manual_payment_request_id = @id", ("id",draft)));
        await Deny(["23514"], (c,t) => Exec(c,t,"DELETE FROM procurement.manual_payment_requests WHERE id = @id", ("id",draft)));
        await Deny(["23514"], (c,t) => Exec(c,t,"UPDATE procurement.manual_payment_requests SET amount = 'NaN'::numeric, version = version + 1 WHERE id = @id", ("id",draft)));
        await Deny(["23514"], (c,t) => Exec(c,t,"UPDATE procurement.manual_payment_requests SET creator_principal_id = @actor, version = version + 1 WHERE id = @id", ("id",draft),("actor",recorder)));
        await Deny(["23514"], async(c,t) => await RawBundle(c,t,Guid.NewGuid(), createParent:true, history:false, receipt:false));
        await Deny(["23514"], async(c,t) => await RawBundle(c,t,draft, createParent:false, version:2, action:"Submit", from:"Draft", status:"PendingApproval"));
        await Deny(["23505"], async(c,t) => await RawBundle(c,t,draft, createParent:false, history:false));
        await Deny(["23514"], async(c,t) => await RawBundle(c,t,Guid.NewGuid(), createParent:true, ackStatus:"ConfirmedManually"));
        await Deny(["23514"], async(c,t) => await RawBundle(c,t,Guid.NewGuid(), createParent:true, receiptActor:approver));
        await Deny(["23514"], async(c,t) => {
            await Exec(c,t,"UPDATE procurement.manual_payment_requests SET status='Approved', approved_amount=amount, approved_by_principal_id=@actor, approved_at=@at, updated_at=@at, updated_by=@actor, version=2 WHERE id=@id", ("id",draft),("actor",approver),("at",DateTime.UtcNow));
            await RawBundle(c,t,draft, createParent:false, version:2, action:"Approve", from:"PendingApproval", status:"Approved", actor:approver);
        });
    }
    private async Task RawBundle(NpgsqlConnection c, NpgsqlTransaction t, Guid id, bool createParent, bool history=true, bool receipt=true,
        long version=1, string action="Create", string from="None", string status="Draft", string? actor=null, string? receiptActor=null, string? ackStatus=null)
    {
        actor ??= creator;
        if (createParent) await Exec(c,t,"INSERT INTO procurement.manual_payment_requests (id,tenant_id,legal_entity_id,branch_id,supplier_id,supplier_invoice_id,currency_code,amount,status,creator_principal_id,approver_principal_id,recorder_principal_id,confirmer_principal_id,created_at,created_by,version) "
            + "VALUES (@id,@tenant,@company,@branch,@supplier,@invoice,'ZMW',25.1250,'Draft',@creator,@approver,@recorder,@confirmer,@at,@creator,1)",
            ("id",id),("tenant",Tenant),("company",company),("branch",branch),("supplier",invoice.SupplierId),("invoice",invoice.Id),("creator",creator),("approver",approver),("recorder",recorder),("confirmer",confirmer),("at",DateTime.UtcNow));
        if (history) await Exec(c,t,"INSERT INTO procurement.manual_payment_decisions (id,tenant_id,legal_entity_id,branch_id,manual_payment_request_id,action,from_status,to_status,from_version,to_version,actor_principal_id,occurred_at,created_at,created_by,version,new_amount) "
            + "VALUES (@id,@tenant,@company,@branch,@request,@action,@from,@status,@previous,@version,@actor,@at,@at,@actor,1,CASE WHEN @action = 'Create' THEN 25.1250 ELSE NULL END)",
            ("id",Guid.NewGuid()),("tenant",Tenant),("company",company),("branch",branch),("request",id),("action",action),("from",from),("status",status),("previous",version-1),("version",version),("actor",actor),("at",DateTime.UtcNow));
        if (receipt) await Exec(c,t,"INSERT INTO procurement.manual_payment_receipts (id,tenant_id,legal_entity_id,branch_id,manual_payment_request_id,actor_principal_id,command_key,action,request_sha256,response_json,result_version,created_at,created_by,version) "
            + "VALUES (@id,@tenant,@company,@branch,@request,@actor,@key,@action,@hash,@body,@version,@at,@actor,1)",
            ("id",Guid.NewGuid()),("tenant",Tenant),("company",company),("branch",branch),("request",id),("actor",receiptActor??actor),("key",Guid.NewGuid()),("action",action),("hash",new string('a',64)),
            ("body",JsonSerializer.Serialize(new { id, version, status=ackStatus??status })),("version",version),("at",DateTime.UtcNow));
    }
}
