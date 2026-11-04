using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Submission-time role evidence is retained, never reconstructed for old requests.</summary>
public static class PurchaseRequestRoutingSnapshot
{
    public static bool Valid(JsonElement root, string? expectedPrincipal = null, DateTimeOffset? expectedSubmission = null)
    {
        if (!root.TryGetProperty("ruleSchemaVersion", out var version)) return true;
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 2
            || !root.TryGetProperty("requesterPrincipalId", out var principal) || principal.ValueKind != JsonValueKind.String
            || !new PurchaseRequestApproverBinding("CoreUser", principal.GetString()!).IsValid
            || expectedPrincipal is not null && principal.GetString() != expectedPrincipal
            || !root.TryGetProperty("requesterRoleSnapshotAt", out var timestamp) || timestamp.ValueKind != JsonValueKind.String
            || !timestamp.TryGetDateTimeOffset(out var at) || expectedSubmission is not null && ProcurementEvidenceEncoding.DatabaseTimestamp(at) != ProcurementEvidenceEncoding.DatabaseTimestamp(expectedSubmission)
            || !root.TryGetProperty("requesterRoleKeys", out var roles) || roles.ValueKind != JsonValueKind.Array
            || roles.GetArrayLength() > 100) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles.EnumerateArray())
            if (role.ValueKind != JsonValueKind.String || !PurchaseRequestApproverBinding.ValidRoleKey(role.GetString())
                || !seen.Add(role.GetString()!)) return false;
        return true;
    }
}
