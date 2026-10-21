using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Retained business identity, not current authentication. Never resolve an old maker
/// through today's account membership: offboarding must not erase historical independence.</summary>
public static class PurchaseOrderWorkerIdentity
{
    public static string Participants(IReadOnlyList<PurchaseOrderSourceLine> lines, Guid buyerWorker)
    {
        if (buyerWorker == Guid.Empty || lines.Count == 0
            || lines.Any(x => !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(x.Source.Submission)))
            throw new InvalidOperationException("OrderRetainedWorkerIdentityRequired");
        return JsonSerializer.Serialize(lines.SelectMany(x => new[] {
                x.Source.Submission.MakerWorkerId!.Value, x.Source.Submission.RequesterWorkerId!.Value })
            .Append(buyerWorker).Distinct().Order().ToArray());
    }

    public static string Digest(PurchaseOrder order, int revision, Guid coreCase, Guid buyerWorker, string workers) =>
        ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new { schemaVersion = 1,
            order.TenantId, order.LegalEntityId, order.Id, revision, coreCase,
            order.BuyerSubjectId, buyerWorker, workers }));

    public static bool Verify(PurchaseOrderSubmission pin, PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines)
        => pin.BuyerWorkerId != Guid.Empty
            && pin.ParticipantWorkersJson == Participants(lines, pin.BuyerWorkerId)
            && pin.WorkerIdentitySha256 == Digest(order, pin.CommercialRevision, pin.CoreApprovalCaseId,
                pin.BuyerWorkerId, pin.ParticipantWorkersJson);

    public static bool IsIndependent(string retainedWorkers, Guid currentWorker)
    {
        if (currentWorker == Guid.Empty) return false;
        Guid[]? workers;
        try { workers = JsonSerializer.Deserialize<Guid[]>(retainedWorkers); }
        catch (JsonException) { return false; }
        return workers is { Length: > 0 } && workers.All(x => x != Guid.Empty)
            && workers.Distinct().Count() == workers.Length && !workers.Contains(currentWorker);
    }
}

public sealed record PurchaseOrderAssignmentIdentityEvidence(Guid AssigneeWorkerId, ProcurementSpendingAuthorityEvidence Authority);

public static class PurchaseOrderAssignmentIdentity
{
    public static PurchaseOrderAssignmentIdentityEvidence? ReadVerified(PurchaseOrderAssignmentAuthorityEvidence value)
    {
        if (value.AssigneeWorkerId == Guid.Empty || string.IsNullOrWhiteSpace(value.AuthorityEvidenceJson)
            || value.AuthorityEvidenceSha256 != ProcurementEvidenceEncoding.Digest(value.AuthorityEvidenceJson)) return null;
        PurchaseOrderAssignmentIdentityEvidence? retained;
        try { retained = JsonSerializer.Deserialize<PurchaseOrderAssignmentIdentityEvidence>(value.AuthorityEvidenceJson); }
        catch (JsonException) { return null; }
        var authority = retained?.Authority;
        return retained is not null && authority is not null && retained.AssigneeWorkerId == value.AssigneeWorkerId
            && authority.TenantId == value.TenantId && authority.LegalEntityId == value.LegalEntityId
            && authority.PrincipalId == value.PrincipalId && authority.GrantId == value.GrantId
            && authority.GrantRevision == value.GrantRevision && authority.AuthorityVersion == value.AuthorityVersion
            ? retained : null;
    }
}
