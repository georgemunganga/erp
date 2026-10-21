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
    Task RequireAsync(DocumentOwner owner, DocumentUseReference source, DocumentUseOperation operation,
        IReadOnlyList<Guid> versionIds, CancellationToken cancellationToken);
}
