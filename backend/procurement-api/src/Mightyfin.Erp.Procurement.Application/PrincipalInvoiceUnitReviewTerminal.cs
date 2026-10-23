using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalInvoiceVerifiedUnits(int SchemaVersion, Guid ReviewSourceId,
    Guid MappingVersionId, string OriginalUnitsSha256, Guid CoreCaseId,
    Guid FinalDecisionId, string DecisionsSha256, string ReviewerPrincipalId,
    Guid? ReviewerWorkerAliasId, DateTimeOffset CompletedAt,
    IReadOnlyList<PrincipalInvoiceUnitReviewTransitions.PinnedUnit> Lines);

/// <summary>Retained Core terminal verifier for PO2 original document units.
/// Approved Core state is required but is not itself matching, AP or payment
/// authority. Caller must load the current original document hashes from Core.</summary>
public static class PrincipalInvoiceUnitReviewTerminal
{
    public static PrincipalInvoiceUnitReviewOutcome Seal(PrincipalInvoiceUnitReviewSource source,
        PrincipalInvoiceMappingVersion mapping, ApprovalCase workflow,
        IReadOnlyList<PrincipalInvoiceUnitReviewAssignment> assignments,
        IReadOnlyList<PrincipalInvoiceUnitReviewAttestation> attestations,
        IReadOnlyList<ApprovalDecision> decisions, PolicyVersion matchingPolicy,
        PolicyVersion reviewPolicy, IReadOnlyDictionary<Guid, string> currentDocumentHashes,
        DateTimeOffset now)
    {
        var opening = PrincipalInvoiceUnitReviewTransitions.Verify(source, mapping, workflow, assignments);
        if (source.MatchingPolicyId != matchingPolicy.Id || source.ReviewPolicyId != reviewPolicy.Id
            || matchingPolicy.PolicyKey != InvoiceMatchingPolicyRules.PolicyKey
            || reviewPolicy.PolicyKey != InvoiceRetainedUnitReview.ReviewPolicyKey
            || matchingPolicy.TenantId != source.TenantId || matchingPolicy.LegalEntityId != source.LegalEntityId
            || reviewPolicy.TenantId != source.TenantId || reviewPolicy.LegalEntityId != source.LegalEntityId
            || opening.MatchingPolicySha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(matchingPolicy.RulesJson)
            || opening.ReviewPolicySha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(reviewPolicy.RulesJson)
            || matchingPolicy.State != "Published" || reviewPolicy.State != "Published"
            || matchingPolicy.PublishedAt is null || reviewPolicy.PublishedAt is null
            || matchingPolicy.PublishedAt > source.OpenedAt || reviewPolicy.PublishedAt > source.OpenedAt
            || matchingPolicy.EffectiveFrom > source.OpenedAt || reviewPolicy.EffectiveFrom > source.OpenedAt
            || matchingPolicy.EffectiveTo is { } matchingUntil && matchingUntil <= source.OpenedAt
            || reviewPolicy.EffectiveTo is { } reviewUntil && reviewUntil <= source.OpenedAt
            || workflow.State != "Approved" || workflow.CompletedAt is not { } completed
            || completed < source.OpenedAt || completed > now
            || string.IsNullOrWhiteSpace(workflow.CompletionKey)
            || decisions.Count == 0 || decisions.Select(x => x.Id).Distinct().Count() != decisions.Count
            || decisions.Select(x => x.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() != decisions.Count
            || attestations.Count != decisions.Count
            || attestations.Select(x => x.CoreDecisionId).Distinct().Count() != decisions.Count
            || opening.Lines.Select(x => x.DocumentVersionId).Distinct().Count() != currentDocumentHashes.Count
            || opening.Lines.Any(x => !currentDocumentHashes.TryGetValue(x.DocumentVersionId, out var hash)
                || hash != x.DocumentSha256))
            throw new InvalidOperationException("PrincipalInvoiceOriginalUnitCompletionInvalid");
        var coreAssignments = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
        if (coreAssignments.Length != assignments.Count
            || coreAssignments.Select(x => x.Id).Distinct().Count() != coreAssignments.Length)
            throw new InvalidOperationException("PrincipalInvoiceReviewAssignmentSetChanged");
        var previousAt = workflow.OpenedAt;
        foreach (var stage in workflow.Stages.OrderBy(x => x.Sequence))
        {
            var votes = decisions.Where(x => x.StageId == stage.Id).ToArray();
            if (stage.State != "Approved" || stage.RequiredApprovals is < 1
                || stage.RequiredApprovals > stage.Assignments.Count
                || votes.Length != stage.RequiredApprovals
                || votes.Any(x => x.Action != "Approve" || x.TenantId != source.TenantId
                    || x.LegalEntityId != source.LegalEntityId || x.CaseId != workflow.Id
                    || x.DecidedAt < previousAt || x.DecidedAt > completed
                    || !Guid.TryParseExact(x.IdempotencyKey, "D", out var key) || key == Guid.Empty)
                || stage.Assignments.Count(x => x.State == "Approved") != stage.RequiredApprovals)
                throw new InvalidOperationException("PrincipalInvoiceReviewCoreQuorumInvalid");
            foreach (var assignment in stage.Assignments)
            {
                var pin = assignments.Single(x => x.CoreAssignmentId == assignment.Id);
                var assigned = votes.Where(x => x.AssignmentId == assignment.Id).ToArray();
                if (assignment.TenantId != source.TenantId || assignment.LegalEntityId != source.LegalEntityId
                    || assignment.CaseId != workflow.Id || assignment.StageId != stage.Id
                    || assignment.AssigneeSubjectId != pin.ReviewerPrincipalId
                    || !ProcurementPrincipalEvidence.Independent(source.MakerPrincipalId,
                        source.MakerWorkerAliasId, pin.ReviewerPrincipalId, pin.ReviewerWorkerAliasId)
                    || assignment.State == "Approved" && (assigned.Length != 1
                        || assigned[0].ActorSubjectId != pin.ReviewerPrincipalId)
                    || assignment.State == "Superseded" && assigned.Length != 0
                    || assignment.State is not ("Approved" or "Superseded"))
                    throw new InvalidOperationException("PrincipalInvoiceReviewCoreAssignmentInvalid");
                foreach (var vote in assigned)
                {
                    var attestation = attestations.SingleOrDefault(x => x.CoreDecisionId == vote.Id);
                    if (attestation is null || attestation.ReviewSourceId != source.Id
                        || attestation.CoreCaseId != workflow.Id
                        || attestation.CoreAssignmentId != assignment.Id
                        || attestation.ReviewerPrincipalId != pin.ReviewerPrincipalId
                        || attestation.ReviewerWorkerAliasId != pin.ReviewerWorkerAliasId
                        || attestation.OriginalUnitsSha256 != source.OriginalUnitsSha256
                        || attestation.Statement != InvoiceUnitReviewCommands.OriginalUnitAttestation
                        || attestation.AttestedAt != vote.DecidedAt)
                        throw new InvalidOperationException("PrincipalInvoiceOriginalUnitAttestationChanged");
                }
            }
            previousAt = votes.Max(x => x.DecidedAt);
        }
        if (decisions.Any(x => !coreAssignments.Any(a => a.Id == x.AssignmentId))
            || previousAt != completed)
            throw new InvalidOperationException("PrincipalInvoiceReviewDecisionHistoryInvalid");
        var final = decisions.Single(x => x.DecidedAt == completed
            && x.StageId == workflow.Stages.MaxBy(stage => stage.Sequence)!.Id
            && x.IdempotencyKey == workflow.CompletionKey);
        var signer = assignments.Single(x => x.CoreAssignmentId == final.AssignmentId);
        var decisionsSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(
            decisions.OrderBy(x => x.StageId).ThenBy(x => x.DecidedAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.StageId, x.AssignmentId, x.ActorSubjectId,
                    x.Action, x.IdempotencyKey, x.DecidedAt }).ToArray()));
        var evidence = new PrincipalInvoiceVerifiedUnits(2, source.Id, mapping.Id,
            source.OriginalUnitsSha256, workflow.Id, final.Id, decisionsSha,
            signer.ReviewerPrincipalId, signer.ReviewerWorkerAliasId, completed,
            opening.Lines);
        var json = JsonSerializer.Serialize(evidence);
        return new PrincipalInvoiceUnitReviewOutcome
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            ReviewSourceId = source.Id, CoreCaseId = workflow.Id,
            FinalDecisionId = final.Id, State = "Verified", EvidenceJson = json,
            EvidenceSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            ReviewerPrincipalId = signer.ReviewerPrincipalId,
            ReviewerWorkerAliasId = signer.ReviewerWorkerAliasId,
            CompletedAt = completed
        };
    }

    public static IReadOnlyDictionary<Guid, string> Verify(PrincipalInvoiceUnitReviewOutcome retained,
        PrincipalInvoiceUnitReviewSource source, PrincipalInvoiceMappingVersion mapping,
        ApprovalCase workflow, IReadOnlyList<PrincipalInvoiceUnitReviewAssignment> assignments,
        IReadOnlyList<PrincipalInvoiceUnitReviewAttestation> attestations,
        IReadOnlyList<ApprovalDecision> decisions, PolicyVersion matchingPolicy,
        PolicyVersion reviewPolicy, IReadOnlyDictionary<Guid, string> currentDocumentHashes,
        DateTimeOffset now)
    {
        var rebuilt = Seal(source, mapping, workflow, assignments, attestations, decisions,
            matchingPolicy, reviewPolicy, currentDocumentHashes, now);
        if (retained.State != "Verified" || retained.ReviewSourceId != source.Id
            || retained.CoreCaseId != workflow.Id || retained.FinalDecisionId != rebuilt.FinalDecisionId
            || retained.ReviewerPrincipalId != rebuilt.ReviewerPrincipalId
            || retained.ReviewerWorkerAliasId != rebuilt.ReviewerWorkerAliasId
            || retained.CompletedAt != rebuilt.CompletedAt
            || retained.EvidenceJson != rebuilt.EvidenceJson
            || retained.EvidenceSha256 != rebuilt.EvidenceSha256)
            throw new InvalidOperationException("PrincipalInvoiceOriginalUnitVerificationChanged");
        var evidence = JsonSerializer.Deserialize<PrincipalInvoiceVerifiedUnits>(retained.EvidenceJson)
            ?? throw new InvalidOperationException("PrincipalInvoiceOriginalUnitVerificationMissing");
        return evidence.Lines.ToDictionary(x => x.InvoiceLineId, x => x.UnitCode);
    }
}
