using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure foundation evidence; no registered API, mapped store, or bank execution.</summary>
public sealed class ManualPaymentWorkflowTests
{
    [Fact]
    public void Capacity_exclusion_cannot_be_reused_to_create_a_second_request_or_update_another()
    {
        var f = new Fixture(); var own = f.Create().Request;
        var source = f.Source with { ExcludingManualPaymentRequestId = own.Id };
        Assert.Equal("ManualPaymentCapacityExclusionInvalid", Assert.Throws<InvalidOperationException>(() =>
            ManualPaymentWorkflow.Create(f.Draft, f.Creator, f.Assignee(f.Approver.PrincipalId, "approve"),
                f.Assignee(f.Recorder.PrincipalId, "record"), f.Assignee(f.Confirmer.PrincipalId, "confirm"), source, f.Now)).Message);
        Assert.Equal("ManualPaymentCapacityExclusionInvalid", Assert.Throws<InvalidOperationException>(() =>
            ManualPaymentWorkflow.Submit(own, 1, f.Creator,
                source with { ExcludingManualPaymentRequestId = Guid.NewGuid() }, f.Now)).Message);
        Assert.Equal("PendingApproval", ManualPaymentWorkflow.Submit(own, 1, f.Creator, source, f.Now).Request.Status);
    }
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
            f.Evidence(), f.BusinessDate, f.Now);
        var confirmed = ManualPaymentWorkflow.Confirm(recorded.Request, 4, f.Confirmer, f.BusinessDate, f.Now);
        Assert.Equal("ConfirmedManually", confirmed.Request.Status);
        Assert.Equal(5, confirmed.Request.Version);
        Assert.Equal("PaymentRecorded", confirmed.Decision.FromStatus);
        Assert.Null(confirmed.Request.EvidenceDocumentRefId); // Attachment is optional.
        Assert.Equal(f.Recorder.PrincipalId, confirmed.Request.RecordedByPrincipalId);
        Assert.Equal(f.Confirmer.PrincipalId, confirmed.Request.ConfirmedByPrincipalId);
        Assert.Equal("Approved", approved.Request.Status); // Pure transitions do not mutate prior states.
    }

    [Fact]
    public void Confirmation_checks_both_assigned_and_actual_recorder_evidence_after_a_link_changes()
    {
        var f = new Fixture(); var oldRecorder = Guid.NewGuid(); var currentRecorder = Guid.NewGuid();
        var draft = f.Create(recorder: f.Assignee(f.Recorder.PrincipalId, "record") with { CoreLinkedWorkerId = oldRecorder }).Request;
        var submitted = ManualPaymentWorkflow.Submit(draft, 1, f.Creator, f.Source, f.Now).Request;
        var approved = ManualPaymentWorkflow.Approve(submitted, 2, f.Approver, f.Source, f.Now).Request;
        var recorded = ManualPaymentWorkflow.Record(approved, 3, f.Recorder with { CoreLinkedWorkerId = currentRecorder },
            f.Source, f.Evidence(), f.BusinessDate, f.Now).Request;
        Assert.Equal(oldRecorder, recorded.RecorderCoreLinkedWorkerId);
        Assert.Equal(currentRecorder, recorded.RecordedByCoreLinkedWorkerId);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Confirm(recorded, 4,
            f.Confirmer with { CoreLinkedWorkerId = oldRecorder }, f.BusinessDate, f.Now));
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("confirmation")]
    public void Distinct_accounts_with_the_same_known_Core_person_cannot_be_assigned_as_independent(string pair)
    {
        var f = new Fixture(); var worker = Guid.NewGuid();
        var approver = f.Assignee(f.Approver.PrincipalId, "approve");
        var recorder = f.Assignee(f.Recorder.PrincipalId, "record");
        var confirmer = f.Assignee(f.Confirmer.PrincipalId, "confirm");
        var creator = f.Creator;
        if (pair == "approval") { creator = creator with { CoreLinkedWorkerId = worker }; approver = approver with { CoreLinkedWorkerId = worker }; }
        else { recorder = recorder with { CoreLinkedWorkerId = worker }; confirmer = confirmer with { CoreLinkedWorkerId = worker }; }
        Assert.Throws<InvalidOperationException>(() => f.Create(approver, recorder, confirmer, creator));
    }

    [Fact]
    public void Optional_Core_links_are_pinned_without_requiring_active_employment_and_survive_all_transitions()
    {
        var f = new Fixture(); var makerLink = Guid.NewGuid(); var approvalLink = Guid.NewGuid();
        var recordLink = Guid.NewGuid(); var confirmLink = Guid.NewGuid();
        var maker = f.Creator with { CoreLinkedWorkerId = makerLink };
        var created = f.Create(f.Assignee(f.Approver.PrincipalId, "approve") with { CoreLinkedWorkerId = approvalLink },
            f.Assignee(f.Recorder.PrincipalId, "record") with { CoreLinkedWorkerId = recordLink },
            f.Assignee(f.Confirmer.PrincipalId, "confirm") with { CoreLinkedWorkerId = confirmLink }, maker);
        Assert.Equal(makerLink, created.Decision.ActorCoreLinkedWorkerId);
        Assert.Equal(approvalLink, created.Decision.NewApproverCoreLinkedWorkerId);
        var submitted = ManualPaymentWorkflow.Submit(created.Request, 1, maker, f.Source, f.Now);
        var approved = ManualPaymentWorkflow.Approve(submitted.Request, 2,
            f.Approver with { CoreLinkedWorkerId = approvalLink }, f.Source, f.Now);
        var recorded = ManualPaymentWorkflow.Record(approved.Request, 3,
            f.Recorder with { CoreLinkedWorkerId = recordLink }, f.Source, f.Evidence(), f.BusinessDate, f.Now);
        var confirmed = ManualPaymentWorkflow.Confirm(recorded.Request, 4,
            f.Confirmer with { CoreLinkedWorkerId = confirmLink }, f.BusinessDate, f.Now);
        Assert.Equal(makerLink, confirmed.Request.CreatorCoreLinkedWorkerId);
        Assert.Equal(approvalLink, confirmed.Request.ApprovedByCoreLinkedWorkerId);
        Assert.Equal(recordLink, confirmed.Request.RecordedByCoreLinkedWorkerId);
        Assert.Equal(confirmLink, confirmed.Request.ConfirmedByCoreLinkedWorkerId);
        Assert.Equal(confirmLink, confirmed.Decision.ActorCoreLinkedWorkerId);
        Assert.Equal("Draft", created.Request.Status);
    }

    [Theory]
    [InlineData("approve")][InlineData("return")][InlineData("reject")]
    public void Retained_creator_link_denies_a_newly_colliding_approval_actor(string action)
    {
        var f = new Fixture(); var worker = Guid.NewGuid();
        var draft = f.Create(creator: f.Creator with { CoreLinkedWorkerId = worker }).Request;
        var submitted = ManualPaymentWorkflow.Submit(draft, 1, f.Creator, f.Source, f.Now).Request;
        var alias = f.Approver with { CoreLinkedWorkerId = worker };
        Assert.Throws<InvalidOperationException>(() => action switch {
            "approve" => ManualPaymentWorkflow.Approve(submitted, 2, alias, f.Source, f.Now),
            "return" => ManualPaymentWorkflow.Return(submitted, 2, alias, "Correction", f.Now),
            _ => ManualPaymentWorkflow.Reject(submitted, 2, alias, "Rejected", f.Now)
        });
        Assert.Equal("PendingApproval", submitted.Status); Assert.Equal(2, submitted.Version);
    }

    [Fact]
    public void Reassigning_a_draft_keeps_original_creator_evidence_and_records_assignment_link_changes()
    {
        var f = new Fixture(); var makerLink = Guid.NewGuid(); var oldLink = Guid.NewGuid(); var newLink = Guid.NewGuid();
        var original = f.Create(approver: f.Assignee(f.Approver.PrincipalId, "approve") with { CoreLinkedWorkerId = oldLink },
            creator: f.Creator with { CoreLinkedWorkerId = makerLink }).Request;
        var revised = ManualPaymentWorkflow.Revise(original, 1, f.Draft, f.Creator,
            f.Assignee(f.Approver.PrincipalId, "approve") with { CoreLinkedWorkerId = newLink },
            f.Assignee(f.Recorder.PrincipalId, "record"), f.Assignee(f.Confirmer.PrincipalId, "confirm"), f.Source, f.Now);
        Assert.Equal(makerLink, revised.Request.CreatorCoreLinkedWorkerId);
        Assert.Equal(oldLink, revised.Decision.PreviousApproverCoreLinkedWorkerId);
        Assert.Equal(newLink, revised.Decision.NewApproverCoreLinkedWorkerId);
        Assert.Equal(oldLink, original.ApproverCoreLinkedWorkerId);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Revise(original, 1, f.Draft, f.Creator,
            f.Assignee(f.Approver.PrincipalId, "approve") with { CoreLinkedWorkerId = makerLink },
            f.Assignee(f.Recorder.PrincipalId, "record"), f.Assignee(f.Confirmer.PrincipalId, "confirm"), f.Source, f.Now));
    }

    [Theory]
    [InlineData("confirm")][InlineData("dispute")]
    public void Recorded_person_evidence_blocks_a_different_account_linked_to_the_recorder(string action)
    {
        var f = new Fixture(); var worker = Guid.NewGuid();
        var recorded = ManualPaymentWorkflow.Record(f.Approved(), 3,
            f.Recorder with { CoreLinkedWorkerId = worker }, f.Source, f.Evidence(), f.BusinessDate, f.Now).Request;
        var alias = f.Confirmer with { CoreLinkedWorkerId = worker };
        Assert.Throws<InvalidOperationException>(() => action == "confirm"
            ? ManualPaymentWorkflow.Confirm(recorded, 4, alias, f.BusinessDate, f.Now)
            : ManualPaymentWorkflow.Dispute(recorded, 4, alias, "Check reference", f.Now));
        Assert.Equal(worker, recorded.RecordedByCoreLinkedWorkerId);
        Assert.Equal("PaymentRecorded", recorded.Status);
    }

    [Fact]
    public void Recording_denies_a_person_who_has_become_the_assigned_confirmer_alias()
    {
        var f = new Fixture(); var worker = Guid.NewGuid();
        var draft = f.Create(confirmer: f.Assignee(f.Confirmer.PrincipalId, "confirm") with { CoreLinkedWorkerId = worker }).Request;
        var submitted = ManualPaymentWorkflow.Submit(draft, 1, f.Creator, f.Source, f.Now).Request;
        var approved = ManualPaymentWorkflow.Approve(submitted, 2, f.Approver, f.Source, f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Record(approved, 3,
            f.Recorder with { CoreLinkedWorkerId = worker }, f.Source, f.Evidence(), f.BusinessDate, f.Now));
        Assert.Null(approved.RecordedByPrincipalId);
    }

    [Fact]
    public void Empty_optional_Core_link_is_corrupt_evidence_rather_than_an_unlinked_account()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Create(creator: f.Creator with { CoreLinkedWorkerId = Guid.Empty }));
        Assert.Throws<InvalidOperationException>(() => f.Create(approver: f.Assignee(f.Approver.PrincipalId, "approve") with { CoreLinkedWorkerId = Guid.Empty }));
        var draft = f.Create().Request; draft.CreatorCoreLinkedWorkerId = Guid.Empty;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Submit(draft, 1, f.Creator, f.Source, f.Now));
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
            "selected" => f.Creator with { EffectiveBranchId = null },
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
            f.Recorder, f.Source, f.Evidence(), f.BusinessDate, f.Now));
        var recorded = ManualPaymentWorkflow.Record(approved, 3, f.Recorder, f.Source,
            f.Evidence(), f.BusinessDate, f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Confirm(recorded, 4,
            f.Recorder with { Actions = f.Confirmer.Actions }, f.BusinessDate, f.Now));
        Assert.Equal("PaymentRecorded", recorded.Status);
    }

    [Theory]
    [InlineData("amount")][InlineData("approvedAmount")][InlineData("recordedAmount")]
    [InlineData("approvedPrincipal")][InlineData("approvedAt")][InlineData("recordedPrincipal")]
    [InlineData("recordedAt")][InlineData("paidOn")][InlineData("futurePaidOn")]
    [InlineData("externalReference")]
    public void Confirmation_rejects_malformed_recorded_payment_evidence(string changed)
    {
        var f = new Fixture(); var recorded = f.Recorded();
        switch (changed)
        {
            case "amount": recorded.Amount = 0m; break;
            case "approvedAmount": recorded.ApprovedAmount = 0m; break;
            case "recordedAmount": recorded.RecordedAmount = 0m; break;
            case "approvedPrincipal": recorded.ApprovedByPrincipalId = "not-a-core-principal"; break;
            case "approvedAt": recorded.ApprovedAt = null; break;
            case "recordedPrincipal": recorded.RecordedByPrincipalId = "not-a-core-principal"; break;
            case "recordedAt": recorded.RecordedAt = null; break;
            case "paidOn": recorded.PaidOn = default(DateOnly); break;
            case "futurePaidOn": recorded.PaidOn = f.BusinessDate.AddDays(1); break;
            default: recorded.ExternalReference = " "; break;
        }
        var beforeVersion = recorded.Version;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Confirm(recorded,
            beforeVersion, f.Confirmer, f.BusinessDate, f.Now));
        Assert.Equal("PaymentRecorded", recorded.Status);
        Assert.Equal(beforeVersion, recorded.Version);
        Assert.Null(recorded.ConfirmedByPrincipalId);
    }

    [Theory]
    [InlineData("created")][InlineData("updated")][InlineData("approved")]
    [InlineData("recorded")]
    public void A_transition_cannot_use_server_time_before_retained_history(string changed)
    {
        var f = new Fixture(); var recorded = f.Recorded();
        switch (changed)
        {
            case "created": recorded.CreatedAt = f.Now.AddSeconds(1); break;
            case "updated": recorded.UpdatedAt = f.Now.AddSeconds(1); break;
            case "approved": recorded.ApprovedAt = f.Now.AddSeconds(1); break;
            default: recorded.RecordedAt = f.Now.AddSeconds(1); break;
        }
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Confirm(recorded,
            recorded.Version, f.Confirmer, f.BusinessDate, f.Now));
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
            f.Evidence() with { PaidOn = f.BusinessDate.AddDays(1) }
        })
            Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Record(approved, 3,
                f.Recorder, f.Source, evidence, f.BusinessDate, f.Now));
        Assert.Equal("Approved", approved.Status);
    }

    [Fact]
    public void Company_business_date_can_be_next_day_when_UTC_is_still_previous_day()
    {
        var utc = new DateTimeOffset(2026, 10, 23, 22, 30, 0, TimeSpan.Zero);
        var companyDate = new DateOnly(2026, 10, 24); // Supplied by trusted Core company settings.
        var f = new Fixture(utc, companyDate);
        var recorded = ManualPaymentWorkflow.Record(f.Approved(), 3, f.Recorder,
            f.Source, f.Evidence(), f.BusinessDate, f.Now);
        Assert.Equal(companyDate, recorded.Request.PaidOn);
        Assert.Equal(utc, recorded.Request.RecordedAt);
        var confirmed = ManualPaymentWorkflow.Confirm(recorded.Request, 4,
            f.Confirmer, f.BusinessDate, f.Now);
        Assert.Equal("ConfirmedManually", confirmed.Request.Status);
        Assert.Equal(utc, confirmed.Request.ConfirmedAt);
    }

    [Fact]
    public void Exact_eight_place_outstanding_can_cover_four_place_payment_without_rounding()
    {
        var f = new Fixture();
        var source = f.Source with { RemainingExcludingThisRequest = 25.12500001m };
        var created = ManualPaymentWorkflow.Create(f.Draft, f.Creator,
            f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
            f.Assignee(f.Confirmer.PrincipalId, "confirm"), source, f.Now);
        Assert.Equal(25.1250m, created.Request.Amount);
        Assert.Equal(25.12500001m, source.RemainingExcludingThisRequest);
        foreach (var invalid in new[]
        {
            f.Source with { RemainingExcludingThisRequest = 25.12499999m },
            f.Source with { RemainingExcludingThisRequest = -0.00000001m },
            f.Source with { RemainingExcludingThisRequest = 100.00000001m },
            f.Source with { RemainingExcludingThisRequest = 25.125000001m },
            f.Source with { InvoiceGrossAmount = 100.00001m }
        })
            Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Create(f.Draft, f.Creator,
                f.Assignee(f.Approver.PrincipalId, "approve"), f.Assignee(f.Recorder.PrincipalId, "record"),
                f.Assignee(f.Confirmer.PrincipalId, "confirm"), invalid, f.Now));
    }

    [Fact]
    public void An_actual_past_payment_date_can_precede_the_invoice_date_without_backdating_approval()
    {
        var f = new Fixture();
        var recorded = ManualPaymentWorkflow.Record(f.Approved(), 3, f.Recorder, f.Source,
            f.Evidence() with { PaidOn = f.Source.InvoiceDate.AddDays(-1) }, f.BusinessDate, f.Now);
        Assert.Equal(f.Source.InvoiceDate.AddDays(-1), recorded.Request.PaidOn);
        Assert.Equal(f.Now, recorded.Request.RecordedAt);
        Assert.Equal(f.Now, recorded.Request.ApprovedAt);
    }

    [Fact]
    public void Another_editor_in_the_same_branch_cannot_revise_or_reassign_the_creators_draft()
    {
        var f = new Fixture(); var original = f.Create().Request;
        var otherEditor = f.Approver with { Actions = new HashSet<string> { "procurement-payment-edit" } };
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
            f.Recorder, f.Source, f.Evidence(), f.BusinessDate, f.Now).Request;
        Assert.Throws<InvalidOperationException>(() => ManualPaymentWorkflow.Cancel(recorded, 4,
            f.Creator with { Actions = new HashSet<string> { "procurement-payment-cancel" } }, "No", f.Now));
        var disputed = ManualPaymentWorkflow.Dispute(recorded, 4, f.Confirmer, "Reference under review", f.Now);
        Assert.Equal("Disputed", disputed.Request.Status);
        Assert.Equal("EXT-128", disputed.Request.ExternalReference);
        Assert.Equal(f.Recorder.PrincipalId, disputed.Request.RecordedByPrincipalId);
        Assert.Equal("PaymentRecorded", disputed.Decision.FromStatus);
    }

    private sealed class Fixture
    {
        public Fixture(DateTimeOffset? now = null, DateOnly? businessDate = null)
        {
            Now = now ?? new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
            BusinessDate = businessDate ?? DateOnly.FromDateTime(Now.UtcDateTime);
        }
        public DateTimeOffset Now { get; }
        public DateOnly BusinessDate { get; }
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
            "USD", new DateOnly(2026, 10, 1), 100m, 100m, true);
        public ManualPaymentRecordedEvidence Evidence() => new(BusinessDate,
            "Bank transfer made outside ERP", "EXT-128", 25.1250m, null);
        public ManualPaymentActor Actor(string principal, params string[] actions) => new(principal,
            "tenant", Company, Branch, new HashSet<Guid> { Branch },
            actions.Select(x => "procurement-payment-" + x).ToHashSet(), true);
        public ManualPaymentAssignee Assignee(string principal, string action) => new(principal,
            "tenant", Company, new HashSet<Guid> { Branch },
            new HashSet<string> { "procurement-payment-" + action }, true);
        public ManualPaymentChange Create(ManualPaymentAssignee? approver = null,
            ManualPaymentAssignee? recorder = null, ManualPaymentAssignee? confirmer = null, ManualPaymentActor? creator = null) =>
            ManualPaymentWorkflow.Create(Draft, creator ?? Creator,
                approver ?? Assignee(Approver.PrincipalId, "approve"),
                recorder ?? Assignee(Recorder.PrincipalId, "record"),
                confirmer ?? Assignee(Confirmer.PrincipalId, "confirm"), Source, Now);
        public ManualPaymentRequest Approved()
        {
            var submitted = ManualPaymentWorkflow.Submit(Create().Request, 1, Creator, Source, Now).Request;
            return ManualPaymentWorkflow.Approve(submitted, 2, Approver, Source, Now).Request;
        }
        public ManualPaymentRequest Recorded() => ManualPaymentWorkflow.Record(Approved(), 3,
            Recorder, Source, Evidence(), BusinessDate, Now).Request;
    }
}
