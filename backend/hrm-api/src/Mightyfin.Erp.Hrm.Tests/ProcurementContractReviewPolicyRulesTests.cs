using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementContractReviewPolicyRulesTests
{
    [Fact]
    public void Named_review_stages_require_canonical_distinct_Core_principals_and_explicit_mode()
    {
        var legal = Guid.NewGuid().ToString("D");
        var finance = Guid.NewGuid().ToString("D");
        var json = $$"""{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[{"code":"LEGAL","mode":"All","principalIds":["{{legal}}"]},{"code":"FINANCE","mode":"One","principalIds":["{{finance}}"]}]}""";
        var parsed = ProcurementContractReviewPolicyRules.Parse(json);
        Assert.Equal(2, parsed.Stages.Count);
        Assert.Equal("All", parsed.Stages[0].Mode);
        Assert.Equal("One", parsed.Stages[1].Mode);
        Assert.Empty(ProcurementContractReviewPolicyRules.Validate(json));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(json.Replace(finance, legal)));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(json.Replace("\"One\"", "\"Any\"")));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(json.Replace("SupplierContractReview", "PurchaseOrder")));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(json.Replace("\"LEGAL\"", "\"LEGAL!\"")));
    }

    [Fact]
    public void Draft_can_be_incomplete_but_published_review_requires_a_named_stage()
    {
        const string empty = """{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[]}""";
        Assert.Empty(ProcurementContractReviewPolicyRules.Validate(empty, draft: true));
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(empty));
        var duplicateProperty = empty.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1");
        Assert.NotEmpty(ProcurementContractReviewPolicyRules.Validate(duplicateProperty, draft: true));
    }
}
