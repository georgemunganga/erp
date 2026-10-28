using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderGovernancePhysicalSchemaTests
{
    [Fact]
    public void B2_key_allowance_requires_exact_nonprimary_name_and_ordered_scope()
    {
        var columns = new[] { "TenantId", "LegalEntityId", "Id", "PurchaseOrderId" };
        Assert.True(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesIssuedBinding(
            "ak_invoice_principal_issued_order_binding", columns, false));
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesIssuedBinding(
            "ak_unrelated", columns, false));
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesIssuedBinding(
            "ak_invoice_principal_issued_order_binding", columns, true));
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesIssuedBinding(
            "ak_invoice_principal_issued_order_binding", columns.Skip(1), false));
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesIssuedBinding(
            "ak_invoice_principal_issued_order_binding", columns.Reverse(), false));
    }

    [Fact]
    public void Captured_constraint_cannot_be_reused_under_another_table_or_weakened()
    {
        Assert.NotEmpty(ProcurementPrincipalOrderGovernancePhysicalSchema.CapturedChecks);
        foreach (var (qualifiedName, definition) in ProcurementPrincipalOrderGovernancePhysicalSchema.CapturedChecks)
        {
            Assert.True(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(qualifiedName, definition));
            Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(
                "procurement.unrelated." + qualifiedName.Split('.')[^1], definition));
            Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(
                qualifiedName, definition + " OR TRUE"));
            Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(
                qualifiedName, definition.Replace("CHECK (", "CHECK (TRUE OR ", StringComparison.Ordinal)));
        }
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck("unknown", "CHECK (TRUE)"));
    }

    [Fact]
    public void M5_receipt_and_authority_variants_do_not_admit_other_operations_or_actions()
    {
        const string receipt = "procurement.principal_order_draft_receipts.ck_po_principal_receipt";
        var receipts = ProcurementPrincipalOrderGovernancePhysicalSchema.CapturedChecks[receipt];
        Assert.Contains("ReleaseConfirm", receipts);
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(receipt,
            receipts.Replace("ReleaseConfirm", "ApproveAnything", StringComparison.Ordinal)));
        const string authority = "procurement.spending_authority_identity_evidence.ck_authority_identity_evidence";
        var grant = ProcurementPrincipalOrderGovernancePhysicalSchema.CapturedChecks[authority];
        Assert.Contains("PurchaseOrder.Approve", grant);
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(authority,
            grant.Replace("PurchaseOrder.Approve", "PurchaseOrder.Issue", StringComparison.Ordinal)));
        Assert.False(ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(receipt, grant));
    }

    [Fact]
    public void Physical_read_does_not_require_append_insert_but_both_modes_deny_mutation_and_ownership()
    {
        var read = ProcurementPrincipalOrderGovernancePhysicalSchema.PrivilegeSql(false, "finite_m5_execution");
        var write = ProcurementPrincipalOrderGovernancePhysicalSchema.PrivilegeSql(true, "finite_m5_execution");
        foreach (var table in ProcurementPrincipalOrderGovernancePhysicalSchema.OwnedTables.Values)
        {
            var relation = "'procurement." + table + "'";
            Assert.Contains("has_table_privilege(current_user," + relation + ",'SELECT')", read);
            Assert.DoesNotContain("has_table_privilege(current_user," + relation + ",'INSERT')", read);
            Assert.Contains("has_table_privilege(current_user," + relation + ",'INSERT')", write);
            foreach (var mode in new[] { read, write })
            {
                Assert.Contains("NOT has_any_column_privilege(current_user," + relation + ",'UPDATE')", mode);
                foreach (var privilege in new[] { "DELETE", "TRUNCATE", "TRIGGER" })
                    Assert.Contains("NOT has_table_privilege(current_user," + relation + ",'" + privilege + "')", mode);
                Assert.Contains("c.oid=" + relation + "::regclass", mode);
            }
        }
        Assert.Contains("session_user=current_user AND current_user='finite_m5_execution'", read);
        Assert.Contains("pg_has_role(current_user,c.relowner,'SET')", read);
        Assert.DoesNotContain("'core_approval.cases','INSERT'", write);
        Assert.DoesNotContain("'procurement.policy_versions','UPDATE'", write);
    }
}
