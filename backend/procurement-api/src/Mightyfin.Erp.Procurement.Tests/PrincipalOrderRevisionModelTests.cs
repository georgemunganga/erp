using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderRevisionModelTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Revision_model_names_the_exact_owned_history_graph_and_blocks_nullable_check_arms()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=p33_model_only;Username=invalid").Options,
            new Scope("p33-model", Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        var model = db.GetService<IDesignTimeModel>().Model;
        var origin = model.FindEntityType(typeof(PrincipalOrderCommercialOrigin))!;
        var revision = model.FindEntityType(typeof(PrincipalOrderDraftCommercialRevision))!;
        var slice = model.FindEntityType(typeof(PrincipalOrderEffectiveSourceSlice))!;

        Assert.Equal("principal_order_commercial_origins", origin.GetTableName());
        Assert.Equal("principal_order_draft_commercial_revisions", revision.GetTableName());
        Assert.Equal("principal_order_effective_source_slices", slice.GetTableName());
        Assert.All(new[] { origin, revision, slice }.SelectMany(x => x.GetForeignKeys()),
            fk => Assert.True(fk.GetConstraintName() is { Length: <= 63 }));
        Assert.All(new[] { origin, revision, slice }.SelectMany(x => x.GetIndexes()),
            index => Assert.True(index.GetDatabaseName() is { Length: <= 63 }));
        Assert.Contains(origin.GetForeignKeys(), fk =>
            fk.GetConstraintName() == "fk_po_draft_origin_order" && fk.Properties.Count == 3);
        Assert.Contains(revision.GetForeignKeys(), fk =>
            fk.GetConstraintName() == "fk_po_draft_revision_origin_owner" && fk.Properties.Count == 4);
        Assert.Contains(revision.GetForeignKeys(), fk =>
            fk.GetConstraintName() == "fk_po_draft_revision_previous_owner" && fk.Properties.Count == 4);
        Assert.Contains(slice.GetForeignKeys(), fk =>
            fk.GetConstraintName() == "fk_po_draft_slice_binding_owner" && fk.Properties.Count == 4);
        Assert.Contains(slice.GetForeignKeys(), fk =>
            fk.GetConstraintName() == "fk_po_draft_slice_allocation" && fk.Properties.Count == 3);

        var originCheck = origin.GetCheckConstraints().Single(x => x.Name == "ck_po_draft_origin").Sql;
        var revisionCheck = revision.GetCheckConstraints().Single(x => x.Name == "ck_po_draft_revision").Sql;
        var sliceCheck = slice.GetCheckConstraints().Single(x => x.Name == "ck_po_draft_slice").Sql;
        Assert.Contains("snapshot_json IS NOT NULL", originCheck);
        Assert.Contains("original_direct_source_sha256 IS NOT NULL", originCheck);
        Assert.Contains("previous_snapshot_sha256 IS NOT NULL", revisionCheck);
        Assert.Contains("actor_principal_id IS NOT NULL", revisionCheck);
        Assert.Contains("currency_code IS NOT NULL", sliceCheck);
    }
}
