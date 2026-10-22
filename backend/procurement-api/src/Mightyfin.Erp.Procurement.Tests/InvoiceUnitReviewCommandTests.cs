using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Tests;
public sealed class InvoiceUnitReviewCommandTests
{
    [Fact]
    public void Typed_open_receipt_matches_exact_source_and_new_Core_case_without_asserting_invoice_approval()
    {
        var f = new Fixture(false); f.Verify();
        Assert.False(f.Response.IsInvoiceApproval); Assert.False(f.Response.CanPostToFinance);
        Assert.Equal("Pending", f.Response.ReviewState);
    }
    [Fact]
    public void Actual_Core_vote_matches_typed_request_and_pending_response()
    {
        var f = new Fixture(true); f.Verify(); Assert.NotNull(f.Decision); Assert.Equal("Pending", f.Response.CaseState);
    }
    [Theory] [InlineData("assignment")] [InlineData("action")] [InlineData("case-cas")]
    [InlineData("assignment-cas")] [InlineData("claimed-state")] [InlineData("attestation")] [InlineData("current-mapping")]
    public void Rehashed_unrelated_request_or_claimed_response_is_not_a_durable_decision_receipt(string fault)
    {
        var f = new Fixture(true); f.Verify();
        if (fault == "assignment") f.Request = f.Request with { AssignmentId = f.Plan.Case.Stages.SelectMany(x => x.Assignments).First(x => x.Id != f.Decision!.AssignmentId).Id };
        if (fault == "action") f.Request = f.Request with { Action = "Reject", Reason = "Different actual action" };
        if (fault == "case-cas") f.Request = f.Request with { CaseVersion = f.Plan.Case.Version };
        if (fault == "assignment-cas") f.Request = f.Request with { AssignmentVersion = f.Request.AssignmentVersion + 1 };
        if (fault == "claimed-state") f.Response = f.Response with { ReviewState = "Verified" };
        if (fault == "attestation") f.Request = f.Request with { Attestation = null };
        if (fault == "current-mapping") f.Data.Invoice.CurrentMappingVersionId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => f.Verify());
    }
    sealed class Fixture
    {
        public readonly InvoiceUnitReviewTransitionTests.Fixture Data = new();
        public readonly InvoiceUnitReviewTransitions.OpeningPlan Plan;
        public readonly InvoiceUnitReviewSource Source;
        public readonly InvoiceUnitReviewAssignmentEvidence[] Pins;
        public readonly ApprovalDecision? Decision;
        public InvoiceUnitReviewRequest Request;
        public InvoiceUnitReviewResponse Response;
        public Fixture(bool decision)
        {
            Plan = Data.Open(); Source = InvoiceUnitReviewStorage.Source(Plan, Data.Issued);
            Pins = Plan.AssignmentWorkers.Select(x => new InvoiceUnitReviewAssignmentEvidence { TenantId = Source.TenantId, LegalEntityId = Source.LegalEntityId,
                ReviewSourceId = Source.Id, CoreCaseId = Source.CoreCaseId, CoreAssignmentId = x.AssignmentId, PrincipalId = x.PrincipalId, WorkerId = x.WorkerId }).ToArray();
            var key = Guid.NewGuid(); var at = Data.Policy.Now;
            Request = new(1, "Open", key, Data.Invoice.Id, null, null, Data.Invoice.Version, Data.Mapping.MappingRevision, null, null, null, null, null);
            if (decision)
            {
                at = at.AddMinutes(1); var assignment = Plan.Case.Stages.First().Assignments.First();
                var person = Data.Policy.People.Single(x => x.PrincipalId == assignment.AssigneeSubjectId);
                Request = new(1, "Decision", key, Data.Invoice.Id, Source.Id, assignment.Id, Data.Invoice.Version, null,
                    Plan.Case.Version, assignment.Version, "Approve", null, InvoiceUnitReviewCommands.OriginalUnitAttestation);
                var decisions = new List<ApprovalDecision>();
                InvoiceUnitReviewTransitions.ApplyNewDecision(Plan, Data.Invoice, [Data.Line], Data.Mapping, Data.Issued,
                    Data.Matching, Data.Policy.Policy, person, Data.Hashes, assignment.Id,
                    new(key, Data.Invoice.Version, Plan.Case.Version, assignment.Version, "Approve", null, at), decisions);
                Decision = Assert.Single(decisions);
                // Pure DTO guard test only: emulate documented EF-managed revisions, not persisted DB proof.
                Plan.Case.Version++; assignment.Version++;
            }
            Response = new(1, Data.Invoice.Id, Source.Id, Source.CoreCaseId, key, Request.Operation, Data.Invoice.Version,
                Plan.Case.Version, Plan.Case.State, "Pending", Decision?.Id, null, at, true, false, false);
        }
        public void Verify()
        {
            var pin = Decision is null ? null : Pins.Single(x => x.CoreAssignmentId == Decision.AssignmentId);
            var request = InvoiceUnitReviewCommands.Serialize(Request);
            var row = new InvoiceUnitReviewCommandReceipt { ReviewSourceId = Source.Id, ActorPrincipalId = pin?.PrincipalId ?? Source.MakerPrincipalId,
                ActorWorkerId = pin?.WorkerId ?? Source.MakerWorkerId, Operation = Request.Operation, CommandKey = Request.CommandKey,
                RequestJson = request, Fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(request),
                ResponseJson = InvoiceUnitReviewCommands.Serialize(Response), CompletedAt = Response.CompletedAt };
            InvoiceUnitReviewCommands.Verify(row, Source, Data.Invoice, Plan.Case, Pins, Decision, null);
        }
    }
}
