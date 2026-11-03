using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Retained policy binding for an existing round, never current-policy
/// selection or authority. The host must verify stored publication provenance and
/// current Core actor/assignment/scope independently before calling the Core engine.</summary>
public static class PlanReviewPinnedPolicy
{
    public static bool Matches(PolicyVersion? policy, PlanReviewSubmission submission) =>
        policy is not null && submission.SubmittedAt != default
        && policy.TenantId == submission.TenantId && policy.LegalEntityId == submission.LegalEntityId
        && policy.Id == submission.PolicyVersionId && policy.PolicyVersionNumber == submission.PolicyVersionNumber
        && policy.PolicyKey == PlanReviewPolicyRules.PolicyKey
        && policy.State is "Published" or "Superseded"
        && policy.PublishedAt is { } published && published <= submission.SubmittedAt
        && policy.EffectiveFrom <= submission.SubmittedAt
        && ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) == submission.PolicyRulesSha256
        && PlanReviewPolicyRules.Read(policy.RulesJson) is not null;
}
