using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementMasterImportDiagnosticTests
{
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
