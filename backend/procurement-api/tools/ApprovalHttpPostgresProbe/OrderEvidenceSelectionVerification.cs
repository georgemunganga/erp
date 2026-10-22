using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    // Expected identities come from actual owning rows/known producer references, never from
    // either position of a history array. This requires the parent's additive selection DTO.
    private async Task VerifyOrderEvidenceSelection(Guid orderId, Guid actor, Guid? expectedFinanceId, Guid? expectedIssuedId)
    {
        var detail = await Get($"{Root}/orders/{orderId}/execution-detail", actor);
        Require(detail.GetProperty("commercialEvidenceSelectionVersion").GetInt32() == 1,
            "Owner-selected commercial evidence contract is unavailable.");
        Guid? Pointer(string name) => detail.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null : detail.GetProperty(name).GetGuid();
        Require(Pointer("latestFinanceEvidenceId") == expectedFinanceId && Pointer("latestIssuedVersionId") == expectedIssuedId,
            "Owner-selected commercial evidence points to the wrong producer record.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var ownedFinance = await db.OrderFinanceEvidence.AsNoTracking().Where(x => x.PurchaseOrderId == orderId)
            .Select(x => x.Id).ToArrayAsync();
        var ownedIssued = await db.OrderIssuedVersions.AsNoTracking().Where(x => x.PurchaseOrderId == orderId)
            .Select(x => x.Id).ToArrayAsync();
        var returnedFinance = detail.GetProperty("financeEvidence").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        var returnedIssued = detail.GetProperty("issued").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Require(ownedFinance.Order().SequenceEqual(returnedFinance.Order()) && ownedIssued.Order().SequenceEqual(returnedIssued.Order())
            && (expectedFinanceId is null || ownedFinance.Contains(expectedFinanceId.Value))
            && (expectedIssuedId is null || ownedIssued.Contains(expectedIssuedId.Value)),
            "Selected commercial evidence is absent, duplicated or belongs to a different order.");
    }

    private async Task<Guid> OrderFinanceProducerId(Guid orderId, string reference)
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return await db.OrderFinanceEvidence.AsNoTracking().Where(x => x.PurchaseOrderId == orderId && x.EvidenceReference == reference)
            .Select(x => x.Id).SingleAsync();
    }
}
