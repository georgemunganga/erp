using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class PrincipalOrderRevisionCompositionTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task Host_binds_only_explicit_revision_option_but_uncaptured_store_remains_unavailable(
        string? configured, bool enabled)
    {
        var values = new Dictionary<string, string?>();
        if (configured is not null) values["Procurement:PrincipalOrderDraftRevisions:Enabled"] = configured;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        ProcurementHostIntegration.AddServices(services,
            "Host=never-used.invalid;Database=revision-composition-not-opened", configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(enabled, provider.GetRequiredService<PrincipalOrderRevisionOptions>().Enabled);
        var readiness = provider.GetRequiredService<IPrincipalOrderRevisionReadiness>();
        Assert.IsType<UnavailablePrincipalOrderRevisionReadiness>(readiness);
        // Null is deliberate: an unavailable implementation must not open or
        // replace the caller's owning context, even with the setting enabled.
        Assert.False(await readiness.IsReadyAsync(null!, CancellationToken.None));
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(provider, null!, false, true,
            CancellationToken.None));
        Assert.False(provider.GetRequiredService<ProcurementPrincipalOrderDraftOptions>().Enabled);
    }

    [Fact]
    public async Task Legacy_composition_overload_does_not_select_revision_execution()
    {
        var services = new ServiceCollection();
        ProcurementHostIntegration.AddServices(services,
            "Host=never-used.invalid;Database=revision-legacy-composition-not-opened");
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<PrincipalOrderRevisionOptions>().Enabled);
        Assert.False(await provider.GetRequiredService<IPrincipalOrderRevisionReadiness>()
            .IsReadyAsync(null!, CancellationToken.None));
    }
}
