using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Only compiled EF model/snapshot parity is immutable for these identities. This
/// does not cache database migrations, schema, grants, policies or authorization.
/// Weak model keys allow obsolete model variants to be collected. Entries retain
/// only a gate and a bool, never a scoped context, scope or check delegate.
/// </summary>
internal static class ProcurementCompiledModelParity
{
    private static readonly ModelParityCache Cache = new();

    internal static bool HasPendingModelChanges(DbContext owner) =>
        Cache.Check(ModelIdentity.From(owner), owner.Database.HasPendingModelChanges);

    internal readonly record struct SnapshotIdentity(string? Provider, Type? SnapshotType, Guid ModuleVersionId);
    internal sealed record ModelIdentity(IModel RuntimeModel, IModel DesignModel, SnapshotIdentity Snapshot)
    {
        internal static ModelIdentity From(DbContext owner)
        {
            var snapshotType = owner.GetService<IMigrationsAssembly>().ModelSnapshot?.GetType();
            return new(owner.Model, owner.GetService<IDesignTimeModel>().Model,
                new(owner.Database.ProviderName, snapshotType,
                    snapshotType?.Assembly.ManifestModule.ModuleVersionId ?? Guid.Empty));
        }
    }

    internal sealed class ModelParityCache
    {
        private readonly ConditionalWeakTable<IModel,
            ConditionalWeakTable<IModel, ConcurrentDictionary<SnapshotIdentity, Entry>>> models = new();

        internal bool Check(ModelIdentity identity, Func<bool> check)
        {
            var designs = models.GetValue(identity.RuntimeModel, static _ => new());
            var snapshots = designs.GetValue(identity.DesignModel, static _ => new());
            var entry = snapshots.GetOrAdd(identity.Snapshot, static _ => new());
            lock (entry.Gate)
            {
                if (entry.Completed) return entry.Pending;
                // Exceptions leave Completed false: a later call must retry, never
                // infer a positive readiness result from an interrupted evaluation.
                var pending = check();
                entry.Pending = pending;
                entry.Completed = true;
                return pending;
            }
        }

        private sealed class Entry
        {
            internal readonly object Gate = new();
            internal bool Completed;
            internal bool Pending;
        }
    }
}
