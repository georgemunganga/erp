using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovalPinValidation(IReadOnlyList<string> Blockers)
{
    public bool IsValid => Blockers.Count == 0;
}

/// <summary>
/// Pure evidence construction and verification. Callers still own current identity,
/// authority, Finance freshness/revocation, transaction isolation and Core execution.
/// Never treats the mutable row revision as the submitted business version.
/// </summary>
public static class PurchaseRequestApprovalPinning
{
    public const string EstimatedLineBase = "EstimatedLineBase";
    public const string AutomatedFinance = "AutomatedFinance";
    // These strings describe evidence provenance only. A later authoritative resolver
    // must verify the integration source or approved manual policy; caller input is not authority.
    public const string ApprovedManualEvidence = "ApprovedManualEvidence";

    public static PurchaseRequestApprovalSubmission Pin(PurchaseRequest request,
        PolicyEvaluation evaluation, PolicyVersion policy, BudgetCheckSnapshot finance,
        string tenantId, Guid legalEntityId, Guid coreCaseId, Guid openingCommandKey,
        string financeEvidenceMode, string? manualEvidenceAuthorityRef, DateTimeOffset pinnedAt,
        Guid makerWorkerId, Guid requesterWorkerId)
    {
        var issues = ValidateInputs(request, evaluation, policy, finance, tenantId, legalEntityId);
        if (request.Version != request.SubmittedVersion || request.ApprovalInstanceRef is not null)
            issues.Add("SourceAlreadyChangedOrLinked");
        if (makerWorkerId == Guid.Empty || requesterWorkerId == Guid.Empty || requesterWorkerId != request.RequesterWorkerId)
            issues.Add("OpeningWorkerIdentityRequired");
        if (coreCaseId == Guid.Empty || openingCommandKey == Guid.Empty)
            issues.Add("OpeningIdentityRequired");
        if (financeEvidenceMode is not (AutomatedFinance or ApprovedManualEvidence)
            || financeEvidenceMode == ApprovedManualEvidence && string.IsNullOrWhiteSpace(manualEvidenceAuthorityRef)
            || financeEvidenceMode == AutomatedFinance && manualEvidenceAuthorityRef is not null)
            issues.Add("FinanceEvidenceModeInvalid");
        if (pinnedAt == default || request.SubmittedAt is { } submittedAt && pinnedAt < submittedAt
            || finance.CheckedAt > pinnedAt)
            issues.Add("PinTimeInvalid");
        if (issues.Count != 0) throw new ArgumentException(string.Join(",", issues.Distinct()));
        var snapshot = RequestSnapshot(request);
        var financeSnapshot = FinanceSnapshot(finance);
        return new PurchaseRequestApprovalSubmission
        {
            TenantId = tenantId, LegalEntityId = legalEntityId,
            PurchaseRequestId = request.Id, SubmittedBusinessVersion = request.SubmittedVersion!.Value,
            CoreApprovalCaseId = coreCaseId, OpeningCommandKey = openingCommandKey,
            SnapshotSchemaVersion = 2, MakerSubjectId = request.CreatedBy,
            MakerWorkerId = makerWorkerId, RequesterWorkerId = requesterWorkerId,
            WorkerIdentitySha256 = WorkerIdentityDigest(tenantId, legalEntityId, request.Id,
                request.SubmittedVersion.Value, coreCaseId, request.CreatedBy, makerWorkerId, requesterWorkerId),
            RequestSnapshotJson = snapshot, RequestSnapshotSha256 = Digest(snapshot),
            PolicyEvaluationId = evaluation.Id, PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber,
            PolicyDecisionSha256 = Digest(evaluation.DecisionJson), PolicyRulesSha256 = PolicyRulesDigest(policy.RulesJson),
            FinanceSnapshotId = finance.Id, FinanceSnapshotJson = financeSnapshot,
            FinanceSnapshotSha256 = Digest(financeSnapshot), FinanceEvidenceMode = financeEvidenceMode,
            ManualEvidenceAuthorityRef = manualEvidenceAuthorityRef,
            AmountBasis = EstimatedLineBase, EstimatedAmount = Amount(request),
            CurrencyCode = request.CurrencyCode, PinnedAt = pinnedAt,
        };
    }

    public static string WorkerIdentityDigest(string tenant, Guid company, Guid request, long submittedVersion,
        Guid caseId, string makerSubject, Guid makerWorker, Guid requesterWorker) => Digest(JsonSerializer.Serialize(new
        {
            schemaVersion = 2, tenant, company, request, submittedVersion, caseId, makerSubject, makerWorker, requesterWorker,
        }));

