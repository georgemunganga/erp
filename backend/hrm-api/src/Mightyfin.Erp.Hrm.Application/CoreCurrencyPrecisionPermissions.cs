namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Explicit Core master-data grants. Neither action has a bootstrap role grant.</summary>
public static class CoreCurrencyPrecisionPermissions
{
    public const string Read = "core-currency-precision-read";
    public const string Configure = "core-currency-precision-configure";
    public static readonly string[] All = [Read, Configure];

    public static bool Allows(bool activeAccount, bool companyAccessible, bool branchConfined,
        bool explicitRead, bool explicitConfigure, bool configure)
    {
        if (!activeAccount || !companyAccessible) return false;
        var canConfigure = !branchConfined && explicitConfigure;
        return configure ? canConfigure : explicitRead || canConfigure;
    }
}
