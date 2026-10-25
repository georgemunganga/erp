using Mightyfin.Erp.Procurement.Application;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqRecommendationDraftTests
{
    private static readonly Guid Source=Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid First=Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Second=Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Supplier=Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly PrincipalRfqRecommendationDraft.SourceLine[] Sources=[new(Source,"1")];
    private static readonly PrincipalRfqRecommendationDraft.OfferLine[] Offers=
    [new(First,new string('a',64),Supplier,Source,"0.6","ZMW"),
     new(Second,new string('b',64),Supplier,Source,"0.6","ZMW")];

    [Fact]
    public void Human_can_propose_split_lines_without_minting_rank_or_award()
    {
        var proposal=PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,"0.5","0.00000001","Observed fit"),
             new(Second,Source,"0.5","999999999999999999999999999999.99999999","Alternate fit")],
            Offers,Sources,"ZMW");
        Assert.Equal(2,proposal.Count);
        Assert.Equal("0.00000001",proposal[0].ProposedValueExact);
        Assert.Equal("999999999999999999999999999999.99999999",proposal[1].ProposedValueExact);
        Assert.All(proposal,x=>Assert.Equal(Supplier,x.SupplierId));
        Assert.Empty(PrincipalRfqRecommendationDraft.Validate([],Offers,Sources,"ZMW"));
    }

    [Theory]
    [InlineData("0.6001","1")]
    [InlineData("0.5","1.000000001")]
    [InlineData("0.5","1e3")]
    [InlineData("0.5","01")]
    public void Invalid_quantity_or_value_fails_closed(string quantity,string value)
        =>Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,quantity,value,"Reason")],Offers,Sources,"ZMW"));

    [Fact]
    public void Combined_proposals_cannot_exceed_original_source_even_if_each_offer_allows_it()
        =>Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,"0.6","1","Reason"),new(Second,Source,"0.6","1","Reason")],
            Offers,Sources,"ZMW"));

    [Fact]
    public void Unopened_offer_or_wrong_currency_cannot_be_proposed()
    {
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(Guid.NewGuid(),Source,"0.5","1","Reason")],Offers,Sources,"ZMW"));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,"0.5","1","Reason")],Offers,Sources,"USD"));
    }

    [Fact]
    public void Duplicate_offer_line_or_duplicated_source_projection_cannot_become_a_draft()
    {
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,"0.2","1","Reason"),
             new(First,Source,"0.2","1","Reason")],Offers,Sources,"ZMW"));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqRecommendationDraft.Validate(
            [new(First,Source,"0.2","1","Reason")],Offers,[Sources[0],Sources[0]],"ZMW"));
    }
}
