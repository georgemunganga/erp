using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class P36RecommendationMigrationOperationTests
{
    [Fact]
    public void Genuine_up_has_only_four_families_and_three_stable_parent_keys()
    {
        var operations=new P36HumanRecommendationDraft().UpOperations;
        var tables=operations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(4,tables.Length);
        Assert.All(tables,t=>Assert.StartsWith("principal_rfq_recommendation_",t.Name));
        Assert.Equal(10,tables.Sum(t=>t.ForeignKeys.Count));
        Assert.Equal(4,tables.Sum(t=>t.CheckConstraints.Count));
        Assert.Equal(new[]{"ak_p36_rec_source_line","ak_p36_rec_seal","ak_p36_rec_offer"}.Order(),
            operations.OfType<AddUniqueConstraintOperation>().Select(x=>x.Name).Order());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<SqlOperation>());
    }

    [Fact]
    public void Retained_family_veto_is_first_before_every_generated_drop_and_parent_key_removal()
    {
        var operations=new P36HumanRecommendationDraft().DownOperations;
        var veto=Assert.IsType<SqlOperation>(operations[0]);
        Assert.Equal(P36RecommendationDraftGuards.BeforeDownSql,veto.Sql);
        Assert.False(veto.SuppressTransaction);
        Assert.Single(operations.OfType<SqlOperation>());
        Assert.Equal(4,operations.OfType<DropTableOperation>().Count());
        Assert.Equal(3,operations.OfType<DropUniqueConstraintOperation>().Count());
        Assert.Contains("ERRCODE = '55000'",veto.Sql,StringComparison.Ordinal);
    }
}
