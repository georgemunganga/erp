using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ReceivingRule(string Code, string CategoryCode, string PurchaseType, ReceivingKind Kind,
    Guid[] SiteIds, bool InspectionRequired, string? ChecklistReference, Guid[] InspectorWorkerIds,
    Guid[] ServiceOwnerWorkerIds, bool AllowRequesterCertification, bool AllowBuyerCertification,
    bool AllowReceiverCertification, bool RequireIndependentInspector, string EvidenceReferencePrefix);
public sealed record ReceivingRuleDocument(string CurrencyCode, IReadOnlyList<ReceivingRule> Rules);

/// <summary>Strict configuration over the owned PolicyVersion lifecycle, not a second policy engine.
/// Every rule/actor/site/control is explicitly configured and independently published. No fallback Goods,
/// automatic acceptance or default inspector. Only policy-approved structured manual references are supported.</summary>
public static class ReceivingPolicyRules
{
    public static bool IsReceivingDocument(string? json)
    {
        try { using var doc = JsonDocument.Parse(json ?? ""); return doc.RootElement.TryGetProperty("transactionType", out var value) && value.GetString() == "Receiving"; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return false; }
    }
    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        try { Parse(json); return []; }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or KeyNotFoundException or FormatException)
        { return [new("$", "Use the supported receiving schema with explicit category, purchase type, sites, named workers and control choices. " + ex.Message)]; }
    }
    public static ReceivingRuleDocument Parse(string? json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && System.Text.Encoding.UTF8.GetByteCount(json!) <= 65536, "ReceivingRulesRequired");
        Require(ProcurementEvidenceEncoding.PolicyJsonDigest(json!).Length == 64, "ReceivingJsonInvalid"); // Reject duplicate/unsupported JSON representations.
        using var document = JsonDocument.Parse(json!); var root = document.RootElement;
        Only(root, "schemaVersion", "transactionType", "currencyCode", "rules");
        Require(root.GetProperty("schemaVersion").GetInt32() == 1 && Text(root, "transactionType", 20) == "Receiving", "ReceivingSchemaUnsupported");
        var currency = Text(root, "currencyCode", 3);
        Require(currency.Length == 3 && currency.All(x => x is >= 'A' and <= 'Z'), "ReceivingCurrencyInvalid");
        var array = root.GetProperty("rules"); Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() is > 0 and <= 64, "ReceivingRulesBoundExceeded");
        var rules = new List<ReceivingRule>();
        foreach (var row in array.EnumerateArray())
        {
            Only(row, "code", "categoryCode", "purchaseType", "kind", "siteIds", "inspectionRequired", "checklistReference",
                "inspectorWorkerIds", "serviceOwnerWorkerIds", "allowRequesterCertification", "allowBuyerCertification", "allowReceiverCertification",
                "requireIndependentInspector", "evidenceMode", "evidenceReferencePrefix");
            Require(Text(row, "evidenceMode", 40) == "ControlledManualReference", "ReceivingEvidenceModeUnsupported");
            var kindText = Text(row, "kind", 10);
            Require(kindText is "Goods" or "Service", "ReceivingKindInvalid");
            var kind = kindText == "Goods" ? ReceivingKind.Goods : ReceivingKind.Service;
            var inspection = row.GetProperty("inspectionRequired").GetBoolean();
            var checklist = row.GetProperty("checklistReference").ValueKind == JsonValueKind.Null ? null : Text(row, "checklistReference", 200);
            var inspectors = Ids(row, "inspectorWorkerIds", false); var owners = Ids(row, "serviceOwnerWorkerIds", false);
            Require(kind == ReceivingKind.Goods || !inspection && checklist is null && inspectors.Length == 0, "ServiceInspectionUnsupported");
            Require(!inspection || checklist is not null && inspectors.Length > 0, "ReceivingInspectorRequired");
            Require(inspection || checklist is null && inspectors.Length == 0, "UnusedInspectionConfiguration");
            Require(kind == ReceivingKind.Service ? owners.Length > 0 : owners.Length == 0, "ReceivingServiceOwnerRequired");
            rules.Add(new(Text(row, "code", 64), Text(row, "categoryCode", 64), Text(row, "purchaseType", 64), kind,
                Ids(row, "siteIds", true), inspection, checklist, inspectors, owners,
                row.GetProperty("allowRequesterCertification").GetBoolean(), row.GetProperty("allowBuyerCertification").GetBoolean(),
                row.GetProperty("allowReceiverCertification").GetBoolean(), row.GetProperty("requireIndependentInspector").GetBoolean(),
                Text(row, "evidenceReferencePrefix", 100)));
        }
        Require(rules.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == rules.Count
            && rules.Select(x => (x.CategoryCode, x.PurchaseType)).Distinct().Count() == rules.Count, "ReceivingClassificationAmbiguous");
        return new(currency, rules);
    }
    public static ReceivingRule Select(ReceivingRuleDocument document, string category, string purchaseType)
        => document.Rules.SingleOrDefault(x => x.CategoryCode == category && x.PurchaseType == purchaseType)
            ?? throw new InvalidOperationException("ReceivingClassificationNotConfigured");
    public static ReceivingPolicyProjection Project(PolicyVersion policy, ReceivingRule rule)
    {
        Require(policy.PolicyKey == "RECEIVING" && policy.State == "Published" && policy.PublishedAt is not null
            && !string.IsNullOrWhiteSpace(policy.PublishedBy) && policy.PublishedBy != policy.CreatedBy
            && !string.IsNullOrWhiteSpace(policy.PublicationReason), "PublishedReceivingPolicyRequired");
        var document = Parse(policy.RulesJson);
        Require(document.Rules.Any(x => JsonSerializer.Serialize(x) == JsonSerializer.Serialize(rule)), "ReceivingRuleNotOwnedByPolicy");
        return new(policy.Id, policy.TenantId, policy.LegalEntityId, null, rule.CategoryCode, rule.Kind, true,
            policy.EffectiveFrom, policy.EffectiveTo, rule.InspectionRequired, rule.ChecklistReference,
            rule.AllowRequesterCertification, rule.AllowBuyerCertification, rule.AllowReceiverCertification,
            rule.RequireIndependentInspector, rule.InspectorWorkerIds.ToHashSet(), rule.ServiceOwnerWorkerIds.ToHashSet(), new HashSet<Guid>(), rule.SiteIds.ToHashSet());
    }
    public static void VerifyReference(ReceivingRule rule, string evidence)
        => Require(!string.IsNullOrWhiteSpace(evidence) && evidence.Length <= 500 && evidence == evidence.Trim()
            && evidence.StartsWith(rule.EvidenceReferencePrefix, StringComparison.Ordinal)
            && evidence.Length > rule.EvidenceReferencePrefix.Length, "ReceivingApprovedEvidenceReferenceRequired");
    private static Guid[] Ids(JsonElement row, string key, bool required)
    {
        var array = row.GetProperty(key); Require(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() <= 100, "ReceivingReferencesBoundExceeded");
        var ids = array.EnumerateArray().Select(x => x.GetGuid()).ToArray();
        Require((!required || ids.Length > 0) && ids.All(x => x != Guid.Empty) && ids.Distinct().Count() == ids.Length, "ReceivingReferencesInvalid"); return ids;
    }
    private static string Text(JsonElement row, string key, int max)
    { var text = row.GetProperty(key).GetString(); Require(!string.IsNullOrWhiteSpace(text) && text == text.Trim() && text.Length <= max, "ReceivingTextInvalid:" + key); return text!; }
    private static void Only(JsonElement row, params string[] keys)
        => Require(row.ValueKind == JsonValueKind.Object && row.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(keys.Order()), "ReceivingUnknownOrMissingField");
    private static void Require(bool okay, string error) { if (!okay) throw new InvalidOperationException(error); }
}
