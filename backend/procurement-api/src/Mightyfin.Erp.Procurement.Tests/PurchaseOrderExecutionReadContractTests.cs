using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseOrderExecutionReadContractTests
{
    private static readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid();
    private static PurchaseOrderExecutionReadActor Actor(params string[] actions) => new("tenant", Company,
        "actor", Guid.NewGuid(), true, Branch, new HashSet<Guid> { Branch }, new HashSet<string>(actions, StringComparer.Ordinal));
    private static PurchaseOrderExecutionReadFacts Facts(string status = "Draft") => new()
    { TenantId = "tenant", LegalEntityId = Company, BranchId = Branch, BuyerPrincipalId = "buyer", Status = status, SourceControlled = true };

    [Fact]
    public void Finance_command_grant_implies_only_purpose_specific_scoped_read_without_global_po_read()
    {
        var actor = Actor("procurement-po-finance-confirm");
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, Facts()));
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("Returned")));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("Approved")));
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("Approved") with { RecordedFinanceActor = true }));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts() with { SourceControlled = false }));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(Actor("procurement-po-read"), Facts()));
    }

    [Fact]
    public void Dispatch_purpose_read_requires_an_issued_version_and_does_not_expose_unissued_drafts()
    {
        var actor = Actor("procurement-po-dispatch-confirm");
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts()));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("IssuePending")));
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("IssuePending") with { HasIssuedVersion = true }));
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("Issued") with { HasIssuedVersion = true }));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts("Approved") with { HasIssuedVersion = true }));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("selected-branch")]
    [InlineData("inactive")]
    [InlineData("worker")]
    public void Purpose_read_never_overrides_current_identity_company_or_selected_branch(string mismatch)
    {
        var actor = Actor("procurement-po-finance-confirm");
        actor = mismatch switch
        {
            "tenant" => actor with { TenantId = "foreign" },
            "company" => actor with { LegalEntityId = Guid.NewGuid() },
            "branch" => actor with { BranchIds = new HashSet<Guid> { Guid.NewGuid() } },
            "selected-branch" => actor with { SelectedBranchId = Guid.NewGuid(), BranchIds = new HashSet<Guid>() },
            "inactive" => actor with { Active = false },
            _ => actor with { WorkerId = Guid.Empty },
        };
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(actor, Facts()));
        Assert.Empty(PurchaseOrderExecutionReadContract.Actions(actor, Facts()));
    }

    [Fact]
    public void Buyer_stays_own_only_and_approver_stays_current_permission_and_assignment_only()
    {
        var read = Actor("procurement-po-read");
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(read with { PrincipalId = "buyer" }, Facts()));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(read, Facts()));
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(Actor("procurement-po-approve"), Facts() with { AssignedToActor = true }));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(Actor("procurement-po-approve"), Facts()));
        Assert.False(PurchaseOrderExecutionReadContract.CanRead(Actor(), Facts() with { AssignedToActor = true }));
    }

    [Fact]
    public void Reading_a_conflicted_finance_record_does_not_confer_command_eligibility()
    {
        var actor = Actor("procurement-po-finance-confirm");
        var facts = Facts() with { CurrentFinancePolicy = true, FinanceIndependent = false };
        Assert.True(PurchaseOrderExecutionReadContract.CanRead(actor, facts));
        var action = PurchaseOrderExecutionReadContract.Actions(actor, facts).Single(x => x.Code == "Finance");
        Assert.False(action.CanStart); Assert.NotEmpty(action.Blockers); Assert.True(action.RequiresCommandValidation);
        Assert.True(PurchaseOrderExecutionReadContract.Actions(actor, facts with { FinanceIndependent = true })
            .Single(x => x.Code == "Finance").CanStart);
        Assert.False(PurchaseOrderExecutionReadContract.Actions(actor, facts with { FinanceIndependent = true, CurrentFinancePolicy = false })
            .Single(x => x.Code == "Finance").CanStart);
    }

    [Fact]
    public void Held_or_expired_but_unreleased_commitment_never_becomes_an_editable_or_cancelable_draft()
    {
        var actor = Actor("procurement-po-read", "procurement-po-edit", "procurement-po-submit") with { PrincipalId = "buyer" };
        var facts = Facts() with { FinanceHeld = true, CurrentFinanceEvidence = false };
        var actions = PurchaseOrderExecutionReadContract.Actions(actor, facts);
        Assert.False(actions.Single(x => x.Code == "SourceEdit").CanStart);
        Assert.False(actions.Single(x => x.Code == "CancelDraft").CanStart);
        Assert.False(actions.Single(x => x.Code == "Submit").CanStart);
        Assert.False(PurchaseOrderExecutionReadContract.Actions(actor, facts with { FinanceHeld = false, HasSubmission = true })
            .Single(x => x.Code == "SourceEdit").CanStart);
        Assert.True(PurchaseOrderExecutionReadContract.Actions(actor, facts with { FinanceHeld = false, HasSubmission = true, DraftReopened = true })
            .Single(x => x.Code == "SourceEdit").CanStart);
    }

    [Fact]
    public void Release_and_delivery_candidates_preserve_stronger_independence_and_are_not_approval()
    {
        var finance = Actor("procurement-po-finance-confirm");
        var facts = Facts("Returned") with { FinanceHeld = true, CurrentFinancePolicy = true, FinanceIndependent = true };
        Assert.False(PurchaseOrderExecutionReadContract.Actions(finance, facts).Single(x => x.Code == "FinanceRelease").CanStart);
        Assert.True(PurchaseOrderExecutionReadContract.Actions(finance, facts with { FinanceReleaseIndependent = true })
            .Single(x => x.Code == "FinanceRelease").CanStart);
        var dispatch = Actor("procurement-po-dispatch-confirm");
        var pending = Facts("IssuePending") with { HasIssuedVersion = true, CurrentDispatchPolicy = true };
        Assert.False(PurchaseOrderExecutionReadContract.Actions(dispatch, pending).Single(x => x.Code == "Delivery").CanStart);
        Assert.True(PurchaseOrderExecutionReadContract.Actions(dispatch, pending with { DispatchIndependent = true })
            .Single(x => x.Code == "Delivery").CanStart);
        Assert.All(PurchaseOrderExecutionReadContract.Actions(dispatch, pending), x => Assert.True(x.RequiresCommandValidation));
    }

    [Fact]
    public void Read_amounts_use_exact_strings_above_js_integer_precision_and_keep_eight_fractional_digits()
    {
        var amounts = PurchaseOrderExecutionReadAmounts.Calculate(new[] { new PurchaseOrderLine
            { Quantity = 9007199254.7409m, UnitPrice = 1.0001m, TaxAmount = 0.0001m } });
        Assert.Equal("9008099974.66637409", amounts.LineBase);
        Assert.Equal("9008099974.66647409", amounts.Gross);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderExecutionReadAmounts.Calculate(new[] {
            new PurchaseOrderLine { Quantity = 99999999999999.9999m, UnitPrice = 99999999999999.9999m } }));
    }

    [Fact]
    public void Recovery_operation_catalog_is_exact_and_never_substitutes_read_permission_for_mutation_permission()
    {
        Assert.Equal("procurement-po-finance-confirm", PurchaseOrderExecutionReadContract.OperationPermissions["Finance"]);
        Assert.Equal("procurement-po-approve", PurchaseOrderExecutionReadContract.OperationPermissions["Decide"]);
        Assert.False(PurchaseOrderExecutionReadContract.OperationPermissions.ContainsKey("finance"));
        Assert.False(PurchaseOrderExecutionReadContract.OperationPermissions.ContainsKey("Unknown"));
        Assert.DoesNotContain(PurchaseOrderExecutionReadContract.OperationPermissions.Values, x => x == "procurement-po-read");
    }
}
