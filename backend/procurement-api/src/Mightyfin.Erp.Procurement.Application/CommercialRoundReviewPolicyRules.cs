using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Published business configuration for an internal round-plan review.
/// Every required value is explicit; there is no default approver or threshold.</summary>
public static class CommercialRoundReviewPolicyRules
{
    public const string PolicyKey = "COMMERCIAL_ROUND_PLAN_REVIEW";

    public sealed record Definition(string Mode, int RequiredApprovals,
        IReadOnlyList<Guid> AssigneePrincipalIds, IReadOnlyList<Guid> MethodIds,
        IReadOnlyList<string> CategoryCodes, int MinimumCriteria,
        IReadOnlyList<string> RequiredCriterionStages, bool DocumentsRequired);

    public static Definition? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32768) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !Only(root, "schemaVersion", "mode",
                    "requiredApprovals", "assigneePrincipalIds", "methodIds", "categoryCodes",
                    "minimumCriteria", "requiredCriterionStages", "documentsRequired")
                || !root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number
                || version.GetInt32() != 1 || !root.TryGetProperty("mode", out var mode)
                || mode.ValueKind != JsonValueKind.String || mode.GetString() is not ("Any" or "All" or "Quorum")
                || !root.TryGetProperty("requiredApprovals", out var count) || !count.TryGetInt32(out var required)
                || !root.TryGetProperty("minimumCriteria", out var minimum) || !minimum.TryGetInt32(out var minimumCriteria)
                || minimumCriteria is < 1 or > 100
                || !root.TryGetProperty("documentsRequired", out var documents) ||
                   documents.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("assigneePrincipalIds", out var people) || people.ValueKind != JsonValueKind.Array
                || people.GetArrayLength() is < 1 or > 20
                || !root.TryGetProperty("methodIds", out var methods) || methods.ValueKind != JsonValueKind.Array
                || methods.GetArrayLength() is < 1 or > 20
                || !root.TryGetProperty("categoryCodes", out var categories) || categories.ValueKind != JsonValueKind.Array
                || categories.GetArrayLength() is < 1 or > 100
                || !root.TryGetProperty("requiredCriterionStages", out var stages) || stages.ValueKind != JsonValueKind.Array
                || stages.GetArrayLength() is < 1 or > 3) return null;
            var ids = new List<Guid>();
            foreach (var person in people.EnumerateArray())
            {
                if (person.ValueKind != JsonValueKind.String || !Guid.TryParseExact(person.GetString(), "D", out var id)
                    || id == Guid.Empty || person.GetString() != id.ToString("D")) return null;
                ids.Add(id);
            }
            var methodIds = new List<Guid>();
            foreach (var method in methods.EnumerateArray())
            {
                if (method.ValueKind != JsonValueKind.String || !Guid.TryParseExact(method.GetString(), "D", out var id)
                    || id == Guid.Empty || method.GetString() != id.ToString("D")) return null;
                methodIds.Add(id);
            }
            var categoryCodes = new List<string>();
            foreach (var category in categories.EnumerateArray())
            {
                if (category.ValueKind != JsonValueKind.String || category.GetString() is not { } code
                    || code.Length is < 1 or > 80 || code != code.Trim()
                    || code.Contains('*') && code != "*"
                    || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '*')))
                    return null;
                categoryCodes.Add(code);
            }
            var requiredStages = new List<string>();
            foreach (var stage in stages.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.String || stage.GetString() is not ("Mandatory" or "Technical" or "Commercial")) return null;
                requiredStages.Add(stage.GetString()!);
            }
            if (ids.Count != ids.Distinct().Count() || methodIds.Count != methodIds.Distinct().Count()
                || categoryCodes.Count != categoryCodes.Distinct(StringComparer.Ordinal).Count()
                || categoryCodes.Contains("*") && categoryCodes.Count != 1
                || requiredStages.Count != requiredStages.Distinct().Count()
                || required is < 1 or > 20 || required > ids.Count
                || mode.GetString() == "Any" && required != 1
                || mode.GetString() == "All" && required != ids.Count) return null;
            return new(mode.GetString()!, required, ids, methodIds, categoryCodes,
                minimumCriteria, requiredStages, documents.GetBoolean());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException)
        { return null; }
    }

    public static IReadOnlyList<string> Validate(string? json) => Read(json) is null
        ? ["Provide schemaVersion 1, named independent Core approvers, method/category scope, quorum, criteria stages and document requirement."]
        : [];

    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("requiredCriterionStages", out _)
                && document.RootElement.TryGetProperty("documentsRequired", out _);
        }
        catch (JsonException) { return false; }
    }

    private static bool Only(JsonElement value, params string[] keys) =>
        value.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal));
}
