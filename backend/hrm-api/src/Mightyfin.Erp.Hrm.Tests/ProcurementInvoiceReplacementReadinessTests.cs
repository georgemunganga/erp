using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Contract/denial tests. Positive finite-role execution is a separate
/// genuine E UAT gate; these tests never grant roles or enable the provider.</summary>
public sealed class ProcurementInvoiceReplacementReadinessTests
{
    [Fact]
    public async Task Unsupported_store_cannot_read_or_record_and_stages_no_effects()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("replacement-readiness", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "test");
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var readiness = new ProcurementInvoiceReplacementSchemaReadiness(new ConfigurationBuilder().Build());
        Assert.False(await readiness.CanReadAsync(db, default));
        Assert.False(await readiness.CanRecordAsync(db, default));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Read_profile_preserves_history_without_INSERT_while_record_profile_requires_append_and_lock_rights()
    {
        var read = ProcurementInvoiceReplacementSchemaReadiness.PrivilegeSql(false, "finite_role");
        var write = ProcurementInvoiceReplacementSchemaReadiness.PrivilegeSql(true, "finite_role");
        foreach (var table in ProcurementInvoiceReplacementNativeShape.Tables)
        {
            Assert.Contains($"has_table_privilege(current_user,'{table}','SELECT')", read);
            Assert.Contains($"NOT has_any_column_privilege(current_user,'{table}','UPDATE')", read);
            Assert.Contains($"NOT has_table_privilege(current_user,'{table}','DELETE,TRUNCATE,TRIGGER')", read);
            Assert.DoesNotContain($"has_table_privilege(current_user,'{table}','INSERT')", read);
            Assert.Contains($"has_table_privilege(current_user,'{table}','INSERT')", write);
        }
        foreach (var table in InvoiceProcurementOwnerTables.Names)
            Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','SELECT')", read);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_versions','SELECT')", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.invoice_intake_command_receipts','SELECT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.invoice_intake_command_receipts','INSERT')", read);
        foreach (var table in new[] { "supplier_invoices", "supplier_invoice_lines", "invoice_amount_calculations", "invoice_intake_command_receipts" })
            Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','INSERT')", write);
        foreach (var table in new[] { "purchase_requests", "purchase_orders", "supplier_invoices", "invoice_match_exposure_states" })
            Assert.Contains($"has_any_column_privilege(current_user,'procurement.{table}','UPDATE')", write);
        Assert.DoesNotContain("'INSERT,UPDATE'", write);
        Assert.DoesNotContain("GRANT ", write);
        Assert.Contains("session_user=current_user AND current_user='finite_role'", read);
        Assert.Contains("pg_read_all_data", read);
        Assert.Contains("NOT has_database_privilege", read);
        Assert.Contains("NOT pg_is_in_recovery()", write);
        Assert.DoesNotContain("CaptureEnabled", read);Assert.DoesNotContain("Enabled", read);
    }

    private static IConfiguration Connections() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> {
            ["ConnectionStrings:Procurement"] = "Host=127.0.0.1;Database=never_opened_replacement;Username=record_finite",
            ["ConnectionStrings:ProcurementApprovalExecution"] = "Host=127.0.0.1;Database=never_opened_replacement;Username=execution_finite"
        }).Build();

    [Fact]
    public void History_accepts_its_actual_record_or_execution_connection_but_new_work_uses_record_only()
    {
        var config = Connections();
        var record = config.GetConnectionString("Procurement")!;
        var execution = config.GetConnectionString("ProcurementApprovalExecution")!;
        Assert.True(ProcurementInvoiceReplacementSchemaReadiness.TrustedRole(config, record, false, out var reader));
        Assert.Equal("record_finite", reader);
        Assert.True(ProcurementInvoiceReplacementSchemaReadiness.TrustedRole(config, execution, false, out reader));
        Assert.Equal("execution_finite", reader);
        Assert.False(ProcurementInvoiceReplacementSchemaReadiness.TrustedRole(config, execution, true, out _));
        Assert.True(ProcurementInvoiceReplacementSchemaReadiness.TrustedRole(config, record, true, out _));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=wrong_database;Username=record_finite")]
    [InlineData("Host=127.0.0.1;Database=never_opened_replacement;Username=postgres")]
    [InlineData("Host=127.0.0.2;Database=never_opened_replacement;Username=record_finite")]
    [InlineData("not-a-connection-string")]
    public void Wrong_database_or_role_never_becomes_a_trusted_profile(string actual)
        => Assert.False(ProcurementInvoiceReplacementSchemaReadiness.TrustedRole(Connections(), actual, false, out _));

    [Fact]
    public void Genuine_native_capture_has_exact_two_owned_tables_and_original_constraint_definitions()
    {
        var shape = JsonNode.Parse(ProcurementInvoiceReplacementNativeShape.Json)!.AsObject();
        Assert.Equal(2, shape.Count);
        Assert.True(ProcurementInvoiceReplacementNativeShape.Matches(shape.ToJsonString()));
        Assert.Equal("657a3fa9f4b911b4f6cf69cb71fe5698dfd635dfc1933f146e1c16513a9e3fba",
            ProcurementInvoiceReplacementNativeShape.CaptureSha256);
        var link = shape["procurement.invoice_replacement_links"]!;
        Assert.Equal(30, link["columns"]!.AsArray().Count);
        Assert.Equal(11, link["constraints"]!.AsArray().Count);
        Assert.Equal(10, link["indexes"]!.AsArray().Count);
        Assert.Empty(link["nonInternalTriggers"]!.AsArray());
    }

    [Theory]
    [InlineData("rls")][InlineData("nullableHash")][InlineData("missingFk")]
    [InlineData("weakenedCheck")][InlineData("nonUniqueKey")][InlineData("trigger")][InlineData("extraColumn")]
    public void Weakened_or_extra_physical_schema_is_rejected(string fault)
    {
        var shape = JsonNode.Parse(ProcurementInvoiceReplacementNativeShape.Json)!;
        var link = shape["procurement.invoice_replacement_links"]!;
        if (fault == "rls") link["rls"] = true;
        if (fault == "nullableHash") link["columns"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "source_original_sha256")!["notNull"] = false;
        if (fault == "missingFk") {
            var constraints = link["constraints"]!.AsArray();
            constraints.Remove(constraints.First(x => x!["kind"]!.GetValue<string>() == "f"));
        }
        if (fault == "weakenedCheck") {
            var check = link["constraints"]!.AsArray().Single(x => x!["kind"]!.GetValue<string>() == "c")!;
            check["definition"] = check["definition"]!.GetValue<string>() + " OR true";
        }
        if (fault == "nonUniqueKey") link["indexes"]![0]!["definition"] = "CREATE INDEX unqualified_key";
        if (fault == "trigger") link["nonInternalTriggers"]!.AsArray().Add("unexpected trigger");
        if (fault == "extraColumn") link["columns"]!.AsArray().Add(new JsonObject { ["name"] = "unexpected" });
        Assert.False(ProcurementInvoiceReplacementNativeShape.Matches(shape.ToJsonString()));
    }
}
