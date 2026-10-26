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
        var readiness = new ProcurementSourcingPolicyIdentitySchemaReadiness();
        Assert.False(await readiness.IsReadyAsync(owner, default));
        Assert.False(await readiness.IsReadReadyAsync(owner, default));
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
    public void Publication_policy_read_requires_current_identity_without_authoring_rights()
    {
        var write = ProcurementSourcingPolicyIdentitySchemaReadiness.PrivilegeSql;
        var read = ProcurementSourcingPolicyIdentitySchemaReadiness.ReadPrivilegeSql;
        Assert.Contains("has_table_privilege(current_user,'procurement.sourcing_policy_identity_evidence','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", write);
        foreach (var table in new[] { "sourcing_policy_identity_evidence", "policy_versions" })
        {
            Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','SELECT')", read);
            Assert.Contains($"NOT has_any_column_privilege(current_user,'procurement.{table}','INSERT')", read);
            Assert.Contains($"NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE')", read);
            Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','DELETE')", read);
        }
        Assert.Contains("has_table_privilege(current_user,'procurement.__procurement_migrations','SELECT')", read);
        Assert.Contains("NOT has_schema_privilege(current_user,'procurement','CREATE')", read);
    }

    [Fact]
    public void Manifest_preserves_actual_named_partial_predicates_and_null_guards()
    {
        Assert.Equal("20261026155612_M4PrincipalRfqPublicationReview", ProcurementSourcingPolicyIdentitySchemaReadiness.MigrationId);
        Assert.Equal("((stage)::text = 'Created'::text)", ProcurementSourcingPolicyIdentitySchemaReadiness.PartialPredicates["ux_source_policy_identity_created"]);
        Assert.Equal("((stage)::text = 'Published'::text)", ProcurementSourcingPolicyIdentitySchemaReadiness.PartialPredicates["ux_source_policy_identity_published"]);
        var definition = ProcurementSourcingPolicyIdentitySchemaReadiness.CheckDefinition;
        Assert.Contains("(rules_sha256 IS NOT NULL)", definition);
        Assert.Contains("(evidence_sha256 IS NOT NULL)", definition);
        Assert.Contains("(created_evidence_stage IS NOT NULL)", definition);
        Assert.Contains("(submitted_evidence_stage IS NOT NULL)", definition);
        Assert.Contains("((schema_version = 1)", definition);
    }
}
