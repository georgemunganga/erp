using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M4PrincipalPublicationReadContractTests
{
    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void Publication_new_work_capability_uses_current_action_and_physical_readiness(
        bool actorCanRead, bool physicalSourceReady, bool enabled, bool approvalStoreReady, bool expected)
    {
        // The first case represents a publish-only assigned reviewer. Its own
        // convert/cancel capability is false, but no buyer action is needed.
        Assert.Equal(expected, ProcurementPrincipalSourcingPublicationCapability.NewWork(
            actorCanRead, physicalSourceReady, enabled, approvalStoreReady));
    }

    [Fact]
    public void Actionable_assignments_exclude_waiting_stage_and_historical_assignments_for_the_same_reviewer()
    {
        var reviewer = Guid.NewGuid().ToString("D");
        var active = new ApprovalAssignment { AssigneeSubjectId = reviewer,
            PermissionKey = "procurement-sourcing-publish", State = "Pending" };
        var waiting = new ApprovalAssignment { AssigneeSubjectId = reviewer,
            PermissionKey = "procurement-sourcing-publish", State = "Pending" };
        var historical = new ApprovalAssignment { AssigneeSubjectId = reviewer,
            PermissionKey = "procurement-sourcing-publish", State = "Approved" };
        var approval = new ApprovalCase { Stages = [
            new ApprovalStage { State = "Active", Assignments = [active, historical] },
            new ApprovalStage { State = "Waiting", Assignments = [waiting] }
        ] };
        Assert.Equal(active.Id, Assert.Single(
            ProcurementSourcingExecutionRoutes.ActivePublicationAssignments(approval, reviewer)).Id);
        approval.Stages.Single(x => x.State == "Active").State = "Complete";
        Assert.Empty(ProcurementSourcingExecutionRoutes.ActivePublicationAssignments(approval, reviewer));
    }
}
