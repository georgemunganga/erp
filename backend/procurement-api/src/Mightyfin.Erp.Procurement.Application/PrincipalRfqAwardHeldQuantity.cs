using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

// Pure candidate only. The owning adapter must load a COMPLETE scoped claim set,
// original P29 reservation, Core history and legacy consumers after sorted source
// locks in one serializable transaction. These arguments do not prove DB origin.
public static class PrincipalRfqAwardHeldQuantity
{
    public sealed record OriginalHold(SourcingLine Line, SourcingDemandBinding Binding,
        PurchaseRequestAllocation Allocation);
    public sealed record Claim(PrincipalRfqRecommendationSubmission Frozen,
        PrincipalRfqAwardReviewSubmission Review,
        IReadOnlyList<PrincipalRfqAwardReviewLine> Lines,
        PrincipalRfqAwardReviewReceipt Receipt,
        PrincipalRfqAwardReviewOutcome? Outcome,
        ApprovalCase CoreCase, PolicyVersion Policy,
        IReadOnlyList<ApprovalDecision> Decisions);
    public sealed record Balance(string HeldQuantityExact, string ClaimedQuantityExact,
        string RemainingQuantityExact);

    public static Balance Evaluate(OriginalHold source, IReadOnlyList<Claim> claims,
        IReadOnlyList<SourcingAward> legacyAwards,
        IReadOnlyList<SourcingAwardLine> legacyLines,
        IReadOnlyList<PurchaseOrder> legacyAwardOrders)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(legacyAwards);
        ArgumentNullException.ThrowIfNull(legacyLines);
        ArgumentNullException.ThrowIfNull(legacyAwardOrders);
        var line = source.Line;
        var binding = source.Binding;
        var allocation = source.Allocation;
        Need(line.Id != Guid.Empty && line.SourcingEventId != Guid.Empty
            && line.PurchaseRequestLineId is { } requestLine && requestLine != Guid.Empty
            && line.Status == "Active" && !string.IsNullOrWhiteSpace(line.TenantId)
            && line.LegalEntityId != Guid.Empty
            && binding.TenantId == line.TenantId && binding.LegalEntityId == line.LegalEntityId
            && binding.SourcingEventId == line.SourcingEventId && binding.SourcingLineId == line.Id
            && binding.PurchaseRequestLineId == requestLine
            && binding.AllocationId != Guid.Empty && binding.AllocationId == allocation.Id
            && binding.TargetType == "SourcingEvent"
            && allocation.TenantId == line.TenantId && allocation.LegalEntityId == line.LegalEntityId
            && allocation.PurchaseRequestId == binding.PurchaseRequestId
            && allocation.PurchaseRequestLineId == requestLine
            && allocation.TargetType == "SourcingEvent"
            && allocation.TargetId == line.SourcingEventId && allocation.Status == "Reserved",
            "RfqAwardOriginalHoldChanged");
        var held = Units(Exact(line.Quantity));
        Need(held > 0 && held == Units(Exact(binding.Quantity))
            && held == Units(Exact(allocation.Quantity)), "RfqAwardOriginalHoldChanged");
        // Legacy award-backed PO and new P38 claims have no reviewed handoff.
        // Never silently subtract both or treat an old status as a release.
        Need(legacyAwards.All(x => x is not null && x.Id != Guid.Empty
                && x.TenantId == line.TenantId && x.LegalEntityId == line.LegalEntityId
                && x.SourcingEventId == line.SourcingEventId)
            && legacyLines.All(x => x is not null && x.Id != Guid.Empty
                && x.TenantId == line.TenantId && x.LegalEntityId == line.LegalEntityId
                && legacyAwards.Any(a => a.Id == x.SourcingAwardId))
            && legacyAwardOrders.All(x => x is not null
                && x.TenantId == line.TenantId && x.LegalEntityId == line.LegalEntityId),
            "RfqAwardLegacyScopeChanged");
        Need(legacyAwards.Count == 0 && legacyLines.Count == 0
            && legacyAwardOrders.All(x => x.SourcingAwardId is null),
            "RfqAwardLegacyHandoffRequired");
        Need(claims.Count <= 10000 && claims.All(x => x is not null),
            "RfqAwardClaimSetInvalid");
        var ids = new HashSet<Guid>();
        var coreIds = new HashSet<Guid>();
        var frozenIds = new HashSet<Guid>();
        var receiptIds = new HashSet<Guid>();
        var lineIds = new HashSet<Guid>();
        var keys = new HashSet<(string Actor, Guid Key)>();
        BigInteger used = 0;
        foreach (var claim in claims)
        {
            var frozen = claim.Frozen;
            var review = claim.Review;
            var receipt = claim.Receipt;
            var core = claim.CoreCase;
            Need(frozen is not null && review is not null && receipt is not null
                && core is not null && claim.Policy is not null && claim.Decisions is not null
                && claim.Lines is { Count: > 0 and <= 500 }
                && review.Id != Guid.Empty && core.Id != Guid.Empty
                && frozen.Id != Guid.Empty && receipt.Id != Guid.Empty
                && ids.Add(review.Id) && coreIds.Add(core.Id)
                && frozenIds.Add(frozen.Id) && receiptIds.Add(receipt.Id)
                && claim.Lines.All(x => x is not null && x.Id != Guid.Empty
                    && lineIds.Add(x.Id))
                && keys.Add((review.MakerPrincipalId, review.CommandKey)),
                "RfqAwardClaimDuplicateOrOrphan");
            Need(review.CoreCaseId == core.Id
                && ProcurementPrincipalEvidence.Canonical(review.MakerPrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(review.MakerWorkerAliasId)
                && review.CommandKey != Guid.Empty
                && Hash(review.RequestSha256) && Hash(review.RecommendationSha256)
                && Hash(review.PolicyRulesSha256) && Hash(review.AssignmentPinsSha256)
                && review.SubmittedAt != default
                && review.CreatedBy == review.MakerPrincipalId
                && review.CreatedAt == review.SubmittedAt && review.Version == 1
                && review.TenantId == line.TenantId && review.LegalEntityId == line.LegalEntityId
                && review.SourcingEventId == line.SourcingEventId
                && frozen.TenantId == review.TenantId && frozen.LegalEntityId == review.LegalEntityId
                && frozen.Id == review.RecommendationSubmissionId
                && frozen.AwardReviewSubmissionId == review.Id
                && frozen.DraftId == review.DraftId
                && frozen.RevisionId == review.RecommendationRevisionId
                && frozen.Revision == review.RecommendationRevision
                && frozen.RevisionSha256 == review.RecommendationSha256
                && frozen.SourcingEventId == review.SourcingEventId
                && frozen.RoundId == review.RoundId && frozen.UnsealingId == review.UnsealingId
                && frozen.BranchId == review.BranchId
                && frozen.MakerPrincipalId == review.MakerPrincipalId
                && frozen.MakerWorkerAliasId == review.MakerWorkerAliasId
                && frozen.CommandKey != Guid.Empty && frozen.CommandKey == review.CommandKey
                && Hash(frozen.RequestSha256) && frozen.RequestSha256 == review.RequestSha256
                && Hash(frozen.RevisionSha256) && Hash(frozen.SnapshotSha256)
                && frozen.CreatedBy == review.MakerPrincipalId
                && frozen.CreatedAt == frozen.SubmittedAt
                && frozen.SubmittedAt == review.SubmittedAt && frozen.Version == 1
                && frozen.SnapshotSha256 == ProcurementEvidenceEncoding.Digest(frozen.SnapshotJson)
                && frozen.SnapshotSha256 == review.RecommendationSha256
                && receipt.SubmissionId == review.Id
                && receipt.RecommendationSubmissionId == frozen.Id
                && receipt.TenantId == review.TenantId
                && receipt.LegalEntityId == review.LegalEntityId
                && receipt.RoundId == review.RoundId
                && receipt.SourcingEventId == review.SourcingEventId
                && receipt.BranchId == review.BranchId
                && receipt.ActorPrincipalId == review.MakerPrincipalId
                && receipt.CommandKey != Guid.Empty && receipt.CommandKey == review.CommandKey
                && Hash(receipt.RequestSha256) && Hash(receipt.ResponseSha256)
                && receipt.RequestSha256 == review.RequestSha256
                && receipt.CreatedBy == review.MakerPrincipalId
                && receipt.CreatedAt == review.SubmittedAt && receipt.Version == 1
                && receipt.ResponseJson == PrincipalRfqAwardReviewWorkflow.ResponseFor(review)
                && receipt.ResponseSha256 == ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
                && claim.Lines.Any(x => x.SourcingLineId == line.Id)
                && LinesMatch(review, frozen, claim.Lines),
                "RfqAwardClaimGraphChanged");
            // One Core owner/policy/pins/decision replay serves both unfinished
            // reservations and terminal releases. Null is verified Pending.
            var terminal = PrincipalRfqAwardCoreHistory.VerifyRetained(review, core,
                claim.Policy, claim.Decisions);
            var released = false;
            if (claim.Outcome is { } outcome)
            {
                Need(terminal is not null
                    && outcome.TenantId == terminal.TenantId
                    && outcome.LegalEntityId == terminal.LegalEntityId
                    && outcome.SubmissionId == terminal.SubmissionId
                    && outcome.CoreCaseId == terminal.CoreCaseId
                    && outcome.TerminalDecisionId == terminal.TerminalDecisionId
                    && outcome.CoreState == terminal.CoreState
                    && outcome.SourceOutcome == terminal.SourceOutcome
                    && outcome.RecordedAt == terminal.RecordedAt,
                    "RfqAwardCoreOutcomeChanged");
                released = outcome.SourceOutcome is "Returned" or "Rejected";
            }
            // A terminal Core case without the owned outcome still consumes hold.
            // CoreApprovedPendingAward also consumes until a future reviewed handoff.
            if (!released)
                foreach (var selected in claim.Lines.Where(x => x.SourcingLineId == line.Id))
                    used += Units(selected.QuantityExact);
        }
        Need(used <= held, "RfqAwardHeldCapacityExceeded");
        return new(Canonical(held), Canonical(used), Canonical(held - used));
    }

