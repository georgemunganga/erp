using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderDraftPersistenceTests
{
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {
        public Guid? WorkLocationId=>null;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;
        public string? CorrelationId=>null;
    }
    [Fact]
    public async Task Version_two_order_cannot_be_cancelled_or_relabelled_without_its_own_receipt()
    {
        var actor=Guid.NewGuid().ToString("D");
        var scope=new Scope("principal-po",Guid.NewGuid(),actor);
        var options=new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using(var db=new ProcurementDbContext(options,scope))
        {
            var order=new PurchaseOrder{SupplierId=Guid.NewGuid(),Number="PO-P30",
                CurrencyCode="ZMW",BuyerSubjectId=actor,BranchRef=Guid.NewGuid(),
                PrincipalDraftSchemaVersion=2};
            db.AddScoped(order);
            var sourceId=Guid.NewGuid();var requestId=Guid.NewGuid();
            var line=new PurchaseOrderLine{PurchaseOrderId=order.Id,PurchaseRequestLineId=sourceId,
                LineNumber=1,Description="Test",UnitCode="EA",Quantity=1m,UnitPrice=1m};
            db.AddScoped(line);
            var allocation=new PurchaseRequestAllocation{PurchaseRequestId=requestId,
                PurchaseRequestLineId=sourceId,TargetType="PurchaseOrder",TargetId=order.Id,
                Method="DIRECT",Status="Reserved",Quantity=1m,EstimatedValue=1m,CurrencyCode="ZMW",
                DecidedBy=actor,DecidedAt=DateTimeOffset.UtcNow,IdempotencyKey="test"};
            db.AddScoped(allocation);
            var binding=new PrincipalOrderSourceBinding{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                PurchaseOrderId=order.Id,PurchaseOrderLineId=line.Id,AllocationId=allocation.Id,
                PurchaseRequestId=requestId,PurchaseRequestLineId=sourceId,BuyerPrincipalId=actor,
                BranchId=order.BranchRef!.Value,CurrencyCode="ZMW",Quantity=1m,
                ProposedUnitPrice=1m,EstimatedValue=1m};
            order.OriginalDirectSourceSha256=PrincipalOrderDraftPins.Digest(order,[line],[binding]);
            binding.OriginalDirectSourceSha256=order.OriginalDirectSourceSha256;
            db.AddScoped(binding);
            db.AddScoped(new PrincipalOrderDraftReceipt{PurchaseOrderId=order.Id,Operation="Create",
                CommandKey=Guid.NewGuid(),ActorPrincipalId=actor,CommandSha256=new string('a',64),
                ResponseJson="{}",ResponseSha256=ProcurementEvidenceEncoding.Digest("{}"),
                ResponseStatus=201,ResultVersion=1,CompletedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        await using(var db=new ProcurementDbContext(options,scope))
        {
            var order=await db.PurchaseOrders.SingleAsync();
            order.Status="Cancelled";
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        }
        await using(var db=new ProcurementDbContext(options,scope))
        {
            var order=await db.PurchaseOrders.SingleAsync();
            Assert.Equal("Draft",order.Status);
            order.OriginalDirectSourceSha256=new string('b',64);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        }
    }
}
