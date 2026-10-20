using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyExactInvoiceIntake(Guid supplier)
    {
        var validLine="{\"description\":\"Raw exact intake\",\"quantity\":1,\"unitPrice\":1,\"taxAmount\":0}";
        var invalid=new[] {
            validLine.Replace("\"quantity\":1","\"quantity\":1.00000000000000000000000000001"),
            validLine.Replace("\"unitPrice\":1","\"unitPrice\":\"1.00000000000000000000000000001\""),
            validLine.Replace("\"taxAmount\":0","\"taxAmount\":1e-1000"),
            "{\"description\":\"Product overflow\",\"quantity\":\"99999999999999.9999\",\"unitPrice\":\"99999999999999.9999\",\"taxAmount\":0}",
            string.Join(',',Enumerable.Repeat("{\"description\":\"Aggregate\",\"quantity\":1,\"unitPrice\":999999999999.9999,\"taxAmount\":0}",200)),
            "{\"description\":\"Gross overflow\",\"quantity\":1,\"unitPrice\":99999999999999.9999,\"taxAmount\":0.0001}",
            "{\"description\":\"Header rounding forbidden\",\"quantity\":0.0001,\"unitPrice\":0.0001,\"taxAmount\":0}" };
        foreach(var line in invalid)
        {
            var before=await ExactIntakeFootprint();
            using var denied=await RawIntake("INVALID-EXACT-"+Guid.NewGuid().ToString("N"),line);await Check(denied,HttpStatusCode.UnprocessableEntity);
            var body=await denied.Content.ReadFromJsonAsync<JsonElement>();Require(body.GetProperty("code").GetString()=="validation-failed","Exact input failure was mislabeled duplicate.");
            Require(before==await ExactIntakeFootprint(),"Invalid exact intake changed invoice/line/evidence/Finance rows.");
        }
        const string products="{\"description\":\"Eight place product one\",\"quantity\":\"0.0001\",\"unitPrice\":0.0001,\"taxAmount\":0},{\"description\":\"Eight place product two\",\"quantity\":0.0001,\"unitPrice\":\"0.9999\",\"taxAmount\":0}";
        using var created=await RawIntake("EXACT-SUM-"+Guid.NewGuid().ToString("N"),products);await Check(created,HttpStatusCode.Created);
        var captured=await created.Content.ReadFromJsonAsync<JsonElement>();var id=captured.GetProperty("id").GetGuid();
        Require(captured.GetProperty("grossAmount").GetDecimal()==.0001m,"Capture rounded exact sum of eight-place products.");
        var restored=await Get($"{Root}/invoices/{id}",OrderBuyer);
        Require(restored.GetProperty("netAmount").GetDecimal()==.0001m && restored.GetProperty("taxAmount").GetDecimal()==0
            && restored.GetProperty("grossAmount").GetDecimal()==captured.GetProperty("grossAmount").GetDecimal()
            && restored.GetProperty("lines")[0].GetProperty("unitPrice").GetDecimal()==.0001m
            && restored.GetProperty("lines")[1].GetProperty("unitPrice").GetDecimal()==.9999m,
            "Persisted original invoice disagrees with exact capture; DB coercion was not rejected.");
        var number="EXACT-MAX-"+Guid.NewGuid().ToString("N");
        const string maximum="{\"description\":\"Stored maximum\",\"quantity\":1,\"unitPrice\":\"99999999999999.9999\",\"taxAmount\":0}";
        using var max=await RawIntake(number,maximum);await Check(max,HttpStatusCode.Created);var maxCaptured=await max.Content.ReadFromJsonAsync<JsonElement>();
        var maxRestored=await Get($"{Root}/invoices/{maxCaptured.GetProperty("id").GetGuid()}",OrderBuyer);
        Require(maxCaptured.GetProperty("grossAmount").GetDecimal()==99999999999999.9999m
            && maxRestored.GetProperty("grossAmount").GetDecimal()==maxCaptured.GetProperty("grossAmount").GetDecimal(),"Maximum exact18,4 did not round-trip.");
        var beforeDuplicate=await ExactIntakeFootprint();using var duplicate=await RawIntake(number,maximum);await Check(duplicate,HttpStatusCode.Conflict);
        Require((await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()=="duplicate-invoice"
            && beforeDuplicate==await ExactIntakeFootprint(),"Actual duplicate identity lost its specific conflict or wrote evidence.");
        Console.WriteLine("PASS exact invoice intake: raw lexemes, product/aggregate/gross boundaries reject422 without writes; eight-place products sum exactly;18,4maximum original/capture DB roundtrip and actual duplicate409.");
        async Task<HttpResponseMessage> RawIntake(string invoiceNumber,string lines)
        {
            var body="{\"supplierId\":\""+supplier+"\",\"invoiceNumber\":\""+invoiceNumber+"\",\"invoiceDate\":\"2026-10-20\",\"currencyCode\":\"ZMW\",\"lines\":["+lines+"]}";
            using var request=new HttpRequestMessage(HttpMethod.Post,Root+"/invoices") { Content=new StringContent(body,Encoding.UTF8,"application/json") };
            request.Headers.Add("X-Probe-User",OrderBuyer.ToString("D"));request.Headers.Add("X-Shell-Entity",OrderCompany.ToString("D"));request.Headers.Add("X-Shell-Location",OrderBranch.ToString("D"));
            return await client.SendAsync(request);
        }
    }
    private async Task<string> ExactIntakeFootprint()
    {
        using var services=Scope(OrderBuyer);var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return JsonSerializer.Serialize(new { invoices=await db.SupplierInvoices.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            lines=await db.SupplierInvoiceLines.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            mappings=await db.InvoiceMatchMappingVersions.CountAsync(),results=await db.InvoiceMatchResults.CountAsync(),claims=await db.InvoiceMatchAllocations.CountAsync(),
            handoffs=await db.FinanceInvoiceHandoffs.CountAsync(),payments=await db.FinancePaymentStatusRefs.CountAsync(),
            commands=await db.CommercialCommandReceipts.CountAsync(),audit=await db.AuditEvents.CountAsync(),outbox=await db.OutboxEvents.CountAsync() },Json);
    }
}
