using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalReceivingRule(string Code,string CategoryCode,string PurchaseType,ReceivingKind Kind,
    IReadOnlySet<Guid> SiteIds,bool InspectionRequired,string? ChecklistReference,
    IReadOnlySet<string> InspectorPrincipalIds,IReadOnlySet<string> AcceptorPrincipalIds,
    IReadOnlySet<string> ServiceOwnerPrincipalIds,bool AllowRequesterCertification,bool AllowBuyerCertification,
    bool AllowReceiverCertification,bool RequireIndependentInspector,string EvidenceReferencePrefix);
public sealed record PrincipalReceivingPolicyDocument(string CurrencyCode,IReadOnlyList<PrincipalReceivingRule> Rules);
public sealed record PrincipalReceivingPolicy(PolicyVersion Record,PrincipalReceivingRule Rule,string RulesSha256);

/// <summary>Explicit schema2 assignments over the existing owned PolicyVersion lifecycle.
/// Parsing/projecting grants no current Core publication authority; the server must verify
/// the owned publication lineage before creating new effects. No legacy worker-ID conversion.</summary>
public static class PrincipalReceivingPolicyRules
{
    public static bool IsDocument(string? json)
    {
        try{using var document=JsonDocument.Parse(json??"");return document.RootElement.TryGetProperty("schemaVersion",out var version)
            &&version.ValueKind==JsonValueKind.Number&&version.TryGetInt32(out var schema)&&schema==2
            &&document.RootElement.TryGetProperty("transactionType",out var type)&&type.ValueKind==JsonValueKind.String&&type.GetString()=="Receiving";}
        catch(Exception error) when(error is JsonException or InvalidOperationException){return false;}
    }
    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        try{_=Parse(json!);return [];}
        catch(Exception error) when(error is InvalidOperationException or JsonException or KeyNotFoundException or FormatException or OverflowException)
        {return [new("$","Choose explicit sites, named Core accounts and receiving controls. "+error.Message)];}
    }
    public static PrincipalReceivingPolicyDocument Parse(string json)
    {
        Need(!string.IsNullOrWhiteSpace(json)&&Encoding.UTF8.GetByteCount(json)<=65536,"PrincipalReceivingRulesRequired");
        Need(ProcurementEvidenceEncoding.PolicyJsonDigest(json).Length==64,"PrincipalReceivingJsonInvalid");
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        Only(root,"schemaVersion","transactionType","currencyCode","rules");
        Need(root.GetProperty("schemaVersion").GetInt32()==2&&Text(root,"transactionType",20)=="Receiving","PrincipalReceivingSchemaRequired");
        var currency=Text(root,"currencyCode",3);Need(currency.Length==3&&currency.All(c=>c is >= 'A' and <= 'Z'),"PrincipalReceivingCurrencyInvalid");
        var array=root.GetProperty("rules");Need(array.ValueKind==JsonValueKind.Array&&array.GetArrayLength() is >0 and <=64,"PrincipalReceivingRulesBound");
        var rules=new List<PrincipalReceivingRule>();
        foreach(var row in array.EnumerateArray())
        {
            Only(row,"code","categoryCode","purchaseType","kind","siteIds","inspectionRequired","checklistReference",
                "inspectorPrincipalIds","acceptorPrincipalIds","serviceOwnerPrincipalIds","allowRequesterCertification",
                "allowBuyerCertification","allowReceiverCertification","requireIndependentInspector","evidenceMode","evidenceReferencePrefix");
            Need(Text(row,"evidenceMode",40)=="ControlledManualReference","PrincipalReceivingEvidenceModeUnsupported");
            var kind=Text(row,"kind",10) switch{"Goods"=>ReceivingKind.Goods,"Service"=>ReceivingKind.Service,_=>throw new InvalidOperationException("PrincipalReceivingKindInvalid")};
            var inspection=row.GetProperty("inspectionRequired").GetBoolean();
            var checklist=row.GetProperty("checklistReference").ValueKind==JsonValueKind.Null?null:Text(row,"checklistReference",200);
            var inspectors=People(row,"inspectorPrincipalIds");var acceptors=People(row,"acceptorPrincipalIds");var owners=People(row,"serviceOwnerPrincipalIds");
            Need(inspection?kind==ReceivingKind.Goods&&checklist is not null&&inspectors.Count>0:checklist is null&&inspectors.Count==0,"PrincipalReceivingInspectionConfigurationInvalid");
            Need(kind==ReceivingKind.Goods?acceptors.Count>0&&owners.Count==0:owners.Count>0&&acceptors.Count==0&&!inspection,"PrincipalReceivingAssignmentsRequired");
            var siteValues=row.GetProperty("siteIds");Need(siteValues.ValueKind==JsonValueKind.Array&&siteValues.GetArrayLength() is >0 and <=64,"PrincipalReceivingSitesRequired");
            var sites=siteValues.EnumerateArray().Select(value=>{Need(value.ValueKind==JsonValueKind.String&&ProcurementPrincipalEvidence.Canonical(value.GetString()),"PrincipalReceivingSiteInvalid");return value.GetGuid();}).ToArray();
            Need(sites.Distinct().Count()==sites.Length,"PrincipalReceivingDuplicateSite");
            rules.Add(new(Text(row,"code",64),Text(row,"categoryCode",64),Text(row,"purchaseType",64),kind,sites.ToHashSet(),inspection,checklist,
                inspectors,acceptors,owners,row.GetProperty("allowRequesterCertification").GetBoolean(),row.GetProperty("allowBuyerCertification").GetBoolean(),
                row.GetProperty("allowReceiverCertification").GetBoolean(),row.GetProperty("requireIndependentInspector").GetBoolean(),Text(row,"evidenceReferencePrefix",100)));
        }
        Need(rules.Select(row=>row.Code).Distinct(StringComparer.Ordinal).Count()==rules.Count
            &&rules.Select(row=>(row.CategoryCode,row.PurchaseType)).Distinct().Count()==rules.Count,"PrincipalReceivingClassificationAmbiguous");
        return new(currency,rules);
    }
    public static PrincipalReceivingPolicy Select(PolicyVersion policy,PrincipalReceivingSource source,DateTimeOffset at)
    {
        Need(policy.State=="Published","PrincipalReceivingPublishedPolicyRequired");
        return Retained(policy,source,at);
    }
    // Original event time and retained publication are checked without requiring
    // today's actor eligibility or today's effective policy selection.
    public static PrincipalReceivingPolicy Retained(PolicyVersion policy,PrincipalReceivingSource source,DateTimeOffset at)
    {
        Need(policy.Id!=Guid.Empty&&policy.PolicyVersionNumber>0&&policy.PolicyKey=="RECEIVING"
            &&policy.State is "Published" or "Superseded"&&policy.PublishedAt is not null&&policy.PublishedAt<=at
            &&ProcurementPrincipalEvidence.Canonical(policy.CreatedBy)&&ProcurementPrincipalEvidence.Canonical(policy.PublishedBy)
            &&policy.CreatedBy!=policy.PublishedBy&&!string.IsNullOrWhiteSpace(policy.PublicationReason)
            &&policy.TenantId==source.Commercial.TenantId&&policy.LegalEntityId==source.Commercial.LegalEntityId
            &&policy.EffectiveFrom<=at&&(policy.EffectiveTo is null||at<policy.EffectiveTo),"PrincipalReceivingPublishedPolicyRequired");
        var doc=Parse(policy.RulesJson);Need(doc.CurrencyCode==source.Commercial.CurrencyCode,"PrincipalReceivingCurrencyMismatch");
        var rule=doc.Rules.SingleOrDefault(row=>row.CategoryCode==source.Commercial.CategoryCode&&row.PurchaseType==source.Commercial.PurchaseType)
            ??throw new InvalidOperationException("PrincipalReceivingClassificationNotConfigured");
        Need(rule.Kind==source.Commercial.Kind&&source.Commercial.BranchId is {} branch&&rule.SiteIds.Contains(branch),"PrincipalReceivingSiteNotConfigured");
        return new(policy,rule,ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson));
    }
    private static HashSet<string> People(JsonElement row,string name)
    {
        var value=row.GetProperty(name);Need(value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()<=64,"PrincipalReceivingPeopleBound");
        var values=value.EnumerateArray().Select(item=>{Need(item.ValueKind==JsonValueKind.String&&ProcurementPrincipalEvidence.Canonical(item.GetString()),"PrincipalReceivingPrincipalInvalid");return item.GetString()!;}).ToArray();
        Need(values.Distinct(StringComparer.Ordinal).Count()==values.Length,"PrincipalReceivingDuplicatePrincipal");return values.ToHashSet(StringComparer.Ordinal);
    }
    private static string Text(JsonElement row,string name,int max){var value=row.GetProperty(name).GetString();Need(!string.IsNullOrWhiteSpace(value)&&value==value.Trim()&&value.Length<=max,"PrincipalReceivingTextInvalid:"+name);return value!;}
    private static void Only(JsonElement row,params string[] keys)=>Need(row.ValueKind==JsonValueKind.Object&&row.EnumerateObject().Select(value=>value.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal)),"PrincipalReceivingUnknownOrMissingField");
    private static void Need(bool okay,string code){if(!okay)throw new InvalidOperationException(code);}
}
