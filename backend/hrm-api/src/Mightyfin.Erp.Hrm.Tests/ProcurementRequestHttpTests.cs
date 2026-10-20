using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Exercises the mapped HTTP routes with the real Core scope middleware and Procurement query filters.</summary>
public sealed class ProcurementRequestHttpTests
{
    [Theory]
    [InlineData(" Goods ", " office_it_2 ", "GOODS", "OFFICE_IT_2")]
    [InlineData("Service", "IT", "SERVICE", "IT")]
    [InlineData("Asset", "IT", "ASSET", "IT")]
    [InlineData("Subscription", "IT", "SUBSCRIPTION", "IT")]
    [InlineData("  ", "  ", null, null)]
    public async Task Editable_request_codes_are_canonical_on_create_update_and_reload(
        string type, string category, string? expectedType, string? expectedCategory)
    {
        await using var fixture = await Fixture.Start();
        var draft = Draft("Canonical request");
        var line = draft.Lines![0] with { PurchaseType = type, CategoryCode = category };
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, draft with { Lines = [line] });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(expectedType, body.GetProperty("lines")[0].GetProperty("purchaseType").GetString());
        Assert.Equal(expectedCategory, body.GetProperty("lines")[0].GetProperty("categoryCode").GetString());
        var edited = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, new ProcurementRequestRoutes.UpdateRequest(body.GetProperty("version").GetInt64(),
                "Edited canonical request", "ZMW", null, null, null, null, null, null, null,
                [line with { Id = body.GetProperty("lines")[0].GetProperty("id").GetGuid() }]));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var reloaded = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        var stored = await reloaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedType, stored.GetProperty("lines")[0].GetProperty("purchaseType").GetString());
        Assert.Equal(expectedCategory, stored.GetProperty("lines")[0].GetProperty("categoryCode").GetString());
    }

    [Theory]
    [InlineData("purchaseType", "GOODS-SERVICE")]
    [InlineData("categoryCode", "OFFICE IT")]
    [InlineData("purchaseType", "ſervice")]
    [InlineData("categoryCode", "IT/OPS")]
    [InlineData("purchaseType", "TOO_LONG")]
    [InlineData("purchaseType", "TYPE_TOO_LONG")]
    [InlineData("categoryCode", "TOO_LONG")]
    public async Task Invalid_request_codes_reject_create_and_update_without_any_persistence(
        string field, string invalid)
    {
        await using var fixture = await Fixture.Start();
        if (invalid == "TOO_LONG") invalid = new string('A', 101);
        if (invalid == "TYPE_TOO_LONG") invalid = new string('A', 41);
        var draft = Draft("Unchanged request");
        var invalidLine = field == "purchaseType" ? draft.Lines![0] with { PurchaseType = invalid }
            : draft.Lines![0] with { CategoryCode = invalid };
        var before = await fixture.RequestPersistenceCounts();
        var rejected = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, draft with { Lines = [draft.Lines![0], invalidLine] });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Contains("validation-failed", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(before, await fixture.RequestPersistenceCounts());

        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests", fixture.Owner, draft);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        var originalGet = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        var originalJson = await originalGet.Content.ReadAsStringAsync();
        before = await fixture.RequestPersistenceCounts();
        var edit = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}", fixture.Owner,
            new ProcurementRequestRoutes.UpdateRequest(original.GetProperty("version").GetInt64(),
                "Must not save header or cancel old line", "USD", null, null, null, null, null, null, null,
                [invalidLine]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal(before, await fixture.RequestPersistenceCounts());
        var unchanged = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        Assert.Equal(originalJson, await unchanged.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delayed_buying_rule_check_uses_submission_policy_not_current_successor()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests", fixture.Owner,
            Draft("Delayed rule check") with { NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post, $"/api/procurement/v1/requests/{id}/submit",
            fixture.Owner, new { version = draft.GetProperty("version").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var source = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var priorPolicy = await fixture.AddHistoricalBuyingPoliciesForDelayedCheck(id);
        var currentResponse = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        var current = await currentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var evaluated = await SendJson(fixture.Client, HttpMethod.Post, $"/api/procurement/v1/requests/{id}/policy-evaluations",
            fixture.Other, new { requestVersion = current.GetProperty("version").GetInt64() }, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        var body = await evaluated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Matched", body.GetProperty("status").GetString());
        Assert.Equal(priorPolicy, body.GetProperty("evaluation").GetProperty("policyVersionId").GetGuid());
    }

    [Fact]
    public async Task Draft_request_persists_and_is_hidden_from_another_employee()
    {
        await using var fixture = await Fixture.Start();
        var client = fixture.Client;
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Office laptops")),
        };
        create.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = record.GetProperty("id").GetGuid();
        Assert.Equal("ZMW", record.GetProperty("currencyCode").GetString());
        Assert.Equal("Draft", record.GetProperty("status").GetString());
        Assert.Single(record.GetProperty("lines").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("approvalInstanceRef").ValueKind);
        Assert.Equal(0m, record.GetProperty("lines")[0].GetProperty("approvedQuantity").GetDecimal());

        var own = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var other = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        var otherList = await Send(client, HttpMethod.Get, "/api/procurement/v1/requests", fixture.Other);
        Assert.Equal(0, (await otherList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());

        var wrongCompany = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, fixture.OtherCompany);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCompany.StatusCode);
    }

    [Fact]
    public async Task Draft_write_requires_an_active_employee_and_action_permission()
    {
        await using var fixture = await Fixture.Start();
        var noSession = await fixture.Client.PostAsJsonAsync("/api/procurement/v1/requests", Draft("No session"));
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);

        var noPermission = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("No permission")),
        };
        noPermission.Headers.Add("X-Test-User", fixture.Owner.ToString());
        noPermission.Headers.Add("X-Test-Role", "ap_processor");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(noPermission)).StatusCode);

        var inactive = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Inactive employee")),
        };
        inactive.Headers.Add("X-Test-User", fixture.Inactive.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(inactive)).StatusCode);
    }

    [Fact]
    public async Task Approval_readiness_lists_missing_evidence_without_granting_authority()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, Draft("Approval readiness"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var path = $"/api/procurement/v1/requests/{id}/approval-readiness";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner)).StatusCode);
        var review = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
            role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var body = await review.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("prerequisitesMet").GetBoolean());
        Assert.False(body.GetProperty("isAuthority").GetBoolean());
        Assert.False(body.GetProperty("canOpenCase").GetBoolean());
        Assert.Contains(body.GetProperty("approvalRoute").GetProperty("blockers").EnumerateArray(),
            item => item.GetString() == "BuyingRulesNotMatched");
        Assert.Contains(body.GetProperty("blockers").EnumerateArray(),
            item => item.GetString() == "BuyingRulesNotMatched");
        Assert.Contains(body.GetProperty("blockers").EnumerateArray(),
            item => item.GetString() == "FinanceReservationNotConfirmed");
    }

    [Fact]
    public async Task Request_submission_is_versioned_audited_and_remains_pending_funding()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, Draft("Buy office laptops"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var version = draft.GetProperty("version").GetInt64();
        var submitPath = $"/api/procurement/v1/requests/{id}/submit";
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, submitPath, fixture.Other,
                new ProcurementRequestRoutes.VersionCommand(version))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, submitPath, fixture.Owner,
                new ProcurementRequestRoutes.VersionCommand(version))).StatusCode);

        var update = new ProcurementRequestRoutes.UpdateRequest(version, "Buy office laptops", null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null, null, null,
            null, null, Draft("Buy office laptops").Lines);
        var revised = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/requests/{id}", fixture.Owner, update);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        var saved = await revised.Content.ReadFromJsonAsync<JsonElement>();
        version = saved.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, submitPath, fixture.Owner,
                new ProcurementRequestRoutes.VersionCommand(version - 1))).StatusCode);
        var submitted = await SendJson(fixture.Client, HttpMethod.Post, submitPath, fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(version));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", pending.GetProperty("status").GetString());
        Assert.Equal("PendingPolicyAndBudget", pending.GetProperty("approvalState").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "Submitted"));
        var pendingPolicy = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/requests/{id}/policy-status", fixture.Owner);
        Assert.Equal("Pending", (await pendingPolicy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        var noPolicy = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/policy-evaluations", fixture.Other,
            new ProcurementPolicyEvaluationRoutes.EvaluateInput(pending.GetProperty("version").GetInt64()),
            "procurement_manager");
        Assert.Equal("MissingPolicy", (await noPolicy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(0, await fixture.RequestAuditCount(id, "PolicyEvaluated"));
        var policyQueue = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/requests/policy-queue", fixture.Other,
            role: "procurement_manager");
        Assert.Equal(1, (await policyQueue.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get,
                "/api/procurement/v1/requests/policy-queue", fixture.Owner,
                role: "employee")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, submitPath, fixture.Owner,
                new ProcurementRequestRoutes.VersionCommand(pending.GetProperty("version").GetInt64()))).StatusCode);
        var withdrawPath = $"/api/procurement/v1/requests/{id}/withdraw";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, withdrawPath, fixture.Owner,
                new ProcurementRequestRoutes.WithdrawCommand(pending.GetProperty("version").GetInt64()))).StatusCode);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post, withdrawPath, fixture.Owner,
            new ProcurementRequestRoutes.WithdrawCommand(pending.GetProperty("version").GetInt64(),
                "Need no longer exists"));
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("Withdrawn", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "Withdrawn"));
    }

    [Fact]
    public async Task Finance_budget_decision_requires_distinct_reviewer_and_confirmed_reservation()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, Draft("Finance review"));
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var version = draft.GetProperty("version").GetInt64();
        var update = new ProcurementRequestRoutes.UpdateRequest(version, "Finance review", null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null, null, null,
            null, null, Draft("Finance review").Lines);
        var revised = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/requests/{id}", fixture.Owner, update);
        var saved = await revised.Content.ReadFromJsonAsync<JsonElement>();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(saved.GetProperty("version").GetInt64()));
        var request = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        version = request.GetProperty("version").GetInt64();
        var path = $"/api/procurement/v1/requests/{id}/budget-checks";
        var ownStatusPath = $"/api/procurement/v1/requests/{id}/budget-status";
        var pendingStatus = await Send(fixture.Client, HttpMethod.Get, ownStatusPath, fixture.Owner);
        Assert.Equal("Pending", (await pendingStatus.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, ownStatusPath, fixture.Other)).StatusCode);
        var decision = new ProcurementBudgetRoutes.BudgetDecision(version, "FIN-001", "Available",
            200, "ZMW", "finance-budget/001", null, "RES-001", "Confirmed");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, decision)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, decision,
                "finance_approver")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                decision with { ReservationRef = null }, "finance_approver")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                decision with { RequestVersion = version - 1 }, "finance_approver")).StatusCode);
        var recorded = await SendJson(fixture.Client, HttpMethod.Post, path,
            fixture.Other, decision, "finance_approver");
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        var result = await recorded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", result.GetProperty("reservationStatus").GetString());
        Assert.Equal(fixture.Other.ToString(), result.GetProperty("checkedBySubjectId").GetString());
        Assert.Equal(HttpStatusCode.OK,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                decision, "finance_approver")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                decision with { Status = "Insufficient", DecisionReason = "No funds" },
                "finance_approver")).StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "BudgetChecked"));
        var ownBudget = await Send(fixture.Client, HttpMethod.Get, ownStatusPath, fixture.Owner);
        var budgetStatus = await ownBudget.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Available", budgetStatus.GetProperty("status").GetString());
        Assert.True(budgetStatus.GetProperty("reservationConfirmed").GetBoolean());
        Assert.False(budgetStatus.TryGetProperty("evidenceRef", out _));
        var own = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        Assert.Equal("PendingPolicyAndBudget", (await own.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("approvalState").GetString());
        var protectedReturn = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/return-for-changes", fixture.Other,
            new ProcurementRequestRoutes.ReturnInput(version, "Correct the requested amount"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.Conflict, protectedReturn.StatusCode);
        Assert.Equal("budget-reservation-active",
            (await protectedReturn.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code").GetString());
        var protectedWithdrawal = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/withdraw", fixture.Owner,
            new ProcurementRequestRoutes.WithdrawCommand(version, "Need has changed"));
        Assert.Equal(HttpStatusCode.Conflict, protectedWithdrawal.StatusCode);
        Assert.Equal("budget-reservation-active",
            (await protectedWithdrawal.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code").GetString());
    }

    [Fact]
    public async Task Finance_budget_review_is_confined_to_assigned_branch()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, Draft("Branch-restricted funding"));
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var revised = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/requests/{id}", fixture.Owner,
            new ProcurementRequestRoutes.UpdateRequest(draft.GetProperty("version").GetInt64(),
                "Branch-restricted funding", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                null, null, null, null, null, null, Draft("Branch-restricted funding").Lines));
        var saved = await revised.Content.ReadFromJsonAsync<JsonElement>();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(saved.GetProperty("version").GetInt64()));
        var request = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var version = request.GetProperty("version").GetInt64();
        await fixture.MoveRequestToOtherBranchAndConfineReviewer(id);

        var queue = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/requests/budget-queue", fixture.Other, role: "finance_approver");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        Assert.Equal(0, (await queue.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("count").GetInt32());
        var path = $"/api/procurement/v1/requests/{id}/budget-checks";
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
                role: "finance_approver")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new ProcurementBudgetRoutes.BudgetDecision(version, "FIN-BRANCH", "Available",
                    200, "ZMW", "finance-budget/branch", null, "RES-BRANCH", "Confirmed"),
                "finance_approver")).StatusCode);
        Assert.Equal(0, await fixture.RequestAuditCount(id, "BudgetChecked"));

        var companyQueue = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/requests/budget-queue", fixture.Reviewer,
            role: "finance_approver");
        Assert.Equal(1, (await companyQueue.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Vendor_and_item_drafts_respect_their_distinct_record_and_action_scopes()
    {
        await using var fixture = await Fixture.Start();
        var vendor = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/vendors")
        {
            Content = JsonContent.Create(new { legalName = "Acme Zambia Ltd", displayName = "Acme",
                countryCode = "ZM" }),
        };
        vendor.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var savedVendor = await fixture.Client.SendAsync(vendor);
        Assert.Equal(HttpStatusCode.Created, savedVendor.StatusCode);
        var vendorId = (await savedVendor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/vendors/{vendorId}", fixture.Other)).StatusCode);

        var deniedItem = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/items")
        {
            Content = JsonContent.Create(new { code = "LAP-1", name = "Laptop", kind = "Goods",
                categoryCode = "IT", unitCode = "each" }),
        };
        deniedItem.Headers.Add("X-Test-User", fixture.Owner.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(deniedItem)).StatusCode);

        var buyerItem = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/items")
        {
            Content = JsonContent.Create(new { code = "LAP-1", name = "Laptop", kind = "Goods",
                categoryCode = "IT", unitCode = "each" }),
        };
        buyerItem.Headers.Add("X-Test-User", fixture.Owner.ToString());
        buyerItem.Headers.Add("X-Test-Role", "procurement_buyer");
        var savedItem = await fixture.Client.SendAsync(buyerItem);
        Assert.Equal(HttpStatusCode.Created, savedItem.StatusCode);
        var result = await savedItem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inactive", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isPurchasable").GetBoolean());
    }

    [Fact]
    public async Task Request_edit_rejects_stale_versions_and_withdrawal_locks_the_record()
    {
        await using var fixture = await Fixture.Start();
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Two laptops")),
        };
        create.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var created = await fixture.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = first.GetProperty("id").GetGuid();
        var lineId = first.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var version = first.GetProperty("version").GetInt64();
        var edit = new ProcurementRequestRoutes.UpdateRequest(version, "Three laptops", null, null,
            null, null, null, null, null, null,
            [new ProcurementRequestRoutes.RequestLineInput(lineId, "Laptop", "each", 3, 100,
                null, null, null, "Goods", null, null, null, null, null, null)]);

        var saved = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, edit);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var latest = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Three laptops", latest.GetProperty("purpose").GetString());
        Assert.Equal(3, latest.GetProperty("lines")[0].GetProperty("requestedQuantity").GetDecimal());
        Assert.True(latest.GetProperty("version").GetInt64() > version);

        var stale = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, edit);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/withdraw", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(latest.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("Withdrawn", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
                fixture.Owner, edit)).StatusCode);
    }

    [Fact]
    public async Task Planning_and_sourcing_drafts_are_separate_from_buying_authority()
    {
        await using var fixture = await Fixture.Start();
        var planInput = new ProcurementPlanInput("FY2026", null, null,
            [new ProcurementPlanLineInput(null, null, "Office chairs", "OFFICE", "each", 10,
                5_000, null, null)]);
        var deniedPlan = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, planInput);
        Assert.Equal(HttpStatusCode.Forbidden, deniedPlan.StatusCode);
        var savedPlan = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, planInput, "manager");
        Assert.Equal(HttpStatusCode.Created, savedPlan.StatusCode);
        var plan = await savedPlan.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", plan.GetProperty("status").GetString());
        Assert.Equal("ZMW", plan.GetProperty("currencyCode").GetString());
        var planId = plan.GetProperty("id").GetGuid();
        Assert.Equal(1, await fixture.RequestAuditCount(planId, "DraftCreated"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/plans/{planId}",
                fixture.Other, role: "manager")).StatusCode);

        var sourceInput = new SourcingDraftInput("Office chairs", "RFQ", null,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8),
            [new SourcingLineInput(null, null, "Office chairs", "each", 10, "OFFICE", null)]);
        var deniedSource = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/sourcing",
            fixture.Owner, sourceInput, "manager");
        Assert.Equal(HttpStatusCode.Forbidden, deniedSource.StatusCode);
        var savedSource = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/sourcing",
            fixture.Owner, sourceInput, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, savedSource.StatusCode);
        var source = await savedSource.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", source.GetProperty("status").GetString());
        Assert.Equal("ZMW", source.GetProperty("currencyCode").GetString());
        var sourceId = source.GetProperty("id").GetGuid();
        Assert.Equal(1, await fixture.RequestAuditCount(sourceId, "DraftCreated"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/sourcing/{sourceId}",
                fixture.Other, role: "procurement_buyer")).StatusCode);
        var revisedSource = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/sourcing/{sourceId}", fixture.Owner,
            new ProcurementSourcingDraftRoutes.VersionedSourcingDraft(source.GetProperty("version").GetInt64(),
                sourceInput with { Title = "Office chairs and desks", Lines = [sourceInput.Lines![0] with
                    { Id = source.GetProperty("lines")[0].GetProperty("id").GetGuid() }] }),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, revisedSource.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(sourceId, "DraftUpdated"));
        var sourceAudit = await fixture.AuditPayload(sourceId, "DraftUpdated");
        Assert.Equal("Office chairs", sourceAudit.GetProperty("before").GetProperty("Title").GetString());
        Assert.Equal("Office chairs and desks", sourceAudit.GetProperty("after").GetProperty("Title").GetString());
        var revisedSourceBody = await revisedSource.Content.ReadFromJsonAsync<JsonElement>();
        var cancelledSource = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/sourcing/{sourceId}/cancel", fixture.Owner,
            new ProcurementSourcingDraftRoutes.SourcingCancel(
                revisedSourceBody.GetProperty("version").GetInt64(), "Need withdrawn"),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, cancelledSource.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(sourceId, "DraftCancelled"));
    }

    [Fact]
    public async Task Draft_plan_and_sourcing_revisions_reject_duplicate_saved_line_ids()
    {
        await using var fixture = await Fixture.Start();
        var planLine = new ProcurementPlanLineInput(null, null, "Office chairs", "OFFICE", "each",
            10, 5_000, null, null);
        var planResponse = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, new ProcurementPlanInput("FY2026", null, null, [planLine]), "manager");
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
        var plan = await planResponse.Content.ReadFromJsonAsync<JsonElement>();
        var savedPlanLine = planLine with { Id = plan.GetProperty("lines")[0].GetProperty("id").GetGuid() };
        var duplicatePlan = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/plans/{plan.GetProperty("id").GetGuid()}", fixture.Owner,
            new ProcurementPlanRoutes.UpdatePlan(plan.GetProperty("version").GetInt64(),
                new ProcurementPlanInput("FY2026", null, null, [savedPlanLine, savedPlanLine])), "manager");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicatePlan.StatusCode);

        var sourcingLine = new SourcingLineInput(null, null, "Office chairs", "each", 10, "OFFICE", null);
        var sourceResponse = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/sourcing",
            fixture.Owner, new SourcingDraftInput("Office chairs", "RFQ", null,
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8), [sourcingLine]),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, sourceResponse.StatusCode);
        var source = await sourceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var savedSourceLine = sourcingLine with { Id = source.GetProperty("lines")[0].GetProperty("id").GetGuid() };
        var duplicateSource = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/sourcing/{source.GetProperty("id").GetGuid()}", fixture.Owner,
            new ProcurementSourcingDraftRoutes.VersionedSourcingDraft(source.GetProperty("version").GetInt64(),
                new SourcingDraftInput("Office chairs", "RFQ", null,
                    DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8),
                    [savedSourceLine, savedSourceLine])), "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicateSource.StatusCode);
    }

    [Fact]
    public async Task Planning_draft_revisions_keep_removed_lines_and_reject_stale_changes()
    {
        await using var fixture = await Fixture.Start();
        var first = new ProcurementPlanLineInput(null, null, "Office chairs", "OFFICE", "each", 10,
            5_000, null, null);
        var second = new ProcurementPlanLineInput(null, null, "Desks", "OFFICE", "each", 5,
            3_000, null, null);
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, new ProcurementPlanInput("FY2026", null, null, [first, second]), "manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = plan.GetProperty("id").GetGuid();
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftCreated"));
        var originalVersion = plan.GetProperty("version").GetInt64();
        var keepId = plan.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var revisedInput = new ProcurementPlanInput("FY2026", null, "OPS",
            [first with { Id = keepId, EstimatedQuantity = 8 },
                new ProcurementPlanLineInput(null, null, "Monitors", "IT", "each", 4, 2_000, null, null)]);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/plans/{id}",
            fixture.Owner, new ProcurementPlanRoutes.UpdatePlan(originalVersion, revisedInput), "manager");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var revised = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, revised.GetProperty("lines").GetArrayLength());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftUpdated"));
        var planAudit = await fixture.AuditPayload(id, "DraftUpdated");
        Assert.Equal(JsonValueKind.Null, planAudit.GetProperty("before").GetProperty("FundingSourceRef").ValueKind);
        Assert.Equal("OPS", planAudit.GetProperty("after").GetProperty("FundingSourceRef").GetString());
        Assert.Single(revised.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("status").GetString() == "Cancelled");
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/plans/{id}",
                fixture.Owner, new ProcurementPlanRoutes.UpdatePlan(originalVersion, revisedInput),
                "manager")).StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftUpdated"));
        var list = await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/plans",
            fixture.Owner, role: "manager");
        Assert.Equal(2, (await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items")[0].GetProperty("lineCount").GetInt32());

        var cancelled = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/plans/{id}/cancel", fixture.Owner,
            new ProcurementPlanRoutes.CancelPlan(revised.GetProperty("version").GetInt64(),
                "Planning changed"), "manager");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledPlan = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", cancelledPlan.GetProperty("status").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftCancelled"));
        Assert.All(cancelledPlan.GetProperty("lines").EnumerateArray(),
            line => Assert.Equal("Cancelled", line.GetProperty("status").GetString()));
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/plans/{id}",
                fixture.Other, role: "manager")).StatusCode);
    }

    [Fact]
    public async Task Configuration_drafts_require_manager_authority_and_stay_in_selected_company()
    {
        await using var fixture = await Fixture.Start();
        const string categories = "/api/procurement/v1/configuration/categories";
        const string methods = "/api/procurement/v1/configuration/methods";
        var category = new ProcurementConfigurationRoutes.CategoryInput("IT", "Technology", null, null);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, categories, fixture.Owner,
                category, "procurement_buyer")).StatusCode);
        var created = await SendJson(fixture.Client, HttpMethod.Post, categories, fixture.Owner,
            category, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", saved.GetProperty("status").GetString());
        var id = saved.GetProperty("id").GetGuid();
        var version = saved.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, categories, fixture.Owner,
                category, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{categories}/{id}", fixture.Owner,
                new ProcurementConfigurationRoutes.VersionedCategory(version,
                    category with { Code = "OTHER" }), "procurement_manager")).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, $"{categories}/{id}",
            fixture.Owner, new ProcurementConfigurationRoutes.VersionedCategory(version,
                category with { Name = "IT equipment" }), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var latest = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IT equipment", latest.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{categories}/{id}", fixture.Owner,
                new ProcurementConfigurationRoutes.VersionedCategory(version,
                    category), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{categories}/{id}", fixture.Owner,
                fixture.OtherCompany, "procurement_manager")).StatusCode);

        var method = new ProcurementConfigurationRoutes.MethodInput("RFQ", "Request for quotes", null, null);
        var savedMethod = await SendJson(fixture.Client, HttpMethod.Post, methods, fixture.Owner,
            method, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, savedMethod.StatusCode);
        Assert.Equal("Draft", (await savedMethod.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(1, (await (await Send(fixture.Client, HttpMethod.Get, methods,
            fixture.Owner, role: "procurement_buyer")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Buying_method_requires_independent_review_and_active_linked_policy()
    {
        await using var fixture = await Fixture.Start();
        const string methods = "/api/procurement/v1/configuration/methods";
        var created = await SendJson(fixture.Client, HttpMethod.Post, methods, fixture.Owner,
            new ProcurementConfigurationRoutes.MethodInput("DIRECT", "Direct purchase", null, null),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var method = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = method.GetProperty("id").GetGuid();
        var version = method.GetProperty("version").GetInt64();
        var path = $"{methods}/{id}/activate";
        var review = new ProcurementConfigurationRoutes.ActivationInput(version, "Reviewed method and scope");
        using var otherCompanyReview = new HttpRequestMessage(HttpMethod.Post, path)
        { Content = JsonContent.Create(review) };
        otherCompanyReview.Headers.Add("X-Test-User", fixture.Other.ToString());
        otherCompanyReview.Headers.Add("X-Test-Role", "procurement_manager");
        otherCompanyReview.Headers.Add("X-Shell-Entity", fixture.OtherCompany.ToString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.SendAsync(otherCompanyReview)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, review,
                "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                review with { Reason = "" }, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                review with { Version = version + 1 }, "procurement_manager")).StatusCode);
        var activated = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            review, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal("Active", (await activated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "Activated"));

        var draftPolicyId = await fixture.AddDraftPolicy();
        var linked = await SendJson(fixture.Client, HttpMethod.Post, methods, fixture.Owner,
            new ProcurementConfigurationRoutes.MethodInput("RFQ", "Request for quotes", null, draftPolicyId),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        var linkedMethod = await linked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{methods}/{linkedMethod.GetProperty("id").GetGuid()}/activate", fixture.Other,
                new ProcurementConfigurationRoutes.ActivationInput(
                    linkedMethod.GetProperty("version").GetInt64(), "Reviewed"),
                "procurement_manager")).StatusCode);
        await fixture.PublishPolicy(draftPolicyId);
        var linkedActivated = await SendJson(fixture.Client, HttpMethod.Post,
            $"{methods}/{linkedMethod.GetProperty("id").GetGuid()}/activate", fixture.Other,
            new ProcurementConfigurationRoutes.ActivationInput(
                linkedMethod.GetProperty("version").GetInt64(), "Reviewed active policy"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, linkedActivated.StatusCode);
    }

    [Fact]
    public async Task Policy_drafts_are_scoped_versioned_and_cannot_publish_before_review()
    {
        await using var fixture = await Fixture.Start();
        const string policies = "/api/procurement/v1/configuration/policies";
        var from = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(2));
        var draft = new ProcurementPolicyDraftRoutes.PolicyDraft("PURCHASE_REQUEST", from,
            from.AddMonths(1), "{\"schemaVersion\":1}");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, policies, fixture.Owner,
                draft, "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, policies, fixture.Owner,
                draft with { RulesJson = "[1,2]" }, "procurement_manager")).StatusCode);
        var created = await SendJson(fixture.Client, HttpMethod.Post, policies, fixture.Owner,
            draft, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var policy = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = policy.GetProperty("id").GetGuid();
        Assert.Equal("Draft", policy.GetProperty("state").GetString());
        Assert.Equal(1, policy.GetProperty("policyVersionNumber").GetInt32());
        Assert.Equal(TimeSpan.Zero, policy.GetProperty("effectiveFrom").GetDateTimeOffset().Offset);
        Assert.Equal(from.ToUniversalTime(), policy.GetProperty("effectiveFrom").GetDateTimeOffset());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftCreated"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{policies}/{id}", fixture.Owner,
                fixture.OtherCompany, "procurement_manager")).StatusCode);
        Assert.Equal(1, (await (await Send(fixture.Client, HttpMethod.Get, policies,
            fixture.Owner, role: "procurement_manager")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        Assert.Equal(1, (await (await Send(fixture.Client, HttpMethod.Get,
            $"{policies}?key=purchase_request", fixture.Owner,
            role: "procurement_manager")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        var successor = await SendJson(fixture.Client, HttpMethod.Post, policies, fixture.Owner,
            draft with { EffectiveFrom = from.AddMonths(1), EffectiveTo = from.AddMonths(2) },
            "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, successor.StatusCode);
        Assert.Equal(2, (await successor.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("policyVersionNumber").GetInt32());
        var version = policy.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{policies}/{id}", fixture.Owner,
                new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(version,
                    draft with { PolicyKey = "OTHER" }), "procurement_manager")).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, $"{policies}/{id}", fixture.Owner,
            new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(version,
                draft with { RulesJson = "{\"schemaVersion\":1,\"note\":\"for review\"}" }),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftUpdated"));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{policies}/{id}", fixture.Owner,
                new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(version, draft),
                "procurement_manager")).StatusCode);
        var updatedPolicy = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{policies}/{id}/publish", fixture.Other,
                new ProcurementPolicyDraftRoutes.PublicationInput(
                    updatedPolicy.GetProperty("version").GetInt64(), "Not reviewed"),
                "procurement_manager")).StatusCode);
    }

    [Fact]
    public async Task Policy_rule_validation_reports_field_issues_without_publishing()
    {
        await using var fixture = await Fixture.Start();
        const string path = "/api/procurement/v1/configuration/policies/validate";
        var rules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW",
            amountBasis = "EstimatedLineBase",
            rules = new[] { new
            {
                code = "DEFAULT", priority = 0,
                when = new { minimumAmountInclusive = (decimal?)null,
                    maximumAmountExclusive = (decimal?)null, categoryCodes = Array.Empty<string>(),
                    purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<string>() },
                result = new { budgetReservation = "Required", sourcingMethodCode = (string?)null,
                    minimumQuoteCount = (int?)null, approvalStages = new[]
                    { new { code = "NEED", assignee = "LineManager", mode = "One" } } },
            } },
        });
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                new ProcurementPolicyDraftRoutes.PolicyValidationRequest(rules),
                "procurement_buyer")).StatusCode);
        var valid = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new ProcurementPolicyDraftRoutes.PolicyValidationRequest(rules), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.True((await valid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isValid").GetBoolean());
        var invalid = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new ProcurementPolicyDraftRoutes.PolicyValidationRequest(rules.Replace("EstimatedLineBase", "Net", StringComparison.Ordinal)),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var result = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("isValid").GetBoolean());
        Assert.Contains(result.GetProperty("issues").EnumerateArray(), x =>
            x.GetProperty("path").GetString() == "$.amountBasis");
        Assert.Equal(0, (await (await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/configuration/policies", fixture.Owner,
            role: "procurement_manager")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Submitted_request_pins_a_rule_decision_without_approving_spend()
    {
        await using var fixture = await Fixture.Start();
        var policyId = await fixture.AddPublishedBuyingPolicy();
        const string requests = "/api/procurement/v1/requests";
        var created = await SendJson(fixture.Client, HttpMethod.Post, requests,
            fixture.Owner, Draft("Buying rule check"));
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var update = new ProcurementRequestRoutes.UpdateRequest(
            draft.GetProperty("version").GetInt64(), "Buying rule check", null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null, null, null,
            null, null, Draft("Buying rule check").Lines);
        var saved = await SendJson(fixture.Client, HttpMethod.Put, $"{requests}/{id}",
            fixture.Owner, update);
        var version = (await saved.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"{requests}/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(version));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var request = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingPolicyAndBudget", request.GetProperty("approvalState").GetString());
        var requestVersion = request.GetProperty("version").GetInt64();
        var ownStatus = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/{id}/policy-status", fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, ownStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get,
                $"{requests}/{id}/policy-status", fixture.Other,
                role: "procurement_manager")).StatusCode);
        var status = await ownStatus.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Matched", status.GetProperty("status").GetString());
        Assert.False(status.GetProperty("isAuthority").GetBoolean());
        var evaluation = status.GetProperty("evaluation");
        Assert.Equal(policyId, evaluation.GetProperty("policyVersionId").GetGuid());
        Assert.Equal(requestVersion, evaluation.GetProperty("requestVersion").GetInt64());
        Assert.Equal("DEFAULT", evaluation.GetProperty("decision").GetProperty("lines")[0]
            .GetProperty("decision").GetProperty("ruleCode").GetString());
        Assert.Equal(200m, evaluation.GetProperty("decision").GetProperty("lines")[0]
            .GetProperty("lineBaseAmount").GetDecimal());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "PolicyEvaluated"));
        var readinessPath = $"{requests}/{id}/approval-readiness";
        var routeResponse = await Send(fixture.Client, HttpMethod.Get, readinessPath,
            fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, routeResponse.StatusCode);
        var routeBody = await routeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(routeBody.GetProperty("approvalRoute").GetProperty("hasSingleRoute").GetBoolean());
        Assert.False(routeBody.GetProperty("assignmentPlan").GetProperty("resolved").GetBoolean());
        Assert.NotEmpty(routeBody.GetProperty("assignmentPlan").GetProperty("blockers").EnumerateArray());
        Assert.False(routeBody.GetProperty("prerequisitesMet").GetBoolean());
        Assert.False(routeBody.GetProperty("canOpenCase").GetBoolean());
        await fixture.AssignOwnerManagerToOther();
        var assignedResponse = await Send(fixture.Client, HttpMethod.Get, readinessPath,
            fixture.Other, role: "procurement_manager");
        var assignedBody = await assignedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(assignedBody.GetProperty("assignmentPlan").GetProperty("resolved").GetBoolean());
        Assert.Equal(fixture.Other.ToString(), assignedBody.GetProperty("assignmentPlan")
            .GetProperty("stages")[0].GetProperty("candidates")[0]
            .GetProperty("subjectId").GetString());
        Assert.False(assignedBody.GetProperty("canOpenCase").GetBoolean());
        await fixture.AddRequestLineAfterPolicyCheck(id);
        var staleResponse = await Send(fixture.Client, HttpMethod.Get, readinessPath,
            fixture.Other, role: "procurement_manager");
        var staleBody = await staleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(staleBody.GetProperty("blockers").EnumerateArray(),
            item => item.GetString() == "PinnedDecisionLinesChanged");
        Assert.False(staleBody.GetProperty("prerequisitesMet").GetBoolean());
        Assert.False(staleBody.GetProperty("canOpenCase").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{requests}/{id}/policy-evaluations", fixture.Owner,
                new ProcurementPolicyEvaluationRoutes.EvaluateInput(requestVersion),
                "employee")).StatusCode);
        var repeat = await SendJson(fixture.Client, HttpMethod.Post,
            $"{requests}/{id}/policy-evaluations", fixture.Other,
            new ProcurementPolicyEvaluationRoutes.EvaluateInput(requestVersion),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(evaluation.GetProperty("id").GetGuid(),
            (await repeat.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("evaluation").GetProperty("id").GetGuid());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "PolicyEvaluated"));
        var queue = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/policy-queue", fixture.Other, role: "procurement_manager");
        Assert.Equal(0, (await queue.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Blocked_rule_decision_stays_visible_for_procurement_follow_up()
    {
        await using var fixture = await Fixture.Start();
        await fixture.AddPublishedBuyingPolicy();
        const string requests = "/api/procurement/v1/requests";
        var input = Draft("Unlisted category") with
        {
            Lines = [Draft("Unlisted category").Lines![0] with { CategoryCode = "UNKNOWN" }],
        };
        var created = await SendJson(fixture.Client, HttpMethod.Post, requests,
            fixture.Owner, input);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var revised = await SendJson(fixture.Client, HttpMethod.Put,
            $"{requests}/{id}", fixture.Owner,
            new ProcurementRequestRoutes.UpdateRequest(
                draft.GetProperty("version").GetInt64(), input.Purpose, null,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null,
                null, null, null, null, input.Lines));
        var version = (await revised.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"{requests}/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(version));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var status = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/{id}/policy-status", fixture.Owner);
        var own = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Blocked", own.GetProperty("status").GetString());
        Assert.Equal("InactiveReference", own.GetProperty("evaluation")
            .GetProperty("resultCode").GetString());
        Assert.False(own.GetProperty("isAuthority").GetBoolean());
        var queue = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/policy-queue", fixture.Other,
            role: "procurement_manager");
        var item = (await queue.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("records")[0];
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("Blocked", item.GetProperty("policyOutcome").GetString());
        Assert.Equal("InactiveReference", item.GetProperty("policyResultCode").GetString());
    }

    [Fact]
    public async Task Buyer_can_return_a_blocked_request_for_correction_and_new_rule_evidence()
    {
        await using var fixture = await Fixture.Start();
        await fixture.AddPublishedBuyingPolicy();
        const string requests = "/api/procurement/v1/requests";
        var input = Draft("Correct category") with
        {
            NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Lines = [Draft("Correct category").Lines![0] with { CategoryCode = "UNKNOWN" }],
        };
        var created = await SendJson(fixture.Client, HttpMethod.Post, requests, fixture.Owner, input);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"{requests}/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(draft.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var first = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var oldVersion = first.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{requests}/{id}/review",
                fixture.Owner, role: "employee")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{requests}/{id}/review",
                fixture.Other, fixture.OtherCompany, "procurement_manager")).StatusCode);
        var review = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/{id}/review", fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal("UNKNOWN", (await review.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines")[0].GetProperty("categoryCode").GetString());
        var path = $"{requests}/{id}/return-for-changes";
        var command = new ProcurementRequestRoutes.ReturnInput(oldVersion, "Choose an active buying category.");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PostAsJsonAsync(path, command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                command, "employee")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                command, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                command with { Reason = " " }, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                command with { Version = oldVersion - 1 }, "procurement_manager")).StatusCode);
        var returned = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            command, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var correction = await returned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReturnedForChanges", correction.GetProperty("status").GetString());
        Assert.Equal(command.Reason, correction.GetProperty("returnReason").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "ReturnedForChanges"));
        var returnEvidence = await fixture.AuditPayload(id, "ReturnedForChanges");
        Assert.Equal("UNKNOWN", returnEvidence.GetProperty("submittedSnapshot")
            .GetProperty("lines")[0].GetProperty("CategoryCode").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                command, "procurement_manager")).StatusCode);
        var priorStatus = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/{id}/policy-status", fixture.Owner);
        Assert.Equal("NotSubmitted", (await priorStatus.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());

        var fixedLine = input.Lines![0] with
        {
            Id = correction.GetProperty("lines")[0].GetProperty("id").GetGuid(),
            CategoryCode = null,
        };
        var updated = await SendJson(fixture.Client, HttpMethod.Put,
            $"{requests}/{id}", fixture.Owner,
            new ProcurementRequestRoutes.UpdateRequest(
                correction.GetProperty("version").GetInt64(), input.Purpose, null,
                input.NeededBy, null, null, null, null, null, null, [fixedLine]));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var revised = await updated.Content.ReadFromJsonAsync<JsonElement>();
        var resubmitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"{requests}/{id}/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(revised.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        var current = await resubmitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", current.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("returnReason").ValueKind);
        Assert.True(current.GetProperty("version").GetInt64() > oldVersion);
        var policy = await Send(fixture.Client, HttpMethod.Get,
            $"{requests}/{id}/policy-status", fixture.Owner);
        Assert.Equal("Matched", (await policy.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(2, await fixture.RequestAuditCount(id, "PolicyEvaluated"));
        var originalEvidence = await fixture.AuditPayload(id, "ReturnedForChanges");
        Assert.Equal("UNKNOWN", originalEvidence.GetProperty("submittedSnapshot")
            .GetProperty("lines")[0].GetProperty("CategoryCode").GetString());
    }

    [Fact]
    public async Task Policy_review_submission_requires_author_current_version_and_company_rules()
    {
        await using var fixture = await Fixture.Start();
        const string basePath = "/api/procurement/v1/configuration/policies";
        var rules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW",
            amountBasis = "EstimatedLineBase",
            rules = new[] { new { code = "DEFAULT", priority = 0,
                when = new { minimumAmountInclusive = (decimal?)null,
                    maximumAmountExclusive = (decimal?)null, categoryCodes = Array.Empty<string>(),
                    purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<string>() },
                result = new { budgetReservation = "Required", sourcingMethodCode = (string?)null,
                    minimumQuoteCount = (int?)null,
                    approvalStages = new[] { new { code = "NEED", assignee = "LineManager", mode = "One" } } } } },
        });
        var draft = new ProcurementPolicyDraftRoutes.PolicyDraft("PURCHASE_REQUEST",
            DateTimeOffset.UtcNow.AddDays(1), null, rules);
        var created = await SendJson(fixture.Client, HttpMethod.Post, basePath, fixture.Owner,
            draft, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = saved.GetProperty("id").GetGuid();
        var version = saved.GetProperty("version").GetInt64();
        var preview = await SendJson(fixture.Client, HttpMethod.Post,
            $"{basePath}/{id}/preview", fixture.Owner,
            new PolicyScenario("ZMW", 100m, null, null, null), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var explanation = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(explanation.GetProperty("isAuthority").GetBoolean());
        Assert.Equal("DEFAULT", explanation.GetProperty("decision").GetProperty("ruleCode").GetString());
        Assert.Empty(explanation.GetProperty("coverageIssues").EnumerateArray());
        var route = $"{basePath}/{id}/submit-review";
        var review = new ProcurementPolicyDraftRoutes.ReviewSubmission(version, "Checked the company rules");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, route, fixture.Other,
                review, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, route, fixture.Owner,
                review with { Reason = " " }, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, route, fixture.Owner,
                review with { Version = version + 1 }, "procurement_manager")).StatusCode);
        var sent = await SendJson(fixture.Client, HttpMethod.Post, route, fixture.Owner,
            review, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var reviewed = await sent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingReview", reviewed.GetProperty("state").GetString());
        Assert.Equal(fixture.Owner.ToString(), reviewed.GetProperty("reviewRequestedBy").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "ReviewRequested"));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{basePath}/{id}", fixture.Owner,
                new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(
                    reviewed.GetProperty("version").GetInt64(), draft), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, route, fixture.Owner,
                review with { Version = reviewed.GetProperty("version").GetInt64() },
                "procurement_manager")).StatusCode);

        var publishPath = $"{basePath}/{id}/publish";
        var publication = new ProcurementPolicyDraftRoutes.PublicationInput(
            reviewed.GetProperty("version").GetInt64(), "Approved after independent review");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Owner,
                publication, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Other,
                publication with { Reason = " " }, "procurement_manager")).StatusCode);
        var published = await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Other,
            publication, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedRecord = await published.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", publishedRecord.GetProperty("state").GetString());
        Assert.Equal(fixture.Other.ToString(), publishedRecord.GetProperty("publishedBy").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "Published"));
        var before = await Send(fixture.Client, HttpMethod.Get,
            $"{basePath}/effective?key=PURCHASE_REQUEST", fixture.Owner,
            role: "procurement_manager");
        Assert.Equal("MissingPolicy", (await before.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        var effectiveAt = Uri.EscapeDataString(draft.EffectiveFrom.AddMinutes(1).ToString("O"));
        var after = await Send(fixture.Client, HttpMethod.Get,
            $"{basePath}/effective?key=PURCHASE_REQUEST&at={effectiveAt}", fixture.Owner,
            role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var effective = await after.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Valid", effective.GetProperty("status").GetString());
        Assert.False(effective.GetProperty("isAuthority").GetBoolean());
        Assert.Equal(id, effective.GetProperty("policy").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Other,
                publication with { Version = publishedRecord.GetProperty("version").GetInt64() },
                "procurement_manager")).StatusCode);

        var overlappingDraft = await SendJson(fixture.Client, HttpMethod.Post, basePath,
            fixture.Owner, draft with { EffectiveFrom = draft.EffectiveFrom },
            "procurement_manager");
        var overlapping = await overlappingDraft.Content.ReadFromJsonAsync<JsonElement>();
        var overlappingId = overlapping.GetProperty("id").GetGuid();
        var overlappingReview = await SendJson(fixture.Client, HttpMethod.Post,
            $"{basePath}/{overlappingId}/submit-review", fixture.Owner,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(
                overlapping.GetProperty("version").GetInt64(), "Review replacement"),
            "procurement_manager");
        var ready = await overlappingReview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{basePath}/{overlappingId}/publish", fixture.Other,
                new ProcurementPolicyDraftRoutes.PublicationInput(
                    ready.GetProperty("version").GetInt64(), "Checked replacement"),
                "procurement_manager")).StatusCode);

        var successorStart = draft.EffectiveFrom.AddHours(1);
        var successorDraft = await SendJson(fixture.Client, HttpMethod.Post, basePath,
            fixture.Owner, draft with { EffectiveFrom = successorStart }, "procurement_manager");
        var successor = await successorDraft.Content.ReadFromJsonAsync<JsonElement>();
        var successorId = successor.GetProperty("id").GetGuid();
        var successorReview = await SendJson(fixture.Client, HttpMethod.Post,
            $"{basePath}/{successorId}/submit-review", fixture.Owner,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(
                successor.GetProperty("version").GetInt64(), "Review the new period"),
            "procurement_manager");
        var successorReady = await successorReview.Content.ReadFromJsonAsync<JsonElement>();
        var successorPublish = await SendJson(fixture.Client, HttpMethod.Post,
            $"{basePath}/{successorId}/publish", fixture.Other,
            new ProcurementPolicyDraftRoutes.PublicationInput(
                successorReady.GetProperty("version").GetInt64(), "Replace open-ended rules"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, successorPublish.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "Superseded"));
        var priorAt = Uri.EscapeDataString(successorStart.AddMinutes(-1).ToString("O"));
        var prior = await Send(fixture.Client, HttpMethod.Get,
            $"{basePath}/effective?key=PURCHASE_REQUEST&at={priorAt}", fixture.Owner,
            role: "procurement_manager");
        Assert.Equal(id, (await prior.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("policy").GetProperty("id").GetGuid());
        var successorAt = Uri.EscapeDataString(successorStart.AddMinutes(1).ToString("O"));
        var later = await Send(fixture.Client, HttpMethod.Get,
            $"{basePath}/effective?key=PURCHASE_REQUEST&at={successorAt}", fixture.Owner,
            role: "procurement_manager");
        Assert.Equal(successorId, (await later.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("policy").GetProperty("id").GetGuid());

        var uncoveredRules = rules.Replace("\"minimumAmountInclusive\":null",
            "\"minimumAmountInclusive\":1000", StringComparison.Ordinal);
        var uncoveredDraft = await SendJson(fixture.Client, HttpMethod.Post, basePath,
            fixture.Owner, draft with { RulesJson = uncoveredRules,
                EffectiveFrom = draft.EffectiveFrom.AddDays(2) }, "procurement_manager");
        var uncovered = await uncoveredDraft.Content.ReadFromJsonAsync<JsonElement>();
        var uncoveredId = uncovered.GetProperty("id").GetGuid();
        var uncoveredReview = await SendJson(fixture.Client, HttpMethod.Post,
            $"{basePath}/{uncoveredId}/submit-review", fixture.Owner,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(
                uncovered.GetProperty("version").GetInt64(), "Check the coverage"),
            "procurement_manager");
        var awaitingCoverage = await uncoveredReview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{basePath}/{uncoveredId}/publish", fixture.Other,
                new ProcurementPolicyDraftRoutes.PublicationInput(
                    awaitingCoverage.GetProperty("version").GetInt64(), "Coverage is incomplete"),
                "procurement_manager")).StatusCode);
        var returnPath = $"{basePath}/{uncoveredId}/return-for-changes";
        Assert.Contains(fixture.Routes(), route => route.EndsWith("/{id:guid}/return-for-changes", StringComparison.Ordinal));
        var beforeReturn = await Send(fixture.Client, HttpMethod.Get, $"{basePath}/{uncoveredId}",
            fixture.Owner, role: "procurement_manager");
        Assert.True(beforeReturn.StatusCode == HttpStatusCode.OK,
            $"Expected policy before return, got {beforeReturn.StatusCode}: {await beforeReturn.Content.ReadAsStringAsync()}");
        var returnInput = new ProcurementPolicyDraftRoutes.PublicationInput(
            awaitingCoverage.GetProperty("version").GetInt64(), "Add an unrestricted fallback");
        var ownerReturn = await SendJson(fixture.Client, HttpMethod.Post, returnPath, fixture.Owner,
            returnInput, "procurement_manager");
        Assert.True(ownerReturn.StatusCode == HttpStatusCode.Forbidden,
            $"Expected Forbidden at {returnPath}, got {ownerReturn.StatusCode}: {await ownerReturn.Content.ReadAsStringAsync()}");
        var returned = await SendJson(fixture.Client, HttpMethod.Post, returnPath, fixture.Other,
            returnInput, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var returnedRecord = await returned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", returnedRecord.GetProperty("state").GetString());
        Assert.Equal("Add an unrestricted fallback",
            returnedRecord.GetProperty("lastReturnReason").GetString());
        var reloaded = await Send(fixture.Client, HttpMethod.Get,
            $"{basePath}/{uncoveredId}", fixture.Owner, role: "procurement_manager");
        Assert.Equal("Add an unrestricted fallback",
            (await reloaded.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("lastReturnReason").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(uncoveredId, "ReturnedForChanges"));
        Assert.Equal(HttpStatusCode.OK,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{basePath}/{uncoveredId}",
                fixture.Owner, new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(
                    returnedRecord.GetProperty("version").GetInt64(),
                    draft with { EffectiveFrom = draft.EffectiveFrom.AddDays(2) }),
                "procurement_manager")).StatusCode);

        var wrongCurrency = await SendJson(fixture.Client, HttpMethod.Post, basePath, fixture.Owner,
            draft with { RulesJson = rules.Replace("ZMW", "USD", StringComparison.Ordinal) },
            "procurement_manager");
        var wrong = await wrongCurrency.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{basePath}/{wrong.GetProperty("id").GetGuid()}/submit-review", fixture.Owner,
                new ProcurementPolicyDraftRoutes.ReviewSubmission(
                    wrong.GetProperty("version").GetInt64(), "Review requested"),
                "procurement_manager")).StatusCode);
    }

    [Fact]
    public async Task Null_nested_item_update_returns_scoped_validation_without_changing_record_or_version()
    {
        await using var fixture=await Fixture.Start();
        using var created=await SendJson(fixture.Client,HttpMethod.Post,"/api/procurement/v1/items",fixture.Owner,
            new { code="NULL-UPDATE",name="Retained item",kind="Goods",categoryCode="IT",unitCode="each" },"procurement_buyer");
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var item=await created.Content.ReadFromJsonAsync<JsonElement>();
        var path="/api/procurement/v1/items/"+item.GetProperty("id").GetGuid();
        var payload=new { version=item.GetProperty("version").GetInt64(),item=(object?)null };
        var before=await fixture.OriginalValueFootprint();
        using var denied=await SendJson(fixture.Client,HttpMethod.Put,path,fixture.Owner,payload,"procurement_buyer",company:fixture.OtherCompany);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var response=await SendJson(fixture.Client,HttpMethod.Put,path,fixture.Owner,payload,"procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation-failed",error.GetProperty("code").GetString());
        Assert.Contains("item details",error.GetProperty("message").GetString());
        Assert.Equal(before,await fixture.OriginalValueFootprint());
        using var restored=await Send(fixture.Client,HttpMethod.Get,path,fixture.Owner,role:"procurement_buyer");
        var saved=await restored.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("Retained item",saved.GetProperty("name").GetString());
        Assert.Equal(item.GetProperty("version").GetInt64(),saved.GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("item-moq","0.00001")]
    [InlineData("item-conversion","1.0000001")]
    [InlineData("item-conversion","1000000000000")]
    [InlineData("entry-price","0.00001")]
    [InlineData("entry-price","1.00000000000000000000000000001")]
    [InlineData("entry-min","0.00001")]
    [InlineData("entry-max","100000000000000")]
    public async Task Catalog_original_values_reject_precision_or_range_without_any_persisted_effect(string field,string token)
    {
        await using var fixture=await Fixture.Start();
        var created=await SendJson(fixture.Client,HttpMethod.Post,"/api/procurement/v1/items",fixture.Owner,
            new { code="EXACT-PARENT",name="Existing draft",kind="Goods",categoryCode="IT",unitCode="each" },"procurement_buyer");
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var item=await created.Content.ReadFromJsonAsync<JsonElement>();
        var path="/api/procurement/v1/items";string body;
        if(field.StartsWith("item-")) body="{\"code\":\"RAW-REJECT\",\"name\":\"Original value\",\"kind\":\"Goods\",\"categoryCode\":\"IT\",\"unitCode\":\"each\",\""
            +(field=="item-moq"?"minimumOrderQuantity":"purchaseUnitConversion")+"\":"+token+"}";
        else
        {
            path+="/"+item.GetProperty("id").GetGuid()+"/catalog-entries";
            var values=new Dictionary<string,string> { ["unitPrice"]="1",["minimumQuantity"]="null",["maximumQuantity"]="null" };
            values[field=="entry-price"?"unitPrice":field=="entry-min"?"minimumQuantity":"maximumQuantity"]=token;
            body="{\"currencyCode\":\"ZMW\",\"unitCode\":\"each\",\"effectiveFrom\":\"2026-10-20T00:00:00Z\",\"unitPrice\":"+values["unitPrice"]+",\"minimumQuantity\":"+values["minimumQuantity"]+",\"maximumQuantity\":"+values["maximumQuantity"]+"}";
        }
        var before=await fixture.OriginalValueFootprint();using var response=await RawOriginalValue(fixture,path,body,"procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation-failed",error.GetProperty("code").GetString());
        Assert.Equal(before,await fixture.OriginalValueFootprint());
    }
    [Theory]
    [InlineData("request")][InlineData("request-update")][InlineData("plan")][InlineData("plan-update")]
    [InlineData("contract")][InlineData("contract-update")][InlineData("budget")]
    public async Task Shared_original_amount_boundaries_do_not_round_raw_tokens_before_validation(string feature)
    {
        await using var fixture=await Fixture.Start();const string invalid="1.00000000000000000000000000001";
        var suffix=Guid.NewGuid().ToString("D");
        var (path,body,method)=feature switch
        {
            "request"=>("/requests","{\"lines\":[{\"requestedQuantity\":"+invalid+"}]}",HttpMethod.Post),
            "request-update"=>("/requests/"+suffix,"{\"version\":1,\"lines\":[{\"estimatedUnitPrice\":"+invalid+"}]}",HttpMethod.Put),
            "plan"=>("/plans","{\"lines\":[{\"estimatedAmount\":"+invalid+"}]}",HttpMethod.Post),
            "plan-update"=>("/plans/"+suffix,"{\"version\":1,\"draft\":{\"lines\":[{\"estimatedQuantity\":"+invalid+"}]}}",HttpMethod.Put),
            "contract"=>("/contracts","{\"ceilingAmount\":"+invalid+"}",HttpMethod.Post),
            "contract-update"=>("/contracts/"+suffix,"{\"version\":1,\"draft\":{\"ceilingAmount\":"+invalid+"}}",HttpMethod.Put),
            _=>("/requests/"+suffix+"/budget-checks","{\"checkedAmount\":"+invalid+"}",HttpMethod.Post)
        };
        var role=feature=="budget"?"finance_approver":feature.StartsWith("request")?"employee":feature.StartsWith("plan")?"manager":"procurement_buyer";
        var before=await fixture.OriginalValueFootprint();using var response=await RawOriginalValue(fixture,"/api/procurement/v1"+path,body,role,method);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation-failed",error.GetProperty("code").GetString());
        Assert.Contains("exact amounts",error.GetProperty("message").GetString());
        Assert.Equal(before,await fixture.OriginalValueFootprint());
    }
    [Fact]
    public async Task Catalog_original_exact_bounds_round_trip_and_invalid_update_retains_version_and_price()
    {
        await using var fixture=await Fixture.Start();
        var body="{\"code\":\"EXACT-VALID\",\"name\":\"Exact units\",\"kind\":\"Goods\",\"categoryCode\":\"IT\",\"unitCode\":\"each\",\"purchaseUnitConversion\":\"1.000001\",\"minimumOrderQuantity\":0.0001}";
        using var created=await RawOriginalValue(fixture,"/api/procurement/v1/items",body,"procurement_buyer");Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var item=await created.Content.ReadFromJsonAsync<JsonElement>();var path="/api/procurement/v1/items/"+item.GetProperty("id").GetGuid();
        using var restored=await Send(fixture.Client,HttpMethod.Get,path,fixture.Owner,role:"procurement_buyer");var detail=await restored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1.000001m,detail.GetProperty("purchaseUnitConversion").GetDecimal());Assert.Equal(.0001m,detail.GetProperty("minimumOrderQuantity").GetDecimal());
        const string price="{\"currencyCode\":\"ZMW\",\"unitCode\":\"each\",\"effectiveFrom\":\"2026-10-20T00:00:00Z\",\"unitPrice\":\"99999999999999.9999\",\"minimumQuantity\":\"0.0001\",\"maximumQuantity\":null}";
        using var added=await RawOriginalValue(fixture,path+"/catalog-entries",price,"procurement_buyer");Assert.Equal(HttpStatusCode.Created,added.StatusCode);
        var entry=await added.Content.ReadFromJsonAsync<JsonElement>();var before=await fixture.OriginalValueFootprint();
        var invalid="{\"version\":"+entry.GetProperty("version").GetInt64()+",\"entry\":"+price.Replace("99999999999999.9999","0.00001")+"}";
        using var rejected=await RawOriginalValue(fixture,path+"/catalog-entries/"+entry.GetProperty("id").GetGuid(),invalid,"procurement_buyer",HttpMethod.Put);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,rejected.StatusCode);Assert.Equal(before,await fixture.OriginalValueFootprint());
        using var entries=await Send(fixture.Client,HttpMethod.Get,path+"/catalog-entries",fixture.Owner,role:"procurement_buyer");
        var stored=(await entries.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0];
        Assert.Equal(99999999999999.9999m,stored.GetProperty("unitPrice").GetDecimal());Assert.Equal(.0001m,stored.GetProperty("minimumQuantity").GetDecimal());
    }
    private static Task<HttpResponseMessage> RawOriginalValue(Fixture fixture,string path,string body,string role,HttpMethod? method=null)
    {
        var request=new HttpRequestMessage(method??HttpMethod.Post,path) { Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json") };
        request.Headers.Add("X-Test-User",fixture.Owner.ToString());request.Headers.Add("X-Test-Role",role);
        return fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task Independent_review_activates_category_then_item_without_publishing_a_price()
    {
        await using var fixture = await Fixture.Start();
        const string categories = "/api/procurement/v1/configuration/categories";
        var createdCategory = await SendJson(fixture.Client, HttpMethod.Post, categories, fixture.Owner,
            new ProcurementConfigurationRoutes.CategoryInput("OFFICE", "Office equipment", null, null),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, createdCategory.StatusCode);
        var category = await createdCategory.Content.ReadFromJsonAsync<JsonElement>();
        var categoryPath = $"{categories}/{category.GetProperty("id").GetGuid()}/activate";
        var categoryReview = new ProcurementConfigurationRoutes.ActivationInput(
            category.GetProperty("version").GetInt64(), "Checked category coding");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, categoryPath, fixture.Owner,
                categoryReview, "procurement_manager")).StatusCode);

        var createdItem = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/items", fixture.Owner,
            new { code = "CHAIR-1", name = "Office chair", kind = "Goods",
                categoryCode = "OFFICE", unitCode = "each" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdItem.StatusCode);
        var item = await createdItem.Content.ReadFromJsonAsync<JsonElement>();
        var itemPath = $"/api/procurement/v1/items/{item.GetProperty("id").GetGuid()}/activate";
        var itemReview = new ItemActivationInput(item.GetProperty("version").GetInt64(),
            "Checked item details and category");
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, itemPath, fixture.Other,
                itemReview, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, itemPath, fixture.Owner,
                itemReview, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, itemPath, fixture.Other,
                itemReview, "procurement_buyer")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await SendJson(fixture.Client, HttpMethod.Post, categoryPath, fixture.Other,
                categoryReview, "procurement_manager")).StatusCode);
        var activated = await SendJson(fixture.Client, HttpMethod.Post, itemPath, fixture.Other,
            itemReview, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var active = await activated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", active.GetProperty("status").GetString());
        Assert.True(active.GetProperty("isPurchasable").GetBoolean());
        Assert.Equal(1, await fixture.RequestAuditCount(item.GetProperty("id").GetGuid(), "Activated"));
        Assert.Equal(1, await fixture.RequestAuditCount(category.GetProperty("id").GetGuid(), "Activated"));
        var catalog = await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/catalog",
            fixture.Owner, role: "employee");
        Assert.Equal(0, (await catalog.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        var deactivated = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/items/{item.GetProperty("id").GetGuid()}/deactivate",
            fixture.Other, new VersionInput(active.GetProperty("version").GetInt64()),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(item.GetProperty("id").GetGuid(), "Deactivated"));
    }

    [Fact]
    public async Task Catalog_price_publication_requires_active_item_vendor_and_independent_review()
    {
        await using var fixture = await Fixture.Start();
        var categoryResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/configuration/categories", fixture.Owner,
            new ProcurementConfigurationRoutes.CategoryInput("OFFICE", "Office", null, null),
            "procurement_manager");
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonElement>();
        await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/configuration/categories/{category.GetProperty("id").GetGuid()}/activate",
            fixture.Other, new ProcurementConfigurationRoutes.ActivationInput(
                category.GetProperty("version").GetInt64(), "Reviewed"), "procurement_manager");
        var itemResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/items", fixture.Owner,
            new { code = "CHAIR-1", name = "Chair", kind = "Goods", categoryCode = "OFFICE", unitCode = "each" },
            "procurement_buyer");
        var item = await itemResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = item.GetProperty("id").GetGuid();
        await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/items/{itemId}/activate", fixture.Other,
            new ItemActivationInput(item.GetProperty("version").GetInt64(), "Reviewed"),
            "procurement_manager");
        var vendorResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/vendors", fixture.Owner,
            new { legalName = "Office Supply Ltd", displayName = "Office Supply", countryCode = "ZM",
                categoryCode = "OFFICE",
                primaryContact = new { name = "Sales", email = "sales@example.test" } });
        var vendor = await vendorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var vendorId = vendor.GetProperty("id").GetGuid();
        var entryPath = $"/api/procurement/v1/items/{itemId}/catalog-entries";
        var entryResponse = await SendJson(fixture.Client, HttpMethod.Post, entryPath,
            fixture.Owner, new EntryInput(vendorId, "ZMW", "each", 250,
                null, null, null, DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddMonths(1), null), "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, entryResponse.StatusCode);
        var entry = await entryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var publishPath = $"{entryPath}/{entry.GetProperty("id").GetGuid()}/publish";
        var publish = new CatalogPublishInput(entry.GetProperty("version").GetInt64(),
            "Checked approved vendor and price");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Owner,
                publish, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, publishPath, fixture.Other,
                publish, "procurement_manager")).StatusCode);

        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{vendorId}/submit", fixture.Owner,
            new VendorSubmitInput(vendor.GetProperty("version").GetInt64(), "Catalog supply"));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var approved = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{vendorId}/approve", fixture.Other,
            new VendorApprovalInput(pending.GetProperty("version").GetInt64(),
                "Duplicate and identity checks completed", true), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var published = await SendJson(fixture.Client, HttpMethod.Post, publishPath,
            fixture.Other, publish, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedBody = await published.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", publishedBody.GetProperty("status").GetString());
        // Catalog classification is owner data, not an editable policy-routing hint.
        var catalogDraft = Draft("Owned catalog classification") with
        {
            NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Lines = [new ProcurementRequestRoutes.RequestLineInput(null, "Chair", "each", 1, 250,
                itemId, vendorId, null, null, null, null, null, null, null, null,
                entry.GetProperty("id").GetGuid())],
        };
        var beforeSpoof = await fixture.RequestPersistenceCounts();
        foreach (var spoof in new[]
        {
            catalogDraft.Lines![0] with { PurchaseType = "SERVICE" },
            catalogDraft.Lines![0] with { CategoryCode = "OTHER" },
        })
        {
            var denied = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
                fixture.Owner, catalogDraft with { Lines = [spoof] });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
            Assert.Equal(beforeSpoof, await fixture.RequestPersistenceCounts());
        }
        var owned = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, catalogDraft);
        Assert.Equal(HttpStatusCode.Created, owned.StatusCode);
        var ownedBody = await owned.Content.ReadFromJsonAsync<JsonElement>();
        var ownedId = ownedBody.GetProperty("id").GetGuid();
        Assert.Equal("GOODS", ownedBody.GetProperty("lines")[0].GetProperty("purchaseType").GetString());
        Assert.Equal("OFFICE", ownedBody.GetProperty("lines")[0].GetProperty("categoryCode").GetString());
        var priorOwnedRead = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{ownedId}", fixture.Owner);
        var priorOwnedJson = await priorOwnedRead.Content.ReadAsStringAsync();
        var beforeEdit = await fixture.RequestPersistenceCounts();
        var spoofEdit = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{ownedId}", fixture.Owner,
            new ProcurementRequestRoutes.UpdateRequest(ownedBody.GetProperty("version").GetInt64(),
                "Must not mutate header", "ZMW", catalogDraft.NeededBy, null, null, null, null, null, null,
                [catalogDraft.Lines![0] with { PurchaseType = "SERVICE" }]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, spoofEdit.StatusCode);
        Assert.Equal(beforeEdit, await fixture.RequestPersistenceCounts());
        var afterOwnedRead = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{ownedId}", fixture.Owner);
        Assert.Equal(priorOwnedJson, await afterOwnedRead.Content.ReadAsStringAsync());
        await fixture.AddPublishedBuyingPolicy("GOODS");
        var ownedSubmit = await SendJson(fixture.Client, HttpMethod.Post, $"/api/procurement/v1/requests/{ownedId}/submit",
            fixture.Owner, new ProcurementRequestRoutes.VersionCommand(ownedBody.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.OK, ownedSubmit.StatusCode);
        var ownedPolicy = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{ownedId}/policy-status", fixture.Owner);
        Assert.Equal("Matched", (await ownedPolicy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        // Simulate an inherited tampered draft; submission must neither repair nor authorize it.
        var inheritedResponse = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, catalogDraft);
        Assert.Equal(HttpStatusCode.Created, inheritedResponse.StatusCode);
        var inherited = await inheritedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var inheritedId = inherited.GetProperty("id").GetGuid();
        await fixture.SetDraftClassification(inheritedId, "OFFICE", "SERVICE");
        var inheritedRead = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{inheritedId}", fixture.Owner);
        var inheritedJson = await inheritedRead.Content.ReadFromJsonAsync<JsonElement>();
        var inheritedBefore = await fixture.RequestPersistenceCounts();
        var inheritedSubmit = await SendJson(fixture.Client, HttpMethod.Post, $"/api/procurement/v1/requests/{inheritedId}/submit",
            fixture.Owner, new ProcurementRequestRoutes.VersionCommand(inheritedJson.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inheritedSubmit.StatusCode);
        Assert.Equal(inheritedBefore, await fixture.RequestPersistenceCounts());
        var deniedInheritedRead = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/requests/{inheritedId}", fixture.Owner);
        var retainedInherited = await deniedInheritedRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", retainedInherited.GetProperty("status").GetString());
        Assert.Equal("SERVICE", retainedInherited.GetProperty("lines")[0].GetProperty("purchaseType").GetString());
        Assert.Equal(0, await fixture.RequestAuditCount(inheritedId, "PolicyEvaluated"));
        Assert.Equal(1, await fixture.RequestAuditCount(entry.GetProperty("id").GetGuid(), "Published"));
        var catalog = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/catalog", fixture.Owner);
        Assert.Equal(1, (await catalog.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        await fixture.AddExpiredQualification(vendorId);
        var expiredCatalog = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/catalog", fixture.Owner);
        Assert.Equal(0, (await expiredCatalog.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        await fixture.AddCurrentQualification(vendorId);
        var renewedCatalog = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/catalog", fixture.Owner);
        Assert.Equal(1, (await renewedCatalog.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/catalog",
                fixture.Owner, fixture.OtherCompany)).StatusCode);
        var invalidPriceRequest = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/requests", fixture.Owner, new
            {
                purpose = "Office chair", currencyCode = "ZMW", neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                lines = new[] { new { description = "Chair", unitCode = "each", requestedQuantity = 1,
                    estimatedUnitPrice = 249, catalogItemId = itemId, preferredSupplierId = vendorId,
                    catalogEntryId = entry.GetProperty("id").GetGuid(), categoryCode = "OFFICE" } },
            }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidPriceRequest.StatusCode);
        var wrongCurrencyRequest = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/requests", fixture.Owner, new
            {
                purpose = "Office chair", currencyCode = "USD", neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                lines = new[] { new { description = "Chair", unitCode = "each", requestedQuantity = 1,
                    estimatedUnitPrice = 250, catalogItemId = itemId, preferredSupplierId = vendorId,
                    catalogEntryId = entry.GetProperty("id").GetGuid(), categoryCode = "OFFICE" } },
            }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCurrencyRequest.StatusCode);
        var requestResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/requests", fixture.Owner, new
            {
                purpose = "Office chair", currencyCode = "ZMW", neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                lines = new[] { new { description = "Chair", unitCode = "each", requestedQuantity = 1,
                    estimatedUnitPrice = 250, catalogItemId = (Guid?)itemId, preferredSupplierId = (Guid?)vendorId,
                    catalogEntryId = (Guid?)entry.GetProperty("id").GetGuid(), categoryCode = " office ", purchaseType = "Goods" },
                    new { description = "Office supplies", unitCode = "each", requestedQuantity = 1,
                        estimatedUnitPrice = 25, catalogItemId = (Guid?)null, preferredSupplierId = (Guid?)null,
                        catalogEntryId = (Guid?)null, categoryCode = "OFFICE", purchaseType = "Service" } },
            }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var request = await requestResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(entry.GetProperty("id").GetGuid(), request.GetProperty("lines")[0]
            .GetProperty("catalogEntryId").GetGuid());
        Assert.Equal(1, request.GetProperty("lines")[0].GetProperty("catalogPublishedVersion").GetInt32());
        Assert.Equal("GOODS", request.GetProperty("lines")[0].GetProperty("purchaseType").GetString());
        Assert.Equal("OFFICE", request.GetProperty("lines")[0].GetProperty("categoryCode").GetString());
        Assert.Equal("SERVICE", request.GetProperty("lines")[1].GetProperty("purchaseType").GetString());
        var unchangedItem = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/items/{itemId}",
            fixture.Owner, role: "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, unchangedItem.StatusCode);
        Assert.Equal("Goods", (await unchangedItem.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, request.GetProperty("lines")[1].GetProperty("catalogEntryId").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"{entryPath}/{entry.GetProperty("id").GetGuid()}/withdraw", fixture.Owner,
                new CatalogWithdrawInput(publishedBody.GetProperty("version").GetInt64(), ""),
                "procurement_buyer")).StatusCode);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post,
            $"{entryPath}/{entry.GetProperty("id").GetGuid()}/withdraw", fixture.Owner,
            new CatalogWithdrawInput(publishedBody.GetProperty("version").GetInt64(),
                "Superseded by a new vendor price"), "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(entry.GetProperty("id").GetGuid(), "Withdrawn"));
        var emptyCatalog = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/catalog", fixture.Owner);
        Assert.Equal(0, (await emptyCatalog.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        var staleCatalogSubmit = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{request.GetProperty("id").GetGuid()}/submit",
            fixture.Owner, new ProcurementRequestRoutes.VersionCommand(request.GetProperty("version").GetInt64()),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, staleCatalogSubmit.StatusCode);
    }

    [Fact]
    public async Task Request_list_counts_only_current_lines_after_a_draft_revision()
    {
        await using var fixture = await Fixture.Start();
        var twoLines = Draft("Mixed equipment") with
        {
            Lines =
            [
                new ProcurementRequestRoutes.RequestLineInput(null, "Laptop", "each", 1, 100,
                    null, null, null, "Goods", null, null, null, null, null, null),
                new ProcurementRequestRoutes.RequestLineInput(null, "Printer", "each", 1, 80,
                    null, null, null, "Goods", null, null, null, null, null, null),
            ],
        };
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, twoLines);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = record.GetProperty("id").GetGuid();
        var keep = record.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var revised = new ProcurementRequestRoutes.UpdateRequest(record.GetProperty("version").GetInt64(),
            "Mixed equipment", null, null, null, null, null, null, null, null,
            [twoLines.Lines![0] with { Id = keep },
                new ProcurementRequestRoutes.RequestLineInput(null, "Scanner", "each", 1, 60,
                    null, null, null, "Goods", null, null, null, null, null, null)]);
        var updated = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/requests/{id}", fixture.Owner, revised);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var revisedRecord = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, revisedRecord.GetProperty("lines").GetArrayLength());
        Assert.Single(revisedRecord.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("approvalStatus").GetString() == "Cancelled");
        var list = await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/requests", fixture.Owner);
        var summary = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("records")[0];
        Assert.Equal(2, summary.GetProperty("lineCount").GetInt32());
    }

    [Fact]
    public async Task Vendor_submission_requires_details_and_stays_non_orderable_during_review()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Acme Zambia Ltd", displayName = "Acme", countryCode = "ZM",
                categoryCode = "OFFICE", primaryContact = new { name = "A Person", email = "acme@example.test" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = saved.GetProperty("id").GetGuid();
        var version = saved.GetProperty("version").GetInt64();
        var path = $"/api/procurement/v1/vendors/{id}/submit";

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                new VendorSubmitInput(version, ""))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSubmitInput(version, "Office supplies"))).StatusCode);

        var submitted = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new VendorSubmitInput(version, "Office supplies"));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var result = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isOrderable").GetBoolean());
        Assert.Equal(1, await fixture.DecisionCount(id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                new VendorSubmitInput(version, "Repeat"))).StatusCode);

        var list = await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/vendors", fixture.Owner);
        var rows = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Contains(rows.EnumerateArray(), row => row.GetProperty("id").GetGuid() == id
            && row.GetProperty("status").GetString() == "Submitted");
        var reviewerList = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/vendors?status=Submitted", fixture.Other,
            role: "procurement_manager");
        Assert.Contains((await reviewerList.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        var unrelatedList = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/vendors?status=Submitted", fixture.Other);
        Assert.Empty((await unrelatedList.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/vendors/{id}", fixture.Other)).StatusCode);
        var proposerView = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner);
        Assert.False((await proposerView.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("canReturnForInformation").GetBoolean());
        var reviewerView = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Other, role: "procurement_manager");
        Assert.True((await reviewerView.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("canReturnForInformation").GetBoolean());

        var reviewPath = $"/api/procurement/v1/vendors/{id}/return";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Owner,
                new VendorReviewReturnInput(result.GetProperty("version").GetInt64(), "Need more details"),
                "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
                new VendorReviewReturnInput(result.GetProperty("version").GetInt64(), ""),
                "procurement_manager")).StatusCode);
        var returned = await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
            new VendorReviewReturnInput(result.GetProperty("version").GetInt64(), "Confirm legal address"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var returnedRecord = await returned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("More Information Required", returnedRecord.GetProperty("status").GetString());
        Assert.False(returnedRecord.GetProperty("isOrderable").GetBoolean());
        Assert.Equal(2, await fixture.DecisionCount(id));

        var resubmitted = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new VendorSubmitInput(returnedRecord.GetProperty("version").GetInt64(),
                "Legal address confirmed with contact"));
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        var resubmittedRecord = await resubmitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", resubmittedRecord.GetProperty("status").GetString());
        Assert.Equal(3, await fixture.DecisionCount(id));

        var returnedAgain = await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
            new VendorReviewReturnInput(resubmittedRecord.GetProperty("version").GetInt64(),
                "No longer needed"), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, returnedAgain.StatusCode);
        var returnedAgainRecord = await returnedAgain.Content.ReadFromJsonAsync<JsonElement>();
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/withdraw", fixture.Owner,
            new VendorWithdrawInput(returnedAgainRecord.GetProperty("version").GetInt64(),
                "Request cancelled"));
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("Withdrawn", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(5, await fixture.DecisionCount(id));
    }

    [Fact]
    public async Task Vendor_draft_contacts_and_addresses_can_be_corrected_and_removed_without_erasing_history()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Draft Supplies Ltd", displayName = "Draft Supplies",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Old contact", email = "old@example.test" },
                sites = new[] { new { kind = "Billing", addressLine1 = "Old road", countryCode = "ZM",
                    isPrimary = true } } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var path = $"/api/procurement/v1/vendors/{id}";
        var detail = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var contactId = body.GetProperty("contacts")[0].GetProperty("id").GetGuid();
        var siteId = body.GetProperty("sites")[0].GetProperty("id").GetGuid();
        var version = body.GetProperty("version").GetInt64();
        var contactPath = $"{path}/contacts/{contactId}";
        var editContact = new { version, contact = new { name = "New contact", email = "new@example.test",
            isPrimary = true } };
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Put, contactPath, fixture.Other,
                editContact)).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, contactPath,
            fixture.Owner, editContact);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var current = await updated.Content.ReadFromJsonAsync<JsonElement>();
        var nextVersion = current.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, contactPath, fixture.Owner,
                editContact)).StatusCode);
        var sitePath = $"{path}/sites/{siteId}";
        var updatedSite = await SendJson(fixture.Client, HttpMethod.Put, sitePath,
            fixture.Owner, new { version = nextVersion, site = new { kind = "Billing",
                addressLine1 = "New road", countryCode = "ZM", isPrimary = true } });
        Assert.Equal(HttpStatusCode.OK, updatedSite.StatusCode);
        nextVersion = (await updatedSite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{contactPath}/remove",
                fixture.Owner, new VendorDetailRemoval(nextVersion, ""))).StatusCode);
        var removedContact = await SendJson(fixture.Client, HttpMethod.Post, $"{contactPath}/remove",
            fixture.Owner, new VendorDetailRemoval(nextVersion, "Wrong person"));
        Assert.Equal(HttpStatusCode.OK, removedContact.StatusCode);
        nextVersion = (await removedContact.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64();
        var removedSite = await SendJson(fixture.Client, HttpMethod.Post, $"{sitePath}/remove",
            fixture.Owner, new VendorDetailRemoval(nextVersion, "Wrong address"));
        Assert.Equal(HttpStatusCode.OK, removedSite.StatusCode);
        nextVersion = (await removedSite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64();
        var refreshed = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("contacts").EnumerateArray());
        Assert.Empty(body.GetProperty("sites").EnumerateArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{path}/submit", fixture.Owner,
                new VendorSubmitInput(nextVersion, "Need supplier"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{contactPath}/remove", fixture.Owner,
                new VendorDetailRemoval(nextVersion, "Again"))).StatusCode);
        Assert.Equal(1, await fixture.VendorDetailAuditCount(contactId, "Removed"));
        Assert.Equal(1, await fixture.VendorDetailAuditCount(siteId, "Removed"));
    }

    [Fact]
    public async Task Vendor_activation_requires_independent_manager_review_and_records_decision()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Supply Zambia Ltd", displayName = "Supply Zambia",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Supplier Contact", email = "contact@example.test" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/submit", fixture.Owner,
            new VendorSubmitInput(draft.GetProperty("version").GetInt64(), "Required for office supplies"));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var version = pending.GetProperty("version").GetInt64();
        var approvalPath = $"/api/procurement/v1/vendors/{id}/approve";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Other,
                new VendorApprovalInput(version, "Reviewed", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Owner,
                new VendorApprovalInput(version, "Reviewed", true), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Other,
                new VendorApprovalInput(version, "Reviewed", false), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Other,
                new VendorApprovalInput(version - 1, "Reviewed", true), "procurement_manager")).StatusCode);

        var approved = await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Other,
            new VendorApprovalInput(version, "Identity and duplicate check completed", true),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var active = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", active.GetProperty("status").GetString());
        Assert.True(active.GetProperty("isOrderable").GetBoolean());
        Assert.Equal(2, await fixture.DecisionCount(id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, approvalPath, fixture.Other,
                new VendorApprovalInput(active.GetProperty("version").GetInt64(), "Repeat", true),
                "procurement_manager")).StatusCode);
    }

    [Fact]
    public async Task Independent_vendor_approver_can_review_submitted_vendor_without_manage_grant()
    {
        await using var fixture = await Fixture.Start();
        const string reviewerActions = "procurement-access,procurement-vendor-read,procurement-vendor-approve";
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Review Supplier Ltd", displayName = "Review Supplier",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Supplier Contact" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var path = $"/api/procurement/v1/vendors/{id}";

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Reviewer,
                actions: reviewerActions)).StatusCode);
        var submitted = await SendJson(fixture.Client, HttpMethod.Post, path + "/submit",
            fixture.Owner, new VendorSubmitInput(draft.GetProperty("version").GetInt64(), "Needed"));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        var reviewerList = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/vendors?status=Submitted", fixture.Reviewer, actions: reviewerActions);
        Assert.Equal(HttpStatusCode.OK, reviewerList.StatusCode);
        var rows = (await reviewerList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Contains(rows.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        var reviewerDetail = await Send(fixture.Client, HttpMethod.Get, path, fixture.Reviewer,
            actions: reviewerActions);
        Assert.Equal(HttpStatusCode.OK, reviewerDetail.StatusCode);
        var detail = await reviewerDetail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(detail.GetProperty("canApprove").GetBoolean());
        Assert.Single(detail.GetProperty("contacts").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Reviewer,
                new { version = detail.GetProperty("version").GetInt64() },
                actions: reviewerActions)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
                role: "employee")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Reviewer,
                company: fixture.OtherCompany, actions: reviewerActions)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invoice_finance_readiness_explains_missing_steps_without_new_authority_or_writes(bool matchingSchema)
    {
        await using var fixture = await Fixture.Start(matchingSchema: matchingSchema);
        var supplier = await fixture.AddInvoiceSupplier();
        var captured = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/invoices", fixture.Other,
            new InvoiceIntakeInput(supplier, null, "READINESS-1", DateOnly.FromDateTime(DateTime.UtcNow), null, "ZMW",
                [new InvoiceIntakeLineInput("Recorded invoice", 1, 100, 0, null, null)]), "ap_processor");
        Assert.Equal(HttpStatusCode.Created, captured.StatusCode);
        var invoice = await captured.Content.ReadFromJsonAsync<JsonElement>(); var id = invoice.GetProperty("id").GetGuid();
        var before = await fixture.InvoiceReadinessFootprint();
        var path = $"/api/procurement/v1/invoices/{id}/finance-readiness";
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner)).StatusCode);
        foreach (var role in new[] { "ap_processor", "erp_admin" })
        {
            var response = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: role);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, body.GetProperty("invoiceId").GetGuid());
            Assert.Equal(invoice.GetProperty("version").GetInt64(), body.GetProperty("invoiceVersion").GetInt64());
            Assert.Equal("Blocked", body.GetProperty("state").GetString()); Assert.False(body.GetProperty("canPrepareHandoff").GetBoolean());
            var codes = body.GetProperty("blockers").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();
            Assert.Contains("InvoiceValidationAuthorityUnavailable", codes); Assert.Contains("FinanceReceiverUnavailable", codes);
            Assert.Contains(matchingSchema ? "InvoiceNotCompared" : "MatchingEvidenceUnavailable", codes);
            Assert.Equal(matchingSchema, body.GetProperty("evidence").GetProperty("matchingSchemaAvailable").GetBoolean());
            Assert.Equal("NotRevalidated", body.GetProperty("evidence").GetProperty("comparisonCurrentness").GetString());
            if (!matchingSchema)
            {
                Assert.DoesNotContain("InvoiceNotCompared", codes);
                Assert.Equal(JsonValueKind.Null, body.GetProperty("evidence").GetProperty("unresolvedExceptionCount").ValueKind);
            }
            Assert.NotEmpty(body.GetProperty("nextSteps").EnumerateArray());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/invoices/" + Guid.NewGuid() + "/finance-readiness", fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
            company: fixture.OtherCompany, role: "erp_admin")).StatusCode);
        var foreignInvoiceId = await fixture.AddForeignTenantReadinessInvoice();
        var foreignRead = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/invoices/{foreignInvoiceId}/finance-readiness", fixture.Other, role: "erp_admin");
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        Assert.DoesNotContain("FOREIGN-TENANT-PRIVATE", await foreignRead.Content.ReadAsStringAsync());
        Assert.Equal(before, await fixture.InvoiceReadinessFootprint());
        await fixture.ConfineInvoiceReaderElsewhere(fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceReadinessFootprint());
    }

    [Fact]
    public async Task Manual_invoice_intake_is_scoped_deduplicated_and_never_posts_to_finance()
    {
        await using var fixture = await Fixture.Start();
        var supplierId = await fixture.AddInvoiceSupplier();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var input = new InvoiceIntakeInput(supplierId, null, " INV-001 ", today,
            today.AddDays(30), "zmw", [new InvoiceIntakeLineInput("Office supplies", 2, 125, 25, null, "OPS")]);
        var path = "/api/procurement/v1/invoices";
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PostAsJsonAsync(path, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, input)).StatusCode);

        var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other, input,
            "ap_processor");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = row.GetProperty("id").GetGuid();
        Assert.Equal("Received", row.GetProperty("status").GetString());
        Assert.Equal(275m, row.GetProperty("grossAmount").GetDecimal());

        var detail = await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Other,
            role: "ap_processor");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var saved = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INV-001", saved.GetProperty("invoiceNumber").GetString());
        Assert.Single(saved.GetProperty("lines").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Other,
                company: fixture.OtherCompany, role: "ap_processor")).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                input with { InvoiceNumber = "inv-001" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                input with { InvoiceNumber = "INV-002", DueDate = today.AddDays(-1) }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                input with { InvoiceNumber = "INV-003", Lines = [new InvoiceIntakeLineInput("Invalid", 0, 1, 0, null, null)] },
                "ap_processor")).StatusCode);
        Assert.Equal(0, await fixture.InvoiceHandoffCount());
    }

    [Theory]
    [InlineData("quantity","1.00000000000000000000000000001")]
    [InlineData("unitPrice","1.00000000000000000000000000001")]
    [InlineData("taxAmount","1.00000000000000000000000000001")]
    [InlineData("quantity","\"1.00000000000000000000000000001\"")]
    [InlineData("unitPrice","\"0.00001\"")]
    [InlineData("taxAmount","1e-1000")]
    [InlineData("quantity","100000000000000")]
    public async Task Invoice_raw_decimal_lexemes_reject_without_rounding_or_any_writes(string field,string token)
    {
        await using var fixture=await Fixture.Start();var supplier=await fixture.AddInvoiceSupplier();
        var before=await fixture.InvoiceReadinessFootprint();
        var values=new Dictionary<string,string> { ["quantity"]="1",["unitPrice"]="1",["taxAmount"]="0" };values[field]=token;
        var line="{\"description\":\"Raw exact invoice\",\"quantity\":"+values["quantity"]+",\"unitPrice\":"+values["unitPrice"]+",\"taxAmount\":"+values["taxAmount"]+"}";
        using var response=await RawInvoice(fixture,supplier,"RAW-LEXEME",line);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation-failed",error.GetProperty("code").GetString());
        Assert.Contains("four decimal",error.GetProperty("message").GetString());
        Assert.Equal(before,await fixture.InvoiceReadinessFootprint());
    }
    [Theory]
    [InlineData("product-overflow")][InlineData("aggregate-overflow")][InlineData("gross-overflow")]
    [InlineData("header-scale")][InlineData("invoice-number")][InlineData("null-line")]
    public async Task Invoice_exact_header_contract_rejects_before_tracking_or_persisting(string scenario)
    {
        await using var fixture=await Fixture.Start();var supplier=await fixture.AddInvoiceSupplier();
        var before=await fixture.InvoiceReadinessFootprint();var number="INVALID-HEADER";
        var lines=scenario switch
        {
            "product-overflow"=>"{\"description\":\"Overflow\",\"quantity\":\"99999999999999.9999\",\"unitPrice\":\"99999999999999.9999\",\"taxAmount\":0}",
            "aggregate-overflow"=>string.Join(',',Enumerable.Repeat("{\"description\":\"Aggregate\",\"quantity\":1,\"unitPrice\":999999999999.9999,\"taxAmount\":0}",200)),
            "gross-overflow"=>"{\"description\":\"Gross\",\"quantity\":1,\"unitPrice\":\"99999999999999.9999\",\"taxAmount\":0.0001}",
            "header-scale"=>"{\"description\":\"No rounding\",\"quantity\":0.0001,\"unitPrice\":0.0001,\"taxAmount\":0}",
            "null-line"=>"null",
            _=>"{\"description\":\"Long number\",\"quantity\":1,\"unitPrice\":1,\"taxAmount\":0}"
        };
        if(scenario=="invoice-number") number=new string('N',81);
        using var response=await RawInvoice(fixture,supplier,number,lines);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation-failed",error.GetProperty("code").GetString());
        Assert.Equal(before,await fixture.InvoiceReadinessFootprint());
    }
    [Fact]
    public async Task Invoice_exact_eight_place_products_may_sum_to_representable_header_without_line_rounding()
    {
        await using var fixture=await Fixture.Start();var supplier=await fixture.AddInvoiceSupplier();
        const string lines="{\"description\":\"First exact product\",\"quantity\":\"0.0001\",\"unitPrice\":0.0001,\"taxAmount\":0},{\"description\":\"Second exact product\",\"quantity\":0.0001,\"unitPrice\":\"0.9999\",\"taxAmount\":0}";
        using var response=await RawInvoice(fixture,supplier,"EXACT-PRODUCT-SUM",lines);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var captured=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(.0001m,captured.GetProperty("grossAmount").GetDecimal());
        using var detail=await Send(fixture.Client,HttpMethod.Get,"/api/procurement/v1/invoices/"+captured.GetProperty("id").GetGuid(),fixture.Other,role:"ap_processor");
        var saved=await detail.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(.0001m,saved.GetProperty("netAmount").GetDecimal());
        Assert.Equal(.0001m,saved.GetProperty("grossAmount").GetDecimal());Assert.Equal(0m,saved.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(.0001m,saved.GetProperty("lines")[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(.9999m,saved.GetProperty("lines")[1].GetProperty("unitPrice").GetDecimal());
    }
    private static Task<HttpResponseMessage> RawInvoice(Fixture fixture,Guid supplier,string number,string lines)
    {
        var body="{\"supplierId\":\""+supplier+"\",\"invoiceNumber\":\""+number+"\",\"invoiceDate\":\"2026-10-20\",\"currencyCode\":\"ZMW\",\"lines\":["+lines+"]}";
        var request=new HttpRequestMessage(HttpMethod.Post,"/api/procurement/v1/invoices") { Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json") };
        request.Headers.Add("X-Test-User",fixture.Other.ToString());request.Headers.Add("X-Test-Role","ap_processor");
        return fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task Invoice_intake_hides_other_branches_and_rejects_out_of_scope_order_links()
    {
        await using var fixture = await Fixture.Start();
        var supplierId = await fixture.AddInvoiceSupplier();
        var orderInput = new OrderDraftInput(supplierId, "ZMW", null, null, null, null,
            [new OrderDraftLineInput(null, "Office supplies", "EA", 1, 100, 0, null, null, null)]);
        var orderResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/orders", fixture.Other, orderInput, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var orderId = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        const string path = "/api/procurement/v1/invoices";
        var input = new InvoiceIntakeInput(supplierId, null, "BRANCH-INV-001",
            DateOnly.FromDateTime(DateTime.UtcNow), null, null,
            [new InvoiceIntakeLineInput("Office supplies", 1, 100, 0, null, null)]);
        var assignedBranch = await fixture.ConfineBuyerAndMoveOrderToOtherBranch(orderId);
        var invoiceBranch = await fixture.OrderBranch(orderId);
        // Capture the original in its real branch; never rewrite a captured invoice to arrange scope.
        var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Reviewer, input,
            "ap_processor", selectedLocation: invoiceBranch);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        Assert.Equal(invoiceBranch, original.GetProperty("branchRef").GetGuid());

        var list = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "ap_processor");
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Reviewer, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other, input, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                input with { InvoiceNumber = "BRANCH-INV-002", PurchaseOrderId = orderId }, "ap_processor")).StatusCode);

        var scoped = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            input with { InvoiceNumber = "BRANCH-INV-003" }, "ap_processor");
        Assert.Equal(HttpStatusCode.Created, scoped.StatusCode);
        var own = await scoped.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(assignedBranch, own.GetProperty("branchRef").GetGuid());
        Assert.Equal("ZMW", own.GetProperty("currencyCode").GetString());
        Assert.Equal(HttpStatusCode.OK,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{own.GetProperty("id").GetGuid()}",
                fixture.Other, role: "ap_processor")).StatusCode);
        var ownList = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "ap_processor");
        Assert.Equal(1, (await ownList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(0, await fixture.InvoiceHandoffCount());
    }

    [Fact]
    public async Task Buyer_order_drafts_are_scoped_versioned_and_retire_removed_lines()
    {
        await using var fixture = await Fixture.Start();
        var supplierId = await fixture.AddInvoiceSupplier();
        var path = "/api/procurement/v1/orders";
        var first = new OrderDraftLineInput(null, "Laptop", "EA", 2, 500, 100, "IT", null, null);
        var input = new OrderDraftInput(supplierId, null, "Lusaka office", null, "Net 30", null, [first]);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PostAsJsonAsync(path, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, input)).StatusCode);
        var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            input, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var lineId = draft.GetProperty("lines")[0].GetProperty("id").GetGuid();
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal("ZMW", draft.GetProperty("currencyCode").GetString());
        Assert.Equal(1100m, draft.GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Other,
                company: fixture.OtherCompany, role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Owner,
                role: "procurement_buyer")).StatusCode);

        var replacement = new OrderDraftLineInput(null, "Monitor", "EA", 1, 250, 0, null, null, null);
        var revised = input with { Lines = [replacement] };
        var updatePath = $"{path}/{id}";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Put, updatePath, fixture.Other,
                new ProcurementOrderDraftRoutes.VersionedOrderDraft(1, null, revised),
                "procurement_buyer")).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, updatePath, fixture.Other,
            new ProcurementOrderDraftRoutes.VersionedOrderDraft(1, "Specification changed", revised),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var saved = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, saved.GetProperty("version").GetInt64());
        Assert.Equal(250m, saved.GetProperty("total").GetDecimal());
        Assert.Single(saved.GetProperty("lines").EnumerateArray());
        Assert.NotEqual(lineId, saved.GetProperty("lines")[0].GetProperty("id").GetGuid());
        Assert.Equal((1, 2), await fixture.OrderLineCounts(id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, updatePath, fixture.Other,
                new ProcurementOrderDraftRoutes.VersionedOrderDraft(1, "Old", revised),
                "procurement_buyer")).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/cancel", fixture.Other,
                new ProcurementOrderDraftRoutes.OrderDraftCancel(2, ""),
                "procurement_buyer")).StatusCode);
        var cancelled = await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/cancel", fixture.Other,
            new ProcurementOrderDraftRoutes.OrderDraftCancel(2, "No longer needed"),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var closed = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", closed.GetProperty("status").GetString());
        Assert.Equal("No longer needed", closed.GetProperty("closureReason").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, updatePath, fixture.Other,
                new ProcurementOrderDraftRoutes.VersionedOrderDraft(3, "", revised),
                "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/issue", fixture.Other,
                new { }, "procurement_buyer")).StatusCode);
    }

    [Fact]
    public async Task Buyer_order_drafts_keep_the_selected_branch_and_hide_other_branches()
    {
        await using var fixture = await Fixture.Start();
        var supplierId = await fixture.AddInvoiceSupplier();
        var input = new OrderDraftInput(supplierId, null, "Lusaka office", null, "Net 30", null,
            [new OrderDraftLineInput(null, "Laptop", "EA", 1, 500, 0, null, null, null)]);
        const string path = "/api/procurement/v1/orders";
        var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            input, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = first.GetProperty("id").GetGuid();
        var assignedBranch = await fixture.ConfineBuyerAndMoveOrderToOtherBranch(id);

        var list = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
            role: "procurement_buyer");
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{path}/{id}", fixture.Other,
                role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{path}/{id}", fixture.Other,
                new ProcurementOrderDraftRoutes.VersionedOrderDraft(1, null, input),
                "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/cancel", fixture.Other,
                new ProcurementOrderDraftRoutes.OrderDraftCancel(1, "No longer needed"),
                "procurement_buyer")).StatusCode);

        var scoped = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            input, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, scoped.StatusCode);
        var own = await scoped.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(assignedBranch, own.GetProperty("branchRef").GetGuid());
        var ownList = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other,
            role: "procurement_buyer");
        Assert.Equal(1, (await ownList.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Active_vendor_can_be_suspended_only_by_independent_manager_with_reason()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Hold Supplier Ltd", displayName = "Hold Supplier",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Supplier Contact" } });
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/submit", fixture.Owner,
            new VendorSubmitInput(draft.GetProperty("version").GetInt64(), "Supplier review"));
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var approved = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/approve", fixture.Other,
            new VendorApprovalInput(pending.GetProperty("version").GetInt64(), "Verified", true),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var active = await approved.Content.ReadFromJsonAsync<JsonElement>();
        var version = active.GetProperty("version").GetInt64();
        await fixture.AddOpenOrder(id);
        var ordersPath = $"/api/procurement/v1/vendors/{id}/open-orders";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, ordersPath, fixture.Owner)).StatusCode);
        var openOrders = await Send(fixture.Client, HttpMethod.Get, ordersPath, fixture.Other,
            role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, openOrders.StatusCode);
        var ordersBody = await openOrders.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ordersBody.GetProperty("count").GetInt32());
        Assert.Equal("Issued", ordersBody.GetProperty("items")[0].GetProperty("status").GetString());
        var path = $"/api/procurement/v1/vendors/{id}/suspend";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(version, "Risk", true),
                role: "procurement_manager", company: fixture.OtherCompany)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(version, "Risk", false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                new VendorSuspendInput(version, "Risk", false), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(version, "", false), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(version - 1, "Risk", false), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(version, "Risk", false), "procurement_manager")).StatusCode);
        var suspended = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
            new VendorSuspendInput(version, "Diligence expired", true), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var result = await suspended.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Suspended", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isOrderable").GetBoolean());
        Assert.Equal("Diligence expired", result.GetProperty("holdReason").GetString());
        Assert.Equal(1, result.GetProperty("openOrders").GetInt32());
        Assert.Equal(3, await fixture.DecisionCount(id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other,
                new VendorSuspendInput(result.GetProperty("version").GetInt64(), "Repeat", true),
                "procurement_manager")).StatusCode);

        var reactivatePath = $"/api/procurement/v1/vendors/{id}/reactivate";
        var heldVersion = result.GetProperty("version").GetInt64();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Other,
                new VendorReactivateInput(heldVersion, "Resolved", true), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Owner,
                new VendorReactivateInput(heldVersion, "Resolved", true), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Reviewer,
                new VendorReactivateInput(heldVersion, "Resolved", true))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Reviewer,
                new VendorReactivateInput(heldVersion, "", true), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Reviewer,
                new VendorReactivateInput(heldVersion, "Resolved", false), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Reviewer,
                new VendorReactivateInput(heldVersion - 1, "Resolved", true), "procurement_manager")).StatusCode);
        var reactivated = await SendJson(fixture.Client, HttpMethod.Post, reactivatePath,
            fixture.Reviewer, new VendorReactivateInput(heldVersion, "Current diligence verified", true),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        var reopened = await reactivated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", reopened.GetProperty("status").GetString());
        Assert.True(reopened.GetProperty("isOrderable").GetBoolean());
        Assert.Equal(4, await fixture.DecisionCount(id));
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, reactivatePath, fixture.Reviewer,
                new VendorReactivateInput(reopened.GetProperty("version").GetInt64(), "Repeat", true),
                "procurement_manager")).StatusCode);
        await fixture.AddExpiredQualification(id);
        var requestWithExpiredVendor = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/requests", fixture.Owner, new
            {
                purpose = "Office supplies", currencyCode = "ZMW",
                neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                lines = new[] { new { description = "Paper", unitCode = "each", requestedQuantity = 1,
                    estimatedUnitPrice = 100, preferredSupplierId = id, categoryCode = "OFFICE" } },
            }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, requestWithExpiredVendor.StatusCode);
    }

    [Fact]
    public async Task Vendor_rejection_keeps_supplier_non_orderable_and_requires_reason()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Unqualified Ltd", displayName = "Unqualified",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Supplier Contact" } });
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var submitted = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/submit", fixture.Owner,
            new VendorSubmitInput(draft.GetProperty("version").GetInt64(), "Review"));
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var version = pending.GetProperty("version").GetInt64();
        var rejectPath = $"/api/procurement/v1/vendors/{id}/reject";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, rejectPath, fixture.Other,
                new VendorReviewReturnInput(version, ""), "procurement_manager")).StatusCode);
        var rejected = await SendJson(fixture.Client, HttpMethod.Post, rejectPath, fixture.Other,
            new VendorReviewReturnInput(version, "Company registration could not be confirmed"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var result = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isOrderable").GetBoolean());
        Assert.Equal(2, await fixture.DecisionCount(id));
    }

    [Fact]
    public async Task Vendor_draft_qualification_can_be_corrected_and_removed_with_history()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Equipment Supplier Ltd", displayName = "Equipment Supplier",
                countryCode = "ZM", categoryCode = "EQUIPMENT",
                primaryContact = new { name = "Supplier Contact", email = "equipment@example.test" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var vendor = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = vendor.GetProperty("id").GetGuid();
        var qualification = new VendorQualificationInput("Insurance", "Insurer", "POL-1",
            new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), null);
        var added = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/qualifications", fixture.Owner,
            new VersionedQualification(vendor.GetProperty("version").GetInt64(), qualification));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var current = await (await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner)).Content.ReadFromJsonAsync<JsonElement>();
        var qualificationId = current.GetProperty("qualifications")[0].GetProperty("id").GetGuid();
        var path = $"/api/procurement/v1/vendors/{id}/qualifications/{qualificationId}";
        var revision = new VersionedQualification(current.GetProperty("version").GetInt64(),
            qualification with { ReferenceNumber = "POL-2" });
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Other, revision)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
                revision with { Version = vendor.GetProperty("version").GetInt64() })).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner, revision);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(1, await fixture.VendorDetailAuditCount(qualificationId, "DraftUpdated"));
        var audit = await fixture.AuditPayload(qualificationId, "DraftUpdated");
        Assert.Equal("POL-1", audit.GetProperty("before").GetProperty("ReferenceNumber").GetString());
        Assert.Equal("POL-2", audit.GetProperty("after").GetProperty("ReferenceNumber").GetString());
        var revised = await (await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("POL-2", revised.GetProperty("qualifications")[0].GetProperty("referenceNumber").GetString());
        var removalPath = path + "/remove";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, removalPath, fixture.Owner,
                new VendorDetailRemoval(revised.GetProperty("version").GetInt64(), ""))).StatusCode);
        var removed = await SendJson(fixture.Client, HttpMethod.Post, removalPath, fixture.Owner,
            new VendorDetailRemoval(revised.GetProperty("version").GetInt64(), "Entered by mistake"));
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(1, await fixture.VendorDetailAuditCount(qualificationId, "Removed"));
        var after = await (await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, after.GetProperty("qualifications").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
                revision with { Version = after.GetProperty("version").GetInt64() })).StatusCode);
    }

    [Fact]
    public async Task Vendor_qualification_is_versioned_and_locked_during_review()
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors",
            fixture.Owner, new { legalName = "Office Supply Ltd", displayName = "Office Supply",
                countryCode = "ZM", categoryCode = "OFFICE",
                primaryContact = new { name = "Supplier Contact", email = "supplier@example.test" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var vendor = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = vendor.GetProperty("id").GetGuid();
        var version = vendor.GetProperty("version").GetInt64();
        var path = $"/api/procurement/v1/vendors/{id}/qualifications";
        var invalid = new VersionedQualification(version,
            new VendorQualificationInput("Tax clearance", "Revenue authority", "TC-1",
                new DateOnly(2026, 1, 1), new DateOnly(2025, 12, 31), null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, invalid)).StatusCode);
        var valid = invalid with { Qualification = invalid.Qualification with
            { ExpiresOn = new DateOnly(2027, 12, 31) } };
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Other, valid)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, valid)).StatusCode);
        var detail = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner);
        var saved = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", saved.GetProperty("qualifications")[0]
            .GetProperty("reviewStatus").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, valid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"/api/procurement/v1/vendors/{id}/submit", fixture.Owner,
                new VendorSubmitInput(saved.GetProperty("version").GetInt64(),
                    "Office supplies"))).StatusCode);
        var afterSubmit = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/vendors/{id}", fixture.Owner);
        var submitted = await afterSubmit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                valid with { Version = submitted.GetProperty("version").GetInt64() })).StatusCode);

        var submittedVersion = submitted.GetProperty("version").GetInt64();
        var qualificationId = submitted.GetProperty("qualifications")[0].GetProperty("id").GetGuid();
        var reviewPath = $"/api/procurement/v1/vendors/{id}/qualifications/{qualificationId}/review";
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post,
                $"/api/procurement/v1/vendors/{id}/approve", fixture.Other,
                new VendorApprovalInput(submittedVersion, "Ready", true), "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Owner,
                new VendorQualificationReviewInput(submittedVersion, "Verified", "Checked"),
                "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
                new VendorQualificationReviewInput(submittedVersion, "Invalid", "Checked"),
                "procurement_manager")).StatusCode);
        var reviewed = await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
            new VendorQualificationReviewInput(submittedVersion, "Verified", "Confirmed with issuer"),
            "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        var review = await reviewed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Verified", review.GetProperty("reviewStatus").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, reviewPath, fixture.Other,
                new VendorQualificationReviewInput(review.GetProperty("supplierVersion").GetInt64(),
                    "Verified", "Repeat"), "procurement_manager")).StatusCode);
        var approved = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/vendors/{id}/approve", fixture.Other,
            new VendorApprovalInput(review.GetProperty("supplierVersion").GetInt64(),
                "All qualifications checked", true), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    }

    [Fact]
    public async Task Contract_draft_requires_available_supplier_and_remains_non_active()
    {
        await using var fixture = await Fixture.Start();
        var vendorResponse = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/vendors", fixture.Owner,
            new { legalName = "Contract Supplier Ltd", displayName = "Contract Supplier",
                countryCode = "ZM" });
        Assert.Equal(HttpStatusCode.Created, vendorResponse.StatusCode);
        var vendor = await vendorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = vendor.GetProperty("id").GetGuid();
        var input = new ContractDraftInput(supplierId, "Office supplies", "Framework", null,
            10_000, new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30),
            new DateOnly(2027, 6, 30));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/contracts",
                fixture.Other, input, "procurement_buyer")).StatusCode);
        var created = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/contracts", fixture.Owner, input, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contract = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", contract.GetProperty("status").GetString());
        Assert.Equal("ZMW", contract.GetProperty("currencyCode").GetString());
        Assert.Equal("Draft", contract.GetProperty("draftVersion").GetProperty("status").GetString());
        var id = contract.GetProperty("id").GetGuid();
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftCreated"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/contracts/{id}",
                fixture.Other, role: "procurement_buyer")).StatusCode);
        var updated = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/contracts/{id}", fixture.Owner,
            new ProcurementContractDraftRoutes.VersionedContractDraft(
                contract.GetProperty("version").GetInt64(), input with { CeilingAmount = 12_000 }),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var revised = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12_000, revised.GetProperty("ceilingAmount").GetDecimal());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftUpdated"));
        var contractAudit = await fixture.AuditPayload(id, "DraftUpdated");
        Assert.Equal(10_000, contractAudit.GetProperty("before").GetProperty("CeilingAmount").GetDecimal());
        Assert.Equal(12_000, contractAudit.GetProperty("after").GetProperty("CeilingAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put,
                $"/api/procurement/v1/contracts/{id}", fixture.Owner,
                new ProcurementContractDraftRoutes.VersionedContractDraft(
                    contract.GetProperty("version").GetInt64(), input), "procurement_buyer")).StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftUpdated"));
        var cancelled = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/contracts/{id}/cancel", fixture.Owner,
            new ProcurementContractDraftRoutes.ContractCancel(
                revised.GetProperty("version").GetInt64(), "Agreement not needed"),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledContract = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", cancelledContract.GetProperty("status").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftCancelled"));
        Assert.Equal("Cancelled", cancelledContract.GetProperty("draftVersion")
            .GetProperty("status").GetString());
    }

    private static ProcurementRequestRoutes.NewRequest Draft(string purpose) => new(
        purpose, null, null, null, null, null, null, null, null,
        [new ProcurementRequestRoutes.RequestLineInput(null, "Laptop", "each", 2, 100,
            null, null, null, "Goods", null, null, null, null, null, null)]);

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        Guid user, Guid? company = null, string? role = null, string? actions = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-User", user.ToString());
        if (company.HasValue) request.Headers.Add("X-Shell-Entity", company.Value.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (actions is not null) request.Headers.Add("X-Test-Actions", actions);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJson<T>(HttpClient client, HttpMethod method, string path,
        Guid user, T body, string? role = null, Guid? company = null, string? actions = null, Guid? selectedLocation = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Test-User", user.ToString());
        if (company.HasValue) request.Headers.Add("X-Shell-Entity", company.Value.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (actions is not null) request.Headers.Add("X-Test-Actions", actions);
        if (selectedLocation.HasValue) request.Headers.Add("X-Shell-Location", selectedLocation.Value.ToString());
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Invoice_documents_use_Core_evidence_and_enforce_permissions_and_branch_scope()
    {
        await using var fixture = await Fixture.Start(documents: true);
        var supplierId = await fixture.AddInvoiceSupplier();
        var invoice = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/invoices", fixture.Owner,
            new InvoiceIntakeInput(supplierId, null, "DOC-INV-1", new DateOnly(2026, 10, 20), null, "ZMW",
                [new InvoiceIntakeLineInput("Supplies", 1, 100, 0, null, null)]), "ap_processor");
        Assert.Equal(HttpStatusCode.Created, invoice.StatusCode);
        var id = (await invoice.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/procurement/v1/invoices/{id}/documents";
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other)).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await SendJson(fixture.Client, HttpMethod.Post,
            path, fixture.Owner, new { file = "not a raw upload" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("%PDF-1.7 evidence", Guid.Empty)).StatusCode);
        var key = Guid.NewGuid();
        var upload = await Upload("%PDF-1.7 evidence", key);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var document = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var version = document.GetProperty("versionId").GetGuid();
        Assert.True(document.GetProperty("canDownload").GetBoolean());
        var retry = await Upload("%PDF-1.7 evidence", key);
        Assert.Equal(version, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versionId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Upload("%PDF-other evidence", key)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Upload("MZ-not-a-PDF", Guid.NewGuid())).StatusCode);
        var download = await Send(fixture.Client, HttpMethod.Get, path + $"/{version}/download", fixture.Owner, role: "ap_processor");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("%PDF-1.7 evidence", await download.Content.ReadAsStringAsync());
        Assert.Equal("no-store", download.Headers.CacheControl?.ToString());
        await fixture.ConfineInvoiceReaderElsewhere(fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path + $"/{version}/download", fixture.Other, role: "ap_processor")).StatusCode);
        fixture.FailDocumentScan(true);
        var failed = await Upload("%PDF-waiting for scanner", Guid.NewGuid());
        var waiting = await failed.Content.ReadFromJsonAsync<JsonElement>();
        var waitingId = waiting.GetProperty("versionId").GetGuid();
        Assert.False(waiting.GetProperty("canDownload").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(fixture.Client, HttpMethod.Get, path + $"/{waitingId}/download", fixture.Owner, role: "ap_processor")).StatusCode);
        fixture.FailDocumentScan(false);
        var rescanned = await Send(fixture.Client, HttpMethod.Post, path + $"/{waitingId}/retry-scan", fixture.Owner, role: "ap_processor");
        Assert.True((await rescanned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("canDownload").GetBoolean());
        Assert.Equal(0, await fixture.InvoiceHandoffCount());
        fixture.SetDocumentReadiness(false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Upload("%PDF-blocked-by-schema", Guid.NewGuid())).StatusCode);

        async Task<HttpResponseMessage> Upload(string body, Guid uploadKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body) };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Add("X-Test-User", fixture.Owner.ToString());
            request.Headers.Add("X-Test-Role", "ap_processor");
            request.Headers.Add("Idempotency-Key", uploadKey.ToString());
            request.Headers.Add("X-Document-Name", "invoice.pdf");
            return await fixture.Client.SendAsync(request);
        }
    }

    private sealed class DocumentTestReadiness : IDocumentSchemaReadiness
    {
        public bool Ready { get; set; } = true;
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(Ready);
    }
    private sealed class DocumentTestScanner : Mightyfin.Erp.Core.Documents.Application.IDocumentScanner
    {
        public bool Fail { get; set; }
        public async Task<Mightyfin.Erp.Core.Documents.Application.DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256, CancellationToken cancellationToken = default)
        {
            var hash = Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(content, cancellationToken));
            return new(Fail ? Mightyfin.Erp.Core.Documents.Domain.ScannerVerdict.Failed : Mightyfin.Erp.Core.Documents.Domain.ScannerVerdict.Clean,
                hash, "test-scanner", Fail ? null : "test-definitions", Fail ? "scanner-unavailable" : null);
        }
    }

    private sealed class Fixture(WebApplication app, Guid company, Guid owner, Guid other, Guid reviewer, Guid inactive, Guid otherCompany, string? documentRoot = null)
        : IAsyncDisposable
    {
        public string[] Routes() => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints.OfType<RouteEndpoint>())
            .Select(endpoint => endpoint.RoutePattern.RawText ?? "").ToArray();
        public HttpClient Client { get; } = app.GetTestClient();
        public Guid Owner { get; } = owner;
        public Guid Other { get; } = other;
        public Guid Reviewer { get; } = reviewer;
        public Guid Inactive { get; } = inactive;
        public Guid OtherCompany { get; } = otherCompany;

        public async Task MoveRequestToOtherBranchAndConfineReviewer(Guid requestId)
        {
            using var serviceScope = app.Services.CreateScope();
            var core = serviceScope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var existingBranch = await core.WorkLocations.SingleAsync(x => x.LegalEntityId == company);
            core.WorkLocations.Add(new WorkLocation
            {
                Code = "A2", Name = "Other branch", LegalEntityId = company,
            });
            core.UserBranchAssignments.Add(new HrUserBranchAssignment
            {
                UserId = Other, LocationId = existingBranch.Id,
            });
            await core.SaveChangesAsync();
            var secondBranch = await core.WorkLocations.SingleAsync(x => x.Code == "A2");
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var procurement = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await procurement.PurchaseRequests.SingleAsync(x => x.Id == requestId);
            request.BranchRef = secondBranch.Id;
            await procurement.SaveChangesAsync();
        }

        public async Task<Guid> ConfineBuyerAndMoveOrderToOtherBranch(Guid orderId)
        {
            using var serviceScope = app.Services.CreateScope();
            var core = serviceScope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var assignedBranch = await core.WorkLocations.SingleAsync(x => x.LegalEntityId == company);
            var secondBranch = new WorkLocation
            {
                Code = "A2", Name = "Other branch", LegalEntityId = company,
            };
            core.WorkLocations.Add(secondBranch);
            core.UserBranchAssignments.Add(new HrUserBranchAssignment
            {
                UserId = Other, LocationId = assignedBranch.Id,
            });
            await core.SaveChangesAsync();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "test");
            var procurement = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var order = await procurement.PurchaseOrders.SingleAsync(x => x.Id == orderId);
            order.BranchRef = secondBranch.Id;
            await procurement.SaveChangesAsync();
            return assignedBranch.Id;
        }

        public void SetDocumentReadiness(bool ready) => ((DocumentTestReadiness)app.Services.GetRequiredService<IDocumentSchemaReadiness>()).Ready = ready;
        public void FailDocumentScan(bool fail) => app.Services.GetRequiredService<DocumentTestScanner>().Fail = fail;
        public async Task<Guid> OrderBranch(Guid orderId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.PurchaseOrders.AsNoTracking().SingleAsync(x => x.Id == orderId)).BranchRef!.Value;
        }

        public async Task<int> DecisionCount(Guid supplierId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.SupplierDecisions.CountAsync(x => x.SupplierId == supplierId);
        }

        public async Task<Guid> AddInvoiceSupplier()
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier = new Supplier
            {
                Number = "VEN-INVOICE-" + Guid.NewGuid().ToString("N"),
                LegalName = "Invoice Supplier Ltd", DisplayName = "Invoice Supplier",
                CountryCode = "ZM", Status = "Active", IsOrderable = true,
            };
            db.AddScoped(supplier);
            await db.SaveChangesAsync();
            return supplier.Id;
        }

        public async Task<string> OriginalValueFootprint()
        {
            using var services=app.Services.CreateScope();services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a",company,Owner.ToString(),null,null,false,"original-value-test");
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { items=await db.CatalogItems.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
                prices=await db.CatalogEntries.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
                requests=await db.PurchaseRequests.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),requestLines=await db.PurchaseRequestLines.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
                plans=await db.ProcurementPlans.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),planLines=await db.ProcurementPlanLines.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
                contracts=await db.SupplierContracts.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),contractVersions=await db.SupplierContractVersions.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
                budget=await db.BudgetCheckSnapshots.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),audit=await db.AuditEvents.CountAsync(),outbox=await db.OutboxEvents.CountAsync() });
        }
        public async Task<string> InvoiceReadinessFootprint()
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new
            {
                invoices = await db.SupplierInvoices.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                lines = await db.SupplierInvoiceLines.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                mappings = await db.InvoiceMatchMappingVersions.CountAsync(), comparisons = await db.InvoiceMatchResults.CountAsync(),
                exceptions = await db.InvoiceExceptions.CountAsync(), handoffs = await db.FinanceInvoiceHandoffs.CountAsync(),
                payments = await db.FinancePaymentStatusRefs.CountAsync(), claims = await db.InvoiceMatchAllocations.CountAsync(),
                exposure = await db.InvoiceMatchExposureStates.CountAsync(), receipts = await db.CommercialCommandReceipts.CountAsync(),
                audit = await db.AuditEvents.CountAsync(), outbox = await db.OutboxEvents.CountAsync(),
            });
        }

        public async Task<Guid> AddForeignTenantReadinessInvoice()
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-foreign", company, Owner.ToString(), null, null, false, "foreign-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier = new Supplier { Number = "FOREIGN", LegalName = "Foreign", DisplayName = "Foreign", CountryCode = "ZM" };
            db.AddScoped(supplier);
            var invoice = new SupplierInvoice { SupplierId = supplier.Id, InvoiceNumber = "FOREIGN-TENANT-PRIVATE",
                NormalizedInvoiceNumber = "FOREIGN-TENANT-PRIVATE", CurrencyCode = "ZMW", IntakeChannel = "Manual" };
            db.AddScoped(invoice); await db.SaveChangesAsync(); return invoice.Id;
        }

        public async Task ConfineInvoiceReaderElsewhere(Guid user)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var location = new WorkLocation { Code = "OTHER-INVOICE-BRANCH", Name = "Other invoice branch", LegalEntityId = company };
            core.WorkLocations.Add(location);
            core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = user, LocationId = location.Id });
            await core.SaveChangesAsync();
        }

        public async Task<int> InvoiceHandoffCount()
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.FinanceInvoiceHandoffs.CountAsync();
        }

        public async Task<(int Active, int Total)> OrderLineCounts(Guid orderId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var rows = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == orderId).ToListAsync();
            return (rows.Count(x => x.IsActive), rows.Count);
        }

        public async Task AddOpenOrder(Guid supplierId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new PurchaseOrder
            {
                Number = "PO-TEST-" + Guid.NewGuid().ToString("N"), SupplierId = supplierId,
                CurrencyCode = "ZMW", BuyerSubjectId = Owner.ToString(), Status = "Issued",
            });
            await db.SaveChangesAsync();
        }

        public async Task AddExpiredQualification(Guid supplierId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new SupplierQualification
            {
                SupplierId = supplierId, QualificationType = "Insurance", ReviewStatus = "Verified",
                ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            });
            await db.SaveChangesAsync();
        }

        public async Task AddCurrentQualification(Guid supplierId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new SupplierQualification
            {
                SupplierId = supplierId, QualificationType = "insurance", ReviewStatus = "Verified",
                ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)),
                CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1),
            });
            await db.SaveChangesAsync();
        }

        public async Task SetDraftClassification(Guid requestId, string category, string type)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var line = await db.PurchaseRequestLines.SingleAsync(x => x.PurchaseRequestId == requestId);
            line.CategoryCode = category;
            line.PurchaseType = type;
            await db.SaveChangesAsync();
        }

        public async Task<(int Requests, int Lines, int Audit, int Outbox)> RequestPersistenceCounts()
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.PurchaseRequests.CountAsync(), await db.PurchaseRequestLines.CountAsync(),
                await db.AuditEvents.CountAsync(), await db.OutboxEvents.CountAsync());
        }

        public async Task<int> RequestAuditCount(Guid requestId, string action)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.AuditEvents.CountAsync(x => x.RecordId == requestId && x.Action == action);
        }

        public async Task AssignOwnerManagerToOther()
        {
            using var serviceScope = app.Services.CreateScope();
            var core = serviceScope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var owner = await core.Workers.SingleAsync(x => x.SubjectId == Owner.ToString());
            var manager = await core.Workers.SingleAsync(x => x.SubjectId == Other.ToString());
            owner.ManagerId = manager.Id;
            // Readiness now references the same canonical account as execution, rather
            // than treating Worker.SubjectId as an interchangeable login principal.
            if (!await core.LocalUsers.AnyAsync(x => x.Id == Other))
                core.LocalUsers.Add(new LocalUser { Id = Other, WorkerId = manager.Id, IsActive = true,
                    Email = "preview-manager@example.test", NormalizedEmail = "PREVIEW-MANAGER@EXAMPLE.TEST",
                    DisplayName = "Preview Manager", PasswordHash = "synthetic-unused", RolesCsv = "procurement_manager" });
            await core.SaveChangesAsync();
        }

        public async Task<int> VendorDetailAuditCount(Guid detailId, string action)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.AuditEvents.CountAsync(x => x.RecordId == detailId && x.Action == action);
        }

        public async Task<JsonElement> AuditPayload(Guid requestId, string action)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var json = await db.AuditEvents.Where(x => x.RecordId == requestId && x.Action == action)
                .OrderByDescending(x => x.CreatedAt).Select(x => x.ChangedFieldsJson)
                .FirstAsync();
            return JsonDocument.Parse(json!).RootElement.Clone();
        }

        public async Task<Guid> AddDraftPolicy()
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var policy = new PolicyVersion
            {
                PolicyKey = "RFQ", PolicyVersionNumber = 1,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), State = "Draft",
            };
            db.AddScoped(policy);
            await db.SaveChangesAsync();
            return policy.Id;
        }

        public async Task<Guid> AddPublishedBuyingPolicy(string? purchaseType = null)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var policy = new PolicyVersion
            {
                PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                State = "Published", PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                PublishedBy = Other.ToString(),
                RulesJson = """
                    {"schemaVersion":1,"transactionType":"PurchaseRequest","currencyCode":"ZMW",
                    "amountBasis":"EstimatedLineBase","rules":[{"code":"DEFAULT","priority":0,
                    "when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,
                    "categoryCodes":[],"purchaseTypes":[],"branchIds":[]},
                    "result":{"budgetReservation":"Required","sourcingMethodCode":null,
                    "minimumQuoteCount":null,"approvalStages":[
                    {"code":"NEED","assignee":"LineManager","mode":"One"}]}}]}
                    """,
            };
            if (purchaseType is not null)
                policy.RulesJson = policy.RulesJson.Replace("\"purchaseTypes\":[]",
                    "\"purchaseTypes\":" + JsonSerializer.Serialize(new[] { purchaseType }), StringComparison.Ordinal);
            db.AddScoped(policy);
            await db.SaveChangesAsync();
            return policy.Id;
        }

        public async Task<Guid> AddHistoricalBuyingPoliciesForDelayedCheck(Guid requestId)
        {
            var priorId = await AddPublishedBuyingPolicy();
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "delayed-policy-fixture");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await db.PurchaseRequests.SingleAsync(x => x.Id == requestId);
            request.SubmittedAt = DateTimeOffset.UtcNow.AddDays(-2);
            var prior = await db.PolicyVersions.SingleAsync(x => x.Id == priorId);
            prior.EffectiveFrom = request.SubmittedAt.Value.AddDays(-1);
            prior.PublishedAt = request.SubmittedAt.Value.AddDays(-1);
            prior.PublicationReason = "Synthetic prior approved policy";
            prior.EffectiveTo = request.SubmittedAt.Value.AddDays(1);
            db.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 2,
                EffectiveFrom = prior.EffectiveTo.Value, State = "Published", RulesJson = prior.RulesJson,
                PublishedBy = Other.ToString(), PublishedAt = request.SubmittedAt.Value });
            await db.SaveChangesAsync();
            return priorId;
        }

        public async Task AddRequestLineAfterPolicyCheck(Guid requestId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new PurchaseRequestLine
            {
                PurchaseRequestId = requestId, LineNumber = 2,
                Description = "Additional unreviewed item", UnitCode = "each",
                RequestedQuantity = 1, EstimatedUnitPrice = 1,
            });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.PurchaseRequestLines.CountAsync(x =>
                x.PurchaseRequestId == requestId && x.ApprovalStatus != "Cancelled"));
        }

        public async Task PublishPolicy(Guid id)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Other.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
            policy.State = "Published";
            await db.SaveChangesAsync();
        }

        public static async Task<Fixture> Start(bool documents = false, bool matchingSchema = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            var databaseId = Guid.NewGuid().ToString("N");
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(options =>
                options.UseInMemoryDatabase("proc-core-" + databaseId));
            builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(options =>
                options.UseInMemoryDatabase("proc-drafts-" + databaseId));
            var documentRoot = documents ? Path.Combine(Path.GetTempPath(), "erp-http-documents-" + databaseId) : null;
            if (documents)
            {
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddScoped<Mightyfin.Erp.Core.Documents.Domain.IDocumentScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
                builder.Services.AddDbContext<Mightyfin.Erp.Core.Documents.Infrastructure.DocumentDbContext>(o => o.UseInMemoryDatabase("core-docs-" + databaseId));
                builder.Services.AddScoped<Mightyfin.Erp.Core.Documents.Application.IDocumentRecords, Mightyfin.Erp.Core.Documents.Infrastructure.EfDocumentRecords>();
                builder.Services.AddScoped<Mightyfin.Erp.Core.Documents.Application.IDocumentOwnerAccess, ProcurementInvoiceDocumentAccess>();
                builder.Services.AddSingleton<Mightyfin.Erp.Core.Documents.Application.IDocumentBlobStore>(new Mightyfin.Erp.Core.Documents.Infrastructure.PrivateDocumentStore(documentRoot!));
                builder.Services.AddSingleton(new DocumentTestScanner());
                builder.Services.AddSingleton<Mightyfin.Erp.Core.Documents.Application.IDocumentScanner>(sp => sp.GetRequiredService<DocumentTestScanner>());
                builder.Services.AddSingleton(new Mightyfin.Erp.Core.Documents.Application.DocumentLimits());
                builder.Services.AddScoped<Mightyfin.Erp.Core.Documents.Application.RecordDocumentService>();
                builder.Services.AddSingleton<IDocumentSchemaReadiness>(new DocumentTestReadiness());
            }
            if (matchingSchema)
                builder.Services.AddSingleton<IProcurementInvoiceMatchingSchemaReadiness>(new InvoiceReadinessSchema());
            builder.Services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementRequestRoutes.Map(app);
            ProcurementBudgetRoutes.Map(app);
            ProcurementPolicyEvaluationRoutes.Map(app);
            ProcurementApprovalReadinessRoutes.Map(app);
            ProcurementVendorRoutes.MapRoutes(app);
            ProcurementItemEndpoints.MapRoutes(app);
            ProcurementPlanRoutes.MapRoutes(app);
            ProcurementSourcingDraftRoutes.MapRoutes(app);
            ProcurementContractDraftRoutes.MapRoutes(app);
            ProcurementInvoiceIntakeRoutes.MapRoutes(app);
            if (documents) ProcurementInvoiceDocumentRoutes.MapRoutes(app);
            ProcurementOrderDraftRoutes.MapRoutes(app);
            ProcurementConfigurationRoutes.MapRoutes(app);
            ProcurementPolicyDraftRoutes.MapRoutes(app);

            var company = new LegalEntity { Code = "A", RegisteredName = "Amizo Power", Currency = "ZMW" };
            var otherCompany = new LegalEntity { Code = "B", RegisteredName = "Other company", Currency = "USD" };
            var branch = new WorkLocation { Code = "A1", Name = "Lusaka", LegalEntityId = company.Id };
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var reviewer = Guid.NewGuid();
            var inactive = Guid.NewGuid();
            using (var seed = app.Services.CreateScope())
            {
                var core = seed.ServiceProvider.GetRequiredService<HrmDbContext>();
                core.LegalEntities.AddRange(company, otherCompany);
                core.WorkLocations.Add(branch);
                core.Workers.AddRange(
                    new Worker { EmployeeNo = "PROC-1", FirstName = "Owner", LastName = "Employee",
                        SubjectId = owner.ToString(), Status = "active", LocationId = branch.Id },
                    new Worker { EmployeeNo = "PROC-2", FirstName = "Other", LastName = "Employee",
                        SubjectId = other.ToString(), Status = "active", LocationId = branch.Id },
                    new Worker { EmployeeNo = "PROC-4", FirstName = "Reviewer", LastName = "Employee",
                        SubjectId = reviewer.ToString(), Status = "active", LocationId = branch.Id },
                    new Worker { EmployeeNo = "PROC-3", FirstName = "Inactive", LastName = "Employee",
                        SubjectId = inactive.ToString(), Status = "terminated", LocationId = branch.Id });
                await core.SaveChangesAsync();
            }
            await app.StartAsync();
            return new Fixture(app, company.Id, owner, other, reviewer, inactive, otherCompany.Id, documentRoot);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            if (documentRoot is not null && Directory.Exists(documentRoot)) Directory.Delete(documentRoot, true);
        }
    }

    private sealed class InvoiceReadinessSchema : IProcurementInvoiceMatchingSchemaReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true);
    }

    public sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var user))
                return Task.FromResult(AuthenticateResult.NoResult());
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "employee";
            var actions = Request.Headers["X-Test-Actions"].FirstOrDefault();
            var claims = new List<Claim>
            {
                new("tenant", "tenant-a"),
                new("sub", user.ToString()),
                new(ClaimTypes.Role, role),
            };
            if (actions is not null)
            {
                claims.Add(new Claim(ProcurementPermissions.ConfiguredClaim, "true"));
                claims.AddRange(actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(action => new Claim(ProcurementPermissions.ActionClaim, action)));
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims, "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        }
    }
}
