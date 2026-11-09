using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record CompanyInvitationView(Guid Id, string Recipient, string Role, string State, int Revision, DateTimeOffset ExpiresAt,
    CompanyInvitationDeliveryView Delivery);

/// <summary>Licensed sender-side orchestration. Never sends email or exposes invitation secrets.</summary>
internal sealed class CoreCompanyInvitationsApi(Func<string, HrmDbContext> contextFactory, CoreCompanyTeamApi team,
    TimeProvider clock, IDataProtector protection)
{
    internal const string DeliveryPurpose = "kuubu-company-invitation-delivery-v1";
    internal async Task<CompanyTeamPage<CompanyInvitationView>> ListAsync(CompanyTeamActor actor, Guid company,
        Guid? after, CancellationToken cancellation = default)
    {
        try {
            var ready = await team.Resolve(actor, company, cancellation);
            if (ready.Status != CompanyTeamReadStatus.Ok) return new(ready.Status);
            await using var db = contextFactory(ready.Scope!.TenantId);
            var query = db.CompanyInvitations.AsNoTracking().Where(x => x.LegalEntityId == company && !x.IsArchived);
            if (after is { } cursor) {
                var text = cursor.ToString("D");
                query = query.Where(x => string.Compare(x.Id.ToString().ToLower(), text) > 0);
            }
            var scanned = await query.OrderBy(x => x.Id.ToString().ToLower()).Take(21).ToArrayAsync(cancellation);
            var ids=scanned.Take(20).Select(x=>x.Id).ToArray();var subjects=ids.Select(x=>x.ToString("D")).ToArray();
            var intents=await CoreCompanyInvitationDeliveryRead.Query(db,subjects).ToArrayAsync(cancellation);
            var issued=await db.CompanyInvitationReceipts.AsNoTracking().Where(x=>ids.Contains(x.InvitationId) && x.Operation=="issue")
                .Take(21).ToArrayAsync(cancellation);
            var overflow=intents.Length>ids.Length || issued.Length>ids.Length;
            var rows = scanned.Take(20).Select(row => {
                using var packet = JsonDocument.Parse(protection.Unprotect(row.ProtectedDelivery));
                ProductWorkspaceSetupAuthority.RejectDuplicates(packet.RootElement);
                var email = packet.RootElement.GetProperty("recipient").GetString();
                if (packet.RootElement.GetProperty("schema_version").GetInt32() != 1 || !ProductInvitationRecipient.CanonicalEmail(email)
                    || Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email!))) != row.RecipientHash)
                    throw new JsonException();
                return new CompanyInvitationView(row.Id, email!, row.Role,
                    row.State == "pending" && row.ExpiresAt <= clock.GetUtcNow() ? "expired" : row.State, row.Revision, row.ExpiresAt,
                    overflow?CompanyInvitationDeliveryView.Unavailable:CoreCompanyInvitationDeliveryRead.Read(row,
                        intents.Where(x=>x.SubjectId==row.Id.ToString("D")).ToArray(),issued.Where(x=>x.InvitationId==row.Id).ToArray(),clock.GetUtcNow()));
            }).ToArray();
            var current = await new CoreCompanyAccess(db, clock).ReadAsync(actor.Issuer, actor.Subject, actor.LinkId, company, ready.License!, cancellation);
            return current is not null && current.MayManageTeam
                ? new(CompanyTeamReadStatus.Ok, rows, scanned.Length > 20 ? rows[^1].Id.ToString("D") : null, current.Role)
                : new(CompanyTeamReadStatus.Denied);
        }
        catch (Exception error) when (error is DbException or CryptographicException or JsonException or InvalidOperationException or KeyNotFoundException)
        { return new(CompanyTeamReadStatus.Unavailable); }
    }
    internal async Task<CompanyInvitationResult> IssueAsync(CompanyTeamActor actor, IssueCompanyInvitation command, CancellationToken cancellation = default)
    {
        try {
            var ready = await team.Resolve(actor, command.CompanyId, cancellation);
            return ready.Status == CompanyTeamReadStatus.Ok
            ? await new CoreCompanyInvitations(contextFactory, clock, protection).IssueAsync(actor, command, ready.License!, cancellation)
                : new(ready.Status == CompanyTeamReadStatus.Denied ? CompanyInvitationStatus.Denied : CompanyInvitationStatus.Unavailable);
        } catch (DbException) { return new(CompanyInvitationStatus.Unavailable); }
    }
    internal async Task<CompanyInvitationResult> RevokeAsync(CompanyTeamActor actor, RevokeCompanyInvitation command, CancellationToken cancellation = default)
    {
        try {
            var ready = await team.Resolve(actor, command.CompanyId, cancellation);
            return ready.Status == CompanyTeamReadStatus.Ok
            ? await new CoreCompanyInvitations(contextFactory, clock, protection).RevokeAsync(actor, command, ready.License!, cancellation)
                : new(ready.Status == CompanyTeamReadStatus.Denied ? CompanyInvitationStatus.Denied : CompanyInvitationStatus.Unavailable);
        } catch (DbException) { return new(CompanyInvitationStatus.Unavailable); }
    }
}
