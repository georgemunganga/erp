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

    [Theory]
    [InlineData("matching")][InlineData("wrong-department")][InlineData("ambiguous")][InlineData("no-grant")]
    public async Task Selected_department_and_mixed_case_role_require_exact_finite_authority_without_granting_access(string condition)
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
        var department=Guid.NewGuid();var otherDepartment=Guid.NewGuid();var branch=Guid.NewGuid();
        f.Core.WorkLocations.Add(new(){Id=branch,Code="BRANCH",Name="Synthetic selected branch",LegalEntityId=f.Company});
        f.Core.OrgUnits.AddRange(new OrgUnit{Id=department,Code="DEPT",Name="Synthetic selected department",LegalEntityId=f.Company,Status="active"},
            new OrgUnit{Id=otherDepartment,Code="OTHER",Name="Other department",LegalEntityId=f.Company,Status="active"});
        await f.Core.SaveChangesAsync();
        ProcurementRequestScope ScopeFor(Guid principal)
        {
            var scope=new ProcurementRequestScope();scope.Set("principal-finance",f.Company,principal.ToString("D"),null,null,false,"finite-planner");
            scope.AssignPrincipal(principal);return scope;
        }
        ProcurementDbContext Store(Guid principal)=>new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(f.ProcurementStore).Options,ScopeFor(principal));
        SpendingAuthorityConfigurationActor Actor(Guid principal,string permission)=>new("principal-finance",f.Company,principal.ToString("D"),true,new HashSet<string>{permission});
        async Task SeedAuthority(Guid recipient,string reference)
        {
            var subject=new SpendingAuthoritySubject("principal-finance",f.Company,recipient.ToString("D"),true,true);
            Guid grantId;
            await using(var create=Store(f.Maker))
            {
                var grant=SpendingAuthorityConfiguration.CreateDraft(reference,recipient.ToString("D"),
                    new("ZMW","EstimatedLineBase",100,now.AddDays(-1),now.AddDays(1),AllBranches:true,AllDepartments:false,DepartmentRef:department,AllCategories:true),
                    Actor(f.Maker,SpendingAuthorityConfiguration.ConfigurePermission),now,[]);
                var people=await ProcurementSpendingAuthorityPrincipalIdentity.ResolveCreationActorsAsync(f.Core,ScopeFor(f.Maker),recipient.ToString("D"),default);
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
        }
        var selected=new ProcurementRequestScope();selected.Set("principal-finance",f.Company,f.Checker.ToString("D"),branch,department,true,"selected-department");
        selected.AssignPrincipal(f.Checker);f.Request.BranchRef=branch;f.Request.DepartmentRef=department;
        f.Request.Lines.Add(new(){TenantId=f.Request.TenantId,LegalEntityId=f.Company,PurchaseRequestId=f.Request.Id,
            LineNumber=1,Description="Supplies",CategoryCode="IT",RequestedQuantity=1,EstimatedUnitPrice=1});
        var policyId=Guid.NewGuid();
        var evaluation=new PolicyEvaluation{PurchaseRequestId=f.Request.Id,RequestVersion=f.Request.SubmittedVersion!.Value,
            PolicyVersionId=policyId,Outcome="Matched",AmountBasis="EstimatedLineBase",DecisionJson=System.Text.Json.JsonSerializer.Serialize(new{
                requestId=f.Request.Id,requestVersion=f.Request.SubmittedVersion.Value,policyId,
                lines=f.Request.Lines.Select(x=>new{lineId=x.Id,decision=new{status="Match",
                    approvalStages=new[]{new{code="FINITE",assignee="ProcurementManager",mode="One"}}}})})};
        Task<IReadOnlyList<ProcurementPrincipalApprovalStage>> Resolve()=>ProcurementPrincipalApprovalAssignees.ResolveAsync(
            f.Request,evaluation,f.Core,f.Db,selected,now.AddMinutes(3),default);
        if(condition=="no-grant")
        {
            Assert.Equal("ApprovalAssigneeAuthorityUnavailable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve())).Message);
            Assert.False(f.Db.ChangeTracker.HasChanges());return;
        }
        await SeedAuthority(f.Checker,"FINITE-MANAGER");
        var resolved=await Resolve();
        Assert.Equal(f.Checker.ToString("D"),Assert.Single(Assert.Single(resolved).Assignees).Person.PrincipalId);
        if(condition=="wrong-department")
        {
            f.Request.DepartmentRef=otherDepartment;
            Assert.Equal("ApprovalAssigneeAuthorityUnavailable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve())).Message);
        }
        else if(condition=="ambiguous")
        {
            var second=Guid.NewGuid();f.Core.LocalUsers.Add(new(){Id=second,Email="second@example.test",NormalizedEmail="SECOND@EXAMPLE.TEST",
                DisplayName="Synthetic second finite manager",PasswordHash="not-a-login",RolesCsv="procurement_manager",IsActive=true});
            await f.Core.SaveChangesAsync();await SeedAuthority(second,"FINITE-SECOND");
            Assert.Equal("ApprovalAssigneeAmbiguous",(await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve())).Message);
        }
        else
        {
            (await f.Core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="procurement_manager")).PermissionsCsv="procurement-access";
            await f.Core.SaveChangesAsync();
            Assert.Equal("ApprovalAssigneeAuthorityUnavailable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve())).Message);
        }
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("matching-no-finite-authority")][InlineData("current-link-changed")]
    [InlineData("employee-inactive")][InlineData("workerless")]
    public async Task LineManager_uses_retained_Core_alias_and_real_current_HR_mapping_without_granting_authority(string condition)
    {
        await using var f=await Fixture.Start();var worker=Guid.NewGuid();var manager=Guid.NewGuid();var other=Guid.NewGuid();var branch=Guid.NewGuid();
        f.Core.WorkLocations.Add(new(){Id=branch,Code="LM",Name="Synthetic line manager branch",LegalEntityId=f.Company});
        f.Core.Workers.AddRange(
            new Worker{Id=worker,EmployeeNo="LM-MAKER",FirstName="Maker",LastName="Fixture",Status="active",LocationId=branch,ManagerId=manager},
            new Worker{Id=manager,EmployeeNo="LM-MANAGER",FirstName="Manager",LastName="Fixture",Status="active",LocationId=branch},
            new Worker{Id=other,EmployeeNo="LM-OTHER",FirstName="Other",LastName="Fixture",Status="active",LocationId=branch,ManagerId=manager});
        (await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Maker)).WorkerId=condition=="workerless"?null:worker;
        (await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Checker)).WorkerId=manager;
        (await f.Core.TenantRoleAssignments.SingleAsync()).PermissionsCsv="procurement-access,procurement-approval-decide";
        await f.Core.SaveChangesAsync();
        // Active employment is not original identity. This source retains the real
        // Core alias even when the old active-HR snapshot is absent.
        var request=new PurchaseRequest{TenantId="principal-finance",LegalEntityId=f.Company,CreatedBy=f.Maker.ToString("D"),
            RequesterPrincipalId=f.Maker,RequesterWorkerId=null,SourceChannel="ERP",Status="Draft",Version=1,BranchRef=branch,CurrencyCode="ZMW"};
        PurchaseRequestPrincipalIdentity.CaptureNew(request,f.Maker.ToString("D"),condition=="workerless"?null:worker);
        request.Status="Submitted";request.SubmittedVersion=2;request.Version=2;
        request.Lines.Add(new(){TenantId=request.TenantId,LegalEntityId=f.Company,PurchaseRequestId=request.Id,
            LineNumber=1,Description="Synthetic HR strategy source",RequestedQuantity=1,EstimatedUnitPrice=1,CategoryCode="IT"});
        if(condition=="current-link-changed") (await f.Core.LocalUsers.SingleAsync(x=>x.Id==f.Maker)).WorkerId=other;
        if(condition=="employee-inactive") (await f.Core.Workers.SingleAsync(x=>x.Id==worker)).Status="inactive";
        await f.Core.SaveChangesAsync();
        var policyId=Guid.NewGuid();var evaluation=new PolicyEvaluation{PurchaseRequestId=request.Id,RequestVersion=2,PolicyVersionId=policyId,
            Outcome="Matched",AmountBasis="EstimatedLineBase",DecisionJson=System.Text.Json.JsonSerializer.Serialize(new{
                requestId=request.Id,requestVersion=2,policyId,lines=request.Lines.Select(x=>new{lineId=x.Id,decision=new{status="Match",
                    approvalStages=new[]{new{code="HR_MANAGER",assignee="LineManager",mode="One"}}}})})};
        var before=await f.Db.AuditEvents.CountAsync();
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementPrincipalApprovalAssignees.ResolveAsync(
            request,evaluation,f.Core,f.Db,f.Scope,DateTimeOffset.UtcNow,default));
        Assert.Equal(condition=="matching-no-finite-authority"?"ApprovalAssigneeAuthorityUnavailable":"ApprovalLineManagerUnavailable",error.Message);
        Assert.False(f.Db.ChangeTracker.HasChanges());Assert.Equal(before,await f.Db.AuditEvents.CountAsync());
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
