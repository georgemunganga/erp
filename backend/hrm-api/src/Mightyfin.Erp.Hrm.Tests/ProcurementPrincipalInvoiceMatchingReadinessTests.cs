using Mightyfin.Erp.Hrm.Api;

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
        var read=ProcurementPrincipalInvoiceMatchingReadiness.PrivilegeSql(false);
        var write=ProcurementPrincipalInvoiceMatchingReadiness.PrivilegeSql(true);
        Assert.Contains("erp_proc_execute_finite_v1",read,StringComparison.Ordinal);
        Assert.Contains("procurement.commercial_command_receipts','SELECT'",read,StringComparison.Ordinal);
        Assert.DoesNotContain("procurement.commercial_command_receipts','INSERT'",read,StringComparison.Ordinal);
        Assert.Contains("procurement.commercial_command_receipts','INSERT'",write,StringComparison.Ordinal);
        Assert.Contains("core_approval.decisions','INSERT'",write,StringComparison.Ordinal);
        Assert.DoesNotContain("core_approval.decisions','INSERT'",read,StringComparison.Ordinal);
    }

    [Fact]
    public void Principal_readiness_stays_closed_until_native_check_is_captured()
    {
        Assert.Equal("",ProcurementPrincipalInvoiceMatchingReadiness.CapturedAllocationCheck);
    }
}
