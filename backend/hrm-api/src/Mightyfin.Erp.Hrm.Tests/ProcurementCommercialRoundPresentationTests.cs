using System.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundPresentationTests
{
    [Theory]
    [InlineData(false,true,true,true,true,"Draft",true,true,true,true)]
    [InlineData(true,false,true,true,true,"Draft",true,true,true,true)]
    [InlineData(true,true,false,true,true,"Draft",true,true,true,true)]
    [InlineData(true,true,true,false,true,"Draft",true,true,true,true)]
    [InlineData(true,true,true,true,true,"Cancelled",true,true,true,true)]
    [InlineData(true,true,true,true,true,"Draft",false,true,true,true)]
    public void Technical_scope_state_and_historical_view_never_invent_write_authority(bool ready,bool branch,
        bool draft,bool current,bool enrolled,string? state,bool untouched,bool holds,bool manage,bool cancel)
        =>Assert.Empty(ProcurementCommercialRoundActions.ForRecord(ready,branch,draft,current,enrolled,state,untouched,holds,manage,cancel));
    [Fact]
    public void Every_explicit_version_route_is_read_only_including_the_latest_snapshot()
    {
        Assert.True(ProcurementCommercialRoundActions.IsEditableRoute(null));
        Assert.False(ProcurementCommercialRoundActions.IsEditableRoute(1));
        Assert.False(ProcurementCommercialRoundActions.IsEditableRoute(long.MaxValue));
        Assert.Empty(ProcurementCommercialRoundActions.ForRecord(true,true,true,
            ProcurementCommercialRoundActions.IsEditableRoute(3),true,"Draft",true,true,true,true));
    }
    [Fact]
    public void Incomplete_source_create_requires_manage_and_verified_current_holds()
    {
        Assert.Equal(["Create"],ProcurementCommercialRoundActions.ForRecord(true,true,true,true,false,null,true,true,true,false));
        Assert.Empty(ProcurementCommercialRoundActions.ForRecord(true,true,true,true,false,null,true,true,false,true));
        Assert.Empty(ProcurementCommercialRoundActions.ForRecord(true,true,true,true,false,null,true,false,true,true));
    }
    [Fact]
    public void Cancel_only_actor_can_cancel_but_cannot_revise_or_create()
    {
        Assert.True(ProcurementCommercialRoundCapability.HasRecordAction(new HashSet<string>{"procurement-round-plan-cancel"}));
        Assert.Equal(["Cancel"],ProcurementCommercialRoundActions.ForRecord(true,true,true,true,true,"Draft",true,false,false,true));
        Assert.False(ProcurementCommercialRoundCapability.HasRecordAction(new HashSet<string>{"procurement-round-plan-read"}));
    }
    [Fact]
    public void Revised_plan_actions_are_current_and_keep_cancel_independent_of_changed_hold_eligibility()
    {
        Assert.Equal(["Revise","Cancel"],ProcurementCommercialRoundActions.ForRecord(true,true,true,true,true,"Draft",true,true,true,true));
        Assert.Equal(["Cancel"],ProcurementCommercialRoundActions.ForRecord(true,true,true,true,true,"Draft",true,false,true,true));
        Assert.Equal(["Revise"],ProcurementCommercialRoundActions.ForRecord(true,true,true,true,true,"Draft",true,true,true,false));
    }
    [Theory]
    [InlineData("/api/procurement/v1/sourcing/round-plan-sources","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/round-plan-sources","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/people","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/people","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/people/extra","GET",false)]
    public void Round_only_entry_and_picker_are_exact_read_paths_under_Company_All(string path,string method,bool expected)
        =>Assert.Equal(expected,ProcurementSourcingExecutionRoutes.RoundPlanAllReadPath(new Microsoft.AspNetCore.Http.PathString(path),method));
}

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public void Round_people_cursor_and_branch_projection_translate_without_private_user_or_HR_fields_or_connection()
    {
        const string connection="Host=127.0.0.1;Port=1;Database=round_people_translation_only;Username=unused";
        var scope=new ProcurementRequestScope();var branch=Guid.NewGuid();
        scope.Set("translation-tenant",Guid.NewGuid(),Guid.NewGuid().ToString("D"),branch,null,false,"translation-only");
        using var core=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options,
            new FixedTenantAccessor(scope.TenantId));
        var query=ProcurementSourcingExecutionRoutes.RoundPeopleProjection(core,scope,DateTimeOffset.UtcNow,branch,Guid.NewGuid(),
            ProcurementSourcingDiscoveryQueries.Paging(1,25,"supplier_%"));
        var sql=query.Take(101).ToQueryString();
        Assert.Contains("local_users",sql);Assert.Contains("display_name",sql);Assert.Contains("hr_user_branch_assignments",sql);
        // The shared Core no-declared=OrganisationWide alternative must survive
        // alongside the assigned-target-branch predicate; no HR home inference.
        Assert.Contains("NOT EXISTS",sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR EXISTS",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workers",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password_hash",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles_csv",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payroll",sql,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConnectionState.Closed,core.Database.GetDbConnection().State);
    }
}
