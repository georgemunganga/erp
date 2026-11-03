using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Hrm.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanReviewStorageGuardTests
{
    [Fact]
    public void Extraction_preserves_the_original_B1_SQL_bytes_and_projection_instances()
    {
        // Frozen pre-extraction UTF8 SQL, from Plan prerequisite 469ce32d.
        // Changes to shared optional projections require an explicit contract review.
        Assert.Equal("05a824e242ae8dd146ee9a658e80821acc379736dfe9a5bcf5e5d781111c61ac",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                ProcurementCommercialRoundCoreReadGuard.Sql))));
        Assert.Equal(ProcurementCoreReadProjectionGuard.Sql, ProcurementCommercialRoundCoreReadGuard.Sql);
        Assert.Same(ProcurementCoreReadProjectionGuard.RequiredReadColumns,
            ProcurementCommercialRoundCoreReadGuard.RequiredReadColumns);
        Assert.Same(ProcurementCoreReadProjectionGuard.AllowedReadColumns,
            ProcurementCommercialRoundCoreReadGuard.AllowedReadColumns);
    }

    [Fact]
    public void Plan_keeps_its_smaller_positive_projection_without_requiring_optional_B1_reads()
    {
        var minimum = ProcurementPlanReviewSchemaReadiness.CoreColumns;
        Assert.Equal(7, minimum.Count);
        foreach (var projection in ProcurementInvoiceReplacementSchemaReadiness.CoreReadColumns)
            Assert.Equal(projection.Value, minimum[projection.Key]);
        Assert.All(minimum, projection => Assert.All(projection.Value,
            column => Assert.True(ProcurementCoreReadProjectionGuard.Allows(projection.Key, column))));
        Assert.DoesNotContain("display_name", minimum["hrm.local_users"]);
        Assert.DoesNotContain("time_zone_id", minimum["hrm.legal_entities"]);
        Assert.False(minimum.ContainsKey("hrm.currency_precision_versions"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_profiles_share_the_effective_private_Core_read_and_sealed_update_boundary(bool write)
    {
        var sql = write ? ProcurementPlanReviewSchemaReadiness.PrivilegeSql
            : ProcurementPlanReviewSchemaReadiness.ReadPrivilegeSql;
        Assert.Contains(ProcurementCoreReadProjectionGuard.Sql, sql);
        Assert.Contains(ProcurementPlanReviewSchemaReadiness.SubmissionImmutableColumnsSql, sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.plan_review_submissions','UPDATE')", sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.plan_review_submissions','SELECT')", sql);
        foreach (var column in ProcurementPlanReviewSchemaReadiness.SubmissionUpdateColumns)
        {
            var requirement = "has_column_privilege(current_user,'procurement.plan_review_submissions','"
                + column + "','UPDATE')";
            if (write) Assert.Contains(requirement, sql);
            else Assert.DoesNotContain(requirement, sql);
        }
    }

    [Theory]
    [InlineData("hrm.local_users", "password_hash")]
    [InlineData("hrm.payroll_items", "gross_pay")]
    [InlineData("hrm.currency_precision_command_receipts", "response_json")]
    [InlineData("hrm.new_private_view", "id")]
    public void Plan_does_not_admit_sensitive_or_unlisted_HRM_reads(string table, string column)
        => Assert.False(ProcurementCoreReadProjectionGuard.Allows(table, column));

    [Fact]
    public void Submission_UPDATE_roster_is_only_terminal_transition_and_shared_concurrency_stamps()
    {
        Assert.Equal(new[] { "state", "completed_at", "updated_at", "updated_by", "version" },
            ProcurementPlanReviewSchemaReadiness.SubmissionUpdateColumns);
        var deny = ProcurementPlanReviewSchemaReadiness.SubmissionImmutableColumnsSql;
        Assert.Contains("a.attrelid='procurement.plan_review_submissions'::regclass", deny);
        Assert.Contains("a.attnum>0 AND NOT a.attisdropped", deny);
        Assert.Contains("has_column_privilege(current_user,a.attrelid,a.attnum,'UPDATE')", deny);
        // A positive sealed-column list would silently admit a future column;
        // instead the exception roster is limited to the five mutable fields.
        Assert.Contains("a.attname NOT IN ('state','completed_at','updated_at','updated_by','version')", deny);
        foreach (var pinned in new[] { "id", "tenant_id", "legal_entity_id", "snapshot_json",
            "snapshot_sha256", "policy_version_id", "maker_subject_id", "submitted_at", "created_by" })
            Assert.DoesNotContain("'" + pinned + "'", deny);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Returned")]
    [InlineData("Rejected")]
    [InlineData("Withdrawn")]
    public void Tracked_terminal_save_writes_exactly_the_five_admitted_columns(string state)
    {
        // InMemory exercises the real DbContext guard/stamps without claiming
        // PostgreSQL privilege acceptance or opening any database connection.
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var row = new PlanReviewSubmission
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ProcurementPlanId = Guid.NewGuid(), BranchRef = Guid.NewGuid(),
            SubmittedPlanVersion = 2, CoreApprovalCaseId = Guid.NewGuid(),
            PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1,
            PolicyRulesSha256 = new string('a', 64), MakerSubjectId = "maker",
            CurrencyCode = "ZMW", EstimatedAmount = 10m,
            SnapshotJson = "{}", SnapshotSha256 = ProcurementEvidenceEncoding.Digest("{}"),
            SubmittedAt = DateTimeOffset.UtcNow,
        };
        db.Add(row);
        db.SaveChanges();
        row.State = state;
        row.CompletedAt = DateTimeOffset.UtcNow;
        db.SaveChanges(acceptAllChangesOnSuccess: false);
        var entry = db.Entry(row);
        var table = StoreObjectIdentifier.Table("plan_review_submissions", "procurement");
        var changed = entry.Properties.Where(x => x.IsModified)
            .Select(x => x.Metadata.GetColumnName(table)!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(ProcurementPlanReviewSchemaReadiness.SubmissionUpdateColumns
            .OrderBy(x => x, StringComparer.Ordinal).ToArray(), changed);
        Assert.Equal(2, row.Version);
        Assert.Equal(scope.SubjectId, row.UpdatedBy);
    }

    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "plan-storage-guard-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId => "reviewer";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Narrowing_submission_updates_preserves_parent_Core_and_insert_requirements()
    {
        var sql = ProcurementPlanReviewSchemaReadiness.PrivilegeSql;
        foreach (var table in new[] { "procurement.procurement_plans", "core_approval.cases",
            "core_approval.stages", "core_approval.assignments" })
            Assert.Contains("has_table_privilege(current_user,'" + table + "','UPDATE')", sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.plan_review_submissions','INSERT')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.plan_review_command_receipts','UPDATE')", sql);
    }
}
