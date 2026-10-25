using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Explicit adopted-policy choices, never current Core authority. Zero
/// delay and false flags are valid choices; absent controls mean unavailable.</summary>
public sealed record PrincipalRfqOfferOpeningControls(string OfficerPrincipalId,
    IReadOnlyList<string> ReaderPrincipalIds, bool AllowClosingOfficer,
    int DelayAfterCloseSeconds, bool ReasonRequired, bool ReferenceRequired)
{
    public const string OpenAction = "procurement-sourcing-offers-open";
    public const string ReadAction = "procurement-sourcing-opened-offer-read";

    internal static PrincipalRfqOfferOpeningControls Parse(JsonElement row)
    {
        string[] names = ["officerPrincipalId", "readerPrincipalIds",
            "allowClosingOfficer", "delayAfterCloseSeconds", "reasonRequired", "referenceRequired"];
        if (row.ValueKind != JsonValueKind.Object || !row.EnumerateObject()
            .Select(x => x.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("RfqCommercialRoundOpeningControlsRequired");
        var officer = row.GetProperty("officerPrincipalId");
        var readers = row.GetProperty("readerPrincipalIds");
        var delay = row.GetProperty("delayAfterCloseSeconds").GetInt32();
        if (officer.ValueKind != JsonValueKind.String
            || !ProcurementPrincipalEvidence.Canonical(officer.GetString())
            || readers.ValueKind != JsonValueKind.Array || readers.GetArrayLength() is < 1 or > 100
            || delay is < 0 or > 31_536_000)
            throw new InvalidOperationException("RfqCommercialRoundOpeningControlsInvalid");
        var ids = readers.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String
            ? x.GetString()! : "").ToArray();
        if (ids.Any(x => !ProcurementPrincipalEvidence.Canonical(x))
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidOperationException("RfqCommercialRoundOpeningReadersInvalid");
        static bool Flag(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.True => true, JsonValueKind.False => false,
            _ => throw new InvalidOperationException("RfqCommercialRoundOpeningFlagRequired"),
        };
        return new(officer.GetString()!, Array.AsReadOnly(ids.Order(StringComparer.Ordinal).ToArray()),
            Flag(row.GetProperty("allowClosingOfficer")), delay,
            Flag(row.GetProperty("reasonRequired")), Flag(row.GetProperty("referenceRequired")));
    }

    public string Snapshot() => JsonSerializer.Serialize(this);

    public static bool MatchesRoundSnapshot(string snapshot, PrincipalRfqCommercialRoundPolicy rules)
    {
        using var json = JsonDocument.Parse(snapshot);
        var present = json.RootElement.TryGetProperty("offerOpening", out var pin);
        return rules.SchemaVersion == 1 ? !present && rules.OfferOpening is null
            : rules.SchemaVersion == 2 && rules.OfferOpening is not null && present
                && pin.GetRawText() == rules.OfferOpening.Snapshot();
    }
}
