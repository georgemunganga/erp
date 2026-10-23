using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("MANUAL_PAYMENT_GOVERNANCE_PROBE_CONNECTION")
    ?? throw new InvalidOperationException("A new private synthetic PostgreSQL database is required.");
var address = new NpgsqlConnectionStringBuilder(connection);
if (address.Host != "/private-pg" || !Regex.IsMatch(address.Database ?? "",
        @"\Aproc_payment_governance_20261023_[a-z0-9_]{1,24}\z", RegexOptions.CultureInvariant))
    throw new InvalidOperationException("Only /private-pg and a newly named synthetic governance database are allowed.");

GovernanceProbe? probe = null;
try
{
    probe = new GovernanceProbe(connection);
    await probe.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_MANUAL_PAYMENT_GOVERNANCE_POSTGRES",
        probe.Checks, synthetic = true, httpVerified = false, authProof = false, bankExecution = false }));
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { status = "FAIL_MANUAL_PAYMENT_GOVERNANCE_POSTGRES",
        phase = probe?.Phase, completedChecks = probe?.Checks, errorType = error.GetType().Name,
        sqlState = (error as PostgresException ?? error.GetBaseException() as PostgresException)?.SqlState,
        errorCode = Regex.IsMatch(error.Message, @"\A(?:ManualPayment|OperationalPolicy)[A-Za-z0-9]+\z") ? error.Message : null }));
    Environment.ExitCode = 1;
}

sealed partial class GovernanceProbe(string connection)
{
    private const string Tenant = "synthetic-payment-governance";
    private const string Rules = """
        {"schemaVersion":1,"identitySchemaVersion":2,"policyKey":"MANUAL_PAYMENT_REVIEW","transactionType":"ManualPaymentReview","approvalType":"SingleApprover","independentCreatorApprover":true,"independentRecorderConfirmer":true,"maximumByCurrency":{"ZMW":"100.0000"}}
        """;
    private readonly Guid company = Guid.NewGuid(), branch = Guid.NewGuid();
    private readonly string creator = Guid.NewGuid().ToString("D"), reviewer = Guid.NewGuid().ToString("D"),
        approver = Guid.NewGuid().ToString("D"), recorder = Guid.NewGuid().ToString("D"),
        confirmer = Guid.NewGuid().ToString("D");
    private Guid invoiceId, supplierId, policyId;
    public int Checks { get; private set; }
    public string Phase { get; private set; } = "empty-database-admission";

    // Deliberately synthetic Core principal projections. No HR employee or HTTP-auth claim is made.
    private sealed record Scope(string TenantId, Guid LegalEntityId, Guid? WorkLocationId, string SubjectId)
        : IProcurementScope, IApprovalScope
    {
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public Guid? WorkerId => null;
        public Guid? CoreLinkedWorkerId => null;
        public string? CorrelationId => "synthetic-governance-probe";
    }

