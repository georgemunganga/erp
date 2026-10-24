using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqEvaluationPlanDefinitionTests
{
    private const string Principal="7bf00a55-b537-4a21-b582-2b9055029ae7";
    private static string Rules()=>$$"""
        {"schemaVersion":1,"transactionType":"SourcingEvaluationDefinition","normalizationTotal":"1",
         "criteria":[
           {"code":"TECHNICAL","phase":"Technical","kind":"Scored","mandatory":false,
            "maximumScore":"10.0000","weight":"0.6250","evaluatorPrincipalIds":["{{Principal}}"]},
           {"code":"COMMERCIAL","phase":"Commercial","kind":"Scored","mandatory":true,
            "maximumScore":20,"weight":0.375,"evaluatorPrincipalIds":["{{Principal}}"]},
           {"code":"COMPLIANCE","phase":"Technical","kind":"PassFail","mandatory":true,
            "maximumScore":null,"weight":null,"evaluatorPrincipalIds":["{{Principal}}"]}]}
        """;
    private static string Change(Action<JsonObject> change)
    {var root=JsonNode.Parse(Rules())!.AsObject();change(root);return root.ToJsonString();}
    private static void Denied(string rules)
    {
        var failure=Record.Exception(()=>PrincipalRfqEvaluationPlanDefinition.Parse(rules));
        Assert.NotNull(failure);Assert.True(failure is InvalidOperationException or JsonException);
    }

    [Fact]
    public void Explicit_exact_definition_preserves_false_null_weights_and_named_principals_without_authority()
    {
        var result=PrincipalRfqEvaluationPlanDefinition.Parse(Rules());
        Assert.False(result.IsPublicationAuthority);Assert.Equal(1m,result.NormalizationTotal);
        Assert.Equal(3,result.Criteria.Count);Assert.False(result.Criteria[0].Mandatory);
        Assert.Equal(0.625m,result.Criteria[0].Weight);Assert.Equal(0.375m,result.Criteria[1].Weight);
        Assert.Null(result.Criteria[2].Weight);Assert.Null(result.Criteria[2].MaximumScore);
        Assert.All(result.Criteria,criterion=>Assert.Equal(Principal,Assert.Single(criterion.EvaluatorPrincipalIds)));
        Assert.Equal(64,result.RulesSha256.Length);
    }
    [Fact]
    public void Collections_cannot_be_mutated_after_parse()
    {
        var result=PrincipalRfqEvaluationPlanDefinition.Parse(Rules());
        Assert.Throws<NotSupportedException>(()=>((IList<PrincipalRfqEvaluationPlanDefinition.Criterion>)result.Criteria).Clear());
        Assert.Throws<NotSupportedException>(()=>((IList<string>)result.Criteria[0].EvaluatorPrincipalIds)[0]=Guid.NewGuid().ToString("D"));
    }
    [Fact]
    public void Caller_explicit_normalization_is_preserved_not_automatically_renormalized()
    {
        var input=Change(root=>{root["normalizationTotal"]="100";root["criteria"]![0]!["weight"]="62.5";root["criteria"]![1]!["weight"]="37.5";});
        var result=PrincipalRfqEvaluationPlanDefinition.Parse(input);
        Assert.Equal(100m,result.NormalizationTotal);Assert.Equal(62.5m,result.Criteria[0].Weight);
        Denied(Change(root=>root["normalizationTotal"]="0.9999"));
    }
    [Theory]
    [InlineData("1.00001")]
    [InlineData("100000000000000")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Raw_invalid_normalization_never_rounds_or_falls_back(string value)=>Denied(Change(root=>root["normalizationTotal"]=value));
    [Fact]
    public void Raw_numeric_precision_loss_is_denied()
    {
        Denied(Rules().Replace("\"maximumScore\":20","\"maximumScore\":20.00000000000000000000000000001",StringComparison.Ordinal));
        Denied(Change(root=>root["criteria"]![0]!["maximumScore"]="10.00001"));
    }
    [Fact]
    public void Duplicate_unknown_missing_and_old_publication_schema_are_denied()
    {
        Denied(Rules().Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1",StringComparison.Ordinal));
        Denied(Rules().Replace("\"mandatory\":false","\"mandatory\":false,\"mandatory\":true",StringComparison.Ordinal));
        Denied(Change(root=>root["reviewed"]=true));Denied(Change(root=>root.Remove("normalizationTotal")));
        Denied(Change(root=>root["schemaVersion"]=2));
        Denied(Change(root=>root["transactionType"]="SourcingEvent"));
        Denied(Change(root=>root["criteria"]![0]!["Mandatory"]=false));
    }
    [Fact]
    public void Criterion_and_assignment_identity_is_exact_unique_and_bounded()
    {
        Denied(Change(root=>root["criteria"]![1]!["code"]="TECHNICAL"));
        Denied(Change(root=>root["criteria"]![0]!["evaluatorPrincipalIds"]=new JsonArray(Principal,Principal)));
        Denied(Change(root=>root["criteria"]![0]!["evaluatorPrincipalIds"]=new JsonArray(Guid.Empty.ToString("D"))));
        Denied(Change(root=>root["criteria"]![0]!["evaluatorPrincipalIds"]=new JsonArray(Principal.ToUpperInvariant())));
        Denied(Change(root=>root["criteria"]![0]!["evaluatorPrincipalIds"]=new JsonArray()));
        Denied(Change(root=>root["criteria"]=new JsonArray()));
    }
    [Fact]
    public void Pass_fail_does_not_gain_a_hidden_score_or_weight()
    {
        Denied(Change(root=>root["criteria"]![2]!["weight"]="0"));
        Denied(Change(root=>root["criteria"]![2]!["maximumScore"]="1"));
        Denied(Change(root=>root["criteria"]![0]!["maximumScore"]=null));
        Denied(Change(root=>root["criteria"]![0]!["mandatory"]="false"));
    }
    [Fact]
    public void Bounds_reject_oversized_or_deep_input_before_configuration_can_be_used()
    {
        Denied(new string(' ',65_537)+Rules());
        Denied(Change(root=>root["criteria"]![0]!["code"]=new string('A',65)));
        var deep="0";for(var depth=0;depth<18;depth++)deep="["+deep+"]";
        Denied(Rules().Replace("\"maximumScore\":20","\"maximumScore\":"+deep,StringComparison.Ordinal));
        Denied(Change(root=>{var row=root["criteria"]![0]!.DeepClone();var rows=new JsonArray();for(var i=0;i<65;i++)rows.Add(row.DeepClone());root["criteria"]=rows;}));
    }
    [Fact]
    public void Reordered_json_keeps_digest_but_changed_plan_or_assignment_does_not()
    {
        var original=PrincipalRfqEvaluationPlanDefinition.Parse(Rules());
        var reordered=JsonNode.Parse(Rules())!.AsObject();var reverse=new JsonObject();
        foreach(var pair in reordered.Reverse())reverse.Add(pair.Key,pair.Value?.DeepClone());
        Assert.Equal(original.RulesSha256,PrincipalRfqEvaluationPlanDefinition.Parse(reverse.ToJsonString()).RulesSha256);
        Assert.NotEqual(original.RulesSha256,PrincipalRfqEvaluationPlanDefinition.Parse(Change(root=>root["criteria"]![0]!["mandatory"]=true)).RulesSha256);
        Assert.NotEqual(original.RulesSha256,PrincipalRfqEvaluationPlanDefinition.Parse(Change(root=>root["criteria"]![0]!["evaluatorPrincipalIds"]=new JsonArray(Guid.NewGuid().ToString("D")))).RulesSha256);
    }
}
