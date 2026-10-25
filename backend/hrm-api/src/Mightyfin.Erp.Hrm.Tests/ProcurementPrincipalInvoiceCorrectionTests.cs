using Mightyfin.Erp.Hrm.Api;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceCorrectionTests
{
    [Fact]
    public void Default_host_composition_keeps_principal_invoice_execution_closed_with_reviewed_capture_constants()
    {
        var services = new ServiceCollection();
        ProcurementHostIntegration.AddServices(services, "Host=never-used.invalid;Database=not-opened",
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<ProcurementPrincipalInvoiceMatchingOptions>().Enabled);
        Assert.False(provider.GetRequiredService<ProcurementPrincipalInvoiceSourceOptions>().Enabled);
        // Captured owner metadata is present; neither configured workflow is enabled.
        Assert.True(ProcurementPrincipalInvoiceSchemaCapture.Populated);
        Assert.Contains(services, service => service.ServiceType == typeof(IProcurementPrincipalInvoiceSchemaReadiness)
            && service.ImplementationType == typeof(ProcurementPrincipalInvoiceStoreReadiness));
    }

    [Fact]
    public async Task Genuine_capture_constants_never_admit_an_uninspected_non_postgres_store()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("p31-capture-negative", Guid.NewGuid(), Guid.NewGuid().ToString("D"),
            null, null, false, "capture-test");
        await using var db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, scope);

        Assert.False(await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(db,
            ProcurementPrincipalInvoiceSchemaCapture.Types,
            "SELECT true", ProcurementPrincipalInvoiceSchemaCapture.OwnershipNames,
            ProcurementPrincipalInvoiceSchemaCapture.Physical, CancellationToken.None,
            ProcurementPrincipalInvoiceSchemaCapture.OwnershipManifestSql));
        Assert.Empty(db.ChangeTracker.Entries());
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
