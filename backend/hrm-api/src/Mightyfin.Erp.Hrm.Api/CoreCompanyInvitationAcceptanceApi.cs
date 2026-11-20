using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record CompanyInvitationReview(Guid InvitationId, Guid CompanyId, string CompanyName, string Role, int Revision, DateTimeOffset ExpiresAt);
internal sealed record CompanyInvitationReviewResult(CompanyTeamReadStatus Status, CompanyInvitationReview? Review = null);

internal sealed class CoreCompanyInvitationAcceptanceApi(Func<string, HrmDbContext> contextFactory, CoreCompanyTeamApi team,
    TimeProvider clock, IDataProtector protection)
{
    private async Task<(CoreCompanyInvitation? Row, CoreCompanyTeamApi.Ready? Ready)> Resolve(
        InvitationRecipientObservation recipient, string token, CancellationToken cancellation)
    {
        if (!ProductInvitationIntentStore.Opaque(token) || !recipient.IsCurrent
            || !ProductInvitationRecipient.CanonicalEmail(recipient.VerifiedEmail)) return (null,null);
        var hash = ProductInvitationIntentStore.Hash(token);
        await using var db = contextFactory("untrusted-invitation-secret-lookup");
        var row = await db.CompanyInvitations.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
            x => x.TokenHash == hash && !x.IsArchived, cancellation);
        if (row is null || row.RecipientHash != ProductInvitationIntentStore.Hash(recipient.VerifiedEmail)
            || row.Issuer != ProductCommercialLicense.Issuer) return (null,null);
        var ready = await team.Resolve(new(row.Issuer,row.InviterSubject,row.InviterLinkId), row.LegalEntityId,cancellation);
        if (ready.Status != CompanyTeamReadStatus.Ok) return (null,ready);
        var binding = await db.WorkspaceBindings.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == row.TenantId && x.LegalEntityId == row.LegalEntityId && !x.IsArchived,cancellation);
        if (binding is null || binding.ClientId != recipient.ClientId || ready.Scope!.PrincipalId != row.InviterLocalUserId
            || (row.Role == CoreCompanyRoles.Administrator && ready.Scope.Role != CoreCompanyRoles.Owner)
            || !recipient.IsCurrent || !ready.License!.IsCurrent) return (null,null);
        return (row,ready);
    }
    internal async Task<CompanyInvitationReviewResult> ReviewAsync(InvitationRecipientObservation recipient, string token, CancellationToken cancellation)
    {
        try {
            var (row,ready) = await Resolve(recipient,token,cancellation);
            if (row is null || ready?.License is not { } license)
                return new(ready?.Status==CompanyTeamReadStatus.Unavailable ? CompanyTeamReadStatus.Unavailable : CompanyTeamReadStatus.Denied);
            if (row.State != "pending" || row.ExpiresAt <= clock.GetUtcNow()) return new(CompanyTeamReadStatus.Denied);
            await using var db = contextFactory(row.TenantId);
            var company = await db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.LegalEntityId && !x.IsArchived,cancellation);
            var current = await new CoreCompanyAccess(db,clock).ReadAsync(row.Issuer,row.InviterSubject,row.InviterLinkId,row.LegalEntityId,license,cancellation);
            var stillPending=await db.CompanyInvitations.AsNoTracking().AnyAsync(x=>x.Id==row.Id && x.LegalEntityId==row.LegalEntityId
                && !x.IsArchived && x.State=="pending" && x.Revision==row.Revision,cancellation);
            if (company is null || current is null || !current.MayManageTeam
                || (row.Role == CoreCompanyRoles.Administrator && !current.MayManageOwners) || !recipient.IsCurrent
                || !stillPending || row.ExpiresAt<=clock.GetUtcNow() || !license.IsCurrent)
                return new(CompanyTeamReadStatus.Denied);
            return new(CompanyTeamReadStatus.Ok,new(row.Id,row.LegalEntityId,company.RegisteredName,row.Role,row.Revision,row.ExpiresAt));
        } catch (DbException) { return new(CompanyTeamReadStatus.Unavailable); }
    }
    internal async Task<CompanyInvitationResult> AcceptAsync(InvitationRecipientObservation recipient, AcceptCompanyInvitation command, CancellationToken cancellation)
    {
        try {
            var (row,ready) = await Resolve(recipient,command.Token,cancellation);
            return row is null || ready?.License is not { } license ? new(ready?.Status == CompanyTeamReadStatus.Unavailable ? CompanyInvitationStatus.Unavailable : CompanyInvitationStatus.Denied)
                : await new CoreCompanyInvitations(contextFactory,clock,protection).AcceptAsync(recipient,command,license,cancellation);
        } catch (DbException) { return new(CompanyInvitationStatus.Unavailable); }
    }
}
