using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementOwnerLeaseEvidenceTests
{
    [Fact]
    public void Clean_zero_and_live_local_partitions_have_distinct_bound_digests()
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var order = Guid.NewGuid(); var issued = Guid.NewGuid(); var supplier = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var context = new InvoiceExposureReadContext("m6-lease", company, branch,
            order, issued, supplier, "ZMW", new string('a', 64), at);
        var baseline = new InvoiceProcurementOwnerBaseline
        {
            TenantId = context.TenantId, LegalEntityId = company,
            CoreCaseId = Guid.NewGuid(), CatalogManifestSha256 = new string('b', 64),
            InventorySetSha256 = new string('c', 64)
        };
        var current = new InvoiceProcurementInventorySummary(context.TenantId, company,
            baseline.CatalogManifestSha256, [], new string('d', 64), 0);
        var empty = InvoiceProcurementOwnerLeaseEvidence.Encode(context, baseline,
            Guid.NewGuid(), current, [], Guid.NewGuid(), 0, at.AddMinutes(2));
        Assert.True(InvoiceMatchExecutionEvidence.OwnerCovers(empty, context));
        Assert.Equal(at.AddMinutes(2), empty.ValidUntil);
        Assert.Empty(empty.Allocations);
        var claim = new InvoiceMatchConsumption(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            context.TenantId, company, branch, supplier, order, issued, Guid.NewGuid(),
            "ZMW", InvoiceConsumptionKind.Debit, null, 1m, 1m, 0m, "principal3");
        var live = InvoiceProcurementOwnerLeaseEvidence.Encode(context, baseline,
            Guid.NewGuid(), current, [claim], Guid.NewGuid(), 1, null);
        Assert.True(InvoiceMatchExecutionEvidence.OwnerCovers(live, context));
        Assert.NotEqual(empty.EvidenceSha256, live.EvidenceSha256);
        Assert.Throws<InvalidOperationException>(() => InvoiceProcurementOwnerLeaseEvidence.Encode(
            context, baseline, Guid.NewGuid(), current, [claim], Guid.NewGuid(), 0, null));
        Assert.Throws<InvalidOperationException>(() => InvoiceProcurementOwnerLeaseEvidence.Encode(
            context, baseline, Guid.NewGuid(), current, [claim with { SupplierId = Guid.NewGuid() }],
            Guid.NewGuid(), 1, null));
    }
}
