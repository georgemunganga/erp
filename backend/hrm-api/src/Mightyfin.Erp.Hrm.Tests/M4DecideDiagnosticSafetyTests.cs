using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M4DecideDiagnosticSafetyTests
{
    [Theory]
    [InlineData("admission")]
    [InlineData("opening-and-core")]
    [InlineData("core-decision")]
    [InlineData("policy-revalidation")]
    [InlineData("approved-source")]
    [InlineData("finance")]
    [InlineData("supplier")]
    [InlineData("core-save")]
    [InlineData("record-save")]
    public void Only_fixed_decision_stage_names_are_logged(string stage)
        => Assert.Equal(stage, ProcurementSourcingExecutionRoutes.M4DecideSafeStage(stage));

    [Fact]
    public void Dynamic_stage_and_exception_message_are_not_logged()
    {
        Assert.Equal("unknown", ProcurementSourcingExecutionRoutes.M4DecideSafeStage(null));
        Assert.Equal("unknown", ProcurementSourcingExecutionRoutes.M4DecideSafeStage("buyer@example.invalid"));
        Assert.Equal("OtherInvalidOperation", ProcurementSourcingExecutionRoutes.M4DecideSafeCode(
            new InvalidOperationException("customer payload buyer@example.invalid")));
    }

    [Theory]
    [InlineData("RfqPrincipalHistoryChanged")]
    [InlineData("SourcingPublicationDecisionInvalid")]
    [InlineData("FinanceEvidenceExpired")]
    [InlineData("Load the persisted approval case with all stages and assignments before recording a decision.")]
    public void Only_exact_static_refusal_codes_are_logged(string code)
        => Assert.Equal(code, ProcurementSourcingExecutionRoutes.M4DecideSafeCode(new InvalidOperationException(code)));
}
