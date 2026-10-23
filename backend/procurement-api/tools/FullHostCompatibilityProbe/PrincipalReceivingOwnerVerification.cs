using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task VerifyReceivingFreshOwnerAsync(PrincipalApprovalFixture fixture,PrincipalIssuedFixture goods,
        PrincipalIssuedFixture service,Guid receiptId,Guid auxiliaryId,IReadOnlyCollection<Guid> certificates,Dictionary<string,object?> result)
    {
        // Independent owner reload after actual HTTP, not the HTTP change tracker.
        // Owner access verifies persistence only; it does not stand in for finite
        // current actor authorization or prove downstream stock/Finance posting.
        await using var db=fixture.Procurement();await using var transaction=await db.Database.BeginTransactionAsync();
        var states=await db.PrincipalReceivingSourceStates.OrderBy(x=>x.Id).ToArrayAsync();
        var heads=await db.PrincipalReceivingDraftHeads.ToArrayAsync();
        var facts=await db.PrincipalReceivingFacts.ToArrayAsync();
        var receipts=await db.PrincipalReceivingCommandReceipts.ToArrayAsync();
        var revisions=await db.PrincipalReceivingDraftRevisions.ToArrayAsync();
        var bindings=await db.PrincipalReceivingDraftLineBindings.ToArrayAsync();
        Need(states.Length==2&&heads.Length==4&&facts.Length==6&&receipts.Length==12&&revisions.Length==9&&bindings.Length==9,
            "Receiving reload found duplicate, missing or unexpected owned rows.");
        var goodsState=states.Single(x=>x.PurchaseOrderLineId==goods.OrderLineId);
        var serviceState=states.Single(x=>x.PurchaseOrderLineId==service.OrderLineId);
        Need(goodsState.Sequence==4&&goodsState.AcceptedQuantity==1m&&goodsState.AcceptedNetAmount==5.1234m
            &&serviceState.Sequence==2&&serviceState.AcceptedQuantity==1m&&serviceState.AcceptedNetAmount==0.0001m,
            "Receiving source totals lost exact quantities, values or sequence.");
        Need(heads.Single(x=>x.Id==receiptId) is {State:"Recorded",Revision:3}
            &&heads.Single(x=>x.Id==auxiliaryId) is {State:"Cancelled",Revision:2}
            &&certificates.All(id=>heads.Single(x=>x.Id==id) is {State:"Certified",Revision:2})
            &&heads.All(x=>x.CreatorWorkerAliasId is null)
            &&facts.All(x=>x.ActorWorkerAliasId is null),"Receiving retained state or optional worker identity changed.");
        Need(facts.Count(x=>x.Kind=="Observation")==1&&facts.Count(x=>x.Kind=="Inspection")==1&&facts.Count(x=>x.Kind=="Acceptance")==4
            &&receipts.Select(x=>x.CommandKey).Distinct().Count()==12,"Replay/denial duplicated receiving facts or command receipts.");
        var store=new PrincipalReceivingStore(db);var goodsLedger=await store.LedgerAsync(goodsState,default);
        var serviceLedger=await store.LedgerAsync(serviceState,default);
        Need(goodsLedger.Source.Buyer.WorkerAliasId is null&&goodsLedger.Source.Requester.WorkerAliasId is null
            &&serviceLedger.Source.Buyer.WorkerAliasId is null&&serviceLedger.Source.Requester.WorkerAliasId is null
            &&goodsLedger.Observations.Single().ReceivedQuantity==2m
            &&goodsLedger.Inspections.Single() is {InspectedQuantity:2m,QualifiedQuantity:1.5m}
            &&goodsLedger.Acceptances.Sum(x=>x.Quantity)==1m&&goodsLedger.Acceptances.Sum(x=>x.NetAmount)==5.1234m
            &&serviceLedger.Acceptances.Count==2&&serviceLedger.Acceptances.All(x=>x.Quantity==0.5m&&x.NetAmount==0.00005m),
            "Full retained source/receipt ledger did not verify independent physical/service facts.");
        foreach(var source in new[]{goods,service})
        {
            await store.ReconcileCountersAsync(source.RequestLineId,default);
            var po=await db.PurchaseOrderLines.SingleAsync(x=>x.Id==source.OrderLineId);
            var pr=await db.PurchaseRequestLines.SingleAsync(x=>x.Id==source.RequestLineId);
            Need(po.AcceptedQuantity==1m&&pr.AcceptedQuantity==1m&&pr.AllocatedQuantity==1m,
                "Accepted counter conservation or original allocation changed on fresh owner reload.");
        }
        var events=await db.OutboxEvents.Where(x=>x.AggregateType=="PrincipalReceiving").ToArrayAsync();
        Need(events.Length==12&&events.Count(x=>x.EventType=="procurement.fulfilment.accepted.v2")==4,
            "Receiving handoff declarations were duplicated or missing.");
        foreach(var message in events)
        {
            using var json=JsonDocument.Parse(message.PayloadJson);
            Need(!json.RootElement.GetProperty("financePostingPerformed").GetBoolean()&&!json.RootElement.GetProperty("stockPostingPerformed").GetBoolean(),
                "Receiving handoff claimed a stock or Finance posting.");
        }
        Need(!await db.SupplierInvoices.AnyAsync()&&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),
            "Receiving invented invoice/AP/payment facts.");
        await using(var command=new NpgsqlCommand(PrincipalReceivingOwnershipConstraintSql.ManifestQuery,
            (NpgsqlConnection)db.Database.GetDbConnection(),(NpgsqlTransaction)transaction.GetDbTransaction()))
            Need(await command.ExecuteScalarAsync() is true,"Actual receiving exact same-parent deferred FK manifest did not match.");
        await transaction.RollbackAsync();
        await using var core=fixture.Core();
        Need(!await core.Workers.AnyAsync()&&!await core.LocalUsers.Where(x=>fixture.Subjects.Contains(x.Id)).AnyAsync(x=>x.WorkerId!=null),
            "Actual receiving proof introduced employee accounts.");
        result["principalReceiving"]=new{status="PASS_PRIVATE_P30_PRINCIPAL_RECEIVING_MANUAL_FULFILMENT_ONLY",
            goodsOrderId=goods.OrderId,serviceOrderId=service.OrderId,receiptId,auxiliaryId,serviceCertificates=certificates,
            facts=facts.Length,receipts=receipts.Length,immutableRevisions=revisions.Length,independentInspection=true,
            optionalEmployee=true,manualAcknowledgmentIsNotDelivery=true,physicalOverdeliveryRecorded=true,
            exactEightPlaceAcceptance=true,counterAndReceiptTamperDenied=true,originalGetRecovery=true,
            stockPostingPerformed=false,financePostingPerformed=false,productionVerified=false};
        result["status"]="PASS_PRIVATE_P30_PRINCIPAL_RECEIVING_MANUAL_FULFILMENT_ONLY";
    }
}
