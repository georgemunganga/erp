using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Schema 2 source hold. Historical worker schema 1 bytes and fingerprints are untouched.</summary>
public sealed record RfqPrincipalActor(string PrincipalId, Guid? LinkedWorkerId, string TenantId,
    Guid CompanyId, Guid? SelectedBranchId, IReadOnlySet<Guid> Branches, bool OrganisationWide,
    IReadOnlySet<string> Permissions);

public sealed record RfqPrincipalDemandPin(Guid RequestId, Guid LineId, long RequestVersion,
    Guid SubmissionId, long SubmittedVersion, Guid CaseId, Guid OutcomeId, Guid DecisionId,
    string RequestSha, string PolicyDecisionSha, string PolicyRulesSha, Guid FinanceId, string FinanceSha,
    Guid MethodId, long MethodRevision, string MethodCode, int MinimumQuoteCount,
    string MakerPrincipalId, string RequesterPrincipalId, Guid? MakerWorkerAlias,
    Guid? RequesterWorkerAlias, string PrincipalIdentitySha256,
    Guid AllocationId, Guid EventLineId, decimal Quantity, decimal UnitEstimate, decimal EstimatedValue,
    string UnitCode, string? CategoryCode, Guid? DepartmentId);

public sealed record RfqPrincipalSelection(ApprovedPurchaseSource Source, long ExpectedRequestVersion, decimal Quantity);

public sealed record RfqPrincipalDraft(Guid Id, string TenantId, Guid CompanyId, Guid? BranchId,
    string CurrencyCode, string Title, string MakerPrincipalId, Guid? MakerWorkerAlias,
    string State, IReadOnlyList<RfqPrincipalDemandPin> Lines, string OriginalFingerprint);

public sealed record RfqPrincipalPlan(RfqPrincipalDraft Draft, IReadOnlyList<RfqAllocationInstruction> Effects);

public static class GovernedPrincipalRfqDraft
{
    public static RfqPrincipalPlan Create(RfqPrincipalActor actor, Guid eventId, Guid commandKey,
        string title, IReadOnlyList<RfqPrincipalSelection> selections, DateTimeOffset at)
    {
        RequireActor(actor,"procurement-sourcing-convert");
        title=Normalize(title);
        if(eventId==Guid.Empty || commandKey==Guid.Empty || title.Length is <1 or >200
            || selections is null || selections.Count is <1 or >200
            || selections.Any(x=>x is null || x.Source is null || x.Source.Line is null)
            || selections.Select(x=>x.Source.Line.Id).Distinct().Count()!=selections.Count)
            throw new InvalidOperationException("RfqPrincipalSelectionInvalid");
        var first=selections[0].Source.Request;
        RequireBranch(actor,first.BranchRef);
        var pins=new List<RfqPrincipalDemandPin>();
        var effects=new List<RfqAllocationInstruction>();
        foreach(var selection in selections.OrderBy(x=>x.Source.Line.Id))
        {
            var source=selection.Source;var request=source.Request;var original=source.Submission;
            RequireBranch(actor,request.BranchRef);
            if(request.BranchRef!=first.BranchRef || request.CurrencyCode!=first.CurrencyCode
                || request.Version!=selection.ExpectedRequestVersion || selection.Quantity<=0
                || !ExactDecimalJsonCodec.IsRepresentable(selection.Quantity,18,4)
                || !PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(original)
                || original.FinanceIdentitySchemaVersion!=2
                || original.RequesterPrincipalId is not { } requester || requester==Guid.Empty)
                throw new InvalidOperationException("RfqPrincipalSourceChanged");
            var available=CompetitiveSourcingSource.Check(source,actor.TenantId,actor.CompanyId,at);
            if(selection.Quantity>available.AvailableQuantity || source.Allocations.Any(x=>x.TargetType=="SourcingEvent" && x.TargetId==eventId)
                || available.MethodId!=selections[0].Source.Method.Id
                || available.MethodRevision!=selections[0].Source.Method.Version)
                throw new InvalidOperationException("RfqPrincipalSourceUnavailable");
            var value=ExactDecimalArithmetic.Multiply(selection.Quantity,available.ApprovedUnitPrice);
            var allocation=EffectId(commandKey,source.Line.Id,"allocation");
            var eventLine=EffectId(commandKey,source.Line.Id,"line");
            pins.Add(new(request.Id,source.Line.Id,request.Version,original.Id,original.SubmittedBusinessVersion,
                original.CoreApprovalCaseId,source.Outcome.Id,source.FinalDecision.Id,original.RequestSnapshotSha256,
                original.PolicyDecisionSha256,original.PolicyRulesSha256,original.FinanceSnapshotId,
                original.FinanceSnapshotSha256,available.MethodId,available.MethodRevision,source.Method.Code,
                available.MinimumQuoteCount,original.MakerSubjectId,requester.ToString("D"),
                original.MakerWorkerId,original.RequesterWorkerId,original.PrincipalIdentitySha256!,
                allocation,eventLine,selection.Quantity,available.ApprovedUnitPrice,value,source.Line.UnitCode,
                source.Line.CategoryCode,request.DepartmentRef));
            effects.Add(new("Reserve",allocation,null,request.Id,source.Line.Id,request.Version,eventId,eventLine,
                selection.Quantity,value,request.CurrencyCode,$"rfq.reserve.{commandKey:N}.{source.Line.Id:N}",
                ExactDecimalArithmetic.Add(source.Line.AllocatedQuantity,selection.Quantity)));
        }
        _=ExactDecimalArithmetic.Sum(pins.Select(x=>x.EstimatedValue));
        var draft=NewDraft(eventId,actor.TenantId,actor.CompanyId,first.BranchRef,first.CurrencyCode,title,
            actor.PrincipalId,actor.LinkedWorkerId,"Draft",pins);
        return new(draft,effects.AsReadOnly());
    }

