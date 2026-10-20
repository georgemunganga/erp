using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;
using Flow = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewTransitions;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure owned outcome contract. No EF record, runtime provider, trust signature,
/// permission grant, complete Finance history or AP claim is installed by this helper.</summary>
public static class InvoiceUnitReviewTerminal
{
    public sealed class Outcome
    {
        internal Outcome(Flow.OpeningPlan owned, ApprovalDecision final, Guid worker, string state, int schema, string json)
        {
            ReviewId = owned.Source.ReviewId; SourceVersion = owned.Source.SourceVersion;
            SourceSha256 = owned.SourceSha256; CoreCaseId = owned.Case.Id;
            InvoiceId = owned.Source.InvoiceId; MappingId = owned.Source.MappingId;
            FinalDecisionId = final.Id; ReviewerPrincipalId = final.ActorSubjectId; ReviewerWorkerId = worker;
            CompletedAt = final.DecidedAt; State = state; EvidenceSchemaVersion = schema;
            EvidenceJson = json; EvidenceSha256 = ProcurementEvidenceEncoding.Digest(json);
        }
        public Guid ReviewId { get; }
        public long SourceVersion { get; }
        public string SourceSha256 { get; }
        public Guid CoreCaseId { get; }
        public Guid InvoiceId { get; }
        public Guid MappingId { get; }
        public Guid FinalDecisionId { get; }
        public string ReviewerPrincipalId { get; }
        public Guid ReviewerWorkerId { get; }
        public DateTimeOffset CompletedAt { get; }
        public string State { get; }
        public int EvidenceSchemaVersion { get; }
        public string EvidenceJson { get; }
        public string EvidenceSha256 { get; }
    }

