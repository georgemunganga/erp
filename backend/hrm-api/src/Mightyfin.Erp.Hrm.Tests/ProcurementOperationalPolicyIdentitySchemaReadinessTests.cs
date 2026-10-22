using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Source/model contract only. Exact manifest and actual finite-role PostgreSQL
/// privilege/constraint/transaction proofs remain the parent's separate gates.</summary>
public sealed class ProcurementOperationalPolicyIdentitySchemaReadinessTests
{
    [Fact]
    public void Captured_PG17_manifest_keeps_exact_policy_keys_hashes_metadata_and_lineage_checks()
    {
        Assert.True(ProcurementOperationalPolicyIdentitySchemaReadiness.ManifestCaptured);
        Assert.Equal("((stage)::text = 'Created'::text)",
            ProcurementOperationalPolicyIdentitySchemaReadiness.PartialPredicates["ux_operational_policy_identity_created"]);
        Assert.Equal("((stage)::text = 'Published'::text)",
            ProcurementOperationalPolicyIdentitySchemaReadiness.PartialPredicates["ux_operational_policy_identity_published"]);
        var check = ProcurementOperationalPolicyIdentitySchemaReadiness.CheckDefinition;
        foreach (var part in new[] { "'RECEIVING'::character varying", "'INVOICE_MATCHING'::character varying",
            "(rules_sha256 IS NOT NULL)", "(evidence_sha256 IS NOT NULL)",
            "(created_evidence_stage IS NOT NULL)", "(submitted_evidence_stage IS NOT NULL)",
            "(created_at = action_at)", "((created_by)::text = (actor_principal_id)::text)",
            "(version = 1)", "(updated_at IS NULL)", "(updated_by IS NULL)",
            "(submitted_evidence_id <> created_evidence_id)", "(policy_record_version_at_action > 2)",
            "(actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)" }) Assert.Contains(part, check);
        Assert.Contains("'^[0-9a-f]{64}$'::text", check);
    }

    [Fact]
    public async Task Unsupported_owner_never_becomes_a_read_or_write_provider_and_has_no_side_effects()
    {
        var scope = new ProcurementRequestScope(); scope.Set("p31", Guid.NewGuid(), "reader", null, null, false, "test");
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var provider = new ProcurementOperationalPolicyIdentitySchemaReadiness();
        Assert.False(await provider.IsReadyAsync(db, default)); Assert.False(await provider.CanRecordAsync(db, default));
        Assert.Empty(db.ChangeTracker.Entries()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("operational_policy_identity_evidence")]
    [InlineData("audit_events")]
    [InlineData("outbox_events")]
    public void Immutable_evidence_has_distinct_read_and_record_permissions_but_no_mutation_bypass(string table)
    {
        var read = ProcurementOperationalPolicyIdentitySchemaReadiness.PrivilegeSql(false);
        var write = ProcurementOperationalPolicyIdentitySchemaReadiness.PrivilegeSql(true);
        foreach (var sql in new[] { read, write })
        {
            Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','SELECT')", sql);
            Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','UPDATE')", sql);
            Assert.Contains($"NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE')", sql);
            Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','DELETE')", sql);
            Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','TRUNCATE')", sql);
        }
        Assert.DoesNotContain($"has_table_privilege(current_user,'procurement.{table}','INSERT')", read);
        Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','INSERT')", write);
    }

    [Fact]
    public void Policy_owner_needs_only_its_own_mutable_DML_not_spending_authority_DML()
    {
        var read = ProcurementOperationalPolicyIdentitySchemaReadiness.PrivilegeSql(false);
        var write = ProcurementOperationalPolicyIdentitySchemaReadiness.PrivilegeSql(true);
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','SELECT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.policy_versions','INSERT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", write);
        foreach (var sql in new[] { read, write })
        {
            Assert.Contains("NOT has_table_privilege(current_user,'procurement.policy_versions','DELETE')", sql);
            Assert.Contains("NOT has_table_privilege(current_user,'procurement.policy_versions','TRUNCATE')", sql);
            Assert.DoesNotContain("has_table_privilege(current_user,'procurement.spending_authority_grants'", sql);
            Assert.DoesNotContain("has_table_privilege(current_user,'procurement.spending_authority_categories'", sql);
            Assert.DoesNotContain("GRANT ", sql); Assert.DoesNotContain("ALTER ", sql);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Shared_Core_identity_ownership_and_elevated_role_guards_remain_required(bool record)
    {
        var sql = ProcurementOperationalPolicyIdentitySchemaReadiness.PrivilegeSql(record);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql, sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql, sql);
        Assert.Contains("'policy_versions','operational_policy_identity_evidence'", sql);
        Assert.Contains("pg_has_role(current_user,c.relowner,'MEMBER')", sql);
        Assert.Contains("pg_has_role(current_user,c.relowner,'SET')", sql);
        Assert.Contains("pg_has_role(current_user,n.nspowner,'SET')", sql);
        Assert.Contains("pg_has_role(current_user,d.datdba,'SET')", sql);
        Assert.Contains("pg_has_role(current_user,r.oid,'SET')", sql);
        Assert.Contains("NOT has_schema_privilege(current_user,'procurement','CREATE')", sql);
        Assert.DoesNotContain("core_approval", sql);
    }

    [Fact]
    public void Expected_model_contains_stable_key_aware_owner_lineage_and_three_unique_stage_indexes()
    {
        Assert.Equal("20261022052824_P21CanonicalGovernedProcurementFoundation", ProcurementOperationalPolicyIdentitySchemaReadiness.MigrationId);
        var scope = new ProcurementRequestScope(); scope.Set("p31", Guid.NewGuid(), "reader", null, null, false, "test");
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-opened.invalid;Database=not-connected").Options, scope);
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(OperationalPolicyIdentityRecord))!;
        Assert.Equal("procurement", entity.GetSchema()); Assert.Equal("operational_policy_identity_evidence", entity.GetTableName());
        Assert.Equal("ck_operational_policy_identity", Assert.Single(entity.GetCheckConstraints()).Name);
        Assert.Equal(new[] { "fk_operational_policy_identity_created", "fk_operational_policy_identity_owner", "fk_operational_policy_identity_submitted" },
            entity.GetForeignKeys().Select(x => x.GetConstraintName()!).Order(StringComparer.Ordinal).ToArray());
        foreach (var fk in entity.GetForeignKeys())
        {
            Assert.Contains(fk.Properties, p => p.Name == "PolicyKey");
            Assert.DoesNotContain(fk.PrincipalKey.Properties, p => p.Name == "Version");
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }
        var owner = entity.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(PolicyVersion));
        Assert.Equal("ak_operational_policy_identity_owner", owner.PrincipalKey.GetName());
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "Id", "PolicyKey", "PolicyVersionNumber" }, owner.PrincipalKey.Properties.Select(x => x.Name));
        Assert.Equal(new[] { "ux_operational_policy_identity_created", "ux_operational_policy_identity_published", "ux_operational_policy_identity_revision" },
            entity.GetIndexes().Where(x => x.IsUnique).Select(x => x.GetDatabaseName()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