    public static RfqPrincipalPlan Cancel(RfqPrincipalActor actor,RfqPrincipalDraft draft,Guid commandKey,
        string expectedFingerprint,string reason,IReadOnlyList<ApprovedPurchaseSource> sources)
    {
        RequireActor(actor,"procurement-sourcing-cancel");RequireBranch(actor,draft.BranchId);
        reason=Normalize(reason);
        if(actor.PrincipalId!=draft.MakerPrincipalId || actor.TenantId!=draft.TenantId
            || actor.CompanyId!=draft.CompanyId || commandKey==Guid.Empty || reason.Length is <1 or >2000
            || draft.State!="Draft" || draft.OriginalFingerprint!=expectedFingerprint
            || sources.Count!=draft.Lines.Count || sources.Select(x=>x.Line.Id).Distinct().Count()!=sources.Count)
            throw new InvalidOperationException("RfqPrincipalDraftNotCancellable");
        var effects=new List<RfqAllocationInstruction>();
        foreach(var pin in draft.Lines)
        {
            var source=sources.SingleOrDefault(x=>x.Line.Id==pin.LineId)
                ?? throw new InvalidOperationException("RfqPrincipalReleaseSourceMissing");
            if(source.Request.Id!=pin.RequestId || source.Request.BranchRef!=draft.BranchId
                || source.Request.CurrencyCode!=draft.CurrencyCode)
                throw new InvalidOperationException("RfqPrincipalReleaseSourceChanged");
            _=PurchaseDemandAllocationBalance.Evaluate(source.Request,source.Line,source.Allocations,
                source.AllocationOwners??[],actor.TenantId,actor.CompanyId);
            var holds=source.Allocations.Where(x=>x.TargetType=="SourcingEvent" && x.TargetId==draft.Id).ToArray();
            if(holds.Length!=1 || holds[0].Id!=pin.AllocationId || holds[0].Status!="Reserved"
                || holds[0].Quantity!=pin.Quantity || holds[0].EstimatedValue!=pin.EstimatedValue
                || (source.AllocationOwners??[]).Count(x=>x.TargetType=="SourcingEvent" && x.TargetId==draft.Id
                    && x.TargetLineId==pin.EventLineId && x.PurchaseRequestLineId==pin.LineId
                    && x.TenantId==draft.TenantId && x.LegalEntityId==draft.CompanyId)!=1)
                throw new InvalidOperationException("RfqPrincipalReleaseHoldChanged");
            var hold=holds[0];
            effects.Add(new("Release",hold.Id,hold.Version,pin.RequestId,pin.LineId,source.Request.Version,
                draft.Id,pin.EventLineId,hold.Quantity,hold.EstimatedValue,draft.CurrencyCode,
                $"rfq.release.{commandKey:N}.{hold.Id:N}",
                ExactDecimalArithmetic.Subtract(source.Line.AllocatedQuantity,hold.Quantity)));
        }
        return new(draft with { State="Cancelled" },effects.AsReadOnly());
    }

