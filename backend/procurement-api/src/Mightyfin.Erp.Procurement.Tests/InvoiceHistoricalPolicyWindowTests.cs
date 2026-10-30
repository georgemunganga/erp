using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure scoped retained-window proof; persisted provenance and Core authority are
/// supplied by the owning Serializable reader, not manufactured by this test fixture.</summary>
public sealed class InvoiceHistoricalPolicyWindowTests
{
    private const string Maker="11111111-1111-4111-8111-111111111111";
    private const string Reviewer="22222222-2222-4222-8222-222222222222";
    private static readonly DateTimeOffset At=new(2026,10,20,12,0,0,TimeSpan.Zero);
    private static readonly Guid Company=Guid.Parse("33333333-3333-4333-8333-333333333333");
    private const string Rules="""
        {"schemaVersion":1,"transactionType":"InvoiceMatching","currencyCode":"ZMW","moneyScale":2,"rounding":"ToEven","rules":[{"categoryCode":"GOODS","method":"ThreeWay","acceptanceKind":"Goods","quantity":{"absolute":"0","percent":"0","combination":"AllLimits"},"unitPrice":{"absolute":"0","percent":"0","combination":"AllLimits"},"tax":{"absolute":"0","percent":"0","combination":"AllLimits"},"total":{"absolute":"0","percent":"0","combination":"AllLimits"}}]}
        """;

