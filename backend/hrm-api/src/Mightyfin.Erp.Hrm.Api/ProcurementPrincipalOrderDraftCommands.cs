using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static Task<IResult> Create(CreateInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.Draft is null || input.Sources is null || input.Draft.Lines is null
                || input.Sources.Count!=input.Draft.Lines.Count || input.Sources.Count is <1 or >100
                || input.Sources.Select(x=>x.DraftLineIndex).Distinct().Count()!=input.Sources.Count
                || input.Sources.Any(x=>x.DraftLineIndex<0||x.DraftLineIndex>=input.Draft.Lines.Count
                    || x.PurchaseRequestId==Guid.Empty || x.PurchaseRequestLineId==Guid.Empty || x.ExpectedRequestVersion<1)
                || input.Sources.Select(x=>x.PurchaseRequestLineId).Distinct().Count()!=input.Sources.Count
                || input.Draft.Lines.Any(x=>x.Id is not null))throw new Problem("OrderSourceSelectionInvalid",422);
            var actor=await Actor(core,scope,http,auth,"procurement-po-convert",null,false,token);
            // The command fingerprint covers the submitted bytes before a mutable Core
            // company currency default is resolved. A historical same-key result remains
            // recoverable if that default later changes.
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Create",actor=actor.PrincipalId,draft=input.Draft,
                sources=input.Sources.OrderBy(x=>x.DraftLineIndex).ToArray()});
            if(await Replay(db,core,scope,http,auth,"procurement-po-convert","Create",key,fingerprint,null,token) is { } prior)
                return prior;
            var draft=input.Draft with{CurrencyCode=string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
                ?await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core,scope,token):input.Draft.CurrencyCode};
            if(ProcurementOrderDraftRoutes.Validate(draft) is not null)throw new Problem("OrderDraftInvalid",422);
            foreach(var requestId in input.Sources.Select(x=>x.PurchaseRequestId).Distinct().Order())
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,token);
            var order=new PurchaseOrder{Number="PO-"+Guid.CreateVersion7().ToString("N"),
                PrincipalDraftSchemaVersion=2,SupplierId=draft.SupplierId,CurrencyCode=draft.CurrencyCode!.Trim().ToUpperInvariant(),
                BuyerSubjectId=actor.PrincipalId,Status="Draft",ShipToRef=Clean(draft.ShipToRef),
                BillToRef=Clean(draft.BillToRef),PaymentTermRef=Clean(draft.PaymentTermRef),
                ExpectedDeliveryOn=draft.ExpectedDeliveryOn};
            db.AddScoped(order);
            var supplier=await db.Suppliers.Include(x=>x.Qualifications).SingleOrDefaultAsync(x=>x.Id==order.SupplierId,token);
            var now=DateTimeOffset.UtcNow;
            if(supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier,now))
                throw new Problem("OrderSupplierUnavailable",409);
            var lines=new List<PurchaseOrderLine>();var bindings=new List<PrincipalOrderSourceBinding>();
            foreach(var selected in input.Sources.OrderBy(x=>x.DraftLineIndex))
            {
                var source=await Source(db,cases,core,scope,http,auth,"procurement-po-convert",
                    selected.PurchaseRequestId,selected.PurchaseRequestLineId,token);
                if(source.Request.BranchRef is null || source.Request.BranchRef==Guid.Empty
                    || source.Submission.SnapshotSchemaVersion!=3 || source.Submission.FinanceIdentitySchemaVersion!=2
                    || source.Submission.RequesterPrincipalId is null || source.Submission.RequesterPrincipalId==Guid.Empty
                    || source.Submission.PrincipalIdentitySha256 is not {Length:64})
                    throw new Problem("OrderPrincipalSourceRequired",409);
                if(source.Request.Version!=selected.ExpectedRequestVersion)
                    throw new Problem("OrderSourceVersionChanged",409);
                if(order.BranchRef is null)order.BranchRef=source.Request.BranchRef;
                else if(order.BranchRef!=source.Request.BranchRef)throw new Problem("OrderMixedBranchesUnsupported",409);
                if(order.CurrencyCode!=source.Request.CurrencyCode)throw new Problem("OrderSourceCurrencyChanged",409);
                var finance=await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Request,source.Finance,
                    http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),core,db,scope,now,token);
                if(source.Submission.FinanceEvidenceMode!=finance.Mode
                    || source.Submission.ManualEvidenceAuthorityRef!=finance.Reference)
                    throw new Problem("OrderFinanceAuthorityChanged",409);
                if(await ProcurementCatalogClassificationBinding.StoredIssue(db,[source.Line],token) is not null)
                    throw new Problem("OrderCatalogSourceUnavailable",409);
                _=PurchaseOrderSourceAllocation.Check(source,scope.TenantId,scope.LegalEntityId,now);
                var line=ProcurementOrderDraftRoutes.NewLine(order.Id,selected.DraftLineIndex+1,draft.Lines[selected.DraftLineIndex]);
                line.PurchaseRequestLineId=source.Line.Id;
                if(await ProcurementCatalogClassificationBinding.CommercialIssue(db,order.SupplierId,order.CurrencyCode,
                    [(source.Line,line)],token) is not null)throw new Problem("OrderCatalogPriceChanged",409);
                db.AddScoped(line);
                var allocation=PurchaseOrderSourceAllocation.Reserve(source,order,line,line.Quantity,key,actor.PrincipalId,now);
                db.AddScoped(allocation);
                lines.Add(line);
                bindings.Add(new PrincipalOrderSourceBinding{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                    PurchaseOrderId=order.Id,PurchaseOrderLineId=line.Id,
                    AllocationId=allocation.Id,PurchaseRequestId=source.Request.Id,PurchaseRequestLineId=source.Line.Id,
                    RequestVersion=selected.ExpectedRequestVersion,SubmissionId=source.Submission.Id,
                    SubmittedBusinessVersion=source.Submission.SubmittedBusinessVersion,ApprovalOutcomeId=source.Outcome.Id,
                    CoreApprovalCaseId=source.CoreCase.Id,CoreDecisionId=source.FinalDecision.Id,
                    FinanceSnapshotId=source.Finance.Id,MethodId=source.Method.Id,MethodRevision=checked((int)source.Method.Version),
                    MethodCode=source.Method.Code,RequestSnapshotSha256=source.Submission.RequestSnapshotSha256,
                    PolicyDecisionSha256=source.Submission.PolicyDecisionSha256,
                    PolicyRulesSha256=source.Submission.PolicyRulesSha256,
                    FinanceSnapshotSha256=source.Submission.FinanceSnapshotSha256,
                    PrincipalIdentitySha256=source.Submission.PrincipalIdentitySha256,
                    BuyerPrincipalId=actor.PrincipalId,BuyerWorkerAliasId=actor.CoreLinkedWorkerId,
                    RequesterPrincipalId=source.Submission.RequesterPrincipalId.Value,
                    RequesterWorkerAliasId=source.Submission.RequesterWorkerId,BranchId=source.Request.BranchRef.Value,
                    Quantity=line.Quantity,UnitEstimate=source.Line.EstimatedUnitPrice,ProposedUnitPrice=line.UnitPrice,
                    EstimatedValue=allocation.EstimatedValue,
                    CurrencyCode=order.CurrencyCode});
            }
            order.OriginalDirectSourceSha256=PrincipalOrderDraftPins.Digest(order,lines,bindings);
            foreach(var binding in bindings)
            {
                binding.OriginalDirectSourceSha256=order.OriginalDirectSourceSha256;
                db.AddScoped(binding);
            }
            Record(db,scope,order,"Create",key,new{sourceLines=bindings.Select(x=>x.PurchaseRequestLineId).ToArray()});
            return await Save(db,scope,order,"Create",key,fingerprint,token);
        },ct);

    private static Task<IResult> Cancel(Guid id,CancelInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null)throw new Problem("OrderCancellationInvalid",422);
            var reason=Clean(input.Reason);
            if(input.ExpectedOrderVersion<1 || input.OriginalDirectSourceSha256 is not {Length:64}
                || reason is null || reason.Length>1000)throw new Problem("OrderCancellationInvalid",422);
            _=await Actor(core,scope,http,auth,"procurement-po-edit",null,false,token);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Cancel",id,input.ExpectedOrderVersion,
                input.OriginalDirectSourceSha256,reason});
            if(await Replay(db,core,scope,http,auth,"procurement-po-edit","Cancel",key,fingerprint,id,token) is { } prior)
                return prior;
            var initial=await Owned(db,core,scope,http,auth,"procurement-po-edit",id,token);
            var requestIds=await db.PrincipalOrderSourceBindings.AsNoTracking().Where(x=>x.PurchaseOrderId==id)
                .Select(x=>x.PurchaseRequestId).Distinct().OrderBy(x=>x).ToArrayAsync(token);
            if(requestIds.Length is <1 or >100)throw new Problem("OrderOriginalSourceUnavailable",409);
            foreach(var requestId in requestIds)await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",token);
            await db.Entry(initial).ReloadAsync(token);
            var order=await Owned(db,core,scope,http,auth,"procurement-po-edit",id,token);
            if(order.Status!="Draft" || order.Version!=input.ExpectedOrderVersion
                || order.OriginalDirectSourceSha256!=input.OriginalDirectSourceSha256
                || order.FinanceCommitmentRef is not null)throw new Problem("OrderDraftChanged",409);
            var lines=await db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==id&&x.IsActive).ToArrayAsync(token);
            var bindings=await db.PrincipalOrderSourceBindings.Where(x=>x.PurchaseOrderId==id).ToArrayAsync(token);
            if(lines.Length!=bindings.Length || bindings.Length is <1 or >100
                || !requestIds.ToHashSet().SetEquals(bindings.Select(x=>x.PurchaseRequestId)))
                throw new Problem("OrderOriginalSourceUnavailable",409);
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            var allocations=await db.PurchaseRequestAllocations.Where(x=>x.TargetType=="PurchaseOrder"&&x.TargetId==id).ToArrayAsync(token);
            if(allocations.Length!=bindings.Length)throw new Problem("OrderOriginalSourceUnavailable",409);
            foreach(var binding in bindings)
            {
                _=await Actor(core,scope,http,auth,"procurement-po-edit",binding.BranchId,true,token);
                var retained=await ApprovedDemandSourceLoader.LoadRetainedAsync(db,cases,
                    new SourceBranchScope(scope,binding.BranchId),binding.PurchaseRequestId,binding.PurchaseRequestLineId,
                    new RetainedDemandSourceIdentity(binding.SubmissionId,binding.ApprovalOutcomeId,binding.MethodId),token);
                var allocation=allocations.SingleOrDefault(x=>x.Id==binding.AllocationId);
                var source=retained.Line;var request=retained.Request;
                var submission=retained.Submission;var outcome=retained.Outcome;
                _=PurchaseDemandAllocationBalance.Evaluate(request,source,retained.Allocations,
                    retained.AllocationOwners??[],scope.TenantId,scope.LegalEntityId);
                if(allocation is null || retained.Allocations.Count(x=>x.Id==allocation.Id)!=1
                    || allocation.Status!="Reserved" || allocation.TargetId!=id || allocation.PurchaseRequestId!=request.Id
                    || allocation.PurchaseRequestLineId!=source.Id || allocation.Quantity!=binding.Quantity
                    || allocation.EstimatedValue!=binding.EstimatedValue || allocation.CurrencyCode!=binding.CurrencyCode
                    || allocation.Method!=binding.MethodCode
                    || request.BranchRef!=binding.BranchId || request.CurrencyCode!=binding.CurrencyCode
                    || source.PurchaseRequestId!=request.Id || source.AllocatedQuantity<binding.Quantity
                    || submission.PurchaseRequestId!=request.Id || submission.CoreApprovalCaseId!=binding.CoreApprovalCaseId
                    || submission.SnapshotSchemaVersion!=3 || submission.FinanceIdentitySchemaVersion!=2
                    || submission.SubmittedBusinessVersion!=binding.SubmittedBusinessVersion
                    || submission.FinanceSnapshotId!=binding.FinanceSnapshotId
                    || submission.RequesterPrincipalId!=binding.RequesterPrincipalId
                    || submission.PrincipalIdentitySha256!=binding.PrincipalIdentitySha256
                    || submission.RequestSnapshotSha256!=binding.RequestSnapshotSha256
                    || submission.PolicyDecisionSha256!=binding.PolicyDecisionSha256
                    || submission.PolicyRulesSha256!=binding.PolicyRulesSha256
                    || submission.FinanceSnapshotSha256!=binding.FinanceSnapshotSha256
                    || outcome.SubmissionId!=submission.Id || outcome.CoreDecisionId!=binding.CoreDecisionId
                    || outcome.CoreApprovalCaseId!=binding.CoreApprovalCaseId
                    || outcome.Outcome!="Approved" || retained.CoreCase.Id!=binding.CoreApprovalCaseId
                    || retained.FinalDecision.Id!=binding.CoreDecisionId
                    || retained.Method.Version!=binding.MethodRevision)
                    throw new Problem("OrderOriginalSourceUnavailable",409);
                source.AllocatedQuantity=ExactDecimalArithmetic.Subtract(source.AllocatedQuantity,allocation.Quantity);
                allocation.Status="Released";allocation.Reason=reason;
                request.UpdatedAt=DateTimeOffset.UtcNow;
            }
            order.Status="Cancelled";order.ClosureReason=reason;order.ClosedAt=DateTimeOffset.UtcNow;
            Record(db,scope,order,"Cancel",key,new{reason});
            return await Save(db,scope,order,"Cancel",key,fingerprint,token);
        },ct);
}
