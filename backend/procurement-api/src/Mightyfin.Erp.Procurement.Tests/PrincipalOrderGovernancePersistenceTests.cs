using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderGovernancePersistenceTests
{
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {
        public Guid? WorkLocationId=>null;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;
        public string? CorrelationId=>null;
    }

    [Fact]
    public async Task Funding_evidence_requires_its_recording_actor_and_cannot_be_rewritten()
    {
        var buyer=Guid.NewGuid().ToString("D");var recorder=Guid.NewGuid().ToString("D");
        var tenant="principal-po-governance";var company=Guid.NewGuid();
        var options=new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        Guid orderId;
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,buyer)))
        {
            var order=new PurchaseOrder{Number="PO-GOVERNED",SupplierId=Guid.NewGuid(),
                CurrencyCode="ZMW",BuyerSubjectId=buyer,BranchRef=Guid.NewGuid(),
                PrincipalDraftSchemaVersion=2};
            db.AddScoped(order);orderId=order.Id;
            var sourceLine=Guid.NewGuid();var request=Guid.NewGuid();
            var line=new PurchaseOrderLine{PurchaseOrderId=order.Id,PurchaseRequestLineId=sourceLine,
                LineNumber=1,Description="Service",UnitCode="EA",Quantity=1m,UnitPrice=1m};
            db.AddScoped(line);
            var allocation=new PurchaseRequestAllocation{PurchaseRequestId=request,
                PurchaseRequestLineId=sourceLine,TargetType="PurchaseOrder",TargetId=order.Id,
                Method="DIRECT",Status="Reserved",Quantity=1m,EstimatedValue=1m,CurrencyCode="ZMW",
                DecidedBy=buyer,DecidedAt=DateTimeOffset.UtcNow,IdempotencyKey="fixture"};
            db.AddScoped(allocation);
            var binding=new PrincipalOrderSourceBinding{TenantId=tenant,LegalEntityId=company,
                PurchaseOrderId=order.Id,PurchaseOrderLineId=line.Id,AllocationId=allocation.Id,
                PurchaseRequestId=request,PurchaseRequestLineId=sourceLine,BuyerPrincipalId=buyer,
                BranchId=order.BranchRef!.Value,CurrencyCode="ZMW",Quantity=1m,
                ProposedUnitPrice=1m,EstimatedValue=1m};
            order.OriginalDirectSourceSha256=PrincipalOrderDraftPins.Digest(order,[line],[binding]);
            binding.OriginalDirectSourceSha256=order.OriginalDirectSourceSha256;
            db.AddScoped(binding);
            db.AddScoped(new PrincipalOrderDraftReceipt{PurchaseOrderId=order.Id,Operation="Create",
                CommandKey=Guid.NewGuid(),ActorPrincipalId=buyer,CommandSha256=new string('a',64),
                ResponseJson="{}",ResponseSha256=ProcurementEvidenceEncoding.Digest("{}"),
                ResponseStatus=201,ResultVersion=1,CompletedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        var snapshot="{\"schemaVersion\":2}";
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,buyer)))
        {
            var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==orderId);
            db.AddScoped(new PrincipalOrderFundingEvidence{PurchaseOrderId=orderId,CommercialRevision=1,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,CommercialSnapshotJson=snapshot,
                CommercialSnapshotSha256=ProcurementEvidenceEncoding.Digest(snapshot),
                ManualCommitmentReference="EXT-1",ExternalEvidenceReference="DOC-1",
                ApprovedManualPolicyReference="POL-1",CurrencyCode="ZMW",AttestedGrossAmount=1m,
                RecordedByPrincipalId=recorder,ObservedAt=DateTimeOffset.UtcNow});
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        }
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,recorder)))
        {
            var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==orderId);
            db.AddScoped(new PrincipalOrderFundingEvidence{PurchaseOrderId=orderId,CommercialRevision=1,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,CommercialSnapshotJson=snapshot,
                CommercialSnapshotSha256=ProcurementEvidenceEncoding.Digest(snapshot),
                ManualCommitmentReference="EXT-1",ExternalEvidenceReference="DOC-1",
                ApprovedManualPolicyReference="POL-1",CurrencyCode="ZMW",AttestedGrossAmount=1m,
                RecordedByPrincipalId=recorder,ObservedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,recorder)))
        {
            var funding=await db.PrincipalOrderFundingEvidence.SingleAsync();
            funding.CurrencyCode="USD";
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        }
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,buyer)))
        {
            var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==orderId);
            var funding=await db.PrincipalOrderFundingEvidence.SingleAsync();
            db.AddScoped(new PrincipalOrderReleaseRequest{PurchaseOrderId=orderId,
                FundingEvidenceId=funding.Id,OriginalStatus="Draft",CommercialRevision=0,
                RequestedOrderVersion=order.Version,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,
                Reason="External funding must be released",RequestedByPrincipalId=buyer,
                RequestedAt=DateTimeOffset.UtcNow});
            order.Status="ReleasePending";
            db.AddScoped(new PrincipalOrderDraftReceipt{PurchaseOrderId=orderId,Operation="ReleaseRequest",
                CommandKey=Guid.NewGuid(),ActorPrincipalId=buyer,CommandSha256=new string('b',64),
                ResponseJson="{}",ResponseSha256=ProcurementEvidenceEncoding.Digest("{}"),
                ResponseStatus=200,ResultVersion=checked(order.Version+1),CompletedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        await using(var db=new ProcurementDbContext(options,new Scope(tenant,company,recorder)))
        {
            var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==orderId);
            order.Status="Cancelled";order.ClosureReason="External release unconfirmed";
            order.ClosedAt=DateTimeOffset.UtcNow;
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        }
    }
}
