using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreSchemaStartupTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Default_runtime_migration_authority_is_limited_to_development(bool development, bool expected)
        => Assert.Equal(expected, CoreSchemaStartup.MayApplyMigrations(new ConfigurationBuilder().Build(), development, []));

    [Fact]
    public void Explicit_owner_command_retains_migration_support_in_production()
        => Assert.True(CoreSchemaStartup.MayApplyMigrations(new ConfigurationBuilder().Build(), false, ["--apply-migrations-only"]));

    [Theory]
    [InlineData("false", true, false)]
    [InlineData("true", false, true)]
    public void Explicit_operational_setting_is_honoured(string configured, bool development, bool expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ERP:Database:MigrateOnStartup"] = configured }).Build();
        Assert.Equal(expected, CoreSchemaStartup.MayApplyMigrations(config, development, []));
    }
}
