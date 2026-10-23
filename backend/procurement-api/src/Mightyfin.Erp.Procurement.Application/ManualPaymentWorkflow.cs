using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Trusted Core account projection. EffectiveBranchId is the authorized transaction branch
/// resolved from the explicit form branch; it is separate from the header's read context.
/// The API adapter must resolve this afresh for every command.
/// </summary>
public sealed record ManualPaymentActor(string PrincipalId, string TenantId, Guid LegalEntityId,
    Guid? EffectiveBranchId, IReadOnlySet<Guid> AllowedBranchIds, IReadOnlySet<string> Actions, bool IsActive);

/// <summary>Trusted current Core assignment candidate, never a browser-supplied authority assertion.</summary>
public sealed record ManualPaymentAssignee(string PrincipalId, string TenantId, Guid LegalEntityId,
    IReadOnlySet<Guid> AllowedBranchIds, IReadOnlySet<string> Actions, bool IsActive);

/// <summary>
/// Locked owning-store invoice/capacity read. RemainingExcludingThisRequest includes all other
/// reserved manual requests and imported Finance settlements. The caller must lock all relevant
/// rows in a consistent order; this value alone is not authorization or concurrency protection.
/// </summary>
public sealed record ManualPaymentInvoiceSource(Guid InvoiceId, string TenantId, Guid LegalEntityId,
    Guid BranchId, Guid SupplierId, string CurrencyCode, DateOnly InvoiceDate,
    decimal RemainingExcludingThisRequest, bool IsEligible);

public sealed record ManualPaymentDraft(Guid BranchId, Guid InvoiceId, Guid SupplierId,
    string CurrencyCode, decimal Amount, string ApproverPrincipalId, string RecorderPrincipalId,
    string ConfirmerPrincipalId, string? Purpose);

public sealed record ManualPaymentRecordedEvidence(DateOnly PaidOn, string ExternalMethod,
    string ExternalReference, decimal Amount, Guid? EvidenceDocumentRefId);

public sealed record ManualPaymentChange(ManualPaymentRequest Request, ManualPaymentDecision Decision);

/// <summary>
/// Pure, unregistered manual-payment business transitions. The future route owns authentication,
/// a serializable/locking transaction, CAS update, Core audit, command receipt and outbox.
/// None of these methods moves money, posts AP, or changes FinancePaymentStatusRef.
/// </summary>
public static class ManualPaymentWorkflow
{
    private const string Prefix = "procurement-payment-";

    public static ManualPaymentChange Create(ManualPaymentDraft draft, ManualPaymentActor actor,
        ManualPaymentAssignee approver, ManualPaymentAssignee recorder, ManualPaymentAssignee confirmer,
        ManualPaymentInvoiceSource source, DateTimeOffset now)
    {
        if (now == default) Fail("ManualPaymentTimeInvalid");
        Authorize(actor, draft.BranchId, "create");
        ValidateDraft(draft, actor, approver, recorder, confirmer, source);
        CheckCapacity(draft.Amount, source);
        var request = new ManualPaymentRequest
        {
            TenantId = actor.TenantId, LegalEntityId = actor.LegalEntityId, BranchId = draft.BranchId,
            SupplierInvoiceId = draft.InvoiceId, SupplierId = draft.SupplierId,
            CurrencyCode = draft.CurrencyCode, Amount = draft.Amount, Status = "Draft",
            CreatorPrincipalId = actor.PrincipalId, ApproverPrincipalId = draft.ApproverPrincipalId,
            RecorderPrincipalId = draft.RecorderPrincipalId, ConfirmerPrincipalId = draft.ConfirmerPrincipalId,
            Purpose = CleanOptional(draft.Purpose, 1000), CreatedAt = now, CreatedBy = actor.PrincipalId,
            Version = 1
        };
        return new(request, Decision(request, "Create", "None", 0, actor, now, newAmount: draft.Amount,
            newApprover: draft.ApproverPrincipalId, newRecorder: draft.RecorderPrincipalId,
            newConfirmer: draft.ConfirmerPrincipalId));
    }

