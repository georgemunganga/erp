using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Application;

/// <summary>
/// Proposed port only: no provider, schema, registration or runtime-ready behavior is supplied.
/// The owner adapter must be created on the actual caller's enlisted connection/transaction, never an assumed ambient store.
/// It must prove same physical store/readiness, authorize the exact purpose/current worker/record scope, acquire the agreed
/// lifecycle fence before the SERIALIZABLE snapshot, and use authoritative document/version/scan rows.
/// Missing ownership/enlistment/fence capability must fail closed, not return an in-memory pin as durable evidence.
/// </summary>
public interface IDocumentUsePinStore
{
    /// <summary>
    /// Atomically retain a bounded, nonempty, duplicate-free EXACT set of selections for one use key/revision/purpose.
    /// Reusing a key with any changed reference/selection/actor conflicts. Identical authorized replay returns original pins.
    /// O4 Review and O5 Publication are separate immutable references to the same versions; do not copy content.
    /// No method may scan/network or start/commit an independent transaction inside the caller's transaction.
    /// </summary>
    Task<IReadOnlyList<DocumentUsePin>> AcquireAsync(DocumentOwner owner, DocumentUseReference source,
        IReadOnlyList<DocumentUseSelection> selections, CancellationToken cancellationToken);

    /// <summary>
    /// Authorize current purpose access and return only that reference's retained versions, not the owner's document history.
    /// Do not reconstruct historical evidence from today's availability. No pin implies broad download authorization.
    /// </summary>
    Task<IReadOnlyList<DocumentUsePin>> FindAsync(DocumentOwner owner, DocumentUseReference source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Required coupled managed lifecycle guard, NOT a detached boolean readiness check. Under the same owning transaction
    /// and lifecycle fence, deny retirement/purge whenever retained uses exist. The caller must hold that fence until its
    /// lifecycle mutation commits; a guard call on another context/transaction provides no guarantee. No release/expiry is
    /// implied: default retention continues while uses exist, with policy periods and release authority still unset.
    /// </summary>
    Task RequireNoRetainedUsesAsync(DocumentOwner owner, Guid documentId, CancellationToken cancellationToken);
}
