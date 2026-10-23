using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Denial-only person independence checks, reusing owning Core links. These are not
/// authorization: a command must separately resolve the active Core account, named
/// action, current assignment and transaction branch. No endpoint is registered here.
/// Call inside the owning command transaction before persistence, including replay
/// authorization; browser-supplied person or employee evidence is never accepted.
/// </summary>
internal static class ProcurementManualPaymentPeople
{
    internal static async Task<bool> IndependentAssignmentsNow(HrmDbContext core,
        ManualPaymentRequest request, CancellationToken ct)
        => await ProcurementPrincipalPeople.IndependentNow(core, request.TenantId,
            request.CreatorPrincipalId, request.CreatorCoreLinkedWorkerId,
            request.ApproverPrincipalId, request.ApproverCoreLinkedWorkerId, ct)
        && await ProcurementPrincipalPeople.IndependentNow(core, request.TenantId,
            request.RecorderPrincipalId, request.RecorderCoreLinkedWorkerId,
            request.ConfirmerPrincipalId, request.ConfirmerCoreLinkedWorkerId, ct);

    internal static Task<bool> IndependentApprovalNow(HrmDbContext core,
        ManualPaymentRequest request, string actor, CancellationToken ct)
        => actor != request.ApproverPrincipalId ? Task.FromResult(false)
            : ProcurementPrincipalPeople.IndependentNow(core, request.TenantId,
                request.CreatorPrincipalId, request.CreatorCoreLinkedWorkerId,
                actor, request.ApproverCoreLinkedWorkerId, ct);

    internal static Task<bool> IndependentRecordingNow(HrmDbContext core,
        ManualPaymentRequest request, string actor, CancellationToken ct)
        => actor != request.RecorderPrincipalId ? Task.FromResult(false)
            : ProcurementPrincipalPeople.IndependentNow(core, request.TenantId,
                actor, request.RecorderCoreLinkedWorkerId,
                request.ConfirmerPrincipalId, request.ConfirmerCoreLinkedWorkerId, ct);

    internal static async Task<bool> IndependentConfirmationNow(HrmDbContext core,
        ManualPaymentRequest request, string actor, CancellationToken ct)
        => actor == request.ConfirmerPrincipalId
            && ProcurementPrincipalEvidence.Canonical(request.RecordedByPrincipalId)
            && request.RecordedByPrincipalId == request.RecorderPrincipalId
            && await IndependentRecordingNow(core, request, request.RecorderPrincipalId, ct)
            && await ProcurementPrincipalPeople.IndependentNow(core, request.TenantId,
                request.RecordedByPrincipalId, request.RecordedByCoreLinkedWorkerId,
                actor, request.ConfirmerCoreLinkedWorkerId, ct);
}
