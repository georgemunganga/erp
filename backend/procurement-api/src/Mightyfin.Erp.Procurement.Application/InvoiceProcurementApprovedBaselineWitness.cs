using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Tracked Core case and complete decision graph loaded by the Host on
/// the same shared transaction. This is not a caller-supplied approval flag.</summary>
public sealed record InvoiceProcurementApprovedBaselineWitness(
    ApprovalCase CoreCase, IReadOnlyList<ApprovalDecision> Decisions,
    string ReviewerPrincipalId, Guid? ReviewerWorkerAliasId);
