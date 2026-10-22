using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Explicit server-resolved named participants, not role discovery or an HTTP DTO.
/// The eventual adapter must validate the published SOURCING policy and current canonical accounts.</summary>
public sealed record SourcingPublicationNamedStage(PolicyPreviewStage Stage, IReadOnlyList<RfqCurrentActor> Reviewers);
public sealed record SourcingPublicationReviewerEvidence(string PrincipalId, Guid WorkerId, ProcurementSpendingAuthorityEvidence Authority);
public sealed record SourcingPublicationStageProposal(PolicyPreviewStage Stage, int RequiredApprovals,
    IReadOnlyList<SourcingPublicationReviewerEvidence> Reviewers);
public sealed record SourcingPublicationAuthorityProposal(bool AuthorityEligible, ProcurementSpendContext? Spend,
    IReadOnlyList<SourcingPublicationStageProposal> Stages, IReadOnlyList<string> Blockers)
{
    // A finite authority preflight is never publication, a Core vote/case or current owner truth.
    public bool CanPublish => false;
}

/// <summary>Pure O4/O5 authority prerequisite only. Does not parse/publish policy, resolve
/// identity, decide Core votes, reserve demand, mutate a draft or create publication evidence.</summary>
public static class SourcingPublicationAuthority
{
    public static SourcingPublicationAuthorityProposal Evaluate(GovernedRfqDraft.Draft draft, RfqCurrentActor submitter,
        IReadOnlyList<SourcingPublicationNamedStage> namedStages, DateTimeOffset at,
        IEnumerable<ProcurementSpendingAuthorityGrant> grants)
    {
        var stages = new List<SourcingPublicationStageProposal>();
        ProcurementSpendContext? spend = null;
        try
        {
            if (draft is null || submitter is null || at == default || namedStages is null || namedStages.Count is < 1 or > 20
                || namedStages.Any(x => x is null || x.Stage is null || !ProcurementPolicyCode.IsValid(x.Stage.Code)
                    || x.Stage.Assignee != "ProcurementManager" || x.Stage.Mode is not ("One" or "All")
                    || x.Reviewers is null || x.Reviewers.Count is < 1 or > 100 || x.Reviewers.Any(r => r is null))
                || namedStages.Select(x => x.Stage.Code).Distinct(StringComparer.Ordinal).Count() != namedStages.Count)
                return Denied("SourcingPublicationNamedReviewInvalid");
            // No implicit approver. One worker cannot appear under several aliases in a named graph.
            var named = namedStages.SelectMany(x => x.Reviewers).ToArray();
            if (named.GroupBy(x => x.WorkerId).Any(x => x.Select(r => r.SubjectId).Distinct(StringComparer.Ordinal).Count() != 1)
                || named.GroupBy(x => x.SubjectId, StringComparer.Ordinal).Any(x => x.Select(r => r.WorkerId).Distinct().Count() != 1))
                return Denied("SourcingPublicationReviewerIdentityAmbiguous");
            var amount = SelectedOriginalAmount(draft.Lines);
            spend = new(draft.Id, draft.TenantId, draft.CompanyId, draft.CurrencyCode,
                ProcurementSpendingActions.SourcingPublication.AmountBasis, amount, draft.BranchId,
                Array.AsReadOnly(draft.Lines.Select(x => x.DepartmentId).Distinct().ToArray()),
                Array.AsReadOnly(draft.Lines.Select(x => x.CategoryCode!).Distinct(StringComparer.Ordinal).ToArray()));
            var completeGrants = grants.ToArray(); // Preserve entire logical families; never prefilter limits/scopes.
            foreach (var configuration in namedStages)
            {
                if (configuration.Reviewers.Select(x => x.SubjectId).Distinct(StringComparer.Ordinal).Count() != configuration.Reviewers.Count
                    || configuration.Reviewers.Select(x => x.WorkerId).Distinct().Count() != configuration.Reviewers.Count)
                    return Denied("SourcingPublicationReviewerIdentityAmbiguous");
                var reviewers = new List<SourcingPublicationReviewerEvidence>();
                foreach (var reviewer in configuration.Reviewers)
                {
                    // Reuse original RFQ maker/requester independence and current actor/scope mechanics.
                    // Its blocked intent is not a current policy/Core publication decision.
                    _ = GovernedRfqDraft.PreparePublicationIntent(submitter, draft, reviewer);
                    var authority = ProcurementSpendingAuthority.EvaluateSourcingPublication(spend, reviewer.SubjectId, at, completeGrants);
                    if (!authority.Eligible || authority.Evidence is null)
                        return new(false, spend, [], authority.Blockers);
                    reviewers.Add(new(reviewer.SubjectId, reviewer.WorkerId, authority.Evidence));
                }
                stages.Add(new(new(configuration.Stage.Code, configuration.Stage.Assignee, configuration.Stage.Mode),
                    configuration.Stage.Mode == "One" ? 1 : reviewers.Count, Array.AsReadOnly(reviewers.ToArray())));
            }
            return new(true, spend, Array.AsReadOnly(stages.ToArray()), Array.AsReadOnly(new[] {
                "SourcingPublicationPolicyNotVerified", "CorePublicationReviewNotOpened",
                "CurrentSourceSupplierAndDocumentEvidenceRequired", "PublicationPersistenceUnavailable" }));
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException)
        { return Denied(error.Message); }
        SourcingPublicationAuthorityProposal Denied(string reason) => new(false, spend, [], [reason]);
    }

    /// <summary>All selected/held original event lines, NOT unselected approved PR quantity,
    /// NOT remaining availability after another RFQ, NOT tax/delivery/gross. No rounding.</summary>
    public static decimal SelectedOriginalAmount(IReadOnlyList<RfqDemandPin> lines)
    {
        if (lines is null || lines.Count is < 1 or > 200 || lines.Any(x => x is null)
            || lines.Select(x => x.LineId).Distinct().Count() != lines.Count
            || lines.Select(x => x.EventLineId).Distinct().Count() != lines.Count
            || lines.Select(x => x.AllocationId).Distinct().Count() != lines.Count
            || lines.Any(x => x.LineId == Guid.Empty || x.EventLineId == Guid.Empty || x.AllocationId == Guid.Empty
                || x.MakerWorkerId == Guid.Empty || x.RequesterWorkerId == Guid.Empty || x.DepartmentId == Guid.Empty
                || string.IsNullOrWhiteSpace(x.CategoryCode) || x.Quantity <= 0 || x.UnitEstimate < 0
                || !ExactDecimalJsonCodec.IsRepresentable(x.Quantity, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(x.UnitEstimate, 18, 4)))
            throw new InvalidOperationException("SourcingPublicationSelectedLinesInvalid");
        var values = lines.Select(x =>
        {
            var value = ExactDecimalArithmetic.Multiply(x.Quantity, x.UnitEstimate);
            if (value != x.EstimatedValue || !ExactDecimalJsonCodec.IsRepresentable(value, 38, 8))
                throw new InvalidOperationException("SourcingPublicationSelectedValueInvalid");
            return value;
        }).ToArray();
        var amount = ExactDecimalArithmetic.Sum(values);
        if (amount <= 0 || !ExactDecimalJsonCodec.IsRepresentable(amount, 38, 8))
            throw new InvalidOperationException("SourcingPublicationAmountInvalid");
        return amount;
    }
}
