using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqOfferOpeningAdmissionTests
{
    [Fact]
    public void Opening_and_reader_are_separate_empty_bootstrap_grants_and_default_off()
    {
        Assert.False(new ProcurementRfqCommercialOptions().Enabled);
        Assert.False(new ProcurementRfqCommercialOptions().OfferOpeningEnabled);
        foreach (var role in new[] { "procurement_buyer", "procurement_manager", "finance_approver", "employee", "erp_admin" })
        {
            Assert.DoesNotContain(PrincipalRfqOfferOpeningControls.OpenAction, ProcurementActionCatalog.ForRole(role));
            Assert.DoesNotContain(PrincipalRfqOfferOpeningControls.ReadAction, ProcurementActionCatalog.ForRole(role));
        }
    }
    [Fact]
    public void Purpose_metadata_discovery_fences_named_reader_and_selected_branch_before_paging()
    {
        var sql = ProcurementSourcingExecutionRoutes.CommercialOpenedRoundScopeSql;
        Assert.Contains("r.tenant_id=@tenant AND r.legal_entity_id=@company", sql);
        Assert.Contains("@selected IS NULL OR r.branch_id=@selected", sql);
        Assert.Contains("@orgwide OR r.branch_id=ANY(@branches)", sql);
        Assert.Contains("'ReaderPrincipalIds'", sql);
        Assert.Contains("jsonb_build_array(@principal::text)", sql);
        Assert.Contains("u.approved_round_sha256=r.snapshot_sha256", sql);
        Assert.DoesNotContain("snapshot_json AS", sql);
        Assert.DoesNotContain("supplier_bid", sql);
        Assert.DoesNotContain("unit_price", sql);
    }

}