    private static bool LinesMatch(PrincipalRfqAwardReviewSubmission review,
        PrincipalRfqRecommendationSubmission frozen,
        IReadOnlyList<PrincipalRfqAwardReviewLine> lines)
    {
        try
        {
            using var parsed = JsonDocument.Parse(frozen.SnapshotJson,
                new JsonDocumentOptions { MaxDepth = 32 });
            var selected = parsed.RootElement.GetProperty("lines");
            if (selected.ValueKind != JsonValueKind.Array || selected.GetArrayLength() != lines.Count)
                return false;
            var expected = selected.EnumerateArray().Select(x => (
                OfferId: x.GetProperty("OfferId").GetGuid(),
                OfferSha256: x.GetProperty("OfferSha256").GetString(),
                SupplierId: x.GetProperty("SupplierId").GetGuid(),
                SourcingLineId: x.GetProperty("SourcingLineId").GetGuid(),
                QuantityExact: x.GetProperty("QuantityExact").GetString(),
                CurrencyCode: x.GetProperty("CurrencyCode").GetString()))
                .OrderBy(x => x.SourcingLineId).ThenBy(x => x.OfferId).ToArray();
            var actual = lines.Select(x => (x.OfferId, x.OfferSha256, x.SupplierId,
                x.SourcingLineId, x.QuantityExact, x.CurrencyCode))
                .OrderBy(x => x.SourcingLineId).ThenBy(x => x.OfferId).ToArray();
            return expected.SequenceEqual(actual)
                && lines.All(x => x is not null && x.CreatedBy == review.MakerPrincipalId
                    && x.CreatedAt == review.SubmittedAt && x.Version == 1)
                && lines.All(x => x.TenantId == review.TenantId
                    && x.LegalEntityId == review.LegalEntityId
                    && x.SubmissionId == review.Id
                    && x.RecommendationSubmissionId == frozen.Id
                    && x.RoundId == review.RoundId
                    && x.UnsealingId == review.UnsealingId
                    && x.SourcingEventId == review.SourcingEventId
                    && x.BranchId == review.BranchId
                    && x.OfferId != Guid.Empty && x.SupplierId != Guid.Empty
                    && x.SourcingLineId != Guid.Empty
                    && Units(x.QuantityExact) > 0)
                && lines.Select(x => x.Id).Distinct().Count() == lines.Count
                && lines.Select(x => (x.OfferId, x.SourcingLineId)).Distinct().Count() == lines.Count
                && lines.All(x => x.CurrencyCode == review.CurrencyCode
                    && MoneyUnits(x.RetainedQuoteTotalExact) > 0)
                && MoneyUnits(review.ProposedTotalExact) > 0
                && lines.Aggregate(BigInteger.Zero, (sum, x) => sum + MoneyUnits(x.RetainedQuoteTotalExact))
                    == MoneyUnits(review.ProposedTotalExact);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException
            or FormatException or InvalidOperationException)
        { return false; }
    }

