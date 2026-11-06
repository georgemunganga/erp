using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>
/// Only for metadata fixtures containing synthetic migration-history rows and no business tables.
/// SQLite cannot establish compatibility with the owning Npgsql snapshot. These explicit doubles
/// isolate metadata control flow; PostgreSQL schema/manifest verification is a separate gate.
/// </summary>
internal static class SyntheticProcurementMetadataModel
{
    public static void AddUnavailablePlanAndRoundServices(IServiceCollection services)
    {
        // History-only fixtures own neither plan tables nor the native round contract.
        services.AddSingleton(new ProcurementPlanReviewOptions());
        services.AddSingleton<IProcurementPlanReviewReadiness>(new PlanDraftFixtureReadiness());
        services.AddSingleton(new ProcurementCommercialRoundOptions());
        services.AddSingleton<IProcurementCommercialRoundReadiness, UnavailableRoundReadiness>();
        services.AddSingleton<IProcurementCommercialRoundReviewReadiness, UnavailableRoundReadiness>();
    }

    private sealed class UnavailableRoundReadiness : IProcurementCommercialRoundReadiness, IProcurementCommercialRoundReviewReadiness
    {
        public bool HasCapturedContract => false;
        public Task<bool> ReadyAsync(ProcurementDbContext db, bool record, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> BarrierReadyAsync(ProcurementDbContext db, CancellationToken ct) => Task.FromResult(false);
    }

    public static void Configure(DbContextOptionsBuilder options, string scenario)
    {
        if (scenario == "pending-model") options.ReplaceService<IMigrationsModelDiffer, Pending>();
        else if (scenario == "unknown-model") options.ReplaceService<IMigrationsModelDiffer, Unknown>();
        else options.ReplaceService<IMigrationsModelDiffer, Unchanged>();
    }

    /// <summary>
    /// Generate only SQLite's history DDL with its normal provider services. The fixture's
    /// synthetic differ must remain incapable of generating migration operations.
    /// The caller owns the already-open connection; no business schema is created.
    /// </summary>
    public static async Task CreateSqliteHistoryAsync(DbContext fixture, CancellationToken ct = default)
    {
        if (!fixture.Database.IsSqlite()
            || fixture.Database.GetDbConnection() is not SqliteConnection connection
            || connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Synthetic metadata history setup requires an existing open SQLite fixture connection.");

        // Do not copy fixture options: those intentionally replace IMigrationsModelDiffer.
        // UseSqlite(DbConnection) leaves connection ownership with the caller.
        var options = new DbContextOptionsBuilder<DbContext>().UseSqlite(connection).Options;
        await using var normalProvider = new DbContext(options);
        var history = normalProvider.GetService<IHistoryRepository>();
        var ddl = history.GetCreateIfNotExistsScript();
        await fixture.Database.ExecuteSqlRawAsync(ddl, ct);
    }

    public class Unchanged : IMigrationsModelDiffer
    {
        public virtual bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => false;
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
            => throw new NotSupportedException("Synthetic metadata fixture cannot generate migrations.");
    }
    public sealed class Pending : Unchanged
    {
        public override bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => true;
    }
    public sealed class Unknown : Unchanged
    {
        public override bool HasDifferences(IRelationalModel? source, IRelationalModel? target)
            => throw new InvalidOperationException("Synthetic model comparison unavailable.");
    }
}

/// <summary>Only the in-memory draft HTTP fixture opts into draft-table availability.
/// This never proves PostgreSQL readiness or enables plan review/execution.</summary>
internal sealed class PlanDraftFixtureReadiness(bool draftStoreAvailable = false) : IProcurementPlanReviewReadiness
{
    public Task<bool> CanReadPlanAsync(ProcurementDbContext db, CancellationToken ct) => Task.FromResult(draftStoreAvailable && db.Database.IsInMemory());
    public Task<bool> CanReadReviewAsync(ProcurementDbContext db, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(false);
}
