using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public enum SourcingAudienceLifecycle { RecordedActiveDecision }
public enum SourcingAudienceCategoryMode { Ignore, CoverAllSelectedCategories }
public enum SourcingAudienceQualificationDocuments { NotRequired, CoreAvailableVersionRequired }
public enum SourcingAudienceValidityHorizon { PreparationInstant, ClosingInstant }
public enum SourcingAudienceContactSelection { OneActiveSelected, OneActivePrimarySelected }
public enum SourcingAudienceContactField { Email, Phone }

/// <summary>Parsed configuration only, not a published policy, owning supplier fact or eligibility.
/// Immutable projections preserve unconfigured null separately from explicit empty/false choices.</summary>
public sealed class SourcingAudienceDraft
{
    internal SourcingAudienceDraft(bool specified, string digest,
        SourcingAudienceLifecycle? lifecycle = null, SourcingAudienceCategoryMode? category = null,
        IReadOnlyList<string>? qualifications = null, SourcingAudienceQualificationDocuments? documents = null,
        SourcingAudienceValidityHorizon? horizon = null, bool? noExpiry = null, bool? openEnded = null,
        SourcingAudienceContactSelection? contact = null, IReadOnlyList<SourcingAudienceContactField>? fields = null)
    {
        IsSpecified = specified; ConfigurationSha256 = digest; SupplierLifecycle = lifecycle; CategoryMode = category;
        QualificationTypes = qualifications; QualificationDocuments = documents; ValidThrough = horizon;
        AllowNoQualificationExpiry = noExpiry; AllowOpenEndedSupplierValidity = openEnded;
        ContactSelection = contact; ContactRequiredFields = fields;
    }
    public int PolicySchemaVersion => 2;
    public bool IsSpecified { get; }
    public string ConfigurationSha256 { get; }
    public SourcingAudienceLifecycle? SupplierLifecycle { get; }
    public SourcingAudienceCategoryMode? CategoryMode { get; }
    public IReadOnlyList<string>? QualificationTypes { get; }
    public SourcingAudienceQualificationDocuments? QualificationDocuments { get; }
    public SourcingAudienceValidityHorizon? ValidThrough { get; }
    public bool? AllowNoQualificationExpiry { get; }
    public bool? AllowOpenEndedSupplierValidity { get; }
    public SourcingAudienceContactSelection? ContactSelection { get; }
    public IReadOnlyList<SourcingAudienceContactField>? ContactRequiredFields { get; }
    public bool CanPublish => false;
}

/// <summary>Pure schema2 audience fragment parser only. The existing schema1 full-policy parser,
/// owned routes/identity semantics/DI are untouched. A caller must prove the enclosing policy's
/// authentic version/rule/lineage and owning facts separately; configuration is never eligibility.</summary>
public static class SourcingAudienceConfiguration
{
    public const int MaximumUtf8Bytes = 16_384;
    public const int MaximumQualificationTypes = 100;
    // Same bound as existing SupplierQualification.QualificationType's owning mapping.
    public const int MaximumQualificationTypeLength = 80;
    private static readonly string[] Fields = ["supplierLifecycle", "categoryMode", "qualificationTypes", "qualificationDocuments",
        "validThrough", "allowNoQualificationExpiry", "allowOpenEndedSupplierValidity", "contactSelection", "contactRequiredFields"];

