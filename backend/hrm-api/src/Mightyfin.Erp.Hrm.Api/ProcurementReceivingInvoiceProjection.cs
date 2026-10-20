using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    internal sealed record InvoiceSourceEvidence(InvoiceIssuedOrder Order, IReadOnlyList<InvoiceAcceptedFulfilment> Acceptances,
        IReadOnlyDictionary<Guid,Guid> AcceptanceSourceStates, IReadOnlyList<string> Blockers);

    /// <summary>Historical read under the shared transaction/fence and invoice caller's current record scope.
    /// Reuses existing sealed-source and full ledger reconciliation; never calls Resolve or creates receiving state.</summary>
    internal static async Task<InvoiceSourceEvidence> InvoiceEvidence(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, Guid orderId, Guid issuedId, IReadOnlyList<InvoiceMappingLine> mappings, CancellationToken ct, bool includeAcceptanceHistory = true)
    {
        try { return await InvoiceEvidenceCore(db,cases,scope,orderId,issuedId,mappings,ct,includeAcceptanceHistory); }
        catch(ReceivingProblem error) { throw new InvalidOperationException("InvoiceIssuedOrAcceptedSourceUnavailable",error); }
    }
    private static async Task<InvoiceSourceEvidence> InvoiceEvidenceCore(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, Guid orderId, Guid issuedId, IReadOnlyList<InvoiceMappingLine> mappings, CancellationToken ct, bool includeAcceptanceHistory)
    {
        var ids=await db.PurchaseOrderLines.AsNoTracking().Where(x=>x.PurchaseOrderId==orderId && x.IsActive && x.PurchaseRequestLineId!=null)
            .Select(x=>x.PurchaseRequestLineId!.Value).ToArrayAsync(ct);
        var requestIds=await db.PurchaseRequestLines.AsNoTracking().Where(x=>ids.Contains(x.Id)).Select(x=>x.PurchaseRequestId).Distinct().Order().ToArrayAsync(ct);
        foreach(var requestId in requestIds) await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={orderId} FOR UPDATE",ct);
        var currentIds=await db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==orderId && x.IsActive).Select(x=>x.Id).OrderBy(x=>x).ToArrayAsync(ct);
        if(currentIds.Length==0 || mappings.Any(x=>!currentIds.Contains(x.PurchaseOrderLineId))) throw new InvalidOperationException("InvoiceIssuedLineBindingRequired");
        var loads=new List<SourceLoad>();
        foreach(var id in currentIds) loads.Add(await Source(db,cases,scope,orderId,issuedId,id,ct,false));
        var first=loads[0];
        // Lock all related historical states in stable order before any reconciliation projection.
        var relatedLines=await db.PurchaseOrderLines.Where(x=>x.PurchaseRequestLineId!=null && ids.Contains(x.PurchaseRequestLineId.Value)).Select(x=>x.Id).ToArrayAsync(ct);
        var stateIds=await db.ReceivingSourceStates.Where(x=>relatedLines.Contains(x.PurchaseOrderLineId)).Select(x=>x.Id).OrderBy(x=>x).ToArrayAsync(ct);
        foreach(var id in stateIds) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.receiving_source_states WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",ct);
        var accepted=new List<InvoiceAcceptedFulfilment>(); var bindings=new Dictionary<Guid,Guid>(); var blockers=new HashSet<string>(StringComparer.Ordinal);
        foreach(var load in loads.Where(_=>includeAcceptanceHistory))
        {
            var state=await db.ReceivingSourceStates.SingleOrDefaultAsync(x=>x.IssuedVersionId==issuedId && x.PurchaseOrderLineId==load.Line.Id,ct);
            if(state is null)
            {
                if(!await CompleteAcceptanceHistory(db,load.Line.PurchaseRequestLineId!.Value,null,ct))
                    blockers.Add("ReceivingLegacyAcceptanceReconciliationRequired");
                if(mappings.Any(x=>x.PurchaseOrderLineId==load.Line.Id && (x.GoodsReceiptLineId!=null || x.ServiceAcceptanceId!=null))) blockers.Add("AuthoritativeAcceptedEvidenceUnavailable");
                continue;
            }
            ReceivingPersistenceProjection.VerifyState(state);
            var policy=await db.PolicyVersions.SingleAsync(x=>x.Id==state.ClassificationPolicyVersionId,ct);
            var rule=ReceivingPolicyRules.Select(ReceivingPolicyRules.Parse(policy.RulesJson),load.Category,load.PurchaseType);
            var rules=ReceivingPolicyRules.Project(policy,rule);
            using(var classification=JsonDocument.Parse(state.ClassificationJson))
            {
                var c=classification.RootElement;
                if(c.GetProperty("policyVersionId").GetGuid()!=policy.Id || c.GetProperty("policyVersionNumber").GetInt32()!=policy.PolicyVersionNumber
                    || c.GetProperty("policyRulesSha256").GetString()!=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                    || c.GetProperty("ruleCode").GetString()!=rule.Code || state.Source.Kind!=rule.Kind)
                    throw new InvalidOperationException("InvoiceAcceptedClassificationChanged");
            }
            var delivery=await db.OrderDeliveryEvidence.SingleAsync(x=>x.Id==state.DeliveryEvidenceId && x.IssuedVersionId==issuedId,ct);
            var source=new ReceivingSourceProjection(load.Issued,load.Submission,delivery,state.Source,load.Submission.BuyerWorkerId,state.ClassificationJson,true);
            ControlledReceiving.VerifySource(source);
            var ledger=await Ledger(db,new(load with { Delivery=delivery },state,source,policy,rule,rules),ct);
            if(!ledger.AcceptanceHistoryVerified) { blockers.Add("ReceivingLegacyAcceptanceReconciliationRequired"); continue; }
            // Historical exposure can point to a different receipt than the current invoice.
            // Project the full reconciled acceptance stream, not only this proposal's selected IDs.
            foreach(var id in ledger.Effects.Select(x=>x.AcceptanceRecordId).Distinct().Order())
            {
                if(accepted.Any(x=>x.Id==id)) throw new InvalidOperationException("InvoiceAcceptedIdentityAmbiguous");
                var effects=ledger.Effects.Where(x=>x.AcceptanceRecordId==id).ToArray();
                var balance=ControlledReceiving.Balance(ledger,id);
                if(balance.Reversed && !mappings.Any(x=>x.GoodsReceiptLineId==id || x.ServiceAcceptanceId==id)) continue;
                accepted.Add(new(id,state.Version,state.TenantId,state.LegalEntityId,state.Source.BranchId,orderId,issuedId,load.Line.Id,
                    state.Source.CurrencyCode,state.Source.UnitCode,state.Source.Kind==ReceivingKind.Goods ? InvoiceAcceptanceKind.Goods : InvoiceAcceptanceKind.Service,
                    "Accepted",balance.EffectsSha256,effects.Min(x=>x.OccurredAt),balance.Reversed,balance.AcceptedQuantity,balance.ReturnedQuantity,
                    balance.AcceptedNetAmount,balance.ReturnedNetAmount));
                bindings.Add(id,state.Id);
            }
        }
        return new(new(orderId,issuedId,first.Issued.RevisionNumber,first.Order.Version,first.Order.TenantId,first.Order.LegalEntityId,
            first.Order.BranchRef,first.Order.SupplierId,first.Order.CurrencyCode,"Issued",first.Issued.CommercialSnapshotSha256,
            first.Delivery.ObservedAt,loads.Select(x=>new InvoiceIssuedOrderLine(x.Line.Id,x.Line.Version,x.Category,x.Line.UnitCode,x.Line.Quantity,x.Line.UnitPrice,x.Line.TaxAmount)).ToArray()),
            accepted,bindings,blockers.Order(StringComparer.Ordinal).ToArray());
    }
}
