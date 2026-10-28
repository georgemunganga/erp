using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementBaselineReviewPolicyRulesTests
{
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
