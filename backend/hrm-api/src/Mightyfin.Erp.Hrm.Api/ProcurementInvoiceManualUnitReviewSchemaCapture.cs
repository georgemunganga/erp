using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Filled only from Root's genuine migration and native/restore proof.
/// Empty pins keep manual execution unavailable; they grant no authority.</summary>
internal static class ProcurementInvoiceManualUnitReviewSchemaCapture
{
    internal const string MigrationId="";
    internal const string NativeOutcomeCheck="";
    internal const string RestoredOutcomeCheck="";
    internal static bool MatchesOutcome(string actual)=>NativeOutcomeCheck.Length>0
        && (actual==NativeOutcomeCheck || RestoredOutcomeCheck.Length>0 && actual==RestoredOutcomeCheck);
    internal static async Task<bool> IsInstalledAsync(ProcurementDbContext db,CancellationToken ct)=>
        MigrationId.Length>0 && NativeOutcomeCheck.Length>0
        && (await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal);
}
