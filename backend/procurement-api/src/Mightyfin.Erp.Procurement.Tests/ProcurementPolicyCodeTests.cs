using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ProcurementPolicyCodeTests
{
    [Theory]
    [InlineData(" Goods ", "GOODS")]
    [InlineData("office_it_2", "OFFICE_IT_2")]
    [InlineData(null, null)]
    [InlineData(" \t ", null)]
    public void Draft_boundary_normalizes_only_valid_optional_ascii_codes(string? input, string? expected)
    {
        Assert.True(ProcurementPolicyCode.TryNormalizeOptional(input, out var code));
        Assert.Equal(expected, code);
        Assert.Equal(expected, ProcurementPolicyCode.NormalizeOptional(input));
    }

    [Theory]
    [InlineData("OFFICE IT")]
    [InlineData("OFFICE-IT")]
    [InlineData("IT/OPS")]
    [InlineData("ſervice")]
    [InlineData("ＧＯＯＤＳ")]
    public void Unsupported_representations_fail_without_synonym_or_unicode_conversion(string input)
    {
        Assert.False(ProcurementPolicyCode.TryNormalizeOptional(input, out var code));
        Assert.Null(code);
        Assert.Throws<ArgumentException>(() => ProcurementPolicyCode.NormalizeOptional(input));
    }

    [Fact]
    public void Policy_validation_remains_strict_and_boundary_length_is_measured_after_trim()
    {
        Assert.False(ProcurementPolicyCode.IsValid("Goods"));
        Assert.False(ProcurementPolicyCode.IsValid(" GOODS "));
        Assert.False(ProcurementPolicyCode.IsValid(null));
        Assert.False(ProcurementPolicyCode.IsValid(""));
        var maximum = new string('a', 100);
        Assert.True(ProcurementPolicyCode.TryNormalizeOptional(" " + maximum + " ", out var code));
        Assert.Equal(maximum.ToUpperInvariant(), code);
        Assert.True(ProcurementPolicyCode.IsValid(code));
        Assert.False(ProcurementPolicyCode.TryNormalizeOptional(maximum + "a", out _));
    }
}
