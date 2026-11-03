using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Two named Core-principal stages; no default approver, amount limit or Finance rule.</summary>
public static class PlanReviewPolicyRules
{
    public const string PolicyKey = "PROCUREMENT_PLAN_REVIEW";

    public sealed record Stage(string Purpose, string Mode, int RequiredApprovals,
        IReadOnlyList<Guid> AssigneePrincipalIds);

    public sealed record Definition(IReadOnlyList<Stage> Stages);

    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32_768) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("stages", out var stages)
                && stages.ValueKind == JsonValueKind.Array
                && stages.EnumerateArray().Any(stage => stage.ValueKind == JsonValueKind.Object
                    && stage.TryGetProperty("purpose", out var purpose)
                    && purpose.ValueKind == JsonValueKind.String
                    && purpose.GetString() is "Review" or "Final");
        }
        catch (JsonException) { return false; }
    }

    public static Definition? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32_768) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 12,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !Only(root, "schemaVersion", "stages") ||
                !root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || version.GetInt32() != 2 ||
                !root.TryGetProperty("stages", out var stages) ||
                stages.ValueKind != JsonValueKind.Array || stages.GetArrayLength() != 2) return null;
            var result = new List<Stage>(2);
            var purposes = new[] { "Review", "Final" };
            for (var i = 0; i < 2; i++)
            {
                var stage = stages[i];
                if (stage.ValueKind != JsonValueKind.Object ||
                    !Only(stage, "purpose", "mode", "requiredApprovals", "assigneePrincipalIds") ||
                    !stage.TryGetProperty("purpose", out var purpose) ||
                    purpose.ValueKind != JsonValueKind.String || purpose.GetString() != purposes[i] ||
                    !stage.TryGetProperty("mode", out var mode) || mode.ValueKind != JsonValueKind.String ||
                    mode.GetString() is not ("Any" or "All" or "Quorum") ||
                    !stage.TryGetProperty("requiredApprovals", out var required) ||
                    required.ValueKind != JsonValueKind.Number || !required.TryGetInt32(out var quorum) ||
                    !stage.TryGetProperty("assigneePrincipalIds", out var people) ||
                    people.ValueKind != JsonValueKind.Array || people.GetArrayLength() is < 1 or > 20)
                    return null;
                var ids = new List<Guid>(people.GetArrayLength());
                foreach (var value in people.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.String ||
                        !Guid.TryParseExact(value.GetString(), "D", out var id) || id == Guid.Empty ||
                        value.GetString() != id.ToString("D")) return null;
                    ids.Add(id);
                }
                if (ids.Count != ids.Distinct().Count() || quorum < 1 || quorum > ids.Count ||
                    mode.GetString() == "Any" && quorum != 1 ||
                    mode.GetString() == "All" && quorum != ids.Count) return null;
                result.Add(new Stage(purposes[i], mode.GetString()!, quorum, ids));
            }
            // The final approver is independently named; self approval is also
            // checked against the actual maker and linked Core aliases at submit/decide.
            if (result[0].AssigneePrincipalIds.Intersect(result[1].AssigneePrincipalIds).Any()) return null;
            return new(result);
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        { return null; }
    }

    public static IReadOnlyList<string> Validate(string? json) => Read(json) is null
        ? ["Use schemaVersion 2 with exactly Review and Final stages, independent named Core principals, and valid quorum."]
        : [];

    private static bool Only(JsonElement value, params string[] keys) =>
        value.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal));
}
