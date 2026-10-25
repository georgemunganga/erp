using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Expression and provider SQL-generation tests, not SQL execution/current
/// Core authorization or positive runtime proof. Named membership never grants an action.</summary>
public sealed class ProcurementPrincipalReceivingAssignedReadTests
{
    private static readonly string Inspector=Guid.NewGuid().ToString("D"),Acceptor=Guid.NewGuid().ToString("D"),Owner=Guid.NewGuid().ToString("D");
    private static readonly Guid Branch=Guid.NewGuid();
    private static PrincipalReceivingRule Rule(ReceivingKind kind)=>new("RULE","SITE",kind==ReceivingKind.Goods?"GOODS":"SERVICE",kind,
        new HashSet<Guid>{Branch},kind==ReceivingKind.Goods,kind==ReceivingKind.Goods?"CHECK":null,
        kind==ReceivingKind.Goods?new HashSet<string>{Inspector}:new HashSet<string>(),kind==ReceivingKind.Goods?new HashSet<string>{Acceptor}:new HashSet<string>(),
        kind==ReceivingKind.Service?new HashSet<string>{Owner}:new HashSet<string>(),false,false,false,true,"REF:");
    [Fact]
    public void Exact_named_task_classification_and_site_are_required_before_count_predicate()
    {
        var source=new PrincipalReceivingSourceState{BranchId=Branch,CategoryCode="SITE",PurchaseType="GOODS",Kind="Goods"};
        var row=new ProcurementPrincipalReceivingRoutes.AssignedCandidate{Source=source};
        var inspection=ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Goods)],Inspector,"Inspection").Compile();
        Assert.True(inspection(row));
        Assert.False(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Goods)],Acceptor,"Inspection").Compile()(row));
        Assert.False(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Goods)],Inspector,"Acceptance").Compile()(row));
        Assert.False(inspection(new(){Source=new(){BranchId=Guid.NewGuid(),CategoryCode="SITE",PurchaseType="GOODS",Kind="Goods"}}));
        Assert.False(inspection(new(){Source=new(){BranchId=Branch,CategoryCode="OTHER",PurchaseType="GOODS",Kind="Goods"}}));
        Assert.False(inspection(new(){Source=new(){BranchId=Branch,CategoryCode="SITE",PurchaseType="SERVICE",Kind="Service"}}));
    }
    [Fact]
    public void Service_owner_is_an_explicit_Core_principal_without_any_employee_or_inspector_fallback()
    {
        var row=new ProcurementPrincipalReceivingRoutes.AssignedCandidate{Source=new(){BranchId=Branch,CategoryCode="SITE",PurchaseType="SERVICE",Kind="Service"}};
        Assert.True(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Service)],Owner,"Service").Compile()(row));
        Assert.False(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Service)],Inspector,"Service").Compile()(row));
        Assert.False(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([],Owner,"Service").Compile()(row));
    }
    [Fact]
    public void Actual_Npgsql_query_generation_keeps_owned_revision_source_and_branch_fences_before_paging()
    {
        var principal=Guid.NewGuid();var company=Guid.NewGuid();var scope=new ProcurementRequestScope();
        scope.Set("receiving-sql",company,principal.ToString("D"),Branch,null,true,"fixture");scope.AssignPrincipal(principal);
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql("Host=127.0.0.1;Port=1;Database=never_open;Username=fixture").Options,scope);
        var actor=new PrincipalReceivingActor(principal.ToString("D"),null,scope.TenantId,company,new HashSet<Guid>{Branch},new HashSet<string>{ControlledReceiving.InspectPermission});
        var sql=ProcurementPrincipalReceivingRoutes.BoundDraftLines(db,scope,actor)
            .Where(ProcurementPrincipalReceivingRoutes.AssignedRulePredicate([Rule(ReceivingKind.Goods)],Inspector,"Inspection"))
            .OrderBy(x=>x.Head.Id).Skip(2).Take(5).ToQueryString();
        Assert.Contains("principal_receiving_line_bindings",sql);Assert.Contains("principal_receiving_revisions",sql);
        Assert.Contains("principal_receiving_sources",sql);Assert.Contains("principal_order_source_bindings",sql);
        Assert.Contains("branch_id",sql);Assert.Contains("category_code",sql);Assert.Contains("purchase_type",sql);
        Assert.Contains("core_approval.cases",sql);Assert.Contains("LIMIT",sql);Assert.Contains("OFFSET",sql);
    }
    [Fact]
    public void Principal_directory_orders_scoped_heads_before_paging_with_stable_ties()
    {
        var earlier=new DateTimeOffset(2026,10,24,9,0,0,TimeSpan.Zero);
        var later=earlier.AddMinutes(1);
        var a=new PrincipalReceivingDraftHead{Id=Guid.Parse("00000000-0000-4000-8000-000000000001"),BranchId=Branch,CreatedAt=earlier};
        var b=new PrincipalReceivingDraftHead{Id=Guid.Parse("00000000-0000-4000-8000-000000000002"),BranchId=Branch,CreatedAt=later};
        var c=new PrincipalReceivingDraftHead{Id=Guid.Parse("00000000-0000-4000-8000-000000000003"),BranchId=Branch,CreatedAt=later};
        var denied=new PrincipalReceivingDraftHead{Id=Guid.Parse("00000000-0000-4000-8000-000000000004"),BranchId=Guid.NewGuid(),CreatedAt=later.AddHours(1)};
        var scoped=new[]{c,denied,a,b}.AsQueryable().Where(x=>x.BranchId==Branch);
        Assert.Equal(3,scoped.Count());
        Assert.Equal(new[]{b.Id,c.Id,a.Id},ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,"newest").Select(x=>x.Id));
        Assert.Equal(new[]{a.Id,b.Id,c.Id},ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,"oldest").Select(x=>x.Id));
        Assert.Equal(new[]{c.Id,a.Id},ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,"newest").Skip(1).Take(2).Select(x=>x.Id));
        Assert.Equal(new[]{b.Id,c.Id,a.Id},ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,null).Select(x=>x.Id));
        Assert.False(ProcurementPrincipalReceivingRoutes.SupportedWorklistSort("number"));
        Assert.False(ProcurementPrincipalReceivingRoutes.SupportedWorklistSort("oldest;drop"));
    }
    [Fact]
    public void Principal_directory_sql_orders_after_existing_issued_and_branch_scope()
    {
        var principal=Guid.NewGuid();var company=Guid.NewGuid();var scope=new ProcurementRequestScope();
        scope.Set("receiving-order-sql",company,principal.ToString("D"),Branch,null,true,"fixture");scope.AssignPrincipal(principal);
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql("Host=127.0.0.1;Port=1;Database=never_open;Username=fixture").Options,scope);
        var actor=new PrincipalReceivingActor(principal.ToString("D"),null,scope.TenantId,company,new HashSet<Guid>{Branch},new HashSet<string>{ControlledReceiving.InspectPermission});
        var scoped=ProcurementPrincipalReceivingRoutes.BoundDraftLines(db,scope,actor).Select(x=>x.Head).Distinct();
        var newest=ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,"newest").Skip(2).Take(5).ToQueryString();
        var oldest=ProcurementPrincipalReceivingRoutes.OrderWorklist(scoped,"oldest").Skip(2).Take(5).ToQueryString();
        foreach(var sql in new[]{newest,oldest})
        {
            Assert.Contains("principal_order_source_bindings",sql);
            Assert.Contains("branch_id",sql);
            Assert.Contains("ORDER BY",sql);
            Assert.Contains("LIMIT",sql);
            Assert.Contains("OFFSET",sql);
        }
        Assert.Contains("DESC",newest);
        Assert.DoesNotContain("DESC",oldest);
    }
}
