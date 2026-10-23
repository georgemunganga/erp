using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementPrincipalReceivingPolicy
{
    internal static async Task<PrincipalReceivingPolicy> CurrentAsync(ProcurementDbContext db,ProcurementRequestScope scope,
        IServiceProvider services,PrincipalReceivingSource source,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;
        var candidates=await db.PolicyVersions.Where(x=>x.PolicyKey=="RECEIVING"&&x.State=="Published"
            &&x.EffectiveFrom<=now&&(x.EffectiveTo==null||now<x.EffectiveTo)).OrderByDescending(x=>x.PolicyVersionNumber).Take(2).ToArrayAsync(ct);
        // Never select a second/older policy merely because the current one cannot
        // prove publication or happens not to cover this source.
        if(candidates.Length!=1)throw new InvalidOperationException("PrincipalReceivingEffectivePolicyUnavailable");
        var policy=candidates[0];
        _=await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,services,policy,ct);
        return PrincipalReceivingPolicyRules.Select(policy,source,now);
    }
    internal static async Task<PrincipalReceivingRule> RuleForNewSourceAsync(ProcurementDbContext db,
        ProcurementRequestScope scope,IServiceProvider services,string category,string purchaseType,Guid branch,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;
        var candidates=await db.PolicyVersions.Where(x=>x.PolicyKey=="RECEIVING"&&x.State=="Published"
            &&x.EffectiveFrom<=now&&(x.EffectiveTo==null||now<x.EffectiveTo)).OrderByDescending(x=>x.PolicyVersionNumber).Take(2).ToArrayAsync(ct);
        if(candidates.Length!=1)throw new InvalidOperationException("PrincipalReceivingEffectivePolicyUnavailable");
        var policy=candidates[0];_=await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,services,policy,ct);
        return PrincipalReceivingPolicyRules.Parse(policy.RulesJson).Rules.SingleOrDefault(x=>x.CategoryCode==category
            &&x.PurchaseType==purchaseType&&x.SiteIds.Contains(branch))??throw new InvalidOperationException("PrincipalReceivingClassificationNotConfigured");
    }
}
