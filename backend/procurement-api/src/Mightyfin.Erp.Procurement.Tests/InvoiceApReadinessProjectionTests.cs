using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceApReadinessProjectionTests
{
    private static (SupplierInvoice Invoice, SupplierInvoiceLine[] Lines, InvoiceMatchMappingVersion Mapping, InvoiceMatchResult Result) Facts()
    {
        var invoice = new SupplierInvoice { TenantId = "ap-read", LegalEntityId = Guid.NewGuid(), SupplierId = Guid.NewGuid(),
            InvoiceNumber = "OWN-INVOICE", NormalizedInvoiceNumber = "OWN-INVOICE", CurrencyCode = "ZMW", NetAmount = 100,
            GrossAmount = 100, InvoiceDate = new(2026, 10, 20), Version = 8, Status = "Received" };
        var lines = new[] { new SupplierInvoiceLine { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Own line", Quantity = 1, UnitPrice = 100 } };
        var mapping = InvoiceMappingEvidence.Create(invoice, lines, Guid.NewGuid(), Guid.NewGuid(),
            [new(lines[0].Id, Guid.NewGuid(), null, null, "each", Guid.NewGuid(), "line-1")], "reviewer", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var evidence = JsonSerializer.Serialize(new { discrepancies = Array.Empty<object>(), lines = Array.Empty<object>(),
            source = new { secret = "PRIVATE-OTHER-INVOICE-ALLOCATIONS" } });
        var result = new InvoiceMatchResult { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, ExecutionVersion = 1, Outcome = "Compared", InvoiceVersion = 7,
            MappingVersionId = mapping.Id, PurchaseOrderId = mapping.PurchaseOrderId, IssuedVersionId = mapping.IssuedVersionId,
            InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256, EvidenceJson = evidence,
            EvidenceSha256 = ProcurementEvidenceEncoding.Digest(evidence), SourceWatermarksJson = "{}" };
        return (invoice, lines, mapping, result);
    }
    [Fact]
    public void Recorded_compared_result_and_even_validated_status_never_supply_AP_authority()
    {
        var f = Facts(); f.Invoice.Status = "Validated";
        var view = InvoiceApReadinessProjection.Build(f.Invoice, f.Lines, true, f.Mapping.Id, f.Result.Id, f.Mapping, f.Result, 0);
        Assert.Equal(8, view.InvoiceVersion); Assert.False(view.CanPrepareHandoff); Assert.Equal("Blocked", view.State);
        Assert.Contains(view.Blockers, x => x.Code == "InvoiceValidationAuthorityUnavailable");
        Assert.Contains(view.Blockers, x => x.Code == "FinanceReceiverUnavailable");
        Assert.Equal("Compared", view.Evidence.RecordedComparison!.Outcome);
        Assert.Equal(7, view.Evidence.ComparisonInvoiceVersion); Assert.Equal("NotRevalidated", view.Evidence.ComparisonCurrentness);
        Assert.DoesNotContain("PRIVATE-OTHER-INVOICE-ALLOCATIONS", JsonSerializer.Serialize(view));
    }
    [Fact]
    public void Unavailable_schema_is_not_an_empty_comparison_history_or_zero_exceptions()
    {
        var f = Facts();
        var view = InvoiceApReadinessProjection.Build(f.Invoice, f.Lines, false, null, null, null, null, null);
        Assert.Null(view.Evidence.UnresolvedExceptionCount); Assert.Null(view.Evidence.RecordedComparison);
        Assert.Contains(view.Blockers, x => x.Code == "MatchingEvidenceUnavailable");
        Assert.DoesNotContain(view.Blockers, x => x.Code == "InvoiceNotCompared");
    }
    [Fact]
    public void Current_mapping_and_original_digest_mismatches_are_explicit_without_recomputing_authority()
    {
        var f = Facts(); f.Invoice.InvoiceNumber = "CHANGED-ORIGINAL";
        var view = InvoiceApReadinessProjection.Build(f.Invoice, f.Lines, true, f.Mapping.Id, f.Result.Id, f.Mapping, f.Result, 2);
        Assert.Contains(view.Blockers, x => x.Code == "InvoiceMappingOriginalChanged");
        Assert.Contains(view.Blockers, x => x.Code == "InvoiceComparisonOriginalChanged");
        Assert.Contains(view.Blockers, x => x.Code == "InvoiceExceptionsUnresolved");
        Assert.False(view.Evidence.MappingOriginalCurrent); Assert.False(view.Evidence.ComparisonOriginalCurrent);
        var changed = InvoiceApReadinessProjection.Build(f.Invoice, f.Lines, true, Guid.NewGuid(), f.Result.Id, f.Mapping, f.Result, 0);
        Assert.Contains(changed.Blockers, x => x.Code == "InvoiceComparisonBindingChanged");
        Assert.False(changed.CanPrepareHandoff);
    }
    [Fact]
    public void Wrong_invoice_or_entity_evidence_is_not_exposed_and_bad_JSON_is_not_authority()
    {
        var f = Facts(); var other = new SupplierInvoice { TenantId = "other", LegalEntityId = Guid.NewGuid() };
        var view = InvoiceApReadinessProjection.Build(other, [], true, f.Mapping.Id, f.Result.Id, f.Mapping, f.Result, 0);
        Assert.Null(view.Evidence.MappingVersionId); Assert.Null(view.Evidence.ComparisonId); Assert.Null(view.Evidence.RecordedComparison);
        Assert.DoesNotContain(f.Invoice.Id.ToString(), JsonSerializer.Serialize(view));
        var corrupt = new InvoiceMatchResult { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            SupplierInvoiceId = f.Invoice.Id, MappingVersionId = f.Mapping.Id, PurchaseOrderId = f.Mapping.PurchaseOrderId,
            IssuedVersionId = f.Mapping.IssuedVersionId, ExecutionVersion = 1, Outcome = "Compared", EvidenceJson = "{}", EvidenceSha256 = "invalid" };
        view = InvoiceApReadinessProjection.Build(f.Invoice, f.Lines, true, f.Mapping.Id, corrupt.Id, f.Mapping, corrupt, 0);
        Assert.Contains(view.Blockers, x => x.Code == "InvoiceComparisonEvidenceInvalid"); Assert.Null(view.Evidence.RecordedComparison);
    }
}
