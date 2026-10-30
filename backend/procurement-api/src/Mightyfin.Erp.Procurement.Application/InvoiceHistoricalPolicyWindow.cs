using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Complete owning-server publication evidence, loaded from one scoped transaction.
/// Callers supply all Published audits, rather than selecting an arbitrary matching row.</summary>
public sealed record ScopedHistoricalPolicyPublication(PolicyVersion Policy,
    OperationalPolicyWorkerIdentityEvidence Created,
    OperationalPolicyWorkerIdentityEvidence Submitted,
    OperationalPolicyWorkerIdentityEvidence Published,
    IReadOnlyList<AuditEvent> PublishedAudits);

/// <summary>Verified original publication window at one recorded business observation.
/// This historical witness grants no current configuration, comparison, approval or Finance authority.</summary>
public sealed class ScopedHistoricalPolicyWitness
{
    internal ScopedHistoricalPolicyWitness(PolicyVersion policy, DateTimeOffset observedAt,
        DateTimeOffset? originalEnd, Guid publicationAuditId, Guid publicationIdentityId)
    {
        TenantId=policy.TenantId; LegalEntityId=policy.LegalEntityId; PolicyId=policy.Id;
        PolicyKey=policy.PolicyKey; PolicyVersionNumber=policy.PolicyVersionNumber;
        RulesJson=policy.RulesJson; RulesSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(RulesJson);
        EffectiveFrom=policy.EffectiveFrom; OriginalEffectiveUntil=originalEnd; ObservedAt=observedAt;
        PublicationAuditId=publicationAuditId; PublicationIdentityId=publicationIdentityId;
    }
    public string TenantId { get; }
    public Guid LegalEntityId { get; }
    public Guid PolicyId { get; }
    public string PolicyKey { get; }
    public int PolicyVersionNumber { get; }
    public string RulesJson { get; }
    public string RulesSha256 { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? OriginalEffectiveUntil { get; }
    public DateTimeOffset ObservedAt { get; }
    public Guid PublicationAuditId { get; }
    public Guid PublicationIdentityId { get; }
}

/// <summary>Reconstructs the original declared window from the actual policy Published audit.
/// The only supported later window mutation is the existing publisher's atomic supersession:
/// an open-ended policy is closed at the effective start of a genuinely published successor.</summary>
public static class InvoiceHistoricalPolicyWindow
{
    public static ScopedHistoricalPolicyWitness Verify(ScopedHistoricalPolicyPublication publication,
        DateTimeOffset observedAt, IReadOnlyList<AuditEvent> supersededAudits,
        ScopedHistoricalPolicyPublication? supersedingPublication = null)
    {
        try
        {
            var policy=publication.Policy;
            Need(observedAt!=default && observedAt.Offset==TimeSpan.Zero
                && observedAt==ProcurementEvidenceEncoding.DatabaseTimestamp(observedAt), "HistoricalPolicyObservationRequired");
            var original=Publication(publication);
            Need(original.Audit.CreatedAt<=observedAt && policy.PublishedAt<=observedAt && policy.EffectiveFrom<=observedAt
                && (original.End is null || observedAt<original.End)
                && (policy.EffectiveTo is null || observedAt<policy.EffectiveTo), "HistoricalPolicyObservationOutsideWindow");
            Need(supersededAudits.Count<=1,"HistoricalPolicySupersessionAmbiguous");
            if(policy.EffectiveTo==original.End)
                Need(supersededAudits.Count==0 && supersedingPublication is null,"HistoricalPolicyUnexpectedSupersession");
            else
            {
                Need(original.End is null && policy.EffectiveTo is { } && supersededAudits.Count==1
                    && supersedingPublication is not null,"HistoricalPolicyWindowMutationUnproved");
                var successor=supersedingPublication!; var next=successor.Policy;
                var nextOriginal=Publication(successor);
                Need(next.Id!=policy.Id && next.TenantId==policy.TenantId && next.LegalEntityId==policy.LegalEntityId
                    && next.PolicyKey==policy.PolicyKey && next.PolicyVersionNumber>policy.PolicyVersionNumber
                    && next.EffectiveFrom==policy.EffectiveTo
                    && next.PublishedAt>=policy.PublishedAt && next.PublishedAt<next.EffectiveFrom,
                    "HistoricalPolicySupersedingPublicationMismatch");
                var audit=supersededAudits[0]; using var document=Audit(policy,audit,"Superseded");
                var root=document.RootElement; Fields(policy,root,policy.EffectiveTo);
                Need(root.GetProperty("reason").GetString()==$"Superseded by policy version {next.PolicyVersionNumber}."
                    && audit.ActorSubjectId==next.PublishedBy && audit.CreatedBy==next.PublishedBy
                    && audit.CreatedAt==nextOriginal.Audit.CreatedAt
                    && audit.CreatedAt>=original.Audit.CreatedAt
                    && audit.CreatedAt>=next.PublishedAt, "HistoricalPolicySupersessionAuditMismatch");
            }
            return new(policy,observedAt,original.End,original.Audit.Id,publication.Published.Id);
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or FormatException
            or OverflowException or ArgumentException)
        { throw new InvalidOperationException("HistoricalPolicyEvidenceMalformed",error); }
    }

