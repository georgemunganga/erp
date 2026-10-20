using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

// Proposed v2 source contract only. No runtime provider, policy editor or persistence is installed.
public sealed record InvoiceRetainedUnitReviewEnvelope(int SchemaVersion, string TenantId, Guid LegalEntityId,
    Guid? BranchId, Guid ReviewId, long ReviewSourceVersion, Guid InvoiceId, long InvoiceVersionAtReview,
    Guid MappingVersionId, int MappingRevision, string MappingSha256, string InvoiceOriginalSha256,
    Guid PurchaseOrderId, Guid IssuedVersionId, Guid MatchingPolicyId, int MatchingPolicyVersion,
    string MatchingPolicySha256, Guid ReviewPolicyId, int ReviewPolicyVersion, string ReviewPolicySha256,
    Guid CoreCaseId, Guid FinalDecisionId, string ReviewerPrincipalId, Guid ReviewerWorkerId,
    string MakerPrincipalId, Guid MakerWorkerId, DateTimeOffset ReviewedAt,
    IReadOnlyList<InvoiceReviewedDocumentLine> Lines, IReadOnlyList<InvoiceUnitReviewAssignmentWorker> AssignmentWorkers);

/// <summary>Retained canonical identity pins from owned review assignment evidence, not current
/// account lookups or HTTP authority declarations. Original offboarding does not change history.</summary>
public sealed record InvoiceUnitReviewAssignmentWorker(Guid AssignmentId, string PrincipalId, Guid WorkerId);

/// <summary>All records must be loaded from the authenticated owning store under its transaction.
/// The pure verifier validates binding, not the provenance of arbitrary in-memory objects.</summary>
public sealed record InvoiceRetainedUnitReviewAuthority(Guid ReviewId, long ReviewSourceVersion,
    string EvidenceJson, string EvidenceSha256, ApprovalCase Case,
    IReadOnlyList<ApprovalDecision> Decisions, IReadOnlyList<InvoiceUnitReviewAssignmentWorker> Workers,
    PolicyVersion MatchingPolicy, PolicyVersion ReviewPolicy);

/// <summary>Current authorized comparison caller, distinct from the historical signer.
/// Document hashes are owner-authorized current Available versions for this same invoice.</summary>
public sealed record InvoiceUnitReviewConsumer(string TenantId, Guid LegalEntityId, Guid? BranchId,
    string PrincipalId, Guid WorkerId, IReadOnlySet<string> Permissions, DateTimeOffset At,
    IReadOnlyDictionary<Guid, string> DocumentVersionHashes, Guid? SelectedBranchId,
    IReadOnlySet<Guid> AllowedBranches, bool OrganisationWide);

public static class InvoiceRetainedUnitReview
{
    public const string ReviewPolicyKey = "INVOICE_UNIT_REVIEW";
    public const string ReviewSourceType = "invoice_unit_review";
    public const string ReviewPermission = "procurement-invoice-unit-review";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>Version2 only; v1 caller-bound evidence is never silently promoted to an
    /// independent review. Returns unit evidence only, never AP/history/claim authority.</summary>
    public static IReadOnlyDictionary<Guid, string> Verify(InvoiceRetainedUnitReviewAuthority authority,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        InvoiceMatchMappingVersion mapping, InvoiceUnitReviewConsumer consumer)
    {
        try { return VerifyCore(authority, invoice, originalLines, mapping, consumer); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException
            or FormatException or OverflowException or KeyNotFoundException or NullReferenceException)
        { throw new InvalidOperationException("InvoiceRetainedUnitReviewInvalid", error); }
    }

