using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanReviewReadContractTests
{
    [Fact]
    public void Original_command_key_uses_one_modern_header_and_rejects_conflicting_legacy_header()
    {
        var http = new DefaultHttpContext();
        var original = Guid.NewGuid();
        http.Request.Headers["Idempotency-Key"] = original.ToString("D");
        Assert.True(ProcurementPlanReviewRoutes.TryKey(http, out var parsed));
        Assert.Equal(original, parsed);
        http.Request.Headers["X-Idempotency-Key"] = Guid.NewGuid().ToString("D");
        Assert.False(ProcurementPlanReviewRoutes.TryKey(http, out _));
        http.Request.Headers.Remove("X-Idempotency-Key");
        http.Request.Headers["Idempotency-Key"] = new[] { original.ToString("D"), Guid.NewGuid().ToString("D") };
        Assert.False(ProcurementPlanReviewRoutes.TryKey(http, out _));
    }

    [Fact]
    public void Retained_read_inspection_requires_select_and_readonly_without_write_authority()
    {
        var read = ProcurementPlanReviewSchemaReadiness.ReadPrivilegeSql;
        var write = ProcurementPlanReviewSchemaReadiness.PrivilegeSql;
        Assert.Contains("current_setting('transaction_read_only')='on'", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.plan_review_submissions','SELECT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.plan_review_submissions','INSERT')", read);
        Assert.Contains("has_table_privilege(current_user,'procurement.plan_review_submissions','INSERT')", write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE')", read);
        Assert.Contains("n.nspname='hrm'", read);
    }

    [Fact]
    public void Returned_plan_can_be_cancelled_only_after_its_round_closed_without_pending_history()
    {
        var caseId = Guid.NewGuid();
        var plan = new ProcurementPlan { Status = "Returned", ApprovalInstanceRef = caseId.ToString("D") };
        var returned = new PlanReviewSubmission { State = "Returned", CoreApprovalCaseId = caseId,
            SubmittedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow };
        Assert.True(ProcurementPlanRoutes.ReturnedReviewClosed(plan, [returned]));
        Assert.False(ProcurementPlanRoutes.ReturnedReviewClosed(plan, [returned,
            new PlanReviewSubmission { State = "Pending", SubmittedAt = returned.SubmittedAt.AddMinutes(-1) }]));
        Assert.False(ProcurementPlanRoutes.ReturnedReviewClosed(plan,
            [new PlanReviewSubmission { State = "Returned", CoreApprovalCaseId = Guid.NewGuid(),
                SubmittedAt = returned.SubmittedAt, CompletedAt = returned.CompletedAt }]));
        Assert.False(ProcurementPlanRoutes.ReturnedReviewClosed(plan,
            [new PlanReviewSubmission { State = "Returned", CoreApprovalCaseId = caseId,
                SubmittedAt = returned.SubmittedAt }]));
    }
}
