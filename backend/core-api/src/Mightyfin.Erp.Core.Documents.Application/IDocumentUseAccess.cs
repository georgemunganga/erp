using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Application;

public enum DocumentUseOperation { Acquire, Read, Download }

/// <summary>
/// Owning-module authorization of the CURRENT canonical actor for an exact consuming
/// revision/purpose and version set. A retained pin or generic owner Read is not authority.
/// Implementations must deny unsupported owner/source tuples; no default implementation exists.
/// </summary>
public interface IDocumentUseAccess
{
    /// <summary>
    /// Read with an EMPTY versionIds list means authorize this exact owner/reference/revision/purpose
    /// before its retained set is looked up. It is a required reference-level phase, not a request
    /// for all owner documents or permission to download. The store calls Read again with the exact
    /// loaded retained IDs (also empty when no use exists); authorize that set independently.
    /// Acquire always supplies a nonempty exact selection. Download is separate and must require
    /// nonempty exact retained IDs; metadata Read never substitutes for it.
    /// </summary>
    Task RequireAsync(DocumentOwner owner, DocumentUseReference source, DocumentUseOperation operation,
        IReadOnlyList<Guid> versionIds, CancellationToken cancellationToken);
}
