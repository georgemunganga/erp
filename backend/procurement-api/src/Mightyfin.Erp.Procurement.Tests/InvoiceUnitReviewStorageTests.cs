using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Procurement.Tests;
public sealed class InvoiceUnitReviewStorageTests
{
    [Fact]
    public void Literal_source_and_retained_assignment_pins_roundtrip_to_actual_Core_graph()
    {
        var f = new InvoiceUnitReviewTransitionTests.Fixture(); var plan = f.Open(); var row = InvoiceUnitReviewStorage.Source(plan, f.Issued);
        var pins = plan.AssignmentWorkers.Select(x => new InvoiceUnitReviewAssignmentEvidence { TenantId = row.TenantId, LegalEntityId = row.LegalEntityId,
            ReviewSourceId = row.Id, CoreCaseId = row.CoreCaseId, CoreAssignmentId = x.AssignmentId, PrincipalId = x.PrincipalId, WorkerId = x.WorkerId }).ToArray();
        var restored = InvoiceUnitReviewStorage.Restore(row, pins, plan.Case);
        Assert.Equal(JsonSerializer.Serialize(plan.Source), JsonSerializer.Serialize(restored.Source)); Assert.Equal(plan.SourceJson, restored.SourceJson); Assert.Equal(plan.SourceSha256, restored.SourceSha256);
        Assert.Equal(plan.AssignmentWorkers, restored.AssignmentWorkers); Assert.Equal("Unverified", f.Mapping.VerificationState);
        Assert.False(InvoiceHistoricalExposure.Unavailable().Complete);
    }
    [Theory] [InlineData("scalar-mapping")] [InlineData("literal-change")] [InlineData("case-alias")]
    [InlineData("other-case")] [InlineData("missing-assignment")][InlineData("rebound-assignment-worker")]
    public void Altered_storage_or_assignment_bindings_never_rehydrate_review_authority(string fault)
    {
        var f = new InvoiceUnitReviewTransitionTests.Fixture(); var plan = f.Open(); var original = InvoiceUnitReviewStorage.Source(plan, f.Issued);
        var node = JsonSerializer.SerializeToNode(original)!.AsObject();
        var pins = plan.AssignmentWorkers.Select(x => new InvoiceUnitReviewAssignmentEvidence { TenantId = original.TenantId, LegalEntityId = original.LegalEntityId,
            ReviewSourceId = original.Id, CoreCaseId = original.CoreCaseId, CoreAssignmentId = x.AssignmentId, PrincipalId = x.PrincipalId,
            WorkerId = fault == "rebound-assignment-worker" ? Guid.NewGuid() : x.WorkerId }).ToArray();
        if (fault == "scalar-mapping") node["MappingVersionId"] = Guid.NewGuid();
        if (fault == "literal-change") node["SourceJson"] = original.SourceJson + " ";
        if (fault == "case-alias")
        {
            var source = JsonNode.Parse(original.SourceJson)!.AsObject(); source["reviewId"] = Guid.NewGuid();
            var json = source.ToJsonString(); node["SourceJson"] = json; node["SourceSha256"] = ProcurementEvidenceEncoding.Digest(json);
        }
        if (fault == "other-case") plan.Case.Id = Guid.NewGuid();
        if (fault == "missing-assignment") pins = pins.Skip(1).ToArray();
        var row = node.Deserialize<InvoiceUnitReviewSource>()!;
        if (fault == "case-alias") Assert.Throws<JsonException>(() => InvoiceUnitReviewStorage.Restore(row, pins, plan.Case));
        else Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewStorage.Restore(row, pins, plan.Case));
    }
    [Fact]
    public void Model_uses_literal_text_stable_scoped_keys_and_append_only_evidence_shapes()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql("Host=invalid;Database=design_only;Username=invalid").Options, new Scope());
        var model = db.GetService<IDesignTimeModel>().Model;
        var source = model.FindEntityType(typeof(InvoiceUnitReviewSource))!;
        Assert.Equal("text", source.FindProperty(nameof(InvoiceUnitReviewSource.SourceJson))!.GetColumnType());
        Assert.Equal("text", source.FindProperty(nameof(InvoiceUnitReviewSource.IssuedJson))!.GetColumnType());
        Assert.All(source.GetKeys(), key => Assert.DoesNotContain(key.Properties, p => p.Name == nameof(ProcurementRecord.Version)));
        Assert.Contains(source.GetForeignKeys(), fk => fk.GetConstraintName() == "fk_inv_unit_source_mapping" && fk.Properties.Count == 6);
        Assert.Contains(source.GetIndexes(), ix => ix.GetDatabaseName() == "ux_inv_unit_source_mapping" && ix.IsUnique);
        Assert.Contains(source.GetCheckConstraints(), check => check.Name == "ck_inv_unit_source" && check.Sql.Contains("source_sha256 IS NOT NULL"));
        foreach (var type in new[] { typeof(InvoiceUnitReviewAssignmentEvidence), typeof(InvoiceUnitReviewOutcome), typeof(InvoiceUnitReviewCommandReceipt) })
        {
            var entity = model.FindEntityType(type)!; Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
            Assert.Contains(entity.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceUnitReviewSource));
        }
    }
    sealed class Scope : IProcurementScope
    {
        private readonly Guid company = Guid.NewGuid();
        public string TenantId => "test"; public Guid LegalEntityId => company; public string SubjectId => "test";
        public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => null;
    }
}
