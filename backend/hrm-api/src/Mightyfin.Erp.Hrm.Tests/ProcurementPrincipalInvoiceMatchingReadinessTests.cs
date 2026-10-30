using Mightyfin.Erp.Hrm.Api;
using Microsoft.Extensions.Configuration;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceMatchingReadinessTests
{
    [Fact]
    public void M6b_exact_parent_and_child_tuples_match_the_genuine_generated_migration()
    {
        var proof=ProcurementPrincipalInvoiceMatchingReadiness.ParentKeys;
        Assert.Equal(2,proof.Count);
        Assert.Equal(
            new[]{"tenant_id","legal_entity_id","id","purchase_order_id","issued_version_id","purchase_order_line_id"},
            proof["AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~"]);
        Assert.Equal(new[]{"tenant_id","legal_entity_id","id","source_state_id"},
            proof["AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~"]);
        var foreign=ProcurementPrincipalInvoiceMatchingReadiness.ForeignKeys;
        Assert.Equal(2,foreign.Count);
        Assert.Equal(proof["AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~"],
            foreign["fk_invoice_allocation_principal_source_line"].To);
        Assert.Equal(proof["AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~"],
            foreign["fk_invoice_allocation_principal_fact_source"].To);
        Assert.Equal(2,ProcurementPrincipalInvoiceMatchingReadiness.AllocationIndexes.Count);
    }

    [Fact]
    public void Selector_readiness_does_not_require_baseline_or_matching_insert()
    {
        var read=ProcurementPrincipalInvoiceMatchingReadiness.PrivilegeSql(false,"p25_execute_fixture");
        var write=ProcurementPrincipalInvoiceMatchingReadiness.PrivilegeSql(true,"p25_execute_fixture");
        Assert.Contains("session_user=current_user AND current_user='p25_execute_fixture'",read,StringComparison.Ordinal);
        Assert.Contains("procurement.commercial_command_receipts','SELECT'",read,StringComparison.Ordinal);
        Assert.DoesNotContain("procurement.commercial_command_receipts','INSERT'",read,StringComparison.Ordinal);
        Assert.Contains("procurement.commercial_command_receipts','INSERT'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("core_approval.decisions','INSERT'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("core_approval.decisions','INSERT'",read,StringComparison.Ordinal);
        foreach(var table in new[]{"invoice_match_mapping_versions","invoice_match_allocations",
            "invoice_match_results","invoice_match_exposure_states","invoice_exceptions",
            "commercial_command_receipts","audit_events","outbox_events"})
            Assert.Contains("'procurement."+table+"','INSERT'",write,StringComparison.Ordinal);
        foreach(var table in new[]{"invoice_owner_baselines","invoice_owner_baseline_rows",
            "invoice_owner_command_receipts"})
            Assert.DoesNotContain("'procurement."+table+"','INSERT'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("'core_approval.cases','INSERT'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("'core_approval.assignments','UPDATE'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.supplier_invoices','UPDATE')",
            read,StringComparison.Ordinal);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.supplier_invoices','UPDATE')",
            write,StringComparison.Ordinal);
        Assert.Equal(new[]{"current_mapping_version_id","mapping_revision","matching_state",
            "last_match_result_id","match_revision","active_match_claim_result_id",
            "version","updated_at","updated_by"},
            ProcurementPrincipalInvoiceMatchingReadiness.SupplierInvoiceWriteColumns);
        foreach(var column in ProcurementPrincipalInvoiceMatchingReadiness.SupplierInvoiceWriteColumns)
        {
            Assert.Contains("has_column_privilege(current_user,'procurement.supplier_invoices','"+column+"','UPDATE')",
                write,StringComparison.Ordinal);
            Assert.DoesNotContain("has_column_privilege(current_user,'procurement.supplier_invoices','"+column+"','UPDATE')",
                read,StringComparison.Ordinal);
        }
        Assert.DoesNotContain("has_column_privilege(current_user,'procurement.supplier_invoices','id','UPDATE')",
            write,StringComparison.Ordinal);
        Assert.Contains("NOT has_database_privilege(current_user,current_database(),'CREATE')",write,StringComparison.Ordinal);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','UPDATE')",write,StringComparison.Ordinal);
        var privileged=ProcurementPrincipalInvoiceMatchingReadiness.PrivilegeSql(false,"postgres");
        Assert.Contains("current_user='postgres'",privileged,StringComparison.Ordinal);
        Assert.Contains("r.rolsuper OR r.rolcreatedb",privileged,StringComparison.Ordinal);
        Assert.Contains("pg_has_role(current_user,c.relowner,'SET')",privileged,StringComparison.Ordinal);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','UPDATE')",privileged,StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_finite_execution_identity_must_match_the_actual_shared_connection()
    {
        const string record="Host=/var/run/postgresql;Port=5432;Database=private_erp;Username=p25_record_fixture";
        const string execution="Host=/var/run/postgresql;Port=5432;Database=private_erp;Username=p25_execute_fixture";
        IConfiguration Config(string? r,string? e)=>new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["ConnectionStrings:Procurement"]=r,
                ["ConnectionStrings:ProcurementApprovalExecution"]=e }).Build();
        Assert.True(ProcurementPrincipalInvoiceMatchingReadiness.TryConfiguredExecutionRole(
            Config(record,execution),execution,out var role));
        Assert.Equal("p25_execute_fixture",role);
        Assert.False(ProcurementPrincipalInvoiceMatchingReadiness.TryConfiguredExecutionRole(
            Config(record,execution),record,out _));
        Assert.False(ProcurementPrincipalInvoiceMatchingReadiness.TryConfiguredExecutionRole(
            Config(record,execution),execution.Replace("private_erp","other_private",StringComparison.Ordinal),out _));
        Assert.False(ProcurementPrincipalInvoiceMatchingReadiness.TryConfiguredExecutionRole(
            Config(null,execution),execution,out _));
        Assert.False(ProcurementPrincipalInvoiceMatchingReadiness.TryConfiguredExecutionRole(
            Config(record,"Username=missing"),execution,out _));
    }

    [Fact]
    public void Allocation_check_matches_the_retained_native_capture()
    {
        Assert.Equal("cd6f76386006c3420c302e8e0b2f604cd80061560c73b9fb88ae2fc9bb216393",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(ProcurementPrincipalInvoiceMatchingReadiness.CapturedAllocationCheck))).ToLowerInvariant());
    }
}
