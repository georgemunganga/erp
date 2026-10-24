using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqCriterionAssessmentAdmissionTests
{
    [Fact]
    public async Task Assessment_has_separate_default_off_option_and_unavailable_owner_provider()
    {
        Assert.False(new ProcurementRfqCommercialOptions().CriterionAssessmentEnabled);
        Assert.False(new ProcurementRfqCommercialOptions().OfferOpeningEnabled);
        var provider=new UnavailableProcurementRfqCriterionAssessmentReadiness();
        Assert.False(await provider.IsReadyAsync(null!,false,CancellationToken.None));
        Assert.False(await provider.IsReadyAsync(null!,true,CancellationToken.None));
    }
}
