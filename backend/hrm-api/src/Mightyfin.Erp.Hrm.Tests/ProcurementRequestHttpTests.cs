using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/sourcing/{sourceId}",
                fixture.Other, role: "procurement_buyer")).StatusCode);
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
        Assert.Single(revised.GetProperty("lines").EnumerateArray(),
            line => line.GetProperty("status").GetString() == "Cancelled");
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/plans/{id}",
                fixture.Owner, new ProcurementPlanRoutes.UpdatePlan(originalVersion, revisedInput),
                "manager")).StatusCode);
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
    public async Task Policy_drafts_are_scoped_versioned_and_cannot_publish_through_draft_api()
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
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{policies}/{id}/publish", fixture.Other,
                new { reason = "Not supported" }, "procurement_manager")).StatusCode);
    }

    [Fact]
    public async Task Policy_rule_validation_reports_field_issues_without_publishing()
    {
        await using var fixture = await Fixture.Start();
        const string path = "/api/procurement/v1/configuration/policies/validate";
        var rules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW",
            amountBasis = "EstimatedGross",
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
            new ProcurementPolicyDraftRoutes.PolicyValidationRequest(rules.Replace("EstimatedGross", "Net", StringComparison.Ordinal)),
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
    public async Task Policy_review_submission_requires_author_current_version_and_company_rules()
    {
        await using var fixture = await Fixture.Start();
        const string basePath = "/api/procurement/v1/configuration/policies";
        var rules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW",
            amountBasis = "EstimatedGross",
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
        Assert.Equal(1, await fixture.RequestAuditCount(entry.GetProperty("id").GetGuid(), "Published"));
        var catalog = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/catalog", fixture.Owner);
        Assert.Equal(1, (await catalog.Content.ReadFromJsonAsync<JsonElement>())
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
                    catalogEntryId = (Guid?)entry.GetProperty("id").GetGuid(), categoryCode = "OFFICE" },
                    new { description = "Office supplies", unitCode = "each", requestedQuantity = 1,
                        estimatedUnitPrice = 25, catalogItemId = (Guid?)null, preferredSupplierId = (Guid?)null,
                        catalogEntryId = (Guid?)null, categoryCode = "OFFICE" } },
            }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var request = await requestResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(entry.GetProperty("id").GetGuid(), request.GetProperty("lines")[0]
            .GetProperty("catalogEntryId").GetGuid());
        Assert.Equal(1, request.GetProperty("lines")[0].GetProperty("catalogPublishedVersion").GetInt32());
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
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put,
                $"/api/procurement/v1/contracts/{id}", fixture.Owner,
                new ProcurementContractDraftRoutes.VersionedContractDraft(
                    contract.GetProperty("version").GetInt64(), input), "procurement_buyer")).StatusCode);
        var cancelled = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/contracts/{id}/cancel", fixture.Owner,
            new ProcurementContractDraftRoutes.ContractCancel(
                revised.GetProperty("version").GetInt64(), "Agreement not needed"),
            "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledContract = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", cancelledContract.GetProperty("status").GetString());
        Assert.Equal("Cancelled", cancelledContract.GetProperty("draftVersion")
            .GetProperty("status").GetString());
    }

    private static ProcurementRequestRoutes.NewRequest Draft(string purpose) => new(
        purpose, null, null, null, null, null, null, null, null,
        [new ProcurementRequestRoutes.RequestLineInput(null, "Laptop", "each", 2, 100,
            null, null, null, "Goods", null, null, null, null, null, null)]);

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        Guid user, Guid? company = null, string? role = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-User", user.ToString());
        if (company.HasValue) request.Headers.Add("X-Shell-Entity", company.Value.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJson<T>(HttpClient client, HttpMethod method, string path,
        Guid user, T body, string? role = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Test-User", user.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        return client.SendAsync(request);
    }

    private sealed class Fixture(WebApplication app, Guid company, Guid owner, Guid other, Guid inactive, Guid otherCompany)
        : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public Guid Owner { get; } = owner;
        public Guid Other { get; } = other;
        public Guid Inactive { get; } = inactive;
        public Guid OtherCompany { get; } = otherCompany;

        public async Task<int> DecisionCount(Guid supplierId)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.SupplierDecisions.CountAsync(x => x.SupplierId == supplierId);
        }

        public async Task<int> RequestAuditCount(Guid requestId, string action)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.AuditEvents.CountAsync(x => x.RecordId == requestId && x.Action == action);
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

        public static async Task<Fixture> Start()
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
            ProcurementVendorRoutes.MapRoutes(app);
            ProcurementItemEndpoints.MapRoutes(app);
            ProcurementPlanRoutes.MapRoutes(app);
            ProcurementSourcingDraftRoutes.MapRoutes(app);
            ProcurementContractDraftRoutes.MapRoutes(app);
            ProcurementConfigurationRoutes.MapRoutes(app);
            ProcurementPolicyDraftRoutes.MapRoutes(app);

            var company = new LegalEntity { Code = "A", RegisteredName = "Amizo Power", Currency = "ZMW" };
            var otherCompany = new LegalEntity { Code = "B", RegisteredName = "Other company", Currency = "USD" };
            var branch = new WorkLocation { Code = "A1", Name = "Lusaka", LegalEntityId = company.Id };
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
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
                    new Worker { EmployeeNo = "PROC-3", FirstName = "Inactive", LastName = "Employee",
                        SubjectId = inactive.ToString(), Status = "terminated", LocationId = branch.Id });
                await core.SaveChangesAsync();
            }
            await app.StartAsync();
            return new Fixture(app, company.Id, owner, other, inactive, otherCompany.Id);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
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
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant", "tenant-a"),
                new Claim("sub", user.ToString()),
                new Claim(ClaimTypes.Role, role),
            ], "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        }
    }
}
