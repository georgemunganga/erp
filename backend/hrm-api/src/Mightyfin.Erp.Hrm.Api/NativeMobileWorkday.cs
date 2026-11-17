using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static partial class NativeAttendanceApi
{
    private static Task<IResult> Workday(HttpContext h,Guid attendance,bool write)
    {
        async Task<MobileWorkdayScope?> Authority(HrmDbContext? _,CancellationToken ct){
            if(!Canonical(h.Request.Headers["X-Kuubu-Company"].ToString(),out var company)
                || !Canonical(h.Request.Headers["X-Kuubu-Link"].ToString(),out var link))return null;
            var identity=await Identity(h);
            if(identity.Status!=OperationalSubjectStatus.Verified || identity.Observation is null){
                if(identity.Status!=OperationalSubjectStatus.Denied)throw new MobileWorkdayAccessException(503);
                return null;
            }
            var result=await h.RequestServices.GetRequiredService<NativeEmployeeAdmission>().ReadAsync(identity.Observation,company,link,ct);
            if(result.Status!=200 || result.Grant is not {} grant)throw new MobileWorkdayAccessException(result.Status==503?503:403);
            return new(grant.Scope.TenantId,company,grant.Worker.Id,grant.Scope.PrincipalId,grant.TimeZone);
        }
        return MobileWorkdayApi.Execute(h,attendance,write,
            tenant=>h.RequestServices.GetRequiredService<NativeEmployeeAdmission>().WorkdayContexts(tenant),Authority);
    }
}
