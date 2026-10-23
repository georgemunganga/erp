using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ManualPaymentCoreOpening(ApprovalCase Case, ManualPaymentWorkflowCaseLink Link);

/// <summary>
/// Source-module bindings over the shared Core approval engine. These pure checks do
/// not authenticate database provenance, grant permissions, persist records or publish
/// policy. The host must verify current Core people/publication and use one transaction.
/// </summary>
public static class ManualPaymentCoreWorkflow
{
    public const string Approval = "Approval";
    public const string Confirmation = "Confirmation";

    public static ManualPaymentCoreOpening Open(ManualPaymentChange opening, PolicyVersion policy,
        ManualPaymentInvoiceSource invoice, DateTimeOffset now,
        ManualPaymentWorkflowCaseLink? approvedLink = null, ApprovalCase? approvedCase = null)
    {
        var request = opening.Request; var decision = opening.Decision;
        var purpose = decision.Action switch { "Submit" => Approval, "Record" => Confirmation, _ => "" };
        Need(purpose.Length > 0 && now != default && Same(request, decision)
            && decision.ManualPaymentRequestId == request.Id && decision.BranchId == request.BranchId
            && decision.ToVersion == request.Version && decision.FromVersion + 1 == decision.ToVersion
            && (purpose == Approval ? request.Status == "PendingApproval" : request.Status == "PaymentRecorded")
            && decision.ToStatus == request.Status && invoice.IsEligible && invoice.InvoiceVersion > 0
            && invoice.InvoiceId == request.SupplierInvoiceId && invoice.SupplierId == request.SupplierId
            && invoice.TenantId == request.TenantId && invoice.LegalEntityId == request.LegalEntityId
            && invoice.BranchId == request.BranchId && invoice.CurrencyCode == request.CurrencyCode
            && invoice.InvoiceGrossAmount > 0 && ExactDecimalJsonCodec.IsRepresentable(invoice.InvoiceGrossAmount, 18, 4),
            "ManualPaymentCoreOpeningInvalid");
        ManualPaymentReviewPolicyDocument rules;
        string maker, assignee; Guid? makerAlias, assigneeAlias;
        if (purpose == Approval)
        {
            Need(decision.FromStatus == "Draft" && decision.ActorPrincipalId == request.CreatorPrincipalId
                && approvedLink is null && approvedCase is null, "ManualPaymentCoreOpeningInvalid");
            rules = ManualPaymentReviewPolicyRules.ResolveEffective(policy, request.TenantId, request.LegalEntityId, now).Rules;
            maker = request.CreatorPrincipalId; makerAlias = request.CreatorCoreLinkedWorkerId;
            assignee = request.ApproverPrincipalId; assigneeAlias = request.ApproverCoreLinkedWorkerId;
        }
        else
        {
            Need(approvedLink is not null && approvedCase is not null && approvedLink.Purpose == Approval,
                "ManualPaymentOriginalApprovalRequired");
            VerifyCurrentCase(request, approvedLink!, approvedCase!);
            Need(approvedCase!.State == "Approved" && decision.FromStatus == "Approved"
                && decision.ActorPrincipalId == request.RecordedByPrincipalId
                && request.RecordedByPrincipalId == request.RecorderPrincipalId
                && policy.TenantId == request.TenantId && policy.LegalEntityId == request.LegalEntityId
                && policy.Id == approvedLink!.PolicyVersionId && policy.PolicyVersionNumber == approvedLink.PolicyVersionNumber
                && policy.PolicyKey == ManualPaymentReviewPolicyRules.PolicyKey
                && policy.State is "Published" or "Superseded"
                && ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) == approvedLink.PolicyRulesSha256,
                "ManualPaymentOriginalApprovalRequired");
            rules = ManualPaymentReviewPolicyRules.Parse(policy.RulesJson);
            maker = request.RecordedByPrincipalId!; makerAlias = request.RecordedByCoreLinkedWorkerId;
            assignee = request.ConfirmerPrincipalId; assigneeAlias = request.ConfirmerCoreLinkedWorkerId;
        }
        // Review authority uses the captured whole bill, so splitting it into smaller
        // payment requests cannot evade the approved ceiling.
        Need(ManualPaymentReviewPolicyRules.WithinConfiguredMaximum(rules, request.CurrencyCode, invoice.InvoiceGrossAmount),
            "ManualPaymentPolicyAmountNotCovered");
        Need(ProcurementPrincipalEvidence.Independent(maker, makerAlias, assignee, assigneeAlias),
            "ManualPaymentCoreIndependentAssigneeRequired");
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
        var core = new ApprovalCase { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            SourceModule = "procurement", SourceType = SourceType(purpose), SourceId = request.Id,
            SourceVersion = request.Version, PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber,
            MakerSubjectId = maker, OpenedAt = at, CreatedAt = at, UpdatedAt = at };
        var stage = new ApprovalStage { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            CaseId = core.Id, Sequence = 1, Code = purpose, Mode = "All", RequiredApprovals = 1, State = "Active",
            CreatedAt = at, UpdatedAt = at };
        var assignment = new ApprovalAssignment { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            CaseId = core.Id, StageId = stage.Id, AssigneeSubjectId = assignee, PermissionKey = Permission(purpose),
            CreatedAt = at, UpdatedAt = at };
        stage.Assignments.Add(assignment); core.Stages.Add(stage); ApprovalEngine.ValidateNewCase(core);
        var link = new ManualPaymentWorkflowCaseLink { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            ManualPaymentRequestId = request.Id, BranchId = request.BranchId, Purpose = purpose,
            OpenedRequestVersion = request.Version, OpeningManualDecisionId = decision.Id,
            PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            CoreApprovalCaseId = core.Id, CoreApprovalStageId = stage.Id, CoreApprovalAssignmentId = assignment.Id,
            MakerPrincipalId = maker, MakerCoreLinkedWorkerId = makerAlias,
            AssigneePrincipalId = assignee, AssigneeCoreLinkedWorkerId = assigneeAlias,
            InvoiceVersionAtOpening = invoice.InvoiceVersion, InvoiceGrossAmountAtOpening = invoice.InvoiceGrossAmount,
            OpenedAt = at, CreatedAt = at, CreatedBy = decision.ActorPrincipalId,
        };
        // Immutable init-only records keep the snapshot and its exact-byte digest together.
        var snapshot = Snapshot(request, link);
        link = CopyWithSnapshot(link, snapshot);
        VerifyCurrentCase(request, link, core);
        return new(core, link);
    }

    /// <summary>For a current transaction's unchanged terms. Historical readers use the
    /// retained snapshot; they must not compare an older returned case to a later revised draft.</summary>
    public static void VerifyCurrentCase(ManualPaymentRequest request, ManualPaymentWorkflowCaseLink link, ApprovalCase core)
    {
        Need(Same(request, link) && request.Id == link.ManualPaymentRequestId && request.BranchId == link.BranchId
            && link.Purpose is Approval or Confirmation && link.OpenedRequestVersion <= request.Version
            && link.CoreApprovalCaseId == core.Id && core.TenantId == request.TenantId && core.LegalEntityId == request.LegalEntityId
            && core.SourceModule == "procurement" && core.SourceType == SourceType(link.Purpose)
            && core.SourceId == request.Id && core.SourceVersion == link.OpenedRequestVersion
            && core.PolicyVersionId == link.PolicyVersionId && core.PolicyVersionNumber == link.PolicyVersionNumber
            && core.MakerSubjectId == link.MakerPrincipalId && link.InvoiceVersionAtOpening > 0
            && link.InvoiceGrossAmountAtOpening > 0 && ExactDecimalJsonCodec.IsRepresentable(link.InvoiceGrossAmountAtOpening, 18, 4)
            && (link.Purpose == Approval
                ? link.MakerPrincipalId == request.CreatorPrincipalId && link.MakerCoreLinkedWorkerId == request.CreatorCoreLinkedWorkerId
                    && link.AssigneePrincipalId == request.ApproverPrincipalId && link.AssigneeCoreLinkedWorkerId == request.ApproverCoreLinkedWorkerId
                : link.MakerPrincipalId == request.RecordedByPrincipalId && link.MakerCoreLinkedWorkerId == request.RecordedByCoreLinkedWorkerId
                    && link.AssigneePrincipalId == request.ConfirmerPrincipalId && link.AssigneeCoreLinkedWorkerId == request.ConfirmerCoreLinkedWorkerId)
            && link.SnapshotJson == Snapshot(request, link)
            && ProcurementEvidenceEncoding.Digest(link.SnapshotJson) == link.SnapshotSha256
            && ProcurementPrincipalEvidence.Independent(link.MakerPrincipalId, link.MakerCoreLinkedWorkerId,
                link.AssigneePrincipalId, link.AssigneeCoreLinkedWorkerId), "ManualPaymentCoreCaseBindingInvalid");
        Need(core.Stages.Count == 1, "ManualPaymentCoreCaseBindingInvalid");
        var stage = core.Stages.Single();
        Need(stage.Id == link.CoreApprovalStageId && stage.CaseId == core.Id
            && stage.TenantId == core.TenantId && stage.LegalEntityId == core.LegalEntityId
            && stage.Sequence == 1 && stage.Code == link.Purpose && stage.Mode == "All" && stage.RequiredApprovals == 1
            && stage.Assignments.Count == 1, "ManualPaymentCoreCaseBindingInvalid");
        var assignment = stage.Assignments.Single();
        Need(assignment.Id == link.CoreApprovalAssignmentId && assignment.CaseId == core.Id && assignment.StageId == stage.Id
            && assignment.TenantId == core.TenantId && assignment.LegalEntityId == core.LegalEntityId
            && assignment.AssigneeSubjectId == link.AssigneePrincipalId && assignment.PermissionKey == Permission(link.Purpose)
            && assignment.DelegatedFromSubjectId is null, "ManualPaymentCoreCaseBindingInvalid");
    }

    public static ManualPaymentCoreDecisionLink LinkDecision(ManualPaymentChange change,
        ManualPaymentWorkflowCaseLink link, ApprovalCase core, ApprovalDecision decision)
    {
        VerifyCurrentCase(change.Request, link, core);
        var manual = change.Decision;
        var expected = manual.Action switch {
            "Approve" or "Confirm" => "Approve", "Reject" or "Dispute" => "Reject", "Return" => "Return", "Cancel" => "Cancel", _ => "" };
        var expectedState = expected switch { "Approve" => "Approved", "Reject" => "Rejected", "Return" => "Returned", "Cancel" => "Cancelled", _ => "" };
        Need(expected.Length > 0 && Same(change.Request, manual) && manual.ManualPaymentRequestId == change.Request.Id
            && manual.BranchId == change.Request.BranchId && manual.ToVersion == change.Request.Version
            && manual.FromVersion == link.OpenedRequestVersion && manual.ToVersion == manual.FromVersion + 1
            && (link.Purpose == Approval ? manual.Action is "Approve" or "Return" or "Reject" or "Cancel" : manual.Action is "Confirm" or "Dispute")
            && decision.TenantId == link.TenantId && decision.LegalEntityId == link.LegalEntityId
            && decision.CaseId == core.Id && decision.StageId == link.CoreApprovalStageId
            && decision.AssignmentId == link.CoreApprovalAssignmentId && decision.ActorSubjectId == manual.ActorPrincipalId
            && decision.Id != Guid.Empty && decision.Action == expected
            && (manual.Action is "Approve" or "Confirm" || decision.Reason == manual.Reason)
            && (manual.Action == "Cancel" ? manual.ActorPrincipalId == link.MakerPrincipalId || manual.ActorPrincipalId == link.AssigneePrincipalId
                : manual.ActorPrincipalId == link.AssigneePrincipalId)
            && core.State == expectedState && core.Stages.Single().State == expectedState
            && core.Stages.Single().Assignments.Single().State == expectedState
            && ProcurementEvidenceEncoding.DatabaseTimestamp(core.CompletedAt) == ProcurementEvidenceEncoding.DatabaseTimestamp(manual.OccurredAt)
            && ProcurementEvidenceEncoding.DatabaseTimestamp(decision.DecidedAt) == ProcurementEvidenceEncoding.DatabaseTimestamp(manual.OccurredAt),
            "ManualPaymentCoreDecisionBindingInvalid");
        return new() { TenantId = link.TenantId, LegalEntityId = link.LegalEntityId, ManualPaymentRequestId = change.Request.Id,
            BranchId = link.BranchId, WorkflowCaseLinkId = link.Id, ManualPaymentDecisionId = manual.Id,
            CoreApprovalDecisionId = decision.Id, ManualAction = manual.Action, ActorPrincipalId = manual.ActorPrincipalId,
            ActorCoreLinkedWorkerId = manual.ActorCoreLinkedWorkerId, FromRequestVersion = manual.FromVersion,
            ToRequestVersion = manual.ToVersion, DecidedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(manual.OccurredAt)!.Value,
            CreatedBy = manual.ActorPrincipalId, CreatedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(manual.OccurredAt)!.Value };
    }

    public static ManualPaymentBusinessDateObservation ObserveDate(ManualPaymentChange change,
        DateOnly companyDate, string timeZoneId, DateTimeOffset observedAtUtc)
    {
        var decision = change.Decision;
        Need(Same(change.Request, decision) && decision.ManualPaymentRequestId == change.Request.Id
            && decision.ToVersion == change.Request.Version && decision.BranchId == change.Request.BranchId
            && decision.Action is "Record" or "Confirm" && companyDate != default
            && timeZoneId is { Length: > 0 and <= 100 } && timeZoneId == timeZoneId.Trim()
            && observedAtUtc != default && observedAtUtc.Offset == TimeSpan.Zero
            && ProcurementEvidenceEncoding.DatabaseTimestamp(observedAtUtc) == ProcurementEvidenceEncoding.DatabaseTimestamp(decision.OccurredAt)
            && change.Request.PaidOn is { } paid && paid <= companyDate, "ManualPaymentCompanyDateObservationInvalid");
        return new() { TenantId = change.Request.TenantId, LegalEntityId = change.Request.LegalEntityId,
            ManualPaymentRequestId = change.Request.Id, BranchId = change.Request.BranchId, ManualPaymentDecisionId = decision.Id,
            RequestVersion = decision.ToVersion, Action = decision.Action, CompanyBusinessDate = companyDate,
            CompanyTimeZoneId = timeZoneId, ObservedAtUtc = ProcurementEvidenceEncoding.DatabaseTimestamp(observedAtUtc)!.Value,
            CreatedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(observedAtUtc)!.Value, CreatedBy = decision.ActorPrincipalId };
    }

    private static string SourceType(string purpose) => purpose == Approval ? "manual_payment_request" : "manual_payment_confirmation";
    private static string Permission(string purpose) => purpose == Approval ? "procurement-payment-approve" : "procurement-payment-confirm";
    private static bool Same(ProcurementRecord left, ProcurementRecord right) => left.TenantId == right.TenantId && left.LegalEntityId == right.LegalEntityId;
    private static string Snapshot(ManualPaymentRequest request, ManualPaymentWorkflowCaseLink link) => JsonSerializer.Serialize(new {
        schemaVersion = 1, request.Id, request.TenantId, request.LegalEntityId, request.BranchId, request.SupplierInvoiceId, request.SupplierId,
        request.CurrencyCode, amount = ProcurementEvidenceEncoding.Decimal(request.Amount), requestPurpose = request.Purpose, request.Title,
        request.CreatorPrincipalId, request.CreatorCoreLinkedWorkerId, request.ApproverPrincipalId, request.ApproverCoreLinkedWorkerId,
        request.RecorderPrincipalId, request.RecorderCoreLinkedWorkerId, request.ConfirmerPrincipalId, request.ConfirmerCoreLinkedWorkerId,
        casePurpose = link.Purpose, link.OpenedRequestVersion, link.InvoiceVersionAtOpening,
        invoiceGross = ProcurementEvidenceEncoding.Decimal(link.InvoiceGrossAmountAtOpening),
        link.PolicyVersionId, link.PolicyVersionNumber, link.PolicyRulesSha256,
        link.MakerPrincipalId, link.MakerCoreLinkedWorkerId, link.AssigneePrincipalId, link.AssigneeCoreLinkedWorkerId,
    });
    private static ManualPaymentWorkflowCaseLink CopyWithSnapshot(ManualPaymentWorkflowCaseLink link, string json) => new() {
        Id = link.Id, TenantId = link.TenantId, LegalEntityId = link.LegalEntityId, ManualPaymentRequestId = link.ManualPaymentRequestId,
        BranchId = link.BranchId, Purpose = link.Purpose, OpenedRequestVersion = link.OpenedRequestVersion,
        OpeningManualDecisionId = link.OpeningManualDecisionId, PolicyVersionId = link.PolicyVersionId, PolicyVersionNumber = link.PolicyVersionNumber,
        PolicyRulesSha256 = link.PolicyRulesSha256, CoreApprovalCaseId = link.CoreApprovalCaseId, CoreApprovalStageId = link.CoreApprovalStageId,
        CoreApprovalAssignmentId = link.CoreApprovalAssignmentId, MakerPrincipalId = link.MakerPrincipalId, MakerCoreLinkedWorkerId = link.MakerCoreLinkedWorkerId,
        AssigneePrincipalId = link.AssigneePrincipalId, AssigneeCoreLinkedWorkerId = link.AssigneeCoreLinkedWorkerId,
        InvoiceVersionAtOpening = link.InvoiceVersionAtOpening, InvoiceGrossAmountAtOpening = link.InvoiceGrossAmountAtOpening,
        SnapshotJson = json, SnapshotSha256 = ProcurementEvidenceEncoding.Digest(json), OpenedAt = link.OpenedAt,
        CreatedAt = link.CreatedAt, CreatedBy = link.CreatedBy,
    };
    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool okay, string code)
    { if (!okay) throw new InvalidOperationException(code); }
}
