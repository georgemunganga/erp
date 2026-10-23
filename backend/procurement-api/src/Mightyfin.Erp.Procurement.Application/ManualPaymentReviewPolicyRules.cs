using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <param name="MaximumByCurrency">Null means no configured monetary limit; a present map requires currency coverage.</param>
public sealed record ManualPaymentReviewPolicyDocument(int SchemaVersion, int IdentitySchemaVersion,
    string ApprovalType, bool IndependentCreatorApprover, bool IndependentRecorderConfirmer,
    IReadOnlyDictionary<string, decimal>? MaximumByCurrency);

public sealed record ManualPaymentReviewPolicyProjection(Guid PolicyId, int PolicyVersion,
    string RulesSha256, string TenantId, Guid LegalEntityId, ManualPaymentReviewPolicyDocument Rules);

/// <summary>Strict, pure rules over the existing owned PolicyVersion lifecycle.
/// Core account eligibility, branch authority, publication pinning and approval execution belong to the host.</summary>
public static class ManualPaymentReviewPolicyRules
{
    public const string PolicyKey = "MANUAL_PAYMENT_REVIEW";
    public const string TransactionType = "ManualPaymentReview";
    private const string Invalid = "ManualPaymentReviewPolicyUnavailable";
    private static readonly Regex AmountSyntax = new(@"^(?:0|[1-9][0-9]{0,13})(?:\.[0-9]{1,4})?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static ManualPaymentReviewPolicyDocument Parse(string? json) => ParseCore(json);
    public static ManualPaymentReviewPolicyDocument ParseDraft(string? json) => ParseCore(json);

    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json, bool draft = false)
    {
        try { _ = draft ? ParseDraft(json) : Parse(json); return []; }
        catch (InvalidOperationException) { return [new("$", Invalid)]; }
    }

    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 16384) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("transactionType", out var kind)
                && kind.ValueKind == JsonValueKind.String && kind.GetString() == TransactionType;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Consumes a current, host-authorized policy row; never selects or publishes one.</summary>
    public static ManualPaymentReviewPolicyProjection ResolveEffective(PolicyVersion policy,
        string tenantId, Guid legalEntityId, DateTimeOffset now)
    {
        Need(policy is not null && !string.IsNullOrWhiteSpace(tenantId) && legalEntityId != Guid.Empty
            && policy.TenantId == tenantId && policy.LegalEntityId == legalEntityId
            && policy.Id != Guid.Empty && policy.PolicyKey == PolicyKey && policy.PolicyVersionNumber > 0
            && policy.State == "Published" && now != default
            && policy.EffectiveFrom != default && policy.EffectiveFrom <= now
            && (policy.EffectiveTo is null || now < policy.EffectiveTo)
            && policy.PublishedAt is { } published && published <= now
            && !string.IsNullOrWhiteSpace(policy.CreatedBy) && !string.IsNullOrWhiteSpace(policy.PublishedBy)
            && policy.CreatedBy != policy.PublishedBy && !string.IsNullOrWhiteSpace(policy.PublicationReason));
        var rules = Parse(policy.RulesJson);
        return new(policy.Id, policy.PolicyVersionNumber,
            ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson), tenantId, legalEntityId, rules);
    }

    /// <summary>An absent currency limit is not an amount approval. The host must still check Core actions and assignees.</summary>
    public static bool WithinConfiguredMaximum(ManualPaymentReviewPolicyDocument rules, string currencyCode, decimal amount)
        => rules is not null && rules.SchemaVersion == 1 && rules.IdentitySchemaVersion == 2
            && rules.ApprovalType == "SingleApprover"
            && rules.IndependentCreatorApprover && rules.IndependentRecorderConfirmer
            && currencyCode is { Length: 3 }
            && currencyCode.All(c => c is >= 'A' and <= 'Z')
            && amount > 0 && ExactDecimalJsonCodec.IsRepresentable(amount, 18, 4)
            && (rules.MaximumByCurrency is null
                || rules.MaximumByCurrency.TryGetValue(currencyCode, out var limit) && amount <= limit);

    private static ManualPaymentReviewPolicyDocument ParseCore(string? json)
    {
        try
        {
            Need(!string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json) <= 16384);
            var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(json);
            Need(digest.Length == 64); // Shared canonical codec rejects duplicate properties at every depth.
            using var document = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            Only(root, "schemaVersion", "identitySchemaVersion", "policyKey", "transactionType",
                "approvalType", "independentCreatorApprover", "independentRecorderConfirmer", "maximumByCurrency?");
            Need(root.GetProperty("schemaVersion").ValueKind == JsonValueKind.Number
                && root.GetProperty("schemaVersion").GetInt32() == 1);
            Need(root.GetProperty("identitySchemaVersion").ValueKind == JsonValueKind.Number
                && root.GetProperty("identitySchemaVersion").GetInt32() == 2);
            Need(Text(root, "policyKey") == PolicyKey && Text(root, "transactionType") == TransactionType
                && Text(root, "approvalType") == "SingleApprover"
                && root.GetProperty("independentCreatorApprover").ValueKind == JsonValueKind.True
                && root.GetProperty("independentRecorderConfirmer").ValueKind == JsonValueKind.True);
            IReadOnlyDictionary<string, decimal>? limits = null;
            if (root.TryGetProperty("maximumByCurrency", out var maximums))
            {
                Need(maximums.ValueKind == JsonValueKind.Object && maximums.EnumerateObject().Count() is >= 1 and <= 32);
                var entries = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var entry in maximums.EnumerateObject())
                {
                    Need(entry.Name.Length == 3 && entry.Name.All(c => c is >= 'A' and <= 'Z')
                        && entry.Value.ValueKind == JsonValueKind.String);
                    var literal = entry.Value.GetString();
                    Need(literal is not null && AmountSyntax.IsMatch(literal));
                    var amount = ExactDecimalJsonCodec.Parse(literal, 18, 4, Invalid);
                    Need(amount > 0 && entries.TryAdd(entry.Name, amount));
                }
                limits = new ReadOnlyDictionary<string, decimal>(entries);
            }
            return new(1, 2, "SingleApprover", true, true, limits);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException
            or FormatException or OverflowException or KeyNotFoundException)
        { throw new InvalidOperationException(Invalid, error); }
    }

    private static string? Text(JsonElement row, string key)
    {
        var value = row.GetProperty(key);
        Need(value.ValueKind == JsonValueKind.String);
        return value.GetString();
    }

    private static void Only(JsonElement row, params string[] keys)
    {
        Need(row.ValueKind == JsonValueKind.Object);
        var required = keys.Where(key => !key.EndsWith('?')).ToHashSet(StringComparer.Ordinal);
        var allowed = keys.Select(key => key.TrimEnd('?')).ToHashSet(StringComparer.Ordinal);
        var actual = row.EnumerateObject().Select(property => property.Name).ToArray();
        Need(actual.All(allowed.Contains) && required.IsSubsetOf(actual) && actual.Length <= allowed.Count);
    }

    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool okay)
    { if (!okay) throw new InvalidOperationException(Invalid); }
}
