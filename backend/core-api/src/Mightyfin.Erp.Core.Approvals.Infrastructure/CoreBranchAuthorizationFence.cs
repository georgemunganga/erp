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
    private readonly PostgresSessionAdvisoryLease lease;
    private CoreBranchAuthorizationFence(PostgresSessionAdvisoryLease lease) => this.lease = lease;

    public static async Task<CoreBranchAuthorizationFence> AcquireAsync(NpgsqlConnection connection,
        string tenantId, bool exclusive, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        // Retain the existing key bytes/API: branch writers and all established readers stay paired.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("greenwebb.core.branch-authorization.v1\0" + tenantId));
        var key = BinaryPrimitives.ReadInt64BigEndian(digest);
        return new(await PostgresSessionAdvisoryLease.AcquireAsync(connection, key, exclusive, ct));
    }

    public ValueTask DisposeAsync() => lease.DisposeAsync();
}
