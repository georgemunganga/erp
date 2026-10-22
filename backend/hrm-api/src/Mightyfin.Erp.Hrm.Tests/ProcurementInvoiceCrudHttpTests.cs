using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string InvoicePath = "/api/procurement/v1/invoices";
    private static object InvoiceDraft(Guid supplier, string number, object[]? lines = null) => new
    {
        supplierId = supplier, invoiceNumber = number, invoiceDate = "2026-10-22", currencyCode = "ZMW",
        lines = lines ?? [InvoiceLine("Original", "2", "12.3456", "1.2345")],
    };
    private static object InvoiceLine(string description, string quantity, string price, string tax = "0")
        => new { description, quantity, unitPrice = price, taxAmount = tax, taxCodeRef = "VAT", costCenterRef = "OPS" };
    private static async Task<JsonElement> InvoiceDetail(Fixture fixture, Guid id)
    {
        var response = await Send(fixture.Client, HttpMethod.Get, $"{InvoicePath}/{id}", fixture.Other, role: "ap_processor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<Guid> CaptureCrudInvoice(Fixture fixture, Guid supplier, string number, object[]? lines = null)
    {
        var response = await SendJson(fixture.Client, HttpMethod.Post, InvoicePath, fixture.Other,
            InvoiceDraft(supplier, number, lines), "ap_processor");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Invoice_crud_preserves_exact_values_retired_lines_and_cancel_audit()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        var supplier = await fixture.AddInvoiceSupplier();
        var id = await CaptureCrudInvoice(fixture, supplier, "CRUD-EXACT", [InvoiceLine("First", "2", "12.3456", "1.2345"), InvoiceLine("Removed", "1", "3")]);
        var original = await InvoiceDetail(fixture, id);
        Assert.Equal(1, original.GetProperty("intakeCrudVersion").GetInt32());
        Assert.Equal(new[] { "Edit", "Cancel" }, original.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("12.3456", original.GetProperty("lines")[0].GetProperty("exactAmounts").GetProperty("unitPrice").GetString());
        var update = await SendJson(fixture.Client, HttpMethod.Put, $"{InvoicePath}/{id}", fixture.Other,
            new { version = 1, draft = InvoiceDraft(supplier, "CRUD-EXACT", [InvoiceLine("Corrected", "1", "12345678901234.5678")]), reason = "Correct original capture" }, "ap_processor");
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var current = await InvoiceDetail(fixture, id);
        Assert.Equal(2, current.GetProperty("version").GetInt64());
        Assert.Single(current.GetProperty("lines").EnumerateArray());
        Assert.Equal("12345678901234.5678", current.GetProperty("exactAmounts").GetProperty("grossAmount").GetString());
        Assert.Equal((1, 2), await fixture.InvoiceLineCounts(id));
        var audit = await fixture.AuditPayload(id, "InvoiceIntakeCorrected");
        Assert.Equal("Removed", audit.GetProperty("before").GetProperty("lines")[1].GetProperty("Description").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, $"{InvoicePath}/{id}/cancel", fixture.Other,
            new { version = 1, reason = "Stale cancellation" }, "ap_processor")).StatusCode);
        var cancelled = await SendJson(fixture.Client, HttpMethod.Post, $"{InvoicePath}/{id}/cancel", fixture.Other,
            new { version = 2, reason = "No longer payable" }, "ap_processor");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var final = await InvoiceDetail(fixture, id);
        Assert.Equal("Cancelled", final.GetProperty("status").GetString());
        Assert.Equal(3, final.GetProperty("version").GetInt64());
        Assert.Empty(final.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal("12345678901234.5678", final.GetProperty("exactAmounts").GetProperty("grossAmount").GetString());
        Assert.Equal("No longer payable", (await fixture.AuditPayload(id, "InvoiceIntakeCancelled")).GetProperty("after").GetProperty("reason").GetString());
        Assert.Equal(0, await fixture.InvoiceHandoffCount());
    }

    [Theory]
    [InlineData("0.00001", "1", "0")]
    [InlineData("1", "100000000000000", "0")]
    [InlineData("0.0001", "0.0001", "0")]
    [InlineData("1", "1", "0.00001")]
    public async Task Invoice_crud_rejects_nonrepresentable_inputs_before_writes(string quantity, string price, string tax)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        var supplier = await fixture.AddInvoiceSupplier();
        var id = await CaptureCrudInvoice(fixture, supplier, "CRUD-PRECISION");
        var before = await fixture.InvoiceFootprint(id);
        var draft = InvoiceDraft(supplier, "CRUD-PRECISION", [InvoiceLine("Invalid", quantity, price, tax)]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Put, $"{InvoicePath}/{id}", fixture.Other,
            new { version = 1, draft, reason = "Invalid amount" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceFootprint(id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Post, InvoicePath, fixture.Other,
            InvoiceDraft(supplier, "INVALID-CAPTURE", [InvoiceLine("Invalid", quantity, price, tax)]), "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceFootprint(id));
    }

    [Theory]
    [InlineData("document")]
    [InlineData("posting")]
    [InlineData("matched")]
    [InlineData("mapped")]
    [InlineData("channel")]
    public async Task Invoice_crud_never_changes_processed_originals(string blocker)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        var supplier = await fixture.AddInvoiceSupplier();
        var id = await fixture.SeedProcessedInvoiceOriginal(supplier, blocker); var before = await fixture.InvoiceFootprint(id);
        var detail = await InvoiceDetail(fixture, id); Assert.Empty(detail.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, $"{InvoicePath}/{id}", fixture.Other,
            new { version = detail.GetProperty("version").GetInt64(), draft = InvoiceDraft(supplier, "CHANGED"), reason = "Must not edit evidence" }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, $"{InvoicePath}/{id}/cancel", fixture.Other,
            new { version = detail.GetProperty("version").GetInt64(), reason = "Must not cancel evidence" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceFootprint(id));
    }

    [Fact]
    public async Task Invoice_crud_requires_maker_and_current_permissions_and_hides_unknown_legacy_branch()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        var supplier = await fixture.AddInvoiceSupplier();
        var id = await CaptureCrudInvoice(fixture, supplier, "CRUD-SCOPE"); var path = $"{InvoicePath}/{id}";
        var before = await fixture.InvoiceFootprint(id);
        await fixture.ConfigureCoreRole(fixture.Owner, "ap_processor");
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft = InvoiceDraft(supplier, "NO"), reason = "Other author" }, "ap_processor")).StatusCode);
        await fixture.ConfigureCoreRole(fixture.Other, "employee");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Post, path + "/cancel", fixture.Other,
            new { version = 1, reason = "No permission" }, "employee")).StatusCode);
        await fixture.RestrictCoreAccountToCompany(fixture.Other);
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, company: fixture.OtherCompany, role: "ap_processor")).StatusCode);
        await fixture.ConfineInvoiceActor();
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "ap_processor")).StatusCode);
        var list = await Send(fixture.Client, HttpMethod.Get, InvoicePath, fixture.Other, role: "ap_processor");
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Post, path + "/cancel", fixture.Other,
            new { version = 1, reason = "Unknown site" }, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceFootprint(id));
    }

    [Fact]
    public async Task Invoice_crud_defaults_currency_and_retains_selected_branch_and_duplicate_identity()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Other, "ap_processor");
        var supplier = await fixture.AddInvoiceSupplier();
        await fixture.ConfineInvoiceActor();
        var draft = new { supplierId = supplier, invoiceNumber = "BRANCH-DEFAULT", invoiceDate = "2026-10-22",
            lines = new[] { InvoiceLine("Default currency", "1", "2") } };
        var captured = await SendJson(fixture.Client, HttpMethod.Post, InvoicePath, fixture.Other, draft, "ap_processor");
        Assert.Equal(HttpStatusCode.Created, captured.StatusCode);
        var row = await captured.Content.ReadFromJsonAsync<JsonElement>(); var id = row.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, row.GetProperty("branchRef").GetGuid()); Assert.Equal("ZMW", row.GetProperty("currencyCode").GetString());
        var original = await InvoiceDetail(fixture, id);
        var before = await fixture.InvoiceFootprint(id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(fixture.Client, HttpMethod.Post, $"{InvoicePath}/{id}/cancel", fixture.Other,
            new { version = 1, reason = new string('x', 1001) }, "ap_processor")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, InvoicePath, fixture.Other, draft, "ap_processor")).StatusCode);
        Assert.Equal(before, await fixture.InvoiceFootprint(id));
        var updated = await SendJson(fixture.Client, HttpMethod.Put, $"{InvoicePath}/{id}", fixture.Other,
            new { version = 1, draft = InvoiceDraft(supplier, "BRANCH-DEFAULT"), reason = "Correct quantity" }, "ap_processor");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(original.GetProperty("branchRef").GetGuid(), (await InvoiceDetail(fixture, id)).GetProperty("branchRef").GetGuid());
    }

    private sealed partial class Fixture
    {
        private IServiceScope InvoiceScope()
        {
            var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a", company, Other.ToString(), null, null, false, "invoice-crud-test");
            return serviceScope;
        }
        public async Task<(int Active, int Total)> InvoiceLineCounts(Guid id)
        {
            using var scope = InvoiceScope(); var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.SupplierInvoiceLines.CountAsync(x => x.SupplierInvoiceId == id && x.IsActive), await db.SupplierInvoiceLines.CountAsync(x => x.SupplierInvoiceId == id));
        }
        public async Task<string> InvoiceFootprint(Guid id)
        {
            using var scope = InvoiceScope(); var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { invoice = await db.SupplierInvoices.SingleAsync(x => x.Id == id),
                lines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.LineNumber).ToListAsync(),
                auditCount = await db.AuditEvents.CountAsync(), invoiceCount = await db.SupplierInvoices.CountAsync(), lineCount = await db.SupplierInvoiceLines.CountAsync(), handoffs = await db.FinanceInvoiceHandoffs.CountAsync() });
        }
        // Owner-only synthetic processed-original fixture. Its blocker is present at initial
        // insertion; never mutate an HTTP-captured immutable original to prepare a denial test.
        public async Task<Guid> SeedProcessedInvoiceOriginal(Guid supplier, string blocker)
        {
            using var scope = InvoiceScope(); var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var invoice = new SupplierInvoice { SupplierId = supplier, InvoiceNumber = "CRUD-LOCKED",
                NormalizedInvoiceNumber = "CRUD-LOCKED", InvoiceDate = new DateOnly(2026, 10, 22),
                IntakeChannel = "Manual", Status = "Received", CurrencyCode = "ZMW",
                NetAmount = 24.6912m, TaxAmount = 1.2345m, GrossAmount = 25.9257m };
            var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 1,
                Description = "Processed original", Quantity = 2m, UnitPrice = 12.3456m, TaxAmount = 1.2345m };
            switch (blocker)
            {
                case "document": invoice.EvidenceDocumentRefId = Guid.NewGuid(); break;
                case "posting": invoice.FinancePostingRef = "EXTERNAL-POSTING"; break;
                case "channel": invoice.IntakeChannel = "Electronic"; break;
                case "mapped": line.PurchaseOrderLineId = Guid.NewGuid(); break;
                case "matched": db.AddScoped(new InvoiceMatchResult { SupplierInvoiceId = invoice.Id, MatchType = "TwoWay", Outcome = "Review", MatchedAt = DateTimeOffset.UtcNow }); break;
                default: throw new ArgumentOutOfRangeException(nameof(blocker));
            }
            db.AddScoped(invoice); db.AddScoped(line);
            await db.SaveChangesAsync();
            return invoice.Id;
        }
        public async Task ConfineInvoiceActor()
        {
            using var scope = InvoiceScope(); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var branch = await core.WorkLocations.SingleAsync(x => x.LegalEntityId == company);
            if (!await core.UserBranchAssignments.AnyAsync(x => x.UserId == Other && x.LocationId == branch.Id))
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = Other, LocationId = branch.Id });
            await core.SaveChangesAsync();
        }
    }
}
