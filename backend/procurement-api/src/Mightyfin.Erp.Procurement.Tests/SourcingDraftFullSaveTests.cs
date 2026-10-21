using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Context guard/graph/version regression using synthetic retained domain approval references.
/// InMemory does not establish SQL FK validity or actual API authority; parent PostgreSQL gate must do so.</summary>
public sealed class SourcingDraftFullSaveTests
{
    [Fact]
    public async Task Exact_partial_hold_fresh_reload_restore_cancel_preserves_other_holders_and_Finance()
    {
        var f = new Fixture(); await f.SeedAsync();
        var other = await f.CreateAsync(2m);
        var target = await f.CreateAsync(0.0001m);
        Assert.NotEqual(other.EventId, target.EventId);
        await using var db = f.Db(); var source = await f.SourceAsync(db);
        var ev = await db.SourcingEvents.SingleAsync(x => x.Id == target.EventId);
        var evidence = await db.SourcingDraftEvidenceRecords.SingleAsync(x => x.Id == ev.SourceEvidenceId);
        var bindings = await db.SourcingDemandBindings.Where(x => x.SourcingEventId == ev.Id).ToArrayAsync();
        var line = await db.SourcingLines.SingleAsync(x => x.SourcingEventId == ev.Id);
        var allocation = await db.PurchaseRequestAllocations.SingleAsync(x => x.Id == bindings[0].AllocationId);
        Assert.Equal(0.00000001m, allocation.EstimatedValue);
        var restored = GovernedRfqRestoration.RestoreVerified(SourcingDraftPersistenceProjection.Evidence(evidence), ev,
            new[] { new RfqOwnedDraftLine(SourcingDraftPersistenceProjection.Pin(bindings[0]), line, allocation) }, f.History);
        var originalFingerprint = restored.Fingerprint;
        var beforeFinance = (f.Original.Source.Finance.ReservationRef, f.Original.Source.Finance.Status, f.Original.Source.Finance.CheckedAmount);
        var cancelKey = Guid.NewGuid();
        var cancel = GovernedRfqDraft.Cancel(f.Actor, restored, cancelKey, originalFingerprint, "No longer needed", new[] { source });
        var instruction = Assert.Single(cancel.Instructions);
        allocation.Status = "Released"; allocation.Reason = "No longer needed";
        source.Line.AllocatedQuantity = instruction.ResultingAllocatedQuantity;
        source.Request.UpdatedAt = DateTimeOffset.UtcNow;
        ev.Status = "Cancelled"; ev.CancellationReason = "No longer needed";
        line.Status = "Cancelled"; line.StatusReason = "Event cancelled";
        await db.SaveChangesAsync();
        Assert.Equal(2L, ev.Version);
        db.AddScoped(f.Receipt(ev, "Cancel", cancelKey, 200)); await db.SaveChangesAsync();
        Assert.Equal(2L, ev.Version); // Receipt final save cannot advance an unchanged aggregate again.
        var replay = GovernedRfqDraft.Cancel(f.Actor, cancel.Result, cancelKey, originalFingerprint,
            "No longer needed", new[] { source }, cancel.Receipt);
        Assert.True(replay.IsReplay); Assert.Empty(replay.Instructions);
        await using var read = f.Db();
        var savedLine = await read.PurchaseRequestLines.SingleAsync(x => x.Id == f.Original.Line.Id);
        Assert.Equal(4m, savedLine.AllocatedQuantity); Assert.Equal(0m, savedLine.OrderedQuantity); Assert.Equal(0m, savedLine.AcceptedQuantity);
        var held = await read.PurchaseRequestAllocations.Where(x => x.Status == "Reserved").ToArrayAsync();
        Assert.Equal(2, held.Length); Assert.Contains(held, x => x.TargetType == "PurchaseOrder");
        Assert.Contains(held, x => x.TargetType == "SourcingEvent" && x.TargetId == other.EventId);
        Assert.Equal("Released", (await read.PurchaseRequestAllocations.SingleAsync(x => x.Id == allocation.Id)).Status);
        Assert.Equal(originalFingerprint, (await read.SourcingDraftEvidenceRecords.SingleAsync(x => x.Id == evidence.Id)).OriginalDraftFingerprint);
        Assert.Equal(3, await read.SourcingCommandReceipts.CountAsync());
        Assert.Equal(beforeFinance, (f.Original.Source.Finance.ReservationRef, f.Original.Source.Finance.Status, f.Original.Source.Finance.CheckedAmount));
    }

