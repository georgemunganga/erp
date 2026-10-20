using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestApprovalPinningTests
{
    [Fact]
    public void Operational_revision_and_approval_link_do_not_change_submitted_business_evidence()
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        f.Request.Version += 4;
        f.Request.ApprovalState = "InProgress";
        f.Request.UpdatedAt = f.Now.AddMinutes(2);
        f.Request.Lines.First().Version++;
        Assert.True(f.Verify(pin).IsValid);
        Assert.Equal(4, pin.SubmittedBusinessVersion);
        Assert.Equal(250m, pin.EstimatedAmount);
        Assert.Equal("EstimatedLineBase", pin.AmountBasis);
    }

    [Fact]
    public void Snapshot_is_stable_across_line_collection_order_and_decimal_scale()
    {
        var f = new Fixture();
        f.AddLine();
        var first = f.Pin();
        f.Request.Lines = f.Request.Lines.Reverse().ToList();
        f.Request.Lines.First().RequestedQuantity = 1.0000m;
        var second = f.Pin();
        Assert.Equal(first.RequestSnapshotJson, second.RequestSnapshotJson);
        Assert.Equal(first.RequestSnapshotSha256, second.RequestSnapshotSha256);
    }

    [Fact]
    public void Snapshot_is_stable_across_server_cultures()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var f = new Fixture();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var first = f.Pin();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = f.Pin();
            Assert.Equal(first.RequestSnapshotJson, second.RequestSnapshotJson);
            Assert.Equal(first.FinanceSnapshotSha256, second.FinanceSnapshotSha256);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("description")]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("coding")]
    [InlineData("tax")]
    [InlineData("discount")]
    [InlineData("catalogVersion")]
    [InlineData("requestPurpose")]
    public void Material_input_changes_are_denied_even_without_header_revision_change(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        var line = f.Request.Lines.First();
        switch (change)
        {
            case "description": line.Description += " changed"; break;
            case "quantity": line.RequestedQuantity = 1; break;
            case "price": line.EstimatedUnitPrice = 100; break;
            case "coding": line.CostCenterRef = "CC-OTHER"; break;
            case "tax": line.TaxCodeRef = "TAX-OTHER"; break;
            case "discount": line.DiscountPercent = 1; break;
            case "catalogVersion": line.CatalogPublishedVersion = 2; break;
            case "requestPurpose": f.Request.Purpose += " changed"; break;
        }
        Assert.Contains("SubmittedBusinessInputsChanged", f.Verify(pin).Blockers);
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("duplicate")]
    public void Pinned_policy_must_cover_exact_current_line_identity_set(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        switch (change)
        {
            case "replace": f.Request.Lines.First().Id = Guid.NewGuid(); break;
            case "add": f.Request.Lines.Add(f.NewLine(2)); break;
            case "remove": f.Request.Lines.Clear(); break;
            case "duplicate": f.Request.Lines.Add(f.Request.Lines.First()); break;
        }
        Assert.False(f.Verify(pin).IsValid);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("line")]
    [InlineData("evaluation")]
    [InlineData("policy")]
    [InlineData("finance")]
    public void Foreign_scope_is_rejected_before_pinning(string record)
    {
        var f = new Fixture();
        ProcurementRecord row = record switch
        {
            "request" => f.Request, "line" => f.Request.Lines.First(),
            "evaluation" => f.Evaluation, "policy" => f.Policy, _ => f.Finance,
        };
        row.LegalEntityId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Fact]
    public void Caller_cannot_verify_evidence_under_another_tenant_or_company()
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        Assert.Contains("PinnedScopeMismatch", PurchaseRequestApprovalPinning.Verify(pin,
            f.Request, f.Evaluation, f.Policy, f.Finance, "foreign", f.Company).Blockers);
        Assert.Contains("PinnedScopeMismatch", PurchaseRequestApprovalPinning.Verify(pin,
            f.Request, f.Evaluation, f.Policy, f.Finance, Fixture.Tenant, Guid.NewGuid()).Blockers);
    }

    [Theory]
    [InlineData("evaluation")]
    [InlineData("policy")]
    [InlineData("finance")]
    [InlineData("decision")]
    [InlineData("rules")]
    [InlineData("reservation")]
    [InlineData("submittedVersion")]
    [InlineData("case")]
    public void Replaced_references_or_mutated_evidence_never_reauthorize_a_case(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        switch (change)
        {
            case "evaluation": f.Evaluation.Id = Guid.NewGuid(); break;
            case "policy": f.Policy.Id = Guid.NewGuid(); break;
            case "finance": f.Finance.Id = Guid.NewGuid(); break;
            case "decision": f.Evaluation.DecisionJson += " "; break;
            case "rules": f.Policy.RulesJson = "{\"changed\":true}"; break;
            case "reservation": f.Finance.ReservationRef = "OTHER"; break;
            case "submittedVersion": f.Request.SubmittedVersion++; break;
            case "case": f.Request.ApprovalInstanceRef = Guid.NewGuid().ToString(); break;
        }
        Assert.False(f.Verify(pin).IsValid);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("allocated")]
    [InlineData("ordered")]
    [InlineData("accepted")]
    [InlineData("cancelled")]
    [InlineData("route")]
    [InlineData("approvalStatus")]
    public void Pending_source_cannot_hide_prior_quantity_effects_or_routing(string change)
    {
        var f = new Fixture();
        var pin = f.Pin();
        f.Link(pin);
        var line = f.Request.Lines.First();
        switch (change)
        {
            case "approved": line.ApprovedQuantity = 1; break;
            case "allocated": line.AllocatedQuantity = 1; break;
            case "ordered": line.OrderedQuantity = 1; break;
            case "accepted": line.AcceptedQuantity = 1; break;
            case "cancelled": line.CancelledQuantity = 1; break;
            case "route": line.Route = "DirectPO"; break;
            case "approvalStatus": line.ApprovalStatus = "Approved"; break;
        }
        Assert.Contains("InvalidLines", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Manual_evidence_requires_an_explicit_governance_reference_and_does_not_approve_request()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Pin(PurchaseRequestApprovalPinning.ApprovedManualEvidence));
        var pin = f.Pin(PurchaseRequestApprovalPinning.ApprovedManualEvidence, "POLICY-MANUAL-1");
        Assert.Equal("POLICY-MANUAL-1", pin.ManualEvidenceAuthorityRef);
        Assert.Equal("Submitted", f.Request.Status);
        Assert.Null(f.Request.ApprovalInstanceRef);
        Assert.Equal(0m, f.Request.Lines.First().ApprovedQuantity);
    }

    [Fact]
    public void Unlinked_or_terminal_source_is_not_a_valid_active_case()
    {
        var f = new Fixture();
        var pin = f.Pin();
        Assert.Contains("LinkedApprovalCaseMismatch", f.Verify(pin).Blockers);
        f.Link(pin);
        f.Request.Status = "Approved";
        Assert.Contains("InvalidSource", f.Verify(pin).Blockers);
    }

    [Fact]
    public void Published_policy_must_have_been_effective_at_submission()
    {
        var f = new Fixture();
        f.Policy.EffectiveFrom = f.Now.AddSeconds(1);
        Assert.Throws<ArgumentException>(() => f.Pin());
        f.Policy.EffectiveFrom = f.Now.AddDays(-1);
        f.Policy.EffectiveTo = f.Now;
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    [Theory]
    [InlineData("released")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("stale")]
    [InlineData("noEvidence")]
    public void Invalid_finance_confirmation_cannot_be_pinned(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "released": f.Finance.ReservationStatus = "Released"; break;
            case "amount": f.Finance.CheckedAmount = 249; break;
            case "currency": f.Finance.CurrencyCode = "USD"; break;
            case "stale": f.Finance.RequestVersion = 3; break;
            case "noEvidence": f.Finance.EvidenceRef = null; break;
        }
        Assert.Throws<ArgumentException>(() => f.Pin());
    }

    private sealed class Fixture
    {
        public const string Tenant = "tenant-a";
        public readonly Guid Company = Guid.NewGuid();
        public readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        public PurchaseRequest Request { get; }
        public PolicyVersion Policy { get; }
        public PolicyEvaluation Evaluation { get; }
        public BudgetCheckSnapshot Finance { get; }

        public Fixture()
        {
            Request = Scoped(new PurchaseRequest
            {
                Number = "PR-1", Status = "Submitted", Version = 4, SubmittedVersion = 4,
                SubmittedAt = Now, Purpose = "Office supplies", CurrencyCode = "ZMW",
                RequesterWorkerId = Guid.NewGuid(), ApprovalState = "PendingPolicyAndBudget",
            });
            Request.Lines.Add(NewLine(1));
            Policy = Scoped(new PolicyVersion
            {
                PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 2,
                State = "Published", EffectiveFrom = Now.AddDays(-1), RulesJson = "{}",
            });
            Evaluation = Scoped(new PolicyEvaluation
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, PolicyVersionId = Policy.Id,
                PolicyVersionNumber = 2, AmountBasis = "EstimatedLineBase", Outcome = "Matched",
            });
            RefreshDecision();
            Finance = Scoped(new BudgetCheckSnapshot
            {
                PurchaseRequestId = Request.Id, RequestVersion = 4, Status = "Available",
                CheckedAmount = 250, CurrencyCode = "ZMW", ReservationStatus = "Confirmed",
                ReservationRef = "RESERVE-1", FinanceCheckRef = "CHECK-1", EvidenceRef = "DOC-1",
                CheckedAt = Now.AddSeconds(1), CheckedBySubjectId = "finance-user",
            });
        }

        public PurchaseRequestLine NewLine(int number) => Scoped(new PurchaseRequestLine
        {
            PurchaseRequestId = Request.Id, LineNumber = number, Description = "Office item",
            UnitCode = "EA", RequestedQuantity = number == 1 ? 2 : 1,
            EstimatedUnitPrice = number == 1 ? 125 : 0, EstimatedCurrencyCode = "ZMW",
        });
        public void AddLine() { Request.Lines.Add(NewLine(2)); RefreshDecision(); }
        private void RefreshDecision() => Evaluation.DecisionJson = JsonSerializer.Serialize(new
        {
            requestId = Request.Id, requestVersion = Request.SubmittedVersion, policyId = Policy.Id,
            lines = Request.Lines.Select(x => new
            {
                lineId = x.Id, decision = new { status = "Match", approvalStages = new[]
                    { new { code = "NEED", assignee = "LineManager", mode = "One" } } },
            }),
        });
        public PurchaseRequestApprovalSubmission Pin(string mode = PurchaseRequestApprovalPinning.AutomatedFinance,
            string? authority = null) => PurchaseRequestApprovalPinning.Pin(Request, Evaluation, Policy, Finance,
                Tenant, Company, Guid.NewGuid(), Guid.NewGuid(), mode, authority, Now.AddMinutes(1));
        public void Link(PurchaseRequestApprovalSubmission pin)
        {
            Request.ApprovalInstanceRef = pin.CoreApprovalCaseId.ToString();
            Request.Version++;
        }
        public ApprovalPinValidation Verify(PurchaseRequestApprovalSubmission pin) =>
            PurchaseRequestApprovalPinning.Verify(pin, Request, Evaluation, Policy, Finance, Tenant, Company);
        private T Scoped<T>(T row) where T : ProcurementRecord
        { row.TenantId = Tenant; row.LegalEntityId = Company; row.CreatedBy = "requester-user"; return row; }
    }
}
