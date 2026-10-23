using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Closed until replaced by the genuine migration ID and unmodified PG17 owner capture.
// These are physical contracts, not business permissions or approval policy defaults.
internal static class ProcurementPrincipalCommercialSchemaCapture
{
    internal const string MigrationId = "P30_PENDING_GENUINE_OWNER_MIGRATION";
    internal static readonly IReadOnlyDictionary<string,string> Checks = new Dictionary<string,string>();
    internal static readonly IReadOnlyDictionary<string,string> OwnershipConstraints = new Dictionary<string,string>();
    internal static readonly Type[] Orders = [typeof(PurchaseOrder), typeof(PurchaseOrderLine), typeof(PrincipalOrderSourceBinding), typeof(PrincipalOrderDraftReceipt), typeof(PrincipalOrderFundingEvidence), typeof(PrincipalOrderSubmission), typeof(PrincipalOrderAssignmentEvidence), typeof(PrincipalOrderDecisionEvidence), typeof(PrincipalOrderIssuedVersion), typeof(PrincipalOrderAcknowledgment), typeof(PrincipalOrderReleaseRequest), typeof(PrincipalOrderReleaseConfirmation)];
    internal static readonly Type[] Publication = [typeof(SourcingEvent), typeof(SourcingLine), typeof(SourcingInvitation), typeof(SupplierBid), typeof(SupplierBidLine), typeof(SourcingPolicyIdentityEvidence), typeof(SourcingPublicationOpening), typeof(SourcingPublicationDecision), typeof(SourcingPublicationReceipt), typeof(SourcingManualContact), typeof(SourcingManualQuoteVersion)];
    internal static readonly Type[] Receiving = [typeof(PurchaseOrderLine), typeof(PurchaseRequestLine), typeof(PrincipalReceivingSourceState), typeof(PrincipalReceivingFact), typeof(PrincipalReceivingDraftHead), typeof(PrincipalReceivingDraftRevision), typeof(PrincipalReceivingCommandReceipt), typeof(PrincipalReceivingDraftLineBinding)];
    internal static readonly IReadOnlySet<string> ReceivingOwnershipNames = new HashSet<string>(StringComparer.Ordinal) { "procurement.principal_receiving_facts.fk_prcv_exact_fact_receipt", "procurement.principal_receiving_sources.fk_prcv_exact_issued", "procurement.principal_receiving_sources.fk_prcv_exact_ack", "procurement.principal_receiving_sources.fk_prcv_exact_original", "procurement.principal_receiving_sources.fk_prcv_exact_po_line", "procurement.principal_receiving_drafts.fk_prcv_exact_draft_issued", "procurement.principal_receiving_line_bindings.fk_prcv_exact_line_source", "procurement.principal_receiving_line_bindings.fk_prcv_exact_line_revision", "procurement.principal_receiving_facts.fk_prcv_exact_fact_line", "procurement.principal_receiving_receipts.fk_prcv_exact_receipt_draft", "procurement.principal_receiving_line_bindings.fk_prcv_exact_line_draft" };
}
