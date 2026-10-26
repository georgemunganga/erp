using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M4PrincipalPublicationReadContractTests
{
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
