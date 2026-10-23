using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PurchaseRequestApprovalOutcomeTests
{
    [Fact]
    public void Approval_makes_only_active_lines_orderable_and_replay_preserves_later_counters()
    {
        var f = new Fixture();
        var result = f.Apply();
        Assert.Equal("Approved", f.Request.Status);
        Assert.Equal(2m, f.Line.ApprovedQuantity);
        Assert.Equal("Approved", f.Line.ApprovalStatus);
        Assert.Null(result.ReleaseRequest);
        Assert.Single(result.Events);
        Assert.Equal("Cancelled", f.Cancelled.ApprovalStatus);
        Assert.Equal(7m, f.Cancelled.OrderedQuantity);
        Assert.Equal(3m, f.Cancelled.ApprovedQuantity);
        f.Line.AllocatedQuantity = 1;
        f.Line.OrderedQuantity = 1;
        f.Request.Status = "PartiallyOrdered";
        var replay = f.Apply(result.Outcome);
        Assert.True(replay.IsReplay);
        Assert.Empty(replay.Events);
        Assert.Equal(result.Outcome.Id, replay.Outcome.Id);
        Assert.Equal(1m, f.Line.OrderedQuantity);
        Assert.Equal("PartiallyOrdered", f.Request.Status);
    }

    [Fact]
    public void Rejection_is_nonorderable_and_retries_reuse_one_pending_release()
    {
        var f = new Fixture("Reject");
        var effect = f.Apply();
        Assert.Equal("Rejected", f.Request.Status);
        Assert.Equal("Rejected", f.Line.ApprovalStatus);
        Assert.Equal(0m, f.Line.ApprovedQuantity);
        Assert.NotNull(effect.ReleaseRequest);
        Assert.Equal("RESERVATION-1", effect.ReleaseRequest.ReservationRef);
        Assert.Equal(250m, effect.ReleaseRequest.ReservedAmount);
        Assert.Equal(2, effect.Events.Count);
        var replay = f.Apply(effect.Outcome, effect.ReleaseRequest);
        Assert.True(replay.IsReplay);
        Assert.Equal(effect.ReleaseRequest.Id, replay.ReleaseRequest!.Id);
        Assert.Empty(replay.Events);
        Assert.Equal("Confirmed", f.Finance.ReservationStatus); // Procurement never writes the Finance truth.
    }

    [Fact]
    public async Task Return_remains_locked_when_release_is_unknown_then_authoritative_evidence_unlocks_once()
    {
        var f = new Fixture("Return");
        var effect = f.Apply();
        Assert.Equal("ReturnPendingFinanceRelease", f.Request.Status);
        Assert.Equal(0m, f.Line.ApprovedQuantity);
        var authority = new ReleaseAuthority(null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Resolve(effect, authority));
        Assert.Equal("ReturnPendingFinanceRelease", f.Request.Status);
        authority.Evidence = f.Released();
        var resolution = await f.Resolve(effect, authority);
        Assert.Equal("ReturnedForChanges", f.Request.Status);
        Assert.Equal("Please correct coding", f.Request.ReturnReason);
        Assert.Single(resolution.Events);
        Assert.Equal("Confirmed", f.Finance.ReservationStatus);
        var replay = await f.Resolve(effect, authority, resolution.Confirmation);
        Assert.True(replay.IsReplay);
        Assert.Empty(replay.Events);
        Assert.Equal(resolution.Confirmation.Id, replay.Confirmation.Id);
    }

    [Fact]
    public async Task Confirming_rejected_reservation_does_not_make_demand_editable_or_orderable()
    {
        var f = new Fixture("Reject");
        var effect = f.Apply();
        await f.Resolve(effect, new ReleaseAuthority(f.Released()));
        Assert.Equal("Rejected", f.Request.Status);
        Assert.Equal(0m, f.Line.ApprovedQuantity);
        Assert.Equal("Rejected", f.Line.ApprovalStatus);
    }

    [Fact]
    public async Task Historic_return_cannot_unlock_a_new_submission()
    {
        var f = new Fixture("Return");
        var effect = f.Apply();
        f.Request.SubmittedVersion++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Resolve(effect, new ReleaseAuthority(f.Released())));
        Assert.Equal("ReturnPendingFinanceRelease", f.Request.Status);
    }

    [Fact]
    public void Incompatible_existing_quantity_effects_are_denied_instead_of_reset()
    {
        var f = new Fixture();
        f.Line.OrderedQuantity = 1;
        Assert.Throws<InvalidOperationException>(() => f.Apply());
        Assert.Equal(1m, f.Line.OrderedQuantity);
        Assert.Equal("Submitted", f.Request.Status);
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("scope")]
    [InlineData("status")]
    [InlineData("time")]
    [InlineData("reference")]
    public async Task Incorrect_or_unconfirmed_Finance_evidence_cannot_unlock_return(string change)
    {
        var f = new Fixture("Return");
        var effect = f.Apply();
        var evidence = f.Released();
        evidence = change switch
        {
            "reservation" => evidence with { ReservationRef = "OTHER" },
            "amount" => evidence with { ReleasedAmount = 249 },
            "currency" => evidence with { CurrencyCode = "USD" },
            "scope" => evidence with { LegalEntityId = Guid.NewGuid() },
            "status" => evidence with { Status = "Unknown" },
            "time" => evidence with { ConfirmedAt = f.Now.AddDays(1) },
            _ => evidence with { EvidenceRef = "OTHER" },
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Resolve(effect, new ReleaseAuthority(evidence)));
        Assert.Equal("ReturnPendingFinanceRelease", f.Request.Status);
    }

    [Theory]
    [InlineData("case")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("policy")]
    [InlineData("scope")]
    [InlineData("decision")]
    public void Terminal_status_alone_cannot_approve_a_different_source_or_evidence(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "case": f.Case.Id = Guid.NewGuid(); break;
            case "source": f.Case.SourceId = Guid.NewGuid(); break;
            case "version": f.Case.SourceVersion++; break;
            case "policy": f.Case.PolicyVersionId = Guid.NewGuid(); break;
            case "scope": f.Decision.LegalEntityId = Guid.NewGuid(); break;
            case "decision": f.Decision.IdempotencyKey = "changed"; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Apply());
        Assert.Equal("Submitted", f.Request.Status);
        Assert.Equal(0m, f.Line.ApprovedQuantity);
    }

    [Fact]
    public void Changed_business_inputs_and_mismatched_existing_outcome_do_not_replay()
    {
        var f = new Fixture();
        var effect = f.Apply();
        f.Request.Purpose = "Changed";
        Assert.Throws<InvalidOperationException>(() => f.Apply(effect.Outcome));
        f.Request.Purpose = "Office supplies";
        Assert.Throws<InvalidOperationException>(() => f.Apply(new PurchaseRequestApprovalOutcome()));
    }

    private sealed class ReleaseAuthority(VerifiedFinanceRelease? evidence) : IPurchaseRequestFinanceReleaseAuthority
    {
        public VerifiedFinanceRelease? Evidence { get; set; } = evidence;
        public Task<VerifiedFinanceRelease?> ResolveAsync(PurchaseRequestFinanceReleaseRequest request,
            string reference, CancellationToken ct) => Task.FromResult(Evidence);
    }

    private sealed class Fixture
    {
        private const string Tenant = "outcome-test";
        private readonly Guid company = Guid.NewGuid();
        public readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public PurchaseRequest Request { get; }
        public PurchaseRequestLine Line { get; }
        public PurchaseRequestLine Cancelled { get; }
        public PolicyVersion Policy { get; }
        public PolicyEvaluation Evaluation { get; }
        public BudgetCheckSnapshot Finance { get; }
        public PurchaseRequestApprovalSubmission Pin { get; }
        public ApprovalCase Case { get; }
        public ApprovalDecision Decision { get; }
        public Fixture(string action = "Approve")
        {
            Request = Scope(new PurchaseRequest
            {
                Number = "PR-OUTCOME", Status = "Submitted", Version = 4, SubmittedVersion = 4,
                SubmittedAt = Now, Purpose = "Office supplies", CurrencyCode = "ZMW", RequesterWorkerId = Guid.NewGuid(),
            });
            Line = Scope(new PurchaseRequestLine
            {
                PurchaseRequestId = Request.Id, LineNumber = 1, Description = "Supplies", UnitCode = "EA",
                RequestedQuantity = 2, EstimatedUnitPrice = 125, EstimatedCurrencyCode = "ZMW", CategoryCode = "OFFICE",
            });
            Cancelled = Scope(new PurchaseRequestLine
            {
                PurchaseRequestId = Request.Id, LineNumber = 2, Description = "Cancelled demand", UnitCode = "EA",
                RequestedQuantity = 10, EstimatedUnitPrice = 10, ApprovalStatus = "Cancelled", ApprovedQuantity = 3, OrderedQuantity = 7,
            });
            Request.Lines = [Line, Cancelled];
            Policy = Scope(new PolicyVersion
            { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1, State = "Published", EffectiveFrom = Now.AddDays(-1), RulesJson = "{}" });
            Evaluation = Scope(new PolicyEvaluation
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, PolicyVersionId = Policy.Id, PolicyVersionNumber = 1,
                AmountBasis = "EstimatedLineBase", Outcome = "Matched", DecisionJson = JsonSerializer.Serialize(new
                {
                    requestId = Request.Id, requestVersion = 4, policyId = Policy.Id,
                    lines = new[] { new { lineId = Line.Id, decision = new { status = "Match",
                        approvalStages = new[] { new { code = "NEED", assignee = "LineManager", mode = "One" } } } } },
                }),
            });
            Finance = Scope(new BudgetCheckSnapshot
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, Status = "Available", CheckedAmount = 250,
                CurrencyCode = "ZMW", CheckedAt = Now.AddSeconds(1), CheckedBySubjectId = "finance",
                FinanceCheckRef = "CHECK-1", EvidenceRef = "FUNDING-1", ReservationStatus = "Confirmed", ReservationRef = "RESERVATION-1",
            });
            Case = new ApprovalCase
            {
                TenantId = Tenant, LegalEntityId = company, SourceModule = "procurement", SourceType = "purchase_request",
                SourceId = Request.Id, SourceVersion = 4, PolicyVersionId = Policy.Id, PolicyVersionNumber = 1, MakerSubjectId = Request.CreatedBy,
            };
            var stage = new ApprovalStage { TenantId = Tenant, LegalEntityId = company, CaseId = Case.Id,
                Sequence = 1, Code = "NEED", Mode = "Any", RequiredApprovals = 1, State = "Active" };
            var assignment = new ApprovalAssignment { TenantId = Tenant, LegalEntityId = company,
                CaseId = Case.Id, StageId = stage.Id, AssigneeSubjectId = "manager", PermissionKey = "approve" };
            stage.Assignments.Add(assignment);
            Case.Stages.Add(stage);
            Pin = PurchaseRequestApprovalPinning.Pin(Request, Evaluation, Policy, Finance, Tenant, company,
                Case.Id, Guid.NewGuid(), PurchaseRequestApprovalPinning.AutomatedFinance, null, Now.AddMinutes(1), Guid.NewGuid(), Request.RequesterWorkerId);
            Request.ApprovalInstanceRef = Case.Id.ToString();
            Request.Version++;
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(Case, assignment, new ApprovalActor("manager", Tenant, company, new HashSet<string> { "approve" }),
                action, "decision-1", "Please correct coding", decisions, Now.AddMinutes(2));
            Decision = Assert.Single(decisions);
        }
        public ProcurementApprovalEffects Apply(PurchaseRequestApprovalOutcome? outcome = null,
            PurchaseRequestFinanceReleaseRequest? release = null) => PurchaseRequestApprovalOutcomes.Apply(Request, Pin,
                Case, Decision, Evaluation, Policy, Finance, Tenant, company, Now.AddMinutes(3), outcome, release);
        public VerifiedFinanceRelease Released() => new(Tenant, company, Finance.ReservationRef!, Finance.CheckedAmount,
            "ZMW", "Released", "RELEASE-EVIDENCE-1", "approved-manual-finance", "finance-reviewer", Now.AddMinutes(4));
        public Task<ProcurementFinanceReleaseEffects> Resolve(ProcurementApprovalEffects effect, ReleaseAuthority authority,
            PurchaseRequestFinanceReleaseConfirmation? confirmation = null) => PurchaseRequestApprovalOutcomes.ResolveReleaseAsync(
                Request, Pin, Case, Decision, effect.Outcome, effect.ReleaseRequest!, Evaluation, Policy, Finance, Tenant, company,
                "RELEASE-EVIDENCE-1", authority, Now.AddMinutes(5), confirmation);
        private T Scope<T>(T record) where T : ProcurementRecord
        { record.TenantId = Tenant; record.LegalEntityId = company; record.CreatedBy = "requester"; return record; }
    }
}
