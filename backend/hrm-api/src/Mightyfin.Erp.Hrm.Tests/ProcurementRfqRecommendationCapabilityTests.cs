using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqRecommendationCapabilityTests
{
    private sealed class SubstituteReadiness : IProcurementRfqRecommendationDraftReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
            =>Task.FromResult(true);
    }
    private sealed class SubstituteCommercialReadiness : IProcurementRfqCommercialReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
            =>Task.FromResult(true);
    }

    [Fact]
    public async Task Configured_metadata_rejects_substitute_ready_even_with_all_options_enabled()
    {
        using var services=new ServiceCollection()
            .AddSingleton(new ProcurementRfqCommercialOptions{Enabled=true,
                OfferOpeningEnabled=true,RecommendationDraftEnabled=true})
            .AddSingleton(new ProcurementApprovalExecutionOptions{Enabled=true,
                PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true})
            .AddSingleton<IProcurementRfqRecommendationDraftReadiness>(new SubstituteReadiness())
            .BuildServiceProvider();
        Assert.False(await ProcurementRfqRecommendationCapability.ReadyAsync(
            services,new ProcurementRequestScope(),new DefaultHttpContext(),false,CancellationToken.None));
    }

    [Fact]
    public async Task Concrete_readiness_stays_closed_without_execution_configuration()
    {
        var options=new ProcurementRfqCommercialOptions{Enabled=true,
            OfferOpeningEnabled=true,RecommendationDraftEnabled=true};
        using var services=new ServiceCollection()
            .AddSingleton(new ProcurementApprovalExecutionOptions{Enabled=true,
                PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=false})
            .BuildServiceProvider();
        var concrete=new ProcurementRfqRecommendationStoreReadiness(options,
            new ConfigurationBuilder().Build(),services,null!);
        Assert.False(await concrete.IsConfiguredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Concrete_P36_metadata_rejects_substitute_P32_even_with_execution_enabled()
    {
        var options=new ProcurementRfqCommercialOptions{Enabled=true,
            OfferOpeningEnabled=true,RecommendationDraftEnabled=true};
        using var services=new ServiceCollection()
            .AddSingleton(new ProcurementApprovalExecutionOptions{Enabled=true,
                PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true})
            .BuildServiceProvider();
        var concrete=new ProcurementRfqRecommendationStoreReadiness(options,
            new ConfigurationBuilder().Build(),services,new SubstituteCommercialReadiness());
        Assert.False(await concrete.IsConfiguredAsync(CancellationToken.None));
    }

    [Fact]
    public void Physical_read_rejects_an_unbound_or_different_execution_database()
    {
        const string execution="Host=localhost;Port=5432;Database=procurement;Username=finite_exec";
        const string record="Host=localhost;Port=5432;Database=record_only;Username=record";
        Assert.True(ProcurementRfqRecommendationStoreReadiness.MatchesExecutionDatabase(
            execution,"Host=localhost;Port=5432;Database=procurement;Username=other_finite"));
        Assert.False(ProcurementRfqRecommendationStoreReadiness.MatchesExecutionDatabase(execution,record));
        Assert.False(ProcurementRfqRecommendationStoreReadiness.MatchesExecutionDatabase(null,execution));
    }

    [Fact]
    public void Availability_needs_both_current_actions_and_a_permitted_branch_without_a_worker()
    {
        var branch=Guid.NewGuid(); var denied=Guid.NewGuid();
        var current=new ProcurementPrincipalPeople.Person(Guid.NewGuid().ToString("D"),null,
            new HashSet<string>{PrincipalRfqRecommendationDraft.Action,
                PrincipalRfqOfferOpeningControls.ReadAction},false,new HashSet<Guid>{branch});
        Assert.True(ProcurementRfqRecommendationCapability.HasCurrentActions(current,branch));
        Assert.True(ProcurementRfqRecommendationCapability.HasCurrentActions(current,null));
        Assert.False(ProcurementRfqRecommendationCapability.HasCurrentActions(current,denied));
        Assert.False(ProcurementRfqRecommendationCapability.HasCurrentActions(current with
            {Permissions=new HashSet<string>{PrincipalRfqRecommendationDraft.Action}},branch));
        Assert.False(ProcurementRfqRecommendationCapability.HasCurrentActions(current with
            {Permissions=new HashSet<string>{PrincipalRfqOfferOpeningControls.ReadAction}},branch));
        Assert.False(ProcurementRfqRecommendationCapability.HasCurrentActions(null,branch));
    }
}
