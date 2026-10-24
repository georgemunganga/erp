using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqCriterionAssessmentRulesTests
{
    private const string Evaluator="7bf00a55-b537-4a21-b582-2b9055029ae7";
    private static readonly Guid Document=Guid.Parse("af02533c-ecbf-43b9-9a0e-3d7a60680f24");
    private static readonly string Sha=new('a',64);
    private static PrincipalRfqEvaluationPlanDefinition Plan()=>PrincipalRfqEvaluationPlanDefinition.Parse($$"""
        {"schemaVersion":1,"transactionType":"SourcingEvaluationDefinition","normalizationTotal":"1",
         "criteria":[
           {"code":"TECHNICAL","phase":"Technical","kind":"Scored","mandatory":false,
            "maximumScore":"10.0000","weight":"1.0000","evaluatorPrincipalIds":["{{Evaluator}}"]},
           {"code":"COMPLIANCE","phase":"Technical","kind":"PassFail","mandatory":true,
            "maximumScore":null,"weight":null,"evaluatorPrincipalIds":["{{Evaluator}}"]}]}
        """);
    private static PrincipalRfqCriterionAssessment Score(string? score="0.0000",bool? passed=null,
        string actor=Evaluator,int revision=1)=>PrincipalRfqCriterionAssessmentRules.Validate(
            Plan(),"TECHNICAL",actor,revision,score,passed,"Observed against original offer",Document,Sha);

    [Fact]
    public void Zero_score_is_a_recorded_observation_not_a_missing_result()
    {
        var row=Score();Assert.Equal(0m,row.Score);Assert.Null(row.Passed);
        Assert.Equal(Document,row.OriginalDocumentVersionId);
    }
    [Fact]
    public void Exact_maximum_and_named_evaluator_are_enforced_without_rounding()
    {
        Assert.Equal(10m,Score("10.0000").Score);
        Assert.ThrowsAny<Exception>(()=>Score("10.0001"));
        Assert.ThrowsAny<Exception>(()=>Score("1.00001"));
        Assert.ThrowsAny<Exception>(()=>Score(actor:Guid.NewGuid().ToString("D")));
        Assert.ThrowsAny<Exception>(()=>Score(revision:0));
    }
    [Fact]
    public void Pass_fail_never_acquires_a_score_or_an_inferred_decision()
    {
        var fail=PrincipalRfqCriterionAssessmentRules.Validate(Plan(),"COMPLIANCE",Evaluator,
            2,null,false,"Required document missing",Document,Sha);
        Assert.False(fail.Passed);Assert.Null(fail.Score);
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqCriterionAssessmentRules.Validate(
            Plan(),"COMPLIANCE",Evaluator,1,"0",false,"Reason",Document,Sha));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqCriterionAssessmentRules.Validate(
            Plan(),"COMPLIANCE",Evaluator,1,null,null,"Reason",Document,Sha));
        Assert.Throws<InvalidOperationException>(()=>Score("1",true));
    }
}
