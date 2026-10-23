using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Procurement.Tests;

// Server projection contract tests, not actual Core account/HTTP authorization.
public sealed class PrincipalRfqTargetBranchScopeTests
{
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId,bool IsExplicitAllBranches=true,
        Guid? WorkLocationId=null,Guid? OrgUnitId=null):IProcurementScope
    {public bool IsConfined=>true;public string? CorrelationId=>null;}
    [Fact]
    public void Explicit_All_assigned_A_B_can_bind_A_without_granting_unassigned_C()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();var scope=new Scope("tenant",Guid.NewGuid(),Guid.NewGuid().ToString("D"));
        var actor=new RfqPrincipalActor(scope.SubjectId,null,scope.TenantId,scope.LegalEntityId,null,new HashSet<Guid>{a,b},false,new HashSet<string>());
        var target=PrincipalRfqTargetBranchScope.Bind(scope,actor,a);
        Assert.Equal(a,target.WorkLocationId);Assert.True(target.IsConfined);Assert.False(target.IsExplicitAllBranches);
        Assert.Equal(scope.SubjectId,target.SubjectId);Assert.Null(target.WorkerId);
        Assert.Throws<UnauthorizedAccessException>(()=>PrincipalRfqTargetBranchScope.Bind(scope,actor,c));
        Assert.Throws<UnauthorizedAccessException>(()=>PrincipalRfqTargetBranchScope.Bind(scope with{IsExplicitAllBranches=false},actor,a));
        Assert.Throws<UnauthorizedAccessException>(()=>PrincipalRfqTargetBranchScope.Bind(scope with{OrgUnitId=Guid.NewGuid()},actor,a));
        Assert.Throws<UnauthorizedAccessException>(()=>PrincipalRfqTargetBranchScope.Bind(scope with{WorkLocationId=b},actor,a));
    }
}