    private static IReadOnlyDictionary<Guid, string> VerifyCore(InvoiceRetainedUnitReviewAuthority authority,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        InvoiceMatchMappingVersion mapping, InvoiceUnitReviewConsumer consumer)
    {
        Need(authority is not null && invoice is not null && mapping is not null && consumer is not null);
        Need(consumer.TenantId == invoice.TenantId && consumer.LegalEntityId == invoice.LegalEntityId
            && consumer.BranchId == invoice.BranchRef && consumer.BranchId != Guid.Empty
            && consumer.SelectedBranchId != Guid.Empty && consumer.AllowedBranches is not null
            && (consumer.SelectedBranchId is null || consumer.SelectedBranchId == invoice.BranchRef)
            && (consumer.OrganisationWide || invoice.BranchRef is { } branch && consumer.AllowedBranches.Contains(branch))
            && !string.IsNullOrWhiteSpace(consumer.PrincipalId) && consumer.WorkerId != Guid.Empty
            && consumer.At != default && consumer.Permissions is not null
            && consumer.Permissions.Contains("procurement-access") && consumer.Permissions.Contains("procurement-invoice-match")
            && consumer.DocumentVersionHashes is not null && invoice.CurrentMappingVersionId == mapping.Id
            && invoice.MappingRevision == mapping.MappingRevision);
        Need(originalLines is not null && originalLines.Count is > 0 and <= 200);
        var snapshot = InvoiceMappingEvidence.Verify(mapping, invoice, originalLines);
        Need(authority.ReviewId != Guid.Empty && authority.ReviewSourceVersion > 0
            && Sha(authority.EvidenceSha256) && !string.IsNullOrWhiteSpace(authority.EvidenceJson)
            && ProcurementEvidenceEncoding.PolicyJsonDigest(authority.EvidenceJson).Length == 64
            && ProcurementEvidenceEncoding.Digest(authority.EvidenceJson) == authority.EvidenceSha256);
        var evidence = JsonSerializer.Deserialize<InvoiceRetainedUnitReviewEnvelope>(authority.EvidenceJson, Json);
        Need(evidence is not null && evidence.SchemaVersion == 2
            && evidence.TenantId == invoice.TenantId && evidence.LegalEntityId == invoice.LegalEntityId
            && evidence.BranchId == invoice.BranchRef && evidence.ReviewId == authority.ReviewId
            && evidence.ReviewSourceVersion == authority.ReviewSourceVersion && evidence.InvoiceId == invoice.Id
            && evidence.InvoiceVersionAtReview > 0 && evidence.InvoiceVersionAtReview <= invoice.Version
            && evidence.MappingVersionId == mapping.Id && evidence.MappingRevision == mapping.MappingRevision
            && evidence.MappingSha256 == mapping.MappingSha256 && evidence.InvoiceOriginalSha256 == mapping.InvoiceOriginalSha256
            && evidence.PurchaseOrderId == mapping.PurchaseOrderId && evidence.IssuedVersionId == mapping.IssuedVersionId
            && evidence.MakerPrincipalId == mapping.ActorPrincipalId && evidence.MakerWorkerId == mapping.ActorWorkerId
            && evidence.MakerWorkerId != Guid.Empty && !string.IsNullOrWhiteSpace(evidence.MakerPrincipalId)
            && evidence.ReviewerWorkerId != Guid.Empty && !string.IsNullOrWhiteSpace(evidence.ReviewerPrincipalId)
            && evidence.MakerPrincipalId != evidence.ReviewerPrincipalId && evidence.MakerWorkerId != evidence.ReviewerWorkerId
            && evidence.ReviewedAt >= mapping.ReviewedAt && evidence.ReviewedAt <= consumer.At);

        // Policy identities are intentionally distinct. No current matching rule is relabelled
        // as a review assignment policy and no rule defaults are invented by this verifier.
        var matching = authority.MatchingPolicy; var review = authority.ReviewPolicy;
        Need(matching is not null && review is not null && matching.Id != review.Id
            && matching.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey && review.PolicyKey == ReviewPolicyKey
            && Policy(matching, invoice, evidence.ReviewedAt, consumer.At)
            && Policy(review, invoice, evidence.ReviewedAt, consumer.At)
            && evidence.MatchingPolicyId == matching.Id && evidence.MatchingPolicyVersion == matching.PolicyVersionNumber
            && evidence.MatchingPolicySha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(matching.RulesJson)
            && evidence.ReviewPolicyId == review.Id && evidence.ReviewPolicyVersion == review.PolicyVersionNumber
            && evidence.ReviewPolicySha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(review.RulesJson));
        VerifyCase(authority, evidence, invoice, mapping.ReviewedAt);

        Need(evidence.Lines is not null && evidence.Lines.Count == snapshot.Lines.Count
            && evidence.Lines.All(x => x is not null) && evidence.Lines.Select(x => x.InvoiceLineId).Distinct().Count() == snapshot.Lines.Count);
        var units = new Dictionary<Guid, string>();
        foreach (var link in snapshot.Lines)
        {
            var retained = evidence.Lines.SingleOrDefault(x => x.InvoiceLineId == link.InvoiceLineId);
            Need(retained is not null && link.DocumentVersionId != Guid.Empty && retained.DocumentVersionId == link.DocumentVersionId
                && Sha(retained.DocumentSha256) && consumer.DocumentVersionHashes.TryGetValue(link.DocumentVersionId, out var expectedHash)
                && expectedHash == retained.DocumentSha256 && !string.IsNullOrWhiteSpace(link.DocumentLineReference)
                && retained.DocumentLineReference == link.DocumentLineReference && retained.UnitCode == link.InvoiceUnitCode);
            units.Add(link.InvoiceLineId, retained!.UnitCode);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(units);
    }

    private static void VerifyCase(InvoiceRetainedUnitReviewAuthority authority, InvoiceRetainedUnitReviewEnvelope evidence,
        SupplierInvoice invoice, DateTimeOffset mappingCreatedAt)
    {
        var workflow = authority.Case;
        Need(workflow is not null && Scoped(workflow, invoice) && workflow.Id == evidence.CoreCaseId
            && workflow.SourceModule == "procurement" && workflow.SourceType == ReviewSourceType
            && workflow.SourceId == evidence.ReviewId && workflow.SourceVersion == evidence.ReviewSourceVersion
            && workflow.PolicyVersionId == evidence.ReviewPolicyId && workflow.PolicyVersionNumber == evidence.ReviewPolicyVersion
            && workflow.MakerSubjectId == evidence.MakerPrincipalId && workflow.State == "Approved"
            && workflow.OpenedAt >= mappingCreatedAt && workflow.OpenedAt <= evidence.ReviewedAt
            && authority.ReviewPolicy.PublishedAt <= workflow.OpenedAt && authority.ReviewPolicy.EffectiveFrom <= workflow.OpenedAt
            && authority.MatchingPolicy.PublishedAt <= workflow.OpenedAt && authority.MatchingPolicy.EffectiveFrom <= workflow.OpenedAt
            && workflow.CompletedAt == evidence.ReviewedAt && !string.IsNullOrWhiteSpace(workflow.CompletionKey)
            && workflow.Stages is not null && workflow.Stages.Count > 0
            && workflow.Stages.Select(x => x.Id).Distinct().Count() == workflow.Stages.Count
            && workflow.Stages.Select(x => x.Sequence).Distinct().Count() == workflow.Stages.Count
            && authority.Decisions is not null && authority.Workers is not null
            && authority.Decisions.Select(x => x.Id).Distinct().Count() == authority.Decisions.Count
            && authority.Workers.Select(x => x.AssignmentId).Distinct().Count() == authority.Workers.Count
            && evidence.AssignmentWorkers is not null && evidence.AssignmentWorkers.Count == authority.Workers.Count
            && evidence.AssignmentWorkers.Select(x => x.AssignmentId).Distinct().Count() == authority.Workers.Count
            && authority.Workers.All(pin => evidence.AssignmentWorkers.Contains(pin)));
        var assignments = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
        Need(assignments.Length > 0 && assignments.Select(x => x.Id).Distinct().Count() == assignments.Length
            && authority.Workers.Count == assignments.Length
            && authority.Workers.All(pin => assignments.Any(x => x.Id == pin.AssignmentId && x.AssigneeSubjectId == pin.PrincipalId)
                && pin.WorkerId != Guid.Empty && pin.WorkerId != evidence.MakerWorkerId && pin.PrincipalId != evidence.MakerPrincipalId));
        foreach (var stage in workflow.Stages)
        {
            Need(Scoped(stage, invoice) && stage.CaseId == workflow.Id && stage.State == "Approved"
                && stage.Mode is "Any" or "All" or "Quorum"
                && stage.RequiredApprovals > 0 && stage.RequiredApprovals <= stage.Assignments.Count
                && (stage.Mode != "Any" || stage.RequiredApprovals == 1)
                && (stage.Mode != "All" || stage.RequiredApprovals == stage.Assignments.Count)
                && stage.Assignments.Select(x => x.AssigneeSubjectId).Distinct().Count() == stage.Assignments.Count
                && stage.Assignments.Select(x => authority.Workers.Single(pin => pin.AssignmentId == x.Id).WorkerId).Distinct().Count() == stage.Assignments.Count
                && stage.Assignments.Count(x => x.State == "Approved") >= stage.RequiredApprovals);
            foreach (var assignment in stage.Assignments)
            {
                Need(Scoped(assignment, invoice) && assignment.CaseId == workflow.Id && assignment.StageId == stage.Id
                    && assignment.PermissionKey == ReviewPermission && assignment.State is "Approved" or "Superseded");
                var votes = authority.Decisions.Where(x => x.AssignmentId == assignment.Id).ToArray();
                if (assignment.State == "Superseded") { Need(votes.Length == 0); continue; }
                Need(votes.Length == 1);
                var vote = votes[0];
                Need(Scoped(vote, invoice) && vote.CaseId == workflow.Id && vote.StageId == stage.Id
                    && vote.ActorSubjectId == assignment.AssigneeSubjectId && vote.Action == "Approve"
                    && !string.IsNullOrWhiteSpace(vote.IdempotencyKey) && vote.DecidedAt >= workflow.OpenedAt
                    && vote.DecidedAt <= evidence.ReviewedAt);
            }
        }
        Need(authority.Decisions.All(x => assignments.Any(a => a.Id == x.AssignmentId)));
        var final = authority.Decisions.SingleOrDefault(x => x.Id == evidence.FinalDecisionId);
        Need(final is not null && final.IdempotencyKey == workflow.CompletionKey && final.DecidedAt == evidence.ReviewedAt
            && final.ActorSubjectId == evidence.ReviewerPrincipalId
            && authority.Workers.Single(x => x.AssignmentId == final.AssignmentId).WorkerId == evidence.ReviewerWorkerId);
    }

    private static bool Scoped(ApprovalRecord record, SupplierInvoice invoice)
        => record.Id != Guid.Empty && record.TenantId == invoice.TenantId && record.LegalEntityId == invoice.LegalEntityId;
    private static bool Policy(PolicyVersion policy, SupplierInvoice invoice, DateTimeOffset reviewedAt, DateTimeOffset now)
        => policy.Id != Guid.Empty && policy.TenantId == invoice.TenantId && policy.LegalEntityId == invoice.LegalEntityId
            && policy.PolicyVersionNumber > 0 && policy.State == "Published" && policy.PublishedAt is { } published
            && published <= reviewedAt && policy.EffectiveFrom <= reviewedAt && policy.EffectiveFrom <= now
            && (policy.EffectiveTo is null || now < policy.EffectiveTo.Value);
    private static bool Sha(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need([DoesNotReturnIf(false)] bool condition) { if (!condition) throw new InvalidOperationException("InvoiceRetainedUnitReviewInvalid"); }
}
