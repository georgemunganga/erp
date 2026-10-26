using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementMasterImportDiagnosticTests
{
    private const string UnixRecord = "Host=/private-pg-socket;Port=5432;Database=proc_m2a;Username=record";
    private const string UnixCore = "Host=/private-pg-socket;Port=5432;Database=proc_m2a;Username=core";

    [Fact]
    public void Configured_unix_socket_endpoint_survives_one_open_one_closed_connection()
    {
        using var closedCore = new NpgsqlConnection(UnixCore);
        Assert.Null(closedCore.Host);
        Assert.Equal(0, closedCore.Port);

        // Npgsql exposes Host/Port only through its connector after Open.
        // The real Item catalog filter opens Record while Core remains closed.
        Assert.True(ProcurementMasterImportRoutes.SameDatabaseEndpoints(
            UnixRecord, true, "/private-pg-socket", 5432,
            closedCore.ConnectionString, false, closedCore.Host, closedCore.Port));
        Assert.True(ProcurementMasterImportRoutes.SameDatabaseEndpoints(
            UnixRecord, false, null, 0,
            UnixCore, true, "/private-pg-socket", 5432));
        Assert.True(ProcurementMasterImportRoutes.SameDatabaseEndpoints(
            UnixRecord, false, null, 0, UnixCore, false, null, 0));
    }

    [Fact]
    public void Endpoint_boundary_denies_other_database_server_port_or_observed_connector()
    {
        static bool Same(string core, string? observedHost = "/private-pg-socket",
            int observedPort = 5432) => ProcurementMasterImportRoutes.SameDatabaseEndpoints(
            UnixRecord, true, observedHost, observedPort, core, false, null, 0);

        Assert.False(Same("Host=/private-pg-socket;Port=5432;Database=other;Username=core"));
        Assert.False(Same("Host=/other-socket;Port=5432;Database=proc_m2a;Username=core"));
        Assert.False(Same("Host=/private-pg-socket;Port=5433;Database=proc_m2a;Username=core"));
        Assert.False(Same(UnixCore, "/other-socket"));
        Assert.False(Same(UnixCore, observedPort: 5433));
        Assert.False(Same("not a connection string"));
    }

    [Fact]
    public void Unexpected_preview_failure_logs_only_bounded_diagnostic_fields()
    {
        var capture = new CaptureProvider();
        using var services = new ServiceCollection().AddLogging(builder =>
            builder.SetMinimumLevel(LogLevel.Debug).AddProvider(capture)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "private-trace-01" };
        var secret = "csv-row-and-database-detail-SECRET";

        ProcurementMasterImportRoutes.LogFailure(http, "Item",
            ProcurementMasterImportRoutes.ImportStage.PreviewAddAndSave,
            new InvalidOperationException(secret));
        ProcurementMasterImportRoutes.LogFailure(http, "Item-from-request-SECRET",
            ProcurementMasterImportRoutes.ImportStage.ReadyCorePrivileges,
            new InvalidOperationException(secret));

        Assert.Equal(2, capture.Events.Count);
        Assert.All(capture.Events, text =>
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Item-from-request-SECRET", text, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", text, StringComparison.Ordinal);
            Assert.Contains("private-trace-01", text, StringComparison.Ordinal);
        });
        Assert.Contains("PreviewAddAndSave", capture.Events[0], StringComparison.Ordinal);
        Assert.Contains("Kind: Unknown", capture.Events[1], StringComparison.Ordinal);
    }

    private sealed class CaptureProvider : ILoggerProvider
    {
        public List<string> Events { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Events);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(List<string> events) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception); // Passing the exception would leak its message through sinks.
            events.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
