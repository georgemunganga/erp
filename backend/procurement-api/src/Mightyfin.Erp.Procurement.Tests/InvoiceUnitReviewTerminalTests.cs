using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Flow = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewTransitions;
using Terminal = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewTerminal;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Actual Core domain execution with synthetic owned projections, not runtime/PG authority.</summary>
public sealed class InvoiceUnitReviewTerminalTests
{
    [Fact]
    public void Full_Core_proof_seals_v3_and_another_current_matcher_consumes_without_resigning()
    {
        var f = new Fixture(); f.Complete(); var before = JsonSerializer.Serialize(f.Data.Invoice);
        var outcome = f.Seal();
        Assert.Equal("Verified", outcome.State); Assert.Equal(3, outcome.EvidenceSchemaVersion);
        Assert.Equal(f.Final.Id, outcome.FinalDecisionId); Assert.Equal(f.Plan.SourceSha256, outcome.SourceSha256);
        Assert.Equal("EACH", Assert.Single(f.Units(outcome)).Value);
        Assert.NotEqual(outcome.ReviewerPrincipalId, f.Consumer.PrincipalId);
        var bytes = outcome.EvidenceJson;
        // Neither the original maker nor historical signer needs a current account projection.
        f.Data.Policy.People = [];
        f.Data.Invoice.Version++;
        Assert.Equal("EACH", Assert.Single(f.Units(outcome)).Value);
        Assert.Equal(bytes, outcome.EvidenceJson); Assert.Equal("Unverified", f.Data.Mapping.VerificationState);
        Assert.Null(f.Data.Invoice.ActiveMatchClaimResultId); Assert.Null(f.Data.Invoice.FinancePostingRef);
        Assert.False(InvoiceHistoricalExposure.Unavailable().Complete);
        // Only the deliberate operational Version change above occurred on the original invoice.
        f.Data.Invoice.Version--; Assert.Equal(before, JsonSerializer.Serialize(f.Data.Invoice));
    }

    [Theory]
    [InlineData("pending")][InlineData("omitted-vote")][InlineData("forged-final")][InlineData("worker")]
    [InlineData("weaker-quorum")][InlineData("document")][InlineData("mapping")][InlineData("source-hash")]
    [InlineData("policy")][InlineData("expired")][InlineData("issued")]
    public void No_verified_outcome_without_full_owned_completed_exact_current_proof(string changed)
    {
        var f = new Fixture(); f.Complete();
        switch (changed)
        {
            case "pending": f.Plan.Case.State = "Pending"; break;
            case "omitted-vote": f.Decisions.RemoveAt(0); break;
            case "forged-final": f.FinalId = Guid.NewGuid(); break;
            case "worker": f.Plan = f.Plan with { AssignmentWorkers = f.Plan.AssignmentWorkers.Select(x => x with { WorkerId = Guid.NewGuid() }).ToArray() }; break;
            case "weaker-quorum": f.Plan.Case.Stages.Last().RequiredApprovals = 1; break;
            case "document": f.Data.Hashes.Clear(); break;
            case "mapping": f.Data.Invoice.CurrentMappingVersionId = Guid.NewGuid(); break;
            case "source-hash": f.Plan = f.Plan with { SourceSha256 = new string('a', 64) }; break;
            case "policy": f.Data.Policy.Policy.RulesJson = f.Data.Policy.Policy.RulesJson.Replace("\"All\"", "\"One\""); break;
            case "expired": f.Data.Policy.Policy.EffectiveTo = f.At; break;
            case "issued": f.Data.Issued = f.Data.Issued with { RevisionId = Guid.NewGuid() }; break;
        }
        var invoice = JsonSerializer.Serialize(f.Data.Invoice); var core = JsonSerializer.Serialize(f.Plan.Case);
        Assert.Equal("InvoiceUnitReviewTerminalInvalid", Assert.Throws<InvalidOperationException>(() => f.Seal()).Message);
        Assert.Equal(invoice, JsonSerializer.Serialize(f.Data.Invoice)); Assert.Equal(core, JsonSerializer.Serialize(f.Plan.Case));
        Assert.Null(f.Data.Invoice.ActiveMatchClaimResultId); Assert.Null(f.Data.Invoice.FinancePostingRef);
    }

