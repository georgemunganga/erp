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

    [Theory]
    [InlineData(true, "true")]
    [InlineData(true, "false")]
    [InlineData(false, "true")]
    public void Outbox_publisher_never_applies_migrations_or_bootstraps_identity(
        bool development, string configured)
    {
        var args = new[] { "--run-outbox-publisher" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ERP:Database:MigrateOnStartup"] = configured }).Build();
        var mode = CoreHostStartupPolicy.Parse(args);
        Assert.Equal(CoreHostMode.OutboxPublisher, mode);
        Assert.False(CoreHostStartupPolicy.MayApplyCoreMigrations(mode, config, development, args));
        Assert.False(CoreHostStartupPolicy.MayBootstrapLocalIdentity(mode));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Ordinary_api_retains_existing_development_migration_setting_and_identity_bootstrap(
        bool development, bool expected)
    {
        var config = new ConfigurationBuilder().Build();
        var mode = CoreHostStartupPolicy.Parse([]);
        Assert.Equal(CoreHostMode.Api, mode);
        Assert.Equal(expected, CoreHostStartupPolicy.MayApplyCoreMigrations(mode, config, development, []));
        Assert.True(CoreHostStartupPolicy.MayBootstrapLocalIdentity(mode));
    }

    [Fact]
    public void Migration_owner_still_applies_and_exits_before_identity_bootstrap()
    {
        var args = new[] { "--apply-migrations-only" };
        var mode = CoreHostStartupPolicy.Parse(args);
        Assert.Equal(CoreHostMode.MigrationOwner, mode);
        Assert.True(CoreHostStartupPolicy.MayApplyCoreMigrations(mode, new ConfigurationBuilder().Build(), false, args));
        Assert.False(CoreHostStartupPolicy.MayBootstrapLocalIdentity(mode));
    }

    [Theory]
    [InlineData("--apply-migrations-only", "--run-outbox-publisher")]
    [InlineData("--run-outbox-publisher", "--apply-migrations-only")]
    public void Mixed_owner_and_publisher_flags_fail_before_any_startup_mode_is_selected(string first, string second)
        => Assert.Throws<InvalidOperationException>(() => CoreHostStartupPolicy.Parse([first, second]));
}
