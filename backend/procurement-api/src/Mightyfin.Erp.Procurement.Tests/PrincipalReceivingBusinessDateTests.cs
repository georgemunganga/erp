using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    private static PrincipalReceivingAcceptance ServiceAt(PrincipalReceivingSource source, DateTimeOffset at,
        PrincipalReceivingBusinessDate businessDate, DateOnly to)
    {
        var policy=Policy(source);
        return PrincipalControlledReceiving.CertifyService(Empty(source,policy),policy,Actor(Inspector),
            Guid.NewGuid(),Guid.NewGuid(),0,0.5m,"Completed milestone",to,to,"REC:service",at,businessDate);
    }

    [Fact]
    public void Lusaka_business_day_ahead_of_UTC_allows_completed_local_day()
    {
        var source=Source(ReceivingKind.Service);
        var at=DateTimeOffset.Parse("2026-10-22T22:30:00Z");
        var witness=BusinessDate(source,at,120,"Africa/Lusaka");
        var fact=ServiceAt(source,at,witness,new DateOnly(2026,10,23));
        Assert.Equal(new DateOnly(2026,10,23),fact.BusinessDateObservation!.CompanyBusinessDate);
        Assert.Equal(new DateOnly(2026,10,23),fact.PeriodTo);
    }

    [Fact]
    public void Negative_offset_business_day_behind_UTC_blocks_future_service_period()
    {
        var source=Source(ReceivingKind.Service);
        var at=DateTimeOffset.Parse("2026-10-23T02:30:00Z");
        var witness=BusinessDate(source,at,-300,"America/New_York");
        Assert.Equal(new DateOnly(2026,10,22),witness.CompanyBusinessDate);
        Assert.Throws<InvalidOperationException>(()=>ServiceAt(source,at,witness,new DateOnly(2026,10,23)));
    }

    [Fact]
    public void Witness_must_match_source_instant_local_date_and_bounded_timezone()
    {
        var source=Source(ReceivingKind.Service);var at=At;var good=BusinessDate(source);
        PrincipalReceivingBusinessDate[] invalid=
        [
            good with{SchemaVersion=2},good with{TenantId="other"},good with{LegalEntityId=Guid.NewGuid()},
            good with{ObservedAtUtc=at.AddMinutes(-1)},good with{ObservedAtUtc=at.ToOffset(TimeSpan.FromHours(2))},
            good with{CompanyBusinessDate=good.CompanyBusinessDate.AddDays(1)},
            good with{UtcOffsetMinutes=841},good with{UtcOffsetMinutes=-841},
            good with{CompanyTimeZoneId=" "},good with{CompanyTimeZoneId=new string('x',129)}
        ];
        foreach(var witness in invalid)
            Assert.Throws<InvalidOperationException>(()=>ServiceAt(source,at,witness,new DateOnly(2026,10,22)));
        var subMicrosecond=at.AddTicks(1);
        Assert.Throws<InvalidOperationException>(()=>ServiceAt(source,subMicrosecond,BusinessDate(source,subMicrosecond),new DateOnly(2026,10,22)));
    }

    [Fact]
    public void Historical_service_witness_is_retained_and_tampering_fails_without_current_timezone_lookup()
    {
        var source=Source(ReceivingKind.Service);var policy=Policy(source);var ledger=Empty(source,policy);
        var at=DateTimeOffset.Parse("2026-10-22T22:30:00Z");
        var witness=BusinessDate(source,at,120,"Africa/Lusaka");
        var fact=PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),0,
            0.5m,"Completed milestone",new(2026,10,23),new(2026,10,23),"REC:service",at,witness);
        var retained=Add(ledger,fact);
        Assert.Equal(0.5m,PrincipalControlledReceiving.Balance(retained).AcceptedQuantity);
        // The retained offset is the evidence. A later change to system timezone is irrelevant.
        foreach(var changed in new[]{witness with{UtcOffsetMinutes=-300},witness with{CompanyBusinessDate=new DateOnly(2026,10,22)},
            witness with{ObservedAtUtc=at.AddMinutes(1)},witness with{TenantId="other"}})
            Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(
                retained with{Acceptances=[fact with{BusinessDateObservation=changed}]}));
    }

    [Fact]
    public void Legacy_service_and_goods_envelopes_keep_null_witness_out_of_the_digest()
    {
        var service=Source(ReceivingKind.Service);var policy=Policy(service);var ledger=Empty(service,policy);
        var current=PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),0,
            0.5m,"Completed milestone",new(2026,10,22),new(2026,10,22),"REC:service",At,BusinessDate(service));
        var currentJson=PrincipalReceivingStorageCodec.Encode(current);
        Assert.Contains("BusinessDateObservation",currentJson);
        var legacy=current with{BusinessDateObservation=null};
        var oldJson=PrincipalReceivingStorageCodec.Encode(legacy);
        Assert.DoesNotContain("BusinessDateObservation",oldJson);
        var decoded=PrincipalReceivingStorageCodec.Decode<PrincipalReceivingAcceptance>(oldJson,ProcurementEvidenceEncoding.Digest(oldJson));
        Assert.Null(decoded.BusinessDateObservation);
        Assert.Equal(oldJson,PrincipalReceivingStorageCodec.Encode(decoded));
        Assert.Equal(0.5m,PrincipalControlledReceiving.Balance(Add(ledger,decoded)).AcceptedQuantity);
        var goods=Source();var goodsPolicy=Policy(goods);var observed=Recorded(goods,goodsPolicy);
        var inspection=PrincipalControlledReceiving.Inspect(observed,goodsPolicy,Actor(Inspector),observed.Observations.Single().Id,Guid.NewGuid(),1,1m,"REC:inspection",At);
        observed=Add(observed,inspection);
        var accepted=PrincipalControlledReceiving.AcceptGoods(observed,goodsPolicy,Actor(Inspector),observed.Observations.Single().Id,Guid.NewGuid(),2,1m,"REC:accepted",At);
        var goodsJson=PrincipalReceivingStorageCodec.Encode(accepted);
        Assert.DoesNotContain("BusinessDateObservation",goodsJson);
        Assert.Equal(goodsJson,PrincipalReceivingStorageCodec.Encode(PrincipalReceivingStorageCodec.Decode<PrincipalReceivingAcceptance>(goodsJson,ProcurementEvidenceEncoding.Digest(goodsJson))));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(Add(observed,accepted with{BusinessDateObservation=BusinessDate(goods)})));
    }
}