    /// <summary>Inputs must be actual owned source/Core rows under their enlisted transaction.
    /// Completion is verified, never generated here; callers must enforce unique durable outcome/receipt.</summary>
    public static Outcome Seal(Flow.OpeningPlan owned, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals, InvoiceMatchMappingVersion mapping, InvoiceIssuedOrder issued,
        PolicyVersion matchingPolicy, PolicyVersion reviewPolicy, IReadOnlyDictionary<Guid, string> documentHashes,
        IReadOnlyList<ApprovalDecision> ownedDecisions, Guid finalDecisionId, DateTimeOffset now)
    {
        try
        {
            Flow.ValidateOwnedEvidence(owned, invoice, originals, mapping, issued, matchingPolicy, reviewPolicy, documentHashes, now);
            var workflow = owned.Case;
            Need(workflow.State is "Approved" or "Rejected" or "Returned"
                && workflow.CompletedAt is { } complete && complete >= owned.Source.OpenedAt && complete <= now
                && ownedDecisions.Count > 0 && ownedDecisions.Select(x => x.Id).Distinct().Count() == ownedDecisions.Count);
            var final = ownedDecisions.SingleOrDefault(x => x.Id == finalDecisionId);
            Need(final is not null && final.DecidedAt == workflow.CompletedAt
                && final.TenantId == owned.Source.TenantId && final.LegalEntityId == owned.Source.LegalEntityId
                && final.CaseId == workflow.Id && Guid.TryParseExact(final.IdempotencyKey, "D", out var key)
                && key != Guid.Empty && final.IdempotencyKey == key.ToString("D"));
            var signer = owned.AssignmentWorkers.SingleOrDefault(x => x.AssignmentId == final!.AssignmentId);
            Need(signer is not null && signer.PrincipalId == final!.ActorSubjectId && signer.WorkerId != Guid.Empty);
            if (workflow.State == "Approved")
            {
                var source = owned.Source;
                var envelope = new InvoiceRetainedUnitReviewEnvelope(2, source.TenantId, source.LegalEntityId, source.BranchId,
                    source.ReviewId, source.SourceVersion, source.InvoiceId, invoice.Version, source.MappingId, source.MappingRevision,
                    source.MappingSha256, source.OriginalSha256, source.PurchaseOrderId, source.IssuedVersionId,
                    source.MatchingPolicyId, source.MatchingPolicyVersion, source.MatchingPolicySha256,
                    source.ReviewPolicy.PolicyId, source.ReviewPolicy.PolicyVersion, source.ReviewPolicy.RulesSha256,
                    source.CoreCaseId, final!.Id, signer!.PrincipalId, signer.WorkerId,
                    source.ReviewPolicy.MakerPrincipalId, source.ReviewPolicy.MakerWorkerId, final.DecidedAt,
                    source.Lines, owned.AssignmentWorkers);
                var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)); var sha = ProcurementEvidenceEncoding.Digest(json);
                var authority = new InvoiceRetainedUnitReviewAuthority(source.ReviewId, source.SourceVersion, json, sha,
                    workflow, ownedDecisions, owned.AssignmentWorkers, matchingPolicy, reviewPolicy);
                // Reuse exactly the v2 binding/completion verifier, with no invented matcher permission/identity.
                _ = InvoiceRetainedUnitReview.VerifyOwnedProof(authority, invoice, originals, mapping,
                    new(source.TenantId, source.LegalEntityId, source.BranchId, now, documentHashes));
                return new(owned, final, signer.WorkerId, "Verified", 2, json);
            }
            VerifyNonVerification(owned, ownedDecisions, final!);
            var denied = JsonSerializer.Serialize(new { schemaVersion = 1, type = "InvoiceUnitReviewNonVerification",
                owned.Source.ReviewId, owned.Source.SourceVersion, owned.SourceSha256, owned.Source.InvoiceId,
                owned.Source.MappingId, coreCaseId = workflow.Id, state = workflow.State, finalDecisionId = final!.Id,
                final.ActorSubjectId, signer!.WorkerId, final.DecidedAt, final.Reason });
            return new(owned, final, signer.WorkerId, workflow.State, 1, denied);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or JsonException
            or FormatException or OverflowException or NullReferenceException or KeyNotFoundException)
        { throw new InvalidOperationException("InvoiceUnitReviewTerminalInvalid", error); }
    }

    /// <summary>Proposed typed v2 consumption bridge for a NEW comparison. Current consumer
    /// authorization remains separate from historical signer facts. No v1 rewriting/provider activation.</summary>
    public static IReadOnlyDictionary<Guid, string> UnitsForNewComparison(Outcome outcome, Flow.OpeningPlan owned,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals, InvoiceMatchMappingVersion mapping,
        InvoiceIssuedOrder issued, PolicyVersion matchingPolicy, PolicyVersion reviewPolicy,
        IReadOnlyList<ApprovalDecision> ownedDecisions, InvoiceUnitReviewConsumer currentConsumer)
    {
        Need(outcome.State == "Verified" && outcome.EvidenceSchemaVersion == 2 && outcome.ReviewId == owned.Source.ReviewId
            && outcome.SourceVersion == owned.Source.SourceVersion && outcome.SourceSha256 == owned.SourceSha256
            && outcome.CoreCaseId == owned.Case.Id && outcome.InvoiceId == invoice.Id && outcome.MappingId == mapping.Id
            && outcome.EvidenceSha256 == ProcurementEvidenceEncoding.Digest(outcome.EvidenceJson));
        Flow.ValidateOwnedEvidence(owned, invoice, originals, mapping, issued, matchingPolicy, reviewPolicy,
            currentConsumer.DocumentVersionHashes, currentConsumer.At);
        var final = ownedDecisions.SingleOrDefault(x => x.Id == outcome.FinalDecisionId);
        Need(final is not null && final.ActorSubjectId == outcome.ReviewerPrincipalId && final.DecidedAt == outcome.CompletedAt
            && owned.AssignmentWorkers.Any(x => x.AssignmentId == final.AssignmentId && x.WorkerId == outcome.ReviewerWorkerId));
        return InvoiceRetainedUnitReview.Verify(new(outcome.ReviewId, outcome.SourceVersion,
            outcome.EvidenceJson, outcome.EvidenceSha256, owned.Case, ownedDecisions, owned.AssignmentWorkers,
            matchingPolicy, reviewPolicy), invoice, originals, mapping, currentConsumer);
    }

    // Read integrity of the real Core nonapproval terminal, not a second execution engine.
    // Approved terminal verification remains solely in the reusable v2 verifier above.
    private static void VerifyNonVerification(Flow.OpeningPlan owned, IReadOnlyList<ApprovalDecision> decisions, ApprovalDecision final)
    {
        var workflow = owned.Case; var expectedAction = workflow.State == "Rejected" ? "Reject" : "Return";
        Need(workflow.CompletionKey is null && final.Action == expectedAction && !string.IsNullOrWhiteSpace(final.Reason)
            && decisions.Select(x => x.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() == decisions.Count
            && decisions.Count(x => x.Action != "Approve") == 1);
        var stages = workflow.Stages.OrderBy(x => x.Sequence).ToArray();
        Need(stages.Select(x => x.Id).Distinct().Count() == stages.Length
            && stages.All(x => x.Id != Guid.Empty && x.Assignments.All(a => a.Id != Guid.Empty)));
        var terminalStage = stages.SingleOrDefault(x => x.Id == final.StageId);
        Need(terminalStage is not null && terminalStage.State == workflow.State);
        var previousCompleted = workflow.OpenedAt;
        foreach (var stage in stages)
        {
            var votes = decisions.Where(x => x.StageId == stage.Id).ToArray();
            if (stage.Sequence < terminalStage!.Sequence)
                Need(stage.State == "Approved" && votes.Length == stage.RequiredApprovals && votes.All(x => x.Action == "Approve"));
            else if (stage.Id == terminalStage.Id)
                Need(votes.Any(x => x.Id == final.Id) && votes.Count(x => x.Action == "Approve") < stage.RequiredApprovals);
            else Need(stage.State == "Waiting" && votes.Length == 0 && stage.Assignments.All(x => x.State == "Pending"));
            foreach (var assignment in stage.Assignments)
            {
                var assigned = votes.Where(x => x.AssignmentId == assignment.Id).ToArray();
                Need(assigned.Length <= 1);
                if (assigned.Length == 0)
                {
                    Need(stage.Sequence < terminalStage.Sequence ? assignment.State == "Superseded" : assignment.State == "Pending");
                    continue;
                }
                var vote = assigned[0];
                Need(vote.TenantId == owned.Source.TenantId && vote.LegalEntityId == owned.Source.LegalEntityId
                    && vote.CaseId == workflow.Id && vote.StageId == stage.Id && vote.ActorSubjectId == assignment.AssigneeSubjectId
                    && vote.DecidedAt >= previousCompleted && vote.DecidedAt <= final.DecidedAt
                    && !string.IsNullOrWhiteSpace(vote.IdempotencyKey)
                    && assignment.State == (vote.Action == "Approve" ? "Approved" : workflow.State));
            }
            if (votes.Length > 0) previousCompleted = votes.Max(x => x.DecidedAt);
        }
        Need(decisions.All(x => stages.Any(stage => stage.Id == x.StageId && stage.Assignments.Any(a => a.Id == x.AssignmentId)))
            && final.DecidedAt == previousCompleted);
    }
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("InvoiceUnitReviewTerminalInvalid"); }
}