    public static ManualPaymentChange Revise(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentDraft draft, ManualPaymentActor actor, ManualPaymentAssignee approver,
        ManualPaymentAssignee recorder, ManualPaymentAssignee confirmer,
        ManualPaymentInvoiceSource source, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "edit", "Draft");
        if (current.CreatorPrincipalId != actor.PrincipalId) Fail("ManualPaymentNotCreator");
        if (draft.BranchId != current.BranchId || draft.InvoiceId != current.SupplierInvoiceId
            || draft.SupplierId != current.SupplierId || draft.CurrencyCode != current.CurrencyCode)
            Fail("ManualPaymentSourceImmutable");
        ValidateDraft(draft, actor, approver, recorder, confirmer, source);
        CheckCapacity(draft.Amount, source);
        var next = Copy(current, actor, now);
        next.Amount = draft.Amount;
        next.ApproverPrincipalId = draft.ApproverPrincipalId;
        next.RecorderPrincipalId = draft.RecorderPrincipalId;
        next.ConfirmerPrincipalId = draft.ConfirmerPrincipalId;
        next.Purpose = CleanOptional(draft.Purpose, 1000);
        return new(next, Decision(next, "Revise", current.Status, current.Version, actor, now,
            previousAmount: current.Amount, newAmount: next.Amount,
            previousApprover: current.ApproverPrincipalId, newApprover: next.ApproverPrincipalId,
            previousRecorder: current.RecorderPrincipalId, newRecorder: next.RecorderPrincipalId,
            previousConfirmer: current.ConfirmerPrincipalId, newConfirmer: next.ConfirmerPrincipalId));
    }

    public static ManualPaymentChange Submit(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, ManualPaymentInvoiceSource source, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "submit", "Draft");
        if (current.CreatorPrincipalId != actor.PrincipalId) Fail("ManualPaymentNotCreator");
        CheckSource(current, source); CheckCapacity(current.Amount, source);
        return Move(current, actor, now, "Submit", "PendingApproval");
    }

    public static ManualPaymentChange Approve(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, ManualPaymentInvoiceSource source, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "approve", "PendingApproval");
        RequireAssigned(actor, current.ApproverPrincipalId);
        if (actor.PrincipalId == current.CreatorPrincipalId) Fail("ManualPaymentSelfApproval");
        CheckSource(current, source); CheckCapacity(current.Amount, source);
        var next = Move(current, actor, now, "Approve", "Approved");
        next.Request.ApprovedAmount = current.Amount;
        next.Request.ApprovedByPrincipalId = actor.PrincipalId;
        next.Request.ApprovedAt = now;
        return next;
    }

    public static ManualPaymentChange Return(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, string reason, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "approve", "PendingApproval");
        RequireAssigned(actor, current.ApproverPrincipalId);
        return Move(current, actor, now, "Return", "Draft", Required(reason));
    }

    public static ManualPaymentChange Reject(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, string reason, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "approve", "PendingApproval");
        RequireAssigned(actor, current.ApproverPrincipalId);
        return Move(current, actor, now, "Reject", "Rejected", Required(reason));
    }

    public static ManualPaymentChange Record(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, ManualPaymentInvoiceSource source,
        ManualPaymentRecordedEvidence evidence, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "record", "Approved");
        RequireAssigned(actor, current.RecorderPrincipalId);
        CheckSource(current, source); CheckCapacity(current.Amount, source);
        if (current.ApprovedAmount != current.Amount || current.ApprovedByPrincipalId is null
            || current.ApprovedAt is null || evidence.Amount != current.Amount
            || evidence.PaidOn == default
            || evidence.PaidOn > DateOnly.FromDateTime(now.UtcDateTime)
            || !ValidText(evidence.ExternalMethod, 100) || !ValidText(evidence.ExternalReference, 200)
            || evidence.EvidenceDocumentRefId == Guid.Empty)
            Fail("ManualPaymentRecordingInvalid");
        var next = Move(current, actor, now, "Record", "PaymentRecorded");
        next.Request.RecordedAmount = evidence.Amount;
        next.Request.PaidOn = evidence.PaidOn;
        next.Request.ExternalMethod = evidence.ExternalMethod.Trim();
        next.Request.ExternalReference = evidence.ExternalReference.Trim();
        next.Request.RecordedByPrincipalId = actor.PrincipalId;
        next.Request.RecordedAt = now;
        next.Request.EvidenceDocumentRefId = evidence.EvidenceDocumentRefId;
        next.Decision.PaidOn = evidence.PaidOn;
        next.Decision.RecordedAmount = evidence.Amount;
        next.Decision.ExternalMethod = evidence.ExternalMethod.Trim();
        next.Decision.ExternalReference = evidence.ExternalReference.Trim();
        next.Decision.EvidenceDocumentRefId = evidence.EvidenceDocumentRefId;
        return next;
    }

    public static ManualPaymentChange Confirm(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "confirm", "PaymentRecorded");
        RequireAssigned(actor, current.ConfirmerPrincipalId);
        if (!ValidAmount(current.Amount) || !ValidAmount(current.ApprovedAmount ?? 0m)
            || !ValidAmount(current.RecordedAmount ?? 0m)
            || current.ApprovedAmount != current.Amount || current.RecordedAmount != current.Amount
            || !ProcurementPrincipalEvidence.Canonical(current.ApprovedByPrincipalId)
            || current.ApprovedByPrincipalId != current.ApproverPrincipalId
            || current.ApprovedByPrincipalId == current.CreatorPrincipalId
            || current.ApprovedAt is null || current.ApprovedAt < current.CreatedAt
            || !ProcurementPrincipalEvidence.Canonical(current.RecordedByPrincipalId)
            || actor.PrincipalId == current.RecordedByPrincipalId
            || current.RecordedByPrincipalId != current.RecorderPrincipalId
            || current.RecordedAt is null || current.RecordedAt < current.ApprovedAt
            || current.PaidOn is null || current.PaidOn.Value == default(DateOnly)
            || current.PaidOn > DateOnly.FromDateTime(now.UtcDateTime)
            || !ValidText(current.ExternalMethod, 100) || !ValidText(current.ExternalReference, 200))
            Fail("ManualPaymentIndependentConfirmationRequired");
        var next = Move(current, actor, now, "Confirm", "ConfirmedManually");
        next.Request.ConfirmedByPrincipalId = actor.PrincipalId;
        next.Request.ConfirmedAt = now;
        return next;
    }

    public static ManualPaymentChange Cancel(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, string reason, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "cancel", "Draft", "PendingApproval", "Approved");
        if (actor.PrincipalId != current.CreatorPrincipalId && actor.PrincipalId != current.ApproverPrincipalId)
            Fail("ManualPaymentCancelParticipantRequired");
        return Move(current, actor, now, "Cancel", "Cancelled", Required(reason));
    }

    public static ManualPaymentChange Dispute(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, string reason, DateTimeOffset now)
    {
        Check(current, expectedVersion, actor, now, "confirm", "PaymentRecorded");
        RequireAssigned(actor, current.ConfirmerPrincipalId);
        return Move(current, actor, now, "Dispute", "Disputed", Required(reason));
    }

    private static void ValidateDraft(ManualPaymentDraft draft, ManualPaymentActor actor,
        ManualPaymentAssignee approver, ManualPaymentAssignee recorder,
        ManualPaymentAssignee confirmer, ManualPaymentInvoiceSource source)
    {
        if (draft.BranchId == Guid.Empty || draft.InvoiceId == Guid.Empty || draft.SupplierId == Guid.Empty
            || !ValidCurrency(draft.CurrencyCode) || !ValidAmount(draft.Amount)
            || draft.Purpose?.Length > 1000
            || draft.ApproverPrincipalId == actor.PrincipalId
            || draft.RecorderPrincipalId == draft.ConfirmerPrincipalId)
            Fail("ManualPaymentDraftInvalid");
        CheckAssignee(approver, draft.ApproverPrincipalId, actor, draft.BranchId, "approve");
        CheckAssignee(recorder, draft.RecorderPrincipalId, actor, draft.BranchId, "record");
        CheckAssignee(confirmer, draft.ConfirmerPrincipalId, actor, draft.BranchId, "confirm");
        if (source.InvoiceId != draft.InvoiceId || source.SupplierId != draft.SupplierId
            || source.BranchId != draft.BranchId || source.CurrencyCode != draft.CurrencyCode
            || source.TenantId != actor.TenantId || source.LegalEntityId != actor.LegalEntityId
            || !source.IsEligible || source.InvoiceDate == default)
            Fail("ManualPaymentInvoiceNotEligible");
    }

    private static void CheckSource(ManualPaymentRequest request, ManualPaymentInvoiceSource source)
    {
        if (source.InvoiceId != request.SupplierInvoiceId || source.SupplierId != request.SupplierId
            || source.BranchId != request.BranchId || source.CurrencyCode != request.CurrencyCode
            || source.TenantId != request.TenantId || source.LegalEntityId != request.LegalEntityId
            || !source.IsEligible || source.InvoiceDate == default)
            Fail("ManualPaymentInvoiceNotEligible");
    }

    private static void CheckCapacity(decimal amount, ManualPaymentInvoiceSource source)
    {
        if (!ValidAmount(amount) || !ValidAmount(source.RemainingExcludingThisRequest)
            || amount > source.RemainingExcludingThisRequest)
            Fail("ManualPaymentInsufficientOutstanding");
    }

    private static void Check(ManualPaymentRequest current, long expectedVersion,
        ManualPaymentActor actor, DateTimeOffset now, string action, params string[] states)
    {
        if (current.Id == Guid.Empty || current.BranchId == Guid.Empty || current.SupplierInvoiceId == Guid.Empty
            || current.SupplierId == Guid.Empty || current.Version <= 0 || current.Version == long.MaxValue
            || current.TenantId != actor.TenantId || current.LegalEntityId != actor.LegalEntityId)
            Fail("ManualPaymentScopeInvalid");
        Authorize(actor, current.BranchId, action);
        if (now == default || current.CreatedAt == default || now < current.CreatedAt
            || current.UpdatedAt is { } updated && now < updated
            || current.ApprovedAt is { } approved && now < approved
            || current.RecordedAt is { } recorded && now < recorded
            || current.ConfirmedAt is { } confirmed && now < confirmed)
            Fail("ManualPaymentTimeInvalid");
        if (current.Version != expectedVersion) Fail("ManualPaymentVersionConflict");
        if (!states.Contains(current.Status, StringComparer.Ordinal)) Fail("ManualPaymentStateInvalid");
    }

    private static void Authorize(ManualPaymentActor actor, Guid branch, string action)
    {
        if (!actor.IsActive || !ProcurementPrincipalEvidence.Canonical(actor.PrincipalId)
            || string.IsNullOrWhiteSpace(actor.TenantId) || actor.LegalEntityId == Guid.Empty
            || actor.EffectiveBranchId != branch || !actor.AllowedBranchIds.Contains(branch)
            || !actor.Actions.Contains(Prefix + action)) Fail("ManualPaymentActionForbidden");
    }

    private static void CheckAssignee(ManualPaymentAssignee candidate, string expected,
        ManualPaymentActor actor, Guid branch, string action)
    {
        if (!candidate.IsActive || !ProcurementPrincipalEvidence.Canonical(expected)
            || candidate.PrincipalId != expected || candidate.TenantId != actor.TenantId
            || candidate.LegalEntityId != actor.LegalEntityId || !candidate.AllowedBranchIds.Contains(branch)
            || !candidate.Actions.Contains(Prefix + action)) Fail("ManualPaymentAssigneeUnavailable");
    }

    private static void RequireAssigned(ManualPaymentActor actor, string assigned)
    {
        if (!ProcurementPrincipalEvidence.Canonical(assigned) || actor.PrincipalId != assigned)
            Fail("ManualPaymentNotAssigned");
    }

    private static ManualPaymentChange Move(ManualPaymentRequest current, ManualPaymentActor actor,
        DateTimeOffset now, string action, string status, string? reason = null)
    {
        var next = Copy(current, actor, now);
        next.Status = status;
        next.ClosureReason = reason;
        return new(next, Decision(next, action, current.Status, current.Version, actor, now, reason));
    }

    private static ManualPaymentRequest Copy(ManualPaymentRequest current, ManualPaymentActor actor, DateTimeOffset now) => new()
    {
        Id = current.Id, TenantId = current.TenantId, LegalEntityId = current.LegalEntityId,
        BranchId = current.BranchId, SupplierInvoiceId = current.SupplierInvoiceId, SupplierId = current.SupplierId,
        CurrencyCode = current.CurrencyCode, Amount = current.Amount, Status = current.Status,
        CreatorPrincipalId = current.CreatorPrincipalId, ApproverPrincipalId = current.ApproverPrincipalId,
        RecorderPrincipalId = current.RecorderPrincipalId, ConfirmerPrincipalId = current.ConfirmerPrincipalId,
        Purpose = current.Purpose, ApprovedAmount = current.ApprovedAmount,
        ApprovedByPrincipalId = current.ApprovedByPrincipalId, ApprovedAt = current.ApprovedAt,
        PaidOn = current.PaidOn, ExternalMethod = current.ExternalMethod,
        ExternalReference = current.ExternalReference, RecordedAmount = current.RecordedAmount,
        RecordedByPrincipalId = current.RecordedByPrincipalId, RecordedAt = current.RecordedAt,
        ConfirmedByPrincipalId = current.ConfirmedByPrincipalId, ConfirmedAt = current.ConfirmedAt,
        EvidenceDocumentRefId = current.EvidenceDocumentRefId, ClosureReason = current.ClosureReason,
        CreatedAt = current.CreatedAt, CreatedBy = current.CreatedBy,
        UpdatedAt = now, UpdatedBy = actor.PrincipalId, Version = current.Version + 1
    };

    private static ManualPaymentDecision Decision(ManualPaymentRequest next, string action, string from,
        long previousVersion, ManualPaymentActor actor, DateTimeOffset now, string? reason = null,
        decimal? previousAmount = null, decimal? newAmount = null,
        string? previousApprover = null, string? newApprover = null,
        string? previousRecorder = null, string? newRecorder = null,
        string? previousConfirmer = null, string? newConfirmer = null) => new()
    {
        TenantId = next.TenantId, LegalEntityId = next.LegalEntityId,
        ManualPaymentRequestId = next.Id, BranchId = next.BranchId,
        Action = action, FromStatus = from, ToStatus = next.Status,
        FromVersion = previousVersion, ToVersion = next.Version, ActorPrincipalId = actor.PrincipalId,
        OccurredAt = now, Reason = reason, PreviousAmount = previousAmount, NewAmount = newAmount,
        PreviousApproverPrincipalId = previousApprover, NewApproverPrincipalId = newApprover,
        PreviousRecorderPrincipalId = previousRecorder, NewRecorderPrincipalId = newRecorder,
        PreviousConfirmerPrincipalId = previousConfirmer, NewConfirmerPrincipalId = newConfirmer,
        CreatedAt = now, CreatedBy = actor.PrincipalId
    };

    private static bool ValidAmount(decimal value) => value > 0
        && ExactDecimalJsonCodec.IsRepresentable(value, 18, 4);
    private static bool ValidCurrency(string? value) => value is { Length: 3 }
        && value.All(c => c is >= 'A' and <= 'Z');
    private static bool ValidText(string? value, int max) => !string.IsNullOrWhiteSpace(value)
        && value.Trim().Length <= max;
    private static string Required(string? value) => ValidText(value, 1000)
        ? value!.Trim() : throw new InvalidOperationException("ManualPaymentReasonRequired");
    private static string? CleanOptional(string? value, int max) => value is null ? null
        : value.Trim().Length <= max ? value.Trim() : throw new InvalidOperationException("ManualPaymentTextTooLong");
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
