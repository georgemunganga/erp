using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementModelParityDiagnosticsTests
{
    private static readonly Type Differ = typeof(ProcurementModelParityDiagnosticsTests);

    [Fact]
    public void Cache_is_default_off_and_requires_explicit_configuration()
    {
        Assert.False(ProcurementModelParityDiagnostics.CacheEnabled(new ConfigurationBuilder().Build()));
        Assert.True(ProcurementModelParityDiagnostics.CacheEnabled(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["Procurement:ReadinessPerformance:ModelParityCacheEnabled"] = "true" }).Build()));
    }

    [Fact]
    public void Repeated_same_immutable_model_comparison_is_reused_including_pending_result()
    {
        var design = new ModelBuilder().FinalizeModel();
        var snapshot = new ModelBuilder().FinalizeModel();
        var calls = 0;
        bool Evaluate() => ProcurementModelParityDiagnostics.Compare(design, snapshot, typeof(string),
            "Npgsql.EntityFrameworkCore.PostgreSQL", Differ, () => { calls++; return true; });

        Assert.True(Evaluate());
        Assert.True(Evaluate());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Different_models_snapshot_types_providers_and_differs_are_isolated()
    {
        var design = new ModelBuilder().FinalizeModel();
        var otherDesign = new ModelBuilder().FinalizeModel();
        var snapshot = new ModelBuilder().FinalizeModel();
        var otherSnapshot = new ModelBuilder().FinalizeModel();
        var calls = 0;
        bool Compare(Microsoft.EntityFrameworkCore.Metadata.IModel current,
            Microsoft.EntityFrameworkCore.Metadata.IModel last, Type snapshotType,
            string provider, Type differ)
            => ProcurementModelParityDiagnostics.Compare(current, last, snapshotType, provider, differ,
                () => { calls++; return false; });

        Assert.False(Compare(design, snapshot, typeof(string), "pg", Differ));
        Assert.False(Compare(otherDesign, snapshot, typeof(string), "pg", Differ));
        Assert.False(Compare(design, otherSnapshot, typeof(string), "pg", Differ));
        Assert.False(Compare(design, snapshot, typeof(int), "pg", Differ));
        Assert.False(Compare(design, snapshot, typeof(string), "other", Differ));
        Assert.False(Compare(design, snapshot, typeof(string), "pg", typeof(object)));
        Assert.Equal(6, calls);
    }

    [Fact]
    public void Comparison_failures_are_never_cached_as_ready()
    {
        var design = new ModelBuilder().FinalizeModel();
        var snapshot = new ModelBuilder().FinalizeModel();
        var calls = 0;
        bool Evaluate() => ProcurementModelParityDiagnostics.Compare(design, snapshot, typeof(string),
            "pg", Differ, () =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("Synthetic comparison failure");
                return true;
            });

        Assert.Throws<InvalidOperationException>(() => Evaluate());
        Assert.True(Evaluate());
        Assert.Equal(2, calls);
    }
}
