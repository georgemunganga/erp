using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Owning-provider/manifest source checks. Real finite-role catalog, ACL,
/// transaction and mutation-denial proof remains the separate PostgreSQL gate.</summary>
public sealed class ProcurementSpendingAuthorityIdentitySchemaReadinessTests
{
    [Fact]
    public async Task Unsupported_owner_is_unavailable_for_both_reads_and_writes_without_mutation()
    {
        var scope = new ProcurementRequestScope(); scope.Set("p30-owner", Guid.NewGuid(), "reader", null, null, false, "test");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var provider = new ProcurementSpendingAuthorityIdentitySchemaReadiness();
        Assert.False(await provider.IsReadyAsync(owner, default));
        Assert.False(await provider.CanRecordAsync(owner, default));
        Assert.Empty(owner.ChangeTracker.Entries()); Assert.False(owner.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("spending_authority_identity_evidence")]
    [InlineData("audit_events")]
    [InlineData("outbox_events")]
    public void Read_and_record_have_distinct_grants_but_both_reject_append_only_bypasses(string table)
    {
        var read = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(false);
        var write = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(true);
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
    public void Existing_mutable_grant_and_draft_category_contract_is_not_confused_with_immutable_evidence()
    {
        var read = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(false);
        var write = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(true);
        Assert.Contains("has_table_privilege(current_user,'procurement.spending_authority_grants','SELECT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.spending_authority_grants','INSERT')", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.spending_authority_grants','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.spending_authority_grants','UPDATE')", write);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.spending_authority_grants','DELETE')", write);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.spending_authority_grants','TRUNCATE')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.spending_authority_categories','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.spending_authority_categories','DELETE')", write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.spending_authority_categories','UPDATE')", write);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.spending_authority_categories','TRUNCATE')", write);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Neither_read_nor_record_readiness_accepts_Core_identity_writes_or_superuser_schema_authority(bool capture)
    {
        var sql = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(capture);
        Assert.Contains("rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls", sql);
        Assert.Contains("NOT has_schema_privilege(current_user,'procurement','CREATE')", sql);
        Assert.Contains("n.nspname='hrm'", sql);
        Assert.Contains("has_any_column_privilege(current_user,c.oid,'INSERT')", sql);
        Assert.Contains("has_any_column_privilege(current_user,c.oid,'UPDATE')", sql);
        Assert.Contains("has_table_privilege(current_user,c.oid,'DELETE')", sql);
        Assert.Contains("has_table_privilege(current_user,c.oid,'TRUNCATE')", sql);
        Assert.Contains("nspname='hrm' AND has_schema_privilege(current_user,oid,'CREATE')", sql);
        // Approval engine writes are legitimate for PR/PO execution consumers;
        // the identity-read boundary must not demand unrelated Core write rights.
        Assert.DoesNotContain("core_approval", sql);
        Assert.DoesNotContain("GRANT ", sql); Assert.DoesNotContain("ALTER ", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Revoked_DML_does_not_make_an_owner_or_assumable_owner_role_a_finite_runtime_reader(bool capture)
    {
        var sql = ProcurementSpendingAuthorityIdentitySchemaReadiness.PrivilegeSql(capture);
        Assert.Contains("pg_has_role(current_user,c.relowner,'MEMBER')", sql);
        Assert.Contains("pg_has_role(current_user,c.relowner,'SET')", sql);
        Assert.Contains("pg_has_role(current_user,n.nspowner,'MEMBER')", sql);
        Assert.Contains("d.datname=current_database()", sql);
        Assert.Contains("pg_has_role(current_user,d.datdba,'SET')", sql);
        Assert.Contains("pg_has_role(current_user,r.oid,'SET')", sql);
    }

    [Fact]
    public void Frozen_actual_PG17_manifest_keeps_all_stage_hash_and_original_actor_constraints()
    {
        Assert.Equal("20261021114959_P30SpendingAuthorityWorkerIdentity", ProcurementSpendingAuthorityIdentitySchemaReadiness.MigrationId);
        Assert.Equal("((stage)::text = 'Created'::text)", ProcurementSpendingAuthorityIdentitySchemaReadiness.PartialPredicates["ux_authority_identity_origin"]);
        Assert.Equal("((stage)::text = 'Activated'::text)", ProcurementSpendingAuthorityIdentitySchemaReadiness.PartialPredicates["ux_authority_identity_activation"]);
        var check = ProcurementSpendingAuthorityIdentitySchemaReadiness.CheckDefinition;
        foreach (var fragment in new[] {"(business_fingerprint IS NOT NULL)", "(evidence_sha256 IS NOT NULL)",
            "(created_evidence_stage IS NOT NULL)", "(submitted_evidence_stage IS NOT NULL)",
            "(created_at = action_at)", "((created_by)::text = (actor_principal_id)::text)",
            "(updated_at IS NULL)", "(updated_by IS NULL)", "(version = 1)",
            "(recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)",
            "(submitted_evidence_id <> created_evidence_id)"}) Assert.Contains(fragment, check);
        Assert.Contains("'^[0-9a-fA-F]{64}$'::text", check); // Existing business fingerprints retain their original alphabet.
        Assert.Contains("'^[0-9a-f]{64}$'::text", check); // New evidence digest remains canonical lowercase.
    }

    [Fact]
    public void Actual_model_keeps_named_scoped_FKs_stable_keys_and_expected_unique_stage_indexes()
    {
        var scope = new ProcurementRequestScope(); scope.Set("p30-model", Guid.NewGuid(), "reader", null, null, false, "test");
        using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-opened.invalid;Database=not-connected").Options, scope);
        var model = owner.GetService<IDesignTimeModel>().Model;
        var identity = model.FindEntityType(typeof(SpendingAuthorityIdentityRecord))!;
        var table = StoreObjectIdentifier.Table(identity.GetTableName()!, identity.GetSchema());
        Assert.Equal("procurement", table.Schema); Assert.Equal("spending_authority_identity_evidence", table.Name);
        Assert.Equal("ck_authority_identity_evidence", Assert.Single(identity.GetCheckConstraints()).Name);
        var foreigns = identity.GetForeignKeys().ToArray(); Assert.Equal(3, foreigns.Length);
        Assert.Equal(new[] {"fk_authority_identity_created", "fk_authority_identity_grant", "fk_authority_identity_submitted"},
            foreigns.Select(x=>x.GetConstraintName()!).Order(StringComparer.Ordinal).ToArray());
        foreach (var foreign in foreigns)
        {
            Assert.Equal(DeleteBehavior.Restrict, foreign.DeleteBehavior);
            Assert.Equal(new[] {"TenantId", "LegalEntityId", "GrantId"}, foreign.Properties.Take(3).Select(x=>x.Name));
            Assert.DoesNotContain(foreign.PrincipalKey.Properties,x=>x.Name=="Version");
        }
        Assert.Equal(new[] {"ux_authority_identity_action_revision", "ux_authority_identity_activation", "ux_authority_identity_origin"},
            identity.GetIndexes().Where(x=>x.IsUnique).Select(x=>x.GetDatabaseName()!).Order(StringComparer.Ordinal).ToArray());
        Assert.Null(owner.Database.CurrentTransaction); Assert.Empty(owner.ChangeTracker.Entries());
    }
}
