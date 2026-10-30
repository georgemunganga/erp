using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCurrencyPrecisionRulesTests
{
    [Fact]
    public void Publication_requires_explicit_canonical_configuration_and_expected_revision()
    {
        Assert.True(CoreCurrencyPrecisionRules.ValidPublish("ZMW", 0, 2, "ToEven", "ISO-4217-amendment-154", "Initial reviewed reference"));
        Assert.True(CoreCurrencyPrecisionRules.ValidPublish("ZMW", 1, 4, "AwayFromZero", "reviewed-override", "New published revision"));
        Assert.True(CoreCurrencyPrecisionRules.CanPublish(null, 0));
        Assert.True(CoreCurrencyPrecisionRules.CanPublish(1, 1));
        Assert.False(CoreCurrencyPrecisionRules.CanPublish(1, 0));
        Assert.False(CoreCurrencyPrecisionRules.CanPublish(1, 2));
        Assert.False(CoreCurrencyPrecisionRules.CanPublish(int.MaxValue, int.MaxValue));
    }

    [Theory]
    [InlineData("zmw", 0, 2, "ToEven", "source", "reason")]
    [InlineData("ZM", 0, 2, "ToEven", "source", "reason")]
    [InlineData("ZMW", -1, 2, "ToEven", "source", "reason")]
    [InlineData("ZMW", 0, -1, "ToEven", "source", "reason")]
    [InlineData("ZMW", 0, 5, "ToEven", "source", "reason")]
    [InlineData("ZMW", 0, 2, "HalfUp", "source", "reason")]
    [InlineData("ZMW", 0, 2, "ToEven", " ", "reason")]
    [InlineData("ZMW", 0, 2, "ToEven", "source", " ")]
    public void Rejects_unreviewed_or_unsupported_publication(string code, int revision,
        int minorUnits, string mode, string source, string reason)
        => Assert.False(CoreCurrencyPrecisionRules.ValidPublish(code, revision, minorUnits, mode, source, reason));

    [Fact]
    public void Retirement_cannot_revive_earlier_or_already_retired_revision()
    {
        Assert.True(CoreCurrencyPrecisionRules.CanRetire(3, 3, 3, null));
        Assert.False(CoreCurrencyPrecisionRules.CanRetire(3, 2, 2, null));
        Assert.False(CoreCurrencyPrecisionRules.CanRetire(3, 3, 2, null));
        Assert.False(CoreCurrencyPrecisionRules.CanRetire(3, 3, 3, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Core_grants_are_explicit_and_have_no_default_role_mapping()
    {
        Assert.Equal(["core-currency-precision-read", "core-currency-precision-configure"],
            CoreCurrencyPrecisionPermissions.All);
        Assert.DoesNotContain(CoreCurrencyPrecisionPermissions.All,
            action => ProcurementActionCatalog.BootstrapGrants.ContainsKey(action));
    }

    [Fact]
    public void Current_account_and_company_scope_remain_required_for_read_and_master_configuration()
    {
        static bool Allows(bool active, bool company, bool branch,
            bool read, bool configureGrant, bool write)
            => CoreCurrencyPrecisionPermissions.Allows(active, company, branch, read,
                configureGrant, write);
        Assert.True(Allows(true, true, true, true, false, false));
        Assert.False(Allows(true, true, true, true, true, true));
        Assert.False(Allows(true, true, false, false, false, true));
        Assert.True(Allows(true, true, false, false, true, true));
        Assert.False(Allows(false, true, false, true, true, false));
        Assert.False(Allows(true, false, false, true, true, false));
    }
}
