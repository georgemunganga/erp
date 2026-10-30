using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementInvoiceCurrencySchemaReadinessTests
{
    [Fact]
    public void Read_mode_requires_both_owned_relations_and_only_Core_version_data()
    {
        var sql = ProcurementInvoiceCurrencySchemaReadiness.PrivilegeSql(InvoiceCurrencySchemaMode.RecordRead, "record_test");
        Assert.Contains("has_table_privilege(current_user,'procurement.invoice_amount_calculations','SELECT')", sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.invoice_intake_command_receipts','SELECT')", sql);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_versions','SELECT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'hrm.currency_precision_command_receipts','SELECT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.invoice_intake_command_receipts','INSERT')", sql);
    }

    [Fact]
    public void Capture_requires_both_appends_and_never_Core_write_or_child_update()
    {
        var sql = ProcurementInvoiceCurrencySchemaReadiness.PrivilegeSql(InvoiceCurrencySchemaMode.RecordCapture, "record_test");
        foreach (var table in new[] { "procurement.invoice_amount_calculations", "procurement.invoice_intake_command_receipts" })
        {
            Assert.Contains("has_table_privilege(current_user,'" + table + "','INSERT')", sql);
            Assert.Contains("NOT has_any_column_privilege(current_user,'" + table + "','UPDATE')", sql);
            Assert.Contains("NOT has_table_privilege(current_user,'" + table + "','DELETE,TRUNCATE,TRIGGER')", sql);
        }
        Assert.Contains("NOT has_any_column_privilege(current_user,'hrm.currency_precision_versions','INSERT')", sql);
    }

    [Fact]
    public void Execution_can_read_recovery_evidence_but_cannot_create_intake_rows()
    {
        var sql = ProcurementInvoiceCurrencySchemaReadiness.PrivilegeSql(InvoiceCurrencySchemaMode.ExecutionRead, "execute_test");
        foreach (var table in new[] { "procurement.invoice_amount_calculations", "procurement.invoice_intake_command_receipts" })
            Assert.Contains("NOT has_any_column_privilege(current_user,'" + table + "','INSERT')", sql);
        Assert.Contains("pg_read_all_data", sql); Assert.Contains("rolbypassrls", sql);
        Assert.Equal(CoreCurrencyPrecisionSchemaReadiness.MigrationId,
            ProcurementInvoiceCurrencySchemaReadiness.CoreMigrationId);
        Assert.Equal("20261030042444_M6dInvoiceIntakeReceipts",
            ProcurementInvoiceCurrencySchemaReadiness.ReceiptMigrationId);
    }
}
