using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Proves the mutable current PO still describes the exact sealed obligation.
/// Supplier labels are reconstructed from retained evidence, never current mutable profile/eligibility.
/// Current Finance commitment availability is intentionally not re-authorized at receiving.</summary>
public static class ReceivingIssuedAuthority
{
    public static void Verify(PurchaseOrder order, IReadOnlyList<PurchaseOrderLine> currentLines,
        PurchaseOrderIssuedVersion issued, PurchaseOrderSubmission submitted, ApprovalCase workflow, PolicyVersion originalPolicy)
    {
        Need(order.Status == "Issued" && issued.PurchaseOrderId == order.Id && issued.SubmissionId == submitted.Id
            && submitted.PurchaseOrderId == order.Id && order.CurrentRevision == issued.RevisionNumber
            && submitted.CommercialRevision == issued.RevisionNumber && order.SubmittedBusinessVersion == submitted.CommercialRevision
            && order.SubmittedAt == submitted.SubmittedAt && order.ApprovalInstanceRef == submitted.CoreApprovalCaseId.ToString("D")
            && submitted.BuyerPrincipalId == order.BuyerSubjectId && submitted.CurrencyCode == order.CurrencyCode
            && submitted.TenantId == order.TenantId && submitted.LegalEntityId == order.LegalEntityId
            && issued.TenantId == order.TenantId && issued.LegalEntityId == order.LegalEntityId
            && issued.CommercialSnapshotJson == submitted.CommercialSnapshotJson
            && issued.CommercialSnapshotSha256 == submitted.CommercialSnapshotSha256
            && issued.CommercialSnapshotSha256 == ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
            && workflow.Id == submitted.CoreApprovalCaseId && workflow.State == "Approved" && workflow.SourceModule == "procurement"
            && workflow.SourceType == "purchase_order" && workflow.SourceId == order.Id && workflow.SourceVersion == submitted.CommercialRevision
            && workflow.TenantId == order.TenantId && workflow.LegalEntityId == order.LegalEntityId
            && workflow.MakerSubjectId == submitted.BuyerPrincipalId && workflow.PolicyVersionId == submitted.PolicyVersionId
            && workflow.PolicyVersionNumber == submitted.PolicyVersionNumber && workflow.CompletedAt is { } completed && completed <= issued.AuthorizedAt
            && originalPolicy.Id == submitted.PolicyVersionId && originalPolicy.PolicyVersionNumber == submitted.PolicyVersionNumber
            && originalPolicy.TenantId == order.TenantId && originalPolicy.LegalEntityId == order.LegalEntityId
            && originalPolicy.PolicyKey == "PURCHASE_ORDER" && originalPolicy.State == "Published"
            && originalPolicy.EffectiveFrom <= submitted.SubmittedAt && (originalPolicy.EffectiveTo == null || submitted.SubmittedAt < originalPolicy.EffectiveTo)
            && submitted.PolicyRulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(originalPolicy.RulesJson), "ReceivingCurrentIssuedAuthorityChanged");
        Need(currentLines.Count > 0 && currentLines.All(x => x.PurchaseOrderId == order.Id && x.TenantId == order.TenantId && x.LegalEntityId == order.LegalEntityId && x.IsActive)
            && currentLines.Select(x => x.Id).Distinct().Count() == currentLines.Count, "ReceivingCurrentIssuedLinesChanged");
        using var document = JsonDocument.Parse(issued.CommercialSnapshotJson); var root = document.RootElement;
        var retainedSupplier = new Supplier { Id = order.SupplierId, LegalName = root.GetProperty("LegalName").GetString()!,
            DisplayName = root.GetProperty("DisplayName").GetString()!, CountryCode = root.GetProperty("CountryCode").GetString()! };
        Need(PurchaseOrderCommercialWorkflow.Snapshot(order, currentLines, retainedSupplier, issued.RevisionNumber) == issued.CommercialSnapshotJson,
            "ReceivingCurrentCommercialSnapshotChanged");
    }
    private static void Need(bool okay, string code) { if (!okay) throw new InvalidOperationException(code); }
}
