using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    private sealed record LineHints(bool Record,bool Inspect,bool Accept,bool Certify,string? Blocker);
    private static async Task<object> DetailView(ProcurementDbContext db,HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,PrincipalReceivingDraftHead head,PrincipalReceivingActor actor,CancellationToken ct)
    {
        var hints=new Dictionary<Guid,LineHints>();var revision=Revision(db,head);
        foreach(var source in PrincipalReceivingDraftSnapshot.Sources(head.Kind,revision))
        {
            PrincipalReceivingPolicy? policy=null;string? blocker=null;
            try{policy=await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,source,ct);}
            catch(OperationalPolicyPublicationProblem){blocker="ReceivingCurrentPolicyUnavailable";}
            catch(InvalidOperationException error) when(!ProcurementPostgresFailures.IsStoreFailure(error))
            {blocker="ReceivingCurrentPolicyUnavailable";}
            var state=db.PrincipalReceivingSourceStates.Local.Single(x=>x.IssuedVersionId==source.Commercial.IssuedVersionId&&x.PurchaseOrderLineId==source.Commercial.PurchaseOrderLineId);
            var ledger=PrincipalReceivingStorageCodec.Ledger(state,db.PrincipalReceivingFacts.Local.Where(x=>x.SourceStateId==state.Id).ToArray(),db.PolicyVersions.Local.ToArray());
            var observation=ledger.Observations.SingleOrDefault(x=>x.ReceiptId==head.Id);
            var inspection=observation is null?null:ledger.Inspections.SingleOrDefault(x=>x.ObservationId==observation.Id);
            var bound=policy is not null&&(observation is null||observation.PolicyVersionId==policy.Record.Id&&observation.PolicyRulesSha256==policy.RulesSha256);
            async Task<bool> Independent(IEnumerable<PrincipalReceivingPerson> people)
            {try{await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,people,ct);return true;}catch(UnauthorizedAccessException){return false;}}
            var makers=new List<PrincipalReceivingPerson>();
            if(policy is not null&&!policy.Rule.AllowBuyerCertification)makers.Add(source.Buyer);
            if(policy is not null&&!policy.Rule.AllowRequesterCertification)makers.Add(source.Requester);
            var makerIndependent=await Independent(makers);
            var inspect=bound&&observation is not null&&inspection is null&&policy!.Rule.InspectionRequired
                &&policy.Rule.InspectorPrincipalIds.Contains(actor.PrincipalId)&&actor.Permissions.Contains(ControlledReceiving.InspectPermission)
                &&(!policy.Rule.RequireIndependentInspector||await Independent([observation.Receiver]));
            var physical=observation is null?0:inspection?.QualifiedQuantity??ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity,observation.InitiallyRejectedQuantity);
            var accepted=observation is null?0:ExactDecimalArithmetic.Sum(ledger.Acceptances.Where(x=>x.ObservationId==observation.Id).Select(x=>x.Quantity));
            var remaining=PrincipalControlledReceiving.Balance(ledger).RemainingQuantity;
            var accept=bound&&observation is not null&&head.State=="Recorded"&&policy!.Rule.AcceptorPrincipalIds.Contains(actor.PrincipalId)
                &&actor.Permissions.Contains(ControlledReceiving.AcceptPermission)&&makerIndependent&&remaining>0&&physical>accepted
                &&(!policy.Rule.InspectionRequired||inspection is not null)
                &&(policy.Rule.AllowReceiverCertification||await Independent([observation.Receiver]));
            var service=head.Kind=="Service"?PrincipalReceivingDraftSnapshot.Service(revision):null;
            var certify=bound&&service is not null&&head.State=="Draft"&&service.Quantity<=remaining
                &&policy!.Rule.ServiceOwnerPrincipalIds.Contains(actor.PrincipalId)&&actor.Permissions.Contains(ControlledReceiving.CertifyPermission)&&makerIndependent;
            var record=bound&&head.State=="Draft"&&head.Kind=="Goods"&&head.CreatorPrincipalId==actor.PrincipalId&&actor.Permissions.Contains(ControlledReceiving.RecordPermission);
            hints[source.Commercial.PurchaseOrderLineId]=new(record,inspect,accept,certify,blocker);
        }
        return View(db,head,actor,hints);
    }
    private static object View(ProcurementDbContext db,PrincipalReceivingDraftHead head,PrincipalReceivingActor? actor=null,
        IReadOnlyDictionary<Guid,LineHints>? hints=null)
    {
        var revision=Revision(db,head);var sources=PrincipalReceivingDraftSnapshot.Sources(head.Kind,revision);
        object Candidate(string action,bool allowed,string? blocker=null)=>new{action,candidate=allowed,blocker=allowed?null:blocker??"ReceivingCurrentPermissionOrStateRequired"};
        var own=actor?.PrincipalId==head.CreatorPrincipalId;
        var canEdit=own&&head.State=="Draft"&&actor!.Permissions.Contains("procurement-receipt-edit");
        var drafts=new[]{Candidate("EditDraft",canEdit),Candidate("CancelDraft",canEdit)};
        var ledgers=sources.ToDictionary(x=>x.Commercial.PurchaseOrderLineId,source=>
        {
            var state=db.PrincipalReceivingSourceStates.Local.Single(x=>x.IssuedVersionId==source.Commercial.IssuedVersionId&&x.PurchaseOrderLineId==source.Commercial.PurchaseOrderLineId);
            return PrincipalReceivingStorageCodec.Ledger(state,db.PrincipalReceivingFacts.Local.Where(x=>x.SourceStateId==state.Id).ToArray(),db.PolicyVersions.Local.ToArray());
        });
        var order=db.PurchaseOrders.Local.Single(x=>x.Id==head.PurchaseOrderId);
        object Labels(PrincipalReceivingSource source)
        {
            var line=db.PurchaseOrderLines.Local.Single(x=>x.Id==source.Commercial.PurchaseOrderLineId);
            return new{orderNumber=order.Number,supplierDisplayName="",description=line.Description,unitCode=source.Commercial.UnitCode,
                currencyCode=source.Commercial.CurrencyCode,branchRef=head.BranchId,issuedVersionId=head.IssuedVersionId};
        }
        object[] Effects(PrincipalReceivingLedger ledger)
        {
            var rows=new List<(long Sequence,object Value)>();var observed=ledger.Observations.Where(x=>x.ReceiptId==head.Id).ToArray();
            foreach(var x in observed)rows.Add((x.Sequence,new{id=x.Id,kind="Physical observation",quantity=Exact(x.ReceivedQuantity),
                netAmount=(string?)null,ledgerSequence=x.Sequence,evidenceReference=x.EvidenceReference,occurredAt=x.RecordedAt}));
            foreach(var x in ledger.Inspections.Where(x=>observed.Any(o=>o.Id==x.ObservationId)))rows.Add((x.Sequence,new{id=x.Id,kind="Inspection",quantity=Exact(x.QualifiedQuantity),
                netAmount=(string?)null,ledgerSequence=x.Sequence,evidenceReference=x.EvidenceReference,occurredAt=x.InspectedAt}));
            foreach(var x in ledger.Acceptances.Where(x=>head.Kind=="Service"?x.AcceptanceRecordId==head.Id:observed.Any(o=>o.Id==x.ObservationId)))
                rows.Add((x.Sequence,new{id=x.Id,kind="Acceptance",quantity=Exact(x.Quantity),netAmount=Exact(x.NetAmount),ledgerSequence=x.Sequence,evidenceReference=x.EvidenceReference,occurredAt=x.AcceptedAt}));
            return rows.OrderBy(x=>x.Sequence).Select(x=>x.Value).ToArray();
        }
        object? receipt=null,service=null;
        if(head.Kind=="Goods")
        {
            var draft=PrincipalReceivingDraftSnapshot.Goods(revision);var lines=new List<object>();var readiness=new List<object>();
            foreach(var line in draft.Lines)
            {
                var ledger=ledgers[line.Source.Commercial.PurchaseOrderLineId];var balance=PrincipalControlledReceiving.Balance(ledger);
                var observed=ledger.Observations.SingleOrDefault(x=>x.ReceiptId==head.Id&&x.ReceiptLineId==line.Id);
                var inspected=observed is null?null:ledger.Inspections.SingleOrDefault(x=>x.ObservationId==observed.Id);
                var accepted=observed is null?0:ExactDecimalArithmetic.Sum(ledger.Acceptances.Where(x=>x.ObservationId==observed.Id).Select(x=>x.Quantity));
                var rejected=inspected is null?line.RejectedQuantity:ExactDecimalArithmetic.Add(line.RejectedQuantity,ExactDecimalArithmetic.Subtract(inspected.InspectedQuantity,inspected.QualifiedQuantity));
                decimal? held=observed is null?null:ExactDecimalArithmetic.Subtract(ExactDecimalArithmetic.Subtract(observed.ReceivedQuantity,rejected),accepted);
                var hint=hints?.GetValueOrDefault(line.Source.Commercial.PurchaseOrderLineId);
                lines.Add(new{id=line.Id,version=head.Revision,purchaseOrderLineId=line.Source.Commercial.PurchaseOrderLineId,sourceVersion=ledger.Sequence,
                    receivedQuantity=Exact(line.Quantity),initiallyRejectedQuantity=Exact(line.RejectedQuantity),rejectedQuantity=Exact(rejected),acceptedQuantity=Exact(accepted),
                    unacceptedHeldQuantity=held is {} q?Exact(q):null,status=head.State,
                    inspection=inspected is null?null:new{qualifiedQuantity=Exact(inspected.QualifiedQuantity),inspectionRejectedQuantity=Exact(ExactDecimalArithmetic.Subtract(inspected.InspectedQuantity,inspected.QualifiedQuantity)),checklistReference=inspected.ChecklistReference}});
                readiness.Add(new{lineId=line.Id,sourceVersion=ledger.Sequence,labels=Labels(line.Source),
                    capacity=new{verified=true,orderRemainingQuantity=Exact(balance.RemainingQuantity),physicalRemainingQuantity=held is {} physical?Exact(physical):null,
                        acceptableQuantity=hint?.Accept==true?Exact(decimal.Min(balance.RemainingQuantity,held??0)):null,blocker=hint?.Blocker},
                    actionCandidates=new[]{Candidate("Record",hint?.Record==true,hint?.Blocker),Candidate("Inspect",hint?.Inspect==true,hint?.Blocker),Candidate("Accept",hint?.Accept==true,hint?.Blocker)},effects=Effects(ledger)});
            }
            var observations=ledgers.Values.SelectMany(x=>x.Observations).Where(x=>x.ReceiptId==head.Id).ToArray();
            receipt=new{id=head.Id,number=head.Id.ToString("D"),version=head.Revision,status=head.State,executionVersion=2,purchaseOrderId=head.PurchaseOrderId,
                issuedVersionId=head.IssuedVersionId,siteRef=head.BranchId,receivedAt=draft.ObservedAt,recordedAt=observations.Length==0?(DateTimeOffset?)null:observations.Max(x=>x.RecordedAt),
                deliveryNoteRef=draft.DeliveryNoteReference,notes=draft.Notes,physicalEvidenceRecorded=observations.Length==draft.Lines.Count,
                matchingAuthorityAvailable=false,lines,lineReadiness=readiness,draftActionCandidates=drafts,readPurpose="Receipt"};
        }
        else
        {
            var draft=PrincipalReceivingDraftSnapshot.Service(revision);var ledger=ledgers[draft.Source.Commercial.PurchaseOrderLineId];var balance=PrincipalControlledReceiving.Balance(ledger);
            var accepted=ledger.Acceptances.SingleOrDefault(x=>x.AcceptanceRecordId==head.Id);var hint=hints?.GetValueOrDefault(draft.Source.Commercial.PurchaseOrderLineId);
            service=new{id=head.Id,version=head.Revision,status=head.State,executionVersion=2,purchaseOrderLineId=draft.Source.Commercial.PurchaseOrderLineId,
                sourcePurchaseOrderId=head.PurchaseOrderId,issuedBindingId=head.IssuedVersionId,sourceVersion=ledger.Sequence,draft.PeriodFrom,draft.PeriodTo,
                deliverableRef=draft.DeliverableReference,notes=draft.Notes,cancellationReason=draft.CancellationReason,intendedQuantity=accepted is null?Exact(draft.Quantity):null,
                acceptedQuantity=Exact(accepted?.Quantity??0),acceptedNetAmount=Exact(accepted?.NetAmount??0),currentAcceptedQuantity=Exact(balance.AcceptedQuantity),currentAcceptedNetAmount=Exact(balance.AcceptedNetAmount),
                isCertifiedRecord=accepted is not null,matchingAuthorityAvailable=false,labels=Labels(draft.Source),
                capacity=new{verified=true,orderRemainingQuantity=Exact(balance.RemainingQuantity),physicalRemainingQuantity=(string?)null,acceptableQuantity=hint?.Certify==true?Exact(decimal.Min(balance.RemainingQuantity,draft.Quantity)):null,blocker=hint?.Blocker},
                actionCandidates=new[]{Candidate("Certify",hint?.Certify==true,hint?.Blocker)},draftActionCandidates=drafts,effects=Effects(ledger),readPurpose="Receipt"};
        }
        return new{id=head.Id,version=head.Revision,executionVersion=2,status=head.State,kind=head.Kind,purchaseOrderId=head.PurchaseOrderId,
            issuedVersionId=head.IssuedVersionId,siteId=head.BranchId,draft=DraftView(head,revision),receipt,service,correctionsAvailable=false,matchingAuthorityAvailable=false};
    }
}
