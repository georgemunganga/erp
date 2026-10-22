using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Hrm.Api;
internal static class ProcurementInvoiceUnitReviewPeople
{
    internal static async Task<InvoiceUnitReviewPolicyRules.Person?> Resolve(HrmDbContext core, string tenant, Guid company, string principal, CancellationToken ct)
    {
        var person = await ProcurementPrincipalPeople.Resolve(core, tenant, company, principal, ct);
        return person is null ? null : new(tenant, company, person.PrincipalId, person.WorkerId, true,
            person.Permissions, person.OrganisationWide, person.AllowedBranches);
    }
}
