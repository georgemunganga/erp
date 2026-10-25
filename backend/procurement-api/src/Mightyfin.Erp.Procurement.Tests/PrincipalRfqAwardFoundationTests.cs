using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqAwardFoundationTests
{
    private static readonly Guid Company=Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Branch=Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Offer=Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Source=Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Supplier=Guid.Parse("55555555-5555-4555-8555-555555555555");
    private const string Maker="66666666-6666-4666-8666-666666666666";
    private const string Reviewer="77777777-7777-4777-8777-777777777777";
    private static readonly DateTimeOffset Now=new(2026,10,25,4,0,0,TimeSpan.Zero);

    private static string Rules(string? ceiling="100") =>
        "{\"schemaVersion\":1,\"transactionType\":\"SupplierAward\",\"allocationMode\":\"FullOfferLineOnly\",\"currencyCode\":\"ZMW\",\"maximumAmountExact\":\""
        +ceiling+"\",\"stages\":[{\"code\":\"REVIEW\",\"mode\":\"All\",\"principalIds\":[\""+Reviewer+"\"]}]}";

    private static PrincipalRfqAwardFoundation.CurrentPerson Person(string id,string action,
        Guid? worker=null,Guid? allowedBranch=null) =>new(id,worker,false,
            new HashSet<Guid>{allowedBranch??Branch},
            new HashSet<string>{"procurement-access",action});

    private static (PrincipalRfqAwardSourceQualification.VerifiedSelection Selection,
        PrincipalRfqAwardFoundation.RetainedLine[] Retained,PolicyVersion Policy,
        PrincipalRfqAwardFoundation.CurrentPerson Maker,
        PrincipalRfqAwardFoundation.CurrentPerson[] Reviewers) Valid()
    {
        var line=new PrincipalRfqAwardSourceQualification.SelectedLine(Offer,new string('a',64),
            Supplier,Source,"0.5","1","ZMW","Human rationale");
        var selection=new PrincipalRfqAwardSourceQualification.VerifiedSelection(
            Guid.CreateVersion7(),Guid.CreateVersion7(),1,Guid.CreateVersion7(),
            Guid.CreateVersion7(),new string('b',64),[line]);
        var retained=new PrincipalRfqAwardFoundation.RetainedLine[]
            {new(Offer,new string('a',64),Supplier,Source,"0.5","0.5","ZMW","50.00000001")};
        var policy=new PolicyVersion {TenantId="tenant-a",LegalEntityId=Company,
            PolicyKey=PrincipalRfqAwardFoundation.PolicyKey,PolicyVersionNumber=1,
            State="Published",RulesJson=Rules(),EffectiveFrom=Now.AddDays(-1)};
        return(selection,retained,policy,
            Person(Maker,PrincipalRfqAwardFoundation.SubmitAction),
            [Person(Reviewer,PrincipalRfqAwardFoundation.ReviewAction)]);
    }

    private static PrincipalRfqAwardCandidate Qualify(
        PrincipalRfqAwardSourceQualification.VerifiedSelection selection,
        PrincipalRfqAwardFoundation.RetainedLine[] retained,PolicyVersion policy,
        PrincipalRfqAwardFoundation.CurrentPerson maker,
        PrincipalRfqAwardFoundation.CurrentPerson[] reviewers,
        IReadOnlyList<PrincipalRfqAwardFoundation.ExcludedParticipant>? excluded=null,
        int? expectedRevision=null,string? requestJson=null)
    {
        var body=requestJson??PrincipalRfqAwardFoundation.CommandBody(selection,policy.Id,Branch);
        return PrincipalRfqAwardFoundation.Qualify(selection,retained,policy,maker,reviewers,
            excluded??[],Guid.CreateVersion7(),"tenant-a",Company,Branch,Guid.CreateVersion7(),body,
            ProcurementEvidenceEncoding.Digest(body),expectedRevision??selection.Revision,Now);
    }

    [Fact]
    public void Explicit_published_policy_and_current_named_independent_people_produce_only_a_candidate()
    {
        var x=Valid();var value=Qualify(x.Selection,x.Retained,x.Policy,x.Maker,x.Reviewers);
        Assert.Equal("50.00000001",value.ProposedTotalExact);
        Assert.Equal("0.5",Assert.Single(value.Lines).QuantityExact);
        Assert.Equal(x.Selection.RecommendationSha256,value.RecommendationSha256);
        Assert.IsType<PrincipalRfqAwardCandidate>(value);
    }

    [Fact]
    public void Partial_quantity_or_stale_revision_cannot_be_promoted_as_full_quote_line()
    {
        var x=Valid();
        var partial=x.Selection with {Lines=[x.Selection.Lines[0] with {QuantityExact="0.4"}]};
        Assert.Throws<InvalidOperationException>(()=>Qualify(partial,x.Retained,x.Policy,x.Maker,x.Reviewers));
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,x.Maker,
            x.Reviewers,expectedRevision:2));
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,x.Maker,
            x.Reviewers,requestJson:"{}"));
    }

    [Fact]
    public void Substituted_offer_supplier_currency_or_insufficient_hold_denies()
    {
        var x=Valid();var source=x.Retained[0];
        foreach(var changed in new[]{source with {OfferSha256=new string('e',64)},
            source with {SupplierId=Guid.CreateVersion7()},source with {CurrencyCode="USD"},
            source with {RemainingHeldQuantityExact="0.4"}})
            Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,[changed],x.Policy,
                x.Maker,x.Reviewers));
    }

    [Fact]
    public void Separate_offers_cannot_spend_the_same_source_hold_twice()
    {
        var x=Valid();var second=Guid.CreateVersion7();
        var selection=x.Selection with {Lines=[x.Selection.Lines[0],
            x.Selection.Lines[0] with {OfferId=second,OfferSha256=new string('d',64)}]};
        var retained=new[]{x.Retained[0],x.Retained[0] with {OfferId=second,
            OfferSha256=new string('d',64)}};
        Assert.Throws<InvalidOperationException>(()=>Qualify(selection,retained,x.Policy,
            x.Maker,x.Reviewers));
    }

    [Fact]
    public void Missing_action_branch_or_independence_denies_before_candidate()
    {
        var x=Valid();
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,
            Person(Maker,"procurement-sourcing-recommend-draft"),x.Reviewers));
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,
            x.Maker,[Person(Reviewer,PrincipalRfqAwardFoundation.ReviewAction,
                allowedBranch:Guid.CreateVersion7())]));
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,
            x.Maker,x.Reviewers,[new(Reviewer,null)]));
        var alias=Guid.CreateVersion7();
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,x.Policy,
            Person(Maker,PrincipalRfqAwardFoundation.SubmitAction,alias),
            [Person(Reviewer,PrincipalRfqAwardFoundation.ReviewAction,alias)]));
    }

    [Fact]
    public void Wrong_or_absent_policy_and_amount_ceiling_denies()
    {
        var x=Valid();
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,
            new PolicyVersion{TenantId="tenant-a",LegalEntityId=Company,
                PolicyKey=PrincipalRfqAwardFoundation.PolicyKey,State="Draft",
                PolicyVersionNumber=1,RulesJson=Rules(),EffectiveFrom=Now.AddDays(-1)},
            x.Maker,x.Reviewers));
        x.Policy.RulesJson=Rules("49");
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,
            x.Policy,x.Maker,x.Reviewers));
        x.Policy.RulesJson=Rules();x.Policy.LegalEntityId=Guid.CreateVersion7();
        Assert.Throws<InvalidOperationException>(()=>Qualify(x.Selection,x.Retained,
            x.Policy,x.Maker,x.Reviewers));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardFoundation.ParseRules("{}"));
    }

    [Fact]
    public void Original_receipt_requires_current_actor_scope_action_key_and_stored_digest()
    {
        var x=Valid();var candidate=Qualify(x.Selection,x.Retained,x.Policy,x.Maker,x.Reviewers);
        const string response="{\"state\":\"PendingReview\"}";
        var receipt=new PrincipalRfqAwardFoundation.StoredReceipt(candidate.SubmissionId,
            "tenant-a",Company,Branch,Maker,candidate.CommandKey,candidate.RequestSha256,
            response,ProcurementEvidenceEncoding.Digest(response));
        Assert.Equal(response,PrincipalRfqAwardFoundation.RecoverOriginal(candidate,receipt,
            x.Maker,"tenant-a",Company,Branch));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardFoundation.RecoverOriginal(
            candidate,receipt with {ResponseJson="{}"},x.Maker,"tenant-a",Company,Branch));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardFoundation.RecoverOriginal(
            candidate,receipt with {SubmissionId=Guid.CreateVersion7()},x.Maker,
            "tenant-a",Company,Branch));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardFoundation.RecoverOriginal(
            candidate,receipt,Person(Reviewer,PrincipalRfqAwardFoundation.SubmitAction),
            "tenant-a",Company,Branch));
    }
}
