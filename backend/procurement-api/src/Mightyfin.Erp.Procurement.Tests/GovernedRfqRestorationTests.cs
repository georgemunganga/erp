using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class GovernedRfqRestorationTests
{
    [Fact]
    public void Original_exact_evidence_roundtrips_and_cancel_does_not_reapprove_current_business()
    {
        var f = new Fixture(0.0001m, 0.0001m);
        var restored = f.Restore();
        Assert.Equal(f.Draft.Fingerprint, restored.Fingerprint);
        Assert.Equal(0.00000001m, Assert.Single(restored.Lines).EstimatedValue);
        using var payload = JsonDocument.Parse(f.Evidence.PayloadJson);
        var exact = payload.RootElement.GetProperty("Lines")[0].GetProperty("EstimatedValue");
        Assert.Equal(JsonValueKind.String, exact.ValueKind);
        Assert.Equal("1E-08", exact.GetString()); // Existing canonical G29 encoding; never change retained financial bytes for presentation.
        Assert.Equal(0.00000001m, ExactDecimalJsonCodec.Parse(exact.GetString(), 38, 8));
        f.Source.Method.Status = "Inactive";
        f.Source.Request.Status = "Cancelled"; // Original retained immutable authority remains attributable.
        f.Source.Line.EstimatedUnitPrice = 999;
        var again = f.Restore();
        var release = GovernedRfqDraft.Cancel(f.Actor, again, Guid.NewGuid(), again.Fingerprint,
            "No longer required", new[] { f.Source.Source });
        Assert.Equal(0.00000001m, Assert.Single(release.Instructions).EstimatedValue);
        Assert.Equal(0.0001m, f.Source.Line.AllocatedQuantity);
        Assert.Equal("Reserved", f.Allocation.Status);
    }

    [Fact]
    public void Equivalent_decimal_scale_has_stable_schema_one_fingerprint()
    {
        var f = new Fixture(2.0000m, 10.0000m);
        Assert.Equal(f.Draft.Fingerprint, f.Restore().Fingerprint);
        var json = JsonNode.Parse(f.Evidence.PayloadJson)!.AsObject();
        json["Lines"]![0]!["Quantity"] = "2.0000";
        var evidence = f.Rehashed(json.ToJsonString());
        Assert.Equal(f.Draft.Fingerprint, f.Restore(evidence).Fingerprint);
    }

    [Theory]
    [InlineData("schema")][InlineData("missing")][InlineData("unknown")][InlineData("duplicate")]
    [InlineData("null-lines")][InlineData("overprecise")][InlineData("overflow")][InlineData("binding")]
    [InlineData("original-sha")][InlineData("original-scope")][InlineData("original-price")]
    [InlineData("current-owner")][InlineData("current-line")][InlineData("target-type")]
    [InlineData("allocation-value")][InlineData("allocation-key")][InlineData("decision")]
    [InlineData("finance")][InlineData("policy")][InlineData("terminal")][InlineData("unknown-worker")]
    public void Malformed_tampered_or_crossbound_owned_evidence_is_denied_without_effects(string scenario)
    {
        var f = new Fixture(); var evidence = f.Evidence;
        switch (scenario)
        {
            case "schema": evidence = evidence with { SchemaVersion = 2 }; break;
            case "missing": evidence = f.Rehashed(evidence.PayloadJson.Replace("\"SchemaVersion\":1,", "")); break;
            case "unknown": evidence = f.Rehashed(evidence.PayloadJson.Insert(1, "\"Unexpected\":true,")); break;
            case "duplicate": evidence = f.Rehashed(evidence.PayloadJson.Insert(1, "\"SchemaVersion\":1,")); break;
            case "null-lines":
                var missing = JsonNode.Parse(evidence.PayloadJson)!; missing["Lines"] = null;
                evidence = f.Rehashed(missing.ToJsonString()); break;
            case "overprecise":
            case "overflow":
                var amount = JsonNode.Parse(evidence.PayloadJson)!;
                amount["Lines"]![0]!["EstimatedValue"] = scenario == "overflow" ? "999999999999999999999999999999.12345678" : "20.000000001";
                evidence = f.Rehashed(amount.ToJsonString()); break;
            case "binding": f.Bindings[0] = f.Bindings[0] with { Pin = f.Bindings[0].Pin with { MethodId = Guid.NewGuid() } }; break;
            case "original-sha": evidence = evidence with { PayloadSha256 = new string('0', 64) }; break;
            case "original-scope": f.Source.Source.Policy.LegalEntityId = Guid.NewGuid(); break;
            case "original-price": f.Source.Source.Evaluation.DecisionJson = "{}"; break;
            case "current-owner": f.Event.OwnerSubjectId = "another"; break;
            case "current-line": f.Line.Id = Guid.NewGuid(); break;
            case "target-type": f.Allocation.TargetType = "PurchaseOrder"; break;
            case "allocation-value": f.Allocation.EstimatedValue--; break;
            case "allocation-key": f.Allocation.IdempotencyKey = "wrong"; break;
            case "decision": f.Source.Source.FinalDecision.Action = "Return"; break;
            case "finance": f.Source.Source.Finance.ReservationRef = "changed"; break;
            case "policy": f.Source.Source.Policy.RulesJson = "{\"changed\":true}"; break;
            case "terminal": f.Event.Status = "Cancelled"; break;
            case "unknown-worker": evidence = evidence with { MakerWorkerId = Guid.Empty }; break;
        }
        var before = (f.Allocation.Status, f.Source.Line.AllocatedQuantity, f.Source.Line.OrderedQuantity, f.Event.Version);
        Assert.Equal("RfqRetainedDraftInvalid", Assert.Throws<InvalidOperationException>(() => f.Restore(evidence)).Message);
        Assert.Equal(before, (f.Allocation.Status, f.Source.Line.AllocatedQuantity, f.Source.Line.OrderedQuantity, f.Event.Version));
    }

    [Fact]
    public void Missing_extra_or_replaced_owned_relations_are_not_ignored()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => GovernedRfqRestoration.RestoreVerified(f.Evidence, f.Event, [], f.History));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqRestoration.RestoreVerified(f.Evidence, f.Event,
            new[] { f.Bindings[0], f.Bindings[0] }, f.History));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqRestoration.RestoreVerified(f.Evidence, f.Event, f.Bindings, []));
        Assert.Throws<InvalidOperationException>(() => GovernedRfqRestoration.RestoreVerified(f.Evidence, f.Event,
            f.Bindings, new[] { f.History[0], f.History[0] }));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Restore_and_cancel_preserve_exact_versioned_Finance_without_current_identity_lookup(bool bound)
    {
        var f = new Fixture(boundFinance: bound); var source = f.Source.Source;
        var bytes = source.Submission.FinanceSnapshotJson; var hash = source.Submission.FinanceSnapshotSha256;
        Assert.Equal(bound ? 1 : 0, source.Submission.FinanceIdentitySchemaVersion);
        Assert.Equal(FinanceSnapshotCodec.Encode(source.Finance, bound ? 1 : 0), bytes);
        var restored = f.Restore();
        _ = GovernedRfqDraft.Cancel(f.Actor, restored, Guid.NewGuid(), restored.Fingerprint,
            "Retained source only; old Finance account may be inactive", new[] { source });
        Assert.Equal(bytes, source.Submission.FinanceSnapshotJson); Assert.Equal(hash, source.Submission.FinanceSnapshotSha256);
    }

    [Theory]
    [InlineData("downgrade")][InlineData("worker")][InlineData("unknown")]
    public void Bound_history_cannot_downgrade_or_rebind_checker(string change)
    {
        var f = new Fixture(boundFinance: true); var source = f.Source.Source;
        var allocation = (f.Allocation.Status, f.Source.Line.AllocatedQuantity, f.Event.Version);
        if (change is "downgrade" or "unknown")
        {
            // Deliberately corrupted retained input, without weakening the immutable domain property.
            var json = JsonNode.Parse(JsonSerializer.Serialize(source.Submission))!;
            json["FinanceIdentitySchemaVersion"] = change == "downgrade" ? 0 : 99;
            var tampered = JsonSerializer.Deserialize<PurchaseRequestApprovalSubmission>(json.ToJsonString())!;
            f.History[0] = f.History[0] with { Submission = tampered };
        }
        if (change == "worker") source.Finance.CheckedByWorkerId = Guid.NewGuid();
        Assert.Equal("RfqRetainedDraftInvalid", Assert.Throws<InvalidOperationException>(() => f.Restore()).Message);
        Assert.Equal(allocation, (f.Allocation.Status, f.Source.Line.AllocatedQuantity, f.Event.Version));
    }

    private sealed class Fixture
    {
        public PurchaseOrderSourceAllocationTests.Fixture Source { get; }
        public RfqCurrentActor Actor { get; }
        public GovernedRfqDraft.Draft Draft { get; }
        public RfqOwnedDraftEvidence Evidence { get; }
        public SourcingEvent Event { get; }
        public SourcingLine Line { get; }
        public PurchaseRequestAllocation Allocation { get; }
        public List<RfqOwnedDraftLine> Bindings { get; } = [];
        public List<RfqHistoricalApproval> History { get; } = [];
        public Fixture(decimal quantity = 2, decimal rate = 10, bool boundFinance = false)
        {
            Source = new(quantity, rate, executionRoute: "CompetitiveSourcing", minimumQuoteCount: 0, boundFinance: boundFinance);
            Actor = new("buyer", Guid.NewGuid(), Source.Request.TenantId, Source.Company, null,
                new HashSet<Guid>(), true, new HashSet<string> { "procurement-sourcing-convert", "procurement-sourcing-cancel" },
                true, new HashSet<Guid>());
            var key = Guid.NewGuid();
            var plan = GovernedRfqDraft.Create(Actor, Guid.NewGuid(), key, "Equipment quotes",
                new[] { new RfqDemandSelection(Source.Source, Source.Request.Version, quantity) }, Source.Now.AddMinutes(2));
            Draft = plan.Result; Evidence = GovernedRfqRestoration.Capture(Draft, key);
            var pin = Assert.Single(Draft.Lines); var effect = Assert.Single(plan.Instructions);
            Event = new() { Id = Draft.Id, TenantId = Draft.TenantId, LegalEntityId = Draft.CompanyId,
                OwnerSubjectId = Actor.SubjectId, CurrencyCode = Draft.CurrencyCode, Title = Draft.Title, Method = pin.MethodCode };
            Line = new() { Id = pin.EventLineId, TenantId = Draft.TenantId, LegalEntityId = Draft.CompanyId,
                SourcingEventId = Draft.Id, PurchaseRequestLineId = pin.LineId, Quantity = pin.Quantity,
                Description = Source.Line.Description, UnitCode = pin.UnitCode, CategoryCode = pin.CategoryCode,
                NeededBy = Source.Line.NeededBy };
            Allocation = new() { Id = pin.AllocationId, TenantId = Draft.TenantId, LegalEntityId = Draft.CompanyId,
                PurchaseRequestId = pin.RequestId, PurchaseRequestLineId = pin.LineId, TargetType = "SourcingEvent",
                TargetId = Draft.Id, Status = "Reserved", Quantity = pin.Quantity, EstimatedValue = pin.EstimatedValue,
                CurrencyCode = Draft.CurrencyCode, Method = pin.MethodCode, IdempotencyKey = effect.EffectKey };
            Source.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(Event, Line));
            Source.Ledger.Add(Allocation); Source.Line.AllocatedQuantity = quantity;
            Bindings.Add(new(pin, Line, Allocation));
            var s = Source.Source;
            History.Add(new(s.Submission, s.Outcome, s.CoreCase, s.FinalDecision, s.Evaluation, s.Policy, s.Finance));
        }
        public GovernedRfqDraft.Draft Restore(RfqOwnedDraftEvidence? evidence = null) =>
            GovernedRfqRestoration.RestoreVerified(evidence ?? Evidence, Event, Bindings, History);
        public RfqOwnedDraftEvidence Rehashed(string json) => Evidence with
            { PayloadJson = json, PayloadSha256 = ProcurementEvidenceEncoding.Digest(json) };
    }
}
