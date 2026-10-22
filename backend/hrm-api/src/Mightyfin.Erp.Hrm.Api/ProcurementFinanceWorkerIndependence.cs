using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Worker independence is distinct from account and permission checks. No new identity master.</summary>
internal static class ProcurementFinanceWorkerIndependence
{
    internal static async Task RequireAsync(PurchaseRequest request, Guid verifierWorker,
        HrmDbContext core, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (request.TenantId != scope.TenantId || request.LegalEntityId != scope.LegalEntityId
            || verifierWorker == Guid.Empty || request.RequesterWorkerId is null || request.RequesterWorkerId == Guid.Empty
            || verifierWorker == request.RequesterWorkerId)
            throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");

        if (request.RequesterPrincipalId is not null && !PurchaseRequestOwnership.HasLegacyWorkerEvidenceIdentity(request))
            throw new InvalidOperationException("PrincipalFinanceEvidenceRequired");

        if (request.ApprovalInstanceRef is not null)
        {
            if (!Guid.TryParseExact(request.ApprovalInstanceRef, "D", out var caseId))
                throw new InvalidOperationException("FinanceEvidenceMakerIdentityUnavailable");
            var pin = await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.PurchaseRequestId == request.Id && x.CoreApprovalCaseId == caseId, ct);
            RequireRetainedMaker(request, pin, verifierWorker, scope);
            return; // Historical maker need not retain an active login account.
        }

        // Existing ERP capture fixes requester to the actual maker worker and has no
        // on-behalf input. Never infer this equivalence for legacy/import/on-behalf rows.
        if (request.SourceChannel != "ERP" || !string.IsNullOrWhiteSpace(request.OnBehalfOfSubjectId))
            throw new InvalidOperationException("FinanceEvidenceMakerIdentityUnavailable");
        var maker = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, request.CreatedBy, ct);
        // Check a currently resolvable maker too (including a rebound account); the
        // retained requester remains authoritative after original maker offboarding.
        if (maker?.WorkerId == verifierWorker)
            throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");
    }

    // The caller loads this pin from the owning scoped store; a digest alone is not authority.
    internal static void RequireRetainedMaker(PurchaseRequest request, PurchaseRequestApprovalSubmission? pin,
        Guid verifierWorker, ProcurementRequestScope scope)
    {
        if (pin is null || pin.TenantId != scope.TenantId || pin.LegalEntityId != scope.LegalEntityId
            || request.TenantId != scope.TenantId || request.LegalEntityId != scope.LegalEntityId
            || pin.PurchaseRequestId != request.Id || pin.CoreApprovalCaseId.ToString("D") != request.ApprovalInstanceRef
            || pin.MakerSubjectId != request.CreatedBy || pin.RequesterWorkerId != request.RequesterWorkerId
            || pin.SubmittedBusinessVersion != request.SubmittedVersion
            || !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin))
            throw new InvalidOperationException("FinanceEvidenceMakerIdentityUnavailable");
        if (verifierWorker == Guid.Empty || verifierWorker == pin.MakerWorkerId || verifierWorker == pin.RequesterWorkerId)
            throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");
    }

}
