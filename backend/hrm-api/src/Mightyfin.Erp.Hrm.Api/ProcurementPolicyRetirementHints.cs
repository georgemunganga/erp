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
            if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            {
                await RequireSourcingIdentitySchema(http, db, ct);
                _ = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
            }
            if (IsOperationalIdentityPolicy(policy.PolicyKey))
            {
                // Nonmutating write-readiness inspection, matching the actual command gate.
                await RequireOperationalIdentitySchema(http, db, true, ct);
                _ = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(organizationDb, db, scope,
                    policy, ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
            }
            return true;
        }
        catch (SourcingIdentityProblem) { return false; }
        catch (OperationalIdentityProblem) { return false; }
        catch (InvalidOperationException error) when (error.Message.StartsWith("OperationalPolicy", StringComparison.Ordinal)
            && !ProcurementPostgresFailures.IsStoreFailure(error)) { return false; }
    }
}