    [Fact]
    public async Task Added_binding_cannot_adopt_unchanged_orphan_hold_and_skip_counter_effect()
    {
        var f = new Fixture(); await f.SeedAsync();
        await using var db = f.Db(); var source = await f.SourceAsync(db);
        var plan = GovernedRfqDraft.Create(f.Actor, Guid.NewGuid(), Guid.NewGuid(), "New RFQ",
            new[] { new RfqDemandSelection(source, source.Request.Version, 1m) }, f.Original.Now.AddMinutes(2));
        var graph = f.AddGraph(db, plan, Guid.NewGuid(), source, addAllocation: false);
        // Simulate an existing/orphan polymorphic row. It has no Added effect in this command.
        db.Attach(graph.Allocation); source.Line.AllocatedQuantity = 3m; source.Request.UpdatedAt = DateTimeOffset.UtcNow;
        Assert.Equal(EntityState.Unchanged, db.Entry(graph.Allocation).State);
        Assert.Equal("RfqBindingInvalid", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        await using var read = f.Db();
        Assert.Empty(await read.SourcingEvents.ToArrayAsync()); Assert.Empty(await read.SourcingDraftEvidenceRecords.ToArrayAsync());
        Assert.Empty(await read.SourcingDemandBindings.ToArrayAsync()); Assert.Empty(await read.SourcingCommandReceipts.ToArrayAsync());
        Assert.Equal(2m, (await read.PurchaseRequestLines.SingleAsync()).AllocatedQuantity);
        Assert.Single(await read.PurchaseRequestAllocations.ToArrayAsync());
    }

    private sealed class Fixture
    {
        public PurchaseOrderSourceAllocationTests.Fixture Original { get; } = new(10m, 0.0001m,
            executionRoute: "CompetitiveSourcing", minimumQuoteCount: 0);
        private readonly DbContextOptions<ProcurementDbContext> options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public RfqCurrentActor Actor { get; }
        public IReadOnlyList<RfqHistoricalApproval> History { get; }
        public Fixture()
        {
            Actor = new("buyer", Guid.NewGuid(), Original.Request.TenantId, Original.Company, null, new HashSet<Guid>(), true,
                new HashSet<string> { "procurement-sourcing-convert", "procurement-sourcing-cancel" }, true, new HashSet<Guid>());
            var s = Original.Source; History = new[] { new RfqHistoricalApproval(s.Submission, s.Outcome, s.CoreCase, s.FinalDecision, s.Evaluation, s.Policy, s.Finance) };
        }
        public ProcurementDbContext Db(string subject = "buyer") => new(options, new Scope(Actor.TenantId, Actor.CompanyId, subject));
        public async Task SeedAsync()
        {
            await using var db = Db("requester");
            Original.Line.AllocatedQuantity = 2;
            Original.PoLine.Quantity = 2; Original.PoLine.UnitPrice = 0.0001m;
            db.AddScoped(Original.Request); db.AddScoped(Original.Order); db.AddScoped(Original.PoLine);
            db.AddScoped(new PurchaseRequestAllocation { PurchaseRequestId = Original.Request.Id, PurchaseRequestLineId = Original.Line.Id,
                TargetType = "PurchaseOrder", TargetId = Original.Order.Id, Quantity = 2, EstimatedValue = 0.0002m,
                CurrencyCode = "ZMW", Method = Original.Method.Code, Status = "Reserved", IdempotencyKey = "retained-po" });
            await db.SaveChangesAsync();
        }
        public async Task<ApprovedPurchaseSource> SourceAsync(ProcurementDbContext db)
        {
            var request = await db.PurchaseRequests.Include(x => x.Lines).SingleAsync(); var line = request.Lines.Single();
            var allocations = await db.PurchaseRequestAllocations.ToArrayAsync(); var owners = new List<PurchaseDemandAllocationOwner>();
            var order = await db.PurchaseOrders.SingleAsync(); var orderLine = await db.PurchaseOrderLines.SingleAsync();
            owners.Add(PurchaseDemandAllocationOwner.FromOrder(order, orderLine));
            foreach (var ev in await db.SourcingEvents.ToArrayAsync())
                foreach (var row in await db.SourcingLines.Where(x => x.SourcingEventId == ev.Id).ToArrayAsync()) owners.Add(PurchaseDemandAllocationOwner.FromSourcing(ev, row));
            return Original.Source with { Request = request, Line = line, Allocations = allocations, AllocationOwners = owners };
        }
        public async Task<(Guid EventId, Guid EvidenceId)> CreateAsync(decimal quantity)
        {
            await using var db = Db(); var source = await SourceAsync(db);
            var key = Guid.NewGuid(); var plan = GovernedRfqDraft.Create(Actor, Guid.NewGuid(), key, "Quote request",
                new[] { new RfqDemandSelection(source, source.Request.Version, quantity) }, Original.Now.AddMinutes(2));
            var graph = AddGraph(db, plan, key, source, true);
            source.Line.AllocatedQuantity = Assert.Single(plan.Instructions).ResultingAllocatedQuantity;
            source.Line.Route = "PendingSourcing"; source.Request.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            db.AddScoped(Receipt(graph.Event, "Create", key, 201)); await db.SaveChangesAsync();
            Assert.Equal(1L, graph.Event.Version);
            return (graph.Event.Id, graph.Evidence.Id);
        }
        public (SourcingEvent Event, SourcingDraftEvidence Evidence, PurchaseRequestAllocation Allocation) AddGraph(
            ProcurementDbContext db, GovernedRfqDraft.Plan plan, Guid key, ApprovedPurchaseSource source, bool addAllocation)
        {
            var s = Original.Source;
            foreach (var reference in new ProcurementRecord[] { s.Submission, s.Outcome, s.Finance, s.Evaluation, s.Policy, s.Method })
                if (!db.ChangeTracker.Entries<ProcurementRecord>().Any(x => x.Entity.GetType() == reference.GetType() && x.Entity.Id == reference.Id)) db.Attach(reference);
            var captured = GovernedRfqRestoration.Capture(plan.Result, key); var p = Assert.Single(plan.Result.Lines);
            var evidence = new SourcingDraftEvidence { SourcingEventId = plan.Result.Id, BranchRef = plan.Result.BranchId,
                MakerSubjectId = Actor.SubjectId, MakerWorkerId = Actor.WorkerId, CreateCommandKey = key,
                OriginalDraftFingerprint = captured.OriginalFingerprint, PayloadJson = captured.PayloadJson, PayloadSha256 = captured.PayloadSha256 };
            var ev = new SourcingEvent { Id = plan.Result.Id, Number = "RF-" + plan.Result.Id, ExecutionVersion = 1,
                BranchRef = plan.Result.BranchId, MakerWorkerId = Actor.WorkerId, SourceEvidenceId = evidence.Id,
                OriginalDraftFingerprint = plan.Result.Fingerprint, MethodId = p.MethodId, MethodRevision = p.MethodRevision,
                Method = p.MethodCode, OwnerSubjectId = Actor.SubjectId, Title = plan.Result.Title, CurrencyCode = plan.Result.CurrencyCode };
            var line = new SourcingLine { Id = p.EventLineId, SourcingEventId = ev.Id, PurchaseRequestLineId = p.LineId,
                LineNumber = 1, Description = source.Line.Description, UnitCode = p.UnitCode, CategoryCode = p.CategoryCode, Quantity = p.Quantity, NeededBy = source.Line.NeededBy };
            var allocation = new PurchaseRequestAllocation { Id = p.AllocationId, PurchaseRequestId = p.RequestId, PurchaseRequestLineId = p.LineId,
                TargetType = "SourcingEvent", TargetId = ev.Id, Status = "Reserved", Quantity = p.Quantity, EstimatedValue = p.EstimatedValue,
                CurrencyCode = ev.CurrencyCode, Method = p.MethodCode, IdempotencyKey = $"rfq.reserve.{key:N}.{p.LineId:N}" };
            var binding = new SourcingDemandBinding { SourcingEventId = ev.Id, SourceEvidenceId = evidence.Id, SourcingLineId = line.Id,
                PurchaseRequestId = p.RequestId, PurchaseRequestLineId = p.LineId, RequestVersion = p.RequestVersion,
                SubmissionId = p.SubmissionId, SubmittedVersion = p.SubmittedVersion, CoreApprovalCaseId = p.CaseId,
                ApprovalOutcomeId = p.OutcomeId, CoreDecisionId = p.DecisionId, RequestSha = p.RequestSha, PolicyDecisionSha = p.PolicyDecisionSha,
                PolicyRulesSha = p.PolicyRulesSha, FinanceSnapshotId = p.FinanceId, FinanceSha = p.FinanceSha,
                MethodId = p.MethodId, MethodRevision = p.MethodRevision, MethodCode = p.MethodCode, MinimumQuoteCount = p.MinimumQuoteCount,
                MakerWorkerId = p.MakerWorkerId, RequesterWorkerId = p.RequesterWorkerId, AllocationId = p.AllocationId,
                Quantity = p.Quantity, UnitEstimate = p.UnitEstimate, EstimatedValue = p.EstimatedValue, UnitCode = p.UnitCode,
                CategoryCode = p.CategoryCode, DepartmentRef = p.DepartmentId };
            db.AddScoped(ev); db.AddScoped(evidence); db.AddScoped(line); db.AddScoped(binding);
            allocation.TenantId = Actor.TenantId; allocation.LegalEntityId = Actor.CompanyId;
            if (addAllocation) db.AddScoped(allocation);
            return (ev, evidence, allocation);
        }
        public SourcingCommandReceipt Receipt(SourcingEvent ev, string operation, Guid key, int status) => new()
        { SourcingEventId = ev.Id, Operation = operation, CommandKey = key, ActorPrincipalId = Actor.SubjectId,
            ActorWorkerId = Actor.WorkerId, ResponseStatusCode = status, ResultVersion = ev.Version,
            CommandFingerprint = new string('a', 64), ResponseJson = "{}", ResponseSha256 = ProcurementEvidenceEncoding.Digest("{}"), CompletedAt = DateTimeOffset.UtcNow };
    }
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "rfq-full-save"; }
}
