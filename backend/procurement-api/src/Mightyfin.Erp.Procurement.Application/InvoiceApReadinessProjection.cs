using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceApBlocker(string Code, string Message);
public sealed record InvoiceApNextStep(string Code, string Message);
public sealed record InvoiceApEvidence(bool MatchingSchemaAvailable, string MappingState, Guid? MappingVersionId,
    int? MappingRevision, bool? MappingOriginalCurrent, Guid? ComparisonId, long? ComparisonInvoiceVersion,
    bool? ComparisonOriginalCurrent, InvoiceMatchSummary? RecordedComparison, int? UnresolvedExceptionCount,
    string ComparisonCurrentness = "NotRevalidated");
public sealed record InvoiceApReadiness(Guid InvoiceId, long InvoiceVersion, string InvoiceStatus,
    string State, bool CanPrepareHandoff, InvoiceApEvidence Evidence,
    IReadOnlyList<InvoiceApBlocker> Blockers, IReadOnlyList<InvoiceApNextStep> NextSteps);

/// <summary>Explanatory read facts only. Never runs matching, authenticates Finance or grants AP authority.</summary>
public static class InvoiceApReadinessProjection
{
    public static InvoiceApReadiness Build(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> lines,
        bool matchingSchemaAvailable, Guid? currentMappingId, Guid? lastResultId,
        InvoiceMatchMappingVersion? mapping, InvoiceMatchResult? result, int? unresolvedExceptions)
    {
        lines = lines.Where(line => line.IsActive).ToArray();
        var blockers = new List<InvoiceApBlocker>(); var steps = new List<InvoiceApNextStep>();
        void Block(string code, string message) => blockers.Add(new(code, message));
        string mappingState = "Unavailable"; bool? mappingCurrent = null; bool? comparisonCurrent = null;
        InvoiceMatchSummary? summary = null;
        if (mapping is not null && (mapping.Id != currentMappingId || mapping.SupplierInvoiceId != invoice.Id
            || mapping.TenantId != invoice.TenantId || mapping.LegalEntityId != invoice.LegalEntityId)) mapping = null;
        if (result is not null && (result.Id != lastResultId || result.SupplierInvoiceId != invoice.Id
            || result.TenantId != invoice.TenantId || result.LegalEntityId != invoice.LegalEntityId)) result = null;
        var digest = InvoiceMappingEvidence.OriginalDigest(invoice, lines);
        if (lines.Count == 0) Block("InvoiceLinesMissing", "Review the original invoice lines.");
        if (!matchingSchemaAvailable)
        {
            Block("MatchingEvidenceUnavailable", "Invoice comparison records are not available in this workspace yet.");
            steps.Add(new("ReviewInvoiceDocuments", "Check the invoice and its original documents while comparison is unavailable."));
        }
        else
        {
            if (currentMappingId is null)
            { mappingState = "NotMapped"; Block("InvoiceMappingMissing", "Link the original invoice lines to their order and receipt evidence."); }
            else if (mapping is null || mapping.Id != currentMappingId || mapping.SupplierInvoiceId != invoice.Id
                || mapping.TenantId != invoice.TenantId || mapping.LegalEntityId != invoice.LegalEntityId)
            { mappingState = "Invalid"; Block("InvoiceMappingEvidenceInvalid", "The recorded line mapping needs review."); }
            else
            {
                mappingCurrent = mapping.InvoiceOriginalSha256 == digest;
                try
                {
                    using var mappingJson = JsonDocument.Parse(mapping.MappingJson);
                    if (!mappingJson.RootElement.TryGetProperty("lines", out var mappedLines)
                        || mappedLines.ValueKind != JsonValueKind.Array
                        || mappedLines.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.Object))
                        throw new InvalidOperationException("InvoiceMappingEvidenceChanged");
                    _ = InvoiceMappingReviewSourceDispatch.Verify(mapping, invoice, lines);
                    mappingState = mapping.VerificationState == "Verified" ? "RecordedVerified" : "ReviewRequired";
                    if (mapping.VerificationState != "Verified")
                        Block("InvoiceMappingReviewRequired", "The original document lines and units still need verified review.");
                }
                catch (Exception error) when (error is InvalidOperationException or JsonException or ArgumentException or KeyNotFoundException)
                { mappingState = "Invalid"; Block(mappingCurrent == false ? "InvoiceMappingOriginalChanged" : "InvoiceMappingEvidenceInvalid", "The mapping no longer proves the current original invoice. Review it again."); }
            }
            if (lastResultId is null) Block("InvoiceNotCompared", "No invoice comparison has been recorded.");
            else if (result is null || result.Id != lastResultId || result.SupplierInvoiceId != invoice.Id
                || result.TenantId != invoice.TenantId || result.LegalEntityId != invoice.LegalEntityId
                || result.MappingVersionId != currentMappingId || mapping is null
                || result.PurchaseOrderId != mapping.PurchaseOrderId || result.IssuedVersionId != mapping.IssuedVersionId)
                Block("InvoiceComparisonBindingChanged", "The recorded comparison does not belong to the current line mapping.");
            else
            {
                comparisonCurrent = result.InvoiceOriginalSha256 == digest;
                if (!comparisonCurrent.Value) Block("InvoiceComparisonOriginalChanged", "The original invoice has changed since comparison.");
                try
                {
                    summary = InvoiceMatchReadProjection.Summary(result, lines.Select(x => x.Id).ToHashSet());
                    if (result.Outcome != "Compared") Block("InvoiceComparisonNeedsReview", "Review the recorded comparison issues before invoice approval.");
                }
                catch (Exception error) when (error is InvalidOperationException or JsonException or ArgumentException or KeyNotFoundException or FormatException)
                { Block("InvoiceComparisonEvidenceInvalid", "The recorded comparison evidence cannot be verified for display."); }
            }
            if (unresolvedExceptions is > 0) Block("InvoiceExceptionsUnresolved", "Resolve the invoice's outstanding review issues.");
            steps.Add(new(mappingState != "RecordedVerified" ? "ReviewInvoiceMapping" : "ReviewInvoiceComparison",
                mappingState != "RecordedVerified" ? "Review the invoice line links and original documents." : "Review the recorded comparison and outstanding issues."));
        }
        // These capabilities do not exist yet; neither a status string nor a historical comparison supplies them.
        Block("InvoiceValidationAuthorityUnavailable", "Invoice approval and accounting validation are not connected yet.");
        Block("FinanceReceiverUnavailable", "A Finance receiver is not connected for supplier invoices.");
        steps.Add(new("AwaitInvoiceApprovalAndFinanceConnection", "Invoice approval and a Finance connection are required before this invoice can be sent."));
        return new(invoice.Id, invoice.Version, invoice.Status, "Blocked", false,
            new(matchingSchemaAvailable, mappingState, mapping?.Id, mapping?.MappingRevision, mappingCurrent,
                result?.Id, result?.InvoiceVersion, comparisonCurrent, summary, matchingSchemaAvailable ? unresolvedExceptions : null),
            blockers, steps);
    }
}
