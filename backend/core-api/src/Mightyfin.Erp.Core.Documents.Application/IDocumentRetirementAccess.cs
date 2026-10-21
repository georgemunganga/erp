namespace Mightyfin.Erp.Core.Documents.Application;

/// <summary>
/// Owning module must authorize the CURRENT canonical actor and exact scoped business
/// owner/document/revision for retirement. Read or Upload is never retirement authority.
/// No default implementation exists. The managed writer creates this authorizer against
/// its actual enlisted owner context; implementations must not use detached authorization stores.
/// </summary>
public interface IDocumentRetirementAccess
{
    Task RequireAsync(DocumentOwner owner, Guid documentId, long expectedDocumentRevision, CancellationToken ct);
}
