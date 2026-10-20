using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>EF's execution strategy may wrap a confirmed PostgreSQL abort more than once.</summary>
internal static class ProcurementPostgresFailures
{
    internal static PostgresException? Postgres(Exception exception) => exception.GetBaseException() as PostgresException;
    internal static bool IsConcurrency(Exception exception) => Postgres(exception)?.SqlState
        is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
}
