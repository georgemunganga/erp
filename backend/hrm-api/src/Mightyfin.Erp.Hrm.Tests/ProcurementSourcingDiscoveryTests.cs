using Microsoft.EntityFrameworkCore;
using Npgsql;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementSourcingDiscoveryTests
{
    [Fact]
    public void Paging_is_bounded_and_search_is_literal_not_a_wildcard_language()
    {
        var defaults = ProcurementSourcingDiscoveryQueries.Paging(null, null, null);
        Assert.Equal((1, 25, 0, ""), (defaults.Number, defaults.Size, defaults.Offset, defaults.Search));
        var page = ProcurementSourcingDiscoveryQueries.Paging(3, 5, "  50%_\\path  ");
        Assert.Equal(10, page.Offset);
        Assert.Equal("%50\\%\\_\\\\path%", page.Pattern);
        Assert.Equal(2499975, ProcurementSourcingDiscoveryQueries.Paging(100000, 25, "").Offset);
    }

    [Theory]
    [InlineData(0, 25, "")][InlineData(100001, 25, "")][InlineData(1, 0, "")][InlineData(1, 26, "")]
    [InlineData(1, 25, "internal\tcontrol")]
    public void Invalid_page_or_control_search_denies(int page, int size, string search)
        => Assert.Throws<ArgumentException>(() => ProcurementSourcingDiscoveryQueries.Paging(page, size, search));

    [Fact]
    public void Search_size_is_bounded_before_database_work()
        => Assert.Throws<ArgumentException>(() => ProcurementSourcingDiscoveryQueries.Paging(1, 25, new string('x', 101)));

    [Fact]
    public void Explicit_branch_context_filters_company_wide_readers_before_count_and_page()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var requests = new[] { new PurchaseRequest { BranchRef = a }, new PurchaseRequest { BranchRef = b }, new PurchaseRequest() }.AsQueryable();
        var drafts = new[] { new SourcingEvent { BranchRef = a }, new SourcingEvent { BranchRef = b }, new SourcingEvent() }.AsQueryable();
        var actor = Actor(a, true, []);
        Assert.Equal(1, ProcurementSourcingDiscoveryQueries.ScopedRequests(requests, actor).Count());
        Assert.Equal(a, ProcurementSourcingDiscoveryQueries.ScopedDrafts(drafts, actor).Single().BranchRef);
        Assert.Empty(ProcurementSourcingDiscoveryQueries.ScopedRequests(requests, Actor(a, false, [b])));
        Assert.Empty(ProcurementSourcingDiscoveryQueries.ScopedDrafts(drafts, Actor(a, false, [b])));
    }

    [Fact]
    public void No_selected_branch_does_not_turn_membership_into_organisation_scope()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var requests = new[] { new PurchaseRequest { BranchRef = a }, new PurchaseRequest { BranchRef = b }, new PurchaseRequest() }.AsQueryable();
        Assert.Equal(a, ProcurementSourcingDiscoveryQueries.ScopedRequests(requests, Actor(null, false, [a])).Single().BranchRef);
        Assert.Empty(ProcurementSourcingDiscoveryQueries.ScopedRequests(requests, Actor(null, false, [])));
        Assert.Equal(3, ProcurementSourcingDiscoveryQueries.ScopedRequests(requests, Actor(null, true, [])).Count());
    }

    [Fact]
    public void Candidate_query_uses_retained_source_and_actual_core_tables_before_limit()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("rfq-query", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "query");
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=not_opened;Username=unused;Password=unused").Options, scope);
        var sql = ProcurementSourcingDiscoveryQueries.Candidates(db, scope).OrderBy(x => x.Id).Take(2).ToQueryString();
        Assert.Contains("procurement.purchase_request_approval_submissions", sql);
        Assert.Contains("procurement.purchase_request_approval_outcomes", sql);
        Assert.Contains("core_approval.cases", sql);
        Assert.Contains("core_approval.decisions", sql);
        Assert.Contains("IS NOT DISTINCT FROM request.branch_ref", sql);
        Assert.Contains("workflow.\"SourceVersion\"=pin.submitted_business_version", sql);
        Assert.Contains("method.execution_route='CompetitiveSourcing'", sql);
        Assert.True(sql.IndexOf("pin.request_snapshot_json", StringComparison.Ordinal) < sql.LastIndexOf("LIMIT", StringComparison.Ordinal));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public void Business_evidence_failure_remains_unavailable_but_store_wrappers_never_become_rows()
    {
        Assert.True(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(new InvalidOperationException("SourcingSourcePolicyMismatch")));
        Assert.True(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(new KeyNotFoundException("Retained source missing")));
        var store = new NpgsqlException("Transport unavailable", new System.IO.IOException("Disconnected"));
        Assert.False(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(new InvalidOperationException("Wrapped", store)));
        Assert.False(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(new ArgumentException("Wrapped", new DbUpdateException("Save failed", store))));
        Assert.False(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(new AggregateException(new InvalidOperationException("Business"), store)));
    }

    [Fact]
    public void Principal_cancel_matches_the_locked_request_set_without_assuming_uuid_sort_order()
    {
        var low=Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high=Guid.Parse("80000000-0000-0000-0000-000000000002");
        Assert.True(ProcurementSourcingExecutionRoutes.SameRequestSet([low,high],[high,low,high]));
        Assert.False(ProcurementSourcingExecutionRoutes.SameRequestSet([low,high],[high]));
        Assert.False(ProcurementSourcingExecutionRoutes.SameRequestSet([low,high],[high,Guid.NewGuid()]));
        Assert.False(ProcurementSourcingExecutionRoutes.SameRequestSet([low,low],[low]));
    }

    [Fact]
    public void Principal_cancel_binds_only_a_current_core_authorized_target_branch_for_retained_sources()
    {
        var person=Guid.NewGuid();var company=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();
        var all=new ProcurementRequestScope();
        all.Set("rfq-cancel",company,person.ToString("D"),null,null,true,"test",explicitAllBranches:true);
        all.AssignPrincipal(person);
        var actor=new RfqPrincipalActor(person.ToString("D"),null,"rfq-cancel",company,null,
            new HashSet<Guid>{a,b},false,new HashSet<string>{"procurement-sourcing-cancel"});
        var source=ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(all,actor,a);
        Assert.Equal(a,source.WorkLocationId);
        Assert.True(source.IsConfined);
        Assert.False(source.IsExplicitAllBranches);
        Assert.Equal(person.ToString("D"),source.SubjectId);
        Assert.Null(source.WorkerId);
        Assert.Throws<UnauthorizedAccessException>(()=>
            ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(all,actor,c));

        var selected=new ProcurementRequestScope();
        selected.Set("rfq-cancel",company,person.ToString("D"),a,null,true,"test");
        selected.AssignPrincipal(person);
        var selectedActor=actor with {SelectedBranchId=a};
        Assert.Equal(a,ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(selected,selectedActor,a).WorkLocationId);
        Assert.Throws<UnauthorizedAccessException>(()=>
            ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(selected,selectedActor,b));

        var missingAll=new ProcurementRequestScope();
        missingAll.Set("rfq-cancel",company,person.ToString("D"),null,null,true,"test");
        missingAll.AssignPrincipal(person);
        Assert.Throws<UnauthorizedAccessException>(()=>
            ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(missingAll,actor,a));
        Assert.Throws<UnauthorizedAccessException>(()=>
            ProcurementSourcingExecutionRoutes.PrincipalCancellationSourceScope(all,actor with {CompanyId=Guid.NewGuid()},a));
    }

    private static RfqCurrentActor Actor(Guid? selected, bool organisationWide, Guid[] branches)
        => new("person", Guid.NewGuid(), "tenant", Guid.NewGuid(), selected, branches.ToHashSet(), organisationWide,
            new HashSet<string> { "procurement-sourcing-convert" }, true, new HashSet<Guid>());
}
