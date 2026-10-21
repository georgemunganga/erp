using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ProcurementSpendingActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Company = Guid.NewGuid();

    [Fact]
    public void PR_and_PO_grants_do_not_authorize_each_other_even_with_identical_amount_and_scope()
    {
        var pr = Grant(ProcurementSpendingActions.PurchaseRequest);
        var po = Grant(ProcurementSpendingActions.PurchaseOrder);
        Assert.True(Check(ProcurementSpendingActions.PurchaseRequest, pr).Eligible);
        Assert.True(Check(ProcurementSpendingActions.PurchaseOrder, po).Eligible);
        Assert.False(Check(ProcurementSpendingActions.PurchaseRequest, po).Eligible);
        Assert.False(Check(ProcurementSpendingActions.PurchaseOrder, pr).Eligible);
        var wrong = new ProcurementSpendContext(Guid.NewGuid(), "tenant", Company, "ZMW", "EstimatedLineBase", 100,
            null, [null], ["OFFICE"]);
        Assert.False(ProcurementSpendingAuthority.Evaluate(wrong, "approver", "procurement-po-approve",
            "PurchaseOrder.Approve", Now, [po]).Eligible);
    }

    [Fact]
    public void Multiple_source_departments_require_a_grant_that_covers_all_of_them()
    {
        var po = Grant(ProcurementSpendingActions.PurchaseOrder);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var context = new ProcurementSpendContext(Guid.NewGuid(), "tenant", Company, "ZMW", "ApprovedLineBase", 100,
            null, [first, second], ["OFFICE"]);
        po.AllDepartments = false; po.DepartmentRef = first;
        Assert.False(ProcurementSpendingAuthority.Evaluate(context, "approver", po.Permission, po.Action, Now, [po]).Eligible);
        po.AllDepartments = true; po.DepartmentRef = null;
        Assert.True(ProcurementSpendingAuthority.Evaluate(context, "approver", po.Permission, po.Action, Now, [po]).Eligible);
    }

    [Fact]
    public void Existing_create_signature_keeps_PR_action_and_financial_basis()
    {
        var maker = Actor("maker", SpendingAuthorityConfiguration.ConfigurePermission);
        var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH", "approver", Draft("EstimatedLineBase"), maker, Now, []);
        Assert.Equal("PurchaseRequest.Approve", grant.Action);
        Assert.Equal("procurement-approval-decide", grant.Permission);
        Assert.Equal("EstimatedLineBase", grant.AmountBasis);
        SpendingAuthorityConfiguration.Submit(grant, grant.Version, maker,
            new("tenant", Company, "approver", true, true), Now);
        Assert.Equal("PendingReview", grant.State);
    }

    [Theory]
    [InlineData("PurchaseRequest.Approve", "ApprovedLineBase")]
    [InlineData("PurchaseOrder.Approve", "EstimatedLineBase")]
    public void Cross_transaction_basis_cannot_be_published(string action, string basis)
    {
        var maker = Actor("maker", SpendingAuthorityConfiguration.ConfigurePermission);
        var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH", "approver", Draft(basis), maker, Now, [], action);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Submit(grant, grant.Version,
            maker, new("tenant", Company, "approver", true, true), Now));
        Assert.Equal("Draft", grant.State);
    }

    private static ProcurementSpendingAuthorityResult Check(ProcurementSpendingAction action,
        ProcurementSpendingAuthorityGrant grant) => ProcurementSpendingAuthority.Evaluate(
            new(Guid.NewGuid(), "tenant", Company, "ZMW", action.AmountBasis, 100, null, [null], ["OFFICE"]),
            "approver", action.Permission, action.Action, Now, [grant]);
    private static SpendingAuthorityDraft Draft(string basis) => new("ZMW", basis, 100, Now.AddDays(-1),
        AllBranches: true, AllDepartments: true, AllCategories: true);
    private static SpendingAuthorityConfigurationActor Actor(string id, string permission) =>
        new("tenant", Company, id, true, new HashSet<string> { permission });
    private static ProcurementSpendingAuthorityGrant Grant(ProcurementSpendingAction action) => new()
    {
        TenantId = "tenant", LegalEntityId = Company, PrincipalId = "approver", AuthorityReference = "TEST-" + action.Action,
        AuthorityVersion = 1, Permission = action.Permission, Action = action.Action, AmountBasis = action.AmountBasis,
        State = "Active", CurrencyCode = "ZMW", MaximumAmount = 100, EffectiveFrom = Now.AddDays(-1),
        AllBranches = true, AllDepartments = true, AllCategories = true,
    };
}
