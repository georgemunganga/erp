namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>A validated proposal for a future governed award submission. It is
/// not an award, allocation, purchase order, or Core approval decision.</summary>
public sealed record PrincipalRfqAwardCandidate(
    Guid SubmissionId, string TenantId, Guid CompanyId,
    Guid DraftId, Guid RecommendationRevisionId, string RecommendationSha256,
    Guid RoundId, Guid UnsealingId, Guid PolicyVersionId, string PolicyRulesSha256,
    string MakerPrincipalId, Guid? MakerWorkerAliasId, Guid ReviewBranchId,
    Guid CommandKey, string RequestSha256, int ExpectedRecommendationRevision,
    string CurrencyCode, string ProposedTotalExact,
    IReadOnlyList<PrincipalRfqAwardCandidateLine> Lines);

public sealed record PrincipalRfqAwardCandidateLine(
    Guid OfferId, string OfferSha256, Guid SupplierId, Guid SourcingLineId,
    string QuantityExact, string CurrencyCode, string RetainedQuoteTotalExact);
