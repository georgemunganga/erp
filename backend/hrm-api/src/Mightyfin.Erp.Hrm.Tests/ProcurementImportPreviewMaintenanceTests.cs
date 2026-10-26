using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementImportPreviewMaintenanceTests
{
    [Fact]
    public void Actor_and_company_limits_apply_to_actual_utf8_snapshot_bytes()
    {
        var limits = new ProcurementImportPreviewLimits(3, 64L * 1024 * 1024);
        var oneUnicodeSnapshotBytes = System.Text.Encoding.UTF8.GetByteCount(new string('界', 100));
        Assert.Equal(300, oneUnicodeSnapshotBytes);
        Assert.True(ProcurementImportPreviewLimits.Admits(2, 0, oneUnicodeSnapshotBytes, limits));
        Assert.False(ProcurementImportPreviewLimits.Admits(3, 0, oneUnicodeSnapshotBytes, limits));
        Assert.True(ProcurementImportPreviewLimits.Admits(0,
            limits.CompanyBytes - oneUnicodeSnapshotBytes, oneUnicodeSnapshotBytes, limits));
        Assert.False(ProcurementImportPreviewLimits.Admits(0,
            limits.CompanyBytes - oneUnicodeSnapshotBytes + 1, oneUnicodeSnapshotBytes, limits));
        Assert.False(ProcurementImportPreviewLimits.Admits(0, 0, 0, limits));
    }

    [Fact]
    public void Configuration_cannot_disable_staging_bounds_or_exceed_hard_ceilings()
    {
        static IConfiguration Config(string actor, string bytes) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Procurement:MasterImport:MaxStagedPerActor"] = actor,
                ["Procurement:MasterImport:MaxStagedCompanyBytes"] = bytes
            }).Build();
        Assert.Null(ProcurementImportPreviewLimits.Read(Config("0", "67108864")));
        Assert.Null(ProcurementImportPreviewLimits.Read(Config("11", "67108864")));
        Assert.Null(ProcurementImportPreviewLimits.Read(Config("invalid", "67108864")));
        Assert.Null(ProcurementImportPreviewLimits.Read(Config("3", "268435457")));
        Assert.NotNull(ProcurementImportPreviewLimits.Read(Config("3", "67108864")));
    }

    [Fact]
    public void Maintenance_credential_cannot_be_an_owner_record_or_core_fallback()
    {
        const string record = "Host=127.0.0.1;Database=erp;Username=record;Password=x";
        const string core = "Host=127.0.0.1;Database=erp;Username=core;Password=x";
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(null, record, core));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(record, record, core));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(core, record, core));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            "Host=127.0.0.1;Database=other;Username=maintenance;Password=x", record, core));
        Assert.True(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            "Host=127.0.0.1;Database=erp;Username=maintenance;Password=x", record, core));
    }

    [Fact]
    public void Maintenance_unix_socket_path_case_is_an_exact_database_boundary()
    {
        const string maintenance = "Host=/private-pg-socket;Port=5432;Database=erp;Username=maintenance";
        const string record = "Host=/private-pg-socket;Port=5432;Database=erp;Username=record";
        const string core = "Host=/private-pg-socket;Port=5432;Database=erp;Username=core";
        Assert.True(ProcurementImportPreviewMaintenance.ConnectionBoundary(maintenance, record, core));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            maintenance, "Host=/PRIVATE-PG-SOCKET;Port=5432;Database=erp;Username=record", core));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            maintenance, record, "Host=/PRIVATE-PG-SOCKET;Port=5432;Database=erp;Username=core"));
        Assert.False(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            maintenance, "Host=/private-pg-socket;Port=5433;Database=erp;Username=record", core));
        Assert.True(ProcurementImportPreviewMaintenance.ConnectionBoundary(
            "Host=PG.EXAMPLE;Port=5432;Database=erp;Username=maintenance",
            "Host=pg.example;Port=5432;Database=erp;Username=record",
            "Host=Pg.Example;Port=5432;Database=erp;Username=core"));
    }

    [Fact]
    public void New_work_health_expires_when_sweeps_stop_or_fail()
    {
        var health = new ProcurementImportMaintenanceHealth();
        var now = DateTimeOffset.UtcNow;
        Assert.False(health.IsHealthy(now));
        health.MarkSuccess(now);
        Assert.True(health.IsHealthy(now.AddMinutes(2)));
        Assert.False(health.IsHealthy(now.AddMinutes(4)));
        health.MarkFailure();
        Assert.False(health.IsHealthy(now));
    }

    [Fact]
    public void Only_persisted_unclaimed_expiration_can_end_uncertain_recovery()
    {
        var batch = new ImportBatch { RecordType = "Vendor", SchemaVersion = 1,
            State = "Previewed", PreviewSnapshotText = "{}",
            PreviewSha256 = new string('a', 64), TotalRows = 1,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) };
        Assert.False(ProcurementMasterImportRoutes.IsTerminalExpired(batch));
        batch.State = "Expired";
        Assert.False(ProcurementMasterImportRoutes.IsTerminalExpired(batch));
        batch.PreviewSnapshotText = null;
        Assert.True(ProcurementMasterImportRoutes.IsTerminalExpired(batch));
        batch.CommitCommandKey = Guid.NewGuid();
        Assert.False(ProcurementMasterImportRoutes.IsTerminalExpired(batch));
        batch.CommitCommandKey = null;
        batch.ResultJson = "{}";
        Assert.False(ProcurementMasterImportRoutes.IsTerminalExpired(batch));
    }
}
