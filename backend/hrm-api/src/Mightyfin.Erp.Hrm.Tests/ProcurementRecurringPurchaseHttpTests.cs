using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    private sealed partial class Fixture
    {
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
