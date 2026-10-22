using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMatchingPolicyRulesTests
{
    private const string Rules = """
        {"schemaVersion":1,"transactionType":"InvoiceMatching","currencyCode":"ZMW","moneyScale":2,"rounding":"ToEven","rules":[
          {"categoryCode":"IT","method":"ThreeWay","acceptanceKind":"Goods",
           "quantity":{"absolute":"0","percent":"0","combination":"AllLimits"},
           "unitPrice":{"absolute":"0","percent":"0","combination":"AllLimits"},
           "tax":{"absolute":"0","percent":"0","combination":"AllLimits"},
           "total":{"absolute":"0","percent":"0","combination":"AllLimits"}}]}
        """;
    [Fact]
    public void Explicit_exact_three_way_configuration_is_valid_but_does_not_publish_itself()
    {
        Assert.Empty(InvoiceMatchingPolicyRules.Validate(Rules));
        var parsed=InvoiceMatchingPolicyRules.Parse(Rules);
        Assert.Equal(InvoiceMatchingMethod.ThreeWay,Assert.Single(parsed.Rules).Method);
        Assert.Equal(0m,parsed.Rules[0].Quantity.Absolute);
    }
    [Theory]
    [InlineData("\"moneyScale\":2","\"moneyScale\":9")]
    [InlineData("\"method\":\"ThreeWay\"","\"method\":\"TwoWay\"")]
    [InlineData("\"absolute\":\"0\"","\"absolute\":0")]
    [InlineData("\"percent\":\"0\"","\"percent\":\"100.0001\"")]
    [InlineData("\"rounding\":\"ToEven\"","\"rounding\":\"Automatic\"")]
    [InlineData("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1")]
    public void Unsupported_or_ambiguous_rules_fail_closed_instead_of_choosing_a_tolerance(string from,string to)
        => Assert.NotEmpty(InvoiceMatchingPolicyRules.Validate(Rules.Replace(from,to)));
}
