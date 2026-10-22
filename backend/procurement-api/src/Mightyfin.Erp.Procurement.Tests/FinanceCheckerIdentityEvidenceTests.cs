using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class FinanceCheckerIdentityEvidenceTests
{
    private static readonly Guid Worker = Guid.Parse("44444444-4444-4444-4444-444444444444");
    // Frozen before the new identity encoder: no new property, naming, ordering or decimal/date rewrite.
    private const string LegacyGolden = """
{"Id":"11111111-1111-1111-1111-111111111111","TenantId":"tenant-a","LegalEntityId":"22222222-2222-2222-2222-222222222222","PurchaseRequestId":"33333333-3333-3333-3333-333333333333","RequestVersion":4,"FinanceCheckRef":"CHECK-1","Status":"Available","checkedAmount":"25.9257","CurrencyCode":"ZMW","CheckedAt":"2026-10-20T10:00:00.123456+00:00","CheckedBySubjectId":"finance-user","EvidenceRef":"DOC-1","DecisionReason":null,"ReservationRef":"RESERVE-1","ReservationStatus":"Confirmed","ExchangeRateSource":null}
""";
    private const string LegacySha256 = "253f6801e5c79a6e48704a6eb79a67259dbaffd6374bea7ad2e41a85bdbc1b58";

    [Fact]
    public void Legacy_finance_bytes_and_digest_remain_exact_and_do_not_prove_a_worker()
    {
        var f = Snapshot();
        Assert.Equal(LegacyGolden, FinanceSnapshotCodec.Encode(f, 0));
        Assert.Equal(LegacySha256, ProcurementEvidenceEncoding.Digest(FinanceSnapshotCodec.Encode(f, 0)));
        Assert.True(FinanceSnapshotCodec.Matches(f, 0, LegacyGolden, LegacySha256));
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        var fullLegacy = JsonSerializer.Serialize(f);
        Assert.DoesNotContain("CheckerIdentitySchemaVersion", fullLegacy);
        Assert.DoesNotContain("CheckedByWorkerId", fullLegacy);
        Assert.DoesNotContain("CheckerIdentitySha256", fullLegacy);
        Assert.Equal(fullLegacy, JsonSerializer.Serialize(JsonSerializer.Deserialize<BudgetCheckSnapshot>(fullLegacy)));
        Assert.False(Matches(f, Worker));
        Assert.Throws<ArgumentException>(() => FinanceSnapshotCodec.Encode(f, 1));
    }

    [Fact]
    public void New_binding_preserves_exact_legacy_tuple_but_uses_explicit_separate_finance_version()
    {
        var f = Snapshot(); Bind(f);
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        var boundJson = FinanceSnapshotCodec.Encode(f, 1);
        using var json = JsonDocument.Parse(boundJson);
        Assert.Equal(1, json.RootElement.GetProperty("financeIdentitySchemaVersion").GetInt32());
        Assert.Equal(LegacyGolden, json.RootElement.GetProperty("finance").GetRawText());
        Assert.Equal(Worker, json.RootElement.GetProperty("CheckedByWorkerId").GetGuid());
        Assert.True(FinanceSnapshotCodec.Matches(f, 1, boundJson, ProcurementEvidenceEncoding.Digest(boundJson)));
        Assert.False(FinanceSnapshotCodec.Matches(f, 0, LegacyGolden, LegacySha256));
        Assert.Throws<ArgumentException>(() => FinanceSnapshotCodec.Encode(f, 0));
        // Encoding a bound snapshot must not recursively include its new digest in the financial tuple.
        Assert.Equal(LegacyGolden, FinanceSnapshotCodec.Legacy(f));
    }

    [Fact]
    public void Canonical_account_reassignment_does_not_reinterpret_retained_worker_or_allow_current_alias()
    {
        var f = Snapshot(); Bind(f);
        var before = JsonSerializer.Serialize(f);
        Assert.True(Matches(f, Worker));
        Assert.False(Matches(f, Guid.NewGuid())); // same subject now maps to another active permitted worker
        Assert.False(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f, f.TenantId, f.LegalEntityId,
            f.PurchaseRequestId, f.RequestVersion, "second-account-same-worker", Worker));
        Assert.Equal(before, JsonSerializer.Serialize(f));
        // Historic validity is independent of a live login lookup; operational activity is the caller's gate.
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(f));
    }

    [Fact]
    public void PostgreSQL_microseconds_UTC_and_decimal_scale_normalize_identically_without_amount_rounding()
    {
        var f = Snapshot(); Bind(f);
        var digest = f.CheckerIdentitySha256;
        var encoded = FinanceSnapshotCodec.Encode(f, 1);
        f.CheckedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(f.CheckedAt)!.Value;
        f.CheckedAmount = 25.925700m;
        Assert.Equal(digest, f.CheckerIdentitySha256);
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        Assert.Equal(encoded, FinanceSnapshotCodec.Encode(f, 1));
        f.CheckedAt = f.CheckedAt.ToOffset(TimeSpan.FromHours(2));
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        f.CheckedAt = f.CheckedAt.AddTicks(10);
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("snapshot")]
    [InlineData("request")]
    [InlineData("version")]
    [InlineData("subject")]
    [InlineData("worker")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("status")]
    [InlineData("evidence")]
    [InlineData("reservation")]
    [InlineData("hash")]
    [InlineData("schema")]
    public void Changed_bound_identity_scope_or_financial_tuple_never_verifies(string changed)
    {
        var f = Snapshot(); Bind(f);
        var json = FinanceSnapshotCodec.Encode(f, 1); var sha = ProcurementEvidenceEncoding.Digest(json);
        switch (changed)
        {
            case "tenant": f.TenantId = "other"; break;
            case "company": f.LegalEntityId = Guid.NewGuid(); break;
            case "snapshot": f.Id = Guid.NewGuid(); break;
            case "request": f.PurchaseRequestId = Guid.NewGuid(); break;
            case "version": f.RequestVersion++; break;
            case "subject": f.CheckedBySubjectId = "other"; break;
            case "worker": f.CheckedByWorkerId = Guid.NewGuid(); break;
            case "amount": f.CheckedAmount += .0001m; break;
            case "currency": f.CurrencyCode = "USD"; break;
            case "status": f.Status = "Insufficient"; break;
            case "evidence": f.EvidenceRef = "OTHER"; break;
            case "reservation": f.ReservationRef = "OTHER"; break;
            case "hash": f.CheckerIdentitySha256 = new string('f', 64); break;
            case "schema": f.CheckerIdentitySchemaVersion = 2; break;
        }
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        Assert.False(FinanceSnapshotCodec.Matches(f, 1, json, sha));
    }

    [Theory]
    [InlineData("missing-worker")]
    [InlineData("zero-worker")]
    [InlineData("missing-hash")]
    [InlineData("partial-legacy")]
    [InlineData("unknown")]
    public void Missing_partial_and_unknown_provenance_cannot_be_encoded_as_verified_or_downgraded(string kind)
    {
        var f = Snapshot(); Bind(f);
        switch (kind)
        {
            case "missing-worker": f.CheckedByWorkerId = null; break;
            case "zero-worker": f.CheckedByWorkerId = Guid.Empty; break;
            case "missing-hash": f.CheckerIdentitySha256 = null; break;
            case "partial-legacy": f.CheckerIdentitySchemaVersion = 0; break;
            case "unknown": f.CheckerIdentitySchemaVersion = 99; break;
        }
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        Assert.Throws<ArgumentException>(() => FinanceSnapshotCodec.Encode(f, f.CheckerIdentitySchemaVersion));
        Assert.False(FinanceSnapshotCodec.Matches(f, 0, LegacyGolden, LegacySha256));
    }

    [Fact]
    public void New_capture_does_not_backfill_stamped_history_accept_wrong_subject_or_round_financial_values()
    {
        var f = Snapshot(); f.CreatedAt = f.CheckedAt;
        Assert.Throws<ArgumentException>(() => FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f, f.CheckedBySubjectId, Worker));
        f.CreatedAt = default;
        Assert.Throws<ArgumentException>(() => FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f, "wrong", Worker));
        Assert.Throws<ArgumentException>(() => FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f, f.CheckedBySubjectId, Guid.Empty));
        f.CheckedAmount = .00005m;
        Assert.Throws<ArgumentException>(() => FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f, f.CheckedBySubjectId, Worker));
        Assert.Equal(0, f.CheckerIdentitySchemaVersion);
        Assert.Null(f.CheckedByWorkerId); Assert.Null(f.CheckerIdentitySha256);
    }

    [Fact]
    public void Current_verification_requires_exact_owned_scope_and_original_subject_even_with_valid_binding()
    {
        var f = Snapshot(); Bind(f);
        Assert.False(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f, "other", f.LegalEntityId, f.PurchaseRequestId, f.RequestVersion, f.CheckedBySubjectId, Worker));
        Assert.False(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f, f.TenantId, Guid.NewGuid(), f.PurchaseRequestId, f.RequestVersion, f.CheckedBySubjectId, Worker));
        Assert.False(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f, f.TenantId, f.LegalEntityId, Guid.NewGuid(), f.RequestVersion, f.CheckedBySubjectId, Worker));
        Assert.False(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f, f.TenantId, f.LegalEntityId, f.PurchaseRequestId, f.RequestVersion + 1, f.CheckedBySubjectId, Worker));
    }

    private static bool Matches(BudgetCheckSnapshot f, Guid worker) => FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(
        f, f.TenantId, f.LegalEntityId, f.PurchaseRequestId, f.RequestVersion, f.CheckedBySubjectId, worker);

    private static void Bind(BudgetCheckSnapshot f)
    {
        var binding = FinanceCheckerIdentityEvidence.CreateForNewSnapshot(f, f.CheckedBySubjectId, Worker);
        f.CheckerIdentitySchemaVersion = binding.SchemaVersion;
        f.CheckedByWorkerId = binding.WorkerId; f.CheckerIdentitySha256 = binding.Sha256;
    }

    private static BudgetCheckSnapshot Snapshot() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), TenantId = "tenant-a",
        LegalEntityId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        PurchaseRequestId = Guid.Parse("33333333-3333-3333-3333-333333333333"), RequestVersion = 4,
        FinanceCheckRef = "CHECK-1", Status = "Available", CheckedAmount = 25.925700m, CurrencyCode = "ZMW",
        CheckedAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)).AddTicks(1234567),
        CheckedBySubjectId = "finance-user", EvidenceRef = "DOC-1", ReservationRef = "RESERVE-1",
        ReservationStatus = "Confirmed",
    };
}
