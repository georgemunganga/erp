namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Bind a server-authorized target branch for retained source loading.
/// Structural projection check only; the caller MUST resolve current Core action/
/// branches first. This does not grant All/organisation-wide access.</summary>
public static class PrincipalRfqTargetBranchScope
{
    public static IProcurementScope Bind(IProcurementScope scope,RfqPrincipalActor currentActor,Guid branch)
    {
        ArgumentNullException.ThrowIfNull(scope);ArgumentNullException.ThrowIfNull(currentActor);
        if(branch==Guid.Empty||scope.OrgUnitId is not null||currentActor.TenantId!=scope.TenantId
            ||currentActor.CompanyId!=scope.LegalEntityId||currentActor.PrincipalId!=scope.SubjectId
            ||scope.WorkLocationId is {} selected&&selected!=branch
            ||currentActor.SelectedBranchId is {} actorSelected&&actorSelected!=branch
            ||scope.IsConfined&&scope.WorkLocationId is null&&!scope.IsExplicitAllBranches
            ||!currentActor.OrganisationWide&&!currentActor.Branches.Contains(branch))
            throw new UnauthorizedAccessException("RfqCommercialTargetBranchDenied");
        return new Bound(scope.TenantId,scope.LegalEntityId,scope.SubjectId,branch,scope.WorkerId,scope.CoreLinkedWorkerId,scope.CorrelationId);
    }
    private sealed record Bound(string TenantId,Guid LegalEntityId,string SubjectId,Guid Branch,Guid? WorkerId,
        Guid? CoreLinkedWorkerId,string? CorrelationId):IProcurementScope
    {public Guid? WorkLocationId=>Branch;public Guid? OrgUnitId=>null;public bool IsConfined=>true;public bool IsExplicitAllBranches=>false;}
}
