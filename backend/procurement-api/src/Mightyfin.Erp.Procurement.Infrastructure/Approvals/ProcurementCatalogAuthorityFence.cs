using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Core.Persistence.Infrastructure;
using Npgsql;

namespace Mightyfin.Erp.Procurement.Infrastructure.Approvals;

/// <summary>Cooperative catalog owner/offer authority. Acquire BEFORE transaction/snapshot.
/// Commands: Core branch -> shared catalog -> domain locks. Owner writers: exclusive catalog only.
/// A shared-reader-first command is ordered before the writer; writer-first commands see its commit.
/// Direct SQL/foreign writers must participate or remain an explicitly unsupported mutation path.</summary>
public static class ProcurementCatalogAuthorityFence
{
    public static Task<PostgresSessionAdvisoryLease> AcquireAsync(NpgsqlConnection connection,
        string tenantId, Guid legalEntityId, bool exclusive, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (legalEntityId == Guid.Empty) throw new ArgumentException("A catalog entity is required.", nameof(legalEntityId));
        var identity = "greenwebb.procurement.catalog-authority.v1\0" + tenantId + "\0" + legalEntityId.ToString("D");
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return PostgresSessionAdvisoryLease.AcquireAsync(connection, key, exclusive, ct);
    }
}
