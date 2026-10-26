using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderGovernanceContextTests
{
    [Theory]
    [InlineData(true,false,false,false)]
    [InlineData(true,false,true,true)]
    [InlineData(true,true,false,true)]
    [InlineData(true,true,true,false)]
    [InlineData(false,false,false,false)]
    [InlineData(false,false,true,false)]
    [InlineData(false,true,false,true)]
    [InlineData(false,true,true,false)]
    public void All_context_is_read_only_and_never_substitutes_for_a_transaction_branch(
        bool read,bool hasBranch,bool all,bool allowed)
    {
        Guid? branch=hasBranch?Guid.NewGuid():null;
        Assert.Equal(allowed,ProcurementPrincipalOrderDraftRoutes.GovernedContextAllowed(read,branch,all));
        Assert.False(ProcurementPrincipalOrderDraftRoutes.GovernedContextAllowed(read,Guid.Empty,all));
    }
}
