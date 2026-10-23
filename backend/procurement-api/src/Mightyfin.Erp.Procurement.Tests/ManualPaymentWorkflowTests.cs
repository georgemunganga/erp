using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure foundation evidence; no registered API, mapped store, or bank execution.</summary>
public sealed class ManualPaymentWorkflowTests
{
    [Fact]
    public void System_only_Core_account_can_request_and_independently_confirm_an_external_payment()
    {
        var f = new Fixture();
        var made = f.Create();
        Assert.Equal("Draft", made.Request.Status);
        Assert.Equal(1, made.Request.Version);
        Assert.Equal("None", made.Decision.FromStatus);
        Assert.Equal(0, made.Decision.FromVersion);
        var submitted = ManualPaymentWorkflow.Submit(made.Request, 1, f.Creator, f.Source, f.Now);
        var approved = ManualPaymentWorkflow.Approve(submitted.Request, 2, f.Approver, f.Source, f.Now);
        Assert.Equal("Approved", approved.Request.Status);
        Assert.Equal(approved.Request.Amount, approved.Request.ApprovedAmount);
        Assert.Null(approved.Request.PaidOn); // An approval is not a payment.
        var recorded = ManualPaymentWorkflow.Record(approved.Request, 3, f.Recorder, f.Source,
            new(DateOnly.FromDateTime(f.Now.UtcDateTime), "Bank transfer made outside ERP", "EXT-128", 25.1250m, null), f.Now);
        var confirmed = ManualPaymentWorkflow.Confirm(recorded.Request, 4, f.Confirmer, f.Now);
        Assert.Equal("ConfirmedManually", confirmed.Request.Status);
        Assert.Equal(5, confirmed.Request.Version);
        Assert.Equal("PaymentRecorded", confirmed.Decision.FromStatus);
        Assert.Null(confirmed.Request.EvidenceDocumentRefId); // Attachment is optional.
        Assert.Equal(f.Recorder.PrincipalId, confirmed.Request.RecordedByPrincipalId);
        Assert.Equal(f.Confirmer.PrincipalId, confirmed.Request.ConfirmedByPrincipalId);
        Assert.Equal("Approved", approved.Request.Status); // Pure transitions do not mutate prior states.
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("branch")]
    [InlineData("selected")][InlineData("grant")][InlineData("inactive")]
    [InlineData("principal")]
    public void Current_Core_scope_and_action_are_checked_before_a_command(string changed)
    {
        var f = new Fixture(); var draft = f.Create().Request;
        var actor = changed switch
        {
            "tenant" => f.Creator with { TenantId = "other" },
            "company" => f.Creator with { LegalEntityId = Guid.NewGuid() },
            "branch" => f.Creator with { AllowedBranchIds = new HashSet<Guid>() },
            "selected" => f.Creator with { SelectedBranchId = null },
            "grant" => f.Creator with { Actions = new HashSet<string>() },
            "inactive" => f.Creator with { IsActive = false },
            _ => f.Creator with { PrincipalId = "email@example.com" }
        };
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Submit(draft, 1, actor, f.Source, f.Now));
        Assert.Equal("Draft", draft.Status);
    }

    [Fact]
    public void Assignments_must_be_current_Core_principals_in_the_same_scope_with_the_required_action()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Create(
            f.Draft with { ApproverPrincipalId = f.Creator.PrincipalId }, f.Creator,
            f.Assignee(f.Creator.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Confirmer.PrincipalId, "confirm"), f.Source, f.Now));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Create(
            f.Draft with { ConfirmerPrincipalId = f.Recorder.PrincipalId }, f.Creator,
            f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Recorder.PrincipalId, "confirm"), f.Source, f.Now));
        Assert.Throws<InvalidOperationException>(() => f.Create(approver: f.Assignee(f.Approver.PrincipalId, "approve")
            with { IsActive = false }));
        Assert.Throws<InvalidOperationException>(() => f.Create(approver: f.Assignee(f.Approver.PrincipalId, "approve")
            with { TenantId = "other" }));
        Assert.Throws<InvalidOperationException>(() => f.Create(approver: f.Assignee(f.Approver.PrincipalId, "approve")
            with { Actions = new HashSet<string>() }));
    }

    [Fact]
    public void Stale_version_self_approval_and_self_confirmation_cannot_advance_payment()
    {
        var f = new Fixture(); var draft = f.Create().Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Submit(draft, 0, f.Creator, f.Source, f.Now));
        var submitted = ManualPaymentWorkflow.Submit(draft, 1, f.Creator, f.Source, f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Approve(submitted, 2,
            f.Creator with { Actions = f.Approver.Actions }, f.Source, f.Now));
        var approved = ManualPaymentWorkflow.Approve(submitted, 2, f.Approver, f.Source, f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Record(approved, 2,
            f.Recorder, f.Source, f.Evidence(), f.Now));
        var recorded = ManualPaymentWorkflow.Record(approved, 3, f.Recorder, f.Source, f.Evidence(), f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Confirm(recorded, 4,
            f.Recorder with { Actions = f.Confirmer.Actions }, f.Now));
        Assert.Equal("PaymentRecorded", recorded.Status);
    }

    [Theory]
    [InlineData("over")][InlineData("fraction")][InlineData("currency")]
    [InlineData("supplier")][InlineData("invoice")][InlineData("branch")]
    [InlineData("ineligible")]
    public void Draft_requires_exact_eligible_invoice_and_capacity(string changed)
    {
        var f = new Fixture(); var draft = f.Draft; var source = f.Source;
        switch (changed)
        {
            case "over": draft = draft with { Amount = 101m }; break;
            case "fraction": draft = draft with { Amount = 1.00001m }; break;
            case "currency": draft = draft with { CurrencyCode = "usd" }; break;
            case "supplier": source = source with { SupplierId = Guid.NewGuid() }; break;
            case "invoice": source = source with { InvoiceId = Guid.NewGuid() }; break;
            case "branch": source = source with { BranchId = Guid.NewGuid() }; break;
            default: source = source with { IsEligible = false }; break;
        }
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Create(draft, f.Creator,
            f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Confirmer.PrincipalId, "confirm"), source, f.Now));
    }

    [Fact]
    public void Recording_requires_exact_approved_amount_real_reference_and_nonfuture_date()
    {
        var f = new Fixture(); var approved = f.Approved();
        foreach (var evidence in new[]
        {
            f.Evidence() with { Amount = 24m },
            f.Evidence() with { ExternalReference = " " },
            f.Evidence() with { PaidOn = DateOnly.FromDateTime(f.Now.UtcDateTime).AddDays(1) }
        })
            Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Record(approved, 3,
                f.Recorder, f.Source, evidence, f.Now));
        Assert.Equal("Approved", approved.Status);
    }

    [Fact]
    public void An_actual_past_payment_date_can_precede_the_invoice_date_without_backdating_approval()
    {
        var f = new Fixture();
        var recorded = ManualPaymentWorkflow.Record(f.Approved(), 3, f.Recorder, f.Source,
            f.Evidence() with { PaidOn = f.Source.InvoiceDate.AddDays(-1) }, f.Now);
        Assert.Equal(f.Source.InvoiceDate.AddDays(-1), recorded.Request.PaidOn);
        Assert.Equal(f.Now, recorded.Request.RecordedAt);
        Assert.Equal(f.Now, recorded.Request.ApprovedAt);
    }

    [Fact]
    public void Another_editor_in_the_same_branch_cannot_revise_or_reassign_the_creators_draft()
    {
        var f = new Fixture(); var original = f.Create().Request;
        var otherEditor = f.Approver with { Actions = new HashSet<string> { "procurement.payment.edit" } };
        var error = Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Revise(
            original, original.Version, f.Draft with { Amount = 30m }, otherEditor,
            f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Confirmer.PrincipalId, "confirm"), f.Source, f.Now));
        Assert.Equal("ManualPaymentNotCreator", error.Message);
        Assert.Equal(25.1250m, original.Amount);
        Assert.Equal(f.Approver.PrincipalId, original.ApproverPrincipalId);
        Assert.Equal(1, original.Version);
        Assert.Equal("Draft", original.Status);
    }

    [Fact]
    public void Return_revise_and_resubmit_preserve_earlier_decisions_without_rewriting_prior_state()
    {
        var f = new Fixture(); var submitted = ManualPaymentWorkflow.Submit(f.Create().Request, 1,
            f.Creator, f.Source, f.Now).Request;
        var returned = ManualPaymentWorkflow.Return(submitted, 2, f.Approver, "Correct purpose", f.Now);
        var revised = ManualPaymentWorkflow.Revise(returned.Request, 3, f.Draft with { Amount = 20m },
            f.Creator, f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Confirmer.PrincipalId, "confirm"), f.Source, f.Now);
        Assert.Equal(20m, revised.Request.Amount);
        Assert.Equal(25.1250m, revised.Decision.PreviousAmount);
        Assert.Equal("Draft", revised.Request.Status);
        Assert.Equal("PendingApproval", submitted.Status);
        Assert.Equal(5, ManualPaymentWorkflow.Submit(revised.Request, 4, f.Creator, f.Source, f.Now).Request.Version);
    }

    [Fact]
    public void An_executed_payment_cannot_be_cancelled_and_dispute_never_removes_recorded_evidence()
    {
        var f = new Fixture(); var recorded = ManualPaymentWorkflow.Record(f.Approved(), 3,
            f.Recorder, f.Source, f.Evidence(), f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Cancel(recorded, 4,
            f.Creator with { Actions = new HashSet<string> { "procurement.payment.cancel" } }, "No", f.Now));
        var disputed = ManualPaymentWorkflow.Dispute(recorded, 4, f.Confirmer, "Reference under review", f.Now);
        Assert.Equal("Disputed", disputed.Request.Status);
        Assert.Equal("EXT-128", disputed.Request.ExternalReference);
        Assert.Equal(f.Recorder.PrincipalId, disputed.Request.RecordedByPrincipalId);
        Assert.Equal("PaymentRecorded", disputed.Decision.FromStatus);
    }

    private sealed class Fixture
    {
        public DateTimeOffset Now { get; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        public Guid Company { get; } = Guid.NewGuid();
        public Guid Branch { get; } = Guid.NewGuid();
        public Guid Invoice { get; } = Guid.NewGuid();
        public Guid Supplier { get; } = Guid.NewGuid();
        public ManualPaymentActor Creator => Actor("11111111-1111-1111-1111-111111111111", "create", "submit", "edit", "cancel");
        public ManualPaymentActor Approver => Actor("22222222-2222-2222-2222-222222222222", "approve");
        public ManualPaymentActor Recorder => Actor("33333333-3333-3333-3333-333333333333", "record");
        public ManualPaymentActor Confirmer => Actor("44444444-4444-4444-4444-444444444444", "confirm");
        public ManualPaymentDraft Draft => new(Branch, Invoice, Supplier, "USD", 25.1250m,
            Approver.PrincipalId, Recorder.PrincipalId, Confirmer.PrincipalId, "Invoice settlement");
        public ManualPaymentInvoiceSource Source => new(Invoice, "tenant", Company, Branch, Supplier,
            "USD", new DateOnly(2026, 10, 1), 100m, true);
        public ManualPaymentRecordedEvidence Evidence() => new(DateOnly.FromDateTime(Now.UtcDateTime),
            "Bank transfer made outside ERP", "EXT-128", 25.1250m, null);
        public ManualPaymentActor Actor(string principal, params string[] actions) => new(principal,
            "tenant", Company, Branch, new HashSet<Guid> { Branch },
            actions.Select(x => "procurement.payment." + x).ToHashSet(), true);
        public ManualPaymentAssignee Assignee(string principal, string action) => new(principal,
            "tenant", Company, new HashSet<Guid> { Branch },
            new HashSet<string> { "procurement.payment." + action }, true);
        public ManualPaymentChange Create(ManualPaymentAssignee? approver = null,
            ManualPaymentAssignee? recorder = null, ManualPaymentAssignee? confirmer = null) =>
            ManualPaymentWorkflow.Create(Draft, Creator,
                approver ?? Assignee(Approver.PrincipalId, "approve"),
                recorder ?? Assignee(Recorder.PrincipalId, "record"),
                confirmer ?? Assignee(Confirmer.PrincipalId, "confirm"), Source, Now);
        public ManualPaymentRequest Approved()
        {
            var submitted = ManualPaymentWorkflow.Submit(Create().Request, 1, Creator, Source, Now).Request;
            return ManualPaymentWorkflow.Approve(submitted, 2, Approver, Source, Now).Request;
        }
    }
}
