using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ContractReviewCurrentReviewer(string PrincipalId, Guid? WorkerAliasId,
    bool OrganisationWide, IReadOnlySet<Guid> AllowedBranches, IReadOnlySet<string> Actions);
public sealed record ContractReviewOpening(ContractReviewSubmission Submission, ApprovalCase Case);

/// <summary>Pure source-module opening rules over the existing Core approval engine.
/// The command host must authenticate all current people and the selected branch,
/// verify retained policy publication and save both owners in one transaction.</summary>
public static class ContractReviewCoreWorkflow
{
    public const string SourceType = "SupplierContractReview";
    public const string ReviewAction = "procurement-contract-review";

    public static ContractReviewOpening Open(SupplierContract contract, SupplierContractVersion version,
        IReadOnlyCollection<ContractRate> rates, PolicyVersion policy, Guid reviewBranchId,
        string makerPrincipalId, Guid? makerWorkerAliasId,
        IReadOnlyCollection<ContractReviewCurrentReviewer> currentReviewers,
        int submissionNumber, DateTimeOffset now)
    {
        if (reviewBranchId == Guid.Empty || submissionNumber < 1 || now == default
            || !ProcurementPrincipalEvidence.Canonical(makerPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(makerWorkerAliasId)
            || makerPrincipalId != contract.OwnerSubjectId
            || policy.TenantId != contract.TenantId || policy.LegalEntityId != contract.LegalEntityId
            || policy.PolicyKey != ProcurementContractReviewPolicyRules.PolicyKey || policy.State != "Published"
            || policy.PolicyVersionNumber < 1 || policy.EffectiveFrom > now
            || policy.EffectiveTo is { } until && until <= now)
            throw new InvalidOperationException("ContractReviewOpeningInvalid");
        var rules = ProcurementContractReviewPolicyRules.Parse(policy.RulesJson);
        var frozen = ContractReviewTerms.Capture(contract, version, rates);
        var people = currentReviewers.ToArray();
        var expected = rules.Stages.SelectMany(x => x.PrincipalIds).ToHashSet(StringComparer.Ordinal);
        if (people.Length != expected.Count || people.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal).Count() != people.Length
            || !people.Select(x => x.PrincipalId).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidOperationException("ContractReviewNamedReviewersChanged");
        foreach (var person in people)
            if (!ProcurementPrincipalEvidence.Canonical(person.PrincipalId)
                || !ProcurementPrincipalEvidence.OptionalWorker(person.WorkerAliasId)
                || !person.Actions.Contains("procurement-access") || !person.Actions.Contains(ReviewAction)
                || !person.OrganisationWide && !person.AllowedBranches.Contains(reviewBranchId)
                || !ProcurementPrincipalEvidence.Independent(makerPrincipalId, makerWorkerAliasId,
                    person.PrincipalId, person.WorkerAliasId))
                throw new InvalidOperationException("ContractReviewCurrentReviewerUnavailable");
        for (var i = 0; i < people.Length; i++)
            for (var j = i + 1; j < people.Length; j++)
                if (!ProcurementPrincipalEvidence.Independent(people[i].PrincipalId, people[i].WorkerAliasId,
                        people[j].PrincipalId, people[j].WorkerAliasId))
                    throw new InvalidOperationException("ContractReviewNamedReviewersNotIndependent");
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
        var approvalId = Guid.CreateVersion7();
        var submission = new ContractReviewSubmission
        {
            TenantId = contract.TenantId, LegalEntityId = contract.LegalEntityId,
            ContractId = contract.Id, ContractVersionId = version.Id,
            ContractBusinessVersion = contract.Version, ContractVersionBusinessVersion = version.Version,
            SubmissionNumber = submissionNumber, ReviewBranchId = reviewBranchId,
            SupplierId = contract.SupplierId, MakerPrincipalId = makerPrincipalId,
            MakerWorkerAliasId = makerWorkerAliasId, PolicyVersionId = policy.Id,
            PolicyKey = policy.PolicyKey,
            PolicyVersionNumber = policy.PolicyVersionNumber,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            CoreCaseId = approvalId,
            TermsJson = frozen.Json, TermsSha256 = frozen.Sha256,
            SubmittedAt = at, CreatedAt = at, CreatedBy = makerPrincipalId,
        };
        var approval = new ApprovalCase
        {
            Id = approvalId,
            TenantId = contract.TenantId, LegalEntityId = contract.LegalEntityId,
            SourceModule = "procurement", SourceType = SourceType, SourceId = submission.Id,
            SourceVersion = checked(contract.Version + 1), PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber, MakerSubjectId = makerPrincipalId,
            OpenedAt = at, CreatedAt = at, UpdatedAt = at,
        };
        var sequence = 0;
        foreach (var configured in rules.Stages)
        {
            var stage = new ApprovalStage
            {
                TenantId = contract.TenantId, LegalEntityId = contract.LegalEntityId,
                CaseId = approval.Id, Sequence = ++sequence, Code = configured.Code,
                Mode = configured.Mode == "One" ? "Any" : "All",
                RequiredApprovals = configured.Mode == "One" ? 1 : configured.PrincipalIds.Count,
                State = sequence == 1 ? "Active" : "Waiting", CreatedAt = at, UpdatedAt = at,
            };
            foreach (var principal in configured.PrincipalIds)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    TenantId = contract.TenantId, LegalEntityId = contract.LegalEntityId,
                    CaseId = approval.Id, StageId = stage.Id, AssigneeSubjectId = principal,
                    PermissionKey = ReviewAction, CreatedAt = at, UpdatedAt = at,
                });
            approval.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(approval);
        return new(submission, approval);
    }
}
