using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;
public sealed class ReceivingIssuedAuthorityTests
{
    [Fact]
    public void Exact_current_obligation_survives_supplier_profile_and_historical_policy_expiry()
    {
        var f = new Fixture(); f.Supplier.LegalName = "Current renamed supplier"; f.Supplier.Status = "Suspended";
        f.Policy.EffectiveTo = DateTimeOffset.UtcNow.AddHours(-1); // Still effective at the retained submission.
        f.Verify();
    }
    [Theory]
    [InlineData("branch")] [InlineData("currency")] [InlineData("supplier")] [InlineData("buyer")]
    [InlineData("bill")] [InlineData("ship")] [InlineData("terms")] [InlineData("source-case")]
    [InlineData("submitted-version")] [InlineData("case-version")] [InlineData("case-policy-id")]
    [InlineData("case-policy-number")] [InlineData("original-policy-rules")] [InlineData("line-price")]
    [InlineData("line-description")] [InlineData("line-inactive")] [InlineData("foreign-case")]
    public void Mutable_current_header_line_or_Core_pointer_cannot_hide_behind_an_old_valid_pin(string changed)
    {
        var f = new Fixture();
        switch (changed)
        {
            case "branch": f.Order.BranchRef = Guid.NewGuid(); break;
            case "currency": f.Order.CurrencyCode = "USD"; break;
            case "supplier": f.Order.SupplierId = Guid.NewGuid(); break;
            case "buyer": f.Order.BuyerSubjectId = "another-buyer"; break;
            case "bill": f.Order.BillToRef = "changed"; break;
            case "ship": f.Order.ShipToRef = "changed"; break;
            case "terms": f.Order.PaymentTermRef = "changed"; break;
            case "source-case": f.Order.ApprovalInstanceRef = Guid.NewGuid().ToString("D"); break;
            case "submitted-version": f.Order.SubmittedBusinessVersion = 2; break;
            case "case-version": f.Case.SourceVersion = 2; break;
            case "case-policy-id": f.Case.PolicyVersionId = Guid.NewGuid(); break;
            case "case-policy-number": f.Case.PolicyVersionNumber = 2; break;
            case "original-policy-rules": f.Policy.RulesJson = "{\"changed\":true}"; break;
            case "line-price": f.Line.UnitPrice = 101; break;
            case "line-description": f.Line.Description = "Replacement description"; break;
            case "line-inactive": f.Line.IsActive = false; break;
            case "foreign-case": f.Case.TenantId = "another-tenant"; break;
        }
        Assert.Throws<InvalidOperationException>(f.Verify);
    }
    private sealed class Fixture
    {
        public PurchaseOrder Order { get; }
        public PurchaseOrderLine Line { get; }
        public Supplier Supplier { get; }
        public PolicyVersion Policy { get; }
        public ApprovalCase Case { get; }
        public PurchaseOrderSubmission Submission { get; }
        public PurchaseOrderIssuedVersion Issued { get; }
        public Fixture()
        {
            var company = Guid.NewGuid(); var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow.AddHours(-3))!.Value;
            Policy = new() { TenantId = "test", LegalEntityId = company, PolicyKey = "PURCHASE_ORDER", PolicyVersionNumber = 1,
                State = "Published", EffectiveFrom = at.AddDays(-1), RulesJson = "{\"retained\":\"published-rule-fixture\"}" };
            Case = new() { TenantId = "test", LegalEntityId = company, State = "Approved", SourceModule = "procurement", SourceType = "purchase_order",
                SourceVersion = 1, PolicyVersionId = Policy.Id, PolicyVersionNumber = 1, MakerSubjectId = "buyer", CompletedAt = at.AddHours(1) };
            Order = new() { TenantId = "test", LegalEntityId = company, Status = "Issued", Number = "PO-TEST", CurrentRevision = 1,
                SubmittedBusinessVersion = 1, SubmittedAt = at, ApprovalInstanceRef = Case.Id.ToString("D"), BranchRef = Guid.NewGuid(), CurrencyCode = "ZMW",
                SupplierId = Guid.NewGuid(), BuyerSubjectId = "buyer", BillToRef = "BILL", ShipToRef = "SHIP", PaymentTermRef = "TERMS" };
            Case.SourceId = Order.Id;
            Line = new() { TenantId = "test", LegalEntityId = company, PurchaseOrderId = Order.Id, IsActive = true,
                PurchaseRequestLineId = Guid.NewGuid(), LineNumber = 1, Description = "Original description", UnitCode = "EA", Quantity = 1, UnitPrice = 100 };
            Supplier = new() { Id = Order.SupplierId, LegalName = "Retained legal name", DisplayName = "Retained display name", CountryCode = "ZM" };
            var snapshot = PurchaseOrderCommercialWorkflow.Snapshot(Order, [Line], Supplier, 1);
            Submission = new() { TenantId = "test", LegalEntityId = company, PurchaseOrderId = Order.Id, CoreApprovalCaseId = Case.Id,
                CommercialRevision = 1, SubmittedAt = at, BuyerPrincipalId = "buyer", CurrencyCode = "ZMW", PolicyVersionId = Policy.Id,
                PolicyVersionNumber = 1, PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(Policy.RulesJson),
                CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = ProcurementEvidenceEncoding.Digest(snapshot) };
            Issued = new() { TenantId = "test", LegalEntityId = company, PurchaseOrderId = Order.Id, SubmissionId = Submission.Id, RevisionNumber = 1,
                CommercialSnapshotJson = snapshot, CommercialSnapshotSha256 = Submission.CommercialSnapshotSha256, AuthorizedAt = at.AddHours(2) };
        }
        public void Verify() => ReceivingIssuedAuthority.Verify(Order, [Line], Issued, Submission, Case, Policy);
    }
}
