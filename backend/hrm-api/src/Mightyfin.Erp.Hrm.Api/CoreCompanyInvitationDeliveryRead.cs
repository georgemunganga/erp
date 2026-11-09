using System.Text.Json;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record CompanyInvitationDeliveryView(string State,int Attempts,DateTimeOffset? RetryAt=null,DateTimeOffset? AcceptedAt=null)
{
    internal static readonly CompanyInvitationDeliveryView Unavailable=new("unavailable",0);
}

/// <summary>Read-only mail outcome. It is never membership or inbox confirmation.</summary>
internal static class CoreCompanyInvitationDeliveryRead
{
    internal static CompanyInvitationDeliveryView Read(CoreCompanyInvitation invitation,OutboxMessage[] intents,
        CoreCompanyInvitationReceipt[] receipts,DateTimeOffset now)
    {
        var unknown=CompanyInvitationDeliveryView.Unavailable;
        if(intents.Length!=1 || receipts.Length!=1)return unknown;
        var row=intents[0];var issued=receipts[0];
        if(invitation.IsArchived || invitation.Issuer!=ProductCommercialLicense.Issuer || row.IsArchived || issued.IsArchived || row.TenantId!=invitation.TenantId || issued.TenantId!=invitation.TenantId
            || row.EventType!=CompanyInvitationDeliveryStore.EventType || row.EventVersion!="1"
            || !Guid.TryParseExact(row.PublicId,"D",out var eventId) || eventId==Guid.Empty
            || row.SubjectId!=invitation.Id.ToString("D") || row.CreatedBy!=invitation.Issuer+"#"+invitation.InviterSubject
            || issued.InvitationId!=invitation.Id || issued.LegalEntityId!=invitation.LegalEntityId
            || issued.ActorIssuer!=invitation.Issuer || issued.ActorSubject!=invitation.InviterSubject
            || issued.Operation!="issue" || issued.ResultState!="pending" || issued.ResultRevision!=1 || issued.CommandKey==Guid.Empty
            || row.CorrelationId!=ProductInvitationIntentStore.Hash(issued.CommandKey.ToString("D"))
            || row.PayloadJson is null || row.PayloadJson.Length>1024)return unknown;
        try {
            using var packet=JsonDocument.Parse(row.PayloadJson,new JsonDocumentOptions{MaxDepth=2});
            var payload=packet.RootElement;ProductWorkspaceSetupAuthority.RejectDuplicates(payload);
            if(payload.ValueKind!=JsonValueKind.Object || payload.EnumerateObject().Count()!=2
                || payload.GetProperty("invitation_id").GetGuid()!=invitation.Id
                || payload.GetProperty("company_id").GetGuid()!=invitation.LegalEntityId)return unknown;
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return unknown;}
        var closed=invitation.State!="pending" || invitation.ExpiresAt<=now;
        if(row.Status=="pending")return row.PublishAttempts==0 && row.PublishedAt is null && row.LastTransport is null && row.LastError is null
            ? new(closed?"not-required":"queued",0):unknown;
        if(row.PublishAttempts is <1 or >8 || row.LastTransport!="kuubu-smtp")return unknown;
        if(row.Status=="delivery-accepted")return row.PublishedAt is not null && row.LastError is null
            ? new("accepted",row.PublishAttempts,AcceptedAt:row.PublishedAt):unknown;
        if(row.PublishedAt is not null)return unknown;
        return row.Status switch {
            "failed" when row.PublishAttempts<8 && row.LastError=="invitation-delivery-unconfirmed"=>
                new(closed?"not-required":"retrying",row.PublishAttempts,RetryAt:closed?null:row.AvailableAt),
            "delivery-exhausted" when row.PublishAttempts==8 && row.LastError=="invitation-delivery-unconfirmed"=>new("exhausted",8),
            "delivery-suppressed" when row.LastError=="invitation-current-authority-denied"=>new("suppressed",row.PublishAttempts),
            "delivery-invalid" when row.LastError=="invitation-delivery-invalid"=>new("invalid",row.PublishAttempts),
            _=>unknown
        };
    }
}
