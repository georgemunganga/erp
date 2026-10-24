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

    [Fact]
    public async Task Recommendation_draft_is_a_distinct_unavailable_purpose_even_if_opened_offers_exist()
    {
        var options=new ProcurementRfqCommercialOptions{Enabled=true,OfferOpeningEnabled=true};
        Assert.False(options.RecommendationDraftEnabled);
        var provider=new UnavailableProcurementRfqRecommendationDraftReadiness();
        Assert.False(await provider.IsReadyAsync(null!,false,CancellationToken.None));
        Assert.False(await provider.IsReadyAsync(null!,true,CancellationToken.None));
    }
}
