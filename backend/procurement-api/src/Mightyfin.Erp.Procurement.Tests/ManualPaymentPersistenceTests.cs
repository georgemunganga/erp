using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

// Mapped owning-context evidence. PostgreSQL constraints/API/receipt races need their own gate.
public sealed class ManualPaymentPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, Guid Branch, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId { get; init; } = Branch;
        public bool IsConfined { get; init; }
        public Guid? OrgUnitId { get; init; }
        public Guid? CoreLinkedWorkerId { get; init; }
        public string? CorrelationId => "manual-payment-test";
    }
    private static Scope Owner() => new("payment-tests", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("D"));
    private static DbContextOptions<ProcurementDbContext> Options() => new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public async Task System_only_user_saves_request_history_exact_replay_and_audit_together_then_revises_with_CAS()
    {
        var scope = Owner(); var options = Options();
        await using var db = new ProcurementDbContext(options, scope);
        var invoice = await SeedInvoice(db, scope);
        var made = Create(scope, invoice); var first = Bundle(db, made, scope);
        await db.SaveChangesAsync();
        Assert.Null(made.Request.CreatorCoreLinkedWorkerId);
        Assert.Single(await db.ManualPaymentRequests.ToArrayAsync());
        Assert.Single(await db.ManualPaymentDecisions.ToArrayAsync());
        Assert.Equal(first.ResponseJson, (await db.ManualPaymentReceipts.SingleAsync()).ResponseJson);
        var actor = Actor(scope); var draft = Draft(scope, invoice) with { Amount = 12.3456m };
        var revision = ManualPaymentWorkflow.Revise(made.Request, 1, draft, actor, Assignee(scope, draft.ApproverPrincipalId, "approve"),
            Assignee(scope, draft.RecorderPrincipalId, "record"), Assignee(scope, draft.ConfirmerPrincipalId, "confirm"), Source(scope, invoice), DateTimeOffset.UtcNow);
        db.Entry(made.Request).CurrentValues.SetValues(revision.Request); Bundle(db, revision, scope, includeRequest: false);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.ManualPaymentRequests.SingleAsync();
        Assert.Equal(2, saved.Version); Assert.Equal(12.3456m, saved.Amount);
        Assert.Equal(2, await db.ManualPaymentDecisions.CountAsync()); Assert.Equal(2, await db.ManualPaymentReceipts.CountAsync());
        Assert.All(await db.AuditEvents.Where(x => x.RecordType.StartsWith("ManualPayment")).ToArrayAsync(), x => {
            using var json = JsonDocument.Parse(Assert.IsType<string>(x.ChangedFieldsJson));
            Assert.Equal("ManualPayment", json.RootElement.GetProperty("principalEvidenceKind").GetString());
        });
    }

    [Theory]
    [InlineData("empty")][InlineData("version")][InlineData("status")][InlineData("hash-newline")]
    public async Task Receipt_must_acknowledge_the_exact_saved_record_version_and_state(string malformed)
    {
        var owner = Owner(); var options = Options();
        await using var db = new ProcurementDbContext(options, owner);
        var invoice = await SeedInvoice(db, owner); var audit = await db.AuditEvents.CountAsync();
        var made = Create(owner, invoice); var receipt = Bundle(db, made, owner);
        if (malformed == "empty") receipt.ResponseJson = "{}";
        if (malformed == "version") receipt.ResponseJson = JsonSerializer.Serialize(new { id = made.Request.Id, version = 2, status = "Draft" });
        if (malformed == "status") receipt.ResponseJson = JsonSerializer.Serialize(new { id = made.Request.Id, version = 1, status = "ConfirmedManually" });
        if (malformed == "hash-newline") receipt.RequestSha256 += "\n";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var read = new ProcurementDbContext(options, owner);
        Assert.Empty(await read.ManualPaymentRequests.ToArrayAsync()); Assert.Equal(audit, await read.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("history")][InlineData("receipt")]
    public async Task Incomplete_bundle_cannot_save_a_request_or_its_audit(string missing)
    {
        var scope = Owner(); var options = Options();
        await using var db = new ProcurementDbContext(options, scope);
        var invoice = await SeedInvoice(db, scope); var count = await db.AuditEvents.CountAsync();
        var made = Create(scope, invoice); var receipt = Bundle(db, made, scope);
        db.Entry(missing == "history" ? (object)made.Decision : receipt).State = EntityState.Detached;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var read = new ProcurementDbContext(options, scope);
        Assert.Empty(await read.ManualPaymentRequests.ToArrayAsync()); Assert.Equal(count, await read.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("branch")][InlineData("confined")][InlineData("department")]
    public async Task Owning_context_filters_requests_history_and_receipts_by_scope(string field)
    {
        var owner = Owner(); var options = Options();
        await using (var seed = new ProcurementDbContext(options, owner)) {
            var invoice = await SeedInvoice(seed, owner); Bundle(seed, Create(owner, invoice), owner); await seed.SaveChangesAsync();
        }
        var scope = field switch {
            "tenant" => owner with { TenantId = "another" },
            "company" => owner with { LegalEntityId = Guid.NewGuid() },
            "branch" => owner with { WorkLocationId = Guid.NewGuid() },
            "confined" => owner with { WorkLocationId = null, IsConfined = true },
            _ => owner with { OrgUnitId = Guid.NewGuid() }
        };
        await using var read = new ProcurementDbContext(options, scope);
        Assert.Empty(await read.ManualPaymentRequests.ToArrayAsync()); Assert.Empty(await read.ManualPaymentDecisions.ToArrayAsync());
        Assert.Empty(await read.ManualPaymentReceipts.ToArrayAsync());
    }

    [Theory]
    [InlineData("decision", false)][InlineData("decision", true)]
    [InlineData("receipt", false)][InlineData("receipt", true)]
    public async Task Persisted_history_and_command_receipts_are_append_only(string kind, bool delete)
    {
        var owner = Owner(); var options = Options();
        await using var db = new ProcurementDbContext(options, owner);
        var invoice = await SeedInvoice(db, owner); Bundle(db, Create(owner, invoice), owner); await db.SaveChangesAsync();
        var auditCount = await db.AuditEvents.CountAsync();
        db.ChangeTracker.Clear();
        object record = kind == "decision" ? await db.ManualPaymentDecisions.SingleAsync() : await db.ManualPaymentReceipts.SingleAsync();
        if (delete) db.Entry(record).State = EntityState.Deleted;
        else if (record is ManualPaymentDecision decision) decision.Reason = "Attempted rewrite";
        else ((ManualPaymentCommandReceipt)record).ResponseJson = "{\"rewritten\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var read = new ProcurementDbContext(options, owner);
        Assert.Single(await read.ManualPaymentDecisions.ToArrayAsync()); Assert.Single(await read.ManualPaymentReceipts.ToArrayAsync());
        Assert.Equal(auditCount, await read.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("creator")][InlineData("creator-link")][InlineData("source")][InlineData("currency")]
    public async Task Detached_relabelled_source_or_identity_is_rejected_against_persisted_evidence(string changed)
    {
        var owner = Owner(); var options = Options(); ManualPaymentRequest original;
        await using (var seed = new ProcurementDbContext(options, owner)) {
            var invoice = await SeedInvoice(seed, owner); var made = Create(owner, invoice); Bundle(seed, made, owner); await seed.SaveChangesAsync(); original = made.Request;
        }
        var detached = JsonSerializer.Deserialize<ManualPaymentRequest>(JsonSerializer.Serialize(original))!;
        if (changed == "creator") detached.CreatorPrincipalId = Guid.NewGuid().ToString("D");
        if (changed == "creator-link") detached.CreatorCoreLinkedWorkerId = Guid.NewGuid();
        if (changed == "source") detached.SupplierInvoiceId = Guid.NewGuid();
        if (changed == "currency") detached.CurrencyCode = "USD";
        await using var writer = new ProcurementDbContext(options, owner);
        writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
        await using var read = new ProcurementDbContext(options, owner);
        Assert.Equal(original.CreatorPrincipalId, (await read.ManualPaymentRequests.SingleAsync()).CreatorPrincipalId);
        Assert.Single(await read.ManualPaymentReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Foreign_source_cannot_be_relabelled_by_AddScoped()
    {
        var owner = Owner(); await using var db = new ProcurementDbContext(Options(), owner);
        var invoice = await SeedInvoice(db, owner); var made = Create(owner, invoice);
        made.Request.LegalEntityId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.AddScoped(made.Request));
        Assert.Empty(await db.ManualPaymentRequests.ToArrayAsync());
    }

    [Fact]
    public void Model_retains_scoped_foreign_keys_exact_amounts_CAS_and_actor_scoped_unique_commands()
    {
        var owner = Owner(); using var db = new ProcurementDbContext(Options(), owner);
        var model = db.GetService<IDesignTimeModel>().Model;
        var request = model.FindEntityType(typeof(ManualPaymentRequest))!;
        Assert.Equal(18, request.FindProperty(nameof(ManualPaymentRequest.Amount))!.GetPrecision());
        Assert.Equal(4, request.FindProperty(nameof(ManualPaymentRequest.Amount))!.GetScale());
        Assert.True(request.FindProperty(nameof(ManualPaymentRequest.Version))!.IsConcurrencyToken);
        var receipt = model.FindEntityType(typeof(ManualPaymentCommandReceipt))!;
        Assert.Contains(receipt.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "ActorPrincipalId", "CommandKey" }));
        Assert.Contains(receipt.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(ManualPaymentDecision));
        Assert.All(request.GetForeignKeys(), key => Assert.Contains(key.Properties, p => p.Name == "TenantId"));
        Assert.Contains(request.GetCheckConstraints(), c => c.Name == "ck_manual_payment_state_evidence");
    }

    [Fact]
    public async Task A_real_manual_payment_draft_freezes_invoice_correction_and_registered_credit_capacity()
    {
        var scope = Owner(); await using var db = new ProcurementDbContext(Options(), scope);
        var invoice = await SeedInvoice(db, scope); Assert.True(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
        var made = Create(scope, invoice); Bundle(db, made, scope); await db.SaveChangesAsync();
        Assert.False(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
        // A Draft does not reserve the amount. It still anchors the original invoice.
        invoice.GrossAmount = invoice.NetAmount = 50m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); invoice = await db.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id);
        var stored = await db.ManualPaymentRequests.SingleAsync();
        var actor = Actor(scope) with { Actions = new HashSet<string> { "procurement-payment-submit" } };
        var submitted = ManualPaymentWorkflow.Submit(stored, 1, actor, Source(scope, invoice), DateTimeOffset.UtcNow);
        db.Entry(stored).CurrentValues.SetValues(submitted.Request); Bundle(db, submitted, scope, false); await db.SaveChangesAsync();
        Assert.Equal("ManualPaymentRegisteredAmountExceedsInvoice", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, invoice, null, 80m))).Message);
        await ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, invoice, null, 74.875m);
        Assert.Empty(await db.SupplierCreditNoteRefs.ToArrayAsync()); // Guard does not post a credit.
    }

    [Theory]
    [InlineData("Cancelled")][InlineData("Rejected")]
    public async Task Closed_manual_requests_release_capacity_but_do_not_unfreeze_original_invoice_history(string outcome)
    {
        var scope = Owner(); var options = Options(); await using var db = new ProcurementDbContext(options, scope);
        var invoice = await SeedInvoice(db, scope); var made = Create(scope, invoice); Bundle(db, made, scope); await db.SaveChangesAsync();
        var stored = await db.ManualPaymentRequests.SingleAsync();
        ManualPaymentChange changed;
        if (outcome == "Cancelled") changed = ManualPaymentWorkflow.Cancel(stored, 1,
            Actor(scope) with { Actions = new HashSet<string> { "procurement-payment-cancel" } }, "Withdrawn", DateTimeOffset.UtcNow);
        else {
            var submitted = ManualPaymentWorkflow.Submit(stored, 1, Actor(scope) with { Actions = new HashSet<string> { "procurement-payment-submit" } }, Source(scope, invoice), DateTimeOffset.UtcNow);
            db.Entry(stored).CurrentValues.SetValues(submitted.Request); Bundle(db, submitted, scope, false); await db.SaveChangesAsync();
            var approverScope = scope with { SubjectId = Approver };
            await using var checker = new ProcurementDbContext(options, approverScope);
            var current = await checker.ManualPaymentRequests.SingleAsync();
            changed = ManualPaymentWorkflow.Reject(current, 2,
                Actor(approverScope) with { Actions = new HashSet<string> { "procurement-payment-approve" } }, "Not proceeding", DateTimeOffset.UtcNow);
            checker.Entry(current).CurrentValues.SetValues(changed.Request); Bundle(checker, changed, approverScope, false); await checker.SaveChangesAsync();
            db.ChangeTracker.Clear(); stored = await db.ManualPaymentRequests.SingleAsync();
        }
        if (outcome == "Cancelled") { db.Entry(stored).CurrentValues.SetValues(changed.Request); Bundle(db, changed, scope, false); await db.SaveChangesAsync(); }
        Assert.False(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
        Assert.Equal(100m, ManualPaymentCapacity.Calculate(invoice, await db.ManualPaymentRequests.ToArrayAsync(), [], []).RemainingExcludingThisRequest);
    }

    [Fact]
    public async Task Finance_only_settlements_are_checked_before_accepting_a_credit_claim()
    {
        var scope = Owner(); await using var db = new ProcurementDbContext(Options(), scope); var invoice = await SeedInvoice(db, scope);
        db.AddScoped(new FinancePaymentStatusRef { SupplierInvoiceId = invoice.Id, CurrencyCode = invoice.CurrencyCode,
            FinancePaymentRef = "SYN-FINANCE-ONLY", Status = "Paid", PaidAmount = 100m, SyncedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal("ManualPaymentRegisteredAmountExceedsInvoice", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ManualPaymentCapacityReader.ValidateCreditClaimLockedAsync(db, invoice, null, 1m))).Message);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Database_timestamp_quantization_is_allowed_but_a_real_microsecond_source_change_is_not(bool materialChange)
    {
        var scope = Owner(); await using var db = new ProcurementDbContext(Options(), scope); var invoice = await SeedInvoice(db, scope);
        var made = Create(scope, invoice, new DateTimeOffset(2026,10,23,12,0,0,TimeSpan.Zero).AddTicks(7));
        Bundle(db, made, scope); await db.SaveChangesAsync();
        var draft = Draft(scope, invoice) with { Amount = 20m };
        var revised = ManualPaymentWorkflow.Revise(made.Request, 1, draft, Actor(scope), Assignee(scope, Approver, "approve"),
            Assignee(scope, Recorder, "record"), Assignee(scope, Confirmer, "confirm"), Source(scope, invoice), DateTimeOffset.UtcNow);
        revised.Request.CreatedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(made.Request.CreatedAt)!.Value.AddTicks(materialChange ? 10 : 0);
        db.Entry(made.Request).CurrentValues.SetValues(revised.Request); Bundle(db, revised, scope, false);
        if (materialChange) Assert.Equal("ManualPaymentSourceEvidenceImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        else { await db.SaveChangesAsync(); Assert.Equal(2, made.Request.Version); }
    }

    private static ManualPaymentActor Actor(Scope s) => new(s.SubjectId, s.TenantId, s.LegalEntityId, s.Branch,
        new HashSet<Guid> { s.Branch }, new HashSet<string> { "procurement-payment-create", "procurement-payment-edit" }, true)
        { CoreLinkedWorkerId = s.CoreLinkedWorkerId };
    private static readonly string Approver = Guid.NewGuid().ToString("D"), Recorder = Guid.NewGuid().ToString("D"), Confirmer = Guid.NewGuid().ToString("D");
    private static ManualPaymentDraft Draft(Scope s, SupplierInvoice i) => new(s.Branch, i.Id, i.SupplierId, i.CurrencyCode, 25.1250m,
        Approver, Recorder, Confirmer, "Synthetic external payment");
    private static ManualPaymentAssignee Assignee(Scope s, string principal, string action) => new(principal, s.TenantId, s.LegalEntityId,
        new HashSet<Guid> { s.Branch }, new HashSet<string> { "procurement-payment-" + action }, true);
    private static ManualPaymentInvoiceSource Source(Scope s, SupplierInvoice i) => new(i.Id, s.TenantId, s.LegalEntityId,
        s.Branch, i.SupplierId, i.CurrencyCode, i.InvoiceDate, i.GrossAmount, i.GrossAmount, true);
    private static ManualPaymentChange Create(Scope s, SupplierInvoice i, DateTimeOffset? at = null) => ManualPaymentWorkflow.Create(Draft(s, i), Actor(s),
        Assignee(s, Approver, "approve"), Assignee(s, Recorder, "record"), Assignee(s, Confirmer, "confirm"), Source(s, i), at ?? DateTimeOffset.UtcNow);
    private static ManualPaymentCommandReceipt Bundle(ProcurementDbContext db, ManualPaymentChange change, Scope s, bool includeRequest = true)
    {
        if (includeRequest) db.AddScoped(change.Request); db.AddScoped(change.Decision);
        var receipt = new ManualPaymentCommandReceipt { TenantId = s.TenantId, LegalEntityId = s.LegalEntityId, BranchId = s.Branch,
            ManualPaymentRequestId = change.Request.Id, ActorPrincipalId = s.SubjectId, CommandKey = Guid.NewGuid(),
            Action = change.Decision.Action, ResultVersion = change.Decision.ToVersion, RequestSha256 = new string('a', 64),
            ResponseJson = JsonSerializer.Serialize(new { id = change.Request.Id, version = change.Decision.ToVersion, status = change.Request.Status }) };
        db.AddScoped(receipt); return receipt;
    }
    private static async Task<SupplierInvoice> SeedInvoice(ProcurementDbContext db, Scope s)
    {
        var invoice = new SupplierInvoice { SupplierId = Guid.NewGuid(), BranchRef = s.Branch, InvoiceNumber = "MANUAL-1", NormalizedInvoiceNumber = "MANUAL-1",
            IntakeChannel = "Manual", CurrencyCode = "ZMW", InvoiceDate = new DateOnly(2026, 10, 20), NetAmount = 100m, GrossAmount = 100m };
        db.AddScoped(invoice); await db.SaveChangesAsync(); return invoice;
    }
}
