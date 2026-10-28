using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementBaselineReviewPolicyRulesTests
{
    [Fact]
    public void Baseline_Core_approve_requires_current_exact_policy_but_expired_policy_allows_Return()
    {
        var company=Guid.NewGuid();var maker=Guid.NewGuid().ToString("D");
        var reviewerId=Guid.NewGuid().ToString("D");
        var at=new DateTimeOffset(2026,10,28,16,0,0,TimeSpan.Zero);
        var policy=new PolicyVersion
        {
            TenantId="t",LegalEntityId=company,PolicyKey=InvoiceProcurementBaselineReviewPolicyRules.PolicyKey,
            PolicyVersionNumber=1,State="Published",EffectiveFrom=at.AddMinutes(-2),
            EffectiveTo=at.AddMinutes(2),PublishedAt=at.AddMinutes(-3),
            CreatedBy=Guid.NewGuid().ToString("D"),PublishedBy=Guid.NewGuid().ToString("D"),
            PublicationReason="Independent",RulesJson=JsonSerializer.Serialize(new
            {schemaVersion=1,transactionType=InvoiceProcurementBaselineReviewPolicyRules.TransactionType,
                reviewerPrincipalId=reviewerId})
        };
        var person=new InvoiceProcurementBaselineReviewPolicyRules.Person("t",company,reviewerId,
            null,true,true,new HashSet<string>{"procurement-access",
                InvoiceProcurementBaselineEvidence.ReviewPermission});
        (InvoiceProcurementOwnerBaseline Baseline,ApprovalCase Case,ApprovalAssignment Assignment) Open()
        {
            var baselineId=Guid.NewGuid();var caseId=Guid.NewGuid();
            var workflow=InvoiceProcurementBaselineOpeningPlan.Prepare(baselineId,caseId,policy,
                "t",company,maker,null,person,at);
            return (new InvoiceProcurementOwnerBaseline
            {
                Id=baselineId,TenantId="t",LegalEntityId=company,CoreCaseId=caseId,
                MakerPrincipalId=maker,CapturedAtUtc=workflow.OpenedAt
            },workflow,Assert.Single(Assert.Single(workflow.Stages).Assignments));
        }
        var aliases=new Dictionary<string,Guid?>{[reviewerId]=null};
        var valid=Open();var votes=new List<ApprovalDecision>();
        _=InvoiceProcurementBaselineDecision.Apply(valid.Baseline,valid.Case,valid.Assignment,
            aliases,person,policy,new(Guid.NewGuid(),valid.Case.Version,valid.Assignment.Version,
                "Approve",null,at.AddMinutes(1)),votes);
        Assert.Equal("Approved",valid.Case.State);
        Assert.Single(votes);
        var stale=Open();
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineDecision.Apply(
            stale.Baseline,stale.Case,stale.Assignment,aliases,person,policy,
            new(Guid.NewGuid(),stale.Case.Version,stale.Assignment.Version,"Approve",null,at.AddMinutes(3)),
            new List<ApprovalDecision>()));
        var recovery=Open();var returned=new List<ApprovalDecision>();
        _=InvoiceProcurementBaselineDecision.Apply(recovery.Baseline,recovery.Case,recovery.Assignment,
            aliases,person,null,new(Guid.NewGuid(),recovery.Case.Version,recovery.Assignment.Version,
                "Return","Refresh history",at.AddMinutes(3)),returned);
        Assert.Equal("Returned",recovery.Case.State);
        Assert.Single(returned);
    }

    [Fact]
    public void Published_one_reviewer_policy_accepts_workerless_independent_company_principal_only()
    {
        var company=Guid.NewGuid();var maker=Guid.NewGuid().ToString("D");
        var reviewer=Guid.NewGuid().ToString("D");
        var at=new DateTimeOffset(2026,10,28,16,0,0,TimeSpan.Zero);
        var json=JsonSerializer.Serialize(new{schemaVersion=1,
            transactionType=InvoiceProcurementBaselineReviewPolicyRules.TransactionType,
            reviewerPrincipalId=reviewer});
        var policy=new PolicyVersion
        {
            TenantId="t",LegalEntityId=company,
            PolicyKey=InvoiceProcurementBaselineReviewPolicyRules.PolicyKey,
            PolicyVersionNumber=1,State="Published",RulesJson=json,
            CreatedBy=Guid.NewGuid().ToString("D"),PublishedBy=Guid.NewGuid().ToString("D"),
            PublicationReason="Independent baseline review",EffectiveFrom=at.AddMinutes(-5),
            PublishedAt=at.AddMinutes(-10)
        };
        var person=new InvoiceProcurementBaselineReviewPolicyRules.Person("t",company,reviewer,
            null,true,true,new HashSet<string>{"procurement-access",
                InvoiceProcurementBaselineEvidence.ReviewPermission});
        Assert.Empty(InvoiceProcurementBaselineReviewPolicyRules.Validate(json));
        Assert.Equal(person,InvoiceProcurementBaselineReviewPolicyRules.Resolve(
            policy,"t",company,maker,null,person,at));
        var baselineId=Guid.NewGuid();var caseId=Guid.NewGuid();
        var opened=InvoiceProcurementBaselineOpeningPlan.Prepare(baselineId,caseId,policy,
            "t",company,maker,null,person,at);
        Assert.Equal(baselineId,opened.SourceId);
        Assert.Equal(caseId,opened.Id);
        Assert.Equal(InvoiceProcurementBaselineEvidence.SourceType,opened.SourceType);
        Assert.Equal(policy.Id,opened.PolicyVersionId);
        Assert.Equal("Pending",opened.State);
        var stage=Assert.Single(opened.Stages);
        Assert.Equal("Any",stage.Mode);
        var assignment=Assert.Single(stage.Assignments);
        Assert.Equal(reviewer,assignment.AssigneeSubjectId);
        Assert.Equal(InvoiceProcurementBaselineEvidence.ReviewPermission,assignment.PermissionKey);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineOpeningPlan.Prepare(
            baselineId,caseId,policy,"t",Guid.NewGuid(),maker,null,person,at));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineOpeningPlan.Prepare(
            baselineId,caseId,policy,"t",company,maker,null,person,at.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineReviewPolicyRules.Resolve(
            policy,"t",company,reviewer,null,person,at));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineReviewPolicyRules.Resolve(
            policy,"t",company,maker,null,person with{OrganisationWide=false},at));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineReviewPolicyRules.Resolve(
            policy,"t",company,maker,null,person,at.AddMinutes(-6)));
        Assert.NotEmpty(InvoiceProcurementBaselineReviewPolicyRules.Validate(
            json[..^1]+",\"reviewerPrincipalId\":\""+reviewer+"\"}"));
        Assert.NotEmpty(InvoiceProcurementBaselineReviewPolicyRules.Validate(
            json[..^1]+",\"extra\":true}"));
    }
}
