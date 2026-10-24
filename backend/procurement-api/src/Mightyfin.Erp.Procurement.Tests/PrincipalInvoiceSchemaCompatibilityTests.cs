using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceSchemaCompatibilityTests
{
    [Fact]
    public void Empty_pre_upgrade_store_keeps_legacy_manual_correction_available()
    {
        Assert.False(ProcurementPrincipalInvoiceSchema.Complete(0));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(11)]
    [InlineData(12)]
    public void Partial_or_old_eleven_table_store_blocks_legacy_correction(int installedTables)
    {
        Assert.Equal("PrincipalInvoiceSchemaIncomplete",
            Assert.Throws<InvalidOperationException>(() => ProcurementPrincipalInvoiceSchema.Complete(installedTables)).Message);
    }

    [Fact]
    public void Complete_thirteen_table_store_enables_retained_evidence_fence()
    {
        Assert.True(ProcurementPrincipalInvoiceSchema.Complete(13));
    }
}
