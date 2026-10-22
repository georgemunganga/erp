using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCoreLinkedWorkerScopeTests
{
    [Fact]
    public void Optional_Core_link_binds_once_without_asserting_active_worker_or_authority()
    {
        var principal = Guid.NewGuid(); var linked = Guid.NewGuid();
        var scope = new ProcurementRequestScope();
        Assert.Throws<InvalidOperationException>(() => scope.AssignPrincipal(principal, linked));
        scope.Set("tenant-a", Guid.NewGuid(), principal.ToString("D"), null, null, false, "known-alias");
        scope.AssignPrincipal(principal, linked);
        Assert.Equal(principal, scope.PrincipalId); Assert.Equal(linked, scope.CoreLinkedWorkerId);
        Assert.Null(scope.WorkerId); Assert.False(scope.AdminViewOnly);
        Assert.Throws<InvalidOperationException>(() => scope.AssignPrincipal(principal, Guid.NewGuid()));
        Assert.Equal(linked, scope.CoreLinkedWorkerId);
    }

    [Fact]
    public void Empty_optional_Core_link_is_normalized_to_absence()
    {
        var principal = Guid.NewGuid(); var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", Guid.NewGuid(), principal.ToString("D"), null, null, false, "empty-alias");
        scope.AssignPrincipal(principal, Guid.Empty);
        Assert.Null(scope.CoreLinkedWorkerId); Assert.Null(scope.WorkerId);
        Assert.Throws<InvalidOperationException>(() => scope.AssignPrincipal(principal, Guid.NewGuid()));
    }

    [Fact]
    public void Different_subject_cannot_bind_a_Core_principal_or_its_optional_link()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "wrong-principal");
        Assert.Throws<InvalidOperationException>(() => scope.AssignPrincipal(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Null(scope.PrincipalId); Assert.Null(scope.CoreLinkedWorkerId); Assert.Null(scope.WorkerId);
    }
}
