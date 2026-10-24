using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceIssueOwnerLookupTests
{
    [Fact]
    public void Only_current_matchers_reaching_the_issue_branch_can_be_selected()
    {
        var branch = Guid.NewGuid();
        var other = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        ProcurementPrincipalPeople.Person Person(string id, bool wide, Guid[] branches, params string[] actions)
            => new(id, null, actions.ToHashSet(StringComparer.Ordinal), wide, branches.ToHashSet());

        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(null, branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(owner, true, [], "procurement-invoice-match"), branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), true, []), branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), false, [other], "procurement-invoice-match"), branch, owner));
        Assert.True(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), false, [branch], "procurement-invoice-match"), branch, owner));
        Assert.True(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), true, [], "procurement-invoice-match"), branch, owner));
    }
}
