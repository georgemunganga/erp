using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Narrows an existing authority-eligible pool. A selected person or role grants no authority.</summary>
public sealed record PurchaseRequestApproverBinding(string Kind, string Key)
{
    [JsonIgnore]
    public bool IsValid => Kind switch
    {
        "CoreUser" => Guid.TryParseExact(Key, "D", out var id) && id != Guid.Empty && Key == id.ToString("D"),
        "CoreRole" => ValidRoleKey(Key),
        _ => false,
    };

    public bool Matches(string principalId, IReadOnlyCollection<string> currentRoles) => IsValid
        && (Kind == "CoreUser" ? Key == principalId : currentRoles.Contains(Key, StringComparer.Ordinal));

    public static bool ValidRoleKey(string? key) => !string.IsNullOrEmpty(key) && key.Length <= 100
        && key[0] is >= 'a' and <= 'z'
        && key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    public static PurchaseRequestApproverBinding? ReadStage(JsonElement stage, bool required = false)
    {
        if (!stage.TryGetProperty("binding", out var value))
        {
            if (required) throw new InvalidOperationException("ApprovalBindingRequired");
            return null;
        }
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 2
            || !value.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("ApprovalBindingInvalid");
        var result = new PurchaseRequestApproverBinding(kind.GetString()!, key.GetString()!);
        if (!result.IsValid || stage.GetProperty("assignee").GetString() != "ProcurementManager")
            throw new InvalidOperationException("ApprovalBindingInvalid");
        return result;
    }
}
