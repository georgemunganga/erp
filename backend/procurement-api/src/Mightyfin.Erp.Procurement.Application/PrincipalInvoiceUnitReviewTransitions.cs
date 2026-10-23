using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;
using Rules = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewPolicyRules;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Schema-2 original-unit review uses a distinct Core case and exact
/// principal mapping/document pins. No legacy issued or receiving FK is reused.</summary>
public static class PrincipalInvoiceUnitReviewTransitions
{
    public const string SourceType = "principal_invoice_original_unit_review";
    public sealed record PinnedUnit(Guid InvoiceLineId, Guid DocumentVersionId,
        string DocumentSha256, string DocumentLineReference, string UnitCode);
    public sealed record OpeningEvidence(int SchemaVersion, Guid InvoiceId, Guid MappingVersionId,
        string OriginalInvoiceSha256, string MappingSha256, Guid PurchaseOrderId,
        Guid PrincipalIssuedVersionId, string IssuedSnapshotSha256, Guid MatchingPolicyId,
        string MatchingPolicySha256, Guid ReviewPolicyId, string ReviewPolicySha256,
        string MakerPrincipalId, Guid? MakerWorkerAliasId, IReadOnlyList<PinnedUnit> Lines);
    public sealed record Plan(PrincipalInvoiceUnitReviewSource Source, ApprovalCase Case,
        IReadOnlyList<PrincipalInvoiceUnitReviewAssignment> Pins);

