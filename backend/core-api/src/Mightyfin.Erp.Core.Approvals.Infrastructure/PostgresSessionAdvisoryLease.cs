using Npgsql;

namespace Mightyfin.Erp.Core.Approvals.Infrastructure;

/// <summary>Shared infrastructure only: owning capabilities define keys, scope and lock order.</summary>
public sealed class PostgresSessionAdvisoryLease : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly long key;
    private readonly bool exclusive;
    private bool disposed;
    private PostgresSessionAdvisoryLease(NpgsqlConnection connection, long key, bool exclusive)
    { this.connection = connection; this.key = key; this.exclusive = exclusive; }

    public static async Task<PostgresSessionAdvisoryLease> AcquireAsync(NpgsqlConnection connection,
        long key, bool exclusive, CancellationToken ct = default)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (settings.Multiplexing || settings.NoResetOnClose)
            throw new InvalidOperationException("Authority fencing requires pinned sessions and connection reset.");
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Open the physical connection before acquiring an authority fence.");
        try
        {
            await using var command = new NpgsqlCommand(exclusive
                ? "SELECT pg_advisory_lock(@key)" : "SELECT pg_advisory_lock_shared(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(ct);
            return new(connection, key, exclusive);
        }
        catch
        {
            // Acquisition may have succeeded despite a canceled/unknown response.
            NpgsqlConnection.ClearPool(connection);
            await connection.CloseAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var command = new NpgsqlCommand(exclusive
                ? "SELECT pg_advisory_unlock(@key)" : "SELECT pg_advisory_unlock_shared(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            if (await command.ExecuteScalarAsync(cleanup.Token) is not true)
                throw new InvalidOperationException("The authority session fence was not held.");
        }
        catch
        {
            NpgsqlConnection.ClearPool(connection);
            await connection.CloseAsync();
            throw;
        }
    }
}
