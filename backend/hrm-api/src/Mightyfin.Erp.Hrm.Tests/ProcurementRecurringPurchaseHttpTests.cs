using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped HTTP/in-memory evidence; PostgreSQL constraints and migration are separate P23 gates.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    private const string RecurringPath = "/api/procurement/v1/recurring-purchases";

    [Fact]
    public async Task Recurring_purchase_create_update_close_is_owned_exact_and_never_posts_payable()
    {
        await using var fixture = await Fixture.Start();
        var supplier = await fixture.AddInvoiceSupplier();
        object draft(string amount, string title = "Monthly service") => new
        {
            supplierId = supplier, supplierContractId = (Guid?)null, title, frequency = "Monthly",
            firstCycleOn = "2026-11-01", lastCycleOn = "2027-10-01", nextCycleOn = "2026-11-01",
            expectedAmount = amount, currencyCode = "ZMW"
        };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            draft("12345678901234.5678"), "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal("12345678901234.5678", created.GetProperty("exactExpectedAmount").GetString());
        Assert.NotEqual(Guid.Empty, created.GetProperty("ownerWorkerId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Other,
                role: "procurement_buyer")).StatusCode);
        var ownList = await Send(fixture.Client, HttpMethod.Get, RecurringPath, fixture.Owner, role: "procurement_buyer");
        Assert.Equal(1, (await ownList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
                new { version = 1, draft = draft("4") }, "employee")).StatusCode);
        var savedResponse = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1, draft = draft("0.0001", "Revised monthly service") }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("0.0001", saved.GetProperty("exactExpectedAmount").GetString());
        Assert.Equal(2, saved.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
                new { version = 1, reason = "Stale" }, "procurement_buyer")).StatusCode);
        var closeResponse = await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
            new { version = 2, reason = "Contract ended" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
                new { version = 3, draft = draft("5") }, "procurement_buyer")).StatusCode);
        var footprint = await fixture.RecurringFootprint(id);
        Assert.Equal(1, footprint.Records);
        Assert.Equal(0, footprint.Invoices);
        Assert.Equal(0, footprint.Handoffs);
        Assert.True(footprint.Audit >= 3);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    public void Recurring_purchase_rejects_nonpositive_or_imprecise_amounts(string amount)
    {
        var input = new RecurringPurchaseCommitmentInput(Guid.NewGuid(), null, "Monthly service", "Monthly",
            new DateOnly(2026, 11, 1), null, new DateOnly(2026, 11, 1), "ZMW", decimal.Parse(amount,
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(input));
    }

    [Fact]
    public void Recurring_purchase_dates_and_frequency_are_explicit()
    {
        var valid = new RecurringPurchaseCommitmentInput(Guid.NewGuid(), null, "Annual licence", "Yearly",
            new DateOnly(2026, 11, 1), new DateOnly(2028, 11, 1), new DateOnly(2027, 11, 1), "ZMW", 100m);
        Assert.Null(RecurringPurchaseCommitmentRules.Validate(valid));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { Frequency = "EveryDay" }));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { NextCycleOn = new DateOnly(2029, 1, 1) }));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { LastCycleOn = new DateOnly(2026, 1, 1) }));
    }

    [Fact]
    public async Task Recurring_purchase_missing_draft_is_validation_error_without_mutation()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Weekly service", frequency = "Weekly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "10.0001", currencyCode = "ZMW" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var before = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = before.GetProperty("id").GetGuid(); var footprint = await fixture.RecurringFootprint(id);
        var missing = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1 }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        var body = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Equal(footprint, await fixture.RecurringFootprint(id));
        var current = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer");
        Assert.Equal(1, (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Recurring_purchase_selected_branch_company_worker_and_inactive_actor_fail_closed()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var originalBranch = await fixture.SourcingBranch();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Monthly service", frequency = "Monthly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "10.0001", currencyCode = "ZMW" },
            "procurement_buyer", selectedLocation: originalBranch);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        var makerWorker = original.GetProperty("ownerWorkerId").GetGuid();
        Assert.Equal(originalBranch, original.GetProperty("workLocationId").GetGuid());
        var otherBranch = await fixture.AddRecurringBranch();
        var branchRead = await fixture.RecurringGetAtBranch(id, otherBranch);
        Assert.Equal(HttpStatusCode.NotFound, branchRead.StatusCode);
        var branchEdit = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1, draft = new { supplierId = supplier, title = "Cross branch", frequency = "Monthly",
                firstCycleOn = "2026-11-01", nextCycleOn = "2026-11-01", expectedAmount = "11", currencyCode = "ZMW" } },
            "procurement_buyer", selectedLocation: otherBranch);
        Assert.Equal(HttpStatusCode.NotFound, branchEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner,
                company: fixture.OtherCompany, role: "procurement_buyer")).StatusCode);
        await fixture.ConfineRecurringOwner(otherBranch);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner,
                role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.RecurringGetAtBranch(id, originalBranch)).StatusCode);
        await fixture.SetFinanceWorker(fixture.Owner, fixture.Other);
        var rebound = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer");
        Assert.NotEqual(HttpStatusCode.OK, rebound.StatusCode);
        await fixture.SetFinanceWorker(fixture.Owner, fixture.Owner, active: false);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer")).StatusCode);
        var stored = await fixture.RecurringStored(id);
        Assert.Equal(makerWorker, stored.OwnerWorkerId);
        Assert.Equal(originalBranch, stored.WorkLocationId);
        Assert.Equal("Draft", stored.Status);
    }

    [Fact]
    public async Task Recurring_purchase_closed_history_remains_readable_and_currently_scoped()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Annual service", frequency = "Yearly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "50", currencyCode = "ZMW" }, "procurement_buyer");
        var original = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = original.GetProperty("id").GetGuid();
        var closed = await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
            new { version = 1, reason = "Supplier arrangement ended" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var read = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var history = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", history.GetProperty("status").GetString());
        Assert.Equal("Supplier arrangement ended", history.GetProperty("closedReason").GetString());
        Assert.Equal(2, history.GetProperty("version").GetInt64());
    }

    private sealed partial class Fixture
    {
        public async Task<Guid> AddRecurringBranch()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            var branch = new WorkLocation { Code = "RPC-OTHER", Name = "Other recurring branch", LegalEntityId = company };
            core.WorkLocations.Add(branch); await core.SaveChangesAsync(); return branch.Id;
        }
        public Task<HttpResponseMessage> RecurringGetAtBranch(Guid id, Guid branch)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{RecurringPath}/{id}");
            request.Headers.Add("X-Test-User", Owner.ToString());
            request.Headers.Add("X-Test-Role", "procurement_buyer");
            request.Headers.Add("X-Shell-Location", branch.ToString());
            return Client.SendAsync(request);
        }
        public async Task ConfineRecurringOwner(Guid branch)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment
                { UserId = Owner, LocationId = branch });
            await core.SaveChangesAsync();
        }
        public async Task<(Guid OwnerWorkerId, Guid? WorkLocationId, string Status)> RecurringStored(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-stored-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = await db.RecurringPurchaseCommitments.AsNoTracking().SingleAsync(x => x.Id == id);
            return (row.OwnerWorkerId, row.WorkLocationId, row.Status);
        }
        public async Task<(int Records, int Invoices, int Handoffs, int Audit)> RecurringFootprint(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.RecurringPurchaseCommitments.CountAsync(x => x.Id == id),
                await db.SupplierInvoices.CountAsync(), await db.FinanceInvoiceHandoffs.CountAsync(),
                await db.AuditEvents.CountAsync(x => x.RecordId == id));
        }
    }
}