    [Theory]
    [InlineData("permission")][InlineData("scope")][InlineData("stale-file")]
    public void Current_consumer_is_authorized_separately_from_historical_review(string changed)
    {
        var f = new Fixture(); f.Complete(); var outcome = f.Seal();
        switch (changed)
        {
            case "permission": f.Consumer = f.Consumer with { Permissions = new HashSet<string> { "procurement-access" } }; break;
            case "scope": f.Consumer = f.Consumer with { SelectedBranchId = Guid.NewGuid() }; break;
            case "stale-file": f.Consumer = f.Consumer with { DocumentVersionHashes = new Dictionary<Guid, string>() }; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Units(outcome));
        Assert.Equal("Verified", outcome.State); Assert.Null(f.Data.Invoice.ActiveMatchClaimResultId);
    }

    [Theory]
    [InlineData("Reject", "Rejected")][InlineData("Return", "Returned")]
    public void Actual_Core_nonapproval_outcome_is_retained_but_never_verified_unit_authority(string action, string state)
    {
        var f = new Fixture(); f.Complete(action); var outcome = f.Seal();
        Assert.Equal(state, outcome.State); Assert.Equal(3, outcome.EvidenceSchemaVersion);
        Assert.Null(f.Plan.Case.CompletionKey); // Shared engine only supplies CompletionKey on approval.
        Assert.Throws<InvalidOperationException>(() => f.Units(outcome));
        Assert.Equal("Unverified", f.Data.Mapping.VerificationState); Assert.Null(f.Data.Invoice.FinancePostingRef);
        var retained = outcome.EvidenceJson;
        f.Final.Action = "Approve";
        Assert.Throws<InvalidOperationException>(() => f.Seal()); Assert.Equal(retained, outcome.EvidenceJson);
    }

    private sealed class Fixture
    {
        public InvoiceUnitReviewTransitionTests.Fixture Data = new();
        public Flow.OpeningPlan Plan;
        public List<ApprovalDecision> Decisions = [];
        public Guid FinalId;
        public ApprovalDecision Final => Decisions.Single(x => x.Id == FinalId);
        public DateTimeOffset At => Data.Policy.Now.AddMinutes(2);
        public InvoiceUnitReviewConsumer Consumer;
        public Fixture()
        {
            Plan = Data.Open();
            Consumer = new("test", Data.Policy.Company, Data.Policy.Branch, Guid.NewGuid().ToString("D"), Guid.NewGuid(),
                new HashSet<string> { "procurement-access", "procurement-invoice-match" }, At, Data.Hashes,
                Data.Policy.Branch, new HashSet<Guid> { Data.Policy.Branch }, false);
        }
        public void Complete(string finalAction = "Approve")
        {
            var targets = Plan.Case.Stages.OrderBy(x => x.Sequence).SelectMany(stage =>
                stage.Mode == "Any" ? stage.Assignments.Take(1) : stage.Assignments).ToArray();
            for (var i = 0; i < targets.Length; i++)
            {
                var task = targets[i]; var action = i == targets.Length - 1 ? finalAction : "Approve";
                var person = Data.Policy.People.Single(x => x.PrincipalId == task.AssigneeSubjectId);
                Flow.ApplyNewDecision(Plan, Data.Invoice, [Data.Line], Data.Mapping, Data.Issued, Data.Matching, Data.Policy.Policy,
                    person, Data.Hashes, task.Id, new(Guid.NewGuid(), Data.Invoice.Version, Plan.Case.Version, task.Version,
                        action, action == "Approve" ? null : "Actual Core nonapproval fixture", Data.Policy.Now.AddSeconds(i + 1)), Decisions);
            }
            FinalId = Decisions.Last().Id;
        }
        public Terminal.Outcome Seal() => Terminal.Seal(Plan, Data.Invoice, [Data.Line], Data.Mapping, Data.Issued,
            Data.Matching, Data.Policy.Policy, Data.Hashes, Decisions, FinalId, At);
        public IReadOnlyDictionary<Guid, string> Units(Terminal.Outcome outcome) => Terminal.UnitsForNewComparison(outcome,
            Plan, Data.Invoice, [Data.Line], Data.Mapping, Data.Issued, Data.Matching, Data.Policy.Policy, Decisions, Consumer);
    }
}
