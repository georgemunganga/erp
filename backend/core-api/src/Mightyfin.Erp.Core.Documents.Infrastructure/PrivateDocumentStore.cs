using System.Buffers;
using System.Security.Cryptography;
using Mightyfin.Erp.Core.Documents.Application;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>Generated keys only. The configured root must be private, persistent and outside the web root.</summary>
public sealed class PrivateDocumentStore : IDocumentBlobStore
{
    private readonly string root;
    public PrivateDocumentStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Path.IsPathFullyQualified(rootDirectory))
            throw new ArgumentException("An absolute private document directory is required.");
        root = Path.GetFullPath(rootDirectory);
        EnsurePrivateDirectory(root);
    }

    public async Task<StoredDocumentContent> StoreQuarantinedAsync(string generatedKey, Stream source,
        long maxFileBytes, CancellationToken cancellationToken = default)
    {
        _ = Resolve(generatedKey);
        await using var staged = await StageAsync(source, maxFileBytes, cancellationToken);
        await staged.CommitAsync(generatedKey, cancellationToken);
        return staged.Content;
    }

    public async Task<StagedDocumentContent> StageAsync(Stream source, long maxFileBytes,
        CancellationToken cancellationToken = default)
    {
        if (maxFileBytes is < 1 or > 100 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        RejectLink(root);
        var temporary = Path.Combine(root, ".upload-" + Guid.NewGuid().ToString("N"));
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        var header = new byte[8];
        var headerLength = 0;
        long size = 0;
        var retained = false;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var output = new FileStream(temporary, options))
            {
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    if (read > maxFileBytes - size) throw new InvalidDataException("The document exceeds the file size limit.");
                    var headerBytes = Math.Min(read, header.Length - headerLength);
                    buffer.AsSpan(0, headerBytes).CopyTo(header.AsSpan(headerLength));
                    headerLength += headerBytes;
                    size += read;
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (size == 0) throw new InvalidDataException("Choose a file that is not empty.");
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            var content = new StoredDocumentContent(size, Convert.ToHexStringLower(hash.GetHashAndReset()),
                DetectMediaType(header.AsSpan(0, headerLength)));
            cancellationToken.ThrowIfCancellationRequested();
            retained = true;
            return new Staged(this, temporary, content);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            if (!retained && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed class Staged(PrivateDocumentStore store, string temporary, StoredDocumentContent content)
        : StagedDocumentContent(content)
    {
        private bool disposed;
        public override async Task CommitAsync(string generatedKey, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            var target = store.Resolve(generatedKey);
            EnsurePrivateDirectory(Path.GetDirectoryName(target)!);
            try { File.Move(temporary, target, overwrite: false); }
            catch (IOException) when (File.Exists(target))
            {
                // Resume only identical immutable content; never replace an existing version.
                await using var existing = await store.OpenPrivateReadAsync(generatedKey, cancellationToken);
                if (existing.Length != Content.SizeBytes || !Convert.ToHexStringLower(await SHA256.HashDataAsync(existing, cancellationToken))
                    .Equals(Content.Sha256, StringComparison.Ordinal))
                    throw new IOException("This document version already has different content.");
            }
        }
        public override ValueTask DisposeAsync()
        {
            disposed = true;
            if (File.Exists(temporary)) File.Delete(temporary);
            return ValueTask.CompletedTask;
        }
    }

    public Task<Stream> OpenPrivateReadAsync(string generatedKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(generatedKey);
        RejectLink(Path.GetDirectoryName(path)!);
        RejectLink(path);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    private string Resolve(string key)
    {
        var parts = key?.Split('/');
        if (parts is not { Length: 2 } || !Guid.TryParseExact(parts[0], "N", out var document)
            || !Guid.TryParseExact(parts[1], "N", out var version) || document == Guid.Empty || version == Guid.Empty)
            throw new ArgumentException("A generated document version key is required.");
        RejectLink(root);
        return Path.Combine(root, document.ToString("N"), version.ToString("N"));
    }

    private static void EnsurePrivateDirectory(string path)
    {
        RejectLink(path);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, mode);
            if ((File.GetUnixFileMode(path) & ~mode) != 0)
                throw new InvalidOperationException("The document directory must be accessible only to its service owner.");
        }
    }

    private static void RejectLink(string path)
    {
        if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
            throw new InvalidOperationException("Document storage cannot use symbolic links.");
    }

    private static string DetectMediaType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("%PDF-"u8)) return "application/pdf";
        if (header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (header.Length >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255) return "image/jpeg";
        throw new InvalidDataException("Choose a PDF, PNG or JPEG document.");
    }
}
