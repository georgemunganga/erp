using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqOfferOpeningStorageTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null; public Guid? OrgUnitId => null;
        public bool IsConfined => false; public string? CorrelationId => null;
    }
    private static (PrincipalRfqOfferOpeningRecord Row, PrincipalRfqCommercialRoundOpeningRecord Round,
        PrincipalRfqCommercialRoundSealRecord Seal, PrincipalRfqOfferOpeningReceipt Receipt) Rows(string? changedSnapshot = null)
    {
        var f = new PrincipalRfqOfferOpeningTests.Fixture(); var proof = f.Open();
        var round = new PrincipalRfqCommercialRoundOpeningRecord { Id = f.Round.RoundId,
            TenantId = "fixture", LegalEntityId = f.Company, BranchId = f.Branch,
            SourcingEventId = f.Round.EventId, CommercialPolicyVersionId = f.Policy.Id,
            PublishedPolicyIdentityId = Guid.NewGuid(), PolicyRulesSha256 = proof.PolicyRulesSha256,
            PreparedPlanId = f.Round.PreparedPlanId, PreparedPlanSha256 = f.Round.PreparedPlanSha256,
            SnapshotJson = f.Snapshot, SnapshotSha256 = f.Round.ApprovalEvidenceSha256 };
        var snapshot = changedSnapshot ?? proof.SnapshotJson;
        var row = new PrincipalRfqOfferOpeningRecord { TenantId = "fixture", LegalEntityId = f.Company,
            OpeningId = round.Id, SealId = f.Seal.Id, SourcingEventId = round.SourcingEventId,
            BranchId = round.BranchId, PolicyVersionId = f.Policy.Id,
            PublishedPolicyIdentityId = round.PublishedPolicyIdentityId,
            PolicyRulesSha256 = proof.PolicyRulesSha256, ApprovedRoundSha256 = round.SnapshotSha256,
            OfficerPrincipalId = proof.OfficerPrincipalId, OfficerWorkerAliasId = proof.OfficerWorkerAliasId,
            CommandKey = Guid.NewGuid(), RequestSha256 = new string('a', 64), OpenedAt = proof.OpenedAt,
            OfferCount = proof.OfferCount, OfferSetSha256 = proof.OfferSetSha256,
            Reason = proof.Reason, Reference = proof.Reference,
            SnapshotJson = snapshot, SnapshotSha256 = ProcurementEvidenceEncoding.Digest(snapshot) };
        var response = PrincipalRfqOfferOpeningStorage.Response(row);
        var receipt = new PrincipalRfqOfferOpeningReceipt { TenantId = row.TenantId,
            LegalEntityId = row.LegalEntityId, UnsealingId = row.Id, OpeningId = row.OpeningId,
            SourcingEventId = row.SourcingEventId, BranchId = row.BranchId,
            ActorPrincipalId = row.OfficerPrincipalId, CommandKey = row.CommandKey,
            RequestSha256 = row.RequestSha256, ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response) };
        return (row, round, f.Seal, receipt);
    }

    [Fact]
    public void Stored_opening_and_original_ack_bind_exact_same_parent_and_body()
    {
        var (row, round, seal, receipt) = Rows();
        PrincipalRfqOfferOpeningStorage.Verify(row, round, seal);
        PrincipalRfqOfferOpeningStorage.VerifyReceipt(row, receipt);
        var wrong = Rows();
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqOfferOpeningStorage.Verify(row, wrong.Round, wrong.Seal));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqOfferOpeningStorage.VerifyReceipt(row, wrong.Receipt));
        var ack = JsonNode.Parse(receipt.ResponseJson)!; ack["awardMade"] = true;
        var json = ack.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqOfferOpeningStorage.VerifyReceipt(row,
            new PrincipalRfqOfferOpeningReceipt { TenantId = receipt.TenantId, LegalEntityId = receipt.LegalEntityId,
                UnsealingId = receipt.UnsealingId, OpeningId = receipt.OpeningId,
                SourcingEventId = receipt.SourcingEventId, BranchId = receipt.BranchId,
                ActorPrincipalId = receipt.ActorPrincipalId, CommandKey = receipt.CommandKey,
                RequestSha256 = receipt.RequestSha256, ResponseJson = json,
                ResponseSha256 = ProcurementEvidenceEncoding.Digest(json) }));
    }

    [Fact]
    public void Missing_same_save_receipt_and_modified_or_deleted_evidence_are_denied()
    {
        var (row, _, _, _) = Rows();
        var scope = new Scope(row.TenantId, row.LegalEntityId, row.OfficerPrincipalId);
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        db.AddScoped(row);
        Assert.Equal("RfqOfferOpeningSameSaveReceiptRequired", Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        db.ChangeTracker.Clear(); db.Attach(row);
        db.Entry(row).Property(x => x.Reason).CurrentValue = "Rewrite original reason";
        Assert.Equal("RfqOfferOpeningEvidenceAppendOnly", Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        db.ChangeTracker.Clear(); db.Attach(row); db.Remove(row);
        Assert.Equal("RfqOfferOpeningEvidenceAppendOnly", Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void New_owner_tuples_are_nonnullable_exact_directed_and_short_named()
    {
        var (_, round, _, _) = Rows();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope(round.TenantId, round.LegalEntityId, Guid.NewGuid().ToString("D")));
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(PrincipalRfqOfferOpeningRecord), typeof(PrincipalRfqOfferOpeningReceipt) })
        {
            var entity = model.FindEntityType(type)!;
            Assert.Equal(ValueGenerated.Never, entity.FindProperty("Id")!.ValueGenerated);
            Assert.All(entity.GetKeys(), key => Assert.True(key.GetName()!.Length <= 63));
            Assert.All(entity.GetIndexes(), index => Assert.True(index.GetDatabaseName()!.Length <= 63));
            foreach (var fk in entity.GetForeignKeys())
            {
                Assert.True(fk.GetConstraintName()!.Length <= 63);
                Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
                Assert.All(fk.Properties, property => Assert.False(property.IsNullable));
            }
            Assert.All(entity.GetKeys().SelectMany(x => x.Properties), property =>
                Assert.DoesNotContain(property.Name, new[] { "Version", "Revision", "State", "Sequence", "AcceptedQuantity" }));
        }
        var opening = model.FindEntityType(typeof(PrincipalRfqOfferOpeningRecord))!;
        Assert.Contains(opening.GetForeignKeys(), x => x.GetConstraintName() == "fk_p32_unseal_exact_seal"
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "SealId",
                "OpeningId", "SourcingEventId", "BranchId", "OfferCount", "OfferSetSha256" }));
        var receipt = model.FindEntityType(typeof(PrincipalRfqOfferOpeningReceipt))!;
        Assert.Contains(receipt.GetForeignKeys(), x => x.GetConstraintName() == "fk_p32_unseal_receipt_command"
            && x.Properties.Select(p => p.Name).Contains("RequestSha256"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(8, true)]
    public void Physical_family_installation_is_either_absent_or_complete(int count, bool installed)
        => Assert.Equal(installed, ProcurementRfqCommercialSchema.Complete(count));

    [Theory]
    [InlineData(1)] [InlineData(6)] [InlineData(7)] [InlineData(9)]
    public void Older_six_family_or_other_partial_P32_installation_is_unavailable(int count)
        => Assert.Throws<InvalidOperationException>(() => ProcurementRfqCommercialSchema.Complete(count));
}
