using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqRecommendationReadinessTests
{
    private sealed class CommercialReadiness : IProcurementRfqCommercialReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
            => Task.FromResult(!write);
    }

    [Fact]
    public async Task Registered_provider_stays_closed_without_actual_P36_capture_even_when_option_is_on()
    {
        using var services=new ServiceCollection().BuildServiceProvider();
        var provider=new ProcurementRfqRecommendationStoreReadiness(
            new ProcurementRfqCommercialOptions{Enabled=true,OfferOpeningEnabled=true,
                RecommendationDraftEnabled=true},new ConfigurationBuilder().Build(),
            services,new CommercialReadiness());
        Assert.False(ProcurementRfqRecommendationSchemaCapture.Populated);
        Assert.False(await provider.IsReadyAsync(null!,false,CancellationToken.None));
        Assert.False(await provider.IsReadyAsync(null!,true,CancellationToken.None));
    }

    [Fact]
    public void Physical_capture_requires_all_four_checks_ten_ownership_FKs_and_three_parent_keys()
    {
        Assert.Equal(4,ProcurementRfqRecommendationSchemaCapture.Types.Length);
        Assert.Equal(4,ProcurementRfqRecommendationSchemaCapture.TableNames.Count);
        Assert.Equal(4,ProcurementRfqRecommendationSchemaCapture.CheckNames.Count);
        Assert.Equal(10,ProcurementRfqRecommendationSchemaCapture.ForeignNames.Count);
        Assert.Equal(3,ProcurementRfqRecommendationSchemaCapture.ParentNames.Count);
        Assert.False(ProcurementRfqRecommendationSchemaCapture.Complete(
            ProcurementRfqRecommendationSchemaCapture.Checks,
            ProcurementRfqRecommendationSchemaCapture.ForeignKeys,
            ProcurementRfqRecommendationSchemaCapture.ParentKeys));
        var wrong=new HashSet<string>(ProcurementRfqRecommendationSchemaCapture.TableNames,
            StringComparer.Ordinal){"procurement.an_unreviewed_table"};
        Assert.False(ProcurementRfqRecommendationStoreReadiness.MatchesModelCatalog(
            wrong,ProcurementRfqRecommendationSchemaCapture.CheckNames));
    }

    [Fact]
    public void Recommendation_writer_needs_only_its_own_append_and_head_update_privileges()
    {
        var owned=ProcurementRfqRecommendationSchemaCapture.TableNames.Order(StringComparer.Ordinal).ToArray();
        var reader=ProcurementRfqRecommendationStoreReadiness.PrivilegeSql(owned,false);
        var writer=ProcurementRfqRecommendationStoreReadiness.PrivilegeSql(owned,true);
        Assert.Contains("'procurement.principal_rfq_recommendation_drafts','SELECT'",reader);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.principal_rfq_recommendation_drafts','INSERT')",reader);
        foreach(var column in ProcurementRfqRecommendationStoreReadiness.HeadMutableColumns)
            Assert.Contains("has_column_privilege(current_user,'procurement.principal_rfq_recommendation_drafts','"
                +column+"','UPDATE')",writer);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.principal_rfq_recommendation_drafts','UPDATE')",writer);
        Assert.Contains("NOT EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid='procurement.principal_rfq_recommendation_drafts'::regclass",writer);
        Assert.Contains("'procurement.principal_rfq_recommendation_receipts','INSERT'",writer);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.principal_rfq_recommendation_revisions','UPDATE')",writer);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.principal_rfq_recommendation_lines','DELETE,TRUNCATE')",writer);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.principal_rfq_commercial_offer_submissions','INSERT')",writer);
        Assert.DoesNotContain("has_table_privilege(current_user,'core_approval.cases','INSERT')",writer);
        Assert.Throws<ArgumentException>(()=>ProcurementRfqRecommendationStoreReadiness.PrivilegeSql(
            ["procurement.principal_rfq_recommendation_drafts"],true));
    }

    [Fact]
    public void Head_update_matrix_denies_missing_mutable_and_added_immutable_columns()
    {
        var allowed=ProcurementRfqRecommendationStoreReadiness.HeadMutableColumns;
        Assert.Equal(6,allowed.Length);
        Assert.True(ProcurementRfqRecommendationStoreReadiness.MatchesHeadUpdateColumns(allowed));
        Assert.False(ProcurementRfqRecommendationStoreReadiness.MatchesHeadUpdateColumns(
            allowed.Where(x=>x!="updated_by")));
        Assert.False(ProcurementRfqRecommendationStoreReadiness.MatchesHeadUpdateColumns(
            allowed.Append("source_envelope_sha256")));
    }
}
