using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PurchaseRequestApprovalPinningTests
{
    private sealed class PrincipalEventRelease(VerifiedFinanceRelease value) : IPurchaseRequestFinanceReleaseAuthority
    {
        public Task<VerifiedFinanceRelease?> ResolveAsync(PurchaseRequestFinanceReleaseRequest request,
            string reference, CancellationToken ct) => Task.FromResult<VerifiedFinanceRelease?>(value);
    }

    // Pure domain composition with a synthetic release authority; no installed Finance claim.
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Return")]
    public async Task PrincipalApproval_terminal_and_release_events_keep_successor_evidence_and_canonical_replay_keys(string action)
    {
        var f = PrincipalFixture(); var pin = PrincipalPin(f); f.Link(pin);
        var actor = Guid.NewGuid().ToString("D");
        var workflow = new ApprovalCase
        {
            Id = pin.CoreApprovalCaseId, TenantId = Fixture.Tenant, LegalEntityId = f.Company,
            SourceModule = "procurement", SourceType = "purchase_request", SourceId = f.Request.Id,
            SourceVersion = pin.SubmittedBusinessVersion, PolicyVersionId = pin.PolicyVersionId,
            PolicyVersionNumber = pin.PolicyVersionNumber, MakerSubjectId = pin.MakerSubjectId, OpenedAt = pin.PinnedAt,
        };
        var stage = new ApprovalStage { TenantId = Fixture.Tenant, LegalEntityId = f.Company,
            CaseId = workflow.Id, Sequence = 1, Code = "NEED", Mode = "Any", RequiredApprovals = 1, State = "Active" };
        var assignment = new ApprovalAssignment { TenantId = Fixture.Tenant, LegalEntityId = f.Company,
            CaseId = workflow.Id, StageId = stage.Id, AssigneeSubjectId = actor, PermissionKey = "procurement-approval-decide" };
        stage.Assignments.Add(assignment); workflow.Stages.Add(stage);
        ApprovalEngine.ValidateNewCase(workflow);
        var decisions = new List<ApprovalDecision>(); var now = pin.PinnedAt.AddMinutes(1);
        var result = ApprovalEngine.Decide(workflow, assignment, new(actor, Fixture.Tenant, f.Company,
            new HashSet<string> { "procurement-approval-decide" }), action, Guid.NewGuid().ToString("D"),
            action == "Approve" ? null : "Synthetic terminal evidence", decisions, now);
        Assert.True(result.Recorded);
        var decision = Assert.Single(decisions);
        var effect = PurchaseRequestApprovalOutcomes.Apply(f.Request, pin, workflow, decision,
            f.Evaluation, f.Policy, f.Finance, Fixture.Tenant, f.Company, now);
        foreach (var message in effect.Events) AssertPrincipalEvent(message, pin.Id);
        var replay = PurchaseRequestApprovalOutcomes.Apply(f.Request, pin, workflow, decision,
            f.Evaluation, f.Policy, f.Finance, Fixture.Tenant, f.Company, now, effect.Outcome, effect.ReleaseRequest);
        Assert.True(replay.IsReplay); Assert.Empty(replay.Events);
        if (effect.ReleaseRequest is not { } release) return;
        var authority = new PrincipalEventRelease(new(Fixture.Tenant, f.Company, release.ReservationRef,
            release.ReservedAmount, release.CurrencyCode, "Released", "SYNTHETIC-RELEASE", "test-only-authority",
            Guid.NewGuid().ToString("D"), now.AddMinutes(1)));
        var confirmed = await PurchaseRequestApprovalOutcomes.ResolveReleaseAsync(f.Request, pin, workflow, decision,
            effect.Outcome, release, f.Evaluation, f.Policy, f.Finance, Fixture.Tenant, f.Company,
            "SYNTHETIC-RELEASE", authority, now.AddMinutes(2));
        AssertPrincipalEvent(Assert.Single(confirmed.Events), pin.Id);
    }

    private static void AssertPrincipalEvent(OutboxEvent message, Guid submission)
    {
        Assert.EndsWith(".v2", message.EventType);
        Assert.Equal(2, message.EventVersion);
        Assert.EndsWith(".v1", message.IdempotencyKey);
        using var payload = JsonDocument.Parse(message.PayloadJson);
        Assert.Equal(1, payload.RootElement.GetProperty("principalApprovalSchemaVersion").GetInt32());
        Assert.Equal(3, payload.RootElement.GetProperty("submissionSchemaVersion").GetInt32());
        Assert.Equal(submission, payload.RootElement.GetProperty("submissionId").GetGuid());
        Assert.Equal(JsonValueKind.Object, payload.RootElement.GetProperty("details").ValueKind);
    }
}

public sealed partial class PurchaseRequestApprovalOutcomeTests
{
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Return")]
    public async Task PrincipalApproval_successor_does_not_rewrite_legacy_event_types_payload_bytes_or_replay_keys(string action)
    {
        var f = new Fixture(action); var effect = f.Apply();
        Assert.All(effect.Events, message => { Assert.EndsWith(".v1", message.EventType); Assert.Equal(1, message.EventVersion); });
        Assert.Equal(JsonSerializer.Serialize(new { f.Request.Id, f.Pin.SubmittedBusinessVersion,
            caseId = f.Case.Id, outcomeId = effect.Outcome.Id, f.Request.Status }), effect.Events[0].PayloadJson);
        if (effect.ReleaseRequest is not { } release) return;
        Assert.Equal(JsonSerializer.Serialize(new { releaseRequestId = release.Id, f.Request.Id, f.Pin.SubmittedBusinessVersion }),
            effect.Events[1].PayloadJson);
        var confirmed = await f.Resolve(effect, new ReleaseAuthority(f.Released()));
        var message = Assert.Single(confirmed.Events);
        Assert.Equal("procurement.finance_release.confirmed.v1", message.EventType);
        Assert.Equal(1, message.EventVersion);
        Assert.Equal(JsonSerializer.Serialize(new { f.Request.Id, releaseRequestId = release.Id,
            confirmationId = confirmed.Confirmation.Id, f.Request.Status }), message.PayloadJson);
    }
}
