using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Current server-resolved Core facts. An input projection alone proves
/// neither persistence, current account activity nor an owning transaction.</summary>
public sealed record PrincipalRfqEvaluatorAssignment(string CriterionCode,string PrincipalId,Guid? WorkerAliasId);

/// <summary>Immutable proposed prepublication evidence, NOT an approved publication,
/// durable document hold, opened bid or current assignment provider. All source and
/// document records are copied to canonical bytes, never retained as mutable objects.</summary>
public sealed class PrincipalRfqPrepublicationPlan
{
    // Proposed explicit catalog key; deliberately unregistered/no bootstrap grant.
    public const string EvaluatorPermission="procurement-sourcing-evaluate";
    public const string DocumentSourceType="sourcing_commercial_plan";
    public const string DocumentOwnerType="sourcing_event";
    public Guid EventId{get;}
    public long EventVersion{get;}
    public long Revision{get;}
    public string OriginalSourceFingerprint{get;}
    public string EvaluationRulesSha256{get;}
    public string SnapshotJson{get;}
    public string SnapshotSha256{get;}
    public IReadOnlyList<PrincipalRfqEvaluatorAssignment> Assignments{get;}
    public bool CanPublish=>false;

    private PrincipalRfqPrepublicationPlan(Guid id,long eventVersion,long revision,string sourceSha,
        string evaluationSha,string json,IReadOnlyList<PrincipalRfqEvaluatorAssignment> assignments)
    {EventId=id;EventVersion=eventVersion;Revision=revision;OriginalSourceFingerprint=sourceSha;
        EvaluationRulesSha256=evaluationSha;SnapshotJson=json;SnapshotSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(json);Assignments=assignments;}

