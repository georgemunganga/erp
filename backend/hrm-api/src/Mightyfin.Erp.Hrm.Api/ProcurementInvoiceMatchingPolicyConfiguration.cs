using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult?> CheckInvoiceMatchingCompanyRules(PolicyVersion policy, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        // Existing governed-policy identity/maker-checker helpers are shared, despite their original receiving name.
        if (!await ReceivingConfigurationActor(core,scope,permission,ct))
            return Problem("matching-authority-unavailable","Your current account cannot configure invoice matching rules.",403);
        var issues=InvoiceMatchingPolicyRules.Validate(policy.RulesJson);
        if(issues.Count != 0) return Results.Json(new { code="invalid-matching-rules", message="Correct the matching rules before review.",issues },statusCode:422);
        var rules=InvoiceMatchingPolicyRules.Parse(policy.RulesJson);
        var currency=await core.LegalEntities.AsNoTracking().Where(x=>x.TenantId==scope.TenantId && x.Id==scope.LegalEntityId && !x.IsArchived)
            .Select(x=>x.Currency).SingleOrDefaultAsync(ct);
        if(currency != rules.CurrencyCode) return Problem("currency-mismatch","Use the selected company's currency.",422);
        foreach(var rule in rules.Rules)
            if(!await db.ProcurementCategories.AsNoTracking().AnyAsync(x=>x.Code==rule.CategoryCode && x.Status=="Active",ct))
                return Problem("matching-category-unavailable","Choose active procurement categories before review.",422);
        return null;
    }
}
