using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Schema owners apply migrations; the ordinary ERP runtime verifies readiness.
/// Development may retain automatic setup. Production migration execution is explicit.</summary>
public static class CoreSchemaStartup
{
    public static bool MayApplyMigrations(IConfiguration configuration, bool development, IEnumerable<string> arguments)
        => arguments.Contains("--apply-migrations-only", StringComparer.Ordinal)
            || (configuration.GetValue<bool?>("ERP:Database:MigrateOnStartup") ?? development);

    public static async Task EnsureAsync(DbContext owner, string ownerName, bool mayApply, CancellationToken ct = default)
    {
        if (owner.Database.HasPendingModelChanges())
            throw new InvalidOperationException($"{ownerName} has unscaffolded model changes. Prepare the owning migration before startup.");
        if (mayApply)
        {
            await owner.Database.MigrateAsync(ct);
            return;
        }
        if ((await owner.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException($"{ownerName} has pending migrations. Apply them with the owning migration process before starting the runtime.");
    }
}
