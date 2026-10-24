using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Tests;

internal static class ContractReviewEvidenceFixture
{
    // Reconstruct altered stored evidence without making production init-only
    // fields mutable. Every other field, including identity, stays unchanged.
    internal static T Replace<T>(T original, string property, object? value)
    {
        var stored = JsonSerializer.SerializeToNode(original)!.AsObject();
        Assert.True(stored.ContainsKey(property));
        stored[property] = JsonSerializer.SerializeToNode(value);
        return stored.Deserialize<T>()!;
    }
}
