using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped API behavior; InMemory is explicitly not relational isolation evidence.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    private const string CreditPath = "/api/procurement/v1/vendor-credits";
    private static async Task<JsonElement> CaptureCredit(Fixture fixture, Guid invoice, string number, string amount)
    {
        var response = await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = number, amount, reason = "Supplier corrected its document" }, "ap_processor");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task Credit_draft_crud_retains_source_exact_amount_audit_and_original_bill()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-SOURCE", [InvoiceLine("Wide source", "1", "12345678901234.5678")]);
        var billBefore = await fixture.CreditSourceSnapshot(invoice);
        var created = await CaptureCredit(fixture, invoice, " cn-001 ", "12345678901234.5678"); var id = created.GetProperty("id").GetGuid();
        Assert.Equal(1, created.GetProperty("creditCrudVersion").GetInt32());
        Assert.Equal("Draft", created.GetProperty("status").GetString()); Assert.Equal("CN-001", created.GetProperty("creditNumber").GetString());
        Assert.Equal(supplier, created.GetProperty("supplierId").GetGuid()); Assert.Equal("ZMW", created.GetProperty("currencyCode").GetString());
        Assert.Equal("12345678901234.5678", created.GetProperty("exactAmount").GetString());
        Assert.Equal(" cn-001 ", (await fixture.AuditPayload(id, "CreditIntakeCaptured")).GetProperty("after").GetProperty("rawCreditNumber").GetString());
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"{CreditPath}/{id}", fixture.Other,
            new { version = 1, draft = new { creditNumber = "Cn-002", amount = "1.2345", reason = "Correct commercial reason" }, reason = "Fix original capture" }, "ap_processor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revised = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, revised.GetProperty("version").GetInt64()); Assert.Equal("1.2345", revised.GetProperty("exactAmount").GetString());
        Assert.Equal(invoice, revised.GetProperty("supplierInvoiceId").GetGuid()); Assert.Equal("CN-002", revised.GetProperty("creditNumber").GetString());
        Assert.Equal("12345678901234.5678", (await fixture.AuditPayload(id, "CreditIntakeCorrected")).GetProperty("before").GetProperty("Amount").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var stale = await SendJson(fixture.Client, HttpMethod.Post, $"{CreditPath}/{id}/cancel", fixture.Other, new { version = 1, reason = "Old version" }, "ap_processor");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var cancel = await SendJson(fixture.Client, HttpMethod.Post, $"{CreditPath}/{id}/cancel", fixture.Other, new { version = 2, reason = "Reference withdrawn" }, "ap_processor");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var final = await cancel.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Cancelled", final.GetProperty("status").GetString());
        Assert.Equal(3, final.GetProperty("version").GetInt64()); Assert.Equal("1.2345", final.GetProperty("exactAmount").GetString());
        Assert.Empty(final.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal(billBefore, await fixture.CreditSourceSnapshot(invoice)); Assert.Equal(0, await fixture.InvoiceHandoffCount());
    }
    [Fact]
    public async Task Credit_cumulative_ceiling_counts_existing_references_and_cancel_preserves_reserved_number()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-CEILING", [InvoiceLine("Bill", "1", "10")]);
        var first = await CaptureCredit(fixture, invoice, "FIRST", "6"); var second = await CaptureCredit(fixture, invoice, "SECOND", "4");
        var before = await fixture.CreditFootprint();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = "OVER", amount = "0.0001", reason = "Exceeds captured bill gross" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Put, $"{CreditPath}/{first.GetProperty("id").GetGuid()}", fixture.Other,
            new { version = 1, draft = new { creditNumber = "FIRST", amount = "7", reason = "Too high" }, reason = "Correction" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.CreditFootprint());
        Assert.Equal(HttpStatusCode.OK, (await SendJson(fixture.Client, HttpMethod.Post, $"{CreditPath}/{second.GetProperty("id").GetGuid()}/cancel", fixture.Other,
            new { version = 1, reason = "Supplier withdrew" }, "ap_processor")).StatusCode);
        await CaptureCredit(fixture, invoice, "REPLACEMENT", "4");
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = " second ", amount = "1", reason = "Cannot reuse reserved identity" }, "ap_processor")).StatusCode);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    public async Task Credit_invalid_amounts_have_no_effects(string amount)
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-PRECISION"); var before = await fixture.CreditFootprint();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = "INVALID", amount, reason = "Invalid amount" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.CreditFootprint());
    }
    [Fact]
    public async Task Credit_read_scope_and_current_permissions_precede_mutation_or_count()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-SCOPE"); var created = await CaptureCredit(fixture, invoice, "SCOPED", "1");
        var id = created.GetProperty("id").GetGuid(); var before = await fixture.CreditFootprint();
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Put, $"{CreditPath}/{id}", fixture.Owner,
            new { version = 1, draft = new { creditNumber = "CHANGED", amount = "1", reason = "Not original maker" }, reason = "Wrong actor" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Post, $"{CreditPath}/{id}/cancel", fixture.Other,
            new { version = 1, reason = "Missing permission" }, "employee")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, $"{CreditPath}/{id}", fixture.Other,
            company: fixture.OtherCompany, role: "ap_processor")).StatusCode);
        await fixture.ConfineInvoiceActor();
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, $"{CreditPath}/{id}", fixture.Other, role: "ap_processor")).StatusCode);
        var list = await Send(fixture.Client, HttpMethod.Get, CreditPath, fixture.Other, role: "ap_processor");
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = "NEW", amount = "1", reason = "Hidden bill" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.CreditFootprint());
    }
    [Theory]
    [InlineData("historical-received")]
    [InlineData("finance-reference")]
    public async Task Credit_historical_or_finance_bound_reference_cannot_be_rewritten(string blocker)
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-FROZEN"); var created = await CaptureCredit(fixture, invoice, "LOCKED", "1");
        var id = created.GetProperty("id").GetGuid(); await fixture.SetCreditBlocker(id, blocker); var before = await fixture.CreditFootprint();
        var get = await Send(fixture.Client, HttpMethod.Get, $"{CreditPath}/{id}", fixture.Other, role: "ap_processor");
        var current = await get.Content.ReadFromJsonAsync<JsonElement>(); Assert.Empty(current.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, $"{CreditPath}/{id}", fixture.Other,
            new { version = current.GetProperty("version").GetInt64(), draft = new { creditNumber = "NEW", amount = "2", reason = "Locked" }, reason = "Locked" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, $"{CreditPath}/{id}/cancel", fixture.Other,
            new { version = current.GetProperty("version").GetInt64(), reason = "Locked" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.CreditFootprint());
    }
    [Theory]
    [InlineData("wrong-currency")]
    [InlineData("unknown-status")]
    public async Task Credit_unknown_legacy_registered_history_is_not_assumed_zero(string blocker)
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "CREDIT-UNKNOWN");
        var created = await CaptureCredit(fixture, invoice, "OLD-REFERENCE", "1");
        // Explicit owner-only corrupt legacy fixture, never a valid credit-authority producer.
        await fixture.SetCreditBlocker(created.GetProperty("id").GetGuid(), blocker);
        var before = await fixture.CreditFootprint();
        var response = await SendJson(fixture.Client, HttpMethod.Post, CreditPath, fixture.Other,
            new { supplierInvoiceId = invoice, creditNumber = "NEW-REFERENCE", amount = "1", reason = "Do not ignore inconsistent history" }, "ap_processor");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("credit-history-needs-review", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(before, await fixture.CreditFootprint());
    }
    [Fact]
    public async Task Credit_list_search_and_status_are_applied_before_pagination_and_count()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var invoice = await CaptureCrudInvoice(fixture, supplier, "FIND-ME");
        var other = await CaptureCrudInvoice(fixture, supplier, "UNRELATED");
        var first = await CaptureCredit(fixture, invoice, "MATCH-A", "1");
        await CaptureCredit(fixture, invoice, "MATCH-B", "1");
        await CaptureCredit(fixture, other, "OTHER-ONLY", "1");
        var response = await Send(fixture.Client, HttpMethod.Get, CreditPath + "?search=find-me&page=2&pageSize=1", fixture.Other, role: "ap_processor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("total").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(invoice, page.GetProperty("items")[0].GetProperty("supplierInvoiceId").GetGuid());
        Assert.Equal(1, page.GetProperty("creditCrudVersion").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await SendJson(fixture.Client, HttpMethod.Post,
            $"{CreditPath}/{first.GetProperty("id").GetGuid()}/cancel", fixture.Other,
            new { version = 1, reason = "Withdraw selected reference" }, "ap_processor")).StatusCode);
        var filtered = await Send(fixture.Client, HttpMethod.Get, CreditPath + "?status=Cancelled&search=match", fixture.Other, role: "ap_processor");
        var cancelled = await filtered.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, cancelled.GetProperty("total").GetInt32());
        Assert.Equal(first.GetProperty("id").GetGuid(), cancelled.GetProperty("items")[0].GetProperty("id").GetGuid());
        var before = await fixture.CreditFootprint();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
            CreditPath + "?status=Applied", fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
            CreditPath + "?search=" + new string('x', 161), fixture.Other, role: "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.CreditFootprint());
    }
    private sealed partial class Fixture
    {
        public async Task<string> CreditSourceSnapshot(Guid invoice)
        {
            using var serviceScope = InvoiceScope(); var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { header = await db.SupplierInvoices.SingleAsync(x => x.Id == invoice), lines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice).OrderBy(x => x.LineNumber).ToListAsync() });
        }
        public async Task<string> CreditFootprint()
        {
            using var serviceScope = InvoiceScope(); var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { credits = await db.SupplierCreditNoteRefs.OrderBy(x => x.Id).ToListAsync(),
                invoiceCount = await db.SupplierInvoices.CountAsync(), auditCount = await db.AuditEvents.CountAsync(),
                handoffs = await db.FinanceInvoiceHandoffs.CountAsync(), outbox = await db.OutboxEvents.CountAsync() });
        }
        public async Task SetCreditBlocker(Guid id, string blocker)
        {
            using var serviceScope = InvoiceScope(); var db = serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = await db.SupplierCreditNoteRefs.SingleAsync(x => x.Id == id);
            if (blocker == "historical-received") row.Status = "Received";
            else if (blocker == "wrong-currency") row.CurrencyCode = "USD";
            else if (blocker == "unknown-status") row.Status = "UnknownLegacyState";
            else row.FinanceCreditRef = "SYNTHETIC-OWNER-LEGACY-REFERENCE";
            await db.SaveChangesAsync();
        }
    }
}
