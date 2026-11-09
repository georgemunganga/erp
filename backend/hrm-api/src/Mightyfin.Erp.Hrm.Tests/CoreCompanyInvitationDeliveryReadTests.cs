using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyInvitationDeliveryReadTests
{
    private static readonly DateTimeOffset Now=new(2026,11,9,12,0,0,TimeSpan.Zero);
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void PostgreSQL_metadata_query_translates_for_empty_and_populated_subject_pages(bool populated)
    {
        using var db=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=kuubu_session_test_control;Username=synthetic").Options,new FixedTenantAccessor("delivery-read"));
        var sql=CoreCompanyInvitationDeliveryRead.Query(db,populated?[Guid.NewGuid().ToString("D")]:[]).ToQueryString();
        Assert.Contains("payload_json",sql);Assert.Contains("LIMIT",sql);
    }
    private sealed class Fixture
    {
        internal readonly CoreCompanyInvitation Invitation=new(){TenantId="delivery-read",LegalEntityId=Guid.NewGuid(),
            Issuer=ProductCommercialLicense.Issuer,InviterSubject="42",ExpiresAt=Now.AddHours(48)};
        internal readonly CoreCompanyInvitationReceipt Receipt=new(){CommandKey=Guid.NewGuid(),Operation="issue",ResultState="pending",ResultRevision=1};
        internal readonly OutboxMessage Intent=new(){PublicId=Guid.NewGuid().ToString("D"),EventType=CompanyInvitationDeliveryStore.EventType,AvailableAt=Now.AddSeconds(10)};
        internal Fixture()
        {
            Receipt.TenantId=Invitation.TenantId;Receipt.LegalEntityId=Invitation.LegalEntityId;Receipt.InvitationId=Invitation.Id;
            Receipt.ActorIssuer=Invitation.Issuer;Receipt.ActorSubject=Invitation.InviterSubject;
            Intent.TenantId=Invitation.TenantId;Intent.SubjectId=Invitation.Id.ToString("D");Intent.CreatedBy=Invitation.Issuer+"#"+Invitation.InviterSubject;
            Intent.CorrelationId=ProductInvitationIntentStore.Hash(Receipt.CommandKey.ToString("D"));
            Intent.PayloadJson=JsonSerializer.Serialize(new{invitation_id=Invitation.Id,company_id=Invitation.LegalEntityId});
        }
        internal CompanyInvitationDeliveryView Read()=>CoreCompanyInvitationDeliveryRead.Read(Invitation,[Intent],[Receipt],Now);
        internal void Attempt(string state,int attempts,string? error)
        {Intent.Status=state;Intent.PublishAttempts=attempts;Intent.LastTransport="kuubu-smtp";Intent.LastError=error;}
    }
    [Fact]
    public void A_created_intent_is_queued_and_closing_the_invitation_makes_unsent_mail_unnecessary()
    {
        var f=new Fixture();Assert.Equal(new CompanyInvitationDeliveryView("queued",0),f.Read());
        f.Invitation.ExpiresAt=Now;Assert.Equal(new CompanyInvitationDeliveryView("not-required",0),f.Read());
        f.Invitation.ExpiresAt=Now.AddHours(48);f.Invitation.State="revoked";
        Assert.Equal(new CompanyInvitationDeliveryView("not-required",0),f.Read());
        f.Invitation.State="accepted";Assert.Equal(new CompanyInvitationDeliveryView("not-required",0),f.Read());
    }
    [Fact]
    public void Retry_is_a_schedule_and_SMTP_acceptance_is_historical_even_after_company_acceptance()
    {
        var f=new Fixture();f.Attempt("failed",2,"invitation-delivery-unconfirmed");
        Assert.Equal(new CompanyInvitationDeliveryView("retrying",2,RetryAt:f.Intent.AvailableAt),f.Read());
        f.Invitation.State="accepted";Assert.Equal(new CompanyInvitationDeliveryView("not-required",2),f.Read());
        f.Attempt("delivery-accepted",3,null);f.Intent.PublishedAt=Now;
        Assert.Equal(new CompanyInvitationDeliveryView("accepted",3,AcceptedAt:Now),f.Read());
    }
    [Theory]
    [InlineData("delivery-suppressed",1,"invitation-current-authority-denied","suppressed")]
    [InlineData("delivery-invalid",2,"invitation-delivery-invalid","invalid")]
    [InlineData("delivery-exhausted",8,"invitation-delivery-unconfirmed","exhausted")]
    public void Terminal_delivery_outcomes_are_not_company_acceptance(string status,int attempts,string error,string expected)
    {
        var f=new Fixture();f.Attempt(status,attempts,error);
        Assert.Equal(new CompanyInvitationDeliveryView(expected,attempts),f.Read());Assert.Equal("pending",f.Invitation.State);
    }
    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("subject")][InlineData("actor")][InlineData("correlation")]
    [InlineData("receipt-tenant")][InlineData("receipt-company")][InlineData("receipt-actor")][InlineData("receipt-operation")]
    [InlineData("version")][InlineData("event")][InlineData("payload-extra")][InlineData("payload-duplicate")]
    [InlineData("archived")][InlineData("issuer")][InlineData("pending-attempt")][InlineData("pending-published")]
    [InlineData("generic-published")][InlineData("unconfirmed-acceptance")][InlineData("transport")]
    [InlineData("error")][InlineData("attempt-overflow")][InlineData("premature-exhaustion")]
    public void Inconsistent_or_foreign_evidence_never_claims_mail_acceptance(string fault)
    {
        var f=new Fixture();
        switch(fault){
            case "tenant":f.Intent.TenantId="foreign";break;
            case "company":f.Intent.PayloadJson=JsonSerializer.Serialize(new{invitation_id=f.Invitation.Id,company_id=Guid.NewGuid()});break;
            case "subject":f.Intent.SubjectId=Guid.NewGuid().ToString("D");break;
            case "actor":f.Intent.CreatedBy="other-actor";break;
            case "correlation":f.Intent.CorrelationId=new string('0',64);break;
            case "receipt-tenant":f.Receipt.TenantId="foreign";break;
            case "receipt-company":f.Receipt.LegalEntityId=Guid.NewGuid();break;
            case "receipt-actor":f.Receipt.ActorSubject="43";break;
            case "receipt-operation":f.Receipt.Operation="accept";break;
            case "version":f.Intent.EventVersion="2";break;
            case "event":f.Intent.EventType="other.v1";break;
            case "payload-extra":f.Intent.PayloadJson=f.Intent.PayloadJson[..^1]+",\"recipient\":\"hidden@example.test\"}";break;
            case "payload-duplicate":f.Intent.PayloadJson=f.Intent.PayloadJson[..^1]+",\"invitation_id\":\""+f.Invitation.Id+"\"}";break;
            case "archived":f.Invitation.IsArchived=true;break;
            case "issuer":f.Invitation.Issuer="other-issuer";break;
            case "pending-attempt":f.Intent.PublishAttempts=1;break;
            case "pending-published":f.Intent.PublishedAt=Now;break;
            case "generic-published":f.Attempt("published",1,null);f.Intent.PublishedAt=Now;break;
            case "unconfirmed-acceptance":f.Attempt("delivery-accepted",1,null);break;
            case "transport":f.Attempt("delivery-accepted",1,null);f.Intent.PublishedAt=Now;f.Intent.LastTransport="legacy";break;
            case "error":f.Attempt("failed",1,"private transport diagnostic");break;
            case "attempt-overflow":f.Attempt("delivery-accepted",9,null);f.Intent.PublishedAt=Now;break;
            case "premature-exhaustion":f.Attempt("delivery-exhausted",7,"invitation-delivery-unconfirmed");break;
        }
        Assert.Equal(CompanyInvitationDeliveryView.Unavailable,f.Read());
    }
    [Fact]
    public void Missing_and_duplicate_evidence_is_unavailable_with_no_transport_details()
    {
        var f=new Fixture();
        Assert.Equal(CompanyInvitationDeliveryView.Unavailable,CoreCompanyInvitationDeliveryRead.Read(f.Invitation,[],[f.Receipt],Now));
        Assert.Equal(CompanyInvitationDeliveryView.Unavailable,CoreCompanyInvitationDeliveryRead.Read(f.Invitation,[f.Intent,f.Intent],[f.Receipt],Now));
        Assert.Equal(CompanyInvitationDeliveryView.Unavailable,CoreCompanyInvitationDeliveryRead.Read(f.Invitation,[f.Intent],[],Now));
        Assert.Equal(CompanyInvitationDeliveryView.Unavailable,CoreCompanyInvitationDeliveryRead.Read(f.Invitation,[f.Intent],[f.Receipt,f.Receipt],Now));
    }
}
