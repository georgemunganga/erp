using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PurchaseRequestApprovalPinningTests
{
    // Pure composition of the real Core engine with Procurement effects. Current
    // permissions, finite authority and transactional persistence require actual API/PG gates.
    [Theory]
    [InlineData("Approve","Approved")][InlineData("Reject","Rejected")][InlineData("Return","Returned")]
    public void Principal_submission_uses_existing_Core_engine_and_exact_terminal_effect_replay(string action,string terminal)
    {
        var f=PrincipalFixture();var pin=PrincipalPin(f);f.Link(pin);
        var actor=Guid.NewGuid().ToString("D");
        var workflow=new ApprovalCase {Id=pin.CoreApprovalCaseId,TenantId=Fixture.Tenant,LegalEntityId=f.Company,
            SourceModule="procurement",SourceType="purchase_request",SourceId=f.Request.Id,
            SourceVersion=pin.SubmittedBusinessVersion,PolicyVersionId=pin.PolicyVersionId,PolicyVersionNumber=pin.PolicyVersionNumber,
            MakerSubjectId=pin.MakerSubjectId,OpenedAt=pin.PinnedAt};
        var stage=new ApprovalStage {TenantId=Fixture.Tenant,LegalEntityId=f.Company,CaseId=workflow.Id,
            Sequence=1,Code="NEED",Mode="Any",RequiredApprovals=1,State="Active"};
        var assignment=new ApprovalAssignment {TenantId=Fixture.Tenant,LegalEntityId=f.Company,CaseId=workflow.Id,
            StageId=stage.Id,AssigneeSubjectId=actor,PermissionKey="procurement-approval-decide"};
        stage.Assignments.Add(assignment);workflow.Stages.Add(stage);ApprovalEngine.ValidateNewCase(workflow);
        var decisions=new List<ApprovalDecision>();var key=Guid.NewGuid().ToString("D");var now=pin.PinnedAt.AddMinutes(1);
        var result=ApprovalEngine.Decide(workflow,assignment,new(actor,Fixture.Tenant,f.Company,
            new HashSet<string>{"procurement-approval-decide"}),action,key,action=="Approve"?null:"Synthetic independent decision",decisions,now);
        Assert.True(result.Recorded);Assert.True(result.CaseCompleted);Assert.Equal(terminal,workflow.State);
        var decision=Assert.Single(decisions);
        var effects=PurchaseRequestApprovalOutcomes.Apply(f.Request,pin,workflow,decision,f.Evaluation,f.Policy,f.Finance,Fixture.Tenant,f.Company,now);
        Assert.False(effects.IsReplay);Assert.Equal(terminal,effects.Outcome.Outcome);
        if(action=="Approve")
        {
            Assert.Equal("Approved",f.Request.Status);Assert.Null(effects.ReleaseRequest);
            Assert.All(f.Request.Lines,x=>Assert.Equal(x.RequestedQuantity,x.ApprovedQuantity));
        }
        else
        {
            Assert.NotNull(effects.ReleaseRequest);Assert.Equal(f.Finance.ReservationRef,effects.ReleaseRequest.ReservationRef);
            Assert.All(f.Request.Lines,x=>Assert.Equal(0,x.ApprovedQuantity));
        }
        var replay=PurchaseRequestApprovalOutcomes.Apply(f.Request,pin,workflow,decision,f.Evaluation,f.Policy,f.Finance,
            Fixture.Tenant,f.Company,now,effects.Outcome,effects.ReleaseRequest);
        Assert.True(replay.IsReplay);Assert.Empty(replay.Events);
        Assert.True(PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(pin));
        Assert.Null(pin.MakerWorkerId);Assert.Null(pin.RequesterWorkerId);
    }
}
