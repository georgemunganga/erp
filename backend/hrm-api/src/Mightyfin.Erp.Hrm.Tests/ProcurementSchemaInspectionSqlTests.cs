using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>SQL contract tests only; actual PostgreSQL discovery is a separate release gate.</summary>
public sealed class ProcurementSchemaInspectionSqlTests
{
    private static readonly StoreObjectIdentifier Child = StoreObjectIdentifier.Table("child", "procurement");
    private static readonly StoreObjectIdentifier Parent = StoreObjectIdentifier.Table("parent", "procurement");

    [Fact]
    public void Existing_P28_foreign_key_contract_is_unchanged_when_no_name_is_requested()
    {
        Assert.Equal("SELECT EXISTS(SELECT 1 FROM pg_constraint c WHERE c.contype='f' AND c.convalidated AND NOT c.condeferrable AND NOT c.condeferred AND c.confdeltype='r' AND c.confupdtype='a' AND c.confmatchtype='s' AND c.conrelid='procurement.child'::regclass AND c.confrelid='procurement.parent'::regclass AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.p)=ARRAY['tenant_id','parent_id']::text[] AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.p)=ARRAY['tenant_id','id']::text[])",
            ProcurementSchemaInspectionSql.ForeignKeySql(Child, Parent, ["tenant_id", "parent_id"], ["tenant_id", "id"]));
    }

    [Fact]
    public void Named_foreign_key_preserves_columns_actions_validation_and_requires_exact_name()
    {
        var baseline = ProcurementSchemaInspectionSql.ForeignKeySql(Child, Parent, ["tenant_id", "parent_id"], ["tenant_id", "id"]);
        Assert.Equal(baseline[..^1] + " AND c.conname='fk_owner')",
            ProcurementSchemaInspectionSql.ForeignKeySql(Child, Parent, ["tenant_id", "parent_id"], ["tenant_id", "id"], "fk_owner"));
    }

    [Fact]
    public void Partial_unique_predicate_is_exact_quoted_text_not_a_loosened_boolean_rewrite()
    {
        var old = ProcurementSchemaInspectionSql.UniqueIndexSql(Child, ["tenant_id", "id"]);
        Assert.Contains("AND i.indpred IS NULL AND i.indnkeyatts=i.indnatts", old);
        Assert.DoesNotContain("i.indexrelid=", old);
        var partial = ProcurementSchemaInspectionSql.UniqueIndexSql(Child, ["tenant_id", "id"], "ux_created", "((stage)::text = 'Created'::text)");
        Assert.DoesNotContain("i.indpred IS NULL", partial);
        Assert.Contains("pg_get_expr(i.indpred,i.indrelid)='((stage)::text = ''Created''::text)'", partial);
        Assert.Contains("i.indexrelid='\"procurement\".\"ux_created\"'::regclass", partial);
        Assert.Contains("i.indisunique AND i.indisvalid AND i.indimmediate AND i.indexprs IS NULL", partial);
    }

    [Fact]
    public void Named_index_inspection_distinguishes_unique_from_reader_index()
    {
        var unique = ProcurementSchemaInspectionSql.IndexSql(Child,
            ["tenant_id", "legal_entity_id", "source_id"], "ux_source", true);
        var reader = ProcurementSchemaInspectionSql.IndexSql(Child,
            ["tenant_id", "legal_entity_id", "source_id"], "ix_source", false);
        Assert.Contains("i.indisunique=true", unique);
        Assert.Contains("i.indisunique=false", reader);
        Assert.Contains("i.indisvalid AND i.indisready AND i.indislive", reader);
        Assert.Contains("i.indpred IS NULL", reader);
        Assert.Contains("i.indexrelid='\"procurement\".\"ix_source\"'::regclass", reader);
        Assert.Contains("ARRAY['tenant_id','legal_entity_id','source_id']::text[]", reader);
    }

    [Theory]
    [InlineData("procurement.purchase_request_approval_outcomes.ck_pr_approval_outcome_evidence")]
    [InlineData("procurement.purchase_request_finance_release_requests.ck_pr_finance_release_request")]
    public void Captured_P25_checks_accept_only_the_two_exact_restored_definitions(string key)
    {
        var expected = ProcurementPrincipalApprovalStoreReadiness.CheckDefinitions[key];
        var restored = ProcurementPrincipalApprovalStoreReadiness.RestoredCheckDefinitions[key];
        Assert.NotEqual(expected, restored);
        Assert.True(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, expected, expected));
        Assert.True(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, expected, restored));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key + "-other", expected, expected));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key + "-other", expected, restored));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, restored, restored));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, expected,
            restored.Replace("'Returned'", "'Denied'", StringComparison.Ordinal)));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, expected,
            restored.Replace("'Rejected'", "'Returned'", StringComparison.Ordinal)));
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.MatchesCapturedCheck(key, expected,
            restored + " AND true"));
    }
}
