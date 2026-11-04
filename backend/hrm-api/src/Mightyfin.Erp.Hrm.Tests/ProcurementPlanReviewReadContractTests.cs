using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanReviewReadContractTests
{
    [Fact]
    public void Scope_claim_receipt_uses_the_same_camel_case_raw_wire_as_review_commands()
    {
        var plan = Guid.NewGuid();
        var branch = Guid.NewGuid();
        using var json = JsonDocument.Parse(ProcurementPlanRoutes.ClaimScopeResponse(plan, branch, "ZMW", 3));
        Assert.Equal(plan, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(branch, json.RootElement.GetProperty("branchRef").GetGuid());
        Assert.Equal("ZMW", json.RootElement.GetProperty("currencyCode").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("version").GetInt64());
        Assert.True(json.RootElement.GetProperty("scopeClaimed").GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("Id", out _));
    }

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

    [Fact]
    public void Reviewer_task_links_to_the_verified_submission_id_not_its_core_case_id()
    {
        var tenant = "tenant-test";
        var entity = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        var plan = new ProcurementPlan
        {
            Id = Guid.NewGuid(), TenantId = tenant, LegalEntityId = entity, BranchRef = branch,
            OwnerSubjectId = owner, Number = "PLAN-1", FinancialPeriodRef = "2027",
            CurrencyCode = "ZMW", Status = "Submitted", ApprovalInstanceRef = caseId.ToString("D"),
            Lines = [new ProcurementPlanLine { Id = Guid.NewGuid(), LineNumber = 1,
                Description = "Equipment", UnitCode = "EA", EstimatedQuantity = 1,
                EstimatedAmount = 10m }],
        };
        var prepared = PlanReviewSnapshot.Create(plan, 2);
        var round = new PlanReviewSubmission
        {
            Id = Guid.NewGuid(), TenantId = tenant, LegalEntityId = entity,
            ProcurementPlanId = plan.Id, BranchRef = branch, SubmittedPlanVersion = 2,
            CoreApprovalCaseId = caseId, MakerSubjectId = owner, CurrencyCode = "ZMW",
            EstimatedAmount = prepared.EstimatedAmount, SnapshotJson = prepared.Json,
            SnapshotSha256 = prepared.Sha256, PolicyVersionId = policyId,
            PolicyVersionNumber = 1, State = "Pending",
        };
        var link = Link(round);
        Assert.Equal(round.Id, link);
        Assert.NotEqual(caseId, link);
        Assert.Equal(Guid.Empty, Link(WithWrongCase()));

        Guid Link(PlanReviewSubmission candidate) => ProcurementPlanReviewReads.PendingTaskSubmissionId(
            candidate, plan.Id, caseId, 2, tenant, entity, branch, owner, plan.Status,
            plan.ApprovalInstanceRef, policyId, 1);
        PlanReviewSubmission WithWrongCase() => new()
        {
            Id = round.Id, TenantId = round.TenantId, LegalEntityId = round.LegalEntityId,
            ProcurementPlanId = round.ProcurementPlanId, BranchRef = round.BranchRef,
            SubmittedPlanVersion = round.SubmittedPlanVersion, CoreApprovalCaseId = Guid.NewGuid(),
            MakerSubjectId = round.MakerSubjectId, CurrencyCode = round.CurrencyCode,
            EstimatedAmount = round.EstimatedAmount, SnapshotJson = round.SnapshotJson,
            SnapshotSha256 = round.SnapshotSha256, PolicyVersionId = round.PolicyVersionId,
            PolicyVersionNumber = round.PolicyVersionNumber, State = round.State,
        };
    }
}
