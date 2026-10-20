using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed projections of owned persisted rows, not request DTOs or new EF models.
/// Caller must load the complete scoped sets and enforce current access before restoration.</summary>
public sealed record RfqOwnedDraftEvidence(int SchemaVersion, Guid EventId, string TenantId, Guid CompanyId,
    Guid? BranchId, string MakerSubjectId, Guid MakerWorkerId, Guid CreateCommandKey,
    string OriginalFingerprint, string PayloadJson, string PayloadSha256);
public sealed record RfqOwnedDraftLine(RfqDemandPin Pin, SourcingLine Line, PurchaseRequestAllocation Allocation);
public sealed record RfqHistoricalApproval(PurchaseRequestApprovalSubmission Submission,
    PurchaseRequestApprovalOutcome Outcome, ApprovalCase Case, ApprovalDecision Decision,
    PolicyEvaluation Evaluation, PolicyVersion Policy, BudgetCheckSnapshot Finance);

/// <summary>Only original Draft restoration for historical cancellation. No current method,
/// catalog, actor or financial eligibility is inferred. Terminal reads/receipt hydration are separate.</summary>
public static class GovernedRfqRestoration
{
    private sealed record Envelope(int SchemaVersion, Guid EventId, string TenantId, Guid CompanyId,
        Guid? BranchId, string CurrencyCode, string Title, string MakerSubjectId, Guid MakerWorkerId,
        Guid CreateCommandKey, string OriginalFingerprint, RfqDemandPin[] Lines);
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new EvidenceDecimalConverter() },
    };
    private const string EnvelopeFields = "SchemaVersion EventId TenantId CompanyId BranchId CurrencyCode Title MakerSubjectId MakerWorkerId CreateCommandKey OriginalFingerprint Lines";
    private const string PinFields = "RequestId LineId RequestVersion SubmissionId SubmittedVersion CaseId OutcomeId DecisionId RequestSha PolicyDecisionSha PolicyRulesSha FinanceId FinanceSha MethodId MethodRevision MethodCode MinimumQuoteCount MakerWorkerId RequesterWorkerId AllocationId EventLineId Quantity UnitEstimate EstimatedValue UnitCode CategoryCode DepartmentId";
    private const string RequestFields = "schemaVersion TenantId LegalEntityId Id Number CreatedBy RequesterWorkerId SubmittedVersion SubmittedAt Purpose CurrencyCode NeededBy BranchRef SiteRef DepartmentRef CostCenterRef ProjectRef DeliveryLocationRef DeliveryAddressText OnBehalfOfSubjectId PlanRef FundingSourceRef NotesToApprover SourceChannel lines";
    private const string RequestLineFields = "Id PurchaseRequestId LineNumber Description UnitCode quantity unitPrice EstimatedCurrencyCode CatalogItemId CatalogEntryId CatalogPublishedVersion PreferredSupplierId CategoryCode PurchaseType NeededBy discountPercent TaxCodeRef CostCenterRef ProjectRef SpecificationDocumentRefId";

    public static RfqOwnedDraftEvidence Capture(GovernedRfqDraft.Draft draft, Guid createCommandKey)
    {
        if (draft.State != "Draft" || createCommandKey == Guid.Empty) Fail();
        var envelope = new Envelope(1, draft.Id, draft.TenantId, draft.CompanyId, draft.BranchId,
            draft.CurrencyCode, draft.Title, draft.MakerSubjectId, draft.MakerWorkerId, createCommandKey,
            draft.Fingerprint, draft.Lines.ToArray());
        var payload = JsonSerializer.Serialize(envelope, Json);
        return new(1, draft.Id, draft.TenantId, draft.CompanyId, draft.BranchId, draft.MakerSubjectId,
            draft.MakerWorkerId, createCommandKey, draft.Fingerprint, payload, ProcurementEvidenceEncoding.Digest(payload));
    }

    public static GovernedRfqDraft.Draft RestoreVerified(RfqOwnedDraftEvidence evidence, SourcingEvent current,
        IReadOnlyList<RfqOwnedDraftLine> bindings, IReadOnlyList<RfqHistoricalApproval> approvals)
    {
        try { return Restore(evidence, current, bindings, approvals); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException
            or ArgumentException or OverflowException or InvalidOperationException)
        { throw new InvalidOperationException("RfqRetainedDraftInvalid", error); }
    }

    private static GovernedRfqDraft.Draft Restore(RfqOwnedDraftEvidence evidence, SourcingEvent current,
        IReadOnlyList<RfqOwnedDraftLine> bindings, IReadOnlyList<RfqHistoricalApproval> approvals)
    {
        if (evidence is null || current is null || bindings is null || approvals is null
            || bindings.Any(x => x is null || x.Pin is null || x.Line is null || x.Allocation is null)
            || approvals.Any(x => x is null || x.Submission is null || x.Outcome is null || x.Case is null
                || x.Decision is null || x.Evaluation is null || x.Policy is null || x.Finance is null)
            || evidence.SchemaVersion != 1 || evidence.EventId == Guid.Empty || evidence.CompanyId == Guid.Empty
            || string.IsNullOrWhiteSpace(evidence.TenantId) || evidence.MakerWorkerId == Guid.Empty
            || string.IsNullOrWhiteSpace(evidence.MakerSubjectId) || evidence.CreateCommandKey == Guid.Empty
            || evidence.PayloadJson is null || evidence.PayloadJson.Length > 1000000
            || evidence.PayloadSha256 != ProcurementEvidenceEncoding.Digest(evidence.PayloadJson)) Fail();
        using var document = StrictDocument(evidence.PayloadJson);
        Fields(document.RootElement, EnvelopeFields);
        var linesJson = document.RootElement.GetProperty("Lines");
        if (linesJson.ValueKind != JsonValueKind.Array || linesJson.GetArrayLength() is < 1 or > 200) Fail();
        foreach (var row in linesJson.EnumerateArray()) Fields(row, PinFields);
        var value = JsonSerializer.Deserialize<Envelope>(evidence.PayloadJson, Json);
        if (value is null || value.Lines is null || value.Lines.Any(x => x is null) || value.SchemaVersion != 1 || value.EventId != evidence.EventId
            || value.TenantId != evidence.TenantId || value.CompanyId != evidence.CompanyId
            || value.BranchId != evidence.BranchId || value.MakerSubjectId != evidence.MakerSubjectId
            || value.MakerWorkerId != evidence.MakerWorkerId || value.CreateCommandKey != evidence.CreateCommandKey
            || value.OriginalFingerprint != evidence.OriginalFingerprint || current.Id != value.EventId
            || current.TenantId != value.TenantId || current.LegalEntityId != value.CompanyId || current.Status != "Draft"
            || current.OwnerSubjectId != value.MakerSubjectId || current.Title != value.Title
            || current.CurrencyCode != value.CurrencyCode || current.Version < 1
            || bindings.Count != value.Lines.Length || bindings.Select(x => x.Pin.LineId).Distinct().Count() != bindings.Count
            || value.Lines.Select(x => x.LineId).Distinct().Count() != value.Lines.Length
            || value.Lines.Select(x => x.EventLineId).Distinct().Count() != value.Lines.Length
            || value.Lines.Select(x => x.AllocationId).Distinct().Count() != value.Lines.Length
            || !value.Lines.Select(x => x.LineId).SequenceEqual(value.Lines.OrderBy(x => x.LineId).Select(x => x.LineId))) Fail();
        foreach (var pin in value!.Lines)
        {
            var owned = bindings.SingleOrDefault(x => x.Pin.LineId == pin.LineId);
            if (owned is null || owned.Pin != pin) Fail();
            var line = owned!.Line; var allocation = owned.Allocation;
            if (pin.RequestId == Guid.Empty || pin.LineId == Guid.Empty || pin.SubmissionId == Guid.Empty
                || pin.CaseId == Guid.Empty || pin.OutcomeId == Guid.Empty || pin.DecisionId == Guid.Empty
                || pin.FinanceId == Guid.Empty || pin.AllocationId == Guid.Empty || pin.EventLineId == Guid.Empty
                || pin.MakerWorkerId == Guid.Empty || pin.RequesterWorkerId == Guid.Empty
                || pin.SubmittedVersion < 1 || pin.RequestVersion < pin.SubmittedVersion
                || !DigestShape(pin.RequestSha) || !DigestShape(pin.PolicyDecisionSha)
                || !DigestShape(pin.PolicyRulesSha) || !DigestShape(pin.FinanceSha)
                || string.IsNullOrWhiteSpace(pin.UnitCode) || string.IsNullOrWhiteSpace(pin.MethodCode)
                || line.Id != pin.EventLineId || line.SourcingEventId != current.Id || line.Status != "Active"
                || line.TenantId != current.TenantId || line.LegalEntityId != current.LegalEntityId
                || line.PurchaseRequestLineId != pin.LineId || line.Quantity != pin.Quantity || line.UnitCode != pin.UnitCode
                || line.CategoryCode != pin.CategoryCode || allocation.Id != pin.AllocationId
                || allocation.TenantId != current.TenantId || allocation.LegalEntityId != current.LegalEntityId
                || allocation.PurchaseRequestId != pin.RequestId || allocation.PurchaseRequestLineId != pin.LineId
                || allocation.TargetType != "SourcingEvent" || allocation.TargetId != current.Id || allocation.Status != "Reserved"
                || allocation.Quantity != pin.Quantity || allocation.EstimatedValue != pin.EstimatedValue
                || allocation.CurrencyCode != current.CurrencyCode || allocation.Method != pin.MethodCode
                || allocation.IdempotencyKey != $"rfq.reserve.{evidence.CreateCommandKey:N}.{pin.LineId:N}"
                || pin.Quantity <= 0 || !ExactDecimalJsonCodec.IsRepresentable(pin.Quantity, 18, 4)
                || pin.UnitEstimate < 0 || !ExactDecimalJsonCodec.IsRepresentable(pin.UnitEstimate, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(pin.EstimatedValue, 38, 8)
                || pin.EstimatedValue != ExactDecimalArithmetic.Multiply(pin.Quantity, pin.UnitEstimate)
                || pin.MethodId == Guid.Empty || pin.MethodRevision < 1 || pin.MinimumQuoteCount is < 0 or > 20) Fail();
            var history = approvals.Where(x => x.Submission.Id == pin.SubmissionId).ToArray();
            if (history.Length != 1) Fail();
            VerifyOriginal(pin, history[0], value, line);
            if (current.Method != pin.MethodCode) Fail();
        }
        if (approvals.Select(x => x.Submission.Id).Distinct().Count() != approvals.Count
            || approvals.Any(x => !value.Lines.Any(pin => pin.SubmissionId == x.Submission.Id))) Fail();
        _ = ExactDecimalArithmetic.Sum(value.Lines.Select(x => x.EstimatedValue));
        var actor = new RfqCurrentActor(value.MakerSubjectId, value.MakerWorkerId, value.TenantId, value.CompanyId,
            value.BranchId, new HashSet<Guid>(), true, new HashSet<string>(), true, new HashSet<Guid>());
        var draft = GovernedRfqDraft.Draft.Create(value.EventId, actor, value.BranchId, value.CurrencyCode,
            value.Title, "Draft", value.Lines);
        if (draft.Fingerprint != evidence.OriginalFingerprint) Fail();
        return draft;
    }

    private static void VerifyOriginal(RfqDemandPin pin, RfqHistoricalApproval source, Envelope draft, SourcingLine currentLine)
    {
        var p = source.Submission; var o = source.Outcome; var c = source.Case; var d = source.Decision;
        var e = source.Evaluation; var policy = source.Policy; var f = source.Finance;
        if (new ProcurementRecord[] { p, o, e, policy, f }.Any(x => x.TenantId != draft.TenantId || x.LegalEntityId != draft.CompanyId)
            || !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(p) || p.PurchaseRequestId != pin.RequestId
            || p.Id != pin.SubmissionId || p.SubmittedBusinessVersion != pin.SubmittedVersion
            || p.CoreApprovalCaseId != pin.CaseId || p.MakerWorkerId != pin.MakerWorkerId || p.RequesterWorkerId != pin.RequesterWorkerId
            || p.CurrencyCode != draft.CurrencyCode || p.AmountBasis != "EstimatedLineBase"
            || p.RequestSnapshotSha256 != pin.RequestSha || p.RequestSnapshotSha256 != ProcurementEvidenceEncoding.Digest(p.RequestSnapshotJson)
            || p.PolicyDecisionSha256 != pin.PolicyDecisionSha || p.PolicyRulesSha256 != pin.PolicyRulesSha
            || p.FinanceSnapshotId != pin.FinanceId || p.FinanceSnapshotSha256 != pin.FinanceSha
            || p.FinanceSnapshotSha256 != ProcurementEvidenceEncoding.Digest(p.FinanceSnapshotJson)
            || p.FinanceSnapshotJson != PurchaseRequestApprovalPinning.FinanceSnapshot(f)
            || p.PolicyEvaluationId != e.Id || p.PolicyVersionId != policy.Id || p.PolicyVersionNumber != policy.PolicyVersionNumber
            || p.PolicyDecisionSha256 != ProcurementEvidenceEncoding.Digest(e.DecisionJson)
            || p.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || e.PurchaseRequestId != pin.RequestId || e.RequestVersion != pin.SubmittedVersion || e.Outcome != "Matched"
            || e.PolicyVersionId != policy.Id || e.PolicyVersionNumber != policy.PolicyVersionNumber
            || f.Id != pin.FinanceId || f.PurchaseRequestId != pin.RequestId || f.RequestVersion != pin.SubmittedVersion
            || o.Id != pin.OutcomeId || o.SubmissionId != p.Id || o.PurchaseRequestId != pin.RequestId
            || o.SubmittedBusinessVersion != pin.SubmittedVersion || o.CoreApprovalCaseId != pin.CaseId
            || o.CoreDecisionId != pin.DecisionId || o.Outcome != "Approved"
            || c.Id != pin.CaseId || c.TenantId != draft.TenantId || c.LegalEntityId != draft.CompanyId
            || c.SourceModule != "procurement" || c.SourceType != "purchase_request" || c.SourceId != pin.RequestId
            || c.SourceVersion != pin.SubmittedVersion || c.State != "Approved" || c.CompletedAt is null
            || p.PinnedAt == default || c.CompletedAt < p.PinnedAt || d.DecidedAt != c.CompletedAt
            || o.AppliedAt < d.DecidedAt
            || c.PolicyVersionId != policy.Id || c.PolicyVersionNumber != policy.PolicyVersionNumber || c.MakerSubjectId != p.MakerSubjectId
            || d.Id != pin.DecisionId || d.CaseId != c.Id || d.TenantId != draft.TenantId || d.LegalEntityId != draft.CompanyId
            || d.Action != "Approve" || d.ActorSubjectId == c.MakerSubjectId || c.CompletionKey != d.IdempotencyKey
            || o.DecisionCommandKey != d.IdempotencyKey) Fail();
        using var original = StrictDocument(p.RequestSnapshotJson);
        Fields(original.RootElement, RequestFields);
        var request = original.RootElement;
        if (request.GetProperty("schemaVersion").GetInt32() != 1 || Id(request, "Id") != pin.RequestId
            || Text(request, "TenantId") != draft.TenantId || Id(request, "LegalEntityId") != draft.CompanyId
            || Text(request, "CurrencyCode") != draft.CurrencyCode || NullableId(request, "BranchRef") != draft.BranchId
            || NullableId(request, "DepartmentRef") != pin.DepartmentId || Text(request, "CreatedBy") != p.MakerSubjectId
            || Id(request, "RequesterWorkerId") != p.RequesterWorkerId
            || request.GetProperty("SubmittedVersion").GetInt64() != pin.SubmittedVersion) Fail();
        var originals = request.GetProperty("lines").EnumerateArray().ToArray();
        if (originals.Length is < 1 or > 200 || originals.Select(x => Id(x, "Id")).Distinct().Count() != originals.Length) Fail();
        foreach (var row in originals) Fields(row, RequestLineFields);
        var matched = originals.Where(x => Id(x, "Id") == pin.LineId).ToArray();
        if (matched.Length != 1) Fail();
        var line = matched[0];
        if (Id(line, "PurchaseRequestId") != pin.RequestId || Amount(line, "quantity", 18, 4) < pin.Quantity
            || Amount(line, "unitPrice", 18, 4) != pin.UnitEstimate || Text(line, "UnitCode") != pin.UnitCode
            || Text(line, "CategoryCode") != pin.CategoryCode || Text(line, "Description") != currentLine.Description) Fail();
        if (Text(line, "NeededBy") != currentLine.NeededBy?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) Fail();
        var amount = ExactDecimalArithmetic.Sum(originals.Select(x => ExactDecimalArithmetic.Multiply(
            Amount(x, "quantity", 18, 4), Amount(x, "unitPrice", 18, 4))));
        if (amount != p.EstimatedAmount) Fail();
        using var evaluation = StrictDocument(e.DecisionJson);
        var routes = evaluation.RootElement.GetProperty("lines").EnumerateArray().Where(x => Id(x, "lineId") == pin.LineId).ToArray();
        if (routes.Length != 1) Fail();
        var route = routes[0].GetProperty("decision");
        if (Text(route, "status") != "Match" || Text(route, "sourcingMethodCode") != pin.MethodCode
            || route.GetProperty("minimumQuoteCount").GetInt32() != pin.MinimumQuoteCount) Fail();
    }

    private static JsonDocument StrictDocument(string json)
    {
        if (json is null || json.Length > 1000000) Fail();
        var document = JsonDocument.Parse(json!);
        try { NoDuplicates(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void NoDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) Fail(); NoDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var row in value.EnumerateArray()) NoDuplicates(row);
    }
    private static void Fields(JsonElement value, string fields)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(x => x.Name)
            .Order(StringComparer.Ordinal).SequenceEqual(fields.Split(' ').Order(StringComparer.Ordinal))) Fail();
    }
    private static Guid Id(JsonElement value, string key) => value.GetProperty(key).GetGuid();
    private static Guid? NullableId(JsonElement value, string key) => value.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Id(value, key);
    private static string? Text(JsonElement value, string key) => value.GetProperty(key).ValueKind == JsonValueKind.Null ? null : value.GetProperty(key).GetString();
    private static decimal Amount(JsonElement value, string key, int precision, int scale)
    { var token = value.GetProperty(key); return ExactDecimalJsonCodec.Parse(token.ValueKind == JsonValueKind.String ? token.GetString() : token.GetRawText(), precision, scale); }
    private sealed class EvidenceDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => ExactDecimalJsonCodec.Read(ref reader, 38, 8, "RfqEvidenceAmount");
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(ProcurementEvidenceEncoding.Decimal(value));
    }
    private static bool DigestShape(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail() => throw new InvalidOperationException("RfqRetainedDraftInvalid");
}
