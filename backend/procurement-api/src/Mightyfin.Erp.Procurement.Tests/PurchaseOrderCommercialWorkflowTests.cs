using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseOrderCommercialWorkflowTests
{
    [Fact]
    public void Core_approval_and_issue_preserve_one_sealed_version_without_claiming_delivery()
    {
        var f = new Fixture(); var pin = f.Submit();
        f.Approve(pin);
        var issued = PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order, f.Lines, f.Supplier, pin,
            f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1));
        Assert.Equal("IssuePending", f.Source.Order.Status);
        Assert.Null(f.Source.Order.IssuedAt);
        Assert.Equal(1, f.Source.Line.OrderedQuantity);
        Assert.Equal(0, f.Source.Line.AllocatedQuantity);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order,
            f.Lines, f.Supplier, pin, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1)));
        Assert.Equal(1, f.Source.Line.OrderedQuantity);
        var delivery = PurchaseOrderCommercialWorkflow.ConfirmDelivery(f.Source.Order, issued, "DELIVERY-1",
            "Unknown", "SecureEmail", "SYNTHETIC-EVIDENCE", "delivery-operator", f.Now.AddMinutes(2), f.Now.AddMinutes(2), Guid.NewGuid());
        Assert.Equal("Unknown", delivery.Outcome); Assert.Equal("IssuePending", f.Source.Order.Status);
        _ = PurchaseOrderCommercialWorkflow.ConfirmDelivery(f.Source.Order, issued, "DELIVERY-2", "Delivered",
            "SecureEmail", "SYNTHETIC-EVIDENCE-2", "delivery-operator", f.Now.AddMinutes(3), f.Now.AddMinutes(3), Guid.NewGuid());
        Assert.Equal("Issued", f.Source.Order.Status);
        Assert.Equal(pin.CommercialSnapshotSha256, issued.CommercialSnapshotSha256);
    }

    [Fact]
    public void Real_order_submission_and_issue_feed_receiving_with_source_unit_case_preserved()
    {
        // Produce PR approval, allocation, PO snapshots, worker identity and delivery using owner workflows.
        var f = new Fixture(new PurchaseOrderSourceAllocationTests.Fixture(sourceUnitCode: "ea", purchaseType: "GOODS"));
        var submission = f.Submit(); f.Approve(submission);
        var issued = PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order, f.Lines, f.Supplier,
            submission, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1));
        var delivery = PurchaseOrderCommercialWorkflow.ConfirmDelivery(f.Source.Order, issued, "RECEIVING-DELIVERY",
            "Delivered", "SecureEmail", "SYNTHETIC-DELIVERY", "dispatch", f.Now.AddMinutes(2), f.Now.AddMinutes(2), Guid.NewGuid());
        var originalLineage = submission.SourceEvidenceJson;
        Assert.Contains("\"UnitCode\":\"ea\"", originalLineage);
        Assert.Contains("\"UnitCode\":\"EA\"", issued.CommercialSnapshotJson);
        ReceivingSourceProjection Projection()
        {
            var lineageHash = ProcurementEvidenceEncoding.PolicyJsonDigest("{\"sources\":" + submission.SourceEvidenceJson + "}");
            var classification = JsonSerializer.Serialize(new { tenantId = f.Source.Order.TenantId,
                legalEntityId = f.Source.Company, issuedVersionId = issued.Id, orderLineId = f.Source.PoLine.Id,
                sourceEvidenceSha256 = lineageHash, reference = "RECEIVING-TEST", kind = "Goods", netPriceBasis = ControlledReceiving.NetPriceBasis });
            var pin = new ReceivingSourcePin(f.Source.Order.TenantId, f.Source.Company, f.Source.Order.BranchRef,
                f.Source.Order.Id, issued.Id, f.Source.PoLine.Id, f.Source.Line.Id, issued.RevisionNumber,
                issued.CommercialSnapshotSha256, lineageHash, "RECEIVING-TEST", ProcurementEvidenceEncoding.PolicyJsonDigest(classification),
                ReceivingKind.Goods, f.Source.Line.CategoryCode!, "GOODS", "ZMW", "EA", 1m, 10m, ControlledReceiving.NetPriceBasis);
            return new(issued, submission, delivery, pin, f.BuyerWorker, classification, true);
        }
        var source = Projection();
        ControlledReceiving.VerifySource(source);
        Assert.Equal(originalLineage, submission.SourceEvidenceJson); // no rewriting retained request intent.
        var sealedMismatch = Assert.Throws<InvalidOperationException>(() =>
            ControlledReceiving.VerifySource(source with { Pin = source.Pin with { UnitCode = "ea" } }));
        Assert.Equal("IssuedLineMismatch", sealedMismatch.InnerException!.Message);
        foreach (var nonEquivalent in new[] { "EACH", "BOX", " ea " })
        {
            // Simulate corrupt retained evidence with a freshly consistent digest; no synonym is authorized.
            typeof(PurchaseOrderSubmission).GetProperty(nameof(PurchaseOrderSubmission.SourceEvidenceJson))!.SetValue(submission,
                originalLineage.Replace("\"UnitCode\":\"ea\"", "\"UnitCode\":\"" + nonEquivalent + "\""));
            var error = Assert.Throws<InvalidOperationException>(() => ControlledReceiving.VerifySource(Projection()));
            Assert.Equal("SourceClassificationMismatch", error.InnerException!.Message);
        }
        typeof(PurchaseOrderSubmission).GetProperty(nameof(PurchaseOrderSubmission.SourceEvidenceJson))!.SetValue(submission, originalLineage);
        ControlledReceiving.VerifySource(Projection());
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Suspended")]
    [InlineData("expiredQualification")]
    public void Supplier_labels_or_expired_checks_cannot_authorize_issue(string state)
    {
        var f = new Fixture(); var pin = f.Submit(); f.Approve(pin);
        if (state == "expiredQualification") f.Supplier.Qualifications.Add(new SupplierQualification
        { QualificationType = "MANDATORY", ReviewStatus = "Verified", ExpiresOn = DateOnly.FromDateTime(f.Now.UtcDateTime).AddDays(-1), IsActive = true });
        else f.Supplier.Status = state;
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order,
            f.Lines, f.Supplier, pin, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1)));
        Assert.Equal(0, f.Source.Line.OrderedQuantity);
    }

    [Fact]
    public void Changed_price_after_submission_requires_a_new_approval_not_a_silent_edit()
    {
        var f = new Fixture(); var pin = f.Submit(); f.Approve(pin);
        f.Source.PoLine.UnitPrice++;
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order,
            f.Lines, f.Supplier, pin, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1)));
        Assert.Equal("Approved", f.Source.Order.Status);
    }

    [Fact]
    public void Finance_must_attest_exact_gross_amount_and_the_original_reservation_transfer()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Submit(f.Source.Order,
            f.Lines, f.Supplier, f.Policy, f.MakeFinance(9), Guid.NewGuid(), f.Now, f.BuyerWorker));
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Submit(f.Source.Order,
            f.Lines, f.Supplier, f.Policy, f.MakeFinance(11), Guid.NewGuid(), f.Now, f.BuyerWorker));
        Assert.Equal("Draft", f.Source.Order.Status);
    }

    [Fact]
    public void Same_amount_cost_centre_change_requires_new_finance_confirmation()
    {
        var f = new Fixture();
        f.Source.PoLine.CostCenterRef = "OTHER-COST-CENTRE";
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Submit(f.Source.Order,
            f.Lines, f.Supplier, f.Policy, f.Finance, Guid.NewGuid(), f.Now, f.BuyerWorker));
        Assert.Equal("Draft", f.Source.Order.Status);
        Assert.Equal(0, f.Source.Order.CurrentRevision);
    }

    [Fact]
    public void Different_accounts_for_the_same_buyer_cannot_approve_or_confirm_vendor_delivery()
    {
        var f = new Fixture(); var pin = f.Submit();
        var syntheticDecision = new ApprovalDecision { TenantId = f.Supplier.TenantId,
            LegalEntityId = f.Supplier.LegalEntityId, CaseId = f.Case!.Id, ActorSubjectId = "buyer-alias" };
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.ApplyDecision(
            f.Source.Order, pin, f.Case!, syntheticDecision, null, f.BuyerWorker));
        f.Approve(pin);
        var issued = PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order, f.Lines, f.Supplier,
            pin, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.ConfirmDelivery(
            f.Source.Order, issued, "DELIVERY", "Delivered", "SecureEmail", "EVIDENCE", "buyer-alias",
            f.Now.AddMinutes(2), f.Now.AddMinutes(2), f.BuyerWorker));
        Assert.Equal("IssuePending", f.Source.Order.Status);
    }

    [Fact]
    public void Retained_requester_identity_prevents_finance_alias_independence()
    {
        var f = new Fixture();
        var participants = PurchaseOrderWorkerIdentity.Participants(f.Lines, f.BuyerWorker);
        Assert.False(PurchaseOrderWorkerIdentity.IsIndependent(participants, f.Source.Request.RequesterWorkerId));
        Assert.False(PurchaseOrderWorkerIdentity.IsIndependent(participants, f.BuyerWorker));
        Assert.True(PurchaseOrderWorkerIdentity.IsIndependent(participants, Guid.NewGuid()));
        Assert.False(PurchaseOrderWorkerIdentity.IsIndependent("[]", Guid.NewGuid()));
    }

    [Fact]
    public void Return_can_verify_original_evidence_after_supplier_suspension_and_rename()
    {
        var f = new Fixture(); var pin = f.Submit();
        f.Supplier.Status = "Suspended"; f.Supplier.LegalName = "Updated supplier legal name";
        PurchaseOrderCommercialWorkflow.VerifyForReturn(f.Source.Order, f.Lines, pin, f.Policy, f.Finance);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Verify(f.Source.Order,
            f.Lines, f.Supplier, pin, f.Policy, f.Finance, f.Now));
        f.Source.PoLine.UnitPrice++;
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.VerifyForReturn(
            f.Source.Order, f.Lines, pin, f.Policy, f.Finance));
    }

    [Theory]
    [InlineData("Return", "Draft", "ReopenedDraft", 1)]
    [InlineData("Reject", "Cancelled", "CancelledRejectedOrder", 0)]
    public void Confirmed_finance_release_reopens_return_or_releases_rejected_quantities(
        string action, string finalState, string resolutionOutcome, int reservedQuantity)
    {
        var f = new Fixture(); var pin = f.Submit(); f.Decide(pin, action);
        var resolution = Release(f, pin, Guid.NewGuid());
        Assert.Equal(resolutionOutcome, resolution.Outcome);
        Assert.Equal(finalState, f.Source.Order.Status);
        Assert.Equal(reservedQuantity, f.Source.Line.AllocatedQuantity);
        Assert.Equal(0, f.Source.Line.OrderedQuantity);
        Assert.Null(f.Source.Order.FinanceCommitmentRef);
        Assert.Null(f.Source.Order.ApprovalInstanceRef);
        Assert.Throws<InvalidOperationException>(() => Release(f, pin, Guid.NewGuid()));
        Assert.Equal(reservedQuantity, f.Source.Line.AllocatedQuantity);
    }

    [Fact]
    public void Release_requires_independent_retained_worker_and_exact_finance_amount()
    {
        var f = new Fixture(); var pin = f.Submit(); f.Decide(pin, "Return");
        Assert.Throws<InvalidOperationException>(() => Release(f, pin, f.BuyerWorker));
        Assert.Throws<InvalidOperationException>(() => Release(f, pin, f.ApproverWorker));
        Assert.Throws<InvalidOperationException>(() => Release(f, pin, Guid.NewGuid(), 9));
        Assert.Equal("Returned", f.Source.Order.Status);
        Assert.Equal(1, f.Source.Line.AllocatedQuantity);
        Assert.NotNull(f.Source.Order.FinanceCommitmentRef);
    }

    private static PurchaseOrderFinanceResolution Release(Fixture f, PurchaseOrderSubmission pin, Guid verifier,
        decimal amount = 10) => PurchaseOrderFinanceResolutionWorkflow.Resolve(f.Source.Order, f.Lines,
            pin, f.Policy, f.Finance, f.Case!, f.Assignments, "SYNTHETIC-RELEASE", "SYNTHETIC-POLICY",
            amount, "ZMW", [f.Source.Source.Finance.ReservationRef!], "finance-release-account", verifier,
            "Release externally confirmed", f.Now.AddMinutes(1), f.Now.AddMinutes(1));

    [Fact]
    public void Cancelling_unfunded_source_draft_releases_quantity_but_preserves_PR_budget_reservation()
    {
        var f = new Fixture();
        PurchaseOrderFinanceResolutionWorkflow.CancelUnfundedDraft(f.Source.Order, f.Lines, null, null,
            "Need cancelled", f.Now);
        Assert.Equal("Cancelled", f.Source.Order.Status); Assert.Equal(0, f.Source.Line.AllocatedQuantity);
        Assert.Equal("Released", f.Lines.Single().Allocation.Status);
        Assert.Equal("Confirmed", f.Source.Source.Finance.ReservationStatus);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderFinanceResolutionWorkflow.CancelUnfundedDraft(
            f.Source.Order, f.Lines, null, null, "Repeated without receipt", f.Now));
    }

    [Fact]
    public void Funded_draft_requires_independent_finance_release_without_inventing_a_Core_case()
    {
        var f = new Fixture(); f.Source.Order.FinanceCommitmentRef = f.Finance.CommitmentReference;
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderFinanceResolutionWorkflow.CancelUnfundedDraft(
            f.Source.Order, f.Lines, f.Finance, null, "Need cancelled", f.Now.AddYears(1)));
        Assert.Equal(1, f.Source.Line.AllocatedQuantity); // Expiry alone releases nothing.
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderFinanceResolutionWorkflow.ResolveUnsubmittedDraft(
            f.Source.Order, f.Lines, f.Finance, "RELEASE", "APPROVED-POLICY", 10, "ZMW",
            [f.Source.Source.Finance.ReservationRef!], "buyer-alias", f.BuyerWorker, "Cancel", f.Now, f.Now));
        var resolved = PurchaseOrderFinanceResolutionWorkflow.ResolveUnsubmittedDraft(f.Source.Order, f.Lines,
            f.Finance, "RELEASE", "APPROVED-POLICY", 10, "ZMW", [f.Source.Source.Finance.ReservationRef!],
            "independent-finance", Guid.NewGuid(), "Cancel", f.Now, f.Now);
        Assert.Null(resolved.SubmissionId); Assert.Equal("CancelledUnsubmittedOrder", resolved.Outcome);
        Assert.Equal("Cancelled", f.Source.Order.Status); Assert.Equal(0, f.Source.Line.AllocatedQuantity);
        Assert.Null(f.Source.Order.FinanceCommitmentRef);
    }

    [Fact]
    public void Operational_source_and_method_row_revisions_do_not_rewrite_submitted_business_evidence()
    {
        var f = new Fixture(); var pin = f.Submit(); f.Approve(pin);
        f.Source.Request.Version++; f.Source.Source.Method.Version++;
        f.Source.Source.Method.Name = "Updated administrative display label";
        var issued = PurchaseOrderCommercialWorkflow.AuthorizeIssue(f.Source.Order, f.Lines, f.Supplier,
            pin, f.Policy, f.Finance, f.Case!, "buyer", f.Now.AddMinutes(1));
        Assert.Equal(pin.CommercialSnapshotSha256, issued.CommercialSnapshotSha256);
        Assert.Equal("IssuePending", f.Source.Order.Status);
    }

    [Fact]
    public void Commercial_totals_reject_a_high_precision_product_instead_of_rounding_it()
    {
        var line = new PurchaseOrderLine { Quantity = 99999999999999.9999m, UnitPrice = 99999999999999.9999m };
        var before = (line.Quantity, line.UnitPrice, line.TaxAmount);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Totals([line]));
        Assert.Equal(before, (line.Quantity, line.UnitPrice, line.TaxAmount));
    }

    [Fact]
    public void Individually_exact_commercial_lines_must_also_have_an_exact_cumulative_total()
    {
        var lines = new[]
        {
            new PurchaseOrderLine { Quantity = 10000000000000m, UnitPrice = 99999999999999.9999m },
            new PurchaseOrderLine { Quantity = 0.0001m, UnitPrice = 99999999999999.9999m },
        };
        foreach (var line in lines) _ = PurchaseOrderCommercialWorkflow.Totals([line]);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Totals(lines));
    }

    [Fact]
    public void Inexact_gross_total_blocks_submission_before_order_revision_or_source_mutation()
    {
        var f = new Fixture(new(99999999999999.9999m, 0.0001m), 10000000000000m);
        f.Source.PoLine.UnitPrice = 99999999999999.9999m;
        f.Source.PoLine.TaxAmount = 0.0001m;
        var before = (f.Source.Order.Status, f.Source.Order.CurrentRevision, f.Source.Order.SubmittedBusinessVersion,
            f.Source.Order.SubmittedAt, f.Source.Order.ApprovalInstanceRef, f.Source.Order.FinanceCommitmentRef,
            f.Source.Request.UpdatedAt, f.Source.Line.AllocatedQuantity, f.Source.Line.OrderedQuantity);
        var error = Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Submit(
            f.Source.Order, f.Lines, f.Supplier, f.Policy, f.Finance, Guid.NewGuid(), f.Now, f.BuyerWorker));
        Assert.Equal("ExactDecimalNotRepresentable", error.Message);
        Assert.Equal(before, (f.Source.Order.Status, f.Source.Order.CurrentRevision, f.Source.Order.SubmittedBusinessVersion,
            f.Source.Order.SubmittedAt, f.Source.Order.ApprovalInstanceRef, f.Source.Order.FinanceCommitmentRef,
            f.Source.Request.UpdatedAt, f.Source.Line.AllocatedQuantity, f.Source.Line.OrderedQuantity));
    }

    private sealed class Fixture
    {
        public PurchaseOrderSourceAllocationTests.Fixture Source { get; }
        public DateTimeOffset Now => Source.Now.AddMinutes(2);
        public Guid BuyerWorker { get; } = Guid.NewGuid();
        public IReadOnlyList<PurchaseOrderSourceLine> Lines { get; }
        public Supplier Supplier { get; }
        public PolicyVersion Policy { get; }
        public PurchaseOrderFinanceEvidence Finance { get; }
        public ApprovalCase? Case { get; private set; }
        public Guid ApproverWorker { get; } = Guid.NewGuid();
        public List<PurchaseOrderAssignmentAuthorityEvidence> Assignments { get; } = [];
        public Fixture(PurchaseOrderSourceAllocationTests.Fixture? source = null, decimal reserveQuantity = 1m)
        {
            Source = source ?? new();
            var allocation = Source.Reserve(reserveQuantity);
            Lines = [new(Source.PoLine, Source.Source, allocation)];
            Supplier = new() { Id = Source.Order.SupplierId, TenantId = PurchaseOrderSourceAllocationTests.Fixture.Tenant,
                LegalEntityId = Source.Company, LegalName = "Synthetic supplier", DisplayName = "Supplier",
                CountryCode = "ZM", Status = "Active", IsOrderable = true };
            Policy = new() { TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                PolicyKey = "PURCHASE_ORDER", PolicyVersionNumber = 1, State = "Published", EffectiveFrom = Now.AddDays(-1),
                RulesJson = """
                {"schemaVersion":1,"transactionType":"PurchaseOrder","currencyCode":"ZMW","amountBasis":"ApprovedLineBase",
                 "rules":[{"code":"DEFAULT","priority":0,
                   "when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[]},
                   "result":{"budgetReservation":"Required","sourcingMethodCode":null,"minimumQuoteCount":null,
                     "approvalStages":[{"code":"COMMERCIAL","assignee":"ProcurementManager","mode":"One"}]}}]}
                """ };
            Finance = MakeFinance(10);
        }
        public PurchaseOrderFinanceEvidence MakeFinance(decimal amount) => new()
        {
            TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
            PurchaseOrderId = Source.Order.Id, SupplierId = Supplier.Id, CommercialRevision = 1,
            CommitmentReference = "SYNTHETIC-PO-COMMITMENT", EvidenceReference = "SYNTHETIC-EVIDENCE",
            EvidenceMode = "ApprovedManualEvidence", ApprovedManualPolicyReference = "SYNTHETIC-POLICY",
            Status = "Confirmed", CurrencyCode = "ZMW", ConfirmedGrossAmount = amount,
            SourceReservationsJson = JsonSerializer.Serialize(new[] { Source.Source.Finance.ReservationRef }),
            BuyerWorkerId = BuyerWorker, ParticipantWorkersJson = PurchaseOrderWorkerIdentity.Participants(Lines, BuyerWorker),
            WorkerIdentitySha256 = PurchaseOrderWorkerIdentity.Digest(Source.Order, 1, Guid.Empty, BuyerWorker,
                PurchaseOrderWorkerIdentity.Participants(Lines, BuyerWorker)),
            CommercialSnapshotJson = PurchaseOrderCommercialWorkflow.Snapshot(Source.Order, Lines.Select(x => x.OrderLine).ToArray(), Supplier, 1),
            CommercialSnapshotSha256 = ProcurementEvidenceEncoding.Digest(PurchaseOrderCommercialWorkflow.Snapshot(
                Source.Order, Lines.Select(x => x.OrderLine).ToArray(), Supplier, 1)),
            VerifiedByPrincipalId = "independent-finance", VerifiedByWorkerId = Guid.NewGuid(), ConfirmedAt = Now,
        };
        public PurchaseOrderSubmission Submit()
        {
            var id = Guid.NewGuid();
            var pin = PurchaseOrderCommercialWorkflow.Submit(Source.Order, Lines, Supplier, Policy, Finance, id, Now, BuyerWorker);
            Source.Order.Version++; // persistence advances the operational row revision.
            Case = new() { Id = id, TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                SourceModule = "procurement", SourceType = "purchase_order", SourceId = Source.Order.Id,
                SourceVersion = 1, PolicyVersionId = Policy.Id, PolicyVersionNumber = 1, MakerSubjectId = "buyer" };
            return pin;
        }
        public void Approve(PurchaseOrderSubmission pin) => Decide(pin, "Approve");
        public void Decide(PurchaseOrderSubmission pin, string action)
        {
            var stage = new ApprovalStage { TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                CaseId = Case!.Id, Sequence = 1, Code = "COMMERCIAL", Mode = "Any", RequiredApprovals = 1, State = "Active" };
            var assignment = new ApprovalAssignment { TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                CaseId = Case.Id, StageId = stage.Id, AssigneeSubjectId = "independent-po-approver", PermissionKey = "procurement-po-approve" };
            stage.Assignments.Add(assignment); Case.Stages.Add(stage);
            var grant = new ProcurementSpendingAuthorityGrant { TenantId = Supplier.TenantId,
                LegalEntityId = Supplier.LegalEntityId, PrincipalId = assignment.AssigneeSubjectId,
                AuthorityReference = "SYNTHETIC-PO-AUTHORITY", AuthorityVersion = 1, Version = 1,
                Permission = "procurement-po-approve", Action = "PurchaseOrder.Approve", AmountBasis = "ApprovedLineBase",
                State = "Active", CurrencyCode = "ZMW", MaximumAmount = 100, EffectiveFrom = Now.AddDays(-1),
                AllBranches = true, AllDepartments = true, AllCategories = true };
            var authority = ProcurementSpendingAuthority.Evaluate(PurchaseOrderCommercialWorkflow.SpendingContext(Source.Order, Lines),
                assignment.AssigneeSubjectId, grant.Permission, grant.Action, Now, [grant]).Evidence!;
            var authorityJson = JsonSerializer.Serialize(new PurchaseOrderAssignmentIdentityEvidence(ApproverWorker, authority));
            Assignments.Add(new PurchaseOrderAssignmentAuthorityEvidence { TenantId = Supplier.TenantId,
                LegalEntityId = Supplier.LegalEntityId, SubmissionId = pin.Id, CoreApprovalCaseId = Case.Id,
                CoreStageId = stage.Id, CoreAssignmentId = assignment.Id, GrantId = grant.Id, GrantRevision = grant.Version,
                AuthorityVersion = grant.AuthorityVersion, PrincipalId = assignment.AssigneeSubjectId,
                AssigneeWorkerId = ApproverWorker, AssigneeKind = "ProcurementManager", AuthorityEvidenceJson = authorityJson,
                AuthorityEvidenceSha256 = ProcurementEvidenceEncoding.Digest(authorityJson) });
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(Case, assignment, new("independent-po-approver", Supplier.TenantId, Supplier.LegalEntityId,
                new HashSet<string> { "procurement-po-approve" }), action, Guid.NewGuid().ToString("D"), action == "Approve" ? null : "Review required", decisions, Now);
            PurchaseOrderCommercialWorkflow.ApplyDecision(Source.Order, pin, Case, decisions.Single(), action == "Approve" ? null : "Review required", ApproverWorker);
            Source.Order.Version++;
        }
    }
}
