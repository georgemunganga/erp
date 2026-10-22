using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    const string ActualP23 = "20261022112145_P23CorePrincipalInvoiceUnitReview";
    const string ActualP24 = "20261022144102_P24CorePrincipalRecurringPurchasePlanning";
    // Filled only from independently reviewed actual private PostgreSQL capture.
    const string CapturedRecurringCheckSha256 = "19bd4610fb2250b8ae18b28146c5882c0cfef1fadbf284f4f808baee161e50db";
    static Dictionary<string, CheckPin> recurringLegacyChecks = [];
    sealed record CheckPin(string Type, bool Deferrable, bool Deferred, bool Validated, string Definition);
    static readonly JsonSerializerOptions CaptureJson = new(JsonSerializerDefaults.Web);

    static async Task UpgradeRecurringSchema()
    {
        stage = "genuine-P23-and-P24-after-exact-legacy-P22-rehearsal";
        var before = await Legacy();
        await using var db = Db();
        var ids = db.Database.GetMigrations().ToArray();
        Check(ids.Contains(ActualP23) && ids.Contains(ActualP24), "Both genuinely generated successor migrations must be compiled.");
        await db.GetService<IMigrator>().MigrateAsync(ActualP23);
        await db.GetService<IMigrator>().MigrateAsync(ActualP24);
        Check(before == await Legacy(), "Genuine recurring successor changed original legacy invoice data.");
    }

    static async Task VerifyCanonicalSchema()
    {
        await using var db = Db();
        Check(!db.Database.HasPendingModelChanges(), "Current recurring snapshot/model mismatch.");
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Current genuine migration is missing; no readiness override.");
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal);
        Check(applied.Contains(CanonicalP21) && applied.Contains(CorePrincipalMigration())
            && applied.Contains(ActualP23) && applied.Contains(ActualP24), "Genuine current migration history incomplete.");
        Check(recurringLegacyChecks.Count == 40, "Exact released P22 CHECK inventory must have passed before successor integration.");
        var expected = new Dictionary<string, CheckPin>(recurringLegacyChecks, StringComparer.Ordinal);
        var p23 = ReadCapturedChecks("Captured.P23.Checks", "852061b7fbc71a789ed74b7700e7982378dca7f23865ae7951c969f7f123f423");
        Check(p23.Count == 7, "Genuine P23 capture must contain its exact seven changed/new CHECKs.");
        foreach (var (key, value) in p23) expected[key] = value;
        var p24 = ReadCapturedChecks("Captured.P24.Checks", CapturedRecurringCheckSha256);
        Check(p24.Count == 1 && p24.ContainsKey("procurement.recurring_purchase_commitments.ck_recurring_purchase_schedule"),
            "Exactly the actual recurring schedule CHECK must be captured.");
        foreach (var (key, value) in p24) Check(expected.TryAdd(key, value), "Recurring capture cannot replace released CHECKs.");
        var actual = await ReadCurrentChecks();
        Check(actual.Count == expected.Count && expected.All(x => actual.TryGetValue(x.Key, out var value) && value == x.Value),
            "Actual current CHECK catalog differs from retained P22 plus exact captured P23/P24 definitions.");
    }

    static Dictionary<string, CheckPin> ReadCapturedChecks(string resource, string expectedSha)
    {
        Check(expectedSha.Length == 64, "Actual recurring PostgreSQL CHECK capture is pending; never fabricate schema readiness.");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new ProbeFailure("Missing actual PostgreSQL capture resource.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        Check(Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())) == expectedSha, "Captured schema resource hash mismatch.");
        var result = JsonSerializer.Deserialize<Dictionary<string, CheckPin>>(bytes.ToArray(), CaptureJson)
            ?? throw new ProbeFailure("Invalid captured CHECK dictionary.");
        Check(result.Values.All(x => x.Type == "c" && !x.Deferrable && !x.Deferred && x.Validated && x.Definition.StartsWith("CHECK (", StringComparison.Ordinal)),
            "Captured CHECK metadata is not validated/nondeferrable.");
        return result;
    }

    static async Task<Dictionary<string, CheckPin>> ReadCurrentChecks()
    {
        var json = await Scalar("""
            SELECT COALESCE(jsonb_object_agg(n.nspname||'.'||t.relname||'.'||c.conname,
                jsonb_build_object('type',c.contype::text,'deferrable',c.condeferrable,'deferred',c.condeferred,
                    'validated',c.convalidated,'definition',pg_get_constraintdef(c.oid))), '{}'::jsonb)::text
            FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace
            JOIN pg_class t ON t.oid=c.conrelid
            WHERE n.nspname='procurement' AND c.contype='c'
            """);
        return JsonSerializer.Deserialize<Dictionary<string, CheckPin>>(json, CaptureJson)
            ?? throw new ProbeFailure("Actual CHECK catalog missing.");
    }
}
