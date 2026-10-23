using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderFundingScopeTests
{
    [Fact]
    public void Retained_finance_recorder_loses_order_authority_when_current_branch_grant_is_revoked()
    {
        var orderBranch=Guid.NewGuid();
        var recorder=new ProcurementPrincipalPeople.Person(Guid.NewGuid().ToString("D"),null,
            new HashSet<string>{"procurement-access","procurement-po-finance-confirm"},false,
            new HashSet<Guid>{orderBranch});
        Assert.True(ProcurementPrincipalOrderDraftRoutes.RecorderCanActInBranch(recorder,orderBranch));
        var afterRevocation=recorder with { AllowedBranches=new HashSet<Guid>{Guid.NewGuid()} };
        Assert.False(ProcurementPrincipalOrderDraftRoutes.RecorderCanActInBranch(afterRevocation,orderBranch));
        Assert.False(ProcurementPrincipalOrderDraftRoutes.RecorderCanActInBranch(afterRevocation,null));
        Assert.True(ProcurementPrincipalOrderDraftRoutes.RecorderCanActInBranch(
            recorder with { OrganisationWide=true,AllowedBranches=new HashSet<Guid>() },orderBranch));
    }
}
