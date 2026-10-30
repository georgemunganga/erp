using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementCompiledModelParity;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCompiledModelParityTests
{
    private static ModelIdentity Key() => new(new ModelBuilder().FinalizeModel(), new ModelBuilder().FinalizeModel(),
        new("provider-a", typeof(ProcurementCompiledModelParityTests), Guid.NewGuid()));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_parity_is_reused_without_retaining_a_database_or_request_check(bool pending)
    {
        var cache = new ModelParityCache(); var key = Key(); var calls = 0;
        Assert.Equal(pending, cache.Check(key, () => { calls++; return pending; }));
        Assert.Equal(pending, cache.Check(key, () => throw new InvalidOperationException("Must not reevaluate immutable parity")));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("design")]
    [InlineData("provider")]
    [InlineData("snapshot-type")]
    [InlineData("snapshot-module")]
    public void Each_immutable_model_or_snapshot_identity_gets_its_own_result(string variant)
    {
        var cache = new ModelParityCache(); var key = Key();
        var other = variant switch
        {
            "runtime" => key with { RuntimeModel = new ModelBuilder().FinalizeModel() },
            "design" => key with { DesignModel = new ModelBuilder().FinalizeModel() },
            "provider" => key with { Snapshot = key.Snapshot with { Provider = "provider-b" } },
            "snapshot-type" => key with { Snapshot = key.Snapshot with { SnapshotType = typeof(string) } },
            _ => key with { Snapshot = key.Snapshot with { ModuleVersionId = Guid.NewGuid() } }
        };
        Assert.False(cache.Check(key, () => false));
        Assert.True(cache.Check(other, () => true));
        Assert.False(cache.Check(key, () => true));
    }

    [Fact]
    public void A_failed_check_does_not_publish_a_cached_positive_or_negative_and_can_be_retried()
    {
        var cache = new ModelParityCache(); var key = Key(); var calls = 0;
        Assert.Throws<InvalidOperationException>(() => cache.Check(key, () => { calls++; throw new InvalidOperationException("Transient parity failure"); }));
        Assert.Throws<OperationCanceledException>(() => cache.Check(key, () => { calls++; throw new OperationCanceledException(); }));
        Assert.True(cache.Check(key, () => { calls++; return true; }));
        Assert.True(cache.Check(key, () => false));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_completed_check_for_the_same_key()
    {
        var cache = new ModelParityCache(); var key = Key(); var calls = 0;
        using var start = new ManualResetEventSlim();
        var requests = Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return cache.Check(key, () => { Interlocked.Increment(ref calls); return false; });
        })).ToArray();
        start.Set();
        Assert.All(await Task.WhenAll(requests), pending => Assert.False(pending));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void One_failed_concurrent_check_is_retried_under_the_gate_instead_of_becoming_cached_ready()
    {
        var cache = new ModelParityCache(); var key = Key();
        Assert.Throws<InvalidOperationException>(() => cache.Check(key, () => throw new InvalidOperationException()));
        var checks = 0;
        Parallel.For(0, 12, _ => Assert.True(cache.Check(key, () => { Interlocked.Increment(ref checks); return true; })));
        Assert.Equal(1, checks);
    }

    [Fact]
    public void Actual_Postgres_contexts_share_model_parity_across_tenants_but_not_live_connection_state()
    {
        var interceptor = new RejectConnection();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_only;Username=invalid;Pooling=false",
                x => x.MigrationsHistoryTable("__procurement_migrations", "procurement"))
            .AddInterceptors(interceptor).Options;
        var firstScope = new ProcurementRequestScope();
        firstScope.Set("tenant-a", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "a");
        var secondScope = new ProcurementRequestScope();
        secondScope.Set("tenant-b", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "b");
        using var first = new ProcurementDbContext(options, firstScope);
        using var second = new ProcurementDbContext(options, secondScope);
        var firstKey = ModelIdentity.From(first); var secondKey = ModelIdentity.From(second);
        Assert.Same(firstKey.RuntimeModel, secondKey.RuntimeModel);
        Assert.Same(firstKey.DesignModel, secondKey.DesignModel);
        Assert.Equal(firstKey.Snapshot, secondKey.Snapshot);
        Assert.False(first.Database.HasPendingModelChanges());
        Assert.False(ProcurementCompiledModelParity.HasPendingModelChanges(first));
        Assert.False(ProcurementCompiledModelParity.HasPendingModelChanges(second));
        Assert.False(second.Database.HasPendingModelChanges());
        Assert.Equal(0, interceptor.Attempts);
        // A cached model result must not short-circuit a subsequent live database
        // operation. Pending migration history remains a separate fresh read.
        Assert.Throws<InvalidOperationException>(() => second.Database.GetPendingMigrations().ToArray());
        Assert.Equal(1, interceptor.Attempts);
    }

    private sealed class RejectConnection : DbConnectionInterceptor
    {
        internal int Attempts;
        public override InterceptionResult ConnectionOpening(System.Data.Common.DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        { Attempts++; throw new InvalidOperationException("Test forbids database connections"); }
    }
}
