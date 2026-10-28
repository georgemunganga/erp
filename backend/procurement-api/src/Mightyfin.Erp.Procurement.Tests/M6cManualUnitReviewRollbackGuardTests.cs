using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M6cManualUnitReviewRollbackGuardTests
{
    private static string Guard()
    {
        var type=typeof(ProcurementDbContext).Assembly.GetType(
            "Mightyfin.Erp.Procurement.Infrastructure.Data.M6cManualUnitReviewRollbackGuards",true)!;
        return Assert.IsType<string>(type.GetField("BeforeDownSql",BindingFlags.Static|BindingFlags.NonPublic)!.GetRawConstantValue());
    }
    [Fact]
    public void Genuine_C_Down_runs_the_retention_guard_before_the_first_CHECK_drop()
    {
        var type=typeof(ProcurementDbContext).Assembly.GetType(
            "Mightyfin.Erp.Procurement.Infrastructure.Migrations.M6cManualInvoiceUnitAttestation",true)!;
        var migration=Assert.IsAssignableFrom<Migration>(Activator.CreateInstance(type));
        var first=Assert.IsType<SqlOperation>(migration.DownOperations[0]);
        Assert.Equal(Guard(),first.Sql);Assert.False(first.SuppressTransaction);
        var next=Assert.IsType<DropCheckConstraintOperation>(migration.DownOperations[1]);
        Assert.Equal("invoice_unit_review_outcomes",next.Table);Assert.Equal("ck_inv_unit_outcome",next.Name);
    }
    [Fact]
    public void C_guard_protects_manual_pending_receipt_policy_and_terminal_markers_without_B_or_Core_veto()
    {
        var sql=Guard();
        Assert.StartsWith("LOCK TABLE procurement.invoice_unit_review_sources,",sql.TrimStart());
        Assert.Contains("procurement.invoice_unit_review_assignments",sql);
        Assert.Contains("IN ACCESS EXCLUSIVE MODE;",sql);
        Assert.Contains("source_json::jsonb->>'SourceSchemaVersion'='4'",sql);
        Assert.Contains("request_json::jsonb->>'schemaVersion'='2'",sql);
        Assert.Contains("response_json::jsonb->>'evidenceSchemaVersion'='4'",sql);
        Assert.Contains("evidence_schema_version=4",sql);
        Assert.Contains("rules_json::jsonb->>'schemaVersion'='2'",sql);
        Assert.Contains("rules_json::jsonb->>'evidenceMode'='ManualAttestation'",sql);
        Assert.Contains("identity.stage='Published'",sql);
        Assert.Contains("policy.tenant_id=identity.tenant_id",sql);
        Assert.Contains("policy.legal_entity_id=identity.legal_entity_id",sql);
        Assert.Contains("'ManualUnitReviewOpened','ManualUnitReviewDecision'",sql);
        Assert.Contains("'invoice.manual_units_reviewed.v1'",sql);
        Assert.Contains("ERRCODE='55000'",sql);
        Assert.DoesNotContain("core_approval",sql);Assert.DoesNotContain("invoice_owner_baselines",sql);
        Assert.DoesNotContain("invoice_match_mapping_versions",sql);Assert.DoesNotContain("source_schema_version=3",sql);
        Assert.DoesNotContain("DELETE FROM",sql);Assert.DoesNotContain("UPDATE ",sql);Assert.DoesNotContain("TRUNCATE",sql);
    }
}
