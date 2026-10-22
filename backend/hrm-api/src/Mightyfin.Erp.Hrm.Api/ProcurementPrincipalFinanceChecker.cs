using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementPrincipalFinanceChecker
{
    internal static async Task<ProcurementPrincipalPeople.Person> RequireAsync(PurchaseRequest request,
        string checkerPrincipal,Guid? retainedCheckerAlias,HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        if(request.TenantId!=scope.TenantId || request.LegalEntityId!=scope.LegalEntityId
            || !PurchaseRequestPrincipalIdentity.Valid(request)
            || !ProcurementPrincipalEvidence.OptionalWorker(retainedCheckerAlias))
            throw new InvalidOperationException("PrincipalFinanceOriginalIdentityRequired");
        var checker=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,checkerPrincipal,ct);
        if(checker is null || !checker.Permissions.Contains("procurement-budget-check"))
            throw new InvalidOperationException("FinanceEvidenceVerifierPermissionRevoked");
        if(!checker.OrganisationWide && (request.BranchRef is not { } branch || !checker.AllowedBranches.Contains(branch)))
            throw new InvalidOperationException("FinanceEvidenceVerifierScopeChanged");
        if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,request.CreatedBy,
            request.RequesterCoreWorkerId,checker.PrincipalId,retainedCheckerAlias,ct))
            throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");
        if(request.ApprovalInstanceRef is not null)
        {
            if(!Guid.TryParseExact(request.ApprovalInstanceRef,"D",out var caseId))
                throw new InvalidOperationException("FinanceEvidenceMakerIdentityUnavailable");
            var pin=await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==scope.TenantId
                && x.LegalEntityId==scope.LegalEntityId && x.PurchaseRequestId==request.Id && x.CoreApprovalCaseId==caseId,ct);
            if(pin is null || !PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(pin)
                || pin.MakerSubjectId!=request.CreatedBy || pin.RequesterPrincipalId!=request.RequesterPrincipalId
                || pin.RequesterIdentitySha256!=request.RequesterIdentitySha256 || pin.RequesterWorkerId!=request.RequesterCoreWorkerId
                || pin.SubmittedBusinessVersion!=request.SubmittedVersion)
                throw new InvalidOperationException("FinanceEvidenceMakerIdentityUnavailable");
            if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,pin.MakerSubjectId,
                pin.MakerWorkerId,checker.PrincipalId,retainedCheckerAlias,ct))
                throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");
        }
        return checker;
    }

    internal static async Task<ProcurementPrincipalPeople.Person> RequireCurrentAsync(PurchaseRequest request,
        BudgetCheckSnapshot finance,HrmDbContext core,ProcurementDbContext db,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(finance.CheckerIdentitySchemaVersion!=2 || !FinanceCheckerIdentityEvidence.HasValidBinding(finance))
            throw new InvalidOperationException("FinanceEvidenceCheckerIdentityMissingOrInvalid");
        var checker=await RequireAsync(request,finance.CheckedBySubjectId,finance.CheckedByWorkerId,core,db,scope,ct);
        if(!FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(finance,scope.TenantId,scope.LegalEntityId,
            request.Id,request.SubmittedVersion??request.Version,checker.PrincipalId,checker.CoreLinkedWorkerId))
            throw new InvalidOperationException("FinanceEvidenceCheckerIdentityChanged");
        return checker;
    }
}
