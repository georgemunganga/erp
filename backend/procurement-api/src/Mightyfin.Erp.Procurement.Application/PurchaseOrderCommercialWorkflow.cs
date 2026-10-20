using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PurchaseOrderSourceLine(PurchaseOrderLine OrderLine, ApprovedPurchaseSource Source,
    PurchaseRequestAllocation Allocation);
public sealed record PurchaseOrderPolicyRoute(IReadOnlyList<PolicyPreviewStage> Stages, string DecisionJson);

/// <summary>
/// Purchasing invariants and immutable evidence, not task execution or accounting. Callers
/// use Core ApprovalEngine and the shared transaction coordinator, resolve current canonical
/// authority and Finance provenance, and persist every effect/audit/outbox atomically.
/// </summary>
public static class PurchaseOrderCommercialWorkflow
{
    public static PurchaseOrderSubmission Submit(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        Supplier supplier, PolicyVersion policy, PurchaseOrderFinanceEvidence finance,
        Guid coreCaseId, DateTimeOffset now, Guid buyerWorkerId)
    {
        if (order.Status is not ("Draft" or "Returned") || coreCaseId == Guid.Empty || order.CurrentRevision < 0)
            Fail("OrderSubmissionStateInvalid");
        ValidateLines(order, lines, supplier, now, "Reserved");
        var revision = checked(order.CurrentRevision + 1);
        var route = PolicyRoute(order, lines, policy, now);
        var (lineBase, gross) = Totals(lines.Select(x => x.OrderLine));
        ValidateFinance(order, lines, supplier, finance, revision, gross, now, buyerWorkerId);
        var snapshot = Snapshot(order, lines.Select(x => x.OrderLine).ToArray(), supplier, revision);
        var pin = new PurchaseOrderSubmission
        {
            TenantId = order.TenantId, LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id,
            CommercialRevision = revision, SubmittedRowVersion = checked(order.Version + 1), CoreApprovalCaseId = coreCaseId,
            PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber, FinanceEvidenceId = finance.Id,
            BuyerPrincipalId = order.BuyerSubjectId, BuyerWorkerId = buyerWorkerId,
            ParticipantWorkersJson = PurchaseOrderWorkerIdentity.Participants(lines, buyerWorkerId),
            WorkerIdentitySha256 = PurchaseOrderWorkerIdentity.Digest(order, revision, coreCaseId, buyerWorkerId,
                PurchaseOrderWorkerIdentity.Participants(lines, buyerWorkerId)), CurrencyCode = order.CurrencyCode, AmountBasis = "ApprovedLineBase",
            LineBaseAmount = lineBase, GrossAmount = gross, CommercialSnapshotJson = snapshot,
            CommercialSnapshotSha256 = ProcurementEvidenceEncoding.Digest(snapshot), SourceEvidenceJson = SourceSnapshot(lines),
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson), PolicyDecisionJson = route.DecisionJson,
            SubmittedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value,
        };
        order.CurrentRevision = revision; order.SubmittedBusinessVersion = revision; order.SubmittedAt = pin.SubmittedAt;
        order.ApprovalInstanceRef = coreCaseId.ToString("D"); order.FinanceCommitmentRef = finance.CommitmentReference;
        order.ReturnReason = null; order.Status = "PendingApproval";
        return pin;
    }

    public static void Verify(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        Supplier supplier, PurchaseOrderSubmission pin, PolicyVersion policy,
        PurchaseOrderFinanceEvidence finance, DateTimeOffset now, bool ordered = false)
    {
        ValidateLines(order, lines, supplier, now, ordered ? "Ordered" : "Reserved");
        var (lineBase, gross) = Totals(lines.Select(x => x.OrderLine));
        if (pin.TenantId != order.TenantId || pin.LegalEntityId != order.LegalEntityId || pin.PurchaseOrderId != order.Id
            || !PurchaseOrderWorkerIdentity.Verify(pin, order, lines)
            || pin.CommercialRevision != order.CurrentRevision || order.SubmittedBusinessVersion != pin.CommercialRevision
            || order.Version < pin.SubmittedRowVersion || pin.BuyerPrincipalId != order.BuyerSubjectId
            || order.ApprovalInstanceRef != pin.CoreApprovalCaseId.ToString("D")
            || pin.CurrencyCode != order.CurrencyCode || pin.AmountBasis != "ApprovedLineBase"
            || pin.LineBaseAmount != lineBase || pin.GrossAmount != gross
            || pin.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(pin.CommercialSnapshotJson)
            || pin.CommercialSnapshotJson != Snapshot(order, lines.Select(x => x.OrderLine).ToArray(), supplier, pin.CommercialRevision)
            || pin.SourceEvidenceJson != SourceSnapshot(lines)
            || pin.PolicyVersionId != policy.Id || pin.PolicyVersionNumber != policy.PolicyVersionNumber
            || pin.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || pin.PolicyDecisionJson != PolicyRoute(order, lines, policy, pin.SubmittedAt).DecisionJson
            || pin.FinanceEvidenceId != finance.Id || order.FinanceCommitmentRef != finance.CommitmentReference)
            Fail("OrderSubmittedEvidenceChanged");
        ValidateFinance(order, lines, supplier, finance, pin.CommercialRevision, gross, now, pin.BuyerWorkerId);
    }

    public static void ApplyDecision(PurchaseOrder order, PurchaseOrderSubmission pin,
        ApprovalCase approvalCase, ApprovalDecision decision, string? reason, Guid actorWorkerId)
    {
        if (approvalCase.TenantId != order.TenantId || approvalCase.LegalEntityId != order.LegalEntityId
            || approvalCase.Id != pin.CoreApprovalCaseId || approvalCase.SourceModule != "procurement"
            || approvalCase.SourceType != "purchase_order" || approvalCase.SourceId != order.Id
            || approvalCase.SourceVersion != pin.CommercialRevision || approvalCase.PolicyVersionId != pin.PolicyVersionId
            || approvalCase.PolicyVersionNumber != pin.PolicyVersionNumber || approvalCase.MakerSubjectId != order.BuyerSubjectId
            || decision.CaseId != approvalCase.Id || decision.TenantId != order.TenantId || decision.LegalEntityId != order.LegalEntityId
            || !PurchaseOrderWorkerIdentity.IsIndependent(pin.ParticipantWorkersJson, actorWorkerId)
            || decision.ActorSubjectId == order.BuyerSubjectId || order.Status != "PendingApproval")
            Fail("OrderCoreDecisionMismatch");
        if (approvalCase.State == "Pending") return;
        if (approvalCase.State == "Approved") order.Status = "Approved";
        else if (approvalCase.State is "Returned" or "Rejected")
        {
            if (string.IsNullOrWhiteSpace(reason)) Fail("OrderDecisionReasonRequired");
            // Keep source allocations and Finance commitment intact until an explicit
            // verified release/adjustment. Returned is not an unrestricted editable draft.
            order.Status = approvalCase.State; order.ReturnReason = reason;
        }
        else Fail("OrderCoreOutcomeUnsupported");
    }

    public static PurchaseOrderIssuedVersion AuthorizeIssue(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, Supplier supplier, PurchaseOrderSubmission pin,
        PolicyVersion policy, PurchaseOrderFinanceEvidence finance, ApprovalCase approvalCase,
        string actor, DateTimeOffset now)
    {
        Verify(order, lines, supplier, pin, policy, finance, now);
        if (order.Status != "Approved" || approvalCase.Id != pin.CoreApprovalCaseId || approvalCase.State != "Approved"
            || approvalCase.SourceId != order.Id || approvalCase.SourceType != "purchase_order"
            || approvalCase.SourceModule != "procurement" || approvalCase.SourceVersion != pin.CommercialRevision
            || approvalCase.TenantId != order.TenantId || approvalCase.LegalEntityId != order.LegalEntityId
            || approvalCase.PolicyVersionId != pin.PolicyVersionId || approvalCase.PolicyVersionNumber != pin.PolicyVersionNumber
            || string.IsNullOrWhiteSpace(actor)) Fail("OrderIssueNotAuthorized");
        foreach (var line in lines) PurchaseOrderSourceAllocation.ConfirmOrdered(line.Source, order,
            line.OrderLine, line.Allocation, now);
        var issued = new PurchaseOrderIssuedVersion
        {
            TenantId = order.TenantId, LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id,
            SubmissionId = pin.Id, RevisionNumber = pin.CommercialRevision, CommercialSnapshotJson = pin.CommercialSnapshotJson,
            CommercialSnapshotSha256 = pin.CommercialSnapshotSha256, FinanceCommitmentReference = finance.CommitmentReference,
            IssueEffectKey = $"procurement.po.issue.{pin.Id:N}.v1", IssuedByPrincipalId = actor,
            ParticipantWorkersJson = pin.ParticipantWorkersJson,
            AuthorizedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value,
        };
        order.Status = "IssuePending";
        return issued;
    }

    public static PurchaseOrderDeliveryEvidence ConfirmDelivery(PurchaseOrder order,
        PurchaseOrderIssuedVersion issued, string reference, string outcome, string channel,
        string evidenceReference, string verifier, DateTimeOffset observedAt, DateTimeOffset now, Guid verifierWorkerId)
    {
        if (order.Status is not ("IssuePending" or "Issued") || issued.PurchaseOrderId != order.Id
            || issued.TenantId != order.TenantId || issued.LegalEntityId != order.LegalEntityId
            || issued.RevisionNumber != order.CurrentRevision || observedAt < issued.AuthorizedAt || observedAt > now
            || !PurchaseOrderWorkerIdentity.IsIndependent(issued.ParticipantWorkersJson, verifierWorkerId)
            || outcome is not ("Delivered" or "Failed" or "Unknown")
            || new[] { reference, channel, evidenceReference, verifier }.Any(string.IsNullOrWhiteSpace))
            Fail("OrderDeliveryEvidenceInvalid");
        if (order.Status == "Issued" && outcome != "Delivered") Fail("OrderDeliveryAlreadyConfirmed");
        if (outcome == "Delivered") { order.Status = "Issued"; order.IssuedAt = observedAt; }
        return new PurchaseOrderDeliveryEvidence
        {
            TenantId = order.TenantId, LegalEntityId = order.LegalEntityId, IssuedVersionId = issued.Id,
            DeliveryReference = reference, Outcome = outcome, Channel = channel, EvidenceReference = evidenceReference,
            VerifiedByPrincipalId = verifier, VerifiedByWorkerId = verifierWorkerId, ObservedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(observedAt)!.Value,
        };
    }

    public static ProcurementSpendContext SpendingContext(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines)
    {
        var (lineBase, _) = Totals(lines.Select(x => x.OrderLine));
        return new(order.Id, order.TenantId, order.LegalEntityId, order.CurrencyCode, "ApprovedLineBase", lineBase,
            order.BranchRef, lines.Select(x => x.Source.Request.DepartmentRef).Distinct().ToArray(),
            lines.Select(x => x.Source.Line.CategoryCode ?? "").Distinct(StringComparer.Ordinal).ToArray());
    }

    public static PurchaseOrderPolicyRoute PolicyRoute(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, PolicyVersion policy, DateTimeOffset at)
    {
        if (policy.TenantId != order.TenantId || policy.LegalEntityId != order.LegalEntityId
            || policy.PolicyKey != "PURCHASE_ORDER" || policy.State != "Published" || policy.EffectiveFrom > at
            || policy.EffectiveTo <= at || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson).Length == 0)
            Fail("OrderPublishedPolicyRequired");
        using var json = JsonDocument.Parse(policy.RulesJson);
        if (json.RootElement.GetProperty("transactionType").GetString() != "PurchaseOrder"
            || json.RootElement.GetProperty("amountBasis").GetString() != "ApprovedLineBase") Fail("OrderPolicyBasisMismatch");
        if (lines.Count == 0) Fail("OrderActiveSourcesInvalid");
        var approvalAmount = Totals(lines.Select(x => x.OrderLine)).LineBase;
        // Thresholds cover the whole proposed order, even when category conditions differ by line.
        var results = lines.OrderBy(x => x.OrderLine.LineNumber).Select(x => new
        {
            lineId = x.OrderLine.Id,
            result = PolicyRulePreview.Evaluate(policy.RulesJson, new(order.CurrencyCode,
                approvalAmount, x.Source.Line.CategoryCode,
                x.Source.Line.PurchaseType, order.BranchRef)),
            method = x.Source.Method.Code,
        }).ToArray();
        if (results.Any(x => x.result.Status != "Match" || x.result.MinimumQuoteCount is > 0
            || x.result.SourcingMethodCode is { } method && method != x.method || x.result.ApprovalStages.Count == 0))
            Fail("OrderPolicyNeedsSourcingOrReview");
        var stages = results[0].result.ApprovalStages;
        var route = JsonSerializer.Serialize(stages);
        if (results.Any(x => JsonSerializer.Serialize(x.result.ApprovalStages) != route)) Fail("OrderMixedApprovalRoutesUnsupported");
        return new(stages, JsonSerializer.Serialize(results.Select(x => new { x.lineId, x.result }).ToArray()));
    }

    public static (decimal LineBase, decimal Gross) Totals(IEnumerable<PurchaseOrderLine> lines)
    {
        decimal lineBase = 0, gross = 0;
        foreach (var line in lines)
        { lineBase = checked(lineBase + line.Quantity * line.UnitPrice); gross = checked(gross + line.Quantity * line.UnitPrice + line.TaxAmount); }
        return (lineBase, gross);
    }

    private static void ValidateLines(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        Supplier supplier, DateTimeOffset at, string allocationStatus)
    {
        if (supplier.Id != order.SupplierId || supplier.TenantId != order.TenantId || supplier.LegalEntityId != order.LegalEntityId
            || !SupplierEligibility.CanReceiveNewOrders(supplier, at)) Fail("OrderSupplierUnavailable");
        if (lines.Count is < 1 or > 100 || lines.Select(x => x.OrderLine.Id).Distinct().Count() != lines.Count
            || lines.Select(x => x.Source.Line.Id).Distinct().Count() != lines.Count) Fail("OrderActiveSourcesInvalid");
        foreach (var item in lines)
        {
            var line = item.OrderLine; var source = item.Source;
            _ = PurchaseOrderSourceAllocation.Check(source, order.TenantId, order.LegalEntityId, at);
            if (!line.IsActive || line.TenantId != order.TenantId || line.LegalEntityId != order.LegalEntityId
                || line.PurchaseOrderId != order.Id || line.PurchaseRequestLineId != source.Line.Id
                || source.Request.CurrencyCode != order.CurrencyCode || source.Request.BranchRef != order.BranchRef
                || line.Quantity <= 0 || decimal.Round(line.Quantity, 4) != line.Quantity
                || line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 4) != line.UnitPrice
                || line.TaxAmount < 0 || decimal.Round(line.TaxAmount, 4) != line.TaxAmount
                || !string.Equals(line.UnitCode, source.Line.UnitCode, StringComparison.OrdinalIgnoreCase)
                || item.Allocation.TargetId != order.Id || item.Allocation.TargetType != "PurchaseOrder"
                || item.Allocation.Status != allocationStatus || item.Allocation.Quantity != line.Quantity
                || item.Allocation.PurchaseRequestLineId != source.Line.Id
                || source.Allocations.Count(x => x.Id == item.Allocation.Id && ReferenceEquals(x, item.Allocation)) != 1)
                Fail("OrderCommercialSourceMismatch");
        }
    }

    private static void ValidateFinance(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        Supplier supplier, PurchaseOrderFinanceEvidence finance, int revision, decimal gross, DateTimeOffset now, Guid buyerWorkerId)
    {
        if (finance.TenantId != order.TenantId || finance.LegalEntityId != order.LegalEntityId
            || finance.PurchaseOrderId != order.Id || finance.SupplierId != order.SupplierId
            || finance.CommercialRevision != revision || finance.Status != "Confirmed"
            || finance.CurrencyCode != order.CurrencyCode || finance.ConfirmedGrossAmount != gross
            || finance.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(
                Snapshot(order, lines.Select(x => x.OrderLine).ToArray(), supplier, revision))
            || !PurchaseOrderWorkerIdentity.IsIndependent(PurchaseOrderWorkerIdentity.Participants(lines, buyerWorkerId),
                finance.VerifiedByWorkerId)
            || finance.EvidenceMode != "ApprovedManualEvidence" || string.IsNullOrWhiteSpace(finance.ApprovedManualPolicyReference)
            || finance.ConfirmedAt == default || finance.ConfirmedAt > now || finance.VerifiedByPrincipalId == order.BuyerSubjectId
            || new[] { finance.CommitmentReference, finance.EvidenceReference, finance.VerifiedByPrincipalId }.Any(string.IsNullOrWhiteSpace))
            Fail("OrderFinanceCommitmentNotConfirmed");
        var expected = lines.Select(x => x.Source.Finance.ReservationRef!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var actual = JsonSerializer.Deserialize<string[]>(finance.SourceReservationsJson);
        if (actual is null || actual.Length != actual.Distinct(StringComparer.Ordinal).Count()
            || !actual.Order(StringComparer.Ordinal).SequenceEqual(expected)) Fail("OrderFinanceReservationTransferMismatch");
        if (order.FinanceCommitmentRef is { } previous && previous != finance.CommitmentReference
            && finance.ReplacesCommitmentReference != previous) Fail("OrderFinanceReplacementNotConfirmed");
    }

    private static string SourceSnapshot(IReadOnlyList<PurchaseOrderSourceLine> lines) => JsonSerializer.Serialize(
        lines.OrderBy(x => x.OrderLine.LineNumber).Select(x => new
        {
            orderLineId = x.OrderLine.Id, allocationId = x.Allocation.Id, requestId = x.Source.Request.Id,
            requestLineId = x.Source.Line.Id, sourceSubmissionId = x.Source.Submission.Id,
            sourceBusinessVersion = x.Source.Submission.SubmittedBusinessVersion,
            sourceCaseId = x.Source.CoreCase.Id, sourceOutcomeId = x.Source.Outcome.Id,
            sourceSnapshotHash = x.Source.Submission.RequestSnapshotSha256,
            quantity = ProcurementEvidenceEncoding.Decimal(x.Allocation.Quantity),
            approvedUnitPrice = ProcurementEvidenceEncoding.Decimal(x.Source.Line.EstimatedUnitPrice),
            x.Source.Line.UnitCode, x.Source.Line.CategoryCode, x.Source.Line.PurchaseType,
            methodId = x.Source.Method.Id, methodVersion = x.Source.Method.Version,
            financeSnapshotId = x.Source.Finance.Id, x.Source.Finance.ReservationRef,
        }).ToArray());

    public static string Snapshot(PurchaseOrder order, IReadOnlyList<PurchaseOrderLine> lines,
        Supplier supplier, int revision) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, order.TenantId, order.LegalEntityId, order.Id, order.Number, revision,
        order.BuyerSubjectId, order.BranchRef, order.CurrencyCode, order.SupplierId,
        supplier.LegalName, supplier.DisplayName, supplier.CountryCode,
        order.ShipToRef, order.BillToRef, order.PaymentTermRef, order.ExpectedDeliveryOn,
        order.SourcingAwardId, order.SupplierContractVersionId,
        lines = lines.OrderBy(x => x.LineNumber).Select(x => new
        { x.Id, x.LineNumber, x.PurchaseRequestLineId, x.CatalogItemId, x.Description, x.UnitCode,
            quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity), unitPrice = ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
            tax = ProcurementEvidenceEncoding.Decimal(x.TaxAmount), x.CostCenterRef, x.ProjectRef, x.ExpectedDeliveryOn }).ToArray(),
    });
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
