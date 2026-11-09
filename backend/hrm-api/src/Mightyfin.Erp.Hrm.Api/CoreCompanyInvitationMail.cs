using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record InvitationMailEnvelope(string Recipient,string Company,string Role,string ReviewUrl,
    string MessageId,DateTimeOffset ExpiresAt,Func<bool> IsCurrent)
{ public override string ToString()=>nameof(InvitationMailEnvelope); }

internal interface ICompanyInvitationMail
{ Task SendAsync(InvitationMailEnvelope envelope,CancellationToken cancellation); }

/// <summary>Confidential envelope resolution. The queue carries IDs, never a mail address/link.</summary>
internal sealed class CoreCompanyInvitationMail(Func<string,HrmDbContext> contextFactory,CoreCompanyTeamApi team,
    IDataProtector protection,ICompanyInvitationMail transport,TimeProvider clock,string clientId)
{
    internal async Task<InvitationMailOutcome> DeliverAsync(OutboxMessage intent,CancellationToken cancellation)
    {
        if(intent.EventType!=CompanyInvitationDeliveryStore.EventType || intent.EventVersion!="1"
            || intent.IsArchived || !Guid.TryParseExact(intent.PublicId,"D",out var eventId) || eventId==Guid.Empty
            || intent.PayloadJson.Length>1024)return InvitationMailOutcome.Invalid;
        Guid invitationId,companyId;
        try {
            using var packet=JsonDocument.Parse(intent.PayloadJson,new JsonDocumentOptions{MaxDepth=2});
            var root=packet.RootElement;ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            if(root.ValueKind!=JsonValueKind.Object || root.EnumerateObject().Count()!=2)return InvitationMailOutcome.Invalid;
            invitationId=root.GetProperty("invitation_id").GetGuid();companyId=root.GetProperty("company_id").GetGuid();
            if(invitationId==Guid.Empty || companyId==Guid.Empty || intent.SubjectId!=invitationId.ToString("D"))return InvitationMailOutcome.Invalid;
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {return InvitationMailOutcome.Invalid;}
        await using var db=contextFactory(intent.TenantId);
        var row=await db.CompanyInvitations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==invitationId && x.LegalEntityId==companyId,cancellation);
        if(row is null || row.Issuer!=ProductCommercialLicense.Issuer || row.TenantId!=intent.TenantId)return InvitationMailOutcome.Invalid;
        if(row.IsArchived || row.State!="pending" || row.Revision!=1 || row.ExpiresAt<=clock.GetUtcNow())return InvitationMailOutcome.Suppressed;
        var issued=await db.CompanyInvitationReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.InvitationId==row.Id && x.Operation=="issue",cancellation);
        if(issued is null || issued.LegalEntityId!=companyId || issued.ActorIssuer!=row.Issuer
            || issued.ActorSubject!=row.InviterSubject || ProductInvitationIntentStore.Hash(issued.CommandKey.ToString("D"))!=intent.CorrelationId
            || intent.CreatedBy!=row.Issuer+"#"+row.InviterSubject)return InvitationMailOutcome.Invalid;
        if(!await db.WorkspaceBindings.AsNoTracking().AnyAsync(x=>x.LegalEntityId==companyId && !x.IsArchived
            && x.ClientId==clientId,cancellation))return InvitationMailOutcome.Suppressed;
        var ready=await team.Resolve(new(row.Issuer,row.InviterSubject,row.InviterLinkId),companyId,cancellation);
        if(ready.Status==CompanyTeamReadStatus.Unavailable)return InvitationMailOutcome.Retry;
        if(ready.Status!=CompanyTeamReadStatus.Ok || ready.Scope!.TenantId!=row.TenantId
            || ready.Scope.PrincipalId!=row.InviterLocalUserId
            || row.Role==CoreCompanyRoles.Administrator && ready.Scope.Role!=CoreCompanyRoles.Owner)
            return InvitationMailOutcome.Suppressed;
        var company=await db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==companyId && !x.IsArchived,cancellation);
        if(company is null)return InvitationMailOutcome.Suppressed;
        string recipient,link;
        try {
            using var packet=JsonDocument.Parse(protection.Unprotect(row.ProtectedDelivery),new JsonDocumentOptions{MaxDepth=2});
            var root=packet.RootElement;ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            if(root.ValueKind!=JsonValueKind.Object || root.EnumerateObject().Count()!=3
                || root.GetProperty("schema_version").GetInt32()!=1)return InvitationMailOutcome.Invalid;
            recipient=root.GetProperty("recipient").GetString()!;link=root.GetProperty("review_url").GetString()!;
            const string prefix="https://app.kuubuerp.com/accept-invitation#token=";
            if(!ProductInvitationRecipient.CanonicalEmail(recipient) || ProductInvitationIntentStore.Hash(recipient)!=row.RecipientHash
                || link is null || !link.StartsWith(prefix,StringComparison.Ordinal)
                || !ProductInvitationIntentStore.Opaque(link[prefix.Length..])
                || ProductInvitationIntentStore.Hash(link[prefix.Length..])!=row.TokenHash)return InvitationMailOutcome.Invalid;
        }catch(Exception error)when(error is CryptographicException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {return InvitationMailOutcome.Invalid;}
        // Re-read after remote licensing and decryption. Possession of a queued
        // event never replaces current inviter/company authority.
        await using var guard=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(cancellation):null;
        if(guard is not null) {
            var companyScope="core.company.team.v1\n"+row.TenantId+"\n"+companyId.ToString("D");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({companyScope}, 0))",cancellation);
        }
        var current=await new CoreCompanyAccess(db,clock).ReadAsync(row.Issuer,row.InviterSubject,row.InviterLinkId,companyId,ready.License!,cancellation);
        if(current is null || !current.MayManageTeam || current.PrincipalId!=row.InviterLocalUserId
            || row.Role==CoreCompanyRoles.Administrator && !current.MayManageOwners
            || !await db.CompanyInvitations.AsNoTracking().AnyAsync(x=>x.Id==row.Id && !x.IsArchived && x.State=="pending" && x.Revision==1,cancellation))
            return InvitationMailOutcome.Suppressed;
        var deadline=row.ExpiresAt<ready.License!.ExpiresAt?row.ExpiresAt:ready.License.ExpiresAt;
        bool IsCurrent()=>row.ExpiresAt>clock.GetUtcNow() && ready.License.IsCurrent;
        if(!IsCurrent())return InvitationMailOutcome.Suppressed;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(20,Math.Max(0,(deadline-clock.GetUtcNow()).TotalSeconds))));
        try {
            await transport.SendAsync(new(recipient,company.RegisteredName,row.Role,link,
                "<kuubu-invitation-"+intent.Id.ToString("D")+"@kuubuerp.com>",deadline,IsCurrent),timeout.Token);
            return InvitationMailOutcome.Accepted;
        }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
        catch(Exception){return InvitationMailOutcome.Retry;}
    }
}

