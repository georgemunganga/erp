using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementManualPaymentPolicyPublicationTests
{
    private const string Rules = """
        {"schemaVersion":1,"identitySchemaVersion":2,"policyKey":"MANUAL_PAYMENT_REVIEW","transactionType":"ManualPaymentReview",
        "approvalType":"SingleApprover","independentCreatorApprover":true,"independentRecorderConfirmer":true,"maximumByCurrency":{"ZMW":"100.0000"}}
        """;

    [Fact]
    public void Publication_uses_payment_review_schema_instead_of_buying_rule_coverage()
    {
        var policy = new PolicyVersion { PolicyKey = ManualPaymentReviewPolicyRules.PolicyKey, RulesJson = Rules };
        Assert.NotEmpty(PolicyRulePreview.AnalyzeCoverage(Rules));
        Assert.Empty(ProcurementPolicyDraftRoutes.PublicationIssues(policy));
    }

    [Fact]
    public void Incomplete_draft_limits_cannot_be_published()
    {
        var unfinished = Rules.Replace(",\"maximumByCurrency\":{\"ZMW\":\"100.0000\"}", "");
        Assert.Empty(ManualPaymentReviewPolicyRules.Validate(unfinished, draft: true));
        Assert.NotEmpty(ProcurementPolicyDraftRoutes.PublicationIssues(new PolicyVersion
            { PolicyKey = ManualPaymentReviewPolicyRules.PolicyKey, RulesJson = unfinished }));
    }
}
