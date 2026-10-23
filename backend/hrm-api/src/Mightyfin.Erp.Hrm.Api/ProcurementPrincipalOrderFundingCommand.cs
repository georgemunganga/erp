using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static Task<IResult> Funding(Guid id,FundingInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1 || input.ManualCommitmentReference is null
                || input.ExternalEvidenceReference is null || input.CurrencyCode is null
                || input.SourceReservationReferences is null)throw new Problem("OrderFundingInputInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Funding",id,actor=scope.SubjectId,input});
            if(await Replay(db,core,scope,http,auth,"procurement-po-finance-confirm","Funding",key,fingerprint,id,token) is { } prior)
                return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-finance-confirm",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            if(order.Status!="Draft" || order.CurrentRevision!=0 || order.Version!=input.ExpectedOrderVersion
                || await db.PrincipalOrderFundingEvidence.AnyAsync(x=>x.PurchaseOrderId==id,token))
                throw new Problem("OrderFundingSourceChanged",409);
            await CurrentPositiveSources(db,core,scope,http,load,now,token);
            var recorder=await Actor(core,scope,http,auth,"procurement-po-finance-confirm",order.BranchRef,true,token);
            foreach(var pin in load.Bindings)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,pin.BuyerPrincipalId,
                    pin.BuyerWorkerAliasId,recorder.PrincipalId,recorder.CoreLinkedWorkerId,token)
                    || !await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                        pin.RequesterPrincipalId.ToString("D"),pin.RequesterWorkerAliasId,
                        recorder.PrincipalId,recorder.CoreLinkedWorkerId,token))
                    throw new Problem("OrderFinanceSegregationRequired",403);
            foreach(var source in load.Sources)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    source.Source.Finance.CheckedBySubjectId,source.Source.Finance.CheckedByWorkerId,
                    recorder.PrincipalId,recorder.CoreLinkedWorkerId,token))
                    throw new Problem("OrderFinanceSegregationRequired",403);
            var policy=ManualPolicy(http,scope.LegalEntityId,now);
            var commitment=Clean(input.ManualCommitmentReference);
            var external=Clean(input.ExternalEvidenceReference);
            if(commitment is null || commitment.Length>200 || external is null || external.Length>500
                || input.ObservedAt==default || input.ObservedAt>now
                || policy.EvidenceMaximumAgeMinutes is { } minutes&&input.ObservedAt<now.AddMinutes(-minutes))
                throw new Problem("OrderManualEvidenceInvalid",422);
            var expected=load.Sources.Select(x=>x.Source.Finance.ReservationRef).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
            if(expected.Any(string.IsNullOrWhiteSpace)
                || input.SourceReservationReferences.Count!=expected.Length
                || input.SourceReservationReferences.Any(string.IsNullOrWhiteSpace)
                || !expected.SequenceEqual(input.SourceReservationReferences.Order(StringComparer.Ordinal)))
                throw new Problem("OrderSourceReservationsChanged",409);
            var totals=PurchaseOrderCommercialWorkflow.Totals(load.Lines);
            if(input.CurrencyCode!=order.CurrencyCode || input.AttestedGrossAmount!=totals.Gross)
                throw new Problem("OrderFundingAmountMismatch",409);
            var commercial=PrincipalOrderCommercialSnapshot.Capture(order,load.Lines,load.Supplier,1);
            db.AddScoped(new PrincipalOrderFundingEvidence{PurchaseOrderId=id,CommercialRevision=1,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,
                CommercialSnapshotJson=commercial,CommercialSnapshotSha256=ProcurementEvidenceEncoding.Digest(commercial),
                ManualCommitmentReference=commitment,ExternalEvidenceReference=external,
                ApprovedManualPolicyReference=policy.ApprovedPolicyReference,
                SourceReservationsJson=JsonSerializer.Serialize(expected),AttestedGrossAmount=totals.Gross,
                CurrencyCode=order.CurrencyCode,RecordedByPrincipalId=recorder.PrincipalId,
                RecordedByWorkerAliasId=recorder.CoreLinkedWorkerId,
                BuyerWorkerAliasId=load.Bindings[0].BuyerWorkerAliasId,
                ObservedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(input.ObservedAt)!.Value});
            Record(db,scope,order,"Funding",key,new{manualEvidence=true,commitmentReference=commitment});
            return await Save(db,scope,order,"Funding",key,fingerprint,token);
        },ct);
    }
}
