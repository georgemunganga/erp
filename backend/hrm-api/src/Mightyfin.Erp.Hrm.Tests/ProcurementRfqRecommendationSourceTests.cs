using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqRecommendationSourceTests
{
    [Fact]
    public void Verified_source_projection_keeps_command_ids_separate_from_neutral_labels()
    {
        var line1=Guid.NewGuid(); var line2=Guid.NewGuid();
        var offer1=Guid.NewGuid(); var offer2=Guid.NewGuid();
        PrincipalRfqCommercialOfferSubmissionRecord Offer(Guid id,Guid supplier,Guid line,string quantity)
            =>new(){Id=id,SupplierId=supplier,SnapshotSha256=new string('a',64),
                SnapshotJson=JsonSerializer.Serialize(new {currencyCode="USD",
                    lines=new[]{new{SourcingLineId=line,quantityExact=quantity}}})};
        var body=ProcurementSourcingExecutionRoutes.RecommendationSourceBody(
            Guid.NewGuid(),Guid.NewGuid(),"Verified RFQ title",[line1,line2],
            [Offer(offer1,Guid.NewGuid(),line2,"0.25"),
             Offer(offer2,Guid.NewGuid(),line1,"0.75")]);
        using var json=JsonDocument.Parse(body);
        var root=json.RootElement;
        Assert.Equal("Verified RFQ title",root.GetProperty("rfqTitle").GetString());
        Assert.Equal("Line 1",root.GetProperty("sourceLines")[0].GetProperty("lineLabel").GetString());
        Assert.Equal("Line 2",root.GetProperty("sourceLines")[1].GetProperty("lineLabel").GetString());
        var offers=root.GetProperty("offers");
        Assert.Equal(offer1,offers[0].GetProperty("offerId").GetGuid());
        Assert.Equal("Offer 1",offers[0].GetProperty("offerLabel").GetString());
        Assert.Equal(JsonValueKind.Null,offers[0].GetProperty("supplierLabel").ValueKind);
        Assert.Equal("Line 2",offers[0].GetProperty("lines")[0].GetProperty("lineLabel").GetString());
        Assert.Equal("0.25",offers[0].GetProperty("lines")[0].GetProperty("offeredQuantityExact").GetString());
        Assert.Equal("Offer 2",offers[1].GetProperty("offerLabel").GetString());
        Assert.DoesNotContain("supplierId",body,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice",body,StringComparison.OrdinalIgnoreCase);
        Assert.False(root.GetProperty("awardAvailable").GetBoolean());
    }

    [Fact]
    public void Offer_line_outside_verified_original_source_is_rejected_before_a_response()
    {
        var offer=new PrincipalRfqCommercialOfferSubmissionRecord
        {Id=Guid.NewGuid(),SupplierId=Guid.NewGuid(),SnapshotSha256=new string('a',64),
            SnapshotJson=JsonSerializer.Serialize(new{currencyCode="USD",
                lines=new[]{new{SourcingLineId=Guid.NewGuid(),quantityExact="1"}}})};
        var error=Assert.Throws<ProcurementSourcingExecutionRoutes.Problem>(()=>
            ProcurementSourcingExecutionRoutes.RecommendationSourceBody(
                Guid.NewGuid(),Guid.NewGuid(),"RFQ",[Guid.NewGuid()],[offer]));
        Assert.Equal(409,error.Status);
    }
}
