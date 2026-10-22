using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceUnitReviewRequest(int SchemaVersion, string Operation, Guid CommandKey, Guid InvoiceId,
    Guid? ReviewId, Guid? AssignmentId, long InvoiceVersion, int? MappingRevision, long? CaseVersion,
    long? AssignmentVersion, string? Action, string? Reason, string? Attestation);
public sealed record InvoiceUnitReviewResponse(int SchemaVersion, Guid InvoiceId, Guid ReviewId, Guid CoreCaseId,
    Guid CommandKey, string Operation, long InvoiceVersion, long CaseVersion, string CaseState, string ReviewState,
    Guid? DecisionId, Guid? OutcomeId, DateTimeOffset CompletedAt, bool RecordedHistoricalResult,
    bool IsInvoiceApproval, bool CanPostToFinance)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MakerPrincipalId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? IdentitySchemaVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? EvidenceSchemaVersion { get; init; }
}

public static class InvoiceUnitReviewCommands
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 16,
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict };
    public const string OriginalUnitAttestation = "OriginalDocumentUnitsConfirmed";
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Read<T>(string text)
    {
        Need(text is { Length: > 0 and <= 262144 } && Encoding.UTF8.GetByteCount(text) <= 262144);
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(text);
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidOperationException("InvoiceUnitReviewCommandInvalid");
    }
    public static InvoiceUnitReviewResponse BindSource(InvoiceUnitReviewResponse response, InvoiceUnitReviewSource source,
        InvoiceUnitReviewOutcome? outcome = null)
    {
        var owned = InvoiceUnitReviewStorage.Read(source);
        Need(ProcurementPrincipalEvidence.Canonical(source.MakerPrincipalId)
            && response.InvoiceId == source.SupplierInvoiceId && response.ReviewId == source.Id && response.CoreCaseId == source.CoreCaseId
            && (outcome is null ? response.ReviewState == "Pending" : outcome.ReviewSourceId == source.Id
                && outcome.CoreCaseId == source.CoreCaseId && outcome.EvidenceSchemaVersion == 3 && response.OutcomeId == outcome.Id
                && response.ReviewState == outcome.State));
        return response with { MakerPrincipalId = source.MakerPrincipalId, IdentitySchemaVersion = owned.IdentitySchemaVersion,
            EvidenceSchemaVersion = outcome?.EvidenceSchemaVersion };
    }
    // Historical acknowledgement is bound to immutable source/receipt facts, not the
    // invoice's latest version or a freshly guessed employee. Current actor authorization
    // is still required independently by every recovery/replay route.
    public static InvoiceUnitReviewResponse ReadHistorical(InvoiceUnitReviewCommandReceipt row, InvoiceUnitReviewSource source)
    {
        var request = Read<InvoiceUnitReviewRequest>(row.RequestJson); var response = Read<InvoiceUnitReviewResponse>(row.ResponseJson);
        var owned = InvoiceUnitReviewStorage.Read(source);
        Need(ProcurementPrincipalEvidence.Canonical(row.ActorPrincipalId) && ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId)
            && request.SchemaVersion == 1 && response.SchemaVersion == 1 && row.ReviewSourceId == source.Id
            && row.Fingerprint == ProcurementEvidenceEncoding.PolicyJsonDigest(row.RequestJson)
            && request.Operation == row.Operation && response.Operation == row.Operation
            && request.CommandKey == row.CommandKey && response.CommandKey == row.CommandKey
            && request.InvoiceId == source.SupplierInvoiceId && response.InvoiceId == source.SupplierInvoiceId
            && response.ReviewId == source.Id && response.CoreCaseId == source.CoreCaseId
            && response.MakerPrincipalId == source.MakerPrincipalId && response.IdentitySchemaVersion == owned.IdentitySchemaVersion
            && response.InvoiceVersion == request.InvoiceVersion && response.InvoiceVersion > 0 && response.CaseVersion > 0
            && response.RecordedHistoricalResult && !response.IsInvoiceApproval && !response.CanPostToFinance
            && row.CompletedAt == response.CompletedAt && row.CompletedAt >= source.OpenedAt
            && (response.ReviewState == "Pending" ? response.CaseState == "Pending" && response.EvidenceSchemaVersion is null && response.OutcomeId is null
                : (response.ReviewState is "Verified" or "Returned" or "Rejected") && response.EvidenceSchemaVersion == 3 && response.OutcomeId is not null
                    && response.CaseState == (response.ReviewState == "Verified" ? "Approved" : response.ReviewState)));
        if (row.Operation == "Open") Need(row.ActorPrincipalId == source.MakerPrincipalId && row.ActorWorkerId == source.MakerWorkerId && request.ReviewId is null
            && request.AssignmentId is null && request.MappingRevision == source.MappingRevision && request.InvoiceVersion == source.InvoiceVersionAtOpen
            && response.CaseState == "Pending" && response.CaseVersion == 1 && response.ReviewState == "Pending" && response.DecisionId is null);
        else Need(row.Operation == "Decision" && request.ReviewId == source.Id && request.AssignmentId is not null
            && request.Action is "Approve" or "Return" or "Reject" && response.DecisionId is not null);
        return response;
    }
    public static void Verify(InvoiceUnitReviewCommandReceipt row, InvoiceUnitReviewSource source,
        SupplierInvoice invoice, ApprovalCase workflow, IReadOnlyList<InvoiceUnitReviewAssignmentEvidence> pins,
        ApprovalDecision? decision, InvoiceUnitReviewOutcome? outcome)
    {
        var request = Read<InvoiceUnitReviewRequest>(row.RequestJson); var response = ReadHistorical(row, source);
        Need(ProcurementPrincipalEvidence.Canonical(row.ActorPrincipalId) && ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId)
            && row.Fingerprint == ProcurementEvidenceEncoding.PolicyJsonDigest(row.RequestJson) && request.SchemaVersion == 1 && response.SchemaVersion == 1
            && request.Operation == row.Operation && response.Operation == row.Operation && request.CommandKey == row.CommandKey && response.CommandKey == row.CommandKey
            && request.InvoiceId == source.SupplierInvoiceId && response.InvoiceId == source.SupplierInvoiceId && invoice.Id == source.SupplierInvoiceId
            && response.ReviewId == source.Id && row.ReviewSourceId == source.Id && response.CoreCaseId == source.CoreCaseId && workflow.Id == source.CoreCaseId
            && response.InvoiceVersion == invoice.Version && response.CaseVersion == workflow.Version && response.CaseState == workflow.State
            && request.InvoiceVersion == invoice.Version && invoice.CurrentMappingVersionId == source.MappingVersionId && invoice.MappingRevision == source.MappingRevision
            && row.CompletedAt == response.CompletedAt && row.CompletedAt >= source.OpenedAt
            && response.RecordedHistoricalResult && !response.IsInvoiceApproval && !response.CanPostToFinance);
        if (row.Operation == "Open")
        {
            Need(request.ReviewId is null && request.AssignmentId is null && request.MappingRevision == source.MappingRevision
                && request.CaseVersion is null && request.AssignmentVersion is null && request.Action is null && request.Attestation is null
                && request.InvoiceVersion == source.InvoiceVersionAtOpen && row.ActorPrincipalId == source.MakerPrincipalId
                && decision is null && outcome is null && response.DecisionId is null && response.OutcomeId is null
                && workflow.State == "Pending" && workflow.Version == 1 && response.ReviewState == "Pending");
            return;
        }
        Need(row.Operation == "Decision" && request.ReviewId == source.Id && request.AssignmentId is { } assignmentId
            && request.MappingRevision is null && request.CaseVersion is > 0 && request.AssignmentVersion is > 0
            && request.CaseVersion == workflow.Version - 1 && decision is not null && decision.Id == response.DecisionId
            && decision.CaseId == workflow.Id && decision.AssignmentId == request.AssignmentId && decision.ActorSubjectId == row.ActorPrincipalId
            && decision.IdempotencyKey == row.CommandKey.ToString("D") && decision.Action == request.Action && decision.Reason == request.Reason
            && decision.DecidedAt == row.CompletedAt && request.Action is "Approve" or "Return" or "Reject"
            && (request.Action != "Approve" || request.Attestation == OriginalUnitAttestation)
            && (request.Action == "Approve" || !string.IsNullOrWhiteSpace(request.Reason)));
        var assignment = workflow.Stages.SelectMany(x => x.Assignments).Single(x => x.Id == request.AssignmentId);
        Need(request.AssignmentVersion == assignment.Version - 1 && pins.Any(x => x.CoreAssignmentId == assignment.Id
            && x.PrincipalId == row.ActorPrincipalId));
        if (workflow.State == "Pending")
            Need(outcome is null && response.OutcomeId is null && response.ReviewState == "Pending");
        else Need(outcome is not null && outcome.ReviewSourceId == source.Id && outcome.CoreCaseId == workflow.Id
            && outcome.FinalDecisionId == decision!.Id && outcome.Id == response.OutcomeId && outcome.CompletedAt == row.CompletedAt
            && response.ReviewState == outcome.State && outcome.State == (workflow.State == "Approved" ? "Verified" : workflow.State));
    }
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("InvoiceUnitReviewCommandInvalid"); }
}
