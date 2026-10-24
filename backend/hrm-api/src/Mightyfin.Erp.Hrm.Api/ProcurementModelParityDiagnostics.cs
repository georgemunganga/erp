using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Optional process-local cache for EF's immutable model-versus-migration-snapshot comparison.
/// Neither applied migrations nor physical schema, SQL privileges, policies, people or record
/// scope are cached. The model keys are weak; no DbContext or connection is retained.
/// </summary>
internal static class ProcurementModelParityDiagnostics
{
    private sealed class DesignCache
    {
        internal readonly ConditionalWeakTable<IModel, SnapshotCache> Snapshots = new();
        internal readonly SnapshotCache NoSnapshot = new();
    }

    private sealed class SnapshotCache
    {
        internal readonly System.Collections.Concurrent.ConcurrentDictionary<ComparisonKey, bool> Results = new();
    }

    private readonly record struct ComparisonKey(Type? SnapshotType, Guid SnapshotAssemblyId,
        string Provider, Type DifferType);

    private static readonly ConditionalWeakTable<IModel, DesignCache> Cache = new();
    private static readonly Meter Meter = new("GreenWebb.Procurement.Readiness", "1.0.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "procurement.readiness.phase.duration", "ms", "Elapsed readiness phase time");

    internal static bool CacheEnabled(IConfiguration? configuration)
        => configuration?.GetValue<bool>("Procurement:ReadinessPerformance:ModelParityCacheEnabled") == true;

    private static bool TimingEnabled(IConfiguration? configuration)
        => configuration?.GetValue<bool>("Procurement:ReadinessPerformance:DiagnosticsEnabled") == true;

    internal static bool HasPendingModelChanges(ProcurementDbContext owner,
        IConfiguration? configuration, string phase)
        => Measure(configuration, phase, () =>
        {
            if (!CacheEnabled(configuration)) return owner.Database.HasPendingModelChanges();
            var design = owner.GetService<IDesignTimeModel>().Model;
            var migration = owner.GetService<IMigrationsAssembly>().ModelSnapshot;
            var differType = owner.GetService<IMigrationsModelDiffer>().GetType();
            return Compare(design, migration?.Model, migration?.GetType(),
                owner.Database.ProviderName ?? "", differType,
                () => owner.Database.HasPendingModelChanges());
        });

    // GetOrAdd may run a competing comparison twice during a cold race. It only
    // stores successful results; exceptions are never turned into a ready state.
    internal static bool Compare(IModel design, IModel? snapshot, Type? snapshotType,
        string provider, Type differType, Func<bool> compare)
    {
        var designCache = Cache.GetValue(design, static _ => new DesignCache());
        var snapshotCache = snapshot is null ? designCache.NoSnapshot
            : designCache.Snapshots.GetValue(snapshot, static _ => new SnapshotCache());
        var key = new ComparisonKey(snapshotType, snapshotType?.Module.ModuleVersionId ?? Guid.Empty,
            provider, differType);
        return snapshotCache.Results.GetOrAdd(key, _ => compare());
    }

    internal static T Measure<T>(IConfiguration? configuration, string phase, Func<T> work)
    {
        if (!TimingEnabled(configuration)) return work();
        var start = Stopwatch.GetTimestamp();
        try { return work(); }
        finally { Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            new KeyValuePair<string, object?>("phase", phase)); }
    }

    internal static async Task<T> MeasureAsync<T>(IConfiguration? configuration, string phase,
        Func<Task<T>> work)
    {
        if (!TimingEnabled(configuration)) return await work();
        var start = Stopwatch.GetTimestamp();
        try { return await work(); }
        finally { Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            new KeyValuePair<string, object?>("phase", phase)); }
    }
}
