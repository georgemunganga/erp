using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class GovernedRfqDraftTests
{
    [Fact]
    public void Create_returns_exact_owned_reservation_instructions_without_mutating_source()
    {
        var f = Fixture(0.0001m, 0.0001m, 0);
        var actor = Actor(f); var key = Guid.NewGuid(); var id = Guid.NewGuid();
        var before = (f.Line.AllocatedQuantity, f.Request.UpdatedAt, f.Line.Route);
        var plan = Create(f, actor, id, key, 0.0001m);
        var pin = Assert.Single(plan.Result.Lines); var reserve = Assert.Single(plan.Instructions);
        Assert.Equal(0.00000001m, pin.EstimatedValue); Assert.Equal(pin.EstimatedValue, reserve.EstimatedValue);
        Assert.Equal(0, pin.MinimumQuoteCount); Assert.Equal(f.Source.Submission.Id, pin.SubmissionId);
        Assert.Equal(f.Source.Submission.RequestSnapshotSha256, pin.RequestSha);
        Assert.Equal(f.Request.Version, reserve.ExpectedRequestVersion);
        Assert.Equal(pin.EventLineId, reserve.EventLineId); Assert.NotEqual(reserve.AllocationId, reserve.EventLineId);
        Assert.Equal("Reserve", reserve.Kind); Assert.Equal(id, reserve.EventId);
        Assert.Empty(f.Ledger); Assert.Equal(before, (f.Line.AllocatedQuantity, f.Request.UpdatedAt, f.Line.Route));
    }

    [Fact]
    public void Replay_has_no_new_effects_and_changed_payload_actor_or_permission_is_denied()
    {
        var f = Fixture(); var actor = Actor(f); var key = Guid.NewGuid(); var id = Guid.NewGuid();
        var first = Create(f, actor, id, key, 2);
        var replay = Create(f, actor, id, key, 2, first.Receipt);
        Assert.True(replay.IsReplay); Assert.Same(first.Result, replay.Result); Assert.Empty(replay.Instructions);
        Assert.Throws<InvalidOperationException>(() => Create(f, actor, id, key, 3, first.Receipt));
        Assert.Throws<InvalidOperationException>(() => Create(f, actor with { SubjectId = "alias" }, id, key, 2, first.Receipt));
        Assert.Throws<InvalidOperationException>(() => Create(f, actor with { Permissions = new HashSet<string>() }, id, key, 2, first.Receipt));
        Assert.Empty(f.Ledger);
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("worker")][InlineData("branch")]
    [InlineData("department")][InlineData("version")][InlineData("quantity")][InlineData("pin")][InlineData("direct")]
    public void Invalid_source_or_current_identity_produces_no_mutation(string kind)
    {
        var f = Fixture(); var actor = Actor(f); var version = f.Request.Version; decimal quantity = 1;
        switch (kind)
        {
            case "tenant": actor = actor with { TenantId = "other" }; break;
            case "company": actor = actor with { CompanyId = Guid.NewGuid() }; break;
            case "worker": actor = actor with { WorkerId = Guid.Empty }; break;
            case "branch": actor = actor with { SelectedBranchId = Guid.NewGuid() }; break;
            case "department": actor = actor with { AllDepartments = false }; break;
            case "version": version--; break;
            case "quantity": quantity = 0.00001m; break;
            case "pin": f.Source.Evaluation.DecisionJson = "{}"; break;
            case "direct": f.Method.ExecutionRoute = "DirectPurchase"; break;
        }
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.Create(actor, Guid.NewGuid(), Guid.NewGuid(),
            "Equipment quotes", new[] { new RfqDemandSelection(f.Source, version, quantity) }, f.Now.AddMinutes(2)));
        Assert.Empty(f.Ledger); Assert.Equal(0m, f.Line.AllocatedQuantity);
    }

    [Fact]
    public void Target_identity_is_type_and_id_not_an_untyped_UUID()
    {
        var f = Fixture(); var actor = Actor(f);
        f.Ledger.Add(new PurchaseRequestAllocation { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            PurchaseRequestId = f.Request.Id, PurchaseRequestLineId = f.Line.Id, CurrencyCode = "ZMW",
            TargetType = "PurchaseOrder", TargetId = f.Order.Id, Quantity = 1, EstimatedValue = 10, Status = "Reserved" });
        f.Line.AllocatedQuantity = 1;
        var plan = Create(f, actor, f.Order.Id, Guid.NewGuid(), 2);
        Assert.Equal(f.Order.Id, plan.Result.Id);
        Assert.Equal(3m, Assert.Single(plan.Instructions).ResultingAllocatedQuantity);
        Assert.Single(f.Ledger); Assert.Equal(1m, f.Line.AllocatedQuantity);

        // Once a real owned RFQ reservation exists for this tuple, another create is denied.
        ApplyReservationFixture(f, plan.Result);
        f.Line.AllocatedQuantity = 3; // Both retained PO and RFQ holds, not just the RFQ fixture.
        Assert.Equal("RfqSourceUnavailable", Assert.Throws<InvalidOperationException>(() =>
            Create(f, actor, plan.Result.Id, Guid.NewGuid(), 1)).Message);
        Assert.Equal(2, f.Ledger.Count); Assert.Equal(3m, f.Line.AllocatedQuantity);
    }

    [Fact]
    public void Later_invalid_selection_leaves_all_sources_and_original_ledger_unchanged()
    {
        var a = Fixture(); var b = Fixture();
        // Different company is deliberately invalid, irrespective of line ordering.
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.Create(Actor(a), Guid.NewGuid(), Guid.NewGuid(), "Quotes",
            new[] { new RfqDemandSelection(a.Source, a.Request.Version, 2), new RfqDemandSelection(b.Source, b.Request.Version, 2) }, a.Now.AddMinutes(2)));
        Assert.Empty(a.Ledger); Assert.Empty(b.Ledger);
        Assert.Equal(0m, a.Line.AllocatedQuantity); Assert.Equal(0m, b.Line.AllocatedQuantity);
    }

    [Fact]
    public void Cancellation_releases_only_matching_reserved_hold_once_without_current_method_reapproval()
    {
        var f = Fixture(); var actor = Actor(f); var draft = Create(f, actor, Guid.NewGuid(), Guid.NewGuid(), 4).Result;
        ApplyReservationFixture(f, draft);
        f.Method.Status = "Inactive"; // Historic release cannot depend on new commercial eligibility.
        var key = Guid.NewGuid();
        var cancellation = GovernedRfqDraft.Cancel(actor, draft, key, draft.Fingerprint, "  No   longer needed ", new[] { f.Source });
        var release = Assert.Single(cancellation.Instructions);
        Assert.Equal("Release", release.Kind); Assert.Equal(0m, release.ResultingAllocatedQuantity);
        Assert.Equal(40m, release.EstimatedValue); Assert.Equal("Cancelled", cancellation.Result.State);
        Assert.Equal(4m, f.Line.AllocatedQuantity); Assert.Equal("Reserved", Assert.Single(f.Ledger).Status);
        // Fixture applies the returned desired effect exactly as the future transaction adapter must.
        f.Ledger[0].Status = "Released"; f.Line.AllocatedQuantity = 0;
        var replay = GovernedRfqDraft.Cancel(actor, cancellation.Result, key, draft.Fingerprint,
            "No longer needed", new[] { f.Source }, cancellation.Receipt);
        Assert.True(replay.IsReplay); Assert.Empty(replay.Instructions);
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.Cancel(actor, cancellation.Result,
            Guid.NewGuid(), cancellation.Result.Fingerprint, "Again", new[] { f.Source }));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.Cancel(actor, cancellation.Result,
            key, draft.Fingerprint, "Changed reason", new[] { f.Source }, cancellation.Receipt));
    }

    [Theory]
    [InlineData("unknown")][InlineData("quantity")][InlineData("value")][InlineData("owner")][InlineData("replaced-line")][InlineData("stale")]
    public void Cancellation_cannot_free_unproven_or_changed_reservations(string kind)
    {
        var f = Fixture(); var actor = Actor(f); var draft = Create(f, actor, Guid.NewGuid(), Guid.NewGuid(), 4).Result;
        ApplyReservationFixture(f, draft);
        switch (kind)
        {
            case "unknown": f.Ledger[0].Status = "Ordered"; break;
            case "quantity": f.Ledger[0].Quantity = 3; f.Line.AllocatedQuantity = 3; break;
            case "value": f.Ledger[0].EstimatedValue = 39; break;
            case "owner": f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent"); break;
            case "replaced-line":
                f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent");
                var replacementEvent = new SourcingEvent { Id = draft.Id, TenantId = draft.TenantId, LegalEntityId = draft.CompanyId };
                var replacementLine = new SourcingLine { TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
                    SourcingEventId = draft.Id, PurchaseRequestLineId = f.Line.Id };
                f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(replacementEvent, replacementLine));
                break;
        }
        var amount = f.Line.AllocatedQuantity;
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.Cancel(actor, draft, Guid.NewGuid(),
            kind == "stale" ? "stale" : draft.Fingerprint, "Cancel", new[] { f.Source }));
        Assert.Equal(amount, f.Line.AllocatedQuantity);
    }

    [Fact]
    public void Publication_intent_requires_distinct_canonical_worker_and_remains_explicitly_blocked()
    {
        var f = Fixture(); var actor = Actor(f); var draft = Create(f, actor, Guid.NewGuid(), Guid.NewGuid(), 2).Result;
        var reviewer = actor with { SubjectId = "reviewer", WorkerId = Guid.NewGuid() };
        var intent = GovernedRfqDraft.PreparePublicationIntent(actor, draft, reviewer);
        Assert.Equal(20m, intent.FullEventAmount); Assert.Equal(4, intent.Blockers.Count);
        Assert.Contains("FinitePublicationAuthorityNotVerified", intent.Blockers); Assert.Equal("Draft", draft.State);
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.PreparePublicationIntent(actor, draft,
            reviewer with { WorkerId = actor.WorkerId }));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.PreparePublicationIntent(actor, draft,
            reviewer with { WorkerId = f.Source.Submission.RequesterWorkerId!.Value }));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.PreparePublicationIntent(actor, draft,
            reviewer with { WorkerId = f.Source.Submission.MakerWorkerId!.Value }));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqDraft.PreparePublicationIntent(actor, draft,
            reviewer with { CompanyId = Guid.NewGuid() }));
    }

    private static PurchaseOrderSourceAllocationTests.Fixture Fixture(decimal quantity = 10, decimal rate = 10, int quotes = 2)
        => new(quantity, rate, executionRoute: "CompetitiveSourcing", minimumQuoteCount: quotes);
    private static RfqCurrentActor Actor(PurchaseOrderSourceAllocationTests.Fixture f) => new("buyer", Guid.NewGuid(),
        f.Request.TenantId, f.Company, null, new HashSet<Guid>(), true, new HashSet<string>
        { "procurement-sourcing-convert", "procurement-sourcing-cancel", "procurement-sourcing-submit", "procurement-sourcing-publish" }, true, new HashSet<Guid>());
    private static GovernedRfqDraft.Plan Create(PurchaseOrderSourceAllocationTests.Fixture f, RfqCurrentActor actor,
        Guid id, Guid key, decimal quantity, GovernedRfqDraft.Receipt? receipt = null) => GovernedRfqDraft.Create(actor,
            id, key, "Equipment quotes", new[] { new RfqDemandSelection(f.Source, f.Request.Version, quantity) },
            f.Now.AddMinutes(2), receipt);
    private static void ApplyReservationFixture(PurchaseOrderSourceAllocationTests.Fixture f, GovernedRfqDraft.Draft draft)
    {
        var pin = Assert.Single(draft.Lines);
        var sourcing = new SourcingEvent { Id = draft.Id, TenantId = draft.TenantId, LegalEntityId = draft.CompanyId };
        var line = new SourcingLine { Id = pin.EventLineId, TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
            SourcingEventId = draft.Id, PurchaseRequestLineId = pin.LineId };
        f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(sourcing, line));
        f.Ledger.Add(new PurchaseRequestAllocation { Id = pin.AllocationId, TenantId = draft.TenantId,
            LegalEntityId = draft.CompanyId, PurchaseRequestId = pin.RequestId, PurchaseRequestLineId = pin.LineId,
            TargetType = "SourcingEvent", TargetId = draft.Id, Status = "Reserved", CurrencyCode = draft.CurrencyCode,
            Quantity = pin.Quantity, EstimatedValue = pin.EstimatedValue });
        f.Line.AllocatedQuantity = pin.Quantity;
    }
}
