using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingDraftPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "rfq-model"; }
    private static readonly Scope Current = new("rfq-test", Guid.NewGuid(), "buyer");
    private static ProcurementDbContext Db(bool relational = false) => new(relational
        ? new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql("Host=127.0.0.1;Database=model_only;Username=model_only;Password=unused").Options
        : new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Current);

    [Fact]
    public void Npgsql_model_preserves_legacy_defaults_exact_money_and_stable_explicit_new_keys_without_connecting()
    {
        using var db = Db(true); var model = db.GetService<IDesignTimeModel>().Model;
        var header = model.FindEntityType(typeof(SourcingEvent))!;
        Assert.Equal(0, header.FindProperty(nameof(SourcingEvent.ExecutionVersion))!.GetDefaultValue());
        Assert.True(header.FindProperty(nameof(SourcingEvent.SourceEvidenceId))!.IsNullable);
        foreach (var property in new[] { nameof(SourcingEvent.SourceEvidenceId), nameof(SourcingEvent.MakerWorkerId), nameof(SourcingEvent.ExecutionVersion), nameof(SourcingEvent.BranchRef), nameof(SourcingEvent.OriginalDraftFingerprint) })
            Assert.False(header.FindProperty(property)!.IsKey());
        var ownedCheck = header.GetCheckConstraints().Single(x => x.Name == "ck_sourcing_owned_draft").Sql;
        Assert.Contains("method_revision IS NOT NULL AND method_revision>0", ownedCheck);
        Assert.Contains("original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~", ownedCheck);
        Assert.Contains("execution_version=2 AND source_evidence_id IS NOT NULL", ownedCheck);
        Assert.Contains("method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL", ownedCheck);
        var envelopeCheck = model.FindEntityType(typeof(SourcingDraftEvidence))!.GetCheckConstraints()
            .Single(x => x.Name == "ck_rfq_original_envelope").Sql;
        Assert.Contains("schema_version=2 AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~", envelopeCheck);
        var binding = model.FindEntityType(typeof(SourcingDemandBinding))!;
        var bindingCheck = binding.GetCheckConstraints().Single(x => x.Name == "ck_rfq_binding_value").Sql;
        Assert.Contains("identity_schema_version=2 AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~", bindingCheck);
        Assert.Contains("requester_principal_id IS NOT NULL AND requester_principal_id <> '00000000-0000-0000-0000-000000000000' AND requester_principal_id ~", bindingCheck);
        Assert.Contains("principal_identity_sha256 IS NOT NULL AND principal_identity_sha256 ~", bindingCheck);
        Assert.Equal(38, binding.FindProperty(nameof(SourcingDemandBinding.EstimatedValue))!.GetPrecision());
        Assert.Equal(8, binding.FindProperty(nameof(SourcingDemandBinding.EstimatedValue))!.GetScale());
        Assert.Equal(38, model.FindEntityType(typeof(PurchaseRequestAllocation))!.FindProperty(nameof(PurchaseRequestAllocation.EstimatedValue))!.GetPrecision());
        foreach (var type in new[] { typeof(SourcingDraftEvidence), typeof(SourcingDemandBinding), typeof(SourcingCommandReceipt) })
        {
            var entity = model.FindEntityType(type)!;
            var scoped = entity.GetKeys().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id" }));
            Assert.StartsWith("ak_rfq_", scoped.GetName()); Assert.True(scoped.GetName()!.Length < 63);
            Assert.All(entity.GetKeys(), key => Assert.DoesNotContain(key.Properties, p => p.Name.Contains("Quantity") || p.Name.Contains("Value") || p.Name == "Status"));
        }
        Assert.Contains(binding.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(PurchaseRequestAllocation)
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "AllocationId" }));
        var receipt = model.FindEntityType(typeof(SourcingCommandReceipt))!;
        var receiptCheck = receipt.GetCheckConstraints().Single(x => x.Name == "ck_rfq_receipt_identity").Sql;
        Assert.Contains("response_schema_version=2 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~", receiptCheck);
        Assert.DoesNotContain(receipt.GetProperties(), x => x.Name.Contains("PurchaseOrder") || x.Name.Contains("CoreApprovalCase"));
        Assert.Contains(receipt.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SourcingEvent));
        var allocation = model.FindEntityType(typeof(PurchaseRequestAllocation))!;
        Assert.False(allocation.FindProperty(nameof(PurchaseRequestAllocation.TargetId))!.IsKey());
        Assert.False(allocation.FindProperty(nameof(PurchaseRequestAllocation.TargetType))!.IsKey());
        var line = model.FindEntityType(typeof(SourcingLine))!;
        Assert.False(line.FindProperty(nameof(SourcingLine.PurchaseRequestLineId))!.IsKey());
        Assert.Contains(line.GetIndexes(), x => x.GetDatabaseName() == "ux_rfq_line_exact_source" && x.IsUnique);
    }

    [Fact]
    public async Task Legacy_draft_stays_usable_but_cannot_be_upgraded_or_claim_governed_ownership()
    {
        await using var db = Db(); var draft = new SourcingEvent { Number = "RF-1", Title = "Legacy",
            Method = "RFQ", CurrencyCode = "ZMW", OwnerSubjectId = Current.SubjectId };
        db.AddScoped(draft); await db.SaveChangesAsync();
        Assert.Equal(0, draft.ExecutionVersion); Assert.Null(draft.SourceEvidenceId);
        draft.Title = "Changed legacy title"; await db.SaveChangesAsync();
        Assert.Equal(2L, draft.Version);
        draft.ExecutionVersion = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        draft.ExecutionVersion = 0; db.Entry(draft).State = EntityState.Unchanged;
        draft.MakerWorkerId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("envelope")][InlineData("binding")][InlineData("receipt")]
    public async Task All_new_original_evidence_is_append_only_for_every_save_overload(string kind)
    {
        await using var db = Db();
        ProcurementRecord value = kind switch
        {
            "envelope" => new SourcingDraftEvidence(), "binding" => new SourcingDemandBinding(), _ => new SourcingCommandReceipt(),
        };
        value.TenantId = Current.TenantId; value.LegalEntityId = Current.LegalEntityId; value.CreatedBy = Current.SubjectId;
        db.Attach(value); db.Entry(value).Property(nameof(ProcurementRecord.UpdatedBy)).CurrentValue = "changed";
        Assert.Equal("RfqEvidenceAppendOnly", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges(true));
        db.Entry(value).State = EntityState.Unchanged; db.Remove(value);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(false));
    }

    [Fact]
    public async Task Orphan_original_evidence_and_receipt_cannot_invent_an_owned_event()
    {
        await using var db = Db();
        db.AddScoped(new SourcingDraftEvidence { SourcingEventId = Guid.NewGuid(), MakerSubjectId = Current.SubjectId });
        Assert.Equal("RfqOriginalEvidenceOwnerRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear();
        db.AddScoped(new SourcingCommandReceipt { SourcingEventId = Guid.NewGuid(), ActorPrincipalId = "someone-else",
            Operation = "Create", CommandKey = Guid.NewGuid() });
        Assert.Equal("RfqReceiptInvalid", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public void Overlay_proposal_checks_full_scoped_deferred_relationships_not_names_only()
    {
        Assert.Contains("fk_rfq_binding_exact_line", SourcingDraftOwnershipConstraintSql.Install);
        Assert.Contains("fk_rfq_binding_exact_hold", SourcingDraftOwnershipConstraintSql.Install);
        Assert.Contains("ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED", SourcingDraftOwnershipConstraintSql.Install);
        foreach (var part in new[] { "c.convalidated", "c.condeferred", "c.confrelid", "c.confdeltype", "c.conkey", "c.confkey", "i.indisunique", "i.indimmediate" })
            Assert.Contains(part, SourcingDraftOwnershipConstraintSql.ManifestQuery);
    }
}
