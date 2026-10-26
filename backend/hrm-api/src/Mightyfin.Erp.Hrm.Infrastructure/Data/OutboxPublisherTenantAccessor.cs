using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>
/// Publisher-process tenant context. A publisher has no request principal, so the
/// only write scope it may use is the tenant on an outbox row loaded from Core's
/// database. An unbound context cannot save or run tenant-filtered queries.
/// </summary>
public sealed class OutboxPublisherTenantAccessor : ITenantAccessor
{
    private string? claimedTenantId;

    public string GetTenantId() => claimedTenantId
        ?? throw new InvalidOperationException("Outbox publisher tenant is not bound to a claimed row.");

    internal IDisposable BindAuthoritativeRow(OutboxMessage row)
    {
        if (string.IsNullOrWhiteSpace(row.TenantId) || claimedTenantId is not null)
            throw new InvalidOperationException("Outbox publisher requires one database row tenant at a time.");

        claimedTenantId = row.TenantId;
        return new Binding(this);
    }

    private sealed class Binding(OutboxPublisherTenantAccessor owner) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            owner.claimedTenantId = null;
            disposed = true;
        }
    }
}
