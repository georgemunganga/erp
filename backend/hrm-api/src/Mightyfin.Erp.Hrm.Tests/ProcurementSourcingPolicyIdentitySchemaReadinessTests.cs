using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Negative/manifest source contracts. Positive finite-role PostgreSQL proof remains a separate gate.</summary>
public sealed class ProcurementSourcingPolicyIdentitySchemaReadinessTests
{
    [Fact]
    public async Task NonPostgres_owner_stays_unavailable_with_no_writes_or_alternate_connection()
    {
        var scope = new ProcurementRequestScope(); scope.Set("p29-schema", Guid.NewGuid(), "maker", null, null, false, "test");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        Assert.False(await new ProcurementSourcingPolicyIdentitySchemaReadiness().IsReadyAsync(owner, default));
        Assert.False(owner.ChangeTracker.HasChanges());
    }

    [Fact]
    public void Production_registration_uses_actual_owning_store_readiness_without_connection_fallback()
    {
        var services = new ServiceCollection();
        ProcurementHostIntegration.AddServices(services, "Host=never-used.invalid;Database=not-opened",
            new ConfigurationBuilder().Build());
        var registration = Assert.Single(services.Where(x => x.ServiceType == typeof(IProcurementSourcingPolicyIdentitySchemaReadiness)));
        Assert.Equal(typeof(ProcurementSourcingPolicyIdentitySchemaReadiness), registration.ImplementationType);
    }

    [Theory]
    [InlineData("sourcing_policy_identity_evidence")]
    [InlineData("audit_events")]
    [InlineData("outbox_events")]
    public void Append_only_runtime_requires_both_finite_grants_and_denies_mutation(string table)
    {
        var sql = ProcurementSourcingPolicyIdentitySchemaReadiness.PrivilegeSql;
        Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','SELECT')", sql);
        Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','INSERT')", sql);
        Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','UPDATE')", sql);
        Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','DELETE')", sql);
        Assert.Contains($"NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE')", sql);
        Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','TRUNCATE')", sql);
        Assert.Contains("rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls", sql);
        Assert.Contains("NOT has_schema_privilege(current_user,'procurement','CREATE')", sql);
    }

    [Fact]
    public void Policy_authoring_keeps_intended_update_but_rejects_destructive_privileges()
    {
        var sql = ProcurementSourcingPolicyIdentitySchemaReadiness.PrivilegeSql;
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.policy_versions','DELETE')", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.policy_versions','TRUNCATE')", sql);
        Assert.DoesNotContain("NOT has_any_column_privilege(current_user,'procurement.policy_versions','UPDATE')", sql);
    }

    [Fact]
    public void Manifest_preserves_actual_named_partial_predicates_and_null_guards()
    {
        Assert.Equal("20261023222748_P30PrincipalCommercialWorkflows", ProcurementSourcingPolicyIdentitySchemaReadiness.MigrationId);
        Assert.Equal("((stage)::text = 'Created'::text)", ProcurementSourcingPolicyIdentitySchemaReadiness.PartialPredicates["ux_source_policy_identity_created"]);
        Assert.Equal("((stage)::text = 'Published'::text)", ProcurementSourcingPolicyIdentitySchemaReadiness.PartialPredicates["ux_source_policy_identity_published"]);
        var definition = ProcurementSourcingPolicyIdentitySchemaReadiness.CheckDefinition;
        Assert.Contains("(rules_sha256 IS NOT NULL)", definition);
        Assert.Contains("(evidence_sha256 IS NOT NULL)", definition);
        Assert.Contains("(created_evidence_stage IS NOT NULL)", definition);
        Assert.Contains("(submitted_evidence_stage IS NOT NULL)", definition);
        Assert.Contains("(schema_version = ANY (ARRAY[1, 2]))", definition);
        Assert.Contains("((schema_version = 1) AND (actor_worker_id IS NOT NULL)", definition);
        Assert.Contains("((schema_version = 2) AND ((actor_worker_id IS NULL) OR", definition);
        Assert.Contains("(actor_principal_id IS NOT NULL)", definition);
        Assert.Contains("((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text)", definition);
    }

    [Fact]
    public void P32_published_source_index_requires_its_genuine_lineage_and_exact_mapped_set()
    {
        var p30 = ProcurementSourcingPolicyIdentitySchemaReadiness.MigrationId;
        var p32 = ProcurementSourcingPolicyIdentitySchemaReadiness.P32MigrationId;
        var four = new[] { "ux_source_policy_identity_revision", "ux_source_policy_identity_created",
            "ux_source_policy_identity_published", "ux_pr32_published_identity_target" };
        Assert.True(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p30, p32], four));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p30], four));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p32], four));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p30, p32], four[..3]));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p30, p32],
            [..four[..3], "ux_unreviewed_identity_alias"]));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32Indexes([p30, p32],
            [..four[..3], four[2]]));
        Assert.Equal("CREATE UNIQUE INDEX ux_pr32_published_identity_target ON procurement.sourcing_policy_identity_evidence USING btree (tenant_id, legal_entity_id, id, policy_version_id, stage, rules_sha256, evidence_sha256)",
            ProcurementSourcingPolicyIdentitySchemaReadiness.P32PublishedIndexDefinition);
        var keys = new[] { "PK_sourcing_policy_identity_evidence",
            "AK_sourcing_policy_identity_evidence_tenant_id_legal_entity_id~",
            "ak_source_policy_identity_parent" };
        Assert.True(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactPhysicalUniqueIndexes(keys,
            [..keys, ..four]));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactPhysicalUniqueIndexes(keys,
            [..keys, ..four[..3]]));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactPhysicalUniqueIndexes(keys,
            [..keys, ..four, "ux_unreviewed_identity_alias"]));
        Assert.True(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32IndexDefinition(
            ProcurementSourcingPolicyIdentitySchemaReadiness.P32PublishedIndexDefinition));
        Assert.False(ProcurementSourcingPolicyIdentitySchemaReadiness.HasExactP32IndexDefinition(
            ProcurementSourcingPolicyIdentitySchemaReadiness.P32PublishedIndexDefinition.Replace("USING btree", "USING hash", StringComparison.Ordinal)));
    }
}
