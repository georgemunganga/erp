using Npgsql;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>EF's execution strategy may wrap a confirmed PostgreSQL abort more than once.</summary>
internal static class ProcurementPostgresFailures
{
    internal static PostgresException? Postgres(Exception exception) => exception.GetBaseException() as PostgresException;
    internal static bool IsStoreFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is DbException or DbUpdateException
                || current is AggregateException aggregate && aggregate.InnerExceptions.Any(IsStoreFailure)) return true;
        return false;
    }
    internal static bool IsConcurrency(Exception exception) => Postgres(exception)?.SqlState
        is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
}