    public static SourcingAudienceDraft ParseDraft(int policySchemaVersion, string? audienceJson)
    {
        Require(policySchemaVersion == 2, "SourcingAudiencePolicySchemaUnsupported");
        Require(!string.IsNullOrWhiteSpace(audienceJson) && audienceJson.Length <= MaximumUtf8Bytes
            && Encoding.UTF8.GetByteCount(audienceJson) <= MaximumUtf8Bytes, "SourcingAudienceRequiredOrTooLarge");
        try
        {
            using var json = JsonDocument.Parse(audienceJson!, new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            Require(root.ValueKind is JsonValueKind.Null or JsonValueKind.Object, "SourcingAudienceObjectRequired");
            // Shared canonical encoder rejects duplicate properties recursively. Parse the whole
            // fragment first, so wrapping cannot admit trailing/injected root properties.
            var digest = ProcurementEvidenceEncoding.PolicyJsonDigest("{\"audience\":" + audienceJson + "}");
            Require(digest.Length == 64, "SourcingAudienceJsonInvalid");
            if (root.ValueKind == JsonValueKind.Null) return new(false, digest);
            Require(root.EnumerateObject().All(x => Fields.Contains(x.Name, StringComparer.Ordinal)), "SourcingAudienceFieldUnsupported");
            var lifecycle = Choice<SourcingAudienceLifecycle>(root, "supplierLifecycle");
            var category = Choice<SourcingAudienceCategoryMode>(root, "categoryMode");
            var qualifications = QualificationTypes(root);
            var documents = Choice<SourcingAudienceQualificationDocuments>(root, "qualificationDocuments");
            var horizon = Choice<SourcingAudienceValidityHorizon>(root, "validThrough");
            var noExpiry = Boolean(root, "allowNoQualificationExpiry");
            var openEnded = Boolean(root, "allowOpenEndedSupplierValidity");
            var contact = Choice<SourcingAudienceContactSelection>(root, "contactSelection");
            var fields = ContactFields(root);
            return new(true, digest, lifecycle, category, qualifications, documents, horizon, noExpiry, openEnded, contact, fields);
        }
        catch (JsonException error) { throw new InvalidOperationException("SourcingAudienceJsonInvalid", error); }
    }

    public static IReadOnlyList<PolicyRuleIssue> ValidateDraft(int policySchemaVersion, string? audienceJson)
    {
        try { _ = ParseDraft(policySchemaVersion, audienceJson); return []; }
        catch (InvalidOperationException error) { return [new("$.audience", error.Message)]; }
    }

    /// <summary>Completeness of these configuration choices only; not policy review/publication eligibility.</summary>
    public static IReadOnlyList<PolicyRuleIssue> ReviewIssues(SourcingAudienceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.IsSpecified) return [new("$.audience", "SourcingAudienceConfigurationRequired")];
        var issues = new List<PolicyRuleIssue>();
        void Required(string field, bool present)
        { if (!present) issues.Add(new("$.audience." + field, "SourcingAudienceChoiceRequired")); }
        Required("supplierLifecycle", draft.SupplierLifecycle.HasValue);
        Required("categoryMode", draft.CategoryMode.HasValue);
        Required("qualificationTypes", draft.QualificationTypes is not null);
        Required("qualificationDocuments", draft.QualificationDocuments.HasValue);
        Required("validThrough", draft.ValidThrough.HasValue);
        Required("allowNoQualificationExpiry", draft.AllowNoQualificationExpiry.HasValue);
        Required("allowOpenEndedSupplierValidity", draft.AllowOpenEndedSupplierValidity.HasValue);
        Required("contactSelection", draft.ContactSelection.HasValue);
        Required("contactRequiredFields", draft.ContactRequiredFields is not null);
        return issues.AsReadOnly();
    }

    private static JsonElement? Value(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;
    private static T? Choice<T>(JsonElement root, string field) where T : struct, Enum
    {
        var value = Value(root, field); if (value is null) return null;
        return ParseChoice<T>(value.Value, field);
    }
    private static T ParseChoice<T>(JsonElement value, string field) where T : struct, Enum
    {
        Require(value.ValueKind == JsonValueKind.String, "SourcingAudienceChoiceInvalid:" + field);
        var text = value.GetString();
        Require(Enum.TryParse<T>(text, false, out var parsed) && Enum.GetName(parsed) == text, "SourcingAudienceChoiceInvalid:" + field);
        return parsed;
    }
    private static bool? Boolean(JsonElement root, string field)
    {
        var value = Value(root, field); if (value is null) return null;
        Require(value.Value.ValueKind is JsonValueKind.True or JsonValueKind.False, "SourcingAudienceBooleanInvalid:" + field);
        return value.Value.GetBoolean();
    }
    private static IReadOnlyList<string>? QualificationTypes(JsonElement root)
    {
        var value = Value(root, "qualificationTypes"); if (value is null) return null;
        Require(value.Value.ValueKind == JsonValueKind.Array && value.Value.GetArrayLength() <= MaximumQualificationTypes, "SourcingAudienceQualificationTypesInvalid");
        var list = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.Value.EnumerateArray())
        {
            Require(item.ValueKind == JsonValueKind.String, "SourcingAudienceQualificationTypeInvalid");
            var text = item.GetString();
            Require(text is { Length: > 0 and <= MaximumQualificationTypeLength } && text == text.Trim()
                && !text.Any(char.IsControl) && seen.Add(text), "SourcingAudienceQualificationTypeInvalid");
            list.Add(text!);
        }
        return list.AsReadOnly();
    }
    private static IReadOnlyList<SourcingAudienceContactField>? ContactFields(JsonElement root)
    {
        var value = Value(root, "contactRequiredFields"); if (value is null) return null;
        Require(value.Value.ValueKind == JsonValueKind.Array && value.Value.GetArrayLength() <= 2, "SourcingAudienceContactFieldsInvalid");
        var list = new List<SourcingAudienceContactField>();
        foreach (var item in value.Value.EnumerateArray())
        {
            var parsed = ParseChoice<SourcingAudienceContactField>(item, "contactRequiredFields");
            Require(!list.Contains(parsed), "SourcingAudienceContactFieldInvalid");
            list.Add(parsed);
        }
        return list.AsReadOnly();
    }
    private static void Require([DoesNotReturnIf(false)] bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }
}
