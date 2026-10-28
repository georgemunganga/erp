using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure positive schema2 decisions. Complete persisted history, current Core
/// authorization/publication proof and atomic source CAS/receipt saves remain adapter duties.
/// No legacy worker evidence, owner-consumption Boolean or fake delivery is substituted.</summary>
public static class PrincipalControlledReceiving
{
    private const decimal MaximumQuantity=99_999_999_999_999.9999m;
    public static PrincipalGoodsObservation Record(PrincipalReceivingLedger ledger,PolicyVersion policy,
        PrincipalReceivingActor actor,Guid receiptId,Guid receiptLineId,Guid commandKey,long expectedSequence,
        decimal quantity,decimal initiallyRejected,Guid siteId,string deliveryNote,string evidence,
        DateTimeOffset observedAt,DateTimeOffset at)
    {
        var selected=Guard(ledger,policy,actor,ControlledReceiving.RecordPermission,commandKey,expectedSequence,at);
        Need(ledger.Source.Commercial.Kind==ReceivingKind.Goods&&receiptId!=Guid.Empty&&receiptLineId!=Guid.Empty
            &&siteId==ledger.Source.Commercial.BranchId&&selected.Rule.SiteIds.Contains(siteId)
            &&!ledger.Observations.Any(value=>value.ReceiptLineId==receiptLineId),"PrincipalReceivingGoodsSourceRequired");
        Need(Quantity(quantity)&&quantity>0&&Quantity(initiallyRejected)&&initiallyRejected>=0&&initiallyRejected<=quantity,"PrincipalReceivingPhysicalQuantityInvalid");
        Need(observedAt>=ledger.Source.IssuedAuthorizedAt&&observedAt<=at&&!string.IsNullOrWhiteSpace(deliveryNote)&&deliveryNote.Trim().Length<=200,"PrincipalReceivingPhysicalObservationRequired");
        // Physical excess is preserved truthfully. Acceptance remains bounded separately.
        return new(Guid.CreateVersion7(),receiptId,receiptLineId,ledger.Source,siteId,quantity,initiallyRejected,
            Person(actor),deliveryNote.Trim(),Evidence(selected.Rule,evidence),policy.Id,selected.RulesSha256,
            commandKey,Next(ledger),Time(observedAt),Time(at));
    }
    public static PrincipalGoodsInspection Inspect(PrincipalReceivingLedger ledger,PolicyVersion policy,
        PrincipalReceivingActor actor,Guid observationId,Guid commandKey,long expectedSequence,
        decimal qualified,string evidence,DateTimeOffset at)
    {
        var selected=Guard(ledger,policy,actor,ControlledReceiving.InspectPermission,commandKey,expectedSequence,at);
        var observation=ledger.Observations.SingleOrDefault(value=>value.Id==observationId)
            ??throw new InvalidOperationException("PrincipalReceivingObservationRequired");
        Need(selected.Rule.InspectionRequired&&selected.Rule.InspectorPrincipalIds.Contains(actor.PrincipalId)
            &&!ledger.Inspections.Any(value=>value.ObservationId==observationId),"PrincipalReceivingInspectorRequired");
        PinPolicy(observation.PolicyVersionId,observation.PolicyRulesSha256,selected);
        Need(!selected.Rule.RequireIndependentInspector||Independent(observation.Receiver,actor),"PrincipalReceivingIndependentInspectorRequired");
        var inspected=ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity,observation.InitiallyRejectedQuantity);
        Need(inspected>0&&Quantity(qualified)&&qualified>=0&&qualified<=inspected&&at>=observation.RecordedAt,"PrincipalReceivingInspectionQuantityInvalid");
        var outcome=qualified==0?ReceivingInspectionOutcome.Rejected:qualified==inspected?ReceivingInspectionOutcome.Passed:ReceivingInspectionOutcome.PartiallyPassed;
        return new(Guid.CreateVersion7(),observationId,ledger.Source,Person(actor),inspected,qualified,outcome,
            selected.Rule.ChecklistReference!,Evidence(selected.Rule,evidence),policy.Id,selected.RulesSha256,
            commandKey,Next(ledger),Time(at));
    }
    public static PrincipalReceivingAcceptance AcceptGoods(PrincipalReceivingLedger ledger,PolicyVersion policy,
        PrincipalReceivingActor actor,Guid observationId,Guid commandKey,long expectedSequence,
        decimal quantity,string evidence,DateTimeOffset at)
    {
        var selected=Guard(ledger,policy,actor,ControlledReceiving.AcceptPermission,commandKey,expectedSequence,at);
        var observation=ledger.Observations.SingleOrDefault(value=>value.Id==observationId)
            ??throw new InvalidOperationException("PrincipalReceivingObservationRequired");
        Need(ledger.Source.Commercial.Kind==ReceivingKind.Goods&&selected.Rule.AcceptorPrincipalIds.Contains(actor.PrincipalId),"PrincipalReceivingAcceptorRequired");
        PinPolicy(observation.PolicyVersionId,observation.PolicyRulesSha256,selected);
        MakerChecker(ledger.Source,selected.Rule,actor);
        Need(selected.Rule.AllowReceiverCertification||Independent(observation.Receiver,actor),"PrincipalReceivingReceiverCannotCertify");
        var inspection=ledger.Inspections.SingleOrDefault(value=>value.ObservationId==observationId);
        Need(!selected.Rule.InspectionRequired||inspection is not null,"PrincipalReceivingInspectionRequired");
        if(inspection is not null)PinPolicy(inspection.PolicyVersionId,inspection.PolicyRulesSha256,selected);
        var qualified=selected.Rule.InspectionRequired?inspection!.QualifiedQuantity:ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity,observation.InitiallyRejectedQuantity);
        var accepted=ExactDecimalArithmetic.Sum(ledger.Acceptances.Where(value=>value.ObservationId==observationId).Select(value=>value.Quantity));
        Need(quantity>0&&Quantity(quantity)&&quantity<=ExactDecimalArithmetic.Subtract(qualified,accepted)
            &&at>=observation.RecordedAt&&(inspection is null||at>=inspection.InspectedAt),"PrincipalReceivingQualifiedCapacityExceeded");
        Capacity(ledger,quantity);
        return Acceptance(ledger,selected,actor,observation.ReceiptLineId,observation.Id,inspection?.Id,quantity,evidence,null,null,null,commandKey,at);
    }
    public static PrincipalReceivingAcceptance CertifyService(PrincipalReceivingLedger ledger,PolicyVersion policy,
        PrincipalReceivingActor actor,Guid certificateId,Guid commandKey,long expectedSequence,decimal quantity,
        string deliverable,DateOnly from,DateOnly to,string evidence,DateTimeOffset at,
        PrincipalReceivingBusinessDate businessDate)
    {
        var selected=Guard(ledger,policy,actor,ControlledReceiving.CertifyPermission,commandKey,expectedSequence,at);
        Need(ledger.Source.Commercial.Kind==ReceivingKind.Service&&selected.Rule.ServiceOwnerPrincipalIds.Contains(actor.PrincipalId)
            &&certificateId!=Guid.Empty&&!ledger.Acceptances.Any(value=>value.AcceptanceRecordId==certificateId),"PrincipalReceivingServiceOwnerRequired");
        Need(Quantity(quantity)&&quantity>0&&!string.IsNullOrWhiteSpace(deliverable)&&deliverable.Trim().Length<=200
            &&from!=default&&to>=from,"PrincipalReceivingServiceEvidenceRequired");
        BusinessDate(ledger.Source,businessDate,at,to);
        MakerChecker(ledger.Source,selected.Rule,actor);Capacity(ledger,quantity);
        return Acceptance(ledger,selected,actor,certificateId,null,null,quantity,evidence,deliverable.Trim(),from,to,commandKey,at,businessDate);
    }
    public static (decimal AcceptedQuantity,decimal AcceptedNetAmount,decimal RemainingQuantity) Balance(PrincipalReceivingLedger ledger)
    {
        VerifyHistory(ledger);var quantity=ExactDecimalArithmetic.Sum(ledger.Acceptances.Select(value=>value.Quantity));
        return (quantity,ExactDecimalArithmetic.Sum(ledger.Acceptances.Select(value=>value.NetAmount)),ExactDecimalArithmetic.Subtract(ledger.Source.Commercial.AuthorizedQuantity,quantity));
    }
    public static void VerifySource(PrincipalReceivingSource source)
    {
        Need(source is not null&&source.SchemaVersion==2&&source.Commercial is not null,"PrincipalReceivingSourceSchemaRequired");
        var p=source.Commercial;
        Need(!string.IsNullOrWhiteSpace(p.TenantId)&&p.LegalEntityId!=Guid.Empty&&p.BranchId is {} branch&&branch!=Guid.Empty
            &&p.PurchaseOrderId!=Guid.Empty&&p.IssuedVersionId!=Guid.Empty&&p.PurchaseOrderLineId!=Guid.Empty&&p.PurchaseRequestLineId!=Guid.Empty
            &&source.SourceBindingId!=Guid.Empty&&source.SubmissionId!=Guid.Empty&&source.FundingEvidenceId!=Guid.Empty&&source.CoreApprovalCaseId!=Guid.Empty&&source.SupplierAcknowledgmentId!=Guid.Empty
            &&p.IssuedRevision==1&&Sha(p.IssuedSnapshotSha256)&&Sha(p.ClassificationSha256)&&Sha(source.OriginalDirectSourceSha256)
            &&p.SourceEvidenceSha256==source.OriginalDirectSourceSha256&&!string.IsNullOrWhiteSpace(p.ClassificationReference)
            &&p.Kind is ReceivingKind.Goods or ReceivingKind.Service&&p.CurrencyCode is {Length:3}&&p.CurrencyCode.All(c=>c is >= 'A' and <= 'Z')
            &&!string.IsNullOrWhiteSpace(p.CategoryCode)&&!string.IsNullOrWhiteSpace(p.PurchaseType)&&!string.IsNullOrWhiteSpace(p.UnitCode)
            &&Quantity(p.AuthorizedQuantity)&&p.AuthorizedQuantity>0&&Quantity(p.NetUnitPrice)&&p.NetUnitPrice>=0&&p.NetPriceBasis==ControlledReceiving.NetPriceBasis
            &&PersonValid(source.Buyer)&&PersonValid(source.Requester)
            &&source.IssuedAuthorizedAt!=default&&source.SupplierAcknowledgedAt>=source.IssuedAuthorizedAt,"PrincipalReceivingSourceInvalid");
    }
    public static void VerifyHistory(PrincipalReceivingLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);VerifySource(ledger.Source);
        Need(ledger.Sequence>=0&&ledger.Observations is not null&&ledger.Inspections is not null&&ledger.Acceptances is not null&&ledger.RetainedPolicies is not null
            &&ledger.RetainedPolicies.Select(value=>value.Id).Distinct().Count()==ledger.RetainedPolicies.Count,"PrincipalReceivingHistoryRequired");
        var sequences=ledger.Observations.Select(value=>value.Sequence).Concat(ledger.Inspections.Select(value=>value.Sequence)).Concat(ledger.Acceptances.Select(value=>value.Sequence)).Order().ToArray();
        Need(sequences.LongLength==ledger.Sequence&&sequences.Select((sequence,index)=>sequence==index+1L).All(value=>value),"PrincipalReceivingHistoryIncomplete");
        Need(ledger.Observations.Select(value=>value.ReceiptLineId).Distinct().Count()==ledger.Observations.Count
            &&ledger.Observations.Select(value=>value.Id).Distinct().Count()==ledger.Observations.Count
            &&ledger.Inspections.Select(value=>value.Id).Distinct().Count()==ledger.Inspections.Count
            &&ledger.Acceptances.Select(value=>value.Id).Distinct().Count()==ledger.Acceptances.Count
            &&ledger.Observations.Select(value=>value.CommandKey).Distinct().Count()==ledger.Observations.Count
            &&ledger.Inspections.Select(value=>value.CommandKey).Distinct().Count()==ledger.Inspections.Count
            &&ledger.Acceptances.Select(value=>value.CommandKey).Distinct().Count()==ledger.Acceptances.Count,"PrincipalReceivingDuplicateFact");
        var times=EventTimes(ledger).OrderBy(value=>value.Sequence).Select(value=>value.At).ToArray();
        Need(times.Select((value,index)=>index==0||value>=times[index-1]).All(value=>value),"PrincipalReceivingHistoryChronologyInvalid");
        foreach(var observation in ledger.Observations)
        {
            _=RetainedRule(ledger,observation.PolicyVersionId,observation.PolicyRulesSha256,observation.RecordedAt);
            Need(observation.Id!=Guid.Empty&&observation.ReceiptId!=Guid.Empty&&observation.ReceiptLineId!=Guid.Empty&&observation.Source==ledger.Source
                &&observation.Source.Commercial.Kind==ReceivingKind.Goods&&observation.SiteId==ledger.Source.Commercial.BranchId
                &&Quantity(observation.ReceivedQuantity)&&observation.ReceivedQuantity>0&&Quantity(observation.InitiallyRejectedQuantity)
                &&observation.InitiallyRejectedQuantity>=0&&observation.InitiallyRejectedQuantity<=observation.ReceivedQuantity
                &&PersonValid(observation.Receiver)&&observation.PolicyVersionId!=Guid.Empty&&Sha(observation.PolicyRulesSha256)&&observation.CommandKey!=Guid.Empty
                &&!string.IsNullOrWhiteSpace(observation.DeliveryNoteReference)&&!string.IsNullOrWhiteSpace(observation.EvidenceReference)
                &&observation.ObservedAt>=ledger.Source.IssuedAuthorizedAt&&observation.RecordedAt>=observation.ObservedAt,"PrincipalReceivingPhysicalHistoryInvalid");
        }
        foreach(var inspection in ledger.Inspections)
        {
            var rule=RetainedRule(ledger,inspection.PolicyVersionId,inspection.PolicyRulesSha256,inspection.InspectedAt);
            var observed=ledger.Observations.SingleOrDefault(value=>value.Id==inspection.ObservationId);
            Need(observed is not null&&inspection.Id!=Guid.Empty&&inspection.Source==ledger.Source&&PersonValid(inspection.Inspector)
                &&inspection.CommandKey!=Guid.Empty&&inspection.PolicyVersionId==observed!.PolicyVersionId&&inspection.PolicyRulesSha256==observed.PolicyRulesSha256
                &&inspection.Sequence>observed.Sequence&&inspection.InspectedAt>=observed.RecordedAt&&Quantity(inspection.QualifiedQuantity)
                &&inspection.InspectedQuantity==ExactDecimalArithmetic.Subtract(observed.ReceivedQuantity,observed.InitiallyRejectedQuantity)
                &&inspection.QualifiedQuantity>=0&&inspection.QualifiedQuantity<=inspection.InspectedQuantity
                &&rule.InspectionRequired&&rule.InspectorPrincipalIds.Contains(inspection.Inspector.PrincipalId)
                &&(!rule.RequireIndependentInspector||Independent(observed.Receiver,inspection.Inspector))
                &&inspection.Outcome==(inspection.QualifiedQuantity==0?ReceivingInspectionOutcome.Rejected:inspection.QualifiedQuantity==inspection.InspectedQuantity?ReceivingInspectionOutcome.Passed:ReceivingInspectionOutcome.PartiallyPassed)
                &&!string.IsNullOrWhiteSpace(inspection.ChecklistReference)&&!string.IsNullOrWhiteSpace(inspection.EvidenceReference)
                &&ledger.Inspections.Count(value=>value.ObservationId==inspection.ObservationId)==1,"PrincipalReceivingInspectionHistoryInvalid");
        }
        foreach(var effect in ledger.Acceptances)
        {
            var rule=RetainedRule(ledger,effect.PolicyVersionId,effect.PolicyRulesSha256,effect.AcceptedAt);
            Need(effect.Id!=Guid.Empty&&effect.AcceptanceRecordId!=Guid.Empty&&effect.Source==ledger.Source&&PersonValid(effect.Actor)
                &&effect.PolicyVersionId!=Guid.Empty&&Sha(effect.PolicyRulesSha256)&&effect.CommandKey!=Guid.Empty&&Quantity(effect.Quantity)&&effect.Quantity>0
                &&effect.NetAmount==ExactDecimalArithmetic.Multiply(effect.Quantity,ledger.Source.Commercial.NetUnitPrice)
                &&!string.IsNullOrWhiteSpace(effect.EvidenceReference)&&effect.AcceptedAt>=ledger.Source.IssuedAuthorizedAt,"PrincipalReceivingAcceptanceHistoryInvalid");
            Need((rule.AllowBuyerCertification||Independent(ledger.Source.Buyer,effect.Actor))
                &&(rule.AllowRequesterCertification||Independent(ledger.Source.Requester,effect.Actor)),"PrincipalReceivingMakerCheckerHistoryInvalid");
            if(ledger.Source.Commercial.Kind==ReceivingKind.Goods)
            {
                var observed=ledger.Observations.SingleOrDefault(value=>value.Id==effect.ObservationId);
                var inspection=effect.InspectionId is {} id?ledger.Inspections.SingleOrDefault(value=>value.Id==id):null;
                Need(observed is not null&&effect.AcceptanceRecordId==observed!.ReceiptLineId&&effect.PolicyVersionId==observed.PolicyVersionId
                    &&effect.PolicyRulesSha256==observed.PolicyRulesSha256&&effect.Sequence>observed.Sequence&&effect.AcceptedAt>=observed.RecordedAt
                    &&effect.DeliverableReference is null&&effect.PeriodFrom is null&&effect.PeriodTo is null
                    &&effect.BusinessDateObservation is null
                    &&rule.AcceptorPrincipalIds.Contains(effect.Actor.PrincipalId)
                    &&(rule.AllowReceiverCertification||Independent(observed.Receiver,effect.Actor))
                    &&(!rule.InspectionRequired||inspection is not null)
                    &&(effect.InspectionId is null||inspection is not null&&inspection.ObservationId==observed.Id&&inspection.Sequence<effect.Sequence&&inspection.InspectedAt<=effect.AcceptedAt),"PrincipalReceivingGoodsAcceptanceHistoryInvalid");
                var qualified=inspection?.QualifiedQuantity??ExactDecimalArithmetic.Subtract(observed!.ReceivedQuantity,observed.InitiallyRejectedQuantity);
                Need(ExactDecimalArithmetic.Sum(ledger.Acceptances.Where(value=>value.ObservationId==observed!.Id).Select(value=>value.Quantity))<=qualified,"PrincipalReceivingPhysicalCapacityExceeded");
            }
            else Need(rule.ServiceOwnerPrincipalIds.Contains(effect.Actor.PrincipalId)&&effect.ObservationId is null&&effect.InspectionId is null&&!string.IsNullOrWhiteSpace(effect.DeliverableReference)
                &&effect.PeriodFrom is {} from&&from!=default&&effect.PeriodTo is {} to&&to>=from
                &&ledger.Acceptances.Count(value=>value.AcceptanceRecordId==effect.AcceptanceRecordId)==1
                &&(effect.BusinessDateObservation is {} witnessed
                    ?BusinessDateValid(ledger.Source,witnessed,effect.AcceptedAt,retained:true)&&to<=witnessed.CompanyBusinessDate
                    :to<=DateOnly.FromDateTime(effect.AcceptedAt.UtcDateTime)),"PrincipalReceivingServiceHistoryInvalid");
        }
        Need(ExactDecimalArithmetic.Sum(ledger.Acceptances.Select(value=>value.Quantity))<=ledger.Source.Commercial.AuthorizedQuantity,"PrincipalReceivingIssuedCapacityExceeded");
    }
    private static PrincipalReceivingPolicy Guard(PrincipalReceivingLedger ledger,PolicyVersion policy,PrincipalReceivingActor actor,
        string permission,Guid key,long expected,DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(actor);VerifyHistory(ledger);var p=ledger.Source.Commercial;
        Need(ProcurementPrincipalEvidence.Canonical(actor.PrincipalId)&&ProcurementPrincipalEvidence.OptionalWorker(actor.WorkerAliasId)
            &&actor.TenantId==p.TenantId&&actor.LegalEntityId==p.LegalEntityId&&actor.AllowedBranches is not null&&actor.Permissions is not null
            &&p.BranchId is {} branch&&actor.AllowedBranches.Contains(branch)&&actor.Permissions.Contains(permission),"PrincipalReceivingActorDenied");
        Need(expected==ledger.Sequence&&key!=Guid.Empty&&at>=ledger.Source.SupplierAcknowledgedAt
            &&EventTimes(ledger).All(value=>value.At<=at)
            &&!ledger.Observations.Any(value=>value.CommandKey==key)&&!ledger.Inspections.Any(value=>value.CommandKey==key)
            &&!ledger.Acceptances.Any(value=>value.CommandKey==key),"PrincipalReceivingCommandChanged");
        return PrincipalReceivingPolicyRules.Select(policy,ledger.Source,at);
    }
    private static IEnumerable<(long Sequence,DateTimeOffset At)> EventTimes(PrincipalReceivingLedger ledger)=>
        ledger.Observations.Select(value=>(value.Sequence,value.RecordedAt))
            .Concat(ledger.Inspections.Select(value=>(value.Sequence,value.InspectedAt)))
            .Concat(ledger.Acceptances.Select(value=>(value.Sequence,value.AcceptedAt)));
    private static PrincipalReceivingRule RetainedRule(PrincipalReceivingLedger ledger,Guid id,string sha,DateTimeOffset at)
    {
        var policy=ledger.RetainedPolicies.SingleOrDefault(value=>value.Id==id)
            ??throw new InvalidOperationException("PrincipalReceivingRetainedPolicyRequired");
        var retained=PrincipalReceivingPolicyRules.Retained(policy,ledger.Source,at);
        Need(retained.RulesSha256==sha,"PrincipalReceivingRetainedPolicyChanged");return retained.Rule;
    }
    private static void MakerChecker(PrincipalReceivingSource source,PrincipalReceivingRule rule,PrincipalReceivingActor actor)
        =>Need((rule.AllowBuyerCertification||Independent(source.Buyer,actor))&&(rule.AllowRequesterCertification||Independent(source.Requester,actor)),"PrincipalReceivingMakerCheckerDenied");
    private static bool Independent(PrincipalReceivingPerson original,PrincipalReceivingPerson actor)=>ProcurementPrincipalEvidence.Independent(original.PrincipalId,original.WorkerAliasId,actor.PrincipalId,actor.WorkerAliasId);
    private static bool Independent(PrincipalReceivingPerson original,PrincipalReceivingActor actor)=>ProcurementPrincipalEvidence.Independent(original.PrincipalId,original.WorkerAliasId,actor.PrincipalId,actor.WorkerAliasId);
    private static PrincipalReceivingAcceptance Acceptance(PrincipalReceivingLedger ledger,PrincipalReceivingPolicy policy,PrincipalReceivingActor actor,Guid record,Guid? observation,Guid? inspection,decimal quantity,string evidence,string? deliverable,DateOnly? from,DateOnly? to,Guid key,DateTimeOffset at,PrincipalReceivingBusinessDate? businessDate=null)
        =>new PrincipalReceivingAcceptance(Guid.CreateVersion7(),record,ledger.Source,observation,inspection,quantity,ExactDecimalArithmetic.Multiply(quantity,ledger.Source.Commercial.NetUnitPrice),Person(actor),policy.Record.Id,policy.RulesSha256,Evidence(policy.Rule,evidence),deliverable,from,to,key,Next(ledger),Time(at))
        {BusinessDateObservation=businessDate};
    private static void BusinessDate(PrincipalReceivingSource source,PrincipalReceivingBusinessDate witness,DateTimeOffset at,DateOnly to)
    {
        Need(BusinessDateValid(source,witness,at,retained:false),"PrincipalReceivingCompanyBusinessDateInvalid");
        Need(to<=witness.CompanyBusinessDate,"PrincipalReceivingServicePeriodInFuture");
    }
    private static bool BusinessDateValid(PrincipalReceivingSource source,PrincipalReceivingBusinessDate? witness,DateTimeOffset at,bool retained)
    {
        if(witness is null || witness.SchemaVersion!=1 || witness.TenantId!=source.Commercial.TenantId
            || witness.LegalEntityId!=source.Commercial.LegalEntityId || witness.CompanyBusinessDate==default
            || string.IsNullOrWhiteSpace(witness.CompanyTimeZoneId)
            || witness.CompanyTimeZoneId!=witness.CompanyTimeZoneId.Trim() || witness.CompanyTimeZoneId.Length>128
            || witness.UtcOffsetMinutes is < -840 or > 840 || witness.ObservedAtUtc==default
            || witness.ObservedAtUtc.Offset!=TimeSpan.Zero) return false;
        // PostgreSQL preserves microseconds. New commands demand the same exact
        // instant; retained facts compare that canonical instant to AcceptedAt.
        if(retained?Time(witness.ObservedAtUtc)!=at:at!=Time(at)||witness.ObservedAtUtc!=at.ToUniversalTime())return false;
        var local=witness.ObservedAtUtc.ToOffset(TimeSpan.FromMinutes(witness.UtcOffsetMinutes));
        return DateOnly.FromDateTime(local.DateTime)==witness.CompanyBusinessDate;
    }
    private static void Capacity(PrincipalReceivingLedger ledger,decimal quantity)=>Need(quantity<=ExactDecimalArithmetic.Subtract(ledger.Source.Commercial.AuthorizedQuantity,ExactDecimalArithmetic.Sum(ledger.Acceptances.Select(value=>value.Quantity))),"PrincipalReceivingIssuedCapacityExceeded");
    private static void PinPolicy(Guid id,string sha,PrincipalReceivingPolicy current)=>Need(current.Record.Id==id&&current.RulesSha256==sha,"PrincipalReceivingOriginalPolicyChanged");
    private static PrincipalReceivingPerson Person(PrincipalReceivingActor actor)=>new(actor.PrincipalId,actor.WorkerAliasId);
    private static bool PersonValid(PrincipalReceivingPerson? value)=>value is not null&&ProcurementPrincipalEvidence.Canonical(value.PrincipalId)&&ProcurementPrincipalEvidence.OptionalWorker(value.WorkerAliasId);
    private static string Evidence(PrincipalReceivingRule rule,string value){Need(!string.IsNullOrWhiteSpace(value)&&value.Trim().Length<=500&&value.Trim().StartsWith(rule.EvidenceReferencePrefix,StringComparison.Ordinal),"PrincipalReceivingControlledEvidenceRequired");return value.Trim();}
    private static bool Quantity(decimal value)=>value<=MaximumQuantity&&value>=-MaximumQuantity&&decimal.Round(value,4)==value;
    private static bool Sha(string? value)=>value is {Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static DateTimeOffset Time(DateTimeOffset value)=>ProcurementEvidenceEncoding.DatabaseTimestamp(value)!.Value;
    private static long Next(PrincipalReceivingLedger ledger)=>checked(ledger.Sequence+1);
    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool okay,string code){if(!okay)throw new InvalidOperationException(code);}
}
