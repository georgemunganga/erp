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
        internal readonly string ProcurementStore=Guid.NewGuid().ToString();
        internal readonly PurchaseRequest Request;
        internal Fixture()
        {
            Core=new(new DbContextOptionsBuilder<HrmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new FixedTenantAccessor("principal-finance"));
            Scope.Set("principal-finance",Company,Checker.ToString("D"),null,null,false,"principal-finance");Scope.AssignPrincipal(Checker);
            Db=new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(ProcurementStore).Options,Scope);
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
    public async Task Authority_creation_resolves_workerless_current_Core_grants_and_rejects_revoked_configuration_access()
    {
        await using var f=await Fixture.Start();
        var role=await f.Core.TenantRoleAssignments.SingleAsync();
        role.PermissionsCsv="procurement-access,procurement-authority-configure";await f.Core.SaveChangesAsync();
        var people=await ProcurementSpendingAuthorityPrincipalIdentity.ResolveCreationActorsAsync(f.Core,f.Scope,f.Maker.ToString("D"),default);
        Assert.Null(people.Actor.WorkerId);Assert.Null(people.Actor.CoreLinkedWorkerId);Assert.Null(people.Recipient.WorkerId);
        role.PermissionsCsv="procurement-access";await f.Core.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementSpendingAuthorityPrincipalIdentity.ResolveCreationActorsAsync(
            f.Core,f.Scope,f.Maker.ToString("D"),default));
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Mixed_case_stored_manager_role_still_requires_actual_current_permission_and_one_complete_finite_authority()
    {
        await using var f=await Fixture.Start();var reviewer=Guid.NewGuid();var now=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        f.Core.LocalUsers.Add(new(){Id=reviewer,Email="reviewer@example.test",NormalizedEmail="REVIEWER@EXAMPLE.TEST",
            DisplayName="Synthetic independent authority reviewer",PasswordHash="not-a-login",RolesCsv="authority_reviewer",IsActive=true});
        f.Core.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment{RoleKey="procurement_manager",RoleName="Synthetic finite manager",PermissionsCsv="procurement-access,procurement-approval-decide",Active=true},
            new TenantRoleAssignment{RoleKey="authority_reviewer",RoleName="Synthetic grant reviewer",PermissionsCsv="procurement-access,procurement-authority-review",Active=true},
            new TenantRoleAssignment{RoleKey="authority_maker",RoleName="Synthetic grant maker",PermissionsCsv="procurement-access,procurement-authority-configure",Active=true});
        (await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Maker)).RolesCsv="authority_maker";
        (await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Checker)).RolesCsv="PROCUREMENT_MANAGER";
        await f.Core.SaveChangesAsync();
        ProcurementRequestScope ScopeFor(Guid principal)
        {
            var scope=new ProcurementRequestScope();scope.Set("principal-finance",f.Company,principal.ToString("D"),null,null,false,"finite-planner");
            scope.AssignPrincipal(principal);return scope;
        }
        ProcurementDbContext Store(Guid principal)=>new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(f.ProcurementStore).Options,ScopeFor(principal));
        SpendingAuthorityConfigurationActor Actor(Guid principal,string permission)=>new("principal-finance",f.Company,principal.ToString("D"),true,new HashSet<string>{permission});
        var subject=new SpendingAuthoritySubject("principal-finance",f.Company,f.Checker.ToString("D"),true,true);
        Guid grantId;
        await using(var create=Store(f.Maker))
        {
            var grant=SpendingAuthorityConfiguration.CreateDraft("FINITE-MANAGER",f.Checker.ToString("D"),
                new("ZMW","EstimatedLineBase",100,now.AddDays(-1),now.AddDays(1),AllBranches:true,AllDepartments:true,AllCategories:true),
                Actor(f.Maker,SpendingAuthorityConfiguration.ConfigurePermission),now,[]);
            var people=await ProcurementSpendingAuthorityPrincipalIdentity.ResolveCreationActorsAsync(f.Core,ScopeFor(f.Maker),f.Checker.ToString("D"),default);
            grantId=grant.Id;create.Add(grant);create.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureCreation(grant,
                people.Actor.CoreLinkedWorkerId,people.Recipient.CoreLinkedWorkerId,2)));
            await create.SaveChangesAsync();
        }
        await using(var submit=Store(f.Maker))
        {
            var grant=await submit.SpendingAuthorityGrants.SingleAsync(x=>x.Id==grantId);
            var origin=await ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(submit,ScopeFor(f.Maker),grant,default);
            SpendingAuthorityConfiguration.Submit(grant,grant.Version,Actor(f.Maker,SpendingAuthorityConfiguration.ConfigurePermission),subject,now.AddMinutes(1));
            submit.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureSubmission(grant,origin,f.Maker.ToString("D"),null,null)));
            await submit.SaveChangesAsync();
        }
        await using(var activate=Store(reviewer))
        {
            var grant=await activate.SpendingAuthorityGrants.SingleAsync(x=>x.Id==grantId);
            var proof=await ProcurementSpendingAuthorityPrincipalIdentity.RequireReviewerAsync(f.Core,activate,ScopeFor(reviewer),grant,default);
            SpendingAuthorityConfiguration.Activate(grant,grant.Version,Actor(reviewer,SpendingAuthorityConfiguration.ReviewPermission),subject,"Synthetic independent review",now.AddMinutes(2),[grant]);
            activate.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureActivation(grant,proof.Created,proof.Submitted,reviewer.ToString("D"),null,null)));
            await activate.SaveChangesAsync();
        }
        f.Request.Lines.Add(new(){TenantId=f.Request.TenantId,LegalEntityId=f.Company,PurchaseRequestId=f.Request.Id,
            LineNumber=1,Description="Supplies",CategoryCode="IT",RequestedQuantity=1,EstimatedUnitPrice=1});
        var policyId=Guid.NewGuid();
        var evaluation=new PolicyEvaluation{PurchaseRequestId=f.Request.Id,RequestVersion=f.Request.SubmittedVersion!.Value,
            PolicyVersionId=policyId,Outcome="Matched",AmountBasis="EstimatedLineBase",DecisionJson=System.Text.Json.JsonSerializer.Serialize(new{
                requestId=f.Request.Id,requestVersion=f.Request.SubmittedVersion.Value,policyId,
                lines=f.Request.Lines.Select(x=>new{lineId=x.Id,decision=new{status="Match",
                    approvalStages=new[]{new{code="FINITE",assignee="ProcurementManager",mode="One"}}}})})};
        var resolved=await ProcurementPrincipalApprovalAssignees.ResolveAsync(f.Request,evaluation,f.Core,f.Db,f.Scope,now.AddMinutes(3),default);
        Assert.Equal(f.Checker.ToString("D"),Assert.Single(Assert.Single(resolved).Assignees).Person.PrincipalId);
        (await f.Core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="procurement_manager")).PermissionsCsv="procurement-access";
        await f.Core.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementPrincipalApprovalAssignees.ResolveAsync(f.Request,evaluation,f.Core,f.Db,f.Scope,now.AddMinutes(3),default));
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
