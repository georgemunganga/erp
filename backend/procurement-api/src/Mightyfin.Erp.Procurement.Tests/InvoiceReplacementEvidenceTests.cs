using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceReplacementEvidenceTests
{
    private const string Tenant = "replacement-private";
    private static readonly string Actor = Guid.Parse("11111111-1111-4111-8111-111111111111").ToString("D");
    private static readonly DateTimeOffset At = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed record Graph(InvoiceReplacementLink Link, InvoiceReplacementCommandReceipt Receipt,
        SupplierInvoice Source, SupplierInvoice Successor, InvoiceMatchMappingVersion Mapping,
        InvoiceMatchResult Result, InvoiceUnitReviewSource Review, InvoiceUnitReviewOutcome Outcome,
        InvoiceAmountCalculation Calculation, InvoiceIntakeCommandReceipt Capture)
    {
        internal void Verify() => InvoiceReplacementEvidence.Verify(Link, Receipt, Source, Successor,
            Mapping, Result, Review, Outcome, Calculation, Capture);
    }

    private static Graph Valid(string? successorNumber = null, Guid? captureKey = null)
    {
        var company = Guid.NewGuid(); var supplier = Guid.NewGuid(); var branch = Guid.NewGuid();
        var order = Guid.NewGuid(); var issued = Guid.NewGuid(); var mappingId = Guid.NewGuid();
        var resultId = Guid.NewGuid(); var reviewId = Guid.NewGuid(); var outcomeId = Guid.NewGuid();
        var calculationId = Guid.NewGuid(); var captureId = Guid.NewGuid(); var key = Guid.NewGuid();
        var source = new SupplierInvoice { TenantId = Tenant, LegalEntityId = company, CreatedBy = Actor,
            Version = 4, SupplierId = supplier, BranchRef = branch, CurrencyCode = "ZMW",
            InvoiceNumber = "INV-OLD", NormalizedInvoiceNumber = "INV-OLD", Status = "Received",
            CurrentMappingVersionId = mappingId, MappingRevision = 2, LastMatchResultId = resultId,
            MatchingState = "ComparisonBlocked" };
        var successor = new SupplierInvoice { TenantId = Tenant, LegalEntityId = company, CreatedBy = Actor,
            SupplierId = supplier, BranchRef = branch, CurrencyCode = "ZMW", PurchaseOrderId = order,
            InvoiceNumber = successorNumber ?? "INV-CORRECTED", NormalizedInvoiceNumber = successorNumber ?? "INV-CORRECTED",
            Status = "Received" };
        var originalSha = new string('a', 64);
        var mapping = new InvoiceMatchMappingVersion { Id = mappingId, TenantId = Tenant, LegalEntityId = company,
            SupplierInvoiceId = source.Id, PurchaseOrderId = order, IssuedVersionId = issued,
            MappingRevision = 2, SourceSchemaVersion = 3, InvoiceOriginalSha256 = originalSha };
        var result = new InvoiceMatchResult { Id = resultId, TenantId = Tenant, LegalEntityId = company,
            SupplierInvoiceId = source.Id, MappingVersionId = mappingId, PurchaseOrderId = order,
            IssuedVersionId = issued, SourceSchemaVersion = 3, ExecutionVersion = 2, Outcome = "Blocked" };
        var review = new InvoiceUnitReviewSource { Id = reviewId, TenantId = Tenant, LegalEntityId = company,
            SupplierInvoiceId = source.Id, MappingVersionId = mappingId, PurchaseOrderId = order };
        var outcome = new InvoiceUnitReviewOutcome { Id = outcomeId, TenantId = Tenant, LegalEntityId = company,
            ReviewSourceId = reviewId, State = "Verified" };
        var calculation = new InvoiceAmountCalculation { Id = calculationId, TenantId = Tenant,
            LegalEntityId = company, SupplierInvoiceId = successor.Id };
        var internalKey = captureKey ?? InvoiceReplacementEvidence.CaptureCommandKey(Tenant, company, source.Id, key);
        var capture = new InvoiceIntakeCommandReceipt { Id = captureId, TenantId = Tenant,
            LegalEntityId = company, SupplierInvoiceId = successor.Id, CalculationId = calculationId,
            CommandKey = internalKey, ActorPrincipalId = Actor };
        var link = new InvoiceReplacementLink { TenantId = Tenant, LegalEntityId = company,
            SourceInvoiceId = source.Id, SuccessorInvoiceId = successor.Id, SourceInvoiceVersion = source.Version,
            SourceOriginalSha256 = originalSha, SourceMappingVersionId = mappingId,
            SourceMappingRevision = 2, BlockedResultId = resultId, ReviewSourceId = reviewId,
            ReviewOutcomeId = outcomeId, NoClaimProofSha256 = new string('b', 64), SupplierId = supplier,
            CurrencyCode = "ZMW", PurchaseOrderId = order, IssuedVersionId = issued, BranchId = branch,
            CalculationId = calculationId, CaptureReceiptId = captureId, CaptureCommandKey = internalKey,
            MakerPrincipalId = Actor, Reason = "Supplier issued a corrected invoice", CommandKey = key,
            RecordedAtUtc = At, CreatedAt = At, CreatedBy = Actor };
        var receipt = InvoiceReplacementEvidence.CreateReceipt(link, successor, new string('c', 64), At);
        return new(link, receipt, source, successor, mapping, result, review, outcome, calculation, capture);
    }

    [Fact]
    public void Distinct_supplier_issued_successor_and_scoped_graph_are_accepted()
    {
        var graph = Valid(); graph.Verify();
        Assert.Equal('5', graph.Link.CaptureCommandKey.ToString("D")[14]);
        Assert.NotEqual(graph.Link.CommandKey, graph.Link.CaptureCommandKey);
        Assert.Equal(InvoiceReplacementEvidence.ResponseJson(graph.Link, graph.Successor), graph.Receipt.ResponseJson);
    }

    [Fact]
    public void Reusing_the_original_supplier_number_is_rejected()
        => Assert.Throws<InvalidOperationException>(() => Valid("INV-OLD").Verify());

    [Fact]
    public void An_unrelated_capture_key_cannot_impersonate_the_derived_subcommand()
        => Assert.Throws<InvalidOperationException>(() => Valid(captureKey: Guid.NewGuid()).Verify());

    [Fact]
    public void Internal_capture_key_is_stable_for_original_recovery_and_scoped_to_source()
    {
        var company = Guid.NewGuid(); var source = Guid.NewGuid(); var key = Guid.NewGuid();
        var first = InvoiceReplacementEvidence.CaptureCommandKey(Tenant, company, source, key);
        Assert.Equal(first, InvoiceReplacementEvidence.CaptureCommandKey(Tenant, company, source, key));
        Assert.NotEqual(first, InvoiceReplacementEvidence.CaptureCommandKey(Tenant, company, Guid.NewGuid(), key));
        Assert.NotEqual(first, InvoiceReplacementEvidence.CaptureCommandKey(Tenant, company, source, Guid.NewGuid()));
    }

    [Fact]
    public void Different_header_purchase_order_or_supplier_cannot_be_hidden_by_a_valid_link()
    {
        var wrongOrder = Valid(); wrongOrder.Source.PurchaseOrderId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(wrongOrder.Verify);
        var wrongSupplier = Valid(); wrongSupplier.Successor.SupplierId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(wrongSupplier.Verify);
    }

    [Fact]
    public void Later_source_version_change_cannot_reuse_the_original_link_proof()
    {
        var graph = Valid(); graph.Source.Version++;
        Assert.Throws<InvalidOperationException>(graph.Verify);
    }

    [Fact]
    public void A_rehashed_but_different_replacement_response_is_not_the_original_receipt()
    {
        var graph = Valid();
        var tampered = new InvoiceReplacementCommandReceipt { TenantId = graph.Receipt.TenantId,
            LegalEntityId = graph.Receipt.LegalEntityId, LinkId = graph.Link.Id,
            SourceInvoiceId = graph.Source.Id, SuccessorInvoiceId = graph.Successor.Id,
            ActorPrincipalId = Actor, CommandKey = graph.Link.CommandKey,
            RequestSha256 = graph.Receipt.RequestSha256,
            ResponseJson = graph.Receipt.ResponseJson.Replace("INV-CORRECTED", "INV-FORGED", StringComparison.Ordinal),
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(graph.Receipt.ResponseJson.Replace(
                "INV-CORRECTED", "INV-FORGED", StringComparison.Ordinal)),
            CompletedAtUtc = At, CreatedAt = At, CreatedBy = Actor };
        Assert.Throws<InvalidOperationException>(() => InvoiceReplacementEvidence.Verify(graph.Link, tampered,
            graph.Source, graph.Successor, graph.Mapping, graph.Result, graph.Review, graph.Outcome,
            graph.Calculation, graph.Capture));
    }
}
