using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Explicit configuration input only. A valid definition is not a published
/// plan, a current evaluator assignment, an opened bid, a score or award authority.
/// Owning adapters must freeze it before publication with source/policy/document pins.</summary>
public sealed class PrincipalRfqEvaluationPlanDefinition
{
    public sealed record Criterion(string Code,string Phase,string Kind,bool Mandatory,
        decimal? MaximumScore,decimal? Weight,IReadOnlyList<string> EvaluatorPrincipalIds);

    public int SchemaVersion=>1;
    public decimal NormalizationTotal{get;}
    public IReadOnlyList<Criterion> Criteria{get;}
    public string RulesSha256{get;}
    public bool IsPublicationAuthority=>false;

    private PrincipalRfqEvaluationPlanDefinition(decimal total,IReadOnlyList<Criterion> criteria,string digest)
    {NormalizationTotal=total;Criteria=criteria;RulesSha256=digest;}

    /// <summary>No schema1 SOURCING publication rule or existing buyer Draft is
    /// implicitly upgraded. This separately named definition requires explicit fields.</summary>
    public static PrincipalRfqEvaluationPlanDefinition Parse(string json)
    {
        Need(!string.IsNullOrWhiteSpace(json)&&json.Length<=65_536
            &&Encoding.UTF8.GetByteCount(json)<=65_536,"RfqEvaluationDefinitionRequired");
        // Reuse the established recursive duplicate-property/canonical hash verifier.
        // No second permissive serializer/case-insensitive object hydration.
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});
        var digest=ProcurementEvidenceEncoding.PolicyJsonDigest(json);
        Need(digest.Length==64,"RfqEvaluationJsonInvalid");
        var root=document.RootElement;
        Only(root,"schemaVersion","transactionType","normalizationTotal","criteria");
        Need(root.GetProperty("schemaVersion").ValueKind==JsonValueKind.Number
            &&root.GetProperty("schemaVersion").TryGetInt32(out var schema)&&schema==1
            &&Text(root,"transactionType",40)=="SourcingEvaluationDefinition","RfqEvaluationDefinitionVersionUnsupported");
        var total=Amount(root.GetProperty("normalizationTotal"));
        Need(total is >0,"RfqEvaluationNormalizationRequired");
        var rows=root.GetProperty("criteria");
        Need(rows.ValueKind==JsonValueKind.Array&&rows.GetArrayLength() is >=1 and <=64,"RfqEvaluationCriteriaBound");
        var criteria=new List<Criterion>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in rows.EnumerateArray())
        {
            Only(row,"code","phase","kind","mandatory","maximumScore","weight","evaluatorPrincipalIds");
            var code=Text(row,"code",64);var phase=Text(row,"phase",20);var kind=Text(row,"kind",20);
            Need(names.Add(code)&&code.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"RfqEvaluationCriterionCodeInvalid");
            Need((phase is "Technical" or "Commercial")&&(kind is "Scored" or "PassFail"),"RfqEvaluationCriterionKindUnsupported");
            var mandatory=row.GetProperty("mandatory").GetBoolean();
            var maximum=Amount(row.GetProperty("maximumScore"));var weight=Amount(row.GetProperty("weight"));
            Need(kind=="Scored"?maximum is >0&&weight is >0:maximum is null&&weight is null,
                "RfqEvaluationCriterionAmountsInvalid");
            var people=row.GetProperty("evaluatorPrincipalIds");
            Need(people.ValueKind==JsonValueKind.Array&&people.GetArrayLength() is >=1 and <=64,"RfqEvaluationNamedPeopleRequired");
            var principals=people.EnumerateArray().Select(person=>
            {
                Need(person.ValueKind==JsonValueKind.String&&ProcurementPrincipalEvidence.Canonical(person.GetString()),
                    "RfqEvaluationPrincipalInvalid");return person.GetString()!;
            }).ToArray();
            Need(principals.Distinct(StringComparer.Ordinal).Count()==principals.Length,"RfqEvaluationDuplicatePrincipal");
            // Copy and expose read-only collections: callers cannot rewrite a parsed
            // plan through their original arrays or a mutable IReadOnlyList wrapper.
            criteria.Add(new(code,phase,kind,mandatory,maximum,weight,Array.AsReadOnly(principals)));
        }
        Need(criteria.Any(x=>x.Kind=="Scored")
            &&ExactDecimalArithmetic.Sum(criteria.Where(x=>x.Kind=="Scored").Select(x=>x.Weight!.Value))==total,
            "RfqEvaluationWeightTotalMismatch");
        // Maxima, weights and phases are retained exactly; there is no adopted price
        // formula, computed score, pass threshold, tie-break or automatic award here.
        return new(total!.Value,criteria.AsReadOnly(),digest);
    }
    private static decimal? Amount(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Null)return null;
        Need(value.ValueKind is JsonValueKind.String or JsonValueKind.Number,"RfqEvaluationAmountInvalid");
        return ExactDecimalJsonCodec.Parse(value.ValueKind==JsonValueKind.String?value.GetString():value.GetRawText(),18,4,"RfqEvaluationAmount");
    }
    private static string Text(JsonElement row,string name,int max)
    {
        var value=row.GetProperty(name);
        Need(value.ValueKind==JsonValueKind.String,"RfqEvaluationTextInvalid");
        var text=value.GetString();Need(!string.IsNullOrWhiteSpace(text)&&text==text.Trim()&&text.Length<=max,"RfqEvaluationTextInvalid");return text!;
    }
    private static void Only(JsonElement row,params string[] names)=>Need(row.ValueKind==JsonValueKind.Object
        &&row.EnumerateObject().Select(x=>x.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)),"RfqEvaluationUnknownOrMissingField");
    private static void Need(bool condition,string code){if(!condition)throw new InvalidOperationException(code);}
}
