using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// The publisher does not start the HTTP host, so it must own its process signals
/// rather than rely on the web host's ConsoleLifetime.
/// </summary>
internal sealed class CorePublisherProcessSignals : IDisposable
{
    private readonly CancellationTokenSource stopping;
    private readonly PosixSignalRegistration? terminate;
    private int disposed;

    public CorePublisherProcessSignals(CancellationTokenSource stopping)
    {
        this.stopping = stopping;
        Console.CancelKeyPress += OnCancelKeyPress;
        AssemblyLoadContext.Default.Unloading += OnUnloading;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                stopping.Cancel();
            });
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        stopping.Cancel();
    }

    private void OnUnloading(AssemblyLoadContext context) => stopping.Cancel();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        terminate?.Dispose();
        Console.CancelKeyPress -= OnCancelKeyPress;
        AssemblyLoadContext.Default.Unloading -= OnUnloading;
    }
}
