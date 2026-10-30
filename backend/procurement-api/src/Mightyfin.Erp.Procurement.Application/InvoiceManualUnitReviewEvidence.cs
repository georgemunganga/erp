using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Schema 4 is a human attestation over entered values. It deliberately
/// has no document version, hash or availability claim.</summary>
public sealed record InvoiceManualUnitReviewEnvelope(int SchemaVersion, string EvidenceMode,
    string TenantId, Guid LegalEntityId, Guid? BranchId, Guid ReviewId,
    long ReviewSourceVersion, Guid InvoiceId, long InvoiceVersionAtReview,
    Guid MappingVersionId, int MappingRevision, string MappingSha256,
    string InvoiceOriginalSha256, Guid PurchaseOrderId, Guid IssuedVersionId,
    Guid MatchingPolicyId, int MatchingPolicyVersion, string MatchingPolicySha256,
    Guid ReviewPolicyId, int ReviewPolicyVersion, string ReviewPolicySha256,
    Guid CoreCaseId, Guid FinalDecisionId, string MakerPrincipalId,
    Guid? MakerWorkerId, string MakerReason, string ReviewerPrincipalId,
    Guid? ReviewerWorkerId, string ReviewerReason, DateTimeOffset ReviewedAt,
    IReadOnlyList<InvoiceReviewedManualLine> Lines,
    IReadOnlyList<InvoiceUnitReviewAssignmentWorker> AssignmentWorkers);

