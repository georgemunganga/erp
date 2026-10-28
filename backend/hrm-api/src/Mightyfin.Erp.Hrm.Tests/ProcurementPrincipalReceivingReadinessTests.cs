using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalReceivingReadinessTests
{
    [Fact]
    public void Successor_allowance_requires_exact_parent_name_column_order_and_unique_unfiltered_index()
    {
        string[] columns = ["TenantId", "LegalEntityId", "Id", "PurchaseOrderId", "SubmissionId",
            "FundingEvidenceId", "CommercialSnapshotSha256", "OriginalDirectSourceSha256"];
        Assert.True(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_parent", columns, true, null));
        Assert.False(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderAcknowledgment), "ux_prcv_issued_parent", columns, true, null));
        Assert.False(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderIssuedVersion), "ux_prcv_other_parent", columns, true, null));
        Assert.False(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_parent", columns.Reverse(), true, null));
        Assert.False(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_parent", columns, false, null));
        Assert.False(ProcurementPrincipalReceivingParentIndexes.Matches(typeof(PrincipalOrderIssuedVersion), "ux_prcv_issued_parent", columns, true, "true"));
        Assert.Equal(5, ProcurementPrincipalReceivingParentIndexes.ParentTypes.Sum(ProcurementPrincipalReceivingParentIndexes.ExpectedCount));
    }

    [Fact]
    public void Read_requires_history_but_no_insert_or_counter_update_and_write_requires_only_receiving_effects()
    {
        var read = ProcurementPrincipalReceivingPhysicalSchema.PrivilegeSql(false, "finite_receiving");
        var write = ProcurementPrincipalReceivingPhysicalSchema.PrivilegeSql(true, "finite_receiving");
        foreach (var table in new[] { "receiving_source_states", "goods_receipt_lines", "service_acceptances" })
            Assert.Contains("has_table_privilege(current_user,'procurement." + table + "','SELECT')", read);
        Assert.DoesNotContain(",'INSERT')", read);
        Assert.DoesNotContain("has_column_privilege", read);
        Assert.Contains("has_column_privilege(current_user,'procurement.purchase_order_lines','accepted_quantity','UPDATE')", write);
        Assert.Contains("has_column_privilege(current_user,'procurement.purchase_request_lines','accepted_quantity','UPDATE')", write);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.purchase_order_lines','INSERT')", write);
        foreach (var table in new[] { "principal_receiving_facts", "principal_receiving_revisions", "principal_receiving_receipts", "principal_receiving_line_bindings", "audit_events", "outbox_events" })
        {
            Assert.Contains("NOT has_any_column_privilege(current_user,'procurement." + table + "','UPDATE')", read);
            Assert.Contains("has_table_privilege(current_user,'procurement." + table + "','INSERT')", write);
        }
        Assert.Contains("session_user=current_user AND current_user='finite_receiving'", read);
        Assert.Contains("pg_has_role(current_user,r.oid,'SET')", read);
        Assert.DoesNotContain("'procurement.policy_versions','UPDATE'", write);
        Assert.DoesNotContain("'core_approval.cases','INSERT'", write);
    }
}
