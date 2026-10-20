using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Mightyfin.Erp.Core.Approvals.Infrastructure;

/// <summary>
/// Core-owned cooperative branch-membership fence. Acquire on the physical connection
/// BEFORE beginning a transaction or reading authoritative membership. This is deliberately
/// tenant-wide: a command can validate several dynamically resolved approvers.
/// It does not fence role, worker, account, organisation or direct SQL mutations.
/// </summary>
public sealed class CoreBranchAuthorizationFence : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly long key;
    private readonly bool exclusive;
    private bool disposed;

    private CoreBranchAuthorizationFence(NpgsqlConnection connection, long key, bool exclusive)
    { this.connection = connection; this.key = key; this.exclusive = exclusive; }

    public static async Task<CoreBranchAuthorizationFence> AcquireAsync(NpgsqlConnection connection,
        string tenantId, bool exclusive, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (settings.Multiplexing || settings.NoResetOnClose)
            throw new InvalidOperationException("Branch authorization fencing requires pinned sessions and connection reset.");
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Open the physical connection before acquiring the branch fence.");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("greenwebb.core.branch-authorization.v1\0" + tenantId));
        var key = BinaryPrimitives.ReadInt64BigEndian(digest);
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
            // Cancellation/network failure may hide successful session-lock acquisition.
            // Evict this physical session, never return an uncertain lock to the pool.
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
                throw new InvalidOperationException("The branch authorization session fence was not held.");
        }
        catch
        {
            NpgsqlConnection.ClearPool(connection);
            await connection.CloseAsync();
            throw;
        }
    }
}
