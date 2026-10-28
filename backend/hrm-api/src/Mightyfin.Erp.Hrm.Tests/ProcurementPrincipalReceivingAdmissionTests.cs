using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalReceivingAdmissionTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, true, true)]
    public void Read_recovery_remains_available_when_new_work_is_stopped(
        bool connection, bool enabled, bool write, bool expected)
    {
        var options = new ProcurementPrincipalReceivingOptions
        { ExecutionDatabaseConfigured = connection, Enabled = enabled };
        Assert.Equal(expected, ProcurementPrincipalReceivingCapability.Configured(options, write));
    }

    [Fact]
    public void Missing_options_never_admit_reads_or_writes()
    {
        Assert.False(ProcurementPrincipalReceivingCapability.Configured(null, false));
        Assert.False(ProcurementPrincipalReceivingCapability.Configured(null, true));
    }
}