    public static InvoiceMatchingPolicy ProjectMatching(ScopedHistoricalPolicyWitness witness)
    {
        Need(witness.PolicyKey==InvoiceMatchingPolicyRules.PolicyKey,"HistoricalMatchingPolicyRequired");
        var rules=InvoiceMatchingPolicyRules.Parse(witness.RulesJson);
        return new(witness.PolicyId,witness.PolicyVersionNumber,witness.TenantId,witness.LegalEntityId,null,
            "Published",$"INVOICE_MATCHING:{witness.PolicyId:D}:{witness.PolicyVersionNumber}",
            witness.EffectiveFrom,witness.OriginalEffectiveUntil,rules.MoneyScale,rules.Rounding,rules.Rules);
    }

    private static (AuditEvent Audit, DateTimeOffset? End) Publication(ScopedHistoricalPolicyPublication evidence)
    {
        var policy=evidence.Policy;
        OperationalPolicyWorkerIdentity.VerifyPublication(policy,evidence.Created,evidence.Submitted,evidence.Published,
            OperationalPolicyPublicationMode.RetainedHistorical);
        Need(policy.PolicyKey is InvoiceMatchingPolicyRules.PolicyKey or InvoiceRetainedUnitReview.ReviewPolicyKey
            && policy.PublishedAt is { } at && at.Offset==TimeSpan.Zero && policy.EffectiveFrom.Offset==TimeSpan.Zero
            && (policy.EffectiveTo is null || policy.EffectiveTo.Value.Offset==TimeSpan.Zero)
            && !string.IsNullOrWhiteSpace(policy.PublicationReason)
            && evidence.PublishedAudits.Count==1,"HistoricalPolicyPublicationRequired");
        var audit=evidence.PublishedAudits[0]; using var document=Audit(policy,audit,"Published");
        var root=document.RootElement; var end=Time(root,"EffectiveTo"); Fields(policy,root,end);
        Need(end is null || end>policy.EffectiveFrom,"HistoricalPolicyOriginalWindowInvalid");
        Need(root.GetProperty("reason").ValueKind==JsonValueKind.Null
            && audit.ActorSubjectId==policy.PublishedBy && audit.CreatedBy==policy.PublishedBy
            && audit.CreatedAt>=policy.PublishedAt,"HistoricalPolicyPublishedAuditMismatch");
        return(audit,end);
    }

    private static JsonDocument Audit(PolicyVersion policy, AuditEvent audit, string action)
    {
        Need(audit.Id!=Guid.Empty && audit.Version==1 && audit.RecordType=="PolicyVersion"
            && audit.RecordId==policy.Id && audit.TenantId==policy.TenantId
            && audit.LegalEntityId==policy.LegalEntityId && audit.Action==action
            && audit.CreatedAt!=default && audit.CreatedAt.Offset==TimeSpan.Zero
            && audit.UpdatedAt is null && audit.UpdatedBy is null
            && !string.IsNullOrWhiteSpace(audit.ChangedFieldsJson)
            && Encoding.UTF8.GetByteCount(audit.ChangedFieldsJson)<=131072
            && ProcurementEvidenceEncoding.PolicyJsonDigest(audit.ChangedFieldsJson).Length==64,
            "HistoricalPolicyAuditInvalid");
        var document=JsonDocument.Parse(audit.ChangedFieldsJson!);
        var names=new[]{"PolicyKey","PolicyVersionNumber","State","EffectiveFrom","EffectiveTo",
            "ReviewReason","PublicationReason","PublishedAt","PublishedBy","reason","rulesSha256"};
        if(!document.RootElement.EnumerateObject().Select(x=>x.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal)))
        { document.Dispose(); throw new InvalidOperationException("HistoricalPolicyAuditFieldsInvalid"); }
        return document;
    }

    private static void Fields(PolicyVersion policy, JsonElement root, DateTimeOffset? end)
    {
        Need(root.GetProperty("PolicyKey").GetString()==policy.PolicyKey
            && root.GetProperty("PolicyVersionNumber").GetInt32()==policy.PolicyVersionNumber
            && root.GetProperty("State").GetString()=="Published"
            && Time(root,"EffectiveFrom")==policy.EffectiveFrom && Time(root,"EffectiveTo")==end
            && Time(root,"PublishedAt")==ProcurementEvidenceEncoding.DatabaseTimestamp(policy.PublishedAt)
            && root.GetProperty("PublishedBy").GetString()==policy.PublishedBy
            && root.GetProperty("ReviewReason").GetString()==policy.ReviewReason
            && root.GetProperty("PublicationReason").GetString()==policy.PublicationReason
            && root.GetProperty("rulesSha256").GetString()==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policy.RulesJson))),
            "HistoricalPolicyAuditOwnerMismatch");
    }
    private static DateTimeOffset? Time(JsonElement root,string key)
    {
        var value=root.GetProperty(key); if(value.ValueKind==JsonValueKind.Null)return null;
        var at=value.GetDateTimeOffset(); Need(at.Offset==TimeSpan.Zero,"HistoricalPolicyAuditTimeInvalid");
        return ProcurementEvidenceEncoding.DatabaseTimestamp(at);
    }
    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool okay,string code)
    { if(!okay)throw new InvalidOperationException(code); }
}
