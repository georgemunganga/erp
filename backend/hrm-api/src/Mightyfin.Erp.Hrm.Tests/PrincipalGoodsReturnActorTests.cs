using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

// Real current Core role/branch resolution and named authorization policies over
// InMemory. This does not establish physical P37/HTTP/login acceptance.
public sealed class PrincipalGoodsReturnActorTests
{
    private const string Tenant="p37-actor-source";
    private sealed record Accessor : ITenantAccessor { public string GetTenantId()=>Tenant; }
    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly HrmDbContext Core=new(new DbContextOptionsBuilder<HrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new Accessor());
        internal readonly Guid Company=Guid.NewGuid(),Branch=Guid.NewGuid(),OtherBranch=Guid.NewGuid(),Principal=Guid.NewGuid();
        internal readonly ServiceProvider Services;
        internal readonly DefaultHttpContext Http=new();
        internal Fixture()
        {
            var services=new ServiceCollection();services.AddLogging();
            services.AddAuthorization(ProcurementPermissions.AddPolicies);
            Services=services.BuildServiceProvider();Http.RequestServices=Services;
            Http.User=new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim("tenant",Tenant),
                new Claim(ClaimTypes.NameIdentifier,Principal.ToString("D")),
                new Claim(ClaimTypes.Role,"p37_finite"),
                new Claim(ProcurementPermissions.ConfiguredClaim,"true"),
                new Claim(ProcurementPermissions.ActionClaim,"procurement-access"),
                new Claim(ProcurementPermissions.ActionClaim,"procurement-return-create")},"source-unit"));
        }
        internal async Task Start()
        {
            Core.LegalEntities.Add(new LegalEntity{Id=Company,TenantId=Tenant,Code="P37",RegisteredName="Synthetic company",Currency="ZMW"});
            Core.WorkLocations.AddRange(new WorkLocation{Id=Branch,TenantId=Tenant,LegalEntityId=Company,Code="A",Name="A"},
                new WorkLocation{Id=OtherBranch,TenantId=Tenant,LegalEntityId=Company,Code="B",Name="B"});
            Core.TenantRoleAssignments.Add(new TenantRoleAssignment{TenantId=Tenant,RoleKey="p37_finite",RoleName="Synthetic finite return",
                PermissionsCsv="procurement-access,procurement-return-create",Active=true});
            Core.LocalUsers.Add(new LocalUser{Id=Principal,TenantId=Tenant,Email=Principal+"@example.test",NormalizedEmail=(Principal+"@example.test").ToUpperInvariant(),
                DisplayName="Synthetic Core principal",PasswordHash="not-a-login",RolesCsv="p37_finite",IsActive=true});
            Core.UserBranchAssignments.Add(new HrUserBranchAssignment{TenantId=Tenant,UserId=Principal,LocationId=Branch});
            await Core.SaveChangesAsync();
        }
        internal ProcurementRequestScope Scope(Guid branch,Guid? knownAlias=null)
        {
            var scope=new ProcurementRequestScope();scope.Set(Tenant,Company,Principal.ToString("D"),branch,null,true,"p37-test");
            scope.AssignPrincipal(Principal,knownAlias);return scope;
        }
        internal Task<Mightyfin.Erp.Procurement.Application.PrincipalReceivingActor> Resolve(ProcurementRequestScope scope)
            =>ProcurementPrincipalReceivingActor.CurrentAsync(Core,scope,Http,Services.GetRequiredService<IAuthorizationService>(),"procurement-return-create",default);
        public async ValueTask DisposeAsync(){await Core.DisposeAsync();await Services.DisposeAsync();}
    }

    [Fact]
    public async Task Current_finite_Core_principal_can_use_return_action_without_employee_and_stale_claim_cannot_survive_revocation()
    {
        await using var f=new Fixture();await f.Start();
        var actor=await f.Resolve(f.Scope(f.Branch));
        Assert.Equal(f.Principal.ToString("D"),actor.PrincipalId);Assert.Null(actor.WorkerAliasId);
        Assert.Equal(new[]{f.Branch},actor.AllowedBranches);
        Assert.Empty(await f.Core.Workers.ToArrayAsync());
        (await f.Core.TenantRoleAssignments.SingleAsync()).PermissionsCsv="procurement-access";
        await f.Core.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Resolve(f.Scope(f.Branch)));
    }

    [Fact]
    public async Task Return_read_hints_drop_revoked_edit_even_when_the_old_edit_claim_remains()
    {
        await using var f=new Fixture();await f.Start();
        var role=await f.Core.TenantRoleAssignments.SingleAsync();
        role.PermissionsCsv="procurement-access,procurement-return-create,procurement-return-edit";
        await f.Core.SaveChangesAsync();
        var identity=Assert.IsType<ClaimsIdentity>(f.Http.User.Identity);
        identity.AddClaim(new Claim(ProcurementPermissions.ActionClaim,"procurement-return-edit"));
        var head=new Mightyfin.Erp.Procurement.Domain.PrincipalGoodsReturnDraft{
            TenantId=Tenant,LegalEntityId=f.Company,BranchId=f.Branch,
            CreatorPrincipalId=f.Principal.ToString("D")};
        var actor=await f.Resolve(f.Scope(f.Branch));
        Assert.Equal(new[]{"Edit","Cancel"},ProcurementPrincipalReceivingRoutes.ReturnAllowedActions(head,actor,true));
        role.PermissionsCsv="procurement-access,procurement-return-create";
        await f.Core.SaveChangesAsync();
        actor=await f.Resolve(f.Scope(f.Branch));
        Assert.Empty(ProcurementPrincipalReceivingRoutes.ReturnAllowedActions(head,actor,true));
        Assert.True(identity.HasClaim(ProcurementPermissions.ActionClaim,"procurement-return-edit"));
        Assert.Empty(await f.Core.Workers.ToArrayAsync());
    }

    [Fact]
    public async Task Current_return_permission_does_not_replace_the_required_authenticated_tenant()
    {
        await using var f=new Fixture();await f.Start();
        var identity=Assert.IsType<ClaimsIdentity>(f.Http.User.Identity);
        identity.RemoveClaim(Assert.Single(identity.FindAll("tenant")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Resolve(f.Scope(f.Branch)));
    }

    [Fact]
    public async Task Other_selected_branch_or_fabricated_known_worker_link_does_not_grant_return_authority()
    {
        await using var f=new Fixture();await f.Start();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Resolve(f.Scope(f.OtherBranch)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Resolve(f.Scope(f.Branch,Guid.NewGuid())));
        Assert.Empty(await f.Core.Workers.ToArrayAsync());
    }
}
