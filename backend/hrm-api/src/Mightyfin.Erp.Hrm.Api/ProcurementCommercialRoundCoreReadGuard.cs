namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Compatibility facade: preserve the existing B1 projections and SQL exactly.</summary>
internal static class ProcurementCommercialRoundCoreReadGuard
{
    internal static readonly IReadOnlyDictionary<string, string[]> RequiredReadColumns =
        ProcurementCoreReadProjectionGuard.RequiredReadColumns;
    internal static readonly IReadOnlyDictionary<string, string[]> AllowedReadColumns =
        ProcurementCoreReadProjectionGuard.AllowedReadColumns;
    internal static readonly string Sql = ProcurementCoreReadProjectionGuard.Sql;

    internal static bool Allows(string table, string column)
        => ProcurementCoreReadProjectionGuard.Allows(table, column);
}
