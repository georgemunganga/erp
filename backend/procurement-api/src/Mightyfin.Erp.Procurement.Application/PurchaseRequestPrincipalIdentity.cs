using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Original Core account attribution only. This grants no approval, authority or funding.
/// Only new server-owned ERP requests may capture it; old rows are never adopted/backfilled.</summary>
public static class PurchaseRequestPrincipalIdentity
{
    public static void CaptureNew(PurchaseRequest request, string principal, Guid? knownCoreAlias)
    {
        if (request.CreatedAt != default || request.Version != 1 || request.Status != "Draft" || request.SourceChannel != "ERP"
            || !string.IsNullOrWhiteSpace(request.OnBehalfOfSubjectId) || request.RequesterPrincipalId?.ToString("D") != principal
            || request.CreatedBy != principal || !ProcurementPrincipalEvidence.Canonical(principal)
            || !ProcurementPrincipalEvidence.OptionalWorker(knownCoreAlias) || request.RequesterIdentitySchemaVersion != 0
            || request.RequesterCoreWorkerId is not null || request.RequesterIdentitySha256 is not null)
            throw new InvalidOperationException("PrincipalRequestOriginalIdentityRequired");
        request.RequesterIdentitySchemaVersion = 1;
        request.RequesterCoreWorkerId = knownCoreAlias;
        request.RequesterIdentitySha256 = Digest(request);
    }
    public static bool Valid(PurchaseRequest request) => request.RequesterIdentitySchemaVersion == 1
        && request.Id != Guid.Empty && !string.IsNullOrWhiteSpace(request.TenantId) && request.LegalEntityId != Guid.Empty
        && request.RequesterPrincipalId is { } principal && principal != Guid.Empty
        && request.CreatedBy == principal.ToString("D") && ProcurementPrincipalEvidence.Canonical(request.CreatedBy)
        && request.SourceChannel == "ERP" && string.IsNullOrWhiteSpace(request.OnBehalfOfSubjectId)
        && ProcurementPrincipalEvidence.OptionalWorker(request.RequesterCoreWorkerId)
        && request.RequesterIdentitySha256 == Digest(request);
    public static string Digest(PurchaseRequest request) => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new
    { schemaVersion=1, request.Id, request.TenantId, request.LegalEntityId, request.RequesterPrincipalId,
        request.CreatedBy, request.RequesterCoreWorkerId, request.SourceChannel }));
}
