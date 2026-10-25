using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Structural snapshot of the actual independently published SOURCING
/// controls selected for each original source line. Not current database proof or
/// a commercial scoring/publication authority; the owner resolves/revalidates it.</summary>
public sealed class PrincipalRfqPublishedPlanPolicy
{
    public Guid PolicyId{get;}
    public int PolicyVersionNumber{get;}
    public long PolicyRecordVersion{get;}
    public Guid PublishedIdentityId{get;}
    public string PublishedIdentitySha256{get;}
    public string RulesSha256{get;}
    public string OriginalFingerprint{get;}
    public DateTimeOffset ObservedAt{get;}
    public bool RequirementsPackRequired{get;}
    private PrincipalRfqPublishedPlanPolicy(PolicyVersion policy,SourcingPolicyIdentityEvidence published,
        RfqPrincipalDraft draft,DateTimeOffset at,SourcingPublicationControls controls)
    {PolicyId=policy.Id;PolicyVersionNumber=policy.PolicyVersionNumber;PolicyRecordVersion=policy.Version;
        PublishedIdentityId=published.Id;PublishedIdentitySha256=published.EvidenceSha256;
        RulesSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson);
        OriginalFingerprint=draft.OriginalFingerprint;ObservedAt=at;RequirementsPackRequired=controls.RequirementsPackRequired;}

    public static PrincipalRfqPublishedPlanPolicy Select(PolicyVersion policy,SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted,SourcingPolicyIdentityEvidence published,
        RfqPrincipalDraft draft,IReadOnlyList<PolicyScenario> originalClassifications,DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(policy);ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(created);ArgumentNullException.ThrowIfNull(submitted);ArgumentNullException.ThrowIfNull(published);
        ArgumentNullException.ThrowIfNull(originalClassifications);
        if(policy.State!="Published"||policy.PolicyKey!=SourcingPublicationPolicyRules.PolicyKey
            ||policy.TenantId!=draft.TenantId||policy.LegalEntityId!=draft.CompanyId
            ||at==default||policy.EffectiveFrom>at||policy.EffectiveTo is {} end&&at>=end
            ||originalClassifications.Count!=draft.Lines.Count||draft.Lines.Count is <1 or >200)
            throw new InvalidOperationException("RfqPrepublicationPublishedPolicyRequired");
        SourcingPolicyIdentity.VerifyPublication(policy,created,submitted,published);
        var lines=draft.Lines.OrderBy(x=>x.EventLineId).ToArray();
        SourcingPublicationControls? controls=null;
        for(var index=0;index<lines.Length;index++)
        {
            var line=lines[index];var scenario=originalClassifications[index];
            if(scenario is null||scenario.CurrencyCode!=draft.CurrencyCode||scenario.BranchId!=draft.BranchId
                ||scenario.CategoryCode!=line.CategoryCode||scenario.LineBaseAmount!=line.EstimatedValue)
                throw new InvalidOperationException("RfqPrepublicationPolicySourceChanged");
            // Purchase type comes from the owning verified source loader, not a UI
            // classification choice; the candidate cannot establish its provenance.
            var match=SourcingPublicationPolicyRules.Evaluate(policy.RulesJson,scenario);
            if(match.Status!="Match"||match.Controls is null||match.PolicyDigest!=published.RulesSha256
                ||controls is not null&&controls!=match.Controls)
                throw new InvalidOperationException("RfqPrepublicationMixedOrMissingPolicy");
            controls=match.Controls;
        }
        return new(policy,published,draft,at,controls!);
    }
}
