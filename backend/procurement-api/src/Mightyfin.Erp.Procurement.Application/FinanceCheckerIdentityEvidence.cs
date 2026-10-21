using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Retained attribution, not a permission grant, external Finance signature or reservation proof.</summary>
public sealed record FinanceCheckerBinding(int SchemaVersion, Guid WorkerId, string Sha256);

public static class FinanceCheckerIdentityEvidence
{
    public const int BoundVersion = 1;

    /// <summary>
    /// Construct only for a NEW, not-yet-persisted snapshot, after the server resolves the exact
    /// active canonical subject/worker and checks current permission/independence in its transaction.
    /// This pure helper cannot establish those facts or authorize backfilling a historical row.
    /// </summary>
    public static FinanceCheckerBinding CreateForNewSnapshot(BudgetCheckSnapshot snapshot,
        string canonicalSubjectId, Guid canonicalWorkerId)
    {
        if (!HasCaptureTuple(snapshot) || snapshot.CreatedAt != default || snapshot.UpdatedAt is not null
            || canonicalWorkerId == Guid.Empty
            || canonicalSubjectId != snapshot.CheckedBySubjectId
            || snapshot.CheckerIdentitySchemaVersion != 0 || snapshot.CheckedByWorkerId is not null
            || snapshot.CheckerIdentitySha256 is not null)
            throw new ArgumentException("FinanceCheckerCaptureInvalid");
        return new(BoundVersion, canonicalWorkerId, IdentityDigest(snapshot, canonicalWorkerId));
    }

    public static bool HasValidBinding(BudgetCheckSnapshot snapshot) => HasCaptureTuple(snapshot)
        && snapshot.CheckerIdentitySchemaVersion == BoundVersion
        && snapshot.CheckedByWorkerId is { } worker && worker != Guid.Empty
        && snapshot.CheckerIdentitySha256 == IdentityDigest(snapshot, worker);

    /// <summary>Caller still owns current activity, permission, independence and exact authoritative scope.</summary>
    public static bool MatchesCurrentCanonical(BudgetCheckSnapshot snapshot, string tenant, Guid company,
        Guid requestId, long submittedVersion, string canonicalSubjectId, Guid canonicalWorkerId) =>
        HasValidBinding(snapshot) && snapshot.TenantId == tenant && snapshot.LegalEntityId == company
        && snapshot.PurchaseRequestId == requestId && snapshot.RequestVersion == submittedVersion
        && snapshot.CheckedBySubjectId == canonicalSubjectId && snapshot.CheckedByWorkerId == canonicalWorkerId
        && canonicalWorkerId != Guid.Empty;

    private static bool HasCaptureTuple(BudgetCheckSnapshot f) => f.Id != Guid.Empty
        && !string.IsNullOrWhiteSpace(f.TenantId) && f.LegalEntityId != Guid.Empty
        && f.PurchaseRequestId != Guid.Empty && f.RequestVersion > 0
        && !string.IsNullOrWhiteSpace(f.CheckedBySubjectId) && f.CheckedAt != default
        && !string.IsNullOrWhiteSpace(f.FinanceCheckRef) && !string.IsNullOrWhiteSpace(f.Status)
        && !string.IsNullOrWhiteSpace(f.CurrencyCode) && f.CheckedAmount >= 0
        && f.CheckedAmount <= 99_999_999_999_999.9999m && decimal.Round(f.CheckedAmount, 4) == f.CheckedAmount;

    private static string IdentityDigest(BudgetCheckSnapshot snapshot, Guid workerId) =>
        ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new
        {
            schemaVersion = BoundVersion, snapshot.Id, snapshot.TenantId, snapshot.LegalEntityId,
            snapshot.PurchaseRequestId, snapshot.RequestVersion, snapshot.CheckedBySubjectId,
            checkedByWorkerId = workerId,
            checkedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(snapshot.CheckedAt),
            financeTupleSha256 = ProcurementEvidenceEncoding.Digest(FinanceSnapshotCodec.Legacy(snapshot)),
        }));
}

/// <summary>Versioned explicit serialization. Legacy encoder must retain its original exact bytes.</summary>
public static class FinanceSnapshotCodec
{
    // Intentionally identical to the original PurchaseRequestApprovalPinning.FinanceSnapshot.
    // No new identity field is added to this representation, even for future serializers.
    public static string Legacy(BudgetCheckSnapshot f) => JsonSerializer.Serialize(new
    {
        f.Id, f.TenantId, f.LegalEntityId, f.PurchaseRequestId, f.RequestVersion, f.FinanceCheckRef,
        f.Status, checkedAmount = ProcurementEvidenceEncoding.Decimal(f.CheckedAmount), f.CurrencyCode,
        CheckedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(f.CheckedAt),
        f.CheckedBySubjectId, f.EvidenceRef, f.DecisionReason, f.ReservationRef,
        f.ReservationStatus, f.ExchangeRateSource,
    });

    public static string Encode(BudgetCheckSnapshot snapshot, int identitySchemaVersion)
    {
        if (identitySchemaVersion == 0 && snapshot.CheckerIdentitySchemaVersion == 0
            && snapshot.CheckedByWorkerId is null && snapshot.CheckerIdentitySha256 is null)
            return Legacy(snapshot);
        if (identitySchemaVersion == FinanceCheckerIdentityEvidence.BoundVersion
            && FinanceCheckerIdentityEvidence.HasValidBinding(snapshot))
        {
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                writer.WriteNumber("financeIdentitySchemaVersion", identitySchemaVersion);
                writer.WritePropertyName("finance");
                // This is the owned legacy encoder's valid JSON, never caller-supplied raw input.
                // Re-serializing JsonElement would escape '+' in the timestamp and change its bytes.
                writer.WriteRawValue(Legacy(snapshot), skipInputValidation: false);
                writer.WriteString(nameof(snapshot.CheckedByWorkerId), snapshot.CheckedByWorkerId!.Value);
                writer.WriteString(nameof(snapshot.CheckerIdentitySha256), snapshot.CheckerIdentitySha256);
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(output.ToArray());
        }
        throw new ArgumentException("FinanceCheckerIdentityMissingOrInvalid");
    }

    public static bool Matches(BudgetCheckSnapshot snapshot, int identitySchemaVersion, string? json, string? sha256)
    {
        if (json is null || sha256 != ProcurementEvidenceEncoding.Digest(json)) return false;
        try { return json == Encode(snapshot, identitySchemaVersion); }
        catch (ArgumentException) { return false; }
    }
}
