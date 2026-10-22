using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

// Real owning Core projection over InMemory, not HTTP/PG permission or concurrency proof.
public sealed class ProcurementPrincipalFinanceCheckerTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly Guid Company=Guid.NewGuid(),Maker=Guid.NewGuid(),Checker=Guid.NewGuid();
        internal readonly HrmDbContext Core;
        internal readonly ProcurementRequestScope Scope=new();
        internal readonly ProcurementDbContext Db;
        internal readonly PurchaseRequest Request;
        internal Fixture()
        {
            Core=new(new DbContextOptionsBuilder<HrmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new FixedTenantAccessor("principal-finance"));
            Scope.Set("principal-finance",Company,Checker.ToString("D"),null,null,false,"principal-finance");Scope.AssignPrincipal(Checker);
            Db=new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,Scope);
            Request=new(){TenantId="principal-finance",LegalEntityId=Company,Status="Draft",Version=1,SourceChannel="ERP",
                CreatedBy=Maker.ToString("D"),RequesterPrincipalId=Maker};
            PurchaseRequestPrincipalIdentity.CaptureNew(Request,Maker.ToString("D"),null);
            Request.Status="Submitted";Request.SubmittedVersion=2;Request.Version=2;
        }
        internal static async Task<Fixture> Start()
        {
            var f=new Fixture();f.Core.LegalEntities.Add(new(){Id=f.Company,Code="TEST",RegisteredName="Synthetic",Currency="ZMW"});
            f.Core.TenantRoleAssignments.Add(new(){RoleKey="finance_checker",RoleName="Synthetic finite checker",PermissionsCsv="procurement-access,procurement-budget-check",Active=true});
            foreach(var principal in new[]{f.Maker,f.Checker})f.Core.LocalUsers.Add(new(){Id=principal,Email=principal+"@example.test",
                NormalizedEmail=principal.ToString("N").ToUpperInvariant()+"@EXAMPLE.TEST",DisplayName="Synthetic",PasswordHash="not-a-login",RolesCsv="finance_checker",IsActive=true});
            await f.Core.SaveChangesAsync();return f;
        }
        internal Task<ProcurementPrincipalPeople.Person> Check(Guid? retained=null)=>ProcurementPrincipalFinanceChecker.RequireAsync(Request,Checker.ToString("D"),retained,Core,Db,Scope,default);
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await Core.DisposeAsync();}
    }

    [Fact]
    public async Task Current_finite_Core_checker_without_employee_is_eligible_but_stored_permission_revocation_denies()
    {
        await using var f=await Fixture.Start();var checker=await f.Check();Assert.Null(checker.WorkerId);Assert.Null(checker.CoreLinkedWorkerId);
        (await f.Core.TenantRoleAssignments.SingleAsync()).PermissionsCsv="procurement-access";await f.Core.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Check());Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("inactive")][InlineData("locked")][InlineData("archived")][InlineData("samePrincipal")]
    public async Task Ineligible_current_Core_checker_never_uses_a_worker_fallback(string fault)
    {
        await using var f=await Fixture.Start();var user=await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Checker);
        if(fault=="inactive")user.IsActive=false;if(fault=="locked")user.LockedUntil=DateTimeOffset.UtcNow.AddDays(1);if(fault=="archived")user.IsArchived=true;
        await f.Core.SaveChangesAsync();
        if(fault=="samePrincipal")await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementPrincipalFinanceChecker.RequireAsync(
            f.Request,f.Maker.ToString("D"),null,f.Core,f.Db,f.Scope,default));
        else await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Check());
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Retained_checker_alias_still_denies_after_current_link_is_cleared_and_maker_account_is_offboarded()
    {
        await using var f=await Fixture.Start();var alias=Guid.NewGuid();
        var maker=await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Maker);maker.WorkerId=alias;maker.IsActive=false;
        await f.Core.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Check(alias));
        Assert.False(f.Db.ChangeTracker.HasChanges());
        Assert.Empty(await f.Db.AuditEvents.ToListAsync());
    }
}
