using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ExactDecimalArithmeticTests
{
    [Fact]
    public void Full_source_precision_product_is_not_rounded_to_fit_decimal()
    {
        const decimal value = 99999999999999.9999m;
        Assert.Throws<InvalidOperationException>(() => ExactDecimalArithmetic.Multiply(value, value));
        Assert.Equal(0.00000001m, ExactDecimalArithmetic.Multiply(0.0001m, 0.0001m));
        Assert.Equal(-0.00000001m, ExactDecimalArithmetic.Multiply(-0.0001m, 0.0001m));
    }

    [Fact]
    public void Large_sum_and_subtraction_fail_when_a_nonzero_fraction_would_be_lost()
    {
        const decimal whole = 1000000000000000000000000000m;
        Assert.Throws<InvalidOperationException>(() => ExactDecimalArithmetic.Add(whole, 0.0001m));
        Assert.Throws<InvalidOperationException>(() => ExactDecimalArithmetic.Subtract(whole, 0.0001m));
        Assert.Equal(0.0001m, ExactDecimalArithmetic.Sum(new[] { whole, 0.0001m, -whole }));
    }

    [Fact]
    public void Lossless_zero_normalization_and_coefficient_boundaries_are_supported()
    {
        Assert.Equal(decimal.MaxValue, ExactDecimalArithmetic.Multiply(decimal.MaxValue, 1.0000000000000000000000000000m));
        Assert.Equal(decimal.MinValue, ExactDecimalArithmetic.Add(decimal.MinValue, 0m));
        Assert.Throws<InvalidOperationException>(() => ExactDecimalArithmetic.Add(decimal.MaxValue, 1m));
        Assert.Equal(0m, ExactDecimalArithmetic.Multiply(0.0000000000000000000000000001m, 0m));
        Assert.Throws<InvalidOperationException>(() => ExactDecimalArithmetic.Multiply(0.0000000000000000000000000001m, 0.1m));
    }
}
