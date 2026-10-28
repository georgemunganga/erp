using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Negative admission/compatibility tests. Real finite-role database and
/// HTTP workflow evidence remain separate from these source contract tests.</summary>
public sealed class ProcurementInvoiceOwnerBaselineReadinessTests
{
    [Fact]
    public async Task Unsupported_store_cannot_read_or_record_and_never_stages_changes()
    {
        var scope=new ProcurementRequestScope();scope.Set("m6",Guid.NewGuid(),"reader",null,null,false,"test");
        await using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,scope);
        var inspector=new ProcurementInvoiceOwnerBaselineReadiness(new ConfigurationBuilder().Build());
        Assert.False(await inspector.CanReadAsync(db,default));
        Assert.False(await inspector.CanRecordAsync(db,default));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void New_policy_schema_never_qualifies_as_the_legacy_four_key_contract()
    {
        const string name="procurement.operational_policy_identity_evidence.ck_operational_policy_identity";
        var legacy=ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition;
        Assert.False(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(name,legacy));
        Assert.False(ProcurementRestoreCheckAlternatives.Matches(name,legacy,
            ProcurementInvoiceOwnerBaselineSchemaCapture.OperationalCheckDefinition));
        Assert.NotEqual(ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint,
            ProcurementInvoiceOwnerBaselineSchemaCapture.ManualPaymentCatalogFingerprint);
    }

    [Fact]
    public void Only_observed_named_native_and_restored_constraints_are_accepted()
    {
        Assert.True(ProcurementInvoiceOwnerBaselineSchemaCapture.Populated);
        foreach(var pair in ProcurementInvoiceOwnerBaselineSchemaCapture.Checks)
        {
            Assert.True(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(pair.Key,pair.Value));
            Assert.False(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches("other."+pair.Key,pair.Value));
            Assert.False(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(pair.Key,pair.Value+" OR true"));
        }
        foreach(var pair in ProcurementInvoiceOwnerBaselineSchemaCapture.RestoredChecks)
        {
            Assert.True(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(pair.Key,pair.Value));
            Assert.False(ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(pair.Key,pair.Value.Replace(" > 0)"," >= 0)",StringComparison.Ordinal)+" "));
        }
    }

    [Fact]
    public void Storage_admission_keeps_complete_inventory_readonly_identity_and_append_only_proofs()
    {
        var read=ProcurementInvoiceOwnerBaselineReadiness.PrivilegeSql(false,"finite_role");
        var write=ProcurementInvoiceOwnerBaselineReadiness.PrivilegeSql(true,"finite_role");
        foreach(var table in InvoiceProcurementOwnerTables.Names)
            Assert.Contains($"has_table_privilege(current_user,'procurement.{table}','SELECT')",read);
        foreach(var table in ProcurementInvoiceOwnerBaselineSchemaCapture.Tables)
        {
            Assert.Contains($"NOT has_any_column_privilege(current_user,'{table}','UPDATE')",write);
            Assert.Contains($"NOT has_table_privilege(current_user,'{table}','DELETE,TRUNCATE,TRIGGER')",write);
            Assert.Contains($"has_table_privilege(current_user,'{table}','INSERT')",write);
            Assert.DoesNotContain($"has_table_privilege(current_user,'{table}','INSERT')",read);
        }
        Assert.Contains("session_user=current_user AND current_user='finite_role'",write);
        Assert.Contains("has_table_privilege(current_user,'core_approval.cases','INSERT')",write);
        Assert.Contains("has_table_privilege(current_user,'core_approval.cases','UPDATE')",write);
        Assert.DoesNotContain("'INSERT,UPDATE'",write);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs','INSERT')",write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','INSERT')",write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','UPDATE')",write);
        Assert.DoesNotContain("GRANT ",write);
        Assert.DoesNotContain("ALTER ",write);
    }
}