internal sealed class SmtpCompanyInvitationMail:ICompanyInvitationMail
{
    private readonly string host,from,user,password;
    private readonly int port;
    private readonly TimeProvider clock;
    private readonly Func<MailMessage,CancellationToken,Task>? sendOverride;
    internal SmtpCompanyInvitationMail(IConfiguration config,TimeProvider clock,
        Func<MailMessage,CancellationToken,Task>? sendOverride=null)
    {
        this.clock=clock;this.sendOverride=sendOverride;
        string Value(string name)=>config["ERP:CompanyInvitationDelivery:Smtp:"+name]??"";
        host=Value("Host");from=Value("From");user=Value("Username");password=Value("Password");
        port=Value("Port").Length==0?587:int.TryParse(Value("Port"),out var configured)?configured:0;
        try {
            if(Uri.CheckHostName(host)!=UriHostNameType.Dns || IPAddress.TryParse(host,out _) || host.Any(char.IsWhiteSpace) || port is <1 or >65535
                || string.IsNullOrWhiteSpace(from) || from.Contains('\r') || from.Contains('\n')
                || new MailAddress(from).Address!=from || string.IsNullOrEmpty(user)!=string.IsNullOrEmpty(password))throw new FormatException();
        }catch(FormatException){throw new InvalidOperationException("Reviewed Kuubu SMTP settings are required.");}
    }
    public async Task SendAsync(InvitationMailEnvelope envelope,CancellationToken cancellation)
    {
        if(!envelope.IsCurrent() || envelope.ExpiresAt<=clock.GetUtcNow())throw new InvalidOperationException("Invitation delivery expired.");
        using var message=new MailMessage();message.From=new MailAddress(from,"Kuubu");message.To.Add(new MailAddress(envelope.Recipient));
        message.Subject="You are invited to join a company on Kuubu";
        var company=string.Concat(envelope.Company.Select(c=>char.IsControl(c)?' ':c));
        message.Body=$"You have been invited to join {company} as a {envelope.Role} on Kuubu.\n\nReview the invitation and choose whether to join:\n{envelope.ReviewUrl}\n\nSign in using the invited email address. Company acceptance and module permissions are separate. If you did not expect this invitation, you can ignore it.\n";
        message.BodyEncoding=Encoding.UTF8;message.SubjectEncoding=Encoding.UTF8;message.IsBodyHtml=false;
        message.Headers.Add("Message-ID",envelope.MessageId);
        cancellation.ThrowIfCancellationRequested();
        if(!envelope.IsCurrent())throw new InvalidOperationException("Invitation delivery expired.");
        if(sendOverride is not null){await sendOverride(message,cancellation);return;}
        using var client=new SmtpClient(host,port){EnableSsl=true,UseDefaultCredentials=false,Timeout=20000};
        if(user.Length>0)client.Credentials=new NetworkCredential(user,password);
        // The pinned SmtpClient has a synchronous final acknowledgement read.
        // Cancelling its token alone does not interrupt that read promptly.
        // Dispose aborts an active connection; never leave it holding Core locks.
        using var abort=cancellation.UnsafeRegister(static state=>{
            try{((SmtpClient)state!).Dispose();}catch(Exception){} // No callback exception/transport detail escapes cancellation.
        },client);
        cancellation.ThrowIfCancellationRequested();
        await client.SendMailAsync(message,cancellation);
        cancellation.ThrowIfCancellationRequested();
    }
}