    private Scope As(string actor) => new(Tenant, company, branch, actor);
    private ProcurementDbContext Procurement(string actor) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
            provider => provider.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, As(actor));
    private ApprovalDbContext Approvals() => new(
        new DbContextOptionsBuilder<ApprovalDbContext>().UseNpgsql(connection,
            provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval")).Options, As(creator));
    private ProcurementApprovalTransaction Coordinator(string actor) => new(connection, As(actor), As(actor));
    private static DateTimeOffset Now() => ProcurementEvidenceEncoding.DatabaseTimestamp(TimeProvider.System.GetUtcNow())!.Value;
    private void Check(bool assertion)
    { if (!assertion) throw new InvalidOperationException("Synthetic governance invariant failed."); Checks++; }

    public async Task RunAsync()
    {
        await using (var database = new NpgsqlConnection(connection))
        {
            await database.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname NOT IN ('pg_catalog','information_schema')
                        AND n.nspname NOT LIKE 'pg_toast%' AND n.nspname NOT LIKE 'pg_temp%'
                        AND c.relkind IN ('r','p','v','m','S','f'))
                    + (SELECT count(*) FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema','public')
                        AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
                    + (SELECT count(*) FROM pg_extension WHERE extname <> 'plpgsql')
                """, database);
            Check((long)(await command.ExecuteScalarAsync())! == 0);
        }
        Phase = "owning-core-then-procurement-migrations";
        await using (var core = Approvals()) await core.Database.MigrateAsync();
        await using (var procurement = Procurement(creator))
        {
            Check(!procurement.Database.HasPendingModelChanges());
            await procurement.Database.MigrateAsync();
            Check((await procurement.Database.GetAppliedMigrationsAsync()).Last().EndsWith("P28ManualPaymentGovernance", StringComparison.Ordinal));
            // Genuine empty downgrade/upgrade; no retained data is removed.
            await procurement.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                .MigrateAsync("20261023125243_P27ManualPaymentRecords");
            await procurement.Database.MigrateAsync();
            Check(!procurement.Database.HasPendingModelChanges());
        }
        Phase = "synthetic-invoice-and-supplier";
        await using (var db = Procurement(creator))
        {
            var supplier = new Supplier { Number = "SYN-GOV-1", LegalName = "Synthetic governance supplier",
                DisplayName = "Synthetic governance supplier", CountryCode = "ZM" };
            db.AddScoped(supplier); supplierId = supplier.Id;
            var invoice = new SupplierInvoice { SupplierId = supplierId, BranchRef = branch,
                CurrencyCode = "ZMW", InvoiceNumber = "SYN-GOV-INV-1", NormalizedInvoiceNumber = "SYN-GOV-INV-1",
                IntakeChannel = "Manual", InvoiceDate = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(-1),
                NetAmount = 100m, GrossAmount = 100m };
            db.AddScoped(invoice); invoiceId = invoice.Id;
            await db.SaveChangesAsync();
        }
        await PublishPolicyAsync();
        await VerifySqlPolicyDigestAsync();

        Phase = "shared-create";
        var first = await CreateAsync(25.1250m);
        Phase = "shared-submit"; await SubmitAsync(first);
        Phase = "shared-approve"; await ApproveAsync(first);
        Phase = "shared-record"; await RecordAsync(first);
        Phase = "shared-confirm"; await ConfirmAsync(first);

        Phase = "return-resubmit-maker-cancel";
        var second = await CreateAsync(10m);
        await SubmitAsync(second);
        await ReturnAsync(second);
        await SubmitAsync(second);
        await CancelAsync(second);

        Phase = "direct-write-without-shared-transaction-denied";
        await using (var db = Procurement(creator))
        {
            var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == invoiceId);
            var source = new ManualPaymentInvoiceSource(invoice.Id, Tenant, company, branch, supplierId,
                "ZMW", invoice.InvoiceDate, invoice.GrossAmount, 50m, true) { InvoiceVersion = invoice.Version };
            var forbidden = ManualPaymentWorkflow.Create(Draft(1m), Actor(creator), Assignee(approver, "approve"),
                Assignee(recorder, "record"), Assignee(confirmer, "confirm"), source, Now());
            Persist(db, forbidden, creator, Guid.NewGuid(), create: true);
            try { await db.SaveChangesAsync(); throw new InvalidOperationException("Direct manual payment write unexpectedly passed."); }
            catch (InvalidOperationException error) when (error.Message == "ManualPaymentCoreTransactionRequired") { Checks++; }
        }

        Phase = "retained-owning-evidence";
        await using var verify = Procurement(creator);
        await using var coreVerify = Approvals();
        var confirmed = await verify.ManualPaymentRequests.SingleAsync(x => x.Id == first);
        var cancelled = await verify.ManualPaymentRequests.SingleAsync(x => x.Id == second);
        Check(confirmed.Status == "ConfirmedManually" && cancelled.Status == "Cancelled");
        Check(await verify.ManualPaymentWorkflowCases.CountAsync() == 4); // First approval + confirmation, second approval + resubmission.
        Check(await verify.ManualPaymentCoreDecisions.CountAsync() == 4); // Approve, Confirm, Return, maker Cancel.
        Check(await verify.ManualPaymentDateObservations.CountAsync() == 2); // Record and Confirm.
        Check(await coreVerify.Cases.CountAsync() == 4 && await coreVerify.Decisions.CountAsync() == 4);
        var sourceLinks = await verify.ManualPaymentWorkflowCases.AsNoTracking().ToArrayAsync();
        var coreCases = await coreVerify.Cases.AsNoTracking().ToArrayAsync();
        Check(sourceLinks.All(link => coreCases.Any(owner => owner.Id == link.CoreApprovalCaseId
            && owner.SourceModule == "procurement" && owner.SourceId == link.ManualPaymentRequestId
            && owner.SourceVersion == link.OpenedRequestVersion && owner.PolicyVersionId == policyId
            && owner.SourceType == (link.Purpose == ManualPaymentCoreWorkflow.Approval
                ? "manual_payment_request" : "manual_payment_confirmation"))));
        var coreDecisions = await coreVerify.Decisions.AsNoTracking().ToArrayAsync();
        var sourceDecisions = await verify.ManualPaymentCoreDecisions.AsNoTracking().ToArrayAsync();
        var receipts = await verify.ManualPaymentReceipts.AsNoTracking().ToArrayAsync();
        Check(sourceDecisions.All(link => coreDecisions.Any(owner => owner.Id == link.CoreApprovalDecisionId
            && owner.ActorSubjectId == link.ActorPrincipalId
            && receipts.Any(receipt => receipt.ManualPaymentRequestId == link.ManualPaymentRequestId
                && receipt.ResultVersion == link.ToRequestVersion
                && owner.IdempotencyKey == receipt.CommandKey.ToString("D")))));
        Check(await verify.ManualPaymentDecisions.CountAsync() == 10 && receipts.Length == 10);
        Check(await verify.OperationalPolicyIdentityRecords.CountAsync(x => x.PolicyVersionId == policyId) == 3);
        Check(await verify.FinancePaymentStatusRefs.CountAsync() == 0
            && (await verify.SupplierInvoices.SingleAsync(x => x.Id == invoiceId)).FinancePostingRef is null);
        Check(await verify.AuditEvents.CountAsync(x => x.RecordType == nameof(ManualPaymentRequest)) >= 2);
        await NegativeChecksAsync(first);
    }

    private async Task PublishPolicyAsync()
    {
        Phase = "policy-created-submitted-published";
        _ = ManualPaymentReviewPolicyRules.Parse(Rules);
        var createdAt = Now();
        OperationalPolicyWorkerIdentityEvidence created, submitted;
        await using (var db = Procurement(creator))
        {
            var policy = new PolicyVersion { TenantId = Tenant, LegalEntityId = company,
                PolicyKey = ManualPaymentReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1, State = "Draft",
                RulesJson = Rules, EffectiveFrom = createdAt.AddMinutes(-1), CreatedAt = createdAt,
                CreatedBy = creator, Version = 1 };
            policyId = policy.Id;
            created = OperationalPolicyWorkerIdentity.Capture(policy, "Created", creator, null, 1, createdAt, schemaVersion: 2);
            db.AddScoped(policy); db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(created));
            await db.SaveChangesAsync();
        }
        await using (var db = Procurement(creator))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            policy.State = "PendingReview"; policy.ReviewRequestedAt = Now();
            policy.ReviewRequestedBy = creator; policy.ReviewReason = "Synthetic review request";
            submitted = OperationalPolicyWorkerIdentity.Capture(policy, "Submitted", creator, null,
                policy.Version + 1, policy.ReviewRequestedAt.Value, created, schemaVersion: 2);
            db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(submitted));
            await db.SaveChangesAsync();
        }
        await using (var db = Procurement(reviewer))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            policy.State = "Published"; policy.PublishedAt = Now(); policy.PublishedBy = reviewer;
            policy.PublicationReason = "Synthetic independent approval";
            var published = OperationalPolicyWorkerIdentity.Capture(policy, "Published", reviewer, null,
                policy.Version + 1, policy.PublishedAt.Value, created, submitted, schemaVersion: 2);
            db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(published));
            await db.SaveChangesAsync();
            OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, published);
            Check(policy.State == "Published" && policy.Version == 3);
        }
    }

    private ManualPaymentDraft Draft(decimal amount) => new(branch, invoiceId, supplierId, "ZMW", amount,
        approver, recorder, confirmer, "Synthetic external settlement evidence", "Synthetic invoice payment");
    private ManualPaymentActor Actor(string principal) => new(principal, Tenant, company, branch,
        new HashSet<Guid> { branch }, new[] { "create", "edit", "submit", "approve", "record", "confirm", "cancel" }
            .Select(action => "procurement-payment-" + action).ToHashSet(), true);
    private ManualPaymentAssignee Assignee(string principal, string action) => new(principal, Tenant, company,
        new HashSet<Guid> { branch }, new HashSet<string> { "procurement-payment-" + action }, true);
    private async Task<ManualPaymentInvoiceSource> Source(ProcurementDbContext db, string actor, Guid? excluding = null)
        => (await ManualPaymentCapacityReader.ReadLockedAsync(db, As(actor), invoiceId, branch,
            new HashSet<Guid> { branch }, excluding)).Source;
    private static async Task<ManualPaymentRequest> Request(ProcurementDbContext db, Guid id)
        => await db.ManualPaymentRequests.AsNoTracking().SingleAsync(x => x.Id == id);
    private static async Task<ApprovalCase> Case(ApprovalDbContext db, Guid id)
        => await db.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleAsync(x => x.Id == id);
    private static async Task<ManualPaymentWorkflowCaseLink> Link(ProcurementDbContext db, Guid id, string purpose)
        => await db.ManualPaymentWorkflowCases.AsNoTracking()
            .Where(x => x.ManualPaymentRequestId == id && x.Purpose == purpose)
            .OrderByDescending(x => x.OpenedRequestVersion).FirstAsync();
    private static void Persist(ProcurementDbContext db, ManualPaymentChange change, string actor, Guid commandKey, bool create = false)
    {
        if (create) db.AddScoped(change.Request);
        else
        {
            var tracked = db.ManualPaymentRequests.Single(x => x.Id == change.Request.Id);
            db.Entry(tracked).CurrentValues.SetValues(change.Request);
        }
        db.AddScoped(change.Decision);
        db.AddScoped(new ManualPaymentCommandReceipt { TenantId = change.Request.TenantId,
            LegalEntityId = change.Request.LegalEntityId, BranchId = change.Request.BranchId,
            ManualPaymentRequestId = change.Request.Id, ActorPrincipalId = actor, CommandKey = commandKey,
            Action = change.Decision.Action, ResultVersion = change.Decision.ToVersion,
            RequestSha256 = ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new {
                commandKey, actor, action = change.Decision.Action, requestId = change.Request.Id, version = change.Decision.ToVersion })),
            ResponseJson = JsonSerializer.Serialize(new { id = change.Request.Id, version = change.Decision.ToVersion,
                status = change.Request.Status }) });
    }

    private Task<Guid> CreateAsync(decimal amount)
        => Coordinator(creator).ExecuteManualPaymentAsync(async (db, _, _) =>
        {
            var source = await Source(db, creator);
            var change = ManualPaymentWorkflow.Create(Draft(amount), Actor(creator), Assignee(approver, "approve"),
                Assignee(recorder, "record"), Assignee(confirmer, "confirm"), source, Now());
            Persist(db, change, creator, Guid.NewGuid(), create: true);
            return change.Request.Id;
        });

    private Task<bool> SubmitAsync(Guid id)
        => Coordinator(creator).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id); var source = await Source(db, creator, id);
            var change = ManualPaymentWorkflow.Submit(current, current.Version, Actor(creator), source, Now());
            var policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == policyId);
            var opening = ManualPaymentCoreWorkflow.Open(change, policy, source, change.Decision.OccurredAt);
            core.AddCase(opening.Case); Persist(db, change, creator, Guid.NewGuid()); db.AddScoped(opening.Link);
            return true;
        });

    private Task<bool> ApproveAsync(Guid id)
        => Coordinator(approver).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id); var source = await Source(db, approver, id);
            var link = await Link(db, id, ManualPaymentCoreWorkflow.Approval);
            var approval = await Case(core, link.CoreApprovalCaseId);
            var change = ManualPaymentWorkflow.Approve(current, current.Version, Actor(approver), source, Now());
            DecideAndLink(db, core, change, link, approval, approver, "Approve");
            return true;
        });

    private Task<bool> ReturnAsync(Guid id)
        => Coordinator(approver).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id); var link = await Link(db, id, ManualPaymentCoreWorkflow.Approval);
            var approval = await Case(core, link.CoreApprovalCaseId);
            var change = ManualPaymentWorkflow.Return(current, current.Version, Actor(approver), "Review again", Now());
            DecideAndLink(db, core, change, link, approval, approver, "Return");
            return true;
        });

    private Task<bool> CancelAsync(Guid id)
        => Coordinator(creator).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id); var link = await Link(db, id, ManualPaymentCoreWorkflow.Approval);
            var approval = await Case(core, link.CoreApprovalCaseId);
            var change = ManualPaymentWorkflow.Cancel(current, current.Version, Actor(creator), "Synthetic maker withdrawal", Now());
            DecideAndLink(db, core, change, link, approval, creator, "Cancel");
            return true;
        });

    private Task<bool> RecordAsync(Guid id)
        => Coordinator(recorder).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id); var source = await Source(db, recorder, id);
            var approvalLink = await Link(db, id, ManualPaymentCoreWorkflow.Approval);
            var approval = await Case(core, approvalLink.CoreApprovalCaseId);
            var at = Now(); var date = DateOnly.FromDateTime(at.UtcDateTime); // Explicit private UTC fixture; no browser-provided date.
            var change = ManualPaymentWorkflow.Record(current, current.Version, Actor(recorder), source,
                new(date, "External transfer", "SYN-GOV-EXTERNAL-1", current.Amount, null), date, at);
            var policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == approvalLink.PolicyVersionId);
            var opening = ManualPaymentCoreWorkflow.Open(change, policy, source, at, approvalLink, approval);
            core.AddCase(opening.Case); Persist(db, change, recorder, Guid.NewGuid()); db.AddScoped(opening.Link);
            db.AddScoped(ManualPaymentCoreWorkflow.ObserveDate(change, date, "UTC", at));
            return true;
        });

    private Task<bool> ConfirmAsync(Guid id)
        => Coordinator(confirmer).ExecuteManualPaymentAsync(async (db, core, _) =>
        {
            var current = await Request(db, id);
            var link = await Link(db, id, ManualPaymentCoreWorkflow.Confirmation);
            var confirmation = await Case(core, link.CoreApprovalCaseId);
            var at = Now(); var date = DateOnly.FromDateTime(at.UtcDateTime);
            var change = ManualPaymentWorkflow.Confirm(current, current.Version, Actor(confirmer), date, at);
            DecideAndLink(db, core, change, link, confirmation, confirmer, "Approve");
            db.AddScoped(ManualPaymentCoreWorkflow.ObserveDate(change, date, "UTC", at));
            return true;
        });

    private static void DecideAndLink(ProcurementDbContext db, ApprovalDbContext core, ManualPaymentChange change,
        ManualPaymentWorkflowCaseLink link, ApprovalCase approval, string actor, string action)
    {
        var commandKey = Guid.NewGuid(); var decisions = new List<ApprovalDecision>();
        var assignment = approval.Stages.Single().Assignments.Single();
        var principal = new ApprovalActor(actor, change.Request.TenantId, change.Request.LegalEntityId,
            new HashSet<string> { "procurement-payment-approve", "procurement-payment-confirm", "procurement-payment-cancel" });
        if (action == "Cancel")
            ApprovalEngine.Cancel(approval, assignment, principal, "procurement-payment-cancel",
                commandKey.ToString("D"), change.Decision.Reason!, decisions, change.Decision.OccurredAt);
        else
            ApprovalEngine.Decide(approval, assignment, principal, action, commandKey.ToString("D"),
                change.Decision.Reason, decisions, change.Decision.OccurredAt);
        var decision = decisions.Single(); core.Decisions.Add(decision);
        Persist(db, change, actor, commandKey);
        db.AddScoped(ManualPaymentCoreWorkflow.LinkDecision(change, link, approval, decision));
    }
}
