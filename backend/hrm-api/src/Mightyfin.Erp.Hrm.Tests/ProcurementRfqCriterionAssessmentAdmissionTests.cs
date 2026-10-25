using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

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
    [Fact]
    public void Recommendation_receipt_purpose_does_not_survive_removed_named_reader()
    {
        var reader=Guid.NewGuid().ToString("D");
        var other=Guid.NewGuid().ToString("D");
        var controls=new PrincipalRfqOfferOpeningControls(other,[reader],false,0,false,false);
        ProcurementSourcingExecutionRoutes.RequireRecommendationNamedReader(controls,reader);
        // The same original receipt owner and action are insufficient once the
        // supplied verified reader set excludes that principal. This pure gate
        // does not mutate policy or attest a real HTTP/PostgreSQL recovery.
        var removed=controls with {ReaderPrincipalIds=[other]};
        var error=Assert.Throws<ProcurementSourcingExecutionRoutes.Problem>(()=>
            ProcurementSourcingExecutionRoutes.RequireRecommendationNamedReader(removed,reader));
        Assert.Equal(403,error.Status);
        Assert.Equal("RfqRecommendationReaderDenied",error.Message);
        Assert.Equal(new[]{reader},controls.ReaderPrincipalIds);
    }

}
