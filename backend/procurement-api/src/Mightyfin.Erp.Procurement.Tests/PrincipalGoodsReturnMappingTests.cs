using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalGoodsReturnMappingTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Return_draft_source_and_evidence_have_scoped_owner_keys()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            new Scope("return-model", Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        var head = db.Model.FindEntityType(typeof(PrincipalGoodsReturnDraft))!;
        var revision = db.Model.FindEntityType(typeof(PrincipalGoodsReturnRevision))!;
        var receipt = db.Model.FindEntityType(typeof(PrincipalGoodsReturnCommandReceipt))!;
        Assert.Equal("principal_goods_return_drafts", head.GetTableName());
        Assert.Equal("principal_goods_return_revisions", revision.GetTableName());
        Assert.Equal("principal_goods_return_receipts", receipt.GetTableName());
        foreach (var type in new[] { head, revision, receipt })
        {
            Assert.Equal("procurement", type.GetSchema());
            Assert.Contains(type.GetKeys(), key => key.Properties.Select(x => x.Name)
                .SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id" }));
            Assert.All(type.GetKeys(), key => Assert.InRange(key.GetName()!.Length, 1, 63));
            Assert.All(type.GetIndexes(), index => Assert.InRange(index.GetDatabaseName()!.Length, 1, 63));
            Assert.All(type.GetForeignKeys(), foreign => Assert.InRange(foreign.GetConstraintName()!.Length, 1, 63));
        }
        Assert.Contains(head.GetIndexes(), x => x.GetDatabaseName() == "ix_pgret_branch_created");
        Assert.Equal(new[] { nameof(PrincipalGoodsReturnDraft.SourceStateId), nameof(PrincipalGoodsReturnDraft.AcceptanceFactId),
            nameof(PrincipalGoodsReturnDraft.ReceiptId), nameof(PrincipalGoodsReturnDraft.PurchaseOrderLineId),
            nameof(PrincipalGoodsReturnDraft.IssuedVersionId) }.Order().ToArray(),
            head.GetForeignKeys().Select(x => x.Properties[^1].Name).Order().ToArray());
        Assert.Contains(revision.GetForeignKeys(), x => x.Properties[^1].Name == nameof(PrincipalGoodsReturnRevision.DraftId));
        Assert.Contains(receipt.GetForeignKeys(), x => x.GetConstraintName() == "fk_pgret_receipt_draft" &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "DraftId", "BranchId" }));
        var command = Assert.Single(receipt.GetForeignKeys(), x => x.GetConstraintName() == "fk_pgret_receipt_revision");
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "DraftId", "ResultRevision", "ActorPrincipalId", "CommandKey" },
            command.Properties.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "DraftId", "Revision", "ActorPrincipalId", "CommandKey" },
            command.PrincipalKey.Properties.Select(p => p.Name).ToArray());
        Assert.Equal(typeof(PrincipalGoodsReturnRevision), command.PrincipalEntityType.ClrType);
        Assert.Equal("ak_pgret_revision_command", command.PrincipalKey.GetName());
        Assert.DoesNotContain(head.GetKeys().SelectMany(k => k.Properties),
            p => p.Name is "Revision" or "CurrentRevisionId" or "State" or "Version");
    }
}
