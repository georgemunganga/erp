using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed record ClamdScannerOptions(string SocketPath, TimeZoneInfo DefinitionTimeZone,
    TimeSpan Timeout, TimeSpan MaxDefinitionAge);

/// <summary>Local Unix socket only. No caller-provided endpoint or path is sent to ClamD.</summary>
public sealed class ClamdDocumentScanner : IDocumentScanner
{
    private readonly ClamdScannerOptions options;
    private readonly TimeProvider clock;
    public ClamdDocumentScanner(ClamdScannerOptions options, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(options.SocketPath) || !Path.IsPathFullyQualified(options.SocketPath)
            || options.DefinitionTimeZone is null || options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(5)
            || options.MaxDefinitionAge < TimeSpan.FromHours(1) || options.MaxDefinitionAge > TimeSpan.FromDays(7))
            throw new ArgumentException("A local socket, definition timezone, bounded timeout and freshness limit are required.");
        this.options = options;
        this.clock = clock ?? TimeProvider.System;
    }

    public async Task<DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (expectedBytes is < 1 or > 100 * 1024 * 1024 || expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("A bounded content length and SHA-256 are required.");
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        var ct = deadline.Token;
        var observedHash = expectedSha256.ToLowerInvariant();
        var engine = "clamd";
        string? definitions = null;
        DocumentScanResult Failed(string code) => new(ScannerVerdict.Failed, observedHash, engine, definitions, code);
        try
        {
            var before = await Command("VERSION", ct);
            if (!TryVersion(before, out engine, out definitions, out var definitionTime)) return Failed("scanner-definitions-unavailable");
            var age = clock.GetUtcNow() - definitionTime;
            if (age > options.MaxDefinitionAge || age < -TimeSpan.FromMinutes(15)) return Failed("scanner-definitions-stale");
            await using var connection = await Connect(ct);
            await connection.WriteAsync("zINSTREAM\0"u8.ToArray(), ct);
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            string reply;
            try
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var length = new byte[4];
                long total = 0;
                while (true)
                {
                    var count = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (count == 0) break;
                    if (count > expectedBytes - total) return Failed("content-length-mismatch");
                    total += count;
                    hash.AppendData(buffer, 0, count);
                    BinaryPrimitives.WriteUInt32BigEndian(length, (uint)count);
                    await connection.WriteAsync(length, ct);
                    await connection.WriteAsync(buffer.AsMemory(0, count), ct);
                }
                observedHash = Convert.ToHexStringLower(hash.GetHashAndReset());
                if (total != expectedBytes || !observedHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    return Failed("content-hash-mismatch");
                await connection.WriteAsync(new byte[4], ct);
                reply = await ReadReply(connection, ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
            if (before != await Command("VERSION", ct)) return Failed("scanner-definitions-changed");
            // Do not accept ERROR, size-limit responses, truncated or ambiguous records as a clean result.
            if (reply == "stream: OK") return new(ScannerVerdict.Clean, observedHash, engine, definitions);
            if (reply.StartsWith("stream: ", StringComparison.Ordinal) && reply.EndsWith(" FOUND", StringComparison.Ordinal)
                && reply.Length > "stream:  FOUND".Length)
                return new(ScannerVerdict.Infected, observedHash, engine, definitions);
            return Failed("scanner-response-invalid");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failed("scanner-timeout"); }
        catch (SocketException) { return Failed("scanner-unavailable"); }
        catch (IOException) { return Failed("scanner-io-error"); }
        catch (InvalidDataException) { return Failed("scanner-response-invalid"); }
    }

    private async Task<string> Command(string command, CancellationToken ct)
    {
        await using var connection = await Connect(ct);
        await connection.WriteAsync(Encoding.ASCII.GetBytes("z" + command + "\0"), ct);
        return await ReadReply(connection, ct);
    }

    private async Task<NetworkStream> Connect(CancellationToken ct)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(options.SocketPath), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch { socket.Dispose(); throw; }
    }

    private static async Task<string> ReadReply(Stream connection, CancellationToken ct)
    {
        var bytes = new byte[2048];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await connection.ReadAsync(bytes.AsMemory(count, 1), ct);
            if (read == 0) throw new InvalidDataException("Scanner reply ended without a record terminator.");
            if (bytes[count] == 0)
            {
                var trailing = new byte[1];
                if (await connection.ReadAsync(trailing, ct) != 0)
                    throw new InvalidDataException("Scanner returned more than one reply record.");
                return Encoding.ASCII.GetString(bytes, 0, count);
            }
            if (bytes[count] is < 32 or > 126) throw new InvalidDataException("Scanner reply is not a bounded ASCII record.");
            count++;
        }
        throw new InvalidDataException("Scanner reply exceeded the record limit.");
    }

    private bool TryVersion(string response, out string engine, out string? definitions, out DateTimeOffset time)
    {
        engine = "clamd"; definitions = null; time = default;
        var parts = response.Split('/');
        if (parts.Length != 3 || !parts[0].StartsWith("ClamAV ", StringComparison.Ordinal) || parts[0].Length > 100
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var serial) || serial == 0
            || !DateTime.TryParseExact(parts[2], "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var local)) return false;
        try
        {
            time = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), options.DefinitionTimeZone));
        }
        catch (ArgumentException) { return false; }
        engine = parts[0];
        definitions = serial.ToString(CultureInfo.InvariantCulture) + "/" + time.ToString("O", CultureInfo.InvariantCulture);
        return true;
    }
}
