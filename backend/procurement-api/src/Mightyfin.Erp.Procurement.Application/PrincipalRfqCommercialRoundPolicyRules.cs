using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalRfqCommercialApprovalStage(string Code, string Mode,
    IReadOnlyList<string> PrincipalIds);

public sealed record PrincipalRfqCommercialRoundPolicy(int SchemaVersion,
    int IdentitySchemaVersion, string SubmissionMode, bool RequireOriginalOfferDocument,
    bool RequireIndependentReview, string ClosingOfficerPrincipalId,
    IReadOnlyList<PrincipalRfqCommercialApprovalStage> Stages, string RulesSha256);

/// <summary>Strict explicit controls for a distinct commercial review. A parsed
/// document does not publish policy, prove Core authority or open a round.</summary>
public static class PrincipalRfqCommercialRoundPolicyRules
{
    public const string PolicyKey = "SOURCING_COMMERCIAL_ROUND";
    public const string TransactionType = "RfqCommercialRound";
    public const string ApprovalAction = "procurement-sourcing-commercial-approve";
    public const string RequestAction = "procurement-sourcing-commercial-configure";

    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 32_768)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("transactionType", out var kind)
                && kind.ValueKind == JsonValueKind.String
                && kind.GetString() == TransactionType;
        }
        catch (JsonException) { return false; }
    }

    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        try { _ = Parse(json); return []; }
        catch (InvalidOperationException error)
        { return [new PolicyRuleIssue("$", error.Message)]; }
    }

    public static PrincipalRfqCommercialRoundPolicy Parse(string? json)
    {
        try
        {
            Need(!string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json) <= 32_768,
                "RfqCommercialRoundPolicyInvalid");
            // Shared canonical codec rejects duplicate properties at every depth.
            var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(json!);
            using var document = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement;
            Only(root, "schemaVersion", "identitySchemaVersion", "policyKey",
                "transactionType", "submissionMode", "requireOriginalOfferDocument",
                "requireIndependentReview", "closingOfficerPrincipalId", "stages");
            Need(root.GetProperty("schemaVersion").GetInt32() == 1
                && root.GetProperty("identitySchemaVersion").GetInt32() == 2
                && Text(root, "policyKey") == PolicyKey
                && Text(root, "transactionType") == TransactionType
                && Text(root, "submissionMode") == "ManualSurrogateOnly"
                && root.GetProperty("requireOriginalOfferDocument").ValueKind == JsonValueKind.True
                && root.GetProperty("requireIndependentReview").ValueKind == JsonValueKind.True,
                "RfqCommercialRoundPolicyInvalid");
            var closer = Text(root, "closingOfficerPrincipalId");
            Need(ProcurementPrincipalEvidence.Canonical(closer),
                "RfqCommercialRoundCloserRequired");
            var rows = root.GetProperty("stages");
            Need(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() is >= 1 and <= 20,
                "RfqCommercialRoundStagesRequired");
            var stages = new List<PrincipalRfqCommercialApprovalStage>();
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.EnumerateArray())
            {
                Only(row, "code", "mode", "principalIds");
                var code = Text(row, "code");
                var mode = Text(row, "mode");
                Need(code is { Length: >= 1 and <= 64 }
                    && code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                    && codes.Add(code) && mode is "One" or "All",
                    "RfqCommercialRoundStageInvalid");
                var people = row.GetProperty("principalIds");
                Need(people.ValueKind == JsonValueKind.Array
                    && people.GetArrayLength() is >= 1 and <= 100,
                    "RfqCommercialRoundReviewersRequired");
                var principals = people.EnumerateArray().Select(x =>
                {
                    Need(x.ValueKind == JsonValueKind.String,
                        "RfqCommercialRoundReviewerInvalid");
                    var value = x.GetString()!;
                    Need(ProcurementPrincipalEvidence.Canonical(value),
                        "RfqCommercialRoundReviewerInvalid");
                    return value;
                }).ToArray();
                Need(principals.Distinct(StringComparer.Ordinal).Count() == principals.Length,
                    "RfqCommercialRoundDuplicateReviewer");
                stages.Add(new(code, mode, Array.AsReadOnly(principals)));
            }
            var allReviewers = stages.SelectMany(x => x.PrincipalIds).ToArray();
            Need(allReviewers.Length <= 100
                && allReviewers.Distinct(StringComparer.Ordinal).Count() == allReviewers.Length,
                "RfqCommercialRoundReviewerSetInvalid");
            Need(!stages.SelectMany(x => x.PrincipalIds).Contains(closer, StringComparer.Ordinal),
                "RfqCommercialRoundCloserMustBeIndependent");
            return new(1, 2, "ManualSurrogateOnly", true, true, closer,
                stages.AsReadOnly(), digest);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        {
            if (error is InvalidOperationException invalid
                && invalid.Message.StartsWith("RfqCommercialRound", StringComparison.Ordinal))
                throw;
            throw new InvalidOperationException("RfqCommercialRoundPolicyInvalid", error);
        }
    }

    public static PrincipalRfqCommercialRoundPolicy ResolveEffective(
        PolicyVersion policy, string tenantId, Guid companyId, DateTimeOffset now)
    {
        Need(policy is not null && policy.TenantId == tenantId
            && policy.LegalEntityId == companyId && companyId != Guid.Empty
            && policy.PolicyKey == PolicyKey && policy.PolicyVersionNumber > 0
            && policy.State == "Published" && policy.EffectiveFrom <= now
            && (policy.EffectiveTo is null || now < policy.EffectiveTo)
            && policy.PublishedAt is { } published && published <= now
            && ProcurementPrincipalEvidence.Canonical(policy.CreatedBy)
            && ProcurementPrincipalEvidence.Canonical(policy.PublishedBy)
            && policy.CreatedBy != policy.PublishedBy
            && !string.IsNullOrWhiteSpace(policy.PublicationReason),
            "RfqCommercialRoundPublishedPolicyRequired");
        return Parse(policy.RulesJson);
    }

    private static string Text(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        Need(value.ValueKind == JsonValueKind.String,
            "RfqCommercialRoundPolicyInvalid");
        return value.GetString()!;
    }

    private static void Only(JsonElement value, params string[] names)
    {
        Need(value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)),
            "RfqCommercialRoundPolicyInvalid");
    }

    private static void Need(bool okay, string code)
    {
        if (!okay) throw new InvalidOperationException(code);
    }
}