    public static bool HasRetainedWorkerIdentity(PurchaseRequestApprovalSubmission pin) => pin.SnapshotSchemaVersion == 2
        && pin.MakerWorkerId is { } maker && maker != Guid.Empty
        && pin.RequesterWorkerId is { } requester && requester != Guid.Empty
        && pin.WorkerIdentitySha256 == WorkerIdentityDigest(pin.TenantId, pin.LegalEntityId, pin.PurchaseRequestId,
            pin.SubmittedBusinessVersion, pin.CoreApprovalCaseId, pin.MakerSubjectId, maker, requester);

    public static ApprovalPinValidation Verify(PurchaseRequestApprovalSubmission pin,
        PurchaseRequest request, PolicyEvaluation evaluation, PolicyVersion policy,
        BudgetCheckSnapshot currentFinance, string tenantId, Guid legalEntityId) =>
        VerifyEvidence(pin, request, evaluation, policy, currentFinance, tenantId, legalEntityId, true);

    /// <summary>For exact terminal-effect replay/release resolution only. Business evidence remains immutable;
    /// the caller separately verifies the matching terminal Core case and resulting Procurement state.</summary>
    public static ApprovalPinValidation VerifyBusinessEvidence(PurchaseRequestApprovalSubmission pin,
        PurchaseRequest request, PolicyEvaluation evaluation, PolicyVersion policy,
        BudgetCheckSnapshot currentFinance, string tenantId, Guid legalEntityId) =>
        VerifyEvidence(pin, request, evaluation, policy, currentFinance, tenantId, legalEntityId, false);

    private static ApprovalPinValidation VerifyEvidence(PurchaseRequestApprovalSubmission pin,
        PurchaseRequest request, PolicyEvaluation evaluation, PolicyVersion policy,
        BudgetCheckSnapshot currentFinance, string tenantId, Guid legalEntityId, bool requirePendingState)
    {
        var issues = ValidateInputs(request, evaluation, policy, currentFinance, tenantId, legalEntityId, requirePendingState);
        if (!InScope(pin, tenantId, legalEntityId)) issues.Add("PinnedScopeMismatch");
        if (pin.OpeningCommandKey == Guid.Empty || pin.CoreApprovalCaseId == Guid.Empty || pin.PinnedAt == default
            || pin.PinnedAt < request.SubmittedAt || currentFinance.CheckedAt > pin.PinnedAt)
            issues.Add("PinnedMetadataInvalid");
        if (!HasRetainedWorkerIdentity(pin) || pin.RequesterWorkerId != request.RequesterWorkerId)
            issues.Add("PinnedWorkerIdentityMissingOrInvalid");
        if (pin.SnapshotSchemaVersion != 2 || pin.PurchaseRequestId != request.Id
            || pin.SubmittedBusinessVersion != request.SubmittedVersion
            || pin.MakerSubjectId != request.CreatedBy)
            issues.Add("PinnedSourceMismatch");
        if (!Guid.TryParse(request.ApprovalInstanceRef, out var linkedCase)
            || linkedCase == Guid.Empty || linkedCase != pin.CoreApprovalCaseId)
            issues.Add("LinkedApprovalCaseMismatch");
        if (pin.PolicyEvaluationId != evaluation.Id || pin.PolicyVersionId != policy.Id
            || pin.PolicyVersionNumber != policy.PolicyVersionNumber
            || pin.PolicyDecisionSha256 != Digest(evaluation.DecisionJson)
            || pin.PolicyRulesSha256 != PolicyRulesDigest(policy.RulesJson))
            issues.Add("PinnedPolicyMismatch");
        if (pin.FinanceSnapshotId != currentFinance.Id
            || pin.FinanceSnapshotSha256 != Digest(pin.FinanceSnapshotJson)
            || pin.FinanceSnapshotJson != FinanceSnapshot(currentFinance))
            issues.Add("PinnedFinanceMismatch");
        if (pin.FinanceEvidenceMode is not (AutomatedFinance or ApprovedManualEvidence)
            || pin.FinanceEvidenceMode == ApprovedManualEvidence && string.IsNullOrWhiteSpace(pin.ManualEvidenceAuthorityRef)
            || pin.FinanceEvidenceMode == AutomatedFinance && pin.ManualEvidenceAuthorityRef is not null)
            issues.Add("FinanceEvidenceModeInvalid");
        if (pin.AmountBasis != EstimatedLineBase || pin.CurrencyCode != request.CurrencyCode)
            issues.Add("PinnedAmountBasisMismatch");
        // Invalid inputs already block execution; do not build a misleading snapshot from them.
        if (!issues.Contains("InvalidLines") && !issues.Contains("InvalidSource"))
        {
            var snapshot = RequestSnapshot(request);
            if (pin.RequestSnapshotSha256 != Digest(pin.RequestSnapshotJson)
                || pin.RequestSnapshotJson != snapshot)
                issues.Add("SubmittedBusinessInputsChanged");
            try { if (pin.EstimatedAmount != Amount(request)) issues.Add("PinnedAmountMismatch"); }
            catch (OverflowException) { issues.Add("AmountOverflow"); }
        }
        return new(issues.Distinct().ToArray());
    }

