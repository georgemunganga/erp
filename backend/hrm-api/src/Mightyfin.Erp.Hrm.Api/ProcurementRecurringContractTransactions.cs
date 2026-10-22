using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Protects the contract supplier/reference invariant across two writers.</summary>
internal static class ProcurementRecurringContractTransactions
{
    internal static async Task<IDbContextTransaction?> BeginAsync(ProcurementDbContext db, CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    internal static bool IsConflict(Exception error) => error is DbUpdateConcurrencyException
        || ProcurementPostgresFailures.IsConcurrency(error);
}
