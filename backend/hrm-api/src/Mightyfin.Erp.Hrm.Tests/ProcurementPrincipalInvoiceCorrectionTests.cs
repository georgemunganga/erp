using Mightyfin.Erp.Hrm.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceCorrectionTests
{
    [Fact]
    public void Default_host_composition_keeps_principal_invoice_execution_closed_without_a_physical_capture()
    {
        var services = new ServiceCollection();
        ProcurementHostIntegration.AddServices(services, "Host=never-used.invalid;Database=not-opened",
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<ProcurementPrincipalInvoiceMatchingOptions>().Enabled);
        Assert.False(provider.GetRequiredService<ProcurementPrincipalInvoiceSourceOptions>().Enabled);
        Assert.False(ProcurementPrincipalInvoiceSchemaCapture.Populated);
        Assert.Contains(services, service => service.ServiceType == typeof(IProcurementPrincipalInvoiceSchemaReadiness)
            && service.ImplementationType == typeof(ProcurementPrincipalInvoiceStoreReadiness));
    }

    [Theory]
    [InlineData("Verified", "Approved")]
    [InlineData("Returned", "Returned")]
    [InlineData("Rejected", "Rejected")]
    public void Corrected_mapping_uses_the_real_completed_core_case_state(string reviewOutcome, string coreState)
    {
        Assert.Equal(coreState, ProcurementPrincipalInvoiceMappingRoutes.PreviousCoreState(reviewOutcome));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    public void No_other_review_state_can_authorize_a_replacement_mapping(string reviewOutcome)
    {
        Assert.Throws<InvalidOperationException>(() => ProcurementPrincipalInvoiceMappingRoutes.PreviousCoreState(reviewOutcome));
    }
}