    private static List<string> ValidateInputs(PurchaseRequest request, PolicyEvaluation evaluation,
        PolicyVersion policy, BudgetCheckSnapshot finance, string tenantId, Guid legalEntityId, bool requirePendingState = true)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(tenantId) || legalEntityId == Guid.Empty
            || new ProcurementRecord[] { request, evaluation, policy, finance }.Any(x => !InScope(x, tenantId, legalEntityId)))
            issues.Add("SourceScopeMismatch");
        if (request.Id == Guid.Empty || request.SubmittedVersion is not > 0
            || request.Version < request.SubmittedVersion || requirePendingState && request.Status != "Submitted"
            || request.SubmittedAt is null || string.IsNullOrWhiteSpace(request.CreatedBy)
            || request.RequesterWorkerId == Guid.Empty || string.IsNullOrWhiteSpace(request.CurrencyCode))
            issues.Add("InvalidSource");
        var lines = request.Lines.Where(x => x.ApprovalStatus != "Cancelled").ToArray();
        if (lines.Length == 0 || lines.Any(x => !InScope(x, tenantId, legalEntityId)
                || x.PurchaseRequestId != request.Id || x.Id == Guid.Empty || x.LineNumber < 1
                || x.RequestedQuantity <= 0 || x.EstimatedUnitPrice < 0
                || requirePendingState && (x.ApprovalStatus != "Pending" || x.ApprovedQuantity != 0
                    || x.AllocatedQuantity != 0 || x.OrderedQuantity != 0 || x.AcceptedQuantity != 0
                    || x.CancelledQuantity != 0 || x.Route != "Unassigned"))
            || lines.Select(x => x.Id).Distinct().Count() != lines.Length
            || lines.Select(x => x.LineNumber).Distinct().Count() != lines.Length)
            issues.Add("InvalidLines");
        // A successor publication keeps the prior row Published and closes its future
        // selection interval. Eligibility is checked at submission, never at today's date;
        // hash/version checks below still prevent silently changing the pinned rules.
        if (evaluation.Id == Guid.Empty || evaluation.PurchaseRequestId != request.Id
            || evaluation.RequestVersion != request.SubmittedVersion || evaluation.Outcome != "Matched"
            || evaluation.AmountBasis != EstimatedLineBase || string.IsNullOrWhiteSpace(evaluation.DecisionJson)
            || evaluation.PolicyVersionId != policy.Id || evaluation.PolicyVersionNumber != policy.PolicyVersionNumber
            || policy.Id == Guid.Empty || policy.PolicyKey != "PURCHASE_REQUEST" || policy.State != "Published"
            || policy.PolicyVersionNumber < 1 || PolicyRulesDigest(policy.RulesJson).Length == 0
            || request.SubmittedAt is { } time && (policy.EffectiveFrom > time || policy.EffectiveTo <= time))
            issues.Add("PolicyEvidenceInvalid");
        var route = ApprovalRoutePreview.FromEvaluation(evaluation, lines.Select(x => x.Id).ToArray());
        if (!route.HasSingleRoute) issues.AddRange(route.Blockers);
        decimal amount = 0;
        try { amount = Amount(request); }
        catch (OverflowException) { issues.Add("AmountOverflow"); }
        if (finance.Id == Guid.Empty || finance.PurchaseRequestId != request.Id
            || finance.RequestVersion != request.SubmittedVersion || finance.Status != "Available"
            || finance.ReservationStatus != "Confirmed" || string.IsNullOrWhiteSpace(finance.ReservationRef)
            || string.IsNullOrWhiteSpace(finance.FinanceCheckRef) || string.IsNullOrWhiteSpace(finance.EvidenceRef)
            || finance.CurrencyCode != request.CurrencyCode || finance.CheckedAmount < amount
            || finance.CheckedAt < request.SubmittedAt)
            issues.Add("FinanceEvidenceInvalid");
        return issues;
    }

    private static bool InScope(ProcurementRecord record, string tenant, Guid entity) =>
        record.TenantId == tenant && record.LegalEntityId == entity;
    private static decimal Amount(PurchaseRequest request) => request.Lines
        .Where(x => x.ApprovalStatus != "Cancelled").Sum(x => x.RequestedQuantity * x.EstimatedUnitPrice);
    private static string Decimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    private static string Digest(string? value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));

    // PostgreSQL timestamptz stores microseconds; normalize before hashing tracked or reloaded values.
    private static DateTimeOffset? DatabaseTimestamp(DateTimeOffset? value) => value is { } time
        ? new DateTimeOffset(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero) : null;

    // Rules are jsonb in the existing policy store. Whitespace/property order and decimal
    // scale are not policy changes; duplicate keys or unsupported numbers are ambiguous.
    private static string PolicyRulesDigest(string? value)
    {
        try
        {
            using var json = JsonDocument.Parse(value ?? "");
            if (json.RootElement.ValueKind != JsonValueKind.Object) return "";
            using var bytes = new MemoryStream();
            using (var writer = new Utf8JsonWriter(bytes)) WriteCanonicalJson(writer, json.RootElement);
            return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException) { return ""; }
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                if (properties.GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1))
                    throw new FormatException("Ambiguous policy object.");
                writer.WriteStartObject();
                foreach (var property in properties)
                { writer.WritePropertyName(property.Name); WriteCanonicalJson(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonicalJson(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (!value.TryGetDecimal(out var number)) throw new FormatException("Unsupported policy number.");
                var exact = NormalizeNumber(value.GetRawText());
                if (exact != NormalizeNumber(Decimal(number)))
                    throw new FormatException("Policy number loses decimal precision.");
                writer.WriteRawValue(exact); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new FormatException("Unsupported policy value.");
        }
    }

    // Normalize exact base-10 coefficient/exponent strings, never binary floating point.
    // Comparing the raw and decimal forms detects TryGetDecimal's possible rounding.
    private static string NormalizeNumber(string literal)
    {
        var exponentAt = literal.IndexOf('e');
        if (exponentAt < 0) exponentAt = literal.IndexOf('E');
        var exponent = 0;
        if (exponentAt >= 0 && !int.TryParse(literal[(exponentAt + 1)..],
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            throw new FormatException("Unsupported policy exponent.");
        var mantissa = exponentAt < 0 ? literal : literal[..exponentAt];
        var negative = mantissa[0] == '-';
        if (negative) mantissa = mantissa[1..];
        var point = mantissa.IndexOf('.');
        var fractionDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) return "0";
        var trailing = 0;
        while (digits[digits.Length - trailing - 1] == '0') trailing++;
        if (trailing != 0) digits = digits[..^trailing];
        exponent = checked(exponent - fractionDigits + trailing);
        return (negative ? "-" : "") + digits + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }

    // Explicit schema avoids accidentally pinning mutable execution fields or omitting new business fields unnoticed.
    private static string RequestSnapshot(PurchaseRequest r) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, r.TenantId, r.LegalEntityId, r.Id, r.Number, r.CreatedBy,
        r.RequesterWorkerId, r.SubmittedVersion, SubmittedAt = DatabaseTimestamp(r.SubmittedAt), r.Purpose, r.CurrencyCode,
        r.NeededBy, r.BranchRef, r.SiteRef, r.DepartmentRef, r.CostCenterRef, r.ProjectRef,
        r.DeliveryLocationRef, r.DeliveryAddressText, r.OnBehalfOfSubjectId, r.PlanRef,
        r.FundingSourceRef, r.NotesToApprover, r.SourceChannel,
        lines = r.Lines.Where(x => x.ApprovalStatus != "Cancelled").OrderBy(x => x.LineNumber).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.PurchaseRequestId, x.LineNumber, x.Description, x.UnitCode,
                quantity = Decimal(x.RequestedQuantity), unitPrice = Decimal(x.EstimatedUnitPrice),
                x.EstimatedCurrencyCode, x.CatalogItemId, x.CatalogEntryId, x.CatalogPublishedVersion,
                x.PreferredSupplierId, x.CategoryCode, x.PurchaseType, x.NeededBy,
                discountPercent = x.DiscountPercent.HasValue ? Decimal(x.DiscountPercent.Value) : null,
                x.TaxCodeRef, x.CostCenterRef, x.ProjectRef, x.SpecificationDocumentRefId,
            }).ToArray(),
    });

    private static string FinanceSnapshot(BudgetCheckSnapshot f) => JsonSerializer.Serialize(new
    {
        f.Id, f.TenantId, f.LegalEntityId, f.PurchaseRequestId, f.RequestVersion, f.FinanceCheckRef,
        f.Status, checkedAmount = Decimal(f.CheckedAmount), f.CurrencyCode, CheckedAt = DatabaseTimestamp(f.CheckedAt),
        f.CheckedBySubjectId, f.EvidenceRef, f.DecisionReason, f.ReservationRef,
        f.ReservationStatus, f.ExchangeRateSource,
    });
}
