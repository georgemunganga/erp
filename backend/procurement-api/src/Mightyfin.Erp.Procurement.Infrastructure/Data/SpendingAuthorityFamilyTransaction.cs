using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>
/// Complete logical-family lock for configuration commands. Runtime must use PostgreSQL;
/// unsupported providers fail closed instead of pretending publication was serialized.
/// The caller commits audit and all family changes before committing this transaction.
/// </summary>
public static class SpendingAuthorityFamilyTransaction
{
    public static async Task<IDbContextTransaction> BeginAsync(ProcurementDbContext db,
        string tenant, Guid company, string principal, string permission, string action,
        string reference, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("AuthorityPostgresTransactionRequired");
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { tenant, company, principal, permission, action, reference })));
            var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public static Task<List<ProcurementSpendingAuthorityGrant>> LoadAsync(ProcurementDbContext db,
        string principal, string permission, string action, string reference, CancellationToken ct)
        => db.SpendingAuthorityGrants.Include(x => x.Categories)
            .Where(x => x.PrincipalId == principal && x.Permission == permission && x.Action == action
                && x.AuthorityReference == reference).OrderBy(x => x.AuthorityVersion).ToListAsync(ct);
}