    private static BigInteger MoneyUnits(string? exact)
    {
        Need(exact is { Length: > 0 and <= 40 } && exact[0] is >= '0' and <= '9'
            && exact.All(x => x is >= '0' and <= '9' or '.')
            && exact.Count(x => x == '.') <= 1, "RfqAwardExactMoneyInvalid");
        var parts = exact.Split('.');
        Need(parts[0].Length <= 30 && (parts[0].Length == 1 || parts[0][0] != '0')
            && (parts.Length == 1 || parts[1].Length is > 0 and <= 8),
            "RfqAwardExactMoneyInvalid");
        var value = BigInteger.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture)
            * 100000000 + (parts.Length == 1 ? BigInteger.Zero
                : BigInteger.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture)
                    * BigInteger.Pow(10, 8 - parts[1].Length));
        Need(exact == CanonicalMoney(value), "RfqAwardExactMoneyInvalid");
        return value;
    }
    private static string CanonicalMoney(BigInteger units)
    {
        var whole = units / 100000000;
        var fraction = (units % 100000000).ToString(CultureInfo.InvariantCulture)
            .PadLeft(8, '0').TrimEnd('0');
        return fraction.Length == 0 ? whole.ToString(CultureInfo.InvariantCulture)
            : whole.ToString(CultureInfo.InvariantCulture) + "." + fraction;
    }

    private static string Exact(decimal value) => value.ToString("0.############################",
        CultureInfo.InvariantCulture);
    private static BigInteger Units(string? exact)
    {
        // Reuse the owning 18,4 codec; unlike money 38,8, all quantity values
        // fit System.Decimal. The canonical text check rejects its permissive
        // transport spellings before values enter an immutable claim.
        try
        {
            var value = ExactDecimalJsonCodec.Parse(exact, 18, 4, "RfqAwardQuantity");
            var scaled = ExactDecimalArithmetic.Multiply(value, 10000m);
            var units = new BigInteger(scaled);
            Need(scaled == decimal.Truncate(scaled) && exact == Canonical(units),
                "RfqAwardExactQuantityInvalid");
            return units;
        }
        catch (JsonException error)
        { throw new InvalidOperationException("RfqAwardExactQuantityInvalid", error); }
    }
    private static bool Hash(string? text) => text is { Length: 64 }
        && text.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Canonical(BigInteger units)
    {
        var whole = units / 10000;
        var fraction = (units % 10000).ToString(CultureInfo.InvariantCulture)
            .PadLeft(4, '0').TrimEnd('0');
        return fraction.Length == 0 ? whole.ToString(CultureInfo.InvariantCulture)
            : whole.ToString(CultureInfo.InvariantCulture) + "." + fraction;
    }
    private static void Need(bool okay, string code)
    { if (!okay) throw new InvalidOperationException(code); }
}
