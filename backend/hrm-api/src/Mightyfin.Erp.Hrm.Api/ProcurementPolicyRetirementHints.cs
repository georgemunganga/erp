using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    // A hint reuses current command ownership/readiness guards; it is never write authorization.
    // Unknown governed identity hides only this action, not the historical policy itself.
    private static async Task<bool> CanRetirePolicyAuthorHint(PolicyVersion policy, ProcurementDbContext db,
        HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        try
        {
            if (IsContractReviewPolicy(policy.PolicyKey))
            {
                var options = http.RequestServices.GetService<ProcurementContractReviewOptions>();
                var readiness = http.RequestServices.GetService<IProcurementContractReviewSchemaReadiness>();
                if (options is not { Enabled: true, ExecutionDatabaseConfigured: true } || readiness is null
                    || !await readiness.CanRecordAsync(db, ct)) return false;
                _ = await ContractPolicyAuthor(db, organizationDb, scope, policy, ct);
            }
            if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            {
                await RequireSourcingIdentitySchema(http, db, ct);
                _ = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
            }
            if (IsOperationalIdentityPolicy(policy.PolicyKey))
            {
                // Nonmutating write-readiness inspection, matching the actual command gate.
                await RequireOperationalIdentitySchema(http, db, true, ct);
                if (policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey) await RequireUnitReviewPolicySchema(http, db, ct);
                _ = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(organizationDb, db, scope,
                    policy, ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
            }
            return true;
        }
        catch (SourcingIdentityProblem) { return false; }
        catch (OperationalIdentityProblem) { return false; }
        catch (InvalidOperationException error) when (error.Message.StartsWith("ContractReviewPolicy", StringComparison.Ordinal)) { return false; }
        catch (InvalidOperationException error) when (error.Message.StartsWith("OperationalPolicy", StringComparison.Ordinal)
            && !ProcurementPostgresFailures.IsStoreFailure(error)) { return false; }
    }
}
