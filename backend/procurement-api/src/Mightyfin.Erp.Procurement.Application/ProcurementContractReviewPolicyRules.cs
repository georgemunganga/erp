using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed configuration for internal contract review only. This does not
/// attest signature, activate terms, or grant a Core approval action.</summary>
public static class ProcurementContractReviewPolicyRules
{
    public const string PolicyKey = "SUPPLIER_CONTRACT_REVIEW";
    public const string TransactionType = "SupplierContractReview";
    public sealed record Stage(string Code, string Mode, IReadOnlyList<string> PrincipalIds);
    public sealed record Document(int SchemaVersion, IReadOnlyList<Stage> Stages);

    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 65536) return false;
        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            return parsed.RootElement.ValueKind == JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("transactionType", out var kind)
                && kind.ValueKind == JsonValueKind.String && kind.GetString() == TransactionType;
        }
        catch (JsonException) { return false; }
    }

    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json, bool draft = false)
    {
        try { _ = Parse(json, draft); return []; }
        catch (Exception error) when (error is InvalidOperationException or JsonException or FormatException
            or KeyNotFoundException or OverflowException)
        { return [new("$", "Enter named Core reviewers in one or more ordered stages, with One or All decisions.")]; }
    }

    public static Document Parse(string? json, bool draft = false)
    {
        Require(!string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json) <= 65536);
        using var parsed = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 16 });
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(json!); // Canonical JSON rejects duplicate properties.
        var root = parsed.RootElement;
        Only(root, "schemaVersion", "transactionType", "stages");
        Require(root.GetProperty("schemaVersion").GetInt32() == 1
            && root.GetProperty("transactionType").GetString() == TransactionType);
        var rows = root.GetProperty("stages");
        Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() <= 16
            && (draft || rows.GetArrayLength() > 0));
        var stages = new List<Stage>();
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            Only(row, "code", "mode", "principalIds");
            var code = row.GetProperty("code").GetString();
            var mode = row.GetProperty("mode").GetString();
            Require(code is { Length: > 0 and <= 40 } && code[0] is >= 'A' and <= 'Z'
                && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                && mode is "One" or "All");
            var members = row.GetProperty("principalIds");
            Require(members.ValueKind == JsonValueKind.Array && members.GetArrayLength() <= 32
                && (draft || members.GetArrayLength() > 0));
            var ids = members.EnumerateArray().Select(x => x.GetString()).ToArray();
            Require(ids.All(Canonical) && all.Count + ids.Length <= 128);
            foreach (var id in ids) Require(all.Add(id!));
            stages.Add(new(code!, mode!, Array.AsReadOnly(ids.Select(x => x!).Order(StringComparer.Ordinal).ToArray())));
        }
        Require(stages.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == stages.Count);
        return new(1, stages.AsReadOnly());
    }

    private static bool Canonical(string? value) => Guid.TryParseExact(value, "D", out var id)
        && id != Guid.Empty && value == id.ToString("D");
    private static void Only(JsonElement row, params string[] keys) => Require(row.ValueKind == JsonValueKind.Object
        && row.EnumerateObject().Count() == keys.Length
        && row.EnumerateObject().All(x => keys.Contains(x.Name, StringComparer.Ordinal)));
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("ContractReviewPolicyInvalid"); }
}