    [Fact]
    public void Original_published_window_reconstructs_exact_matching_projection()
    {
        var p=Publication(1,At,At.AddDays(1));
        var expected=InvoiceMatchingPolicyRules.Project(p.Policy);
        var witness=InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(2),[]);
        Assert.Equal(InvoiceMatchingFoundation.SerializePolicyEvidence(expected),
            InvoiceMatchingFoundation.SerializePolicyEvidence(InvoiceHistoricalPolicyWindow.ProjectMatching(witness)));
        Assert.Equal(p.Published.Id,witness.PublicationIdentityId);
        Assert.Equal(p.PublishedAudits[0].Id,witness.PublicationAuditId);
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest(Rules),witness.RulesSha256);
    }

    [Fact]
    public void Genuine_supersession_preserves_original_null_end_after_new_policy_begins()
    {
        var f=Superseded(); var original=InvoiceMatchingFoundation.SerializePolicyEvidence(
            InvoiceMatchingPolicyRules.Project(f.Old.Policy) with { EffectiveUntil=null });
        var witness=InvoiceHistoricalPolicyWindow.Verify(f.Old,At.AddDays(2),[f.Audit],f.Next);
        Assert.Null(witness.OriginalEffectiveUntil);
        Assert.Equal(original,InvoiceMatchingFoundation.SerializePolicyEvidence(InvoiceHistoricalPolicyWindow.ProjectMatching(witness)));
        Assert.NotEqual(original,InvoiceMatchingFoundation.SerializePolicyEvidence(InvoiceMatchingPolicyRules.Project(f.Old.Policy)));
    }

    [Fact]
    public void Published_bounded_window_is_retained_and_exclusive_at_end()
    {
        var p=Publication(1,At,At.AddDays(1),At.AddDays(4));
        Assert.Equal(At.AddDays(4),InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(2),[]).OriginalEffectiveUntil);
        Assert.Throws<InvalidOperationException>(()=>InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(4),[]));
    }

    [Theory]
    [InlineData("missingPublished")][InlineData("duplicatePublished")][InlineData("duplicateSuperseded")]
    [InlineData("missingSuperseded")][InlineData("missingSuccessor")][InlineData("crossScope")]
    [InlineData("wrongBoundary")][InlineData("wrongSuccessorNumber")][InlineData("wrongSupersedingActor")]
    [InlineData("wrongBatch")][InlineData("changedRules")][InlineData("changedPublisher")]
    [InlineData("changedFrom")][InlineData("changedReason")][InlineData("arbitraryEnd")]
    public void Missing_ambiguous_or_rebound_owner_evidence_is_rejected(string fault)
    {
        var f=Superseded();var old=f.Old;ScopedHistoricalPolicyPublication? next=f.Next;IReadOnlyList<AuditEvent> audits=[f.Audit];
        if(fault=="missingPublished")old=old with { PublishedAudits=[] };
        if(fault=="duplicatePublished")old=old with { PublishedAudits=[old.PublishedAudits[0],old.PublishedAudits[0]] };
        if(fault=="duplicateSuperseded")audits=[f.Audit,f.Audit];
        if(fault=="missingSuperseded")audits=[];
        if(fault=="missingSuccessor")next=null;
        if(fault=="crossScope")next!.Policy.LegalEntityId=Guid.NewGuid();
        if(fault=="wrongBoundary")old.Policy.EffectiveTo=At.AddDays(5);
        if(fault=="wrongSuccessorNumber")f.Audit.ChangedFieldsJson=f.Audit.ChangedFieldsJson!.Replace("version 2.","version 3.",StringComparison.Ordinal);
        if(fault=="wrongSupersedingActor")f.Audit.ActorSubjectId=Maker;
        if(fault=="wrongBatch")f.Audit.CreatedAt=f.Audit.CreatedAt.AddSeconds(1);
        if(fault=="changedRules")old.Policy.RulesJson=Rules.Replace("\"moneyScale\":2","\"moneyScale\":3",StringComparison.Ordinal);
        if(fault=="changedPublisher")old.Policy.PublishedBy=Maker;
        if(fault=="changedFrom")old.Policy.EffectiveFrom=At.AddHours(1);
        if(fault=="changedReason")old.Policy.PublicationReason="rewritten";
        if(fault=="arbitraryEnd")
        {
            old.Policy.EffectiveTo=At.AddDays(5);
            old=old with { PublishedAudits=[Audit(old.Policy,"Published",At.AddSeconds(1))] };
        }
        Assert.Throws<InvalidOperationException>(()=>InvoiceHistoricalPolicyWindow.Verify(old,At.AddDays(2),audits,next));
    }

    [Theory]
    [InlineData("duplicateKey")][InlineData("unknownKey")][InlineData("missingKey")]
    [InlineData("wrongType")][InlineData("wrongRecord")][InlineData("wrongScope")]
    [InlineData("wrongRulesHash")][InlineData("malformed")]
    public void Published_audit_is_exact_typed_duplicate_free_and_scoped(string fault)
    {
        var p=Publication(1,At,At.AddDays(1));var a=p.PublishedAudits[0];
        if(fault=="duplicateKey")a.ChangedFieldsJson=a.ChangedFieldsJson!.Replace("{","{\"EffectiveTo\":null,",StringComparison.Ordinal);
        if(fault=="unknownKey")a.ChangedFieldsJson=a.ChangedFieldsJson!.Replace("{","{\"extra\":true,",StringComparison.Ordinal);
        if(fault=="missingKey")a.ChangedFieldsJson=a.ChangedFieldsJson!.Replace("\"reason\":null,","",StringComparison.Ordinal);
        if(fault=="wrongType")a.ChangedFieldsJson=a.ChangedFieldsJson!.Replace("\"PolicyVersionNumber\":1","\"PolicyVersionNumber\":\"1\"",StringComparison.Ordinal);
        if(fault=="wrongRecord")a.RecordId=Guid.NewGuid();
        if(fault=="wrongScope")a.TenantId="foreign";
        if(fault=="wrongRulesHash")a.ChangedFieldsJson=a.ChangedFieldsJson!.Replace(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Rules))),new string('0',64),StringComparison.Ordinal);
        if(fault=="malformed")a.ChangedFieldsJson="{";
        Assert.Throws<InvalidOperationException>(()=>InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(2),[]));
    }

    [Theory][InlineData(-1)][InlineData(0)][InlineData(4)]
    public void Observation_must_be_after_publication_and_within_original_and_shortened_windows(int days)
    {
        var f=Superseded();Assert.Throws<InvalidOperationException>(()=>
            InvoiceHistoricalPolicyWindow.Verify(f.Old,At.AddDays(days),[f.Audit],f.Next));
    }

    [Fact]
    public void Unpublished_retired_draft_cannot_become_historical_publication_authority()
    {
        var p=Publication(1,At,At.AddDays(1));p.Policy.State="Retired";
        Assert.Throws<InvalidOperationException>(()=>InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(2),[]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceMatchingPolicyRules.Project(p.Policy));
    }

    [Fact]
    public void Witness_copies_original_values_instead_of_retaining_mutable_policy_reference()
    {
        var p=Publication(1,At,At.AddDays(1));var w=InvoiceHistoricalPolicyWindow.Verify(p,At.AddDays(2),[]);
        p.Policy.RulesJson="{}";p.Policy.EffectiveTo=At;p.Policy.PolicyVersionNumber=99;
        Assert.Equal(1,w.PolicyVersionNumber);Assert.Equal(2,InvoiceHistoricalPolicyWindow.ProjectMatching(w).MoneyScale);
        Assert.Null(w.OriginalEffectiveUntil);
    }

    private static (ScopedHistoricalPolicyPublication Old,ScopedHistoricalPolicyPublication Next,AuditEvent Audit) Superseded()
    {
        var old=Publication(1,At,At.AddDays(1));var next=Publication(2,At.AddDays(3),At.AddDays(4));
        old.Policy.EffectiveTo=next.Policy.EffectiveFrom;old.Policy.Version++;
        var audit=Audit(old.Policy,"Superseded",next.PublishedAudits[0].CreatedAt,$"Superseded by policy version {next.Policy.PolicyVersionNumber}.");
        return(old,next,audit);
    }
    private static ScopedHistoricalPolicyPublication Publication(int number,DateTimeOffset publishedAt,
        DateTimeOffset from,DateTimeOffset? end=null)
    {
        var p=new PolicyVersion{TenantId="tenant",LegalEntityId=Company,PolicyKey=InvoiceMatchingPolicyRules.PolicyKey,
            PolicyVersionNumber=number,EffectiveFrom=from,EffectiveTo=end,RulesJson=Rules,State="Draft",Version=1,
            CreatedBy=Maker,CreatedAt=publishedAt.AddMinutes(-2),ReviewReason="submit",PublicationReason="approve"};
        var created=OperationalPolicyWorkerIdentity.Capture(p,"Created",Maker,null,1,p.CreatedAt,schemaVersion:2);
        p.State="PendingReview";p.Version=2;p.ReviewRequestedBy=Maker;p.ReviewRequestedAt=publishedAt.AddMinutes(-1);
        var submitted=OperationalPolicyWorkerIdentity.Capture(p,"Submitted",Maker,null,2,p.ReviewRequestedAt.Value,created,schemaVersion:2);
        p.State="Published";p.Version=3;p.PublishedBy=Reviewer;p.PublishedAt=publishedAt;
        var published=OperationalPolicyWorkerIdentity.Capture(p,"Published",Reviewer,null,3,publishedAt,created,submitted,schemaVersion:2);
        return new(p,created,submitted,published,[Audit(p,"Published",publishedAt.AddSeconds(1))]);
    }
    private static AuditEvent Audit(PolicyVersion p,string action,DateTimeOffset createdAt,string? reason=null)
        =>new(){TenantId=p.TenantId,LegalEntityId=p.LegalEntityId,RecordType="PolicyVersion",RecordId=p.Id,
            Action=action,ActorSubjectId=Reviewer,CreatedBy=Reviewer,CreatedAt=createdAt,Version=1,
            ChangedFieldsJson=JsonSerializer.Serialize(new{p.PolicyKey,p.PolicyVersionNumber,p.State,p.EffectiveFrom,p.EffectiveTo,
                p.ReviewReason,p.PublicationReason,p.PublishedAt,p.PublishedBy,reason,
                rulesSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.RulesJson)))})};
}
