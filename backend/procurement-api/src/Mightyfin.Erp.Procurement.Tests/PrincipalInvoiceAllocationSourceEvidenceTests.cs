using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceAllocationSourceEvidenceTests
{
    [Fact]
    public void Three_way_claim_requires_exact_fact_source_line_and_cumulative_capacity()
    {
        var f = Fixture();
        PrincipalInvoiceAllocationSourceEvidence.VerifySet([f.Input], [f.History]);
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], []));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History, f.History]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input with { Allocation = Claim(f.Input.Allocation, fact: Guid.NewGuid()) }], [f.History]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input with { Allocation = Claim(f.Input.Allocation, source: Guid.NewGuid()) }], [f.History]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input with { Allocation = Claim(f.Input.Allocation, legacy: Guid.NewGuid()) }], [f.History]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History with { Facts = [f.History.Facts.Single() with
                { PurchaseOrderLineId = Guid.NewGuid() }] }]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input with { Allocation = Claim(f.Input.Allocation, unit: "BOX") }], [f.History]));
        Assert.Equal("PrincipalInvoiceUnitConversionReviewRequired", Assert.Throws<InvalidOperationException>(() =>
            PrincipalInvoiceAllocationSourceEvidence.VerifySet([f.Input],
                [f.History with { UnitCode = "BOX" }])).Message);
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History with { SupplierId = Guid.NewGuid() }]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History with { BranchId = Guid.NewGuid() }]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History with { CurrencyCode = "USD" }]));
        f.Input.Invoice.ActiveMatchClaimResultId = null;
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input], [f.History]));
        f.Input.Invoice.ActiveMatchClaimResultId = f.Input.Result.Id;
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input, AdditionalClaim(f.Input, .6m)], [f.History]));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.VerifySet(
            [f.Input, f.Input], [f.History]));
    }

    [Fact]
    public void Two_way_claim_has_no_principal_fact_and_legacy_identity_stays_separate()
    {
        var f = Fixture(physical: false);
        Assert.Null(PrincipalInvoiceAllocationSourceEvidence.Verify(f.Input, []));
        PrincipalInvoiceAllocationSourceEvidence.VerifySet([f.Input], []);
        Assert.Null(f.Input.Allocation.PrincipalReceivingFactId);
        Assert.Null(f.Input.Allocation.PrincipalReceivingSourceStateId);
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceAllocationSourceEvidence.Verify(
            f.Input with { Allocation = Claim(f.Input.Allocation, fact: Guid.NewGuid()) }, []));
    }

    private static (PrincipalInvoiceAllocationSourceInput Input,
        PrincipalInvoiceAcceptedHistory History) Fixture(bool physical = true)
    {
        var invoice = new SupplierInvoice
        {
            TenantId = "m6-fact-test", LegalEntityId = Guid.NewGuid(),
            BranchRef = Guid.NewGuid(), SupplierId = Guid.NewGuid(),
            InvoiceNumber = "INV-1", NormalizedInvoiceNumber = "INV1",
            CurrencyCode = "ZMW", IntakeChannel = "Manual",
            InvoiceDate = new DateOnly(2026, 10, 28), NetAmount = 1m,
            GrossAmount = 1m
        };
        var original = new SupplierInvoiceLine
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, LineNumber = 1,
            Description = "Original", Quantity = 1m, UnitPrice = 1m
        };
        var order = Guid.NewGuid(); var issued = Guid.NewGuid();
        var orderLine = Guid.NewGuid(); var source = Guid.NewGuid();
        var fact = Guid.NewGuid(); var actor = Guid.NewGuid().ToString("D");
        var link = new PrincipalInvoiceMappingLine(original.Id, orderLine,
            physical ? source : null, physical ? fact : null,
            "EACH", Guid.NewGuid(), "original line");
        var payload = PrincipalInvoiceMappingEvidence.Create(invoice, [original],
            order, issued, [link], actor, null);
        var mapping = new InvoiceMatchMappingVersion
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, PurchaseOrderId = order,
            IssuedVersionId = issued, MappingRevision = 1,
            InvoiceOriginalSha256 = payload.Snapshot.InvoiceOriginalSha256,
            MappingJson = payload.Json, MappingSha256 = payload.Sha256,
            ActorPrincipalId = actor, IdentitySchemaVersion = 2,
            SourceSchemaVersion = 3
        };
        var result = new InvoiceMatchResult
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, MappingVersionId = mapping.Id,
            PurchaseOrderId = order, IssuedVersionId = issued,
            Outcome = "Compared", ExecutionVersion = 2,
            InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256
        };
        invoice.CurrentMappingVersionId = mapping.Id;
        invoice.ActiveMatchClaimResultId = result.Id;
        var claim = new InvoiceMatchAllocation
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, SupplierInvoiceLineId = original.Id,
            MatchResultId = result.Id, ExposureStateId = Guid.NewGuid(),
            PurchaseOrderId = order, IssuedVersionId = issued,
            PurchaseOrderLineId = orderLine, SupplierId = invoice.SupplierId,
            CurrencyCode = invoice.CurrencyCode, UnitCode = "EACH",
            Quantity = .5m, NetAmount = .5m,
            PrincipalReceivingSourceStateId = physical ? source : null,
            PrincipalReceivingFactId = physical ? fact : null
        };
        var accepted = new PrincipalInvoiceAcceptedFact(fact, source, 1,
            order, issued, orderLine, ReceivingKind.Goods, 1m, 1m,
            new string('a', 64), DateTimeOffset.UtcNow);
        return (new(claim, result, mapping, invoice, [original]),
            new PrincipalInvoiceAcceptedHistory(source, new string('b', 64), 1, "EACH", [accepted])
            { SupplierId = invoice.SupplierId, BranchId = invoice.BranchRef!.Value,
                CurrencyCode = invoice.CurrencyCode });
    }

    private static InvoiceMatchAllocation Claim(InvoiceMatchAllocation original,
        Guid? id = null, Guid? fact = null, Guid? source = null, Guid? legacy = null,
        decimal? quantity = null, decimal? net = null, string? unit = null) => new()
    {
        Id = id ?? original.Id, TenantId = original.TenantId,
        LegalEntityId = original.LegalEntityId,
        SupplierInvoiceId = original.SupplierInvoiceId,
        SupplierInvoiceLineId = original.SupplierInvoiceLineId,
        MatchResultId = original.MatchResultId,
        ExposureStateId = original.ExposureStateId,
        PurchaseOrderId = original.PurchaseOrderId,
        IssuedVersionId = original.IssuedVersionId,
        PurchaseOrderLineId = original.PurchaseOrderLineId,
        SupplierId = original.SupplierId, CurrencyCode = original.CurrencyCode,
        UnitCode = unit ?? original.UnitCode, Quantity = quantity ?? original.Quantity,
        NetAmount = net ?? original.NetAmount,
        PrincipalReceivingSourceStateId = source ?? original.PrincipalReceivingSourceStateId,
        PrincipalReceivingFactId = fact ?? original.PrincipalReceivingFactId,
        GoodsReceiptLineId = legacy
    };

    private static PrincipalInvoiceAllocationSourceInput AdditionalClaim(
        PrincipalInvoiceAllocationSourceInput first, decimal amount)
    {
        var prior = first.Invoice;
        var invoice = new SupplierInvoice
        {
            TenantId = prior.TenantId, LegalEntityId = prior.LegalEntityId,
            BranchRef = prior.BranchRef, SupplierId = prior.SupplierId,
            InvoiceNumber = "INV-2", NormalizedInvoiceNumber = "INV2",
            CurrencyCode = prior.CurrencyCode, IntakeChannel = "Manual",
            InvoiceDate = prior.InvoiceDate, NetAmount = amount, GrossAmount = amount
        };
        var line = new SupplierInvoiceLine
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, LineNumber = 1,
            Description = "Second original", Quantity = amount, UnitPrice = 1m
        };
        var accepted = first.Allocation;
        var actor = Guid.NewGuid().ToString("D");
        var payload = PrincipalInvoiceMappingEvidence.Create(invoice, [line],
            accepted.PurchaseOrderId, accepted.IssuedVersionId,
            [new(line.Id, accepted.PurchaseOrderLineId,
                accepted.PrincipalReceivingSourceStateId, accepted.PrincipalReceivingFactId,
                "EACH", Guid.NewGuid(), "second line")], actor, null);
        var mapping = new InvoiceMatchMappingVersion
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, PurchaseOrderId = accepted.PurchaseOrderId,
            IssuedVersionId = accepted.IssuedVersionId, MappingRevision = 1,
            InvoiceOriginalSha256 = payload.Snapshot.InvoiceOriginalSha256,
            MappingJson = payload.Json, MappingSha256 = payload.Sha256,
            ActorPrincipalId = actor, IdentitySchemaVersion = 2, SourceSchemaVersion = 3
        };
        var result = new InvoiceMatchResult
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, MappingVersionId = mapping.Id,
            PurchaseOrderId = accepted.PurchaseOrderId,
            IssuedVersionId = accepted.IssuedVersionId,
            Outcome = "Compared", ExecutionVersion = 2,
            InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256
        };
        invoice.CurrentMappingVersionId = mapping.Id;
        invoice.ActiveMatchClaimResultId = result.Id;
        var allocation = new InvoiceMatchAllocation
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, SupplierInvoiceLineId = line.Id,
            MatchResultId = result.Id, ExposureStateId = accepted.ExposureStateId,
            PurchaseOrderId = accepted.PurchaseOrderId,
            IssuedVersionId = accepted.IssuedVersionId,
            PurchaseOrderLineId = accepted.PurchaseOrderLineId,
            SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode,
            UnitCode = "EACH", Quantity = amount, NetAmount = amount,
            PrincipalReceivingSourceStateId = accepted.PrincipalReceivingSourceStateId,
            PrincipalReceivingFactId = accepted.PrincipalReceivingFactId
        };
        return new(allocation, result, mapping, invoice, [line]);
    }
}