    /// <summary>Build a structural candidate from owned original RFQ envelope/bindings,
    /// current Core projections and owner-loaded document use evidence. A real adapter
    /// must additionally verify full PR/Core/ledger history, live identity/aliases,
    /// published commercial-policy binding and same-store fenced document persistence
    /// inside its enlisted transaction. No false owner-ready/verified input exists.</summary>
    public static PrincipalRfqPrepublicationPlan Prepare(SourcingEvent ev,SourcingDraftEvidence evidence,
        IReadOnlyList<SourcingDemandBinding> bindings,PrincipalRfqEvaluationPlanDefinition definition,
        PrincipalRfqPublishedPlanPolicy policy,
        IReadOnlyList<RfqPrincipalActor> currentEvaluators,IReadOnlyList<DocumentUsePin> requirements,
        long expectedEventVersion,long revision,DocumentUseReference reviewUse,
        DateTimeOffset opensAt,DateTimeOffset closesAt,DateTimeOffset at,
        string comparisonCurrency,string currencyMode,string taxMode)
    {
        ArgumentNullException.ThrowIfNull(ev);ArgumentNullException.ThrowIfNull(evidence);ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(definition);ArgumentNullException.ThrowIfNull(policy);ArgumentNullException.ThrowIfNull(currentEvaluators);
        ArgumentNullException.ThrowIfNull(requirements);ArgumentNullException.ThrowIfNull(reviewUse);
        Need(ev.Status=="Draft"&&ev.Version==expectedEventVersion&&expectedEventVersion>0&&revision>0
            &&ev.BranchRef is {} branch&&branch!=Guid.Empty&&ev.SourceEvidenceId==evidence.Id,
            "RfqPrepublicationDraftRequired");
        Need(bindings.Count is >=1 and <=200&&bindings.All(x=>x is not null),"RfqPrepublicationSourceBindingsRequired");
        var draft=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
        Need(policy.OriginalFingerprint==draft.OriginalFingerprint&&policy.ObservedAt==at,"RfqPrepublicationPolicySourceChanged");
        Need(Instant(at)&&Instant(opensAt)&&Instant(closesAt)&&at<opensAt&&opensAt<closesAt,
            "RfqPrepublicationExplicitWindowRequired");
        // Same-currency original components are deliberately the ONLY initial mode.
        // This does not convert tax-inclusive/exclusive prices or compute tax/FX.
        Need(comparisonCurrency==draft.CurrencyCode&&currencyMode=="SameCurrencyOnly"
            &&taxMode=="OriginalSeparateComponents","RfqPrepublicationNormalizationUnavailable");
        Need(reviewUse.Key!=Guid.Empty&&reviewUse.Module=="procurement"&&reviewUse.RecordType==DocumentSourceType
            &&reviewUse.RecordId==draft.Id&&reviewUse.Revision==revision&&reviewUse.Kind==DocumentUseKind.Review,
            "RfqPrepublicationDocumentUseMismatch");
        Need(requirements.Count<=100&&(!policy.RequirementsPackRequired||requirements.Count>0)&&requirements.All(x=>x is not null)
            &&requirements.Select(x=>x.VersionId).Distinct().Count()==requirements.Count,
            "RfqPrepublicationExactPackRequired");
        foreach(var pin in requirements)
        {
            Need(pin.TenantId==draft.TenantId&&pin.LegalEntityId==draft.CompanyId
                &&pin.Source==reviewUse&&pin.OwnerModule=="procurement"&&pin.OwnerType==DocumentOwnerType
                &&pin.OwnerId==draft.Id&&pin.CreatedBy==draft.MakerPrincipalId&&pin.CreatedAt<=at,
                "RfqPrepublicationDocumentScopeMismatch");
            // Reuse Core's exact retained scan/version/content validator. A clean
            // internal snapshot is still NOT evidence of a committed durable hold.
            _=DocumentUsePin.Restore(pin.Id,pin.TenantId,pin.LegalEntityId,pin.Source,
                new(pin.OwnerModule,pin.OwnerType,pin.OwnerId,pin.DocumentId,pin.VersionId,pin.DocumentRevision,pin.VersionRevision,pin.Sha256),
                pin.VersionNumber,pin.FileName,pin.MediaType,pin.SizeBytes,pin.Scan,pin.CreatedBy,pin.CreatedAt);
        }
        var expected=definition.Criteria.SelectMany(x=>x.EvaluatorPrincipalIds).Distinct(StringComparer.Ordinal).ToArray();
        Need(currentEvaluators.Count==expected.Length&&currentEvaluators.All(x=>x is not null)
            &&currentEvaluators.Select(x=>x.PrincipalId).ToHashSet(StringComparer.Ordinal).SetEquals(expected)
            &&currentEvaluators.Select(x=>x.PrincipalId).Distinct(StringComparer.Ordinal).Count()==expected.Length,
            "RfqPrepublicationExactEvaluatorsRequired");
        var aliases=currentEvaluators.Where(x=>x.LinkedWorkerId is not null).Select(x=>x.LinkedWorkerId).ToArray();
        Need(aliases.Distinct().Count()==aliases.Length,"RfqPrepublicationEvaluatorAliasConflict");
        foreach(var person in currentEvaluators)
        {
            Need(ProcurementPrincipalEvidence.Canonical(person.PrincipalId)&&ProcurementPrincipalEvidence.OptionalWorker(person.LinkedWorkerId)
                &&person.TenantId==draft.TenantId&&person.CompanyId==draft.CompanyId
                &&person.Permissions is not null&&person.Permissions.Contains(EvaluatorPermission)
                &&person.Branches is not null&&(person.SelectedBranchId is null||person.SelectedBranchId==draft.BranchId)
                &&(person.OrganisationWide||person.Branches.Contains(draft.BranchId!.Value)),
                "RfqPrepublicationEvaluatorNotCurrentOrScoped");
            Need(ProcurementPrincipalEvidence.Independent(person.PrincipalId,person.LinkedWorkerId,draft.MakerPrincipalId,draft.MakerWorkerAlias)
                &&draft.Lines.All(line=>ProcurementPrincipalEvidence.Independent(person.PrincipalId,person.LinkedWorkerId,line.MakerPrincipalId,line.MakerWorkerAlias)
                    &&ProcurementPrincipalEvidence.Independent(person.PrincipalId,person.LinkedWorkerId,line.RequesterPrincipalId,line.RequesterWorkerAlias)),
                "RfqPrepublicationEvaluatorNotIndependent");
        }
        var people=currentEvaluators.ToDictionary(x=>x.PrincipalId,StringComparer.Ordinal);
        var assignments=definition.Criteria.OrderBy(x=>x.Code,StringComparer.Ordinal)
            .SelectMany(criterion=>criterion.EvaluatorPrincipalIds.Order(StringComparer.Ordinal)
                .Select(principal=>new PrincipalRfqEvaluatorAssignment(criterion.Code,principal,people[principal].LinkedWorkerId))).ToArray();
        static string Exact(decimal value)=>value.ToString("0.############################",CultureInfo.InvariantCulture);
        var json=JsonSerializer.Serialize(new{schemaVersion=1,recordType=DocumentSourceType,eventId=draft.Id,
            draft.TenantId,draft.CompanyId,draft.BranchId,eventVersion=expectedEventVersion,revision,
            originalFingerprint=draft.OriginalFingerprint,sourceEvidenceId=evidence.Id,sourceEnvelopeSha256=evidence.PayloadSha256,
            draft.MakerPrincipalId,draft.MakerWorkerAlias,preparedAt=at,opensAt,closesAt,
            comparisonCurrency,currencyMode,taxMode,
            publishedPolicy=new{policy.PolicyId,policy.PolicyVersionNumber,policy.PolicyRecordVersion,policy.PublishedIdentityId,
                policy.PublishedIdentitySha256,policy.RulesSha256,policy.RequirementsPackRequired},
            evaluation=new{rulesSha256=definition.RulesSha256,normalizationTotal=Exact(definition.NormalizationTotal),
                criteria=definition.Criteria.Select(x=>new{x.Code,x.Phase,x.Kind,x.Mandatory,
                    maximumScore=x.MaximumScore is {} max?Exact(max):null,weight=x.Weight is {} weight?Exact(weight):null})},
            assignments,
            sources=draft.Lines.OrderBy(x=>x.EventLineId).Select(x=>new{x.RequestId,x.LineId,x.SubmissionId,x.CaseId,x.DecisionId,x.EventLineId,
                x.AllocationId,x.RequestSha,x.PolicyDecisionSha,x.PolicyRulesSha,x.FinanceId,x.FinanceSha,x.PrincipalIdentitySha256,
                quantity=Exact(x.Quantity),estimate=Exact(x.UnitEstimate),estimatedValue=Exact(x.EstimatedValue)}),
            documentReviewUse=reviewUse,
            requirements=requirements.OrderBy(x=>x.VersionId).Select(x=>new{x.Id,x.OwnerModule,x.OwnerType,x.OwnerId,x.DocumentId,x.VersionId,
                x.VersionNumber,x.DocumentRevision,x.VersionRevision,x.Sha256,x.SizeBytes,scan=x.Scan,x.CreatedBy,x.CreatedAt})});
        var result=new PrincipalRfqPrepublicationPlan(draft.Id,expectedEventVersion,revision,draft.OriginalFingerprint,
            definition.RulesSha256,json,Array.AsReadOnly(assignments));
        Need(result.SnapshotSha256.Length==64,"RfqPrepublicationSnapshotInvalid");return result;
    }
    private static bool Instant(DateTimeOffset value)=>value.Offset==TimeSpan.Zero&&value!=default
        &&value!=DateTimeOffset.MaxValue&&value.UtcTicks%10==0;
    private static void Need(bool condition,string code){if(!condition)throw new InvalidOperationException(code);}
}