    public static RfqPrincipalDraft NewDraft(Guid id,string tenant,Guid company,Guid? branch,string currency,
        string title,string maker,Guid? workerAlias,string state,IReadOnlyList<RfqPrincipalDemandPin> pins)
    {
        if(!ProcurementPrincipalEvidence.Canonical(maker) || !ProcurementPrincipalEvidence.OptionalWorker(workerAlias)
            || pins.Count is <1 or >200 || pins.Any(x=>!ProcurementPrincipalEvidence.Canonical(x.MakerPrincipalId)
                || !ProcurementPrincipalEvidence.Canonical(x.RequesterPrincipalId)
                || !ProcurementPrincipalEvidence.OptionalWorker(x.MakerWorkerAlias)
                || !ProcurementPrincipalEvidence.OptionalWorker(x.RequesterWorkerAlias)
                || x.PrincipalIdentitySha256 is not { Length:64 }))
            throw new InvalidOperationException("RfqPrincipalEvidenceInvalid");
        var ordered=pins.OrderBy(x=>x.LineId).ToArray();
        return new(id,tenant,company,branch,currency,title,maker,workerAlias,state,ordered,
            Hash(new {schemaVersion=2,id,tenant,company,branch,currency,title,maker,workerAlias,
                lines=ordered.Select(x=>new {x.RequestId,x.LineId,x.RequestVersion,x.SubmissionId,x.SubmittedVersion,
                    x.CaseId,x.OutcomeId,x.DecisionId,x.RequestSha,x.PolicyDecisionSha,x.PolicyRulesSha,x.FinanceId,
                    x.FinanceSha,x.MethodId,x.MethodRevision,x.MethodCode,x.MinimumQuoteCount,x.MakerPrincipalId,
                    x.RequesterPrincipalId,x.MakerWorkerAlias,x.RequesterWorkerAlias,x.PrincipalIdentitySha256,
                    x.AllocationId,x.EventLineId,quantity=ProcurementEvidenceEncoding.Decimal(x.Quantity),
                    unitEstimate=ProcurementEvidenceEncoding.Decimal(x.UnitEstimate),estimatedValue=ProcurementEvidenceEncoding.Decimal(x.EstimatedValue),
                    x.UnitCode,x.CategoryCode,x.DepartmentId})}));
    }
    private static void RequireActor(RfqPrincipalActor actor,string permission)
    {
        if(!ProcurementPrincipalEvidence.Canonical(actor.PrincipalId) || !ProcurementPrincipalEvidence.OptionalWorker(actor.LinkedWorkerId)
            || string.IsNullOrWhiteSpace(actor.TenantId) || actor.CompanyId==Guid.Empty
            || !actor.Permissions.Contains(permission))throw new InvalidOperationException("RfqPrincipalActorDenied");
    }
    private static void RequireBranch(RfqPrincipalActor actor,Guid? branch)
    {
        if(branch==Guid.Empty || actor.SelectedBranchId==Guid.Empty
            || actor.SelectedBranchId is { } selected && selected!=branch
            || !actor.OrganisationWide && (branch is null || !actor.Branches.Contains(branch.Value)))
            throw new InvalidOperationException("RfqPrincipalBranchDenied");
    }
    private static Guid EffectId(Guid key,Guid line,string purpose)=>new(Convert.FromHexString(Hash(new {key,line,purpose})[..32]));
    private static string Hash<T>(T value)=>ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(value));
    private static string Normalize(string? value)=>string.Join(" ",(value??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
}
