using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>A narrowly recognised, recorded source-3 comparison that stopped at
/// the original invoice header. It has no allocation or capacity effect. A
/// different blocked result must receive its own reviewed classifier. The
/// retained review hash is linked to the stored outcome; this classifier does
/// not itself issue a new Core approval or qualify the old invoice for Finance.</summary>
public static class PrincipalInvoiceBlockedNoClaimEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Discrepancy = "InvoiceHeaderAmountInvalid";
    private static readonly string[] CreatedFields = ["ActorPrincipalId", "ActorWorkerId", "CommandKey",
        "CreatedAt", "CreatedBy", "EvidenceJson", "EvidenceSha256", "ExecutionVersion", "Id",
        "InputFingerprint", "InvoiceOriginalSha256", "InvoiceVersion", "IssuedVersionId",
        "LegacyIssuedVersionRefId", "LegalEntityId", "MappingVersionId", "MatchedAt", "MatchType",
        "Outcome", "PolicyRulesSha256", "PolicyVersionId", "PriceVariance",
        "PrincipalIssuedVersionRefId", "PurchaseOrderId", "QuantityVariance", "SourceSchemaVersion",
        "SourceWatermarksJson", "SupplierInvoiceId", "TaxVariance", "TenantId", "UpdatedAt",
        "UpdatedBy", "Version"];

    public static (IReadOnlySet<Guid> Results, IReadOnlySet<Guid> Exceptions) VerifyClosedFootprint(
        IReadOnlyList<InvoiceMatchResult> results, IReadOnlyList<SupplierInvoice> invoices,
        IReadOnlyList<SupplierInvoiceLine> lines, IReadOnlyList<InvoiceMatchMappingVersion> mappings,
        IReadOnlyList<InvoiceUnitReviewSource> reviewSources,
        IReadOnlyList<InvoiceUnitReviewOutcome> reviewOutcomes,
        IReadOnlyList<PolicyVersion> policies, IReadOnlyList<ProcurementCommercialCommandReceipt> receipts,
        IReadOnlyList<AuditEvent> audits, IReadOnlyList<OutboxEvent> outbox,
        IReadOnlyList<InvoiceException> exceptions, IReadOnlyList<InvoiceMatchAllocation> allocations)
    {
        if (results.Count is < 1 or > 200 || allocations.Count != 0
            || receipts.Count != results.Count || audits.Count != results.Count * 2
            || outbox.Count != results.Count || exceptions.Count != results.Count
            || reviewSources.Count != results.Count || reviewOutcomes.Count != results.Count
            || results.Select(x => x.Id).Distinct().Count() != results.Count
            || results.Select(x => x.CommandKey).Distinct().Count() != results.Count
            || results.Any(x => x.CommandKey is null || x.PolicyVersionId is null
                || x.MappingVersionId is null))
            throw new InvalidOperationException("PrincipalInvoiceBlockedFootprintIncomplete");
        var resultIds = new HashSet<Guid>();
        var exceptionIds = new HashSet<Guid>();
        foreach (var result in results)
        {
            var invoice = invoices.Single(x => x.Id == result.SupplierInvoiceId);
            var mapping = mappings.Single(x => x.Id == result.MappingVersionId);
            var policy = policies.Single(x => x.Id == result.PolicyVersionId);
            var receipt = receipts.Single(x => x.CommandKey == result.CommandKey);
            var reviewSource = reviewSources.Single(x => x.MappingVersionId == mapping.Id);
            var reviewOutcome = reviewOutcomes.Single(x => x.ReviewSourceId == reviewSource.Id);
            var comparisonAudit = audits.Single(x => x.Action == "PrincipalInvoiceCompared"
                && x.CorrelationId == result.CommandKey!.Value.ToString("D"));
            var resultCreateAudit = audits.Single(x => x.RecordType == "InvoiceMatchResult"
                && x.RecordId == result.Id && x.Action == "create");
            var comparedOutbox = outbox.Single(x => x.IdempotencyKey == result.CommandKey!.Value.ToString("D"));
            var relatedExceptions = exceptions.Where(x => x.MatchResultId == result.Id).ToArray();
            Verify(result, invoice, mapping, lines.Where(x => x.SupplierInvoiceId == invoice.Id).ToArray(),
                policy, reviewSource, reviewOutcome, receipt, comparisonAudit, resultCreateAudit, comparedOutbox,
                relatedExceptions, allocations);
            resultIds.Add(result.Id);
            exceptionIds.Add(relatedExceptions.Single().Id);
        }
        if (resultIds.Count != results.Count || exceptionIds.Count != exceptions.Count)
            throw new InvalidOperationException("PrincipalInvoiceBlockedFootprintIncomplete");
        return (resultIds, exceptionIds);
    }

    public static void Verify(InvoiceMatchResult result, SupplierInvoice invoice,
        InvoiceMatchMappingVersion mapping, IReadOnlyList<SupplierInvoiceLine> originalLines,
        PolicyVersion policy, InvoiceUnitReviewSource reviewSource,
        InvoiceUnitReviewOutcome reviewOutcome, ProcurementCommercialCommandReceipt receipt,
        AuditEvent comparisonAudit, AuditEvent resultCreateAudit, OutboxEvent comparedOutbox,
        IReadOnlyList<InvoiceException> exceptions, IReadOnlyList<InvoiceMatchAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(originalLines);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(reviewSource);
        ArgumentNullException.ThrowIfNull(reviewOutcome);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(comparisonAudit);
        ArgumentNullException.ThrowIfNull(resultCreateAudit);
        ArgumentNullException.ThrowIfNull(comparedOutbox);
        ArgumentNullException.ThrowIfNull(exceptions);
        ArgumentNullException.ThrowIfNull(allocations);
        void Need(bool value)
        {
            if (!value) throw new InvalidOperationException("PrincipalInvoiceBlockedNoClaimUnproven");
        }
        static bool Hex(string? value) => value is { Length: 64 }
            && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        static bool Fields(JsonElement value, params string[] expected) => value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Order(StringComparer.Ordinal));
        static Guid? OptionalGuid(JsonElement value) => value.ValueKind == JsonValueKind.Null
            ? null : value.GetGuid();
        static string? OptionalText(JsonElement value) => value.ValueKind == JsonValueKind.Null
            ? null : value.GetString();
        static bool SameVersionRows(JsonElement retained, JsonElement checkpoint)
        {
            if (retained.ValueKind != JsonValueKind.Array || checkpoint.ValueKind != JsonValueKind.Array)
                return false;
            var actual = retained.EnumerateArray().Select(x => (
                Id: x.GetProperty("id").GetGuid(), Version: x.GetProperty("version").GetInt64())).ToArray();
            var pinned = checkpoint.EnumerateArray().Select(x => (
                Id: x.GetProperty("id").GetGuid(), Version: x.GetProperty("version").GetInt64())).ToArray();
            return actual.Length == pinned.Length
                && actual.Select(x => x.Id).Distinct().Count() == actual.Length
                && pinned.Select(x => x.Id).Distinct().Count() == pinned.Length
                && actual.OrderBy(x => x.Id).SequenceEqual(pinned.OrderBy(x => x.Id));
        }
        var key = result.CommandKey;
        Need(result.Id != Guid.Empty && invoice.Id != Guid.Empty && mapping.Id != Guid.Empty
            && key is { } commandKey && commandKey != Guid.Empty
            && result.SourceSchemaVersion == 3 && result.ExecutionVersion == 2
            && result.Outcome == "Blocked" && result.MatchType == "Unresolved"
            && result.QuantityVariance is null && result.PriceVariance is null
            && result.TaxVariance is null
            && result.SupplierInvoiceId == invoice.Id && result.MappingVersionId == mapping.Id
            && result.PurchaseOrderId == mapping.PurchaseOrderId
            && result.IssuedVersionId == mapping.IssuedVersionId
            && result.TenantId == invoice.TenantId && result.LegalEntityId == invoice.LegalEntityId
            && mapping.TenantId == invoice.TenantId && mapping.LegalEntityId == invoice.LegalEntityId
            && result.ActorPrincipalId == receipt.ActorPrincipalId
            && result.ActorPrincipalId == comparisonAudit.ActorSubjectId
            && result.ActorPrincipalId == resultCreateAudit.ActorSubjectId
            && result.ActorPrincipalId == invoice.UpdatedBy
            && result.ActorWorkerId == mapping.ActorWorkerId
            && result.InvoiceVersion > 0 && invoice.Version == result.InvoiceVersion + 1
            && invoice.Status == "Received" && invoice.MatchingState == "ComparisonBlocked"
            && invoice.CurrentMappingVersionId == mapping.Id
            && invoice.LastMatchResultId == result.Id && invoice.ActiveMatchClaimResultId is null
            && invoice.SupplierId != Guid.Empty && invoice.BranchRef is not null
            && invoice.CurrencyCode is { Length: 3 } && invoice.PurchaseOrderId == mapping.PurchaseOrderId
            && invoice.FinancePostingRef is null && invoice.CreditForInvoiceRef is null
            && mapping.SourceSchemaVersion == 3 && mapping.IdentitySchemaVersion == 2
            && mapping.SupplierInvoiceId == invoice.Id && mapping.MappingRevision == invoice.MappingRevision
            && Hex(result.InvoiceOriginalSha256) && result.InvoiceOriginalSha256 == mapping.InvoiceOriginalSha256
            && Hex(result.InputFingerprint) && Hex(result.EvidenceSha256)
            && Hex(result.PolicyRulesSha256) && result.EvidenceJson is { Length: > 0 and <= 1_048_576 }
            && ProcurementEvidenceEncoding.Digest(result.EvidenceJson) == result.EvidenceSha256
            && Hex(ProcurementEvidenceEncoding.PolicyJsonDigest(result.EvidenceJson))
            && result.SourceWatermarksJson is { Length: > 0 and <= 1_048_576 }
            && Hex(ProcurementEvidenceEncoding.PolicyJsonDigest(result.SourceWatermarksJson))
            && result.MatchedAt != default && result.MatchedAt.Offset == TimeSpan.Zero
            && allocations.Count == 0
            && receipt.TenantId == result.TenantId && receipt.LegalEntityId == result.LegalEntityId
            && receipt.Operation == "InvoiceCompare" && receipt.CommandKey == key
            && receipt.PurchaseOrderId == result.PurchaseOrderId
            && receipt.ActorPrincipalId == result.ActorPrincipalId && Hex(receipt.Fingerprint)
            && receipt.CompletedAt >= result.MatchedAt
            && receipt.ResponseJson is { Length: > 0 and <= 262_144 }
            && policy.Id == result.PolicyVersionId && policy.TenantId == result.TenantId
            && policy.LegalEntityId == result.LegalEntityId
            && policy.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey
            && policy.PublishedAt is { } published && published <= result.MatchedAt
            && policy.EffectiveFrom <= result.MatchedAt
            && (policy.EffectiveTo is null || result.MatchedAt < policy.EffectiveTo)
            && ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) == result.PolicyRulesSha256
            && reviewSource.TenantId == result.TenantId && reviewSource.LegalEntityId == result.LegalEntityId
            && reviewSource.SupplierInvoiceId == invoice.Id && reviewSource.MappingVersionId == mapping.Id
            && reviewSource.PurchaseOrderId == mapping.PurchaseOrderId
            && reviewSource.IssuedVersionId == mapping.IssuedVersionId
            && reviewSource.OriginalSha256 == mapping.InvoiceOriginalSha256
            && reviewSource.MappingSha256 == mapping.MappingSha256
            && reviewSource.MatchingPolicyId == policy.Id
            && reviewSource.MakerPrincipalId == mapping.ActorPrincipalId
            && reviewOutcome.TenantId == result.TenantId
            && reviewOutcome.LegalEntityId == result.LegalEntityId
            && reviewOutcome.ReviewSourceId == reviewSource.Id
            && reviewOutcome.CoreCaseId == reviewSource.CoreCaseId
            && reviewOutcome.State == "Verified" && reviewOutcome.EvidenceSchemaVersion == 4
            && Hex(reviewOutcome.EvidenceSha256)
            && ProcurementEvidenceEncoding.Digest(reviewOutcome.EvidenceJson) == reviewOutcome.EvidenceSha256
            && Hex(ProcurementEvidenceEncoding.PolicyJsonDigest(reviewOutcome.EvidenceJson)));
        // The immutable invoice line retains its original nullable legacy source
        // columns. Principal comparison projects the selected PO line and M5 fact
        // into a transient document; it never writes them back to that old row.
        var mappingSnapshot = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, invoice, originalLines);
        try
        {
            using var evidence = JsonDocument.Parse(result.EvidenceJson!);
            var e = evidence.RootElement;
            Need(Fields(e, "schemaVersion", "commandKey", "inputFingerprint", "actor",
                    "invoiceId", "invoiceVersion", "outcome", "discrepancies", "lines", "source")
                && e.GetProperty("schemaVersion").GetInt32() == 1
                && e.GetProperty("commandKey").GetGuid() == key
                && e.GetProperty("inputFingerprint").GetString() == result.InputFingerprint
                && e.GetProperty("actor").GetString() == result.ActorPrincipalId
                && e.GetProperty("invoiceId").GetGuid() == invoice.Id
                && e.GetProperty("invoiceVersion").GetInt64() == result.InvoiceVersion
                && e.GetProperty("outcome").GetString() == "Blocked"
                && e.GetProperty("lines").GetArrayLength() == 0);
            var issues = e.GetProperty("discrepancies");
            Need(issues.GetArrayLength() == 1
                && Fields(issues[0], "invoiceLineId", "code")
                && issues[0].GetProperty("invoiceLineId").ValueKind == JsonValueKind.Null
                && issues[0].GetProperty("code").GetString() == Discrepancy);
            var source = e.GetProperty("source");
            var original = source.GetProperty("invoice").GetProperty("lines");
            var acceptedFacts = source.GetProperty("acceptances");
            Need(original.GetArrayLength() == originalLines.Count(x => x.IsActive));
            foreach (var line in originalLines.Where(x => x.IsActive))
            {
                var recorded = original.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == line.Id);
                var selected = mappingSnapshot.Lines.Single(x => x.InvoiceLineId == line.Id);
                Guid? goodsFact = null, serviceFact = null;
                if (selected.PrincipalReceivingFactId is { } factId)
                {
                    if (selected.PrincipalReceivingSourceStateId is not { } sourceId)
                        throw new InvalidOperationException("PrincipalInvoiceBlockedNoClaimUnproven");
                    var fact = acceptedFacts.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == factId);
                    var kind = fact.GetProperty("kind").GetString();
                    Need(kind is "Goods" or "Service"
                        && fact.GetProperty("tenantId").GetString() == invoice.TenantId
                        && fact.GetProperty("legalEntityId").GetGuid() == invoice.LegalEntityId
                        && fact.GetProperty("branchRef").GetGuid() == invoice.BranchRef
                        && fact.GetProperty("purchaseOrderId").GetGuid() == mapping.PurchaseOrderId
                        && fact.GetProperty("revisionId").GetGuid() == mapping.IssuedVersionId
                        && fact.GetProperty("purchaseOrderLineId").GetGuid() == selected.PurchaseOrderLineId
                        && fact.GetProperty("state").GetString() == "Accepted"
                        && fact.GetProperty("reversed").ValueKind == JsonValueKind.False
                        && fact.GetProperty("currencyCode").GetString() == invoice.CurrencyCode
                        && fact.GetProperty("unitCode").GetString() == selected.InvoiceUnitCode
                        && fact.GetProperty("acceptanceReference").GetString()
                            == $"PrincipalReceiving:{sourceId:D}:{factId:D}");
                    if (kind == "Goods") goodsFact = factId; else serviceFact = factId;
                }
                Need(recorded.GetProperty("version").GetInt64() == line.Version
                    && recorded.GetProperty("supplierInvoiceId").GetGuid() == invoice.Id
                    && recorded.GetProperty("tenantId").GetString() == invoice.TenantId
                    && recorded.GetProperty("legalEntityId").GetGuid() == invoice.LegalEntityId
                    && OptionalGuid(recorded.GetProperty("purchaseOrderLineId")) == selected.PurchaseOrderLineId
                    && OptionalGuid(recorded.GetProperty("goodsReceiptLineId")) == goodsFact
                    && OptionalGuid(recorded.GetProperty("serviceAcceptanceId")) == serviceFact
                    && recorded.GetProperty("unitCode").GetString() == selected.InvoiceUnitCode
                    && recorded.GetProperty("quantity").GetString() == ProcurementEvidenceEncoding.Decimal(line.Quantity)
                    && recorded.GetProperty("unitPrice").GetString() == ProcurementEvidenceEncoding.Decimal(line.UnitPrice)
                    && recorded.GetProperty("taxAmount").GetString() == ProcurementEvidenceEncoding.Decimal(line.TaxAmount)
                    && recorded.GetProperty("description").GetString() == line.Description
                    && OptionalText(recorded.GetProperty("taxCodeRef")) == line.TaxCodeRef
                    && OptionalText(recorded.GetProperty("costCenterRef")) == line.CostCenterRef);
            }
            Need(Fields(source, "invoice", "order", "acceptances", "policy", "consumption", "duplicates")
                && source.GetProperty("invoice").GetProperty("id").GetGuid() == invoice.Id
                && source.GetProperty("invoice").GetProperty("version").GetInt64() == result.InvoiceVersion
                && source.GetProperty("invoice").GetProperty("tenantId").GetString() == invoice.TenantId
                && source.GetProperty("invoice").GetProperty("legalEntityId").GetGuid() == invoice.LegalEntityId
                && source.GetProperty("invoice").GetProperty("branchRef").GetGuid() == invoice.BranchRef
                && source.GetProperty("invoice").GetProperty("supplierId").GetGuid() == invoice.SupplierId
                && source.GetProperty("invoice").GetProperty("currencyCode").GetString() == invoice.CurrencyCode
                && source.GetProperty("invoice").GetProperty("status").GetString() == "Received"
                && source.GetProperty("invoice").GetProperty("invoiceNumber").GetString() == invoice.InvoiceNumber
                && source.GetProperty("invoice").GetProperty("normalizedInvoiceNumber").GetString()
                    == invoice.NormalizedInvoiceNumber
                && source.GetProperty("invoice").GetProperty("invoiceDate").GetString()
                    == invoice.InvoiceDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                && OptionalText(source.GetProperty("invoice").GetProperty("dueDate"))
                    == invoice.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                && OptionalGuid(source.GetProperty("invoice").GetProperty("evidenceDocumentRefId"))
                    == invoice.EvidenceDocumentRefId
                && OptionalText(source.GetProperty("invoice").GetProperty("creditForInvoiceRef"))
                    == invoice.CreditForInvoiceRef
                && OptionalText(source.GetProperty("invoice").GetProperty("financePostingRef"))
                    == invoice.FinancePostingRef
                && source.GetProperty("invoice").GetProperty("netAmount").GetString()
                    == ProcurementEvidenceEncoding.Decimal(invoice.NetAmount)
                && source.GetProperty("invoice").GetProperty("taxAmount").GetString()
                    == ProcurementEvidenceEncoding.Decimal(invoice.TaxAmount)
                && source.GetProperty("invoice").GetProperty("grossAmount").GetString()
                    == ProcurementEvidenceEncoding.Decimal(invoice.GrossAmount)
                && source.GetProperty("invoice").GetProperty("purchaseOrderId").GetGuid() == mapping.PurchaseOrderId
                && source.GetProperty("order").GetProperty("id").GetGuid() == mapping.PurchaseOrderId
                && source.GetProperty("order").GetProperty("revisionId").GetGuid() == mapping.IssuedVersionId
                && source.GetProperty("order").GetProperty("supplierId").GetGuid() == invoice.SupplierId
                && source.GetProperty("order").GetProperty("currencyCode").GetString() == invoice.CurrencyCode
                && source.GetProperty("order").GetProperty("branchRef").GetGuid() == invoice.BranchRef
                && source.GetProperty("policy").GetProperty("id").GetGuid() == policy.Id
                && ProcurementEvidenceEncoding.PolicyJsonDigest(source.GetProperty("policy").GetRawText())
                    == ProcurementEvidenceEncoding.PolicyJsonDigest(InvoiceMatchingFoundation.SerializePolicyEvidence(
                        InvoiceMatchingPolicyRules.Project(policy)))
                && source.GetProperty("acceptances").ValueKind == JsonValueKind.Array
                && source.GetProperty("consumption").GetProperty("complete").GetBoolean()
                && source.GetProperty("consumption").GetProperty("allocations").GetArrayLength() == 0
                && source.GetProperty("duplicates").GetProperty("complete").GetBoolean());
            using var watermarks = JsonDocument.Parse(result.SourceWatermarksJson!);
            var w = watermarks.RootElement;
            Need(Fields(w, "schemaVersion", "mappingVersionId", "mappingSha256", "invoiceOriginalSha256",
                    "invoiceId", "invoiceVersion", "purchaseOrderId", "issuedVersionId", "orderVersion",
                    "orderLines", "accepted", "policyId", "policyVersion", "reviewSha256",
                    "consumptionWatermark", "consumptionSha256", "duplicatesWatermark")
                && w.GetProperty("schemaVersion").GetInt32() == 1
                && w.GetProperty("mappingVersionId").GetGuid() == mapping.Id
                && w.GetProperty("mappingSha256").GetString() == mapping.MappingSha256
                && w.GetProperty("invoiceOriginalSha256").GetString() == mapping.InvoiceOriginalSha256
                && w.GetProperty("invoiceId").GetGuid() == invoice.Id
                && w.GetProperty("invoiceVersion").GetInt64() == result.InvoiceVersion
                && w.GetProperty("purchaseOrderId").GetGuid() == mapping.PurchaseOrderId
                && w.GetProperty("issuedVersionId").GetGuid() == mapping.IssuedVersionId
                && w.GetProperty("orderVersion").GetInt64()
                    == source.GetProperty("order").GetProperty("version").GetInt64()
                && w.GetProperty("policyId").GetGuid() == policy.Id
                && w.GetProperty("policyVersion").GetInt32() == policy.PolicyVersionNumber
                && w.GetProperty("reviewSha256").GetString()
                    == ProcurementEvidenceEncoding.PolicyJsonDigest(reviewOutcome.EvidenceJson)
                && w.GetProperty("consumptionWatermark").GetString()
                    == source.GetProperty("consumption").GetProperty("watermark").GetString()
                && w.GetProperty("consumptionSha256").GetString()
                    == InvoiceMatchExecutionEvidence.AllocationsDigest(Array.Empty<InvoiceMatchConsumption>())
                && w.GetProperty("duplicatesWatermark").GetString()
                    == source.GetProperty("duplicates").GetProperty("watermark").GetString()
                && SameVersionRows(source.GetProperty("order").GetProperty("lines"),
                    w.GetProperty("orderLines"))
                && SameVersionRows(source.GetProperty("acceptances"), w.GetProperty("accepted")));
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException
            or InvalidOperationException or IndexOutOfRangeException)
        { throw new InvalidOperationException("PrincipalInvoiceBlockedNoClaimUnproven", error); }
        Need(exceptions.Count == 1 && exceptions[0].MatchResultId == result.Id
            && exceptions[0].SupplierInvoiceId == invoice.Id
            && exceptions[0].SupplierInvoiceLineId is null
            && exceptions[0].TenantId == result.TenantId
            && exceptions[0].LegalEntityId == result.LegalEntityId
            && exceptions[0].Type == Discrepancy && exceptions[0].Status == "Open"
            && exceptions[0].Resolution is null && exceptions[0].ResolvedAt is null
            && exceptions[0].ResolvingMatchResultId is null
            && exceptions[0].OwnerSubjectId == result.ActorPrincipalId
            && exceptions[0].OwnerWorkerId == result.ActorWorkerId
            && exceptions[0].Reason == "Review the original invoice and authoritative source evidence."
            && exceptions[0].DiscrepancyKey == ProcurementEvidenceEncoding.PolicyJsonDigest(
                JsonSerializer.Serialize(new { schemaVersion = 3, invoiceId = invoice.Id,
                    invoiceLineId = (Guid?)null, code = Discrepancy }, Json)));
        var expectedResponse = JsonSerializer.Serialize(new
        {
            invoiceId = invoice.Id, commandKey = key, commandPermission = "procurement-invoice-match",
            commandSchemaVersion = 3, sourceSchemaVersion = 3, recordedHistoricalResult = true,
            isCurrentApproval = false, canPostToFinance = false,
            result = new { result.Id, result.Outcome, invoice.Version, invoice.MatchingState,
                hasCapacityClaim = false, canPostToFinance = false,
                discrepancies = new[] { new { invoiceLineId = (Guid?)null, code = Discrepancy } },
                sourceBlockers = Array.Empty<string>() }
        }, Json);
        Need(receipt.ResponseJson == expectedResponse);
        var compareMarker = JsonSerializer.Serialize(new { schemaVersion = 3, sourceSchemaVersion = 3,
            mappingVersionId = mapping.Id, resultId = result.Id, commandKey = key,
            outcome = result.Outcome, hasCapacityClaim = false, canPostToFinance = false }, Json);
        var outboxMarker = JsonSerializer.Serialize(new { schemaVersion = 3, sourceSchemaVersion = 3,
            invoiceId = invoice.Id, resultId = result.Id, outcome = result.Outcome,
            executionVersion = 2, principalEvidenceVersion = 2, canPostToFinance = false }, Json);
        Need(comparisonAudit.TenantId == result.TenantId
            && comparisonAudit.LegalEntityId == result.LegalEntityId
            && comparisonAudit.RecordType == "SupplierInvoice"
            && comparisonAudit.RecordId == invoice.Id
            && comparisonAudit.Action == "PrincipalInvoiceCompared"
            && comparisonAudit.CorrelationId == key!.Value.ToString("D")
            && comparisonAudit.ActorSubjectId == result.ActorPrincipalId
            && ProcurementEvidenceEncoding.PolicyJsonDigest(comparisonAudit.ChangedFieldsJson!)
                == ProcurementEvidenceEncoding.PolicyJsonDigest(compareMarker)
            && resultCreateAudit.TenantId == result.TenantId
            && resultCreateAudit.LegalEntityId == result.LegalEntityId
            && resultCreateAudit.RecordType == "InvoiceMatchResult"
            && resultCreateAudit.RecordId == result.Id
            && resultCreateAudit.Action == "create"
            && resultCreateAudit.ActorSubjectId == result.ActorPrincipalId
            && resultCreateAudit.ChangedFieldsJson is { Length: > 0 }
            && comparedOutbox.TenantId == result.TenantId
            && comparedOutbox.LegalEntityId == result.LegalEntityId
            && comparedOutbox.AggregateType == "SupplierInvoice"
            && comparedOutbox.AggregateId == invoice.Id
            && comparedOutbox.EventType == "supplier_invoice.compared.v3"
            && comparedOutbox.EventVersion == 3
            && comparedOutbox.IdempotencyKey == key.Value.ToString("D")
            && ProcurementEvidenceEncoding.PolicyJsonDigest(comparedOutbox.PayloadJson)
                == ProcurementEvidenceEncoding.PolicyJsonDigest(outboxMarker));
        try
        {
            using var created = JsonDocument.Parse(resultCreateAudit.ChangedFieldsJson!);
            var root = created.RootElement;
            var fields = root.GetProperty("fields").EnumerateArray().Select(x => x.GetString()).ToArray();
            Need(root.EnumerateObject().Count() == 3
                && root.GetProperty("principalEvidenceKind").GetString() == "InvoiceMatchExecution"
                && root.GetProperty("principalEvidenceVersion").GetInt32() == 2
                && fields.Order(StringComparer.Ordinal).SequenceEqual(CreatedFields.Order(StringComparer.Ordinal)));
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException
            or InvalidOperationException)
        { throw new InvalidOperationException("PrincipalInvoiceBlockedNoClaimUnproven", error); }
    }
}
