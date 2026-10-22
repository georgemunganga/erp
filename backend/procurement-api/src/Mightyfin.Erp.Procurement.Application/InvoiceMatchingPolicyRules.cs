using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Strict matching rules over the existing independently governed PolicyVersion record.
/// Publishing and canonical maker/checker authority remain owned by the policy application adapter.</summary>
public static class InvoiceMatchingPolicyRules
{
    public const string PolicyKey = "INVOICE_MATCHING";
    public sealed record Document(string CurrencyCode, int MoneyScale, InvoiceMoneyRounding Rounding,
        IReadOnlyList<InvoiceCategoryMatchRule> Rules);
    public static bool IsMatchingDocument(string? json)
    {
        try { using var document = JsonDocument.Parse(json ?? ""); return document.RootElement.TryGetProperty("transactionType", out var value) && value.GetString() == "InvoiceMatching"; }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return false; }
    }
    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        try { Parse(json ?? ""); return []; }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return [new("$", "Use explicit invoice matching categories, method, acceptance kind, rounding and exact tolerance strings. " + error.Message)]; }
    }
    public static Document Parse(string json)
    {
        Need(!string.IsNullOrWhiteSpace(json) && System.Text.Encoding.UTF8.GetByteCount(json) <= 65536
            && ProcurementEvidenceEncoding.PolicyJsonDigest(json).Length == 64, "InvoiceMatchingRulesInvalid");
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        Only(root,"schemaVersion","transactionType","currencyCode","moneyScale","rounding","rules");
        Need(root.GetProperty("schemaVersion").GetInt32() == 1 && Text(root,"transactionType",30) == "InvoiceMatching", "InvoiceMatchingSchemaUnsupported");
        var currency = Text(root,"currencyCode",3); Need(currency.Length == 3 && currency.All(x => x is >= 'A' and <= 'Z'),"InvoiceMatchingCurrencyInvalid");
        var scale = root.GetProperty("moneyScale").GetInt32(); Need(scale is >= 0 and <= 4,"InvoiceMatchingMoneyScaleInvalid");
        var rounding = EnumValue<InvoiceMoneyRounding>(root,"rounding");
        var rows = root.GetProperty("rules"); Need(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() is > 0 and <= 64,"InvoiceMatchingRulesBoundExceeded");
        var rules = new List<InvoiceCategoryMatchRule>();
        foreach(var row in rows.EnumerateArray())
        {
            Only(row,"categoryCode","method","acceptanceKind","quantity","unitPrice","tax","total");
            var method = EnumValue<InvoiceMatchingMethod>(row,"method");
            var kind = row.GetProperty("acceptanceKind").ValueKind == JsonValueKind.Null ? (InvoiceAcceptanceKind?)null : EnumValue<InvoiceAcceptanceKind>(row,"acceptanceKind");
            Need(method == InvoiceMatchingMethod.TwoWay ? kind is null : kind is not null,"InvoiceMatchingAcceptanceMethodInvalid");
            rules.Add(new(Text(row,"categoryCode",64),currency,method,kind,Tolerance(row,"quantity"),Tolerance(row,"unitPrice"),Tolerance(row,"tax"),Tolerance(row,"total")));
        }
        Need(rules.Select(x => x.CategoryCode).Distinct(StringComparer.Ordinal).Count() == rules.Count,"InvoiceMatchingCategoryAmbiguous");
        return new(currency,scale,rounding,rules.OrderBy(x => x.CategoryCode,StringComparer.Ordinal).ToArray());
    }
    public static InvoiceMatchingPolicy Project(PolicyVersion policy)
    {
        Need(policy.PolicyKey == PolicyKey && policy.State == "Published" && policy.Id != Guid.Empty && policy.PolicyVersionNumber > 0
            && policy.PublishedAt is not null && !string.IsNullOrWhiteSpace(policy.PublishedBy) && !string.IsNullOrWhiteSpace(policy.CreatedBy)
            && policy.PublishedBy != policy.CreatedBy && !string.IsNullOrWhiteSpace(policy.PublicationReason),"PublishedInvoiceMatchingPolicyRequired");
        var rules = Parse(policy.RulesJson);
        return new(policy.Id,policy.PolicyVersionNumber,policy.TenantId,policy.LegalEntityId,null,"Published",
            $"INVOICE_MATCHING:{policy.Id:D}:{policy.PolicyVersionNumber}",policy.EffectiveFrom,policy.EffectiveTo,rules.MoneyScale,rules.Rounding,rules.Rules);
    }
    public static string ProjectionDigest(InvoiceMatchingPolicy policy)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(policy with
        { Rules = policy.Rules.OrderBy(x => x.CategoryCode,StringComparer.Ordinal).ThenBy(x => x.CurrencyCode,StringComparer.Ordinal).ToArray() },new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    private static InvoiceFieldTolerance Tolerance(JsonElement row,string key)
    {
        var value = row.GetProperty(key); Only(value,"absolute","percent","combination");
        var absolute = ExactDecimalJsonCodec.Parse(Text(value,"absolute",128),18,4,"InvoiceMatchingTolerance");
        var percent = ExactDecimalJsonCodec.Parse(Text(value,"percent",128),18,4,"InvoiceMatchingTolerance");
        Need(absolute >= 0 && percent is >= 0 and <= 100,"InvoiceMatchingToleranceInvalid");
        return new(absolute,percent,EnumValue<InvoiceToleranceCombination>(value,"combination"));
    }
    private static T EnumValue<T>(JsonElement row,string key) where T : struct,Enum
    {
        var text = Text(row,key,30); Need(Enum.TryParse<T>(text,false,out var value) && Enum.IsDefined(value) && value.ToString() == text,"InvoiceMatchingEnumInvalid:"+key); return value;
    }
    private static string Text(JsonElement row,string key,int max)
    { var value=row.GetProperty(key).GetString(); Need(!string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= max,"InvoiceMatchingTextInvalid:"+key); return value!; }
    private static void Only(JsonElement row,params string[] keys)
        => Need(row.ValueKind == JsonValueKind.Object && row.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(keys.Order()),"InvoiceMatchingUnknownOrMissingField");
    private static void Need(bool okay,string code) { if(!okay) throw new InvalidOperationException(code); }
}
