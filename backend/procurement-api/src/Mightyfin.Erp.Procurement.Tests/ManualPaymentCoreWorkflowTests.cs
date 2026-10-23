using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ManualPaymentCoreWorkflowTests
{
    [Fact]
    public void A_maker_cancellation_is_an_exact_Core_cancellation_not_self_approval()
    {
        var f = new Fixture(); var submitted = f.Submit(); var open = ManualPaymentCoreWorkflow.Open(submitted, f.Policy, f.Source, f.Now);
        var coreDecisions = new List<ApprovalDecision>();
        var result = ApprovalEngine.Cancel(open.Case, open.Case.Stages.Single().Assignments.Single(),
            new(f.Creator, "payment-governance", f.Company, new HashSet<string> { "procurement-payment-cancel" }),
            "procurement-payment-cancel", "synthetic-cancel", "Withdrawn", coreDecisions, f.Now);
        var manual = ManualPaymentWorkflow.Cancel(submitted.Request, 2, f.Actor(f.Creator), "Withdrawn", f.Now);
        Assert.True(result.Recorded);
        var link = ManualPaymentCoreWorkflow.LinkDecision(manual, open.Link, open.Case, Assert.Single(coreDecisions));
        Assert.Equal("Cancel", link.ManualAction); Assert.Equal(f.Creator, link.ActorPrincipalId);
        Assert.Equal("Cancelled", manual.Request.Status);
    }

    [Fact]
    public void Splitting_a_bill_into_small_requests_cannot_evade_the_whole_bill_review_ceiling()
    {
        var f = new Fixture(); f.Policy.RulesJson = f.Policy.RulesJson.Replace("100.0000", "50.0000", StringComparison.Ordinal);
        Assert.Equal("ManualPaymentPolicyAmountNotCovered", Assert.Throws<InvalidOperationException>(() =>
            ManualPaymentCoreWorkflow.Open(f.Submit(), f.Policy, f.Source, f.Now)).Message);
    }

    [Fact]
    public void An_old_returned_Core_case_does_not_authorize_a_later_resubmission()
    {
        var f = new Fixture(); var first = f.Submit(); var old = ManualPaymentCoreWorkflow.Open(first, f.Policy, f.Source, f.Now);
        var oldDecision = Decide(old, f, f.Approver, "Return", "Review again");
        var returned = ManualPaymentWorkflow.Return(first.Request, 2, f.Actor(f.Approver), "Review again", f.Now);
        var resubmitted = ManualPaymentWorkflow.Submit(returned.Request, 3, f.Actor(f.Creator), f.Source, f.Now);
        var current = ManualPaymentCoreWorkflow.Open(resubmitted, f.Policy, f.Source, f.Now);
        Assert.NotEqual(old.Case.Id, current.Case.Id); Assert.Equal(4, current.Case.SourceVersion);
        var approved = ManualPaymentWorkflow.Approve(resubmitted.Request, 4, f.Actor(f.Approver), f.Source, f.Now);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.LinkDecision(approved, old.Link, old.Case, oldDecision));
    }
    [Fact]
    public void Shared_Core_cases_bind_independent_approval_and_confirmation_without_employee_records()
    {
        var f = new Fixture(); var submitted = f.Submit();
        var opened = ManualPaymentCoreWorkflow.Open(submitted, f.Policy, f.Source, f.Now);
        Assert.Equal("manual_payment_request", opened.Case.SourceType);
        Assert.Equal(submitted.Request.Id, opened.Case.SourceId);
        Assert.Equal(2, opened.Case.SourceVersion); Assert.Equal(f.Policy.Id, opened.Case.PolicyVersionId);
        var approved = ManualPaymentWorkflow.Approve(submitted.Request, 2, f.Actor(f.Approver), f.Source, f.Now);
        var approvalDecision = Decide(opened, f, f.Approver, "Approve");
        var approvalLink = ManualPaymentCoreWorkflow.LinkDecision(approved, opened.Link, opened.Case, approvalDecision);
        Assert.Equal(approvalDecision.Id, approvalLink.CoreApprovalDecisionId);
        Assert.Equal(approved.Decision.Id, approvalLink.ManualPaymentDecisionId);
        // Supersession does not erase the already approved original policy for confirmation.
        f.Policy.State = "Superseded";
        var recorded = ManualPaymentWorkflow.Record(approved.Request, 3, f.Actor(f.Recorder), f.Source,
            new(f.Today, "External transfer", "SYN-CORE-PAY", 25.125m, null), f.Today, f.Now);
        var confirmation = ManualPaymentCoreWorkflow.Open(recorded, f.Policy, f.Source, f.Now, opened.Link, opened.Case);
        Assert.Equal("manual_payment_confirmation", confirmation.Case.SourceType);
        Assert.Equal(f.Recorder, confirmation.Case.MakerSubjectId);
        Assert.Equal(f.Confirmer, confirmation.Case.Stages.Single().Assignments.Single().AssigneeSubjectId);
        var observation = ManualPaymentCoreWorkflow.ObserveDate(recorded, f.Today, "UTC", f.Now);
        Assert.Equal(recorded.Decision.Id, observation.ManualPaymentDecisionId); Assert.Equal(4, observation.RequestVersion);
        var confirmed = ManualPaymentWorkflow.Confirm(recorded.Request, 4, f.Actor(f.Confirmer), f.Today, f.Now);
        var confirmationDecision = Decide(confirmation, f, f.Confirmer, "Approve");
        Assert.Equal("Confirm", ManualPaymentCoreWorkflow.LinkDecision(confirmed, confirmation.Link,
            confirmation.Case, confirmationDecision).ManualAction);
        Assert.Equal("ConfirmedManually", confirmed.Request.Status);
    }

    [Theory]
    [InlineData("tenant")][InlineData("source")][InlineData("version")][InlineData("policy")]
    [InlineData("assignee")][InlineData("permission")][InlineData("delegation")][InlineData("extra-stage")]
    public void Another_Core_source_task_or_unpinned_configuration_cannot_be_claimed(string field)
    {
        var f = new Fixture(); var submitted = f.Submit(); var open = ManualPaymentCoreWorkflow.Open(submitted, f.Policy, f.Source, f.Now);
        var assignment = open.Case.Stages.Single().Assignments.Single();
        switch (field)
        {
            case "tenant": open.Case.TenantId = "foreign"; break;
            case "source": open.Case.SourceId = Guid.NewGuid(); break;
            case "version": open.Case.SourceVersion++; break;
            case "policy": open.Case.PolicyVersionId = Guid.NewGuid(); break;
            case "assignee": assignment.AssigneeSubjectId = f.Creator; break;
            case "permission": assignment.PermissionKey = "procurement-request-read"; break;
            case "delegation": assignment.DelegatedFromSubjectId = f.Creator; break;
            case "extra-stage": open.Case.Stages.Add(new ApprovalStage()); break;
        }
        Assert.Equal("ManualPaymentCoreCaseBindingInvalid", Assert.Throws<InvalidOperationException>(() =>
            ManualPaymentCoreWorkflow.VerifyCurrentCase(submitted.Request, open.Link, open.Case)).Message);
    }

    [Theory]
    [InlineData("Reject")][InlineData("Return")]
    public void Returned_or_rejected_source_transition_is_linked_to_its_exact_shared_decision(string action)
    {
        var f = new Fixture(); var submitted = f.Submit(); var open = ManualPaymentCoreWorkflow.Open(submitted, f.Policy, f.Source, f.Now);
        var core = Decide(open, f, f.Approver, action, "Needs correction");
        var manual = action == "Reject"
            ? ManualPaymentWorkflow.Reject(submitted.Request, 2, f.Actor(f.Approver), "Needs correction", f.Now)
            : ManualPaymentWorkflow.Return(submitted.Request, 2, f.Actor(f.Approver), "Needs correction", f.Now);
        Assert.Equal(action, ManualPaymentCoreWorkflow.LinkDecision(manual, open.Link, open.Case, core).ManualAction);
        core.Reason = "Different evidence";
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.LinkDecision(manual, open.Link, open.Case, core));
    }

    [Fact]
    public void A_pure_approval_without_a_completed_Core_case_cannot_open_confirmation()
    {
        var f = new Fixture(); var submitted = f.Submit(); var open = ManualPaymentCoreWorkflow.Open(submitted, f.Policy, f.Source, f.Now);
        var approved = ManualPaymentWorkflow.Approve(submitted.Request, 2, f.Actor(f.Approver), f.Source, f.Now);
        var recorded = ManualPaymentWorkflow.Record(approved.Request, 3, f.Actor(f.Recorder), f.Source,
            new(f.Today, "External transfer", "SYN-UNBOUND", 25.125m, null), f.Today, f.Now);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.Open(recorded, f.Policy, f.Source, f.Now, open.Link, open.Case));
    }

    [Fact]
    public void Company_date_evidence_cannot_be_attached_to_submission_or_another_clock_instant()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.ObserveDate(f.Submit(), f.Today, "UTC", f.Now));
        var approved = ManualPaymentWorkflow.Approve(f.Submit().Request, 2, f.Actor(f.Approver), f.Source, f.Now);
        var recorded = ManualPaymentWorkflow.Record(approved.Request, 3, f.Actor(f.Recorder), f.Source,
            new(f.Today, "External transfer", "SYN-CLOCK", 25.125m, null), f.Today, f.Now);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.ObserveDate(recorded, f.Today, "UTC", f.Now.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentCoreWorkflow.ObserveDate(recorded, f.Today.AddDays(-1), "UTC", f.Now));
    }

    private static ApprovalDecision Decide(ManualPaymentCoreOpening open, Fixture f, string actor, string action, string? reason = null)
    {
        var decisions = new List<ApprovalDecision>();
        var result = ApprovalEngine.Decide(open.Case, open.Case.Stages.Single().Assignments.Single(),
            new(actor, "payment-governance", f.Company, new HashSet<string> { "procurement-payment-approve", "procurement-payment-confirm" }),
            action, Guid.NewGuid().ToString("D"), reason, decisions, f.Now);
        Assert.True(result.Recorded); return Assert.Single(decisions);
    }
    private sealed class Fixture
    {
        internal readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), Invoice = Guid.NewGuid(), Supplier = Guid.NewGuid();
        internal readonly string Creator = Guid.NewGuid().ToString("D"), Approver = Guid.NewGuid().ToString("D"),
            Recorder = Guid.NewGuid().ToString("D"), Confirmer = Guid.NewGuid().ToString("D");
        internal readonly DateTimeOffset Now = new(2026,10,23,12,0,0,TimeSpan.Zero);
        internal DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);
        internal ManualPaymentInvoiceSource Source => new(Invoice, "payment-governance", Company, Branch, Supplier, "ZMW", Today, 100m, 100m, true) { InvoiceVersion = 1 };
        internal readonly PolicyVersion Policy;
        internal Fixture() => Policy = new() { TenantId = "payment-governance", LegalEntityId = Company,
            PolicyKey = ManualPaymentReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1, State = "Published",
            EffectiveFrom = Now.AddDays(-1), PublishedAt = Now.AddHours(-1), CreatedBy = Guid.NewGuid().ToString("D"),
            PublishedBy = Guid.NewGuid().ToString("D"), PublicationReason = "Synthetic policy review",
            RulesJson = """{"schemaVersion":1,"identitySchemaVersion":2,"policyKey":"MANUAL_PAYMENT_REVIEW","transactionType":"ManualPaymentReview","approvalType":"SingleApprover","independentCreatorApprover":true,"independentRecorderConfirmer":true,"maximumByCurrency":{"ZMW":"100.0000"}}""" };
        internal ManualPaymentActor Actor(string id) => new(id, "payment-governance", Company, Branch, new HashSet<Guid> { Branch },
            new[] { "create", "edit", "submit", "approve", "record", "confirm", "cancel" }.Select(x => "procurement-payment-" + x).ToHashSet(), true);
        private ManualPaymentAssignee Assignee(string id, string action) => new(id, "payment-governance", Company,
            new HashSet<Guid> { Branch }, new HashSet<string> { "procurement-payment-" + action }, true);
        internal ManualPaymentChange Submit()
        {
            var made = ManualPaymentWorkflow.Create(new(Branch, Invoice, Supplier, "ZMW", 25.125m, Approver, Recorder, Confirmer, "Synthetic review"),
                Actor(Creator), Assignee(Approver, "approve"), Assignee(Recorder, "record"), Assignee(Confirmer, "confirm"), Source, Now);
            return ManualPaymentWorkflow.Submit(made.Request, 1, Actor(Creator), Source, Now);
        }
    }
}
