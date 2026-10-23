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

    [Fact]
    public void Retained_claim_amount_must_match_frozen_comparison_and_original_invoice()
    {
        var (invoice, original, issued, input) = Fixture();
        var now = DateTimeOffset.UtcNow;
        var mapping = PrincipalInvoiceMappingEvidence.Create(invoice, [original], issued, [],
            new Dictionary<Guid, Guid>(), [input], 1, Guid.NewGuid().ToString("D"), null, now);
        var row = Row(mapping, input);
        var policy = new PolicyVersion { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            PolicyKey = InvoiceMatchingPolicyRules.PolicyKey, PolicyVersionNumber = 1,
            State = "Published", CreatedBy = Guid.NewGuid().ToString("D"),
            PublishedBy = Guid.NewGuid().ToString("D"), PublicationReason = "test",
            PublishedAt = now.AddDays(-1), EffectiveFrom = now.AddDays(-1),
            RulesJson = "{\"schemaVersion\":1,\"transactionType\":\"InvoiceMatching\",\"currencyCode\":\"ZMW\",\"moneyScale\":2,\"rounding\":\"ToEven\",\"rules\":[{\"categoryCode\":\"GOODS\",\"method\":\"TwoWay\",\"acceptanceKind\":null,\"quantity\":{\"absolute\":\"0\",\"percent\":\"0\",\"combination\":\"AllLimits\"},\"unitPrice\":{\"absolute\":\"0\",\"percent\":\"0\",\"combination\":\"AllLimits\"},\"tax\":{\"absolute\":\"0\",\"percent\":\"0\",\"combination\":\"AllLimits\"},\"total\":{\"absolute\":\"0\",\"percent\":\"0\",\"combination\":\"AllLimits\"}}]}",
        };
        var exposure = new PrincipalInvoiceExposureState { TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, BranchId = invoice.BranchRef!.Value,
            PurchaseOrderId = issued.Id, PrincipalIssuedVersionId = issued.RevisionId,
            SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode, Sequence = 1 };
        var reviewSourceId = Guid.NewGuid();
        var outcome = new PrincipalInvoiceUnitReviewOutcome { ReviewSourceId = reviewSourceId,
            State = "Verified" };
        var frozen = new PrincipalInvoiceFrozenComparison(2, invoice.Id, mapping.Id, outcome.Id,
            issued.RevisionId, policy.Id, ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            "prior-owner", new string('a', 64), "prior-consumption", "duplicates",
            [new(original.Id, issued.Lines[0].Id, null, InvoiceMatchingMethod.TwoWay, "1", "1", "0")], null);
        var evidence = System.Text.Json.JsonSerializer.Serialize(frozen,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var comparison = new PrincipalInvoiceComparison { TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, SupplierInvoiceId = invoice.Id,
            MappingVersionId = mapping.Id, PurchaseOrderId = issued.Id,
            PrincipalIssuedVersionId = issued.RevisionId, ReviewSourceId = reviewSourceId,
            ReviewOutcomeId = outcome.Id, MatchingPolicyId = policy.Id, ExposureStateId = exposure.Id,
            InvoiceVersion = invoice.Version, OriginalInvoiceSha256 = mapping.OriginalInvoiceSha256,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            Outcome = "Compared", EvidenceJson = evidence,
            EvidenceSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(evidence),
            OwnerReference = "prior-owner", OwnerWatermark = new string('a', 64) };
        var state = new PrincipalInvoiceState { TenantId = invoice.TenantId,
            LegalEntityId = invoice.LegalEntityId, SupplierInvoiceId = invoice.Id,
            BranchId = invoice.BranchRef.Value, SupplierId = invoice.SupplierId,
            CurrencyCode = invoice.CurrencyCode, CurrentMappingId = mapping.Id,
            LastComparisonId = comparison.Id, ActiveClaimComparisonId = comparison.Id,
            State = "Compared" };
        PrincipalInvoiceClaim Claim(decimal quantity, decimal net) => new()
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, SupplierInvoiceLineId = original.Id,
            MappingVersionId = mapping.Id, ComparisonId = comparison.Id,
            ExposureStateId = exposure.Id, PurchaseOrderId = issued.Id,
            PrincipalIssuedVersionId = issued.RevisionId,
            PurchaseOrderLineId = issued.Lines[0].Id, SupplierId = invoice.SupplierId,
            CurrencyCode = invoice.CurrencyCode, UnitCode = "EACH",
            Quantity = quantity, NetAmount = net, ExposureSequence = 1,
            OwnerCoverageReference = "prior-owner"
        };
        var context = new InvoiceExposureReadContext(invoice.TenantId, invoice.LegalEntityId,
            invoice.BranchRef, issued.Id, issued.RevisionId, invoice.SupplierId,
            invoice.CurrencyCode, new string('b', 64), now);
        PrincipalInvoiceOwnedComparison Owned(PrincipalInvoiceClaim claim) => new(comparison,
            mapping, outcome, invoice, state, policy, [original], [row], [claim]);
        Assert.True(PrincipalInvoiceExposureEvidence.Reconstruct(context, exposure,
            [Owned(Claim(1, 1))], 1, 0, 0, 0, 0, now).Owner.Complete);
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceExposureEvidence.Reconstruct(
            context, exposure, [Owned(Claim(.5m, .5m))], 1, 0, 0, 0, 0, now));
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
