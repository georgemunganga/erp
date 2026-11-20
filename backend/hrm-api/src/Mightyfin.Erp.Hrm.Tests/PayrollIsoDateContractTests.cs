using System.Globalization;
using System.Reflection;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed class PayrollIsoDateContractTests
{
    public class RepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
            "ListProfilesAsync" => Task.FromResult(new List<WorkerPayrollProfile>{new(){EffectiveFrom=new DateOnly(2026,11,1)}}),
            "ListPeriodsAsync" => Task.FromResult(new List<PayPeriod>{new(){StartDate=new(2026,11,1),EndDate=new(2026,11,30),CutoffDate=new(2026,11,25),PayDate=new(2026,11,30)}}),
            _=>throw new InvalidOperationException("Unexpected repository call: "+method?.Name)};
    }
    private sealed class Auth : IAuthzService {
        public string CurrentSubjectId=>"synthetic-payroll";
        public void RequireAnyRole(params string[] roles) {} public bool IsRole(params string[] roles)=>true;public bool CanAccessSensitive(string category)=>true;
    }
    [Theory][InlineData("en-US")][InlineData("en-GB")][InlineData("fr-FR")][InlineData("ar-EG")]
    public async Task Public_payroll_date_contract_is_ISO_under_every_host_culture(string culture)
    {
        var prior=CultureInfo.CurrentCulture;try {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            var service=new PayrollServiceImpl(DispatchProxy.Create<IPayrollRepository,RepositoryProxy>(),new Auth(),null!);
            var profile=Assert.Single(await service.ListProfilesAsync(null,default));Assert.Equal("2026-11-01",profile.EffectiveFrom);
            var period=Assert.Single(await service.ListPeriodsAsync(Guid.NewGuid(),default));Assert.Equal("2026-11-01",period.StartDate);Assert.Equal("2026-11-30",period.EndDate);Assert.Equal("2026-11-25",period.CutoffDate);Assert.Equal("2026-11-30",period.PayDate);
        }finally{CultureInfo.CurrentCulture=prior;}
    }
}
