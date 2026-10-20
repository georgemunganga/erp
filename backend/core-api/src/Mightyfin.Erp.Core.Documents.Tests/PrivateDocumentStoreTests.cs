using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class PrivateDocumentStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "erp-doc-store-" + Guid.NewGuid().ToString("N"));
    private static string Key() => $"{Guid.NewGuid():N}/{Guid.NewGuid():N}";

    [Fact]
    public async Task Streams_content_computes_hash_and_preserves_first_version_on_duplicate_write()
    {
        var store = new PrivateDocumentStore(root);
        var key = Key();
        var content = Encoding.UTF8.GetBytes("%PDF-1.7\noriginal evidence");
        var result = await store.StoreQuarantinedAsync(key, new ByteAtATimeStream(content), 100);
        Assert.Equal(content.Length, result.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), result.Sha256);
        Assert.Equal("application/pdf", result.MediaType);
        await Assert.ThrowsAsync<IOException>(() => store.StoreQuarantinedAsync(key,
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF-replacement")), 100));
        await using var read = await store.OpenPrivateReadAsync(key);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        Assert.Equal(content, copy.ToArray());
        Assert.Empty(Directory.GetFiles(root, ".upload-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("MZ-disguised-executable", 100)]
    [InlineData("%PDF-too-large", 5)]
    public async Task Invalid_or_oversized_input_leaves_no_stored_or_partial_file(string content, long max)
    {
        var store = new PrivateDocumentStore(root);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.StoreQuarantinedAsync(Key(),
            new MemoryStream(Encoding.UTF8.GetBytes(content)), max));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Read_failure_removes_the_partial_upload()
    {
        var store = new PrivateDocumentStore(root);
        await Assert.ThrowsAsync<IOException>(() => store.StoreQuarantinedAsync(Key(), new FailedReadStream(), 100));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Detects_supported_image_signatures_from_bytes()
    {
        var store = new PrivateDocumentStore(root);
        var png = await store.StoreQuarantinedAsync(Key(), new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]), 100);
        var jpeg = await store.StoreQuarantinedAsync(Key(), new MemoryStream([255, 216, 255, 224]), 100);
        Assert.Equal("image/png", png.MediaType);
        Assert.Equal("image/jpeg", jpeg.MediaType);
    }

    [Fact]
    public void Refuses_a_directory_accessible_to_other_users()
    {
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        Assert.Throws<InvalidOperationException>(() => new PrivateDocumentStore(root));
    }

    [Theory]
    [InlineData("../../outside")]
    [InlineData("/absolute/path")]
    [InlineData("invoice.pdf")]
    [InlineData("00000000000000000000000000000000/00000000000000000000000000000000")]
    public async Task Client_paths_cannot_be_used_as_storage_keys(string key)
    {
        var store = new PrivateDocumentStore(root);
        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreQuarantinedAsync(key,
            new MemoryStream("%PDF-test"u8.ToArray()), 100));
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenPrivateReadAsync(key));
    }

    [Fact]
    public async Task Cancelled_upload_does_not_leave_partial_evidence()
    {
        var store = new PrivateDocumentStore(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.StoreQuarantinedAsync(Key(),
            new MemoryStream("%PDF-test"u8.ToArray()), 100, cancellation.Token));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Symbolic_link_cannot_redirect_document_writes()
    {
        if (OperatingSystem.IsWindows()) return;
        var store = new PrivateDocumentStore(root);
        var key = Key();
        var directory = Path.Combine(root, key.Split('/')[0]);
        Directory.CreateSymbolicLink(directory, Path.GetTempPath());
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.StoreQuarantinedAsync(key,
                new MemoryStream("%PDF-test"u8.ToArray()), 100));
        }
        finally { Directory.Delete(directory); }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    private sealed class ByteAtATimeStream(byte[] content) : MemoryStream(content)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }
    private sealed class FailedReadStream : MemoryStream
    {
        private bool started;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (started) throw new IOException("Simulated source interruption");
            started = true;
            "%PDF-"u8.CopyTo(buffer.Span);
            return ValueTask.FromResult(5);
        }
    }
}
