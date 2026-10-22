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
    [Theory]
    [InlineData("missing-maker")][InlineData("other-maker")][InlineData("missing-source-version")]
    [InlineData("downgraded-source-version")][InlineData("pending-terminal-version")][InlineData("invoice-approval")]
    public void Rehashed_receipt_cannot_drop_or_replace_retained_principal_or_schema_claims(string fault)
    {
        var f = new Fixture(false); f.Verify();
        f.Response = fault switch
        {
            "missing-maker" => f.Response with { MakerPrincipalId = null },
            "other-maker" => f.Response with { MakerPrincipalId = Guid.NewGuid().ToString("D") },
            "missing-source-version" => f.Response with { IdentitySchemaVersion = null },
            "downgraded-source-version" => f.Response with { IdentitySchemaVersion = 2 },
            "pending-terminal-version" => f.Response with { EvidenceSchemaVersion = 3 },
            _ => f.Response with { IsInvoiceApproval = true },
        };
        Assert.Throws<InvalidOperationException>(() => f.Verify());
    }
    [Fact]
    public void Source_identity_metadata_is_additive_to_envelope_one_and_pending_is_not_terminal_evidence()
    {
        var f = new Fixture(false); f.Verify();
        Assert.Equal(1, f.Response.SchemaVersion); Assert.Equal(3, f.Response.IdentitySchemaVersion);
        Assert.Equal(f.Source.MakerPrincipalId, f.Response.MakerPrincipalId); Assert.Null(f.Response.EvidenceSchemaVersion);
        var json = InvoiceUnitReviewCommands.Serialize(f.Response);
        Assert.Contains("\"makerPrincipalId\"", json); Assert.Contains("\"identitySchemaVersion\":3", json);
        Assert.DoesNotContain("evidenceSchemaVersion", json);
    }
    [Fact]
    public void Historical_acknowledgement_keeps_original_receipt_when_current_invoice_revision_changes()
    {
        var f = new Fixture(false); f.Verify(); var receipt = f.Receipt();
        f.Data.Invoice.Version++; f.Data.Invoice.CurrentMappingVersionId = Guid.NewGuid();
        var recovered = InvoiceUnitReviewCommands.ReadHistorical(receipt, f.Source);
        Assert.Equal(f.Source.InvoiceVersionAtOpen, recovered.InvoiceVersion);
        Assert.Equal(f.Source.MakerPrincipalId, recovered.MakerPrincipalId);
        Assert.False(recovered.CanPostToFinance); Assert.False(recovered.IsInvoiceApproval);
        // The same receipt cannot authorize a new save against the changed current invoice.
        Assert.Throws<InvalidOperationException>(() => f.Verify());
    }
    [Fact]
    public void Historical_open_receipt_cannot_replace_the_original_known_maker_alias()
    {
        var f = new Fixture(false); f.Verify(); var saved = f.Receipt();
        var forged = new InvoiceUnitReviewCommandReceipt { ReviewSourceId = saved.ReviewSourceId, ActorPrincipalId = saved.ActorPrincipalId,
            ActorWorkerId = Guid.NewGuid(), Operation = saved.Operation, CommandKey = saved.CommandKey, RequestJson = saved.RequestJson,
            ResponseJson = saved.ResponseJson, Fingerprint = saved.Fingerprint, CompletedAt = saved.CompletedAt };
        Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewCommands.ReadHistorical(forged, f.Source));
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
            Response = InvoiceUnitReviewCommands.BindSource(Response, Source);
        }
        public InvoiceUnitReviewCommandReceipt Receipt()
        {
            var pin = Decision is null ? null : Pins.Single(x => x.CoreAssignmentId == Decision.AssignmentId);
            var request = InvoiceUnitReviewCommands.Serialize(Request);
            var row = new InvoiceUnitReviewCommandReceipt { ReviewSourceId = Source.Id, ActorPrincipalId = pin?.PrincipalId ?? Source.MakerPrincipalId,
                ActorWorkerId = pin?.WorkerId ?? Source.MakerWorkerId, Operation = Request.Operation, CommandKey = Request.CommandKey,
                RequestJson = request, Fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(request),
                ResponseJson = InvoiceUnitReviewCommands.Serialize(Response), CompletedAt = Response.CompletedAt };
            return row;
        }
        public void Verify() => InvoiceUnitReviewCommands.Verify(Receipt(), Source, Data.Invoice, Plan.Case, Pins, Decision, null);
    }
}