    public static Plan PrepareOpen(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originals,
        PrincipalInvoiceState state, PrincipalInvoiceMappingVersion mapping,
        IReadOnlyList<PrincipalInvoiceMappingLine> normalized, InvoiceIssuedOrder issued,
        IReadOnlyList<InvoiceAcceptedFulfilment> accepted, IReadOnlyDictionary<Guid, Guid> sourceStates,
        PolicyVersion matching, PolicyVersion review, Rules.Person maker,
        IReadOnlyList<Rules.Person> reviewers, IReadOnlyDictionary<Guid, string> documentHashes,
        DateTimeOffset at)
    {
        var snapshot = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, originals,
            issued, accepted, sourceStates, normalized);
        Require(state.SupplierInvoiceId == invoice.Id && state.TenantId == invoice.TenantId
            && state.LegalEntityId == invoice.LegalEntityId && state.BranchId == invoice.BranchRef
            && state.CurrentMappingId == mapping.Id && state.MappingRevision == mapping.MappingRevision
            && state.InvoiceVersion == invoice.Version && state.State == "Mapped"
            && invoice.Status is "Received" or "MatchPending" or "Exception"
            && invoice.FinancePostingRef is null && at >= mapping.MappedAt);
        Require(maker.PrincipalId == mapping.MakerPrincipalId && maker.WorkerId == mapping.MakerWorkerAliasId
            && maker.TenantId == invoice.TenantId && maker.LegalEntityId == invoice.LegalEntityId
            && maker.Active && maker.Permissions.Contains(InvoiceUnitReviewTransitions.RequestPermission)
            && (maker.OrganisationWide || invoice.BranchRef is { } branch && maker.AllowedBranches.Contains(branch)));
        Require(matching.Id != review.Id && matching.TenantId == invoice.TenantId
            && matching.LegalEntityId == invoice.LegalEntityId && matching.PublishedAt <= at
            && matching.EffectiveFrom <= at && (matching.EffectiveTo is null || at < matching.EffectiveTo)
            && review.TenantId == invoice.TenantId && review.LegalEntityId == invoice.LegalEntityId
            && review.PublishedAt <= at && review.EffectiveFrom <= at
            && (review.EffectiveTo is null || at < review.EffectiveTo));
        _ = InvoiceMatchingPolicyRules.Project(matching);
        var projection = Rules.Resolve(review, invoice.TenantId, invoice.LegalEntityId,
            invoice.BranchRef, maker.PrincipalId, maker.WorkerId, reviewers, at);
        var usedDocuments = snapshot.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray();
        Require(usedDocuments.Length == documentHashes.Count && usedDocuments.All(x =>
            documentHashes.TryGetValue(x, out var sha) && Sha(sha)));
        var pins = snapshot.Lines.OrderBy(x => x.InvoiceLineId).Select(x => new PinnedUnit(
            x.InvoiceLineId, x.DocumentVersionId, documentHashes[x.DocumentVersionId],
            x.DocumentLineReference, x.InvoiceUnitCode)).ToArray();
        var evidence = new OpeningEvidence(2, invoice.Id, mapping.Id,
            mapping.OriginalInvoiceSha256, mapping.MappingSha256, mapping.PurchaseOrderId,
            mapping.PrincipalIssuedVersionId, mapping.IssuedSnapshotSha256, matching.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(matching.RulesJson), review.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(review.RulesJson),
            maker.PrincipalId, maker.WorkerId, pins);
        var json = JsonSerializer.Serialize(evidence);
        var source = new PrincipalInvoiceUnitReviewSource
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, MappingVersionId = mapping.Id,
            PrincipalIssuedVersionId = mapping.PrincipalIssuedVersionId,
            BranchId = invoice.BranchRef!.Value, CoreCaseId = Guid.CreateVersion7(),
            MatchingPolicyId = matching.Id, ReviewPolicyId = review.Id,
            MappingSha256 = mapping.MappingSha256,
            OriginalInvoiceSha256 = mapping.OriginalInvoiceSha256,
            OriginalUnitsJson = json,
            OriginalUnitsSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            MakerPrincipalId = maker.PrincipalId, MakerWorkerAliasId = maker.WorkerId,
            OpenedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(at)!.Value
        };
        var coreCase = new ApprovalCase
        {
            Id = source.CoreCaseId, TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, SourceModule = "procurement",
            SourceType = SourceType, SourceId = source.Id, SourceVersion = 1,
            PolicyVersionId = projection.PolicyId, PolicyVersionNumber = projection.PolicyVersion,
            MakerSubjectId = maker.PrincipalId, OpenedAt = source.OpenedAt
        };
        var assignments = new List<PrincipalInvoiceUnitReviewAssignment>();
        foreach (var rule in projection.Stages)
        {
            var stage = new ApprovalStage
            {
                TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
                CaseId = coreCase.Id, Sequence = rule.Sequence, Code = rule.Code,
                Mode = rule.CoreMode, RequiredApprovals = rule.RequiredApprovals,
                State = rule.Sequence == 1 ? "Active" : "Waiting"
            };
            foreach (var person in rule.Assignees)
            {
                var assignment = new ApprovalAssignment
                {
                    TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
                    CaseId = coreCase.Id, StageId = stage.Id,
                    AssigneeSubjectId = person.PrincipalId,
                    PermissionKey = InvoiceRetainedUnitReview.ReviewPermission
                };
                stage.Assignments.Add(assignment);
                assignments.Add(new PrincipalInvoiceUnitReviewAssignment
                {
                    TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
                    ReviewSourceId = source.Id, CoreCaseId = coreCase.Id,
                    CoreAssignmentId = assignment.Id,
                    ReviewerPrincipalId = person.PrincipalId,
                    ReviewerWorkerAliasId = person.WorkerId
                });
            }
            coreCase.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(coreCase);
        Rules.VerifyCaseGraph(projection, coreCase, assignments.Select(x =>
            new InvoiceUnitReviewAssignmentWorker(x.CoreAssignmentId, x.ReviewerPrincipalId,
                x.ReviewerWorkerAliasId)).ToArray());
        return new(source, coreCase, assignments);
    }

    public static OpeningEvidence Verify(PrincipalInvoiceUnitReviewSource source,
        PrincipalInvoiceMappingVersion mapping, ApprovalCase coreCase,
        IReadOnlyList<PrincipalInvoiceUnitReviewAssignment> assignments)
    {
        Require(source.MappingVersionId == mapping.Id && source.SupplierInvoiceId == mapping.SupplierInvoiceId
            && source.PrincipalIssuedVersionId == mapping.PrincipalIssuedVersionId
            && source.MappingSha256 == mapping.MappingSha256
            && source.OriginalInvoiceSha256 == mapping.OriginalInvoiceSha256
            && source.MakerPrincipalId == mapping.MakerPrincipalId
            && source.MakerWorkerAliasId == mapping.MakerWorkerAliasId
            && source.OriginalUnitsSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(source.OriginalUnitsJson)
            && coreCase.Id == source.CoreCaseId && coreCase.SourceType == SourceType
            && coreCase.SourceId == source.Id && coreCase.SourceVersion == 1
            && coreCase.MakerSubjectId == source.MakerPrincipalId
            && assignments.Count > 0
            && assignments.Count == coreCase.Stages.SelectMany(stage => stage.Assignments).Count()
            && assignments.Select(x => x.CoreAssignmentId).Distinct().Count() == assignments.Count
            && assignments.All(x => x.ReviewSourceId == source.Id
                && x.CoreCaseId == coreCase.Id
                && coreCase.Stages.SelectMany(stage => stage.Assignments).Any(a => a.Id == x.CoreAssignmentId
                    && a.AssigneeSubjectId == x.ReviewerPrincipalId)));
        OpeningEvidence evidence;
        try { evidence = JsonSerializer.Deserialize<OpeningEvidence>(source.OriginalUnitsJson)
            ?? throw new InvalidOperationException("PrincipalInvoiceReviewEvidenceMissing"); }
        catch (JsonException error) { throw new InvalidOperationException("PrincipalInvoiceReviewEvidenceMalformed", error); }
        Require(evidence.SchemaVersion == 2 && evidence.InvoiceId == source.SupplierInvoiceId
            && evidence.MappingVersionId == mapping.Id
            && evidence.OriginalInvoiceSha256 == mapping.OriginalInvoiceSha256
            && evidence.MappingSha256 == mapping.MappingSha256
            && evidence.PurchaseOrderId == mapping.PurchaseOrderId
            && evidence.PrincipalIssuedVersionId == mapping.PrincipalIssuedVersionId
            && evidence.IssuedSnapshotSha256 == mapping.IssuedSnapshotSha256
            && evidence.MatchingPolicyId == source.MatchingPolicyId
            && evidence.ReviewPolicyId == source.ReviewPolicyId
            && evidence.MakerPrincipalId == source.MakerPrincipalId
            && evidence.MakerWorkerAliasId == source.MakerWorkerAliasId
            && evidence.Lines.Count is > 0 and <= 200
            && evidence.Lines.Select(x => x.InvoiceLineId).Distinct().Count() == evidence.Lines.Count
            && evidence.Lines.All(x => x.InvoiceLineId != Guid.Empty
                && x.DocumentVersionId != Guid.Empty && Sha(x.DocumentSha256)
                && !string.IsNullOrWhiteSpace(x.DocumentLineReference)
                && !string.IsNullOrWhiteSpace(x.UnitCode)));
        return evidence;
    }

    private static bool Sha(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool valid)
    {
        if (!valid) throw new InvalidOperationException("PrincipalInvoiceOriginalUnitReviewSourceChanged");
    }
}
