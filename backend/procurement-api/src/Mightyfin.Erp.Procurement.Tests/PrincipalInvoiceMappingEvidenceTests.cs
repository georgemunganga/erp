using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceMappingEvidenceTests
{
    [Fact]
    public void Typed_mapping_requires_exact_normalized_membership_and_original_invoice()
    {
        var (invoice, original, issued, input) = Fixture();
        var mapping = PrincipalInvoiceMappingEvidence.Create(invoice, [original], issued, [],
            new Dictionary<Guid, Guid>(), [input], 1, Guid.NewGuid().ToString("D"), null, DateTimeOffset.UtcNow);
        var normalized = Row(mapping, input);
        Assert.Equal(2, PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, [original], issued,
            [], new Dictionary<Guid, Guid>(), [normalized]).SchemaVersion);
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceMappingEvidence.VerifyRows(mapping,
            [Row(mapping, input with { DocumentLineReference = "different line" })]));
        original.UnitPrice += .0001m;
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceMappingEvidence.Verify(mapping,
            invoice, [original], issued, [], new Dictionary<Guid, Guid>(), [normalized]));
    }

    [Fact]
    public void Acceptance_fact_cannot_be_assigned_to_an_unrelated_source_state()
    {
        var (invoice, original, issued, input) = Fixture();
        var fact = new InvoiceAcceptedFulfilment(Guid.NewGuid(), 1, invoice.TenantId,
            invoice.LegalEntityId, invoice.BranchRef, issued.Id, issued.RevisionId,
            issued.Lines[0].Id, invoice.CurrencyCode, "EACH", InvoiceAcceptanceKind.Goods,
            "Accepted", new string('b', 64), DateTimeOffset.UtcNow, false, 1, 0, 1, 0);
        var declared = input with { PrincipalReceivingSourceStateId = Guid.NewGuid(),
            PrincipalAcceptanceFactId = fact.Id, AcceptanceSequence = 1 };
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceMappingEvidence.Create(
            invoice, [original], issued, [fact], new Dictionary<Guid, Guid> { [fact.Id] = Guid.NewGuid() },
            [declared], 1, Guid.NewGuid().ToString("D"), null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_source_and_declared_units_do_not_create_capacity_without_owner_history()
    {
        var (invoice, original, issued, input) = Fixture();
        var mapping = PrincipalInvoiceMappingEvidence.Create(invoice, [original], issued, [],
            new Dictionary<Guid, Guid>(), [input], 1, Guid.NewGuid().ToString("D"), null, DateTimeOffset.UtcNow);
        var snapshot = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, [original], issued,
            [], new Dictionary<Guid, Guid>(), [Row(mapping, input)]);
        var state = new PrincipalInvoiceState { TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, SupplierInvoiceId = invoice.Id,
            BranchId = invoice.BranchRef!.Value, SupplierId = invoice.SupplierId,
            CurrencyCode = invoice.CurrencyCode, InvoiceVersion = invoice.Version,
            MappingRevision = 1, CurrentMappingId = mapping.Id, State = "Reviewed" };
        var now = DateTimeOffset.UtcNow;
        var zero = new InvoiceFieldTolerance(0, 0, InvoiceToleranceCombination.AllLimits);
        var policy = new InvoiceMatchingPolicy(Guid.NewGuid(), 1, invoice.TenantId, invoice.LegalEntityId,
            invoice.BranchRef, "Published", "independent-approval", now.AddDays(-1), now.AddDays(1), 2,
            InvoiceMoneyRounding.ToEven,
            [new InvoiceCategoryMatchRule("GOODS", "ZMW", InvoiceMatchingMethod.TwoWay,
                null, zero, zero, zero, zero)]);
        var context = new InvoiceExposureReadContext(invoice.TenantId, invoice.LegalEntityId,
            invoice.BranchRef, issued.Id, issued.RevisionId, invoice.SupplierId,
            invoice.CurrencyCode, new string('a', 64), now);
        var duplicates = new InvoiceDuplicateEvidence("current", true,
            [new InvoiceDuplicateIdentity(invoice.Id, invoice.TenantId, invoice.LegalEntityId,
                invoice.SupplierId, invoice.NormalizedInvoiceNumber, invoice.CurrencyCode)]);
        var result = PrincipalInvoiceComparisonRules.Evaluate(invoice, [original], state, mapping,
            snapshot, issued,
            [], new Dictionary<Guid, Guid>(), new Dictionary<Guid, string> { [original.Id] = "EACH" },
            policy, InvoiceHistoricalExposure.Unavailable(), context,
            new InvoiceConsumptionEvidence("OwnerHistoryUnavailable", false, []), duplicates, now);
        Assert.False(result.CanClaim);
        Assert.Contains(result.Blockers, x => x.Code == "CompleteHistoricalExposureUnavailable");

        // A complete owner envelope makes the same exact two-way source
        // arithmetically comparable. This is a pure fixture, not runtime authority.
        var until = now.AddHours(1);
        var envelope = new InvoiceOwnerExposureEnvelope(1, invoice.TenantId, invoice.LegalEntityId,
            invoice.BranchRef, issued.Id, issued.RevisionId, invoice.SupplierId,
            invoice.CurrencyCode, context.PolicyRulesSha256, "owner-fixture", "owner-watermark",
            until, InvoiceMatchExecutionEvidence.AllocationsDigest([]));
        var json = System.Text.Json.JsonSerializer.Serialize(envelope,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var owner = new InvoiceHistoricalExposure(true, "Verified", "owner-fixture",
            "owner-watermark", json, ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            context.PolicyRulesSha256, until, []);
        var exposure = new PrincipalInvoiceExposureState { TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, BranchId = invoice.BranchRef!.Value,
            PurchaseOrderId = issued.Id, PrincipalIssuedVersionId = issued.RevisionId,
            SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode };
        var history = PrincipalInvoiceExposureEvidence.CompleteHistory(owner, context, exposure, []);
        var comparable = PrincipalInvoiceComparisonRules.Evaluate(invoice, [original], state,
            mapping, snapshot, issued, [], new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, string> { [original.Id] = "EACH" }, policy, owner,
            context, history, duplicates, now);
        Assert.True(comparable.CanClaim);
        Assert.Single(comparable.Lines);
    }

    private static (SupplierInvoice Invoice, SupplierInvoiceLine Line, InvoiceIssuedOrder Issued,
        PrincipalInvoiceMappingInputLine Input) Fixture()
    {
        var branch = Guid.NewGuid();
        var invoice = new SupplierInvoice { TenantId = "principal-invoice-test", LegalEntityId = Guid.NewGuid(),
            BranchRef = branch, SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-1",
            NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW", IntakeChannel = "Manual",
            InvoiceDate = new DateOnly(2026, 10, 23), NetAmount = 1, GrossAmount = 1 };
        var line = new SupplierInvoiceLine { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Original line", Quantity = 1,
            UnitPrice = 1 };
        var orderLine = new InvoiceIssuedOrderLine(Guid.NewGuid(), 1, "GOODS", "EACH", 1, 1, 0);
        var issued = new InvoiceIssuedOrder(Guid.NewGuid(), Guid.NewGuid(), 1, 1,
            invoice.TenantId, invoice.LegalEntityId, branch, invoice.SupplierId, invoice.CurrencyCode,
            "Issued", new string('a', 64), DateTimeOffset.UtcNow, [orderLine]);
        var proposal = new PrincipalInvoiceMappingInputLine(line.Id, orderLine.Id, null, null, null,
            "EACH", Guid.NewGuid(), "line 1");
        return (invoice, line, issued, proposal);
    }

    private static Mightyfin.Erp.Procurement.Domain.PrincipalInvoiceMappingLine Row(
        PrincipalInvoiceMappingVersion mapping, PrincipalInvoiceMappingInputLine input) => new()
    {
        TenantId = mapping.TenantId, LegalEntityId = mapping.LegalEntityId,
        MappingVersionId = mapping.Id, SupplierInvoiceId = mapping.SupplierInvoiceId,
        SupplierInvoiceLineId = input.InvoiceLineId, PurchaseOrderId = mapping.PurchaseOrderId,
        PrincipalIssuedVersionId = mapping.PrincipalIssuedVersionId,
        PurchaseOrderLineId = input.PurchaseOrderLineId,
        PrincipalReceivingSourceStateId = input.PrincipalReceivingSourceStateId,
        PrincipalAcceptanceFactId = input.PrincipalAcceptanceFactId,
        AcceptanceSequence = input.AcceptanceSequence, InvoiceUnitCode = input.InvoiceUnitCode,
        DocumentVersionId = input.DocumentVersionId,
        DocumentLineReference = input.DocumentLineReference
    };
}
