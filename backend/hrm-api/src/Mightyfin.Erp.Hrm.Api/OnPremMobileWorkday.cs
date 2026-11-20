using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static partial class OnPremMobileApi
{
    private static Task<IResult> Workday(HttpContext h,Guid attendance,bool write)
    {
        async Task<MobileWorkdayScope?> Authority(HrmDbContext? transaction,CancellationToken ct){
            if(!Guid.TryParseExact(h.Request.Headers["X-Workplace-Company"],"D",out var company)
                || Company(h,company) is not {} allowed || Bearer(h) is not {} token)return null;
            var owned=transaction is null?Contexts(h)(allowed.Tenant):null;
            try{
                var db=transaction??owned!;var hash=LocalSessionTokens.Hash(token);
                var session=await db.LocalMobileSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.AccessHash==hash && x.LegalEntityId==company,ct);
                if(session is null || session.AccessExpiresAt<=Now(h) || await Admit(db,allowed,session,h) is not {} grant)return null;
                return new(allowed.Tenant,company,grant.Worker.Id,grant.User.Id,grant.Company.TimeZoneId);
            }finally{if(owned is not null)await owned.DisposeAsync();}
        }
        return MobileWorkdayApi.Execute(h,attendance,write,Contexts(h),Authority);
    }
}
