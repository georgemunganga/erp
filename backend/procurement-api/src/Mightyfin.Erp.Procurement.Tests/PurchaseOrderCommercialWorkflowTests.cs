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
            "Unknown", "SecureEmail", "SYNTHETIC-EVIDENCE", "delivery-operator", f.Now.AddMinutes(2), f.Now.AddMinutes(2));
        Assert.Equal("Unknown", delivery.Outcome); Assert.Equal("IssuePending", f.Source.Order.Status);
        _ = PurchaseOrderCommercialWorkflow.ConfirmDelivery(f.Source.Order, issued, "DELIVERY-2", "Delivered",
            "SecureEmail", "SYNTHETIC-EVIDENCE-2", "delivery-operator", f.Now.AddMinutes(3), f.Now.AddMinutes(3));
        Assert.Equal("Issued", f.Source.Order.Status);
        Assert.Equal(pin.CommercialSnapshotSha256, issued.CommercialSnapshotSha256);
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
            f.Lines, f.Supplier, f.Policy, f.MakeFinance(9), Guid.NewGuid(), f.Now));
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderCommercialWorkflow.Submit(f.Source.Order,
            f.Lines, f.Supplier, f.Policy, f.MakeFinance(11), Guid.NewGuid(), f.Now));
        Assert.Equal("Draft", f.Source.Order.Status);
    }

    private sealed class Fixture
    {
        public PurchaseOrderSourceAllocationTests.Fixture Source { get; } = new();
        public DateTimeOffset Now => Source.Now.AddMinutes(2);
        public IReadOnlyList<PurchaseOrderSourceLine> Lines { get; }
        public Supplier Supplier { get; }
        public PolicyVersion Policy { get; }
        public PurchaseOrderFinanceEvidence Finance { get; }
        public ApprovalCase? Case { get; private set; }
        public Fixture()
        {
            var allocation = Source.Reserve(1);
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
            VerifiedByPrincipalId = "independent-finance", ConfirmedAt = Now,
        };
        public PurchaseOrderSubmission Submit()
        {
            var id = Guid.NewGuid();
            var pin = PurchaseOrderCommercialWorkflow.Submit(Source.Order, Lines, Supplier, Policy, Finance, id, Now);
            Source.Order.Version++; // persistence advances the operational row revision.
            Case = new() { Id = id, TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                SourceModule = "procurement", SourceType = "purchase_order", SourceId = Source.Order.Id,
                SourceVersion = 1, PolicyVersionId = Policy.Id, PolicyVersionNumber = 1, MakerSubjectId = "buyer" };
            return pin;
        }
        public void Approve(PurchaseOrderSubmission pin)
        {
            var stage = new ApprovalStage { TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                CaseId = Case!.Id, Sequence = 1, Code = "COMMERCIAL", Mode = "Any", RequiredApprovals = 1, State = "Active" };
            var assignment = new ApprovalAssignment { TenantId = Supplier.TenantId, LegalEntityId = Supplier.LegalEntityId,
                CaseId = Case.Id, StageId = stage.Id, AssigneeSubjectId = "independent-po-approver", PermissionKey = "procurement-po-approve" };
            stage.Assignments.Add(assignment); Case.Stages.Add(stage);
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(Case, assignment, new("independent-po-approver", Supplier.TenantId, Supplier.LegalEntityId,
                new HashSet<string> { "procurement-po-approve" }), "Approve", Guid.NewGuid().ToString("D"), null, decisions, Now);
            PurchaseOrderCommercialWorkflow.ApplyDecision(Source.Order, pin, Case, decisions.Single(), null);
            Source.Order.Version++;
        }
    }
}
