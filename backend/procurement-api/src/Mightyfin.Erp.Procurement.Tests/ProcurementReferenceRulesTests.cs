using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ProcurementReferenceRulesTests
{
    [Theory]
    [InlineData("IT-EQUIPMENT")]
    [InlineData("OFFICE_01")]
    [InlineData("A")]
    public void Accepts_stable_reference_codes(string code) =>
        Assert.Null(ProcurementReferenceRules.ValidateCode(code));

    [Theory]
    [InlineData("")]
    [InlineData("-BAD")]
    [InlineData("name with spaces")]
    [InlineData("code.with.dot")]
    public void Rejects_ambiguous_reference_codes(string code) =>
        Assert.NotNull(ProcurementReferenceRules.ValidateCode(code));

    [Fact]
    public void Requires_a_nonempty_name() =>
        Assert.NotNull(ProcurementReferenceRules.ValidateName(" "));
}