public static class InvoiceManualUnitReviewEvidence
{
    /// <summary>Reconstruct a completed manual review at its recorded completion
    /// time. Current command permissions and current policy publication are
    /// checked by the new command separately; neither rewrites historical Core
    /// completion or makes an expired review policy disappear.</summary>
    public static IReadOnlyDictionary<Guid, string> VerifyHistorical(
        InvoiceRetainedUnitReviewAuthority authority,
        InvoiceUnitReviewTransitions.Source source, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals, InvoiceMatchMappingVersion mapping,
        DateTimeOffset completedAt)
        => VerifyOwnedProof(authority, source, invoice, originals, mapping, completedAt,
            retainedHistoricalPublication: true);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false, NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16
    };

    public static IReadOnlyDictionary<Guid, string> Verify(
        InvoiceRetainedUnitReviewAuthority authority,
        InvoiceUnitReviewTransitions.Source source,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals,
        InvoiceMatchMappingVersion mapping, InvoiceUnitReviewConsumer consumer)
    {
        if (consumer is null || source is null || invoice is null || mapping is null
            || consumer.Permissions is null
            || consumer.AllowedBranches is null || consumer.DocumentVersionHashes is null
            || consumer.TenantId != invoice.TenantId
            || consumer.LegalEntityId != invoice.LegalEntityId
            || consumer.BranchId != invoice.BranchRef || consumer.BranchId == Guid.Empty
            || consumer.SelectedBranchId == Guid.Empty
            || consumer.SelectedBranchId is { } selected && selected != invoice.BranchRef
            || !consumer.OrganisationWide && invoice.BranchRef is { } branch
                && !consumer.AllowedBranches.Contains(branch)
            || !ProcurementPrincipalEvidence.Canonical(consumer.PrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(consumer.WorkerId)
            || !consumer.Permissions.Contains("procurement-access")
            || !consumer.Permissions.Contains("procurement-invoice-match")
            || consumer.DocumentVersionHashes.Count != 0)
            throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
        return VerifyOwnedProof(authority, source, invoice, originals, mapping, consumer.At);
    }

    internal static IReadOnlyDictionary<Guid, string> VerifyOwnedProof(
        InvoiceRetainedUnitReviewAuthority authority,
        InvoiceUnitReviewTransitions.Source source,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals,
        InvoiceMatchMappingVersion mapping, DateTimeOffset at,
        bool retainedHistoricalPublication = false)
    {
        try
        {
            if (authority is null || source is null || invoice is null || mapping is null
                || source.ReviewPolicy is null || source.Lines is null
                || originals is null || at == default || source.SourceSchemaVersion != 4
                || source.IdentitySchemaVersion != 3
                || source.TenantId != invoice.TenantId || source.LegalEntityId != invoice.LegalEntityId
                || source.BranchId != invoice.BranchRef || source.InvoiceId != invoice.Id
                || source.MappingId != mapping.Id || source.MappingRevision != mapping.MappingRevision
                || source.MappingSha256 != mapping.MappingSha256
                || source.OriginalSha256 != mapping.InvoiceOriginalSha256
                || source.PurchaseOrderId != mapping.PurchaseOrderId
                || source.IssuedVersionId != mapping.IssuedVersionId
                || source.ReviewPolicy.MakerPrincipalId != mapping.ActorPrincipalId
                || source.ReviewPolicy.MakerWorkerId != mapping.ActorWorkerId
                || source.ReviewPolicy.SchemaVersion != 2
                || source.ReviewPolicy.EvidenceMode != "ManualAttestation"
                || !CanonicalReason(source.ManualReason)
                || source.ManualLines is not { Count: > 0 and <= 200 }
                || source.ManualLines.Any(x => x is null) || source.Lines.Count != 0
                || authority.EvidenceJson is not { Length: > 0 and <= 262144 }
                || Encoding.UTF8.GetByteCount(authority.EvidenceJson) > 262144
                || ProcurementEvidenceEncoding.Digest(authority.EvidenceJson) != authority.EvidenceSha256
                || ProcurementEvidenceEncoding.PolicyJsonDigest(authority.EvidenceJson).Length != 64)
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            var evidence = JsonSerializer.Deserialize<InvoiceManualUnitReviewEnvelope>(authority.EvidenceJson, Json)
                ?? throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            if (JsonSerializer.Serialize(evidence, Json) != authority.EvidenceJson
                || evidence.SchemaVersion != 4 || evidence.EvidenceMode != "ManualAttestation"
                || evidence.TenantId != invoice.TenantId || evidence.LegalEntityId != invoice.LegalEntityId
                || evidence.BranchId != invoice.BranchRef || evidence.ReviewId != source.ReviewId
                || authority.ReviewId != evidence.ReviewId
                || evidence.ReviewSourceVersion != source.SourceVersion
                || authority.ReviewSourceVersion != evidence.ReviewSourceVersion
                || evidence.InvoiceId != invoice.Id
                || evidence.InvoiceVersionAtReview < source.InvoiceVersionAtOpen
                || evidence.InvoiceVersionAtReview > invoice.Version
                || evidence.MappingVersionId != mapping.Id || evidence.MappingRevision != mapping.MappingRevision
                || evidence.MappingSha256 != mapping.MappingSha256
                || evidence.InvoiceOriginalSha256 != mapping.InvoiceOriginalSha256
                || evidence.PurchaseOrderId != mapping.PurchaseOrderId
                || evidence.IssuedVersionId != mapping.IssuedVersionId
                || evidence.MatchingPolicyId != source.MatchingPolicyId
                || evidence.MatchingPolicyVersion != source.MatchingPolicyVersion
                || evidence.MatchingPolicySha256 != source.MatchingPolicySha256
                || evidence.ReviewPolicyId != source.ReviewPolicy.PolicyId
                || evidence.ReviewPolicyVersion != source.ReviewPolicy.PolicyVersion
                || evidence.ReviewPolicySha256 != source.ReviewPolicy.RulesSha256
                || evidence.CoreCaseId != source.CoreCaseId
                || evidence.MakerPrincipalId != source.ReviewPolicy.MakerPrincipalId
                || evidence.MakerWorkerId != source.ReviewPolicy.MakerWorkerId
                || evidence.MakerReason != source.ManualReason
                || evidence.ReviewedAt > at || evidence.ReviewedAt < source.OpenedAt
                || !CanonicalReason(evidence.ReviewerReason)
                || evidence.Lines is null || evidence.Lines.Any(x => x is null)
                || !evidence.Lines.SequenceEqual(source.ManualLines)
                || evidence.AssignmentWorkers is not { Count: > 0 and <= 128 }
                || evidence.AssignmentWorkers.Any(x => x is null))
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            var matching = authority.MatchingPolicy;
            var review = authority.ReviewPolicy;
            if (matching is null || review is null || matching.Id == review.Id
                || matching.TenantId != invoice.TenantId || matching.LegalEntityId != invoice.LegalEntityId
                || review.TenantId != invoice.TenantId || review.LegalEntityId != invoice.LegalEntityId
                || matching.PolicyKey != InvoiceMatchingPolicyRules.PolicyKey
                || review.PolicyKey != InvoiceRetainedUnitReview.ReviewPolicyKey
                || matching.Id != evidence.MatchingPolicyId || review.Id != evidence.ReviewPolicyId
                || matching.PolicyVersionNumber != evidence.MatchingPolicyVersion
                || review.PolicyVersionNumber != evidence.ReviewPolicyVersion
                || ProcurementEvidenceEncoding.PolicyJsonDigest(matching.RulesJson) != evidence.MatchingPolicySha256
                || ProcurementEvidenceEncoding.PolicyJsonDigest(review.RulesJson) != evidence.ReviewPolicySha256
                || InvoiceUnitReviewPolicyRules.Parse(review.RulesJson).SchemaVersion != 2
                || !(review.State == "Published" || retainedHistoricalPublication && review.State == "Superseded")
                || !(matching.State == "Published" || retainedHistoricalPublication && matching.State == "Superseded")
                || review.PublishedAt is null || review.PublishedAt > source.OpenedAt
                || matching.PublishedAt is null || matching.PublishedAt > source.OpenedAt
                || review.EffectiveFrom == default || review.EffectiveFrom > source.OpenedAt
                || matching.EffectiveFrom == default || matching.EffectiveFrom > source.OpenedAt
                || review.EffectiveTo is { } end && at >= end
                || matching.EffectiveTo is { } matchingEnd && at >= matchingEnd)
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            InvoiceUnitReviewPolicyRules.VerifyRetainedProjection(source.ReviewPolicy, review, at,
                retainedHistoricalPublication);
            InvoiceUnitReviewPolicyRules.VerifyCaseGraph(source.ReviewPolicy,
                authority.Case, authority.Workers);
            InvoiceRetainedUnitReview.VerifyCoreCompletion(authority,
                new(4, evidence.ReviewId, evidence.ReviewSourceVersion, evidence.CoreCaseId,
                    evidence.ReviewPolicyId, evidence.ReviewPolicyVersion,
                    evidence.MakerPrincipalId, evidence.MakerWorkerId,
                    evidence.ReviewerPrincipalId, evidence.ReviewerWorkerId,
                    evidence.FinalDecisionId, evidence.ReviewedAt,
                    evidence.AssignmentWorkers), invoice, mapping.ReviewedAt);
            if (authority.Decisions.Any(x => x.Action == "Approve"
                && (string.IsNullOrWhiteSpace(x.Reason) || x.Reason.Length > 2000)))
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            var final = authority.Decisions.Single(x => x.Id == evidence.FinalDecisionId);
            if (final.Reason != evidence.ReviewerReason || string.IsNullOrWhiteSpace(final.Reason)
                || invoice.CurrentMappingVersionId != mapping.Id
                || invoice.MappingRevision != mapping.MappingRevision)
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            var snapshot = InvoiceMappingReviewSourceDispatch.Verify(mapping, invoice, originals);
            if (snapshot.Lines.Count != evidence.Lines.Count
                || evidence.Lines.Select(x => x.InvoiceLineId).Distinct().Count() != evidence.Lines.Count)
                throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
            var units = new Dictionary<Guid, string>();
            foreach (var link in snapshot.Lines)
            {
                var line = evidence.Lines.SingleOrDefault(x => x.InvoiceLineId == link.InvoiceLineId);
                if (line is null || link.DocumentVersionId != Guid.Empty
                    || !string.IsNullOrWhiteSpace(link.DocumentLineReference)
                    || line.UnitCode != link.InvoiceUnitCode
                    || line.UnitCode is not { Length: > 0 and <= 20 })
                    throw new InvalidOperationException("InvoiceManualUnitReviewInvalid");
                units.Add(line.InvoiceLineId, line.UnitCode);
            }
            return new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(units);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or ArgumentException or FormatException or KeyNotFoundException)
        { throw new InvalidOperationException("InvoiceManualUnitReviewInvalid", error); }
    }

    private static bool CanonicalReason(string? reason) => reason is { Length: > 0 and <= 2000 }
        && string.Join(" ", reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) == reason;
}
