using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceLegacyBoundaryTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public async Task Existing_typed_state_rejects_legacy_mapping_in_same_save_boundary()
    {
        var company = Guid.NewGuid(); var invoiceId = Guid.NewGuid();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Scope("p31-legacy-fence", company, Guid.NewGuid().ToString("D")));
        // A retained row already loaded from the owner store is unchanged, not a
        // newly requested principal workflow command in this save.
        db.PrincipalInvoiceStates.Attach(new PrincipalInvoiceState { SupplierInvoiceId = invoiceId,
            TenantId = "p31-legacy-fence", LegalEntityId = company, BranchId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", InvoiceVersion = 1 });
        db.AddScoped(new InvoiceMatchMappingVersion { SupplierInvoiceId = invoiceId });
        Assert.Equal("PrincipalInvoiceLegacyWorkflowUnavailable",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        Assert.Empty(db.AuditEvents.Local);
        Assert.Empty(db.OutboxEvents.Local);
    }

    [Fact]
    public async Task Existing_typed_state_rejects_original_invoice_line_mutation_without_header_mutation()
    {
        var company = Guid.NewGuid(); var invoiceId = Guid.NewGuid();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope("p31-original-fence", company, Guid.NewGuid().ToString("D")));
        db.PrincipalInvoiceStates.Attach(new PrincipalInvoiceState { SupplierInvoiceId = invoiceId,
            TenantId = "p31-original-fence", LegalEntityId = company, BranchId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", InvoiceVersion = 1 });
        db.AddScoped(new SupplierInvoiceLine { SupplierInvoiceId = invoiceId, LineNumber = 1 });
        Assert.Equal("PrincipalInvoiceLegacyWorkflowUnavailable",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public void Frozen_comparison_requires_the_exact_complete_claim_line_set()
    {
        var invoiceLine = Guid.NewGuid(); var orderLine = Guid.NewGuid();
        var lines = new[] { new PrincipalInvoiceComparisonLine(invoiceLine, orderLine, null,
            InvoiceMatchingMethod.TwoWay, "1", "2.0001", "0.0001") };
        var comparison = new PrincipalInvoiceComparison { EvidenceJson = JsonSerializer.Serialize(new { lines },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var claim = new PrincipalInvoiceClaim { SupplierInvoiceLineId = invoiceLine,
            PurchaseOrderLineId = orderLine, Quantity = 1m, NetAmount = 2.0001m, TaxAmount = 0.0001m };
        Assert.True(ProcurementDbContext.ClaimBatchMatchesFrozenComparison(comparison, [claim]));
        Assert.False(ProcurementDbContext.ClaimBatchMatchesFrozenComparison(comparison, []));
        Assert.False(ProcurementDbContext.ClaimBatchMatchesFrozenComparison(comparison,
            [new PrincipalInvoiceClaim { SupplierInvoiceLineId = invoiceLine, PurchaseOrderLineId = orderLine,
                Quantity = 1m, NetAmount = 2.0002m, TaxAmount = 0.0001m }]));
        Assert.False(ProcurementDbContext.ClaimBatchMatchesFrozenComparison(comparison, [claim, claim]));
    }

    [Fact]
    public void A_new_claim_batch_must_use_the_actual_advanced_exposure_cursor()
    {
        var exposure = new PrincipalInvoiceExposureState { Id = Guid.NewGuid(), Sequence = 3 };
        var comparison = new PrincipalInvoiceComparison { Id = Guid.NewGuid(), ExposureStateId = exposure.Id };
        var claim = new PrincipalInvoiceClaim { ComparisonId = comparison.Id,
            ExposureStateId = exposure.Id, ExposureSequence = 1 };
        Assert.False(ProcurementDbContext.ClaimBatchFollowsExposure(comparison, exposure, [claim]));
        claim = new PrincipalInvoiceClaim { ComparisonId = comparison.Id,
            ExposureStateId = exposure.Id, ExposureSequence = 3 };
        Assert.True(ProcurementDbContext.ClaimBatchFollowsExposure(comparison, exposure, [claim]));
        Assert.False(ProcurementDbContext.ClaimBatchFollowsExposure(comparison, exposure,
            [claim, new PrincipalInvoiceClaim { ComparisonId = comparison.Id,
                ExposureStateId = exposure.Id, ExposureSequence = 2 }]));
    }
}
