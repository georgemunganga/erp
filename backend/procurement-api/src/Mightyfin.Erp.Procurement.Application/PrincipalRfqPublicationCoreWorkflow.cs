using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalRfqPublicationOpening(ApprovalCase Case, string SnapshotJson,
    string SnapshotSha256, RfqPublicationPreparation Shortlist,
    IReadOnlyList<string> MatchedRuleCodes);

/// <summary>Pure case graph and immutable opening payload. The owning host must re-read
/// current policy, source, suppliers, Core people/actions and branch grants under the
/// same transaction; this object cannot itself approve or publish an RFQ.</summary>
public static class PrincipalRfqPublicationCoreWorkflow
{
    public const string SourceType = "sourcing_publication";
    public const string Permission = "procurement-sourcing-publish";

    /// <summary>An approval uses the same currently effective, independently published
    /// rules that opened its case. Return and Reject remain possible after expiry.</summary>
    public static bool CurrentPolicyPermitsApproval(SourcingPublicationOpening opening,
        PolicyVersion policy, SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted, SourcingPolicyIdentityEvidence published,
        DateTimeOffset now)
    {
        if (opening is null || policy is null || created is null || submitted is null
            || published is null || now == default || policy.State != "Published"
            || policy.PolicyKey != SourcingPublicationPolicyRules.PolicyKey
            || policy.EffectiveFrom > now || policy.EffectiveTo is { } end && now >= end
            || policy.Id != opening.PolicyVersionId
            || policy.PolicyVersionNumber != opening.PolicyVersionNumber
            || policy.TenantId != opening.TenantId || policy.LegalEntityId != opening.LegalEntityId
            || published.Id != opening.PublishedPolicyEvidenceId
            || created.SchemaVersion != 2 || submitted.SchemaVersion != 2 || published.SchemaVersion != 2
            || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) != opening.PolicyRulesSha256)
            return false;
        try { SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published); }
        catch (InvalidOperationException) { return false; }
        return true;
    }

    public static PrincipalRfqPublicationOpening Open(RfqPrincipalDraft draft, long eventVersion,
        PolicyVersion policy, SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted, SourcingPolicyIdentityEvidence published,
        IReadOnlyList<PolicyScenario> currentClassifications,
        IReadOnlyList<RfqPrincipalActor> namedReviewers,
        RfqPublicationPreparation shortlist, DateTimeOffset now)
    {
        if (draft is null || eventVersion < 1 || draft.State != "Draft"
            || policy is null || created is null || submitted is null || published is null
            || currentClassifications is null || namedReviewers is null || shortlist is null
            || now == default || policy.State != "Published" || policy.EffectiveFrom > now
            || policy.EffectiveTo is { } end && now >= end
            || policy.TenantId != draft.TenantId || policy.LegalEntityId != draft.CompanyId
            || policy.PolicyKey != SourcingPublicationPolicyRules.PolicyKey
            || created.SchemaVersion != 2 || published.SchemaVersion != 2
            || shortlist.DraftFingerprint != draft.OriginalFingerprint
            || shortlist.SupplierIds.Count is < 1 or > 100
            || shortlist.SupplierIds.Distinct().Count() != shortlist.SupplierIds.Count
            || shortlist.SnapshotSha256 is not { Length: 64 }
            || shortlist.SelectedOriginalAmount != ExactDecimalArithmetic.Sum(draft.Lines.Select(x => x.EstimatedValue))
            || currentClassifications.Count != draft.Lines.Count)
            throw new InvalidOperationException("RfqPublicationOpeningInvalid");
        SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published);
        var ordered = draft.Lines.OrderBy(x => x.EventLineId).ToArray();
        var matched = new List<SourcingPublicationPolicyPreview>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var line = ordered[index]; var scenario = currentClassifications[index];
            if (scenario.CurrencyCode != draft.CurrencyCode || scenario.BranchId != draft.BranchId
                || scenario.CategoryCode != line.CategoryCode
                || scenario.LineBaseAmount != line.EstimatedValue)
                throw new InvalidOperationException("RfqPublicationClassificationChanged");
            var preview = SourcingPublicationPolicyRules.Evaluate(policy.RulesJson, scenario);
            if (preview.Status != "Match" || preview.Controls is null || preview.Stages.Count == 0
                || preview.PolicyDigest != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson))
                throw new InvalidOperationException("RfqPublicationPolicyNoMatch");
            matched.Add(preview);
        }
        var first = matched[0];
        if (matched.Any(x => x.Controls != first.Controls
            || !StagesEqual(x.Stages, first.Stages))
            || first.Controls!.MinimumQuoteCount != shortlist.RequiredQuoteCount
            || shortlist.SupplierIds.Count < first.Controls.MinimumInvitedSuppliers
            || shortlist.SupplierIds.Count < first.Controls.MinimumQuoteCount)
            throw new InvalidOperationException("RfqPublicationPolicyMixedControls");
        // Documents and timed closing require independent owner-access and trusted time
        // evidence; until those contracts are implemented, their configured rules block.
        if (first.Controls.RequirementsPackRequired || first.Controls.EvaluationCriteriaRequired
            || first.Controls.ClosingDateRequired)
            throw new InvalidOperationException("RfqPublicationRequiredEvidenceUnavailable");
        var expected = first.Stages.SelectMany(x => x.PrincipalIds).Distinct(StringComparer.Ordinal).ToArray();
        if (namedReviewers.Count != expected.Length
            || namedReviewers.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal).Count() != namedReviewers.Count
            || !namedReviewers.Select(x => x.PrincipalId).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException("RfqPublicationNamedReviewersChanged");
        foreach (var reviewer in namedReviewers)
        {
            if (!ProcurementPrincipalEvidence.Canonical(reviewer.PrincipalId)
                || !ProcurementPrincipalEvidence.OptionalWorker(reviewer.LinkedWorkerId)
                || reviewer.TenantId != draft.TenantId || reviewer.CompanyId != draft.CompanyId
                || !reviewer.Permissions.Contains(Permission)
                || reviewer.SelectedBranchId is { } selected && selected != draft.BranchId
                || !reviewer.OrganisationWide && (draft.BranchId is not { } branch || !reviewer.Branches.Contains(branch))
                || !ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId, reviewer.LinkedWorkerId,
                    draft.MakerPrincipalId, draft.MakerWorkerAlias)
                || ordered.Any(x => !ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId,
                    reviewer.LinkedWorkerId, x.MakerPrincipalId, x.MakerWorkerAlias)
                    || !ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId,
                        reviewer.LinkedWorkerId, x.RequesterPrincipalId, x.RequesterWorkerAlias)))
                throw new InvalidOperationException("RfqPublicationReviewerNotIndependentOrCurrent");
        }
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
        var approval = new ApprovalCase
        {
            TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
            SourceModule = "procurement", SourceType = SourceType, SourceId = draft.Id,
            SourceVersion = eventVersion, PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber,
            MakerSubjectId = draft.MakerPrincipalId, OpenedAt = at, CreatedAt = at, UpdatedAt = at,
        };
        var sequence = 0;
        foreach (var configured in first.Stages)
        {
            var stage = new ApprovalStage
            {
                TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
                CaseId = approval.Id, Sequence = ++sequence, Code = configured.Stage.Code,
                Mode = configured.Stage.Mode == "One" ? "Any" : "All",
                RequiredApprovals = configured.Stage.Mode == "One" ? 1 : configured.PrincipalIds.Count,
                State = sequence == 1 ? "Active" : "Waiting", CreatedAt = at, UpdatedAt = at,
            };
            foreach (var id in configured.PrincipalIds)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
                    CaseId = approval.Id, StageId = stage.Id,
                    AssigneeSubjectId = id, PermissionKey = Permission,
                    CreatedAt = at, UpdatedAt = at,
                });
            approval.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(approval);
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 2, draft.Id, draft.TenantId, draft.CompanyId, draft.BranchId,
            draft.OriginalFingerprint, eventVersion, shortlist.SnapshotSha256,
            supplierIds = shortlist.SupplierIds, policyVersionId = policy.Id,
            policy.PolicyVersionNumber, policyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            publishedPolicyEvidenceId = published.Id, coreCaseId = approval.Id,
            reviewGraph = approval.Stages.Select(stage => new
            {
                stage.Id, stage.Sequence, stage.Code, stage.Mode, stage.RequiredApprovals,
                assignments = stage.Assignments.Select(x => new { x.Id, x.AssigneeSubjectId, x.PermissionKey }),
            }),
        });
        return new(approval, snapshot, ProcurementEvidenceEncoding.Digest(snapshot), shortlist,
            Array.AsReadOnly(matched.Select(x => x.RuleCode!).ToArray()));
    }

    private static bool StagesEqual(IReadOnlyList<SourcingPublicationPolicyStage> left,
        IReadOnlyList<SourcingPublicationPolicyStage> right)
        => left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Stage == pair.Second.Stage
            && pair.First.PrincipalIds.SequenceEqual(pair.Second.PrincipalIds, StringComparer.Ordinal));
}
