using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed literal-text storage codec. Origin/provenance is proved by the owning
/// transaction and Core rows; caller JSON/labels do not grant review authority.</summary>
public static class InvoiceUnitReviewStorage
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict };
    public static InvoiceUnitReviewSource Source(InvoiceUnitReviewTransitions.OpeningPlan plan, InvoiceIssuedOrder issued) => new()
    {
        Id = plan.Source.ReviewId, TenantId = plan.Source.TenantId, LegalEntityId = plan.Source.LegalEntityId,
        SupplierInvoiceId = plan.Source.InvoiceId, MappingVersionId = plan.Source.MappingId,
        PurchaseOrderId = plan.Source.PurchaseOrderId, IssuedVersionId = plan.Source.IssuedVersionId, BranchRef = plan.Source.BranchId,
        InvoiceVersionAtOpen = plan.Source.InvoiceVersionAtOpen, MappingRevision = plan.Source.MappingRevision,
        OriginalSha256 = plan.Source.OriginalSha256, MappingSha256 = plan.Source.MappingSha256,
        MatchingPolicyId = plan.Source.MatchingPolicyId, ReviewPolicyId = plan.Source.ReviewPolicy.PolicyId,
        CoreCaseId = plan.Source.CoreCaseId, MakerPrincipalId = plan.Source.ReviewPolicy.MakerPrincipalId,
        MakerWorkerId = plan.Source.ReviewPolicy.MakerWorkerId, OpenedAt = plan.Source.OpenedAt,
        SourceJson = plan.SourceJson, SourceSha256 = plan.SourceSha256, IssuedJson = JsonSerializer.Serialize(issued),
    };
    public static InvoiceUnitReviewTransitions.Source Read(InvoiceUnitReviewSource row)
    {
        Need(row.SourceJson is { Length: > 0 and <= 262144 } && Encoding.UTF8.GetByteCount(row.SourceJson) <= 262144
            && row.SourceSha256 == ProcurementEvidenceEncoding.Digest(row.SourceJson));
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(row.SourceJson); // recursive duplicates fail closed
        var source = JsonSerializer.Deserialize<InvoiceUnitReviewTransitions.Source>(row.SourceJson, Json)
            ?? throw new InvalidOperationException("InvoiceUnitReviewStoredSourceInvalid");
        Need(source.IdentitySchemaVersion == 3 && JsonSerializer.Serialize(source) == row.SourceJson && source.ReviewId == row.Id && source.SourceVersion == 1
            && (source.SourceSchemaVersion is null
                ? source.ManualReason is null && source.ManualLines is null
                    && source.ReviewPolicy.SchemaVersion is null && source.ReviewPolicy.EvidenceMode is null
                : source.SourceSchemaVersion == 4 && source.ManualReason is { Length: > 0 and <= 2000 }
                    && source.ManualLines is { Count: > 0 and <= 200 }
                    && source.ReviewPolicy.SchemaVersion == 2
                    && source.ReviewPolicy.EvidenceMode == "ManualAttestation" && source.Lines.Count == 0)
            && source.TenantId == row.TenantId && source.LegalEntityId == row.LegalEntityId && source.InvoiceId == row.SupplierInvoiceId
            && source.MappingId == row.MappingVersionId && source.MappingRevision == row.MappingRevision
            && source.PurchaseOrderId == row.PurchaseOrderId && source.IssuedVersionId == row.IssuedVersionId
            && source.BranchId == row.BranchRef && source.InvoiceVersionAtOpen == row.InvoiceVersionAtOpen
            && source.CoreCaseId == row.CoreCaseId && source.OriginalSha256 == row.OriginalSha256 && source.MappingSha256 == row.MappingSha256
            && source.MatchingPolicyId == row.MatchingPolicyId && source.ReviewPolicy.PolicyId == row.ReviewPolicyId
            && source.ReviewPolicy.MakerPrincipalId == row.MakerPrincipalId && source.ReviewPolicy.MakerWorkerId == row.MakerWorkerId
            && source.OpenedAt == row.OpenedAt && row.OpenedAt == ProcurementEvidenceEncoding.DatabaseTimestamp(row.OpenedAt)
            && source.ReviewPolicy.PolicyId != source.MatchingPolicyId);
        return source;
    }
    public static InvoiceUnitReviewTransitions.OpeningPlan Restore(InvoiceUnitReviewSource row,
        IReadOnlyList<InvoiceUnitReviewAssignmentEvidence> pins, ApprovalCase workflow)
    {
        var source = Read(row);
        Need(pins.Count > 0 && pins.Select(x => x.CoreAssignmentId).Distinct().Count() == pins.Count
            && pins.All(x => x.TenantId == row.TenantId && x.LegalEntityId == row.LegalEntityId && x.ReviewSourceId == row.Id
                && x.CoreCaseId == row.CoreCaseId && ProcurementPrincipalEvidence.Canonical(x.PrincipalId) && ProcurementPrincipalEvidence.OptionalWorker(x.WorkerId)));
        var workers = pins.Select(x => new InvoiceUnitReviewAssignmentWorker(x.CoreAssignmentId, x.PrincipalId, x.WorkerId)).ToArray();
        InvoiceUnitReviewPolicyRules.VerifyCaseGraph(source.ReviewPolicy, workflow, workers);
        Need(workflow.Id == row.CoreCaseId && workflow.SourceId == row.Id && workflow.SourceVersion == 1
            && workflow.TenantId == row.TenantId && workflow.LegalEntityId == row.LegalEntityId
            && workflow.SourceModule == "procurement" && workflow.SourceType == InvoiceRetainedUnitReview.ReviewSourceType
            && workflow.OpenedAt == row.OpenedAt);
        return new(source, row.SourceJson, row.SourceSha256, workflow, workers, Guid.Empty, null);
    }
    public static InvoiceUnitReviewOutcome Outcome(InvoiceUnitReviewTerminal.Outcome value, InvoiceUnitReviewSource source) => new()
    {
        TenantId = source.TenantId, LegalEntityId = source.LegalEntityId, ReviewSourceId = source.Id, CoreCaseId = value.CoreCaseId,
        FinalDecisionId = value.FinalDecisionId, ReviewerPrincipalId = value.ReviewerPrincipalId, ReviewerWorkerId = value.ReviewerWorkerId,
        State = value.State, EvidenceSchemaVersion = value.EvidenceSchemaVersion, EvidenceJson = value.EvidenceJson,
        EvidenceSha256 = value.EvidenceSha256, CompletedAt = value.CompletedAt,
    };
    public static void VerifyOutcome(InvoiceUnitReviewOutcome row, InvoiceUnitReviewTerminal.Outcome expected)
    {
        Need(row.ReviewSourceId == expected.ReviewId && row.CoreCaseId == expected.CoreCaseId && row.FinalDecisionId == expected.FinalDecisionId
            && row.State == expected.State && row.EvidenceSchemaVersion == expected.EvidenceSchemaVersion
            && row.ReviewerPrincipalId == expected.ReviewerPrincipalId && row.ReviewerWorkerId == expected.ReviewerWorkerId
            && row.CompletedAt == expected.CompletedAt && row.EvidenceJson == expected.EvidenceJson && row.EvidenceSha256 == expected.EvidenceSha256);
    }
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("InvoiceUnitReviewStoredEvidenceInvalid"); }
}
