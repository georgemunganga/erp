using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class ClamdDocumentScannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 6, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Content = "%PDF-1.7 test evidence"u8.ToArray();
    private static readonly string Hash = Convert.ToHexStringLower(SHA256.HashData(Content));
    private static readonly string Version = "ClamAV 1.5.4/27850/" + Now.AddHours(-1).ToString("ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static ClamdDocumentScanner Client(FakeClamd daemon, TimeSpan? timeout = null) => new(
        new(daemon.Path, TimeZoneInfo.Utc, timeout ?? TimeSpan.FromSeconds(10), TimeSpan.FromHours(48)), new Clock());

    [Fact]
    public async Task Clean_requires_complete_stream_hash_fresh_definitions_and_matching_after_version()
    {
        await using var daemon = new FakeClamd(Version, "stream: OK\0");
        var result = await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, Hash);
        Assert.Equal(ScannerVerdict.Clean, result.Verdict);
        Assert.Equal(Hash, result.Sha256);
        Assert.Equal(Content, daemon.Received);
        Assert.Equal("ClamAV 1.5.4", result.Engine);
        Assert.StartsWith("27850/", result.Definitions);
        Assert.Equal(2, daemon.VersionChecks);
    }

    [Theory]
    [InlineData("stream: Eicar-Test-Signature FOUND\0", ScannerVerdict.Infected)]
    [InlineData("stream: size limit exceeded ERROR\0", ScannerVerdict.Failed)]
    [InlineData("stream: OK\0stream: another ERROR\0", ScannerVerdict.Failed)]
    [InlineData("stream: OK", ScannerVerdict.Failed)]
    [InlineData("stream: OK\n", ScannerVerdict.Failed)]
    [InlineData("unknown response\0", ScannerVerdict.Failed)]
    public async Task Infected_errors_truncated_and_ambiguous_responses_never_release_content(string reply, ScannerVerdict verdict)
    {
        await using var daemon = new FakeClamd(Version, reply);
        var result = await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, Hash);
        Assert.Equal(verdict, result.Verdict);
    }

    [Theory]
    [InlineData("ClamAV 1.5.4")]
    [InlineData("ClamAV 1.5.4/27850/Sun Oct 18 05:00:00 2026")]
    [InlineData("ClamAV 1.5.4/27850/Thu Oct 22 05:00:00 2026")]
    [InlineData("ClamAV 1.5.4/0/Tue Oct 20 05:00:00 2026")]
    [InlineData("ClamAV 1.5.4/27850/bad-date")]
    public async Task Missing_stale_future_and_invalid_definition_evidence_blocks_scanning(string version)
    {
        await using var daemon = new FakeClamd(version, "stream: OK\0");
        var result = await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, Hash);
        Assert.Equal(ScannerVerdict.Failed, result.Verdict);
        Assert.Null(daemon.Received);
    }

    [Fact]
    public async Task Definition_reload_during_scan_blocks_clean_result()
    {
        await using var daemon = new FakeClamd(Version, "stream: OK\0", Version.Replace("27850", "27851"));
        var result = await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, Hash);
        Assert.Equal(ScannerVerdict.Failed, result.Verdict);
        Assert.Equal("scanner-definitions-changed", result.FailureCode);
    }

    [Fact]
    public async Task Altered_content_does_not_accept_clean_scan()
    {
        await using var daemon = new FakeClamd(Version, "stream: OK\0");
        var result = await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, new string('b', 64));
        Assert.Equal(ScannerVerdict.Failed, result.Verdict);
        Assert.Equal("content-hash-mismatch", result.FailureCode);
    }

    [Fact]
    public async Task Overlong_reply_and_unavailable_socket_fail_closed()
    {
        await using var daemon = new FakeClamd(new string('x', 3000), "stream: OK\0");
        Assert.Equal(ScannerVerdict.Failed, (await Client(daemon).ScanAsync(new MemoryStream(Content), Content.Length, Hash)).Verdict);
        var missing = new ClamdDocumentScanner(new("/tmp/no-clamd-" + Guid.NewGuid().ToString("N"),
            TimeZoneInfo.Utc, TimeSpan.FromSeconds(1), TimeSpan.FromHours(48)), new Clock());
        Assert.Equal("scanner-unavailable", (await missing.ScanAsync(new MemoryStream(Content), Content.Length, Hash)).FailureCode);
    }

    [Fact]
    public async Task Timeout_is_failed_but_caller_cancellation_is_propagated()
    {
        await using var daemon = new FakeClamd(Version, "stream: OK\0", delay: true);
        var client = Client(daemon, TimeSpan.FromMilliseconds(150));
        Assert.Equal("scanner-timeout", (await client.ScanAsync(new MemoryStream(Content), Content.Length, Hash)).FailureCode);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ScanAsync(new MemoryStream(Content), Content.Length, Hash, cancellation.Token));
    }

    private sealed class FakeClamd : IAsyncDisposable
    {
        public string Path { get; } = "/tmp/erp-clamd-" + Guid.NewGuid().ToString("N");
        public byte[]? Received { get; private set; }
        public int VersionChecks { get; private set; }
        private readonly Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        private readonly CancellationTokenSource stop = new();
        private readonly Task serving;
        public FakeClamd(string version, string reply, string? afterVersion = null, bool delay = false)
        {
            listener.Bind(new UnixDomainSocketEndPoint(Path));
            listener.Listen(4);
            serving = Serve();
            async Task Serve()
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        using var socket = await listener.AcceptAsync(stop.Token);
                        await using var stream = new NetworkStream(socket);
                        try
                        {
                            var command = new StringBuilder();
                            var one = new byte[1];
                            do { await stream.ReadExactlyAsync(one, stop.Token); if (one[0] != 0) command.Append((char)one[0]); } while (one[0] != 0);
                            if (delay) await Task.Delay(Timeout.Infinite, stop.Token);
                            var response = version + "\0";
                            if (command.ToString() == "zVERSION")
                            {
                                VersionChecks++;
                                response = (VersionChecks > 1 ? afterVersion ?? version : version) + "\0";
                            }
                            else
                            {
                                Assert.Equal("zINSTREAM", command.ToString());
                                var length = new byte[4];
                                using var bytes = new MemoryStream();
                                while (true)
                                {
                                    await stream.ReadExactlyAsync(length, stop.Token);
                                    var count = BinaryPrimitives.ReadUInt32BigEndian(length);
                                    if (count == 0) break;
                                    Assert.InRange(count, 1u, 65536u);
                                    var chunk = new byte[(int)count];
                                    await stream.ReadExactlyAsync(chunk, stop.Token);
                                    bytes.Write(chunk);
                                }
                                Received = bytes.ToArray();
                                response = reply;
                            }
                            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
                        }
                        catch (IOException) { /* Client may reject or cancel before the response. */ }
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (SocketException) when (stop.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Dispose();
            try { await serving; }
            finally { stop.Dispose(); File.Delete(Path); }
        }
    }
}
