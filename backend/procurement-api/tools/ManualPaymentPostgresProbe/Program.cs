using System.Text.Json;
using System.Text.RegularExpressions;
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
        synthetic = true, httpVerified = false, financeOrBankExecution = false }));
} catch (Exception error) {
    Console.WriteLine(JsonSerializer.Serialize(new { status = "FAIL_MANUAL_PAYMENT_POSTGRES_STORAGE", errorType = error.GetType().Name, phase = probe?.Phase,
        sqlState = (error as PostgresException ?? error.InnerException as PostgresException)?.SqlState }));
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
            await db.GetService<IMigrator>().MigrateAsync(previous); await db.Database.MigrateAsync();
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
            Check(await db.FinancePaymentStatusRefs.CountAsync() == 0 && (await db.SupplierInvoices.SingleAsync()).FinancePostingRef is null);
        }
        await using (var foreign = Db(selectedBranch: Guid.NewGuid())) Check(!await foreign.ManualPaymentRequests.AnyAsync());
        var draft = ManualPaymentWorkflow.Create(Draft(), Actor(creator), Assignee(approver, "approve"), Assignee(recorder, "record"), Assignee(confirmer, "confirm"), Source(), DateTimeOffset.UtcNow);
        await Save(draft, creator, true);
        Phase = "negative-postgres-sql"; await NegativeSql(draft.Request.Id);
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
        if (create) db.AddScoped(change.Request);
        else { var stored = await db.ManualPaymentRequests.SingleAsync(x => x.Id == change.Request.Id); db.Entry(stored).CurrentValues.SetValues(change.Request); }
        db.AddScoped(change.Decision);
        db.AddScoped(new ManualPaymentCommandReceipt { TenantId = Tenant, LegalEntityId = company, BranchId = branch, ManualPaymentRequestId = change.Request.Id,
            ActorPrincipalId = actor, CommandKey = Guid.NewGuid(), Action = change.Decision.Action, RequestSha256 = new string('a', 64), ResultVersion = change.Decision.ToVersion,
            ResponseJson = JsonSerializer.Serialize(new { id = change.Request.Id, version = change.Decision.ToVersion, status = change.Request.Status }) });
        await db.SaveChangesAsync(); Checks++;
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
