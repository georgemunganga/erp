namespace Mightyfin.Erp.Core.Documents.Application;

public sealed record StoredDocumentContent(long SizeBytes, string Sha256, string MediaType);

/// <summary>Private infrastructure port. Never expose this directly to a client.
/// The use case must authorize the module owner and scan state before releasing content.</summary>
public abstract class StagedDocumentContent(StoredDocumentContent content) : IAsyncDisposable
{
    public StoredDocumentContent Content { get; } = content;
    public abstract Task CommitAsync(string generatedKey, CancellationToken cancellationToken = default);
    public abstract ValueTask DisposeAsync();
}

public interface IDocumentBlobStore
{
    Task<StagedDocumentContent> StageAsync(Stream source, long maxFileBytes, CancellationToken cancellationToken = default);
    Task<StoredDocumentContent> StoreQuarantinedAsync(string generatedKey, Stream source,
        long maxFileBytes, CancellationToken cancellationToken = default);
    Task<Stream> OpenPrivateReadAsync(string generatedKey, CancellationToken cancellationToken = default);
}
