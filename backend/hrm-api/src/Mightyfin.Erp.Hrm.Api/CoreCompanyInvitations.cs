using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum CompanyInvitationStatus { Issued, Revoked, Accepted, Existing, Invalid, Denied, Conflict, Expired, Unavailable }
internal sealed record CompanyInvitationResult(CompanyInvitationStatus Status, Guid? InvitationId = null,
    string? State = null, int? Revision = null, Guid? MembershipId = null);
internal sealed record IssueCompanyInvitation(Guid CompanyId, Guid CommandKey, string Email, string Role, bool Confirm)
{
    public override string ToString() => nameof(IssueCompanyInvitation);
}
internal sealed record RevokeCompanyInvitation(Guid CompanyId, Guid InvitationId, Guid CommandKey, int ExpectedRevision, bool Confirm);
internal sealed record AcceptCompanyInvitation(string Token, Guid CommandKey, int ExpectedRevision, bool Confirm)
{
    public override string ToString() => nameof(AcceptCompanyInvitation);
}

/// <summary>Explicit, atomic company governance. No mail sending, module grants, local credentials or ERP sessions.</summary>
internal sealed class CoreCompanyInvitations(Func<string, HrmDbContext> contextFactory, TimeProvider clock, IDataProtector deliveryProtection, bool accountHub = false)
{
    internal async Task<CompanyInvitationResult> IssueAsync(CompanyTeamActor actor, IssueCompanyInvitation command,
        CommercialLicenseLease license, CancellationToken cancellation = default)
    {
        if (command.Email is null) return new(CompanyInvitationStatus.Invalid);
        var email = command.Email.Trim().ToLowerInvariant();
        if (!command.Confirm || command.CompanyId == Guid.Empty || command.CommandKey == Guid.Empty
            || !ProductInvitationRecipient.CanonicalEmail(email) || command.Role is not (CoreCompanyRoles.Member or CoreCompanyRoles.Administrator))
            return new(CompanyInvitationStatus.Invalid);
        try
        {
            await using var lookup = contextFactory("invitation-scope-lookup");
            var initial = await Access(lookup, actor, command.CompanyId, license, cancellation);
            if (!MayInvite(initial, command.Role)) return new(CompanyInvitationStatus.Denied);
            await using var db = contextFactory(initial!.TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync(Isolation(db), cancellation);
            await Lock(db, actor.Issuer, actor.Subject, command.CommandKey, initial.TenantId, command.CompanyId, cancellation);
            var current = await Access(db, actor, command.CompanyId, license, cancellation);
            if (!MayInvite(current, command.Role)) return new(CompanyInvitationStatus.Denied);
            var recipient = Hash(email);
            var hash = Hash(JsonSerializer.Serialize(new { schema = "core.company.invitation.issue.v1", command.CompanyId, recipient, command.Role }));
            var prior = await Receipt(db, actor.Issuer, actor.Subject, command.CommandKey, cancellation);
            if (prior is not null) return Replay(prior, hash, command.CompanyId);
            var pending = await db.CompanyInvitations.AsNoTracking().Where(x => x.LegalEntityId == command.CompanyId
                && x.RecipientHash == recipient && x.State == "pending").Select(x => x.ExpiresAt).ToArrayAsync(cancellation);
            if (pending.Any(x => x > clock.GetUtcNow())) return new(CompanyInvitationStatus.Conflict);
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var evidenceActor = actor.Issuer+"#"+actor.Subject;
            var issuedAt = clock.GetUtcNow();
            var invitation = new CoreCompanyInvitation { TenantId = initial.TenantId, LegalEntityId = command.CompanyId, Issuer = actor.Issuer,
                InviterSubject = actor.Subject, InviterLinkId = actor.LinkId, InviterLocalUserId = current!.PrincipalId,
                RecipientHash = recipient, TokenHash = Hash(token), Role = command.Role,
                CreatedAt = issuedAt, ExpiresAt = issuedAt.AddHours(48), CreatedBy = evidenceActor,
                ProtectedDelivery = deliveryProtection.Protect(JsonSerializer.Serialize(new { schema_version = 1,
                    recipient = email, review_url = (accountHub?"https://www.kuubuerp.com/account/#/company-invitation?token=":"https://app.kuubuerp.com/accept-invitation#token=")+token })) };
            db.CompanyInvitations.Add(invitation);
            Evidence(db, invitation, actor.Subject, "issued", command.CommandKey);
            // Durable delivery intent contains only IDs. The encrypted envelope
            // stays in Core; workers must recheck current invitation/authority.
            db.OutboxMessages.Add(new() { PublicId = Guid.CreateVersion7().ToString("D"),
                EventType = "core.company.invitation.delivery.requested.v1", SubjectId = invitation.Id.ToString("D"),
                PayloadJson = JsonSerializer.Serialize(new { invitation_id = invitation.Id, company_id = command.CompanyId }),
                CorrelationId = Hash(command.CommandKey.ToString("D")), CreatedBy = evidenceActor, AvailableAt = clock.GetUtcNow() });
            AddReceipt(db, invitation, actor.Subject, command.CommandKey, hash, "issue");
            await db.SaveChangesAsync(cancellation);
            if (!license.IsCurrent) return new(CompanyInvitationStatus.Denied);
            await transaction.CommitAsync(cancellation);
            return Result(CompanyInvitationStatus.Issued, invitation);
        }
        catch (DbUpdateConcurrencyException) { return new(CompanyInvitationStatus.Conflict); }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or CryptographicException)
        { return new(CompanyInvitationStatus.Unavailable); }
    }

    internal async Task<CompanyInvitationResult> RevokeAsync(CompanyTeamActor actor, RevokeCompanyInvitation command,
        CommercialLicenseLease license, CancellationToken cancellation = default)
    {
        if (!command.Confirm || command.CompanyId == Guid.Empty || command.InvitationId == Guid.Empty
            || command.CommandKey == Guid.Empty || command.ExpectedRevision < 1) return new(CompanyInvitationStatus.Invalid);
        try
        {
            await using var lookup = contextFactory("invitation-scope-lookup");
            var initial = await Access(lookup, actor, command.CompanyId, license, cancellation);
            if (initial is null || !initial.MayManageTeam) return new(CompanyInvitationStatus.Denied);
            await using var db = contextFactory(initial.TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync(Isolation(db), cancellation);
            await Lock(db, actor.Issuer, actor.Subject, command.CommandKey, initial.TenantId, command.CompanyId, cancellation);
            var current = await Access(db, actor, command.CompanyId, license, cancellation);
            var invitation = await db.CompanyInvitations.SingleOrDefaultAsync(x => x.Id == command.InvitationId
                && x.LegalEntityId == command.CompanyId, cancellation);
            if (invitation is null || !MayInvite(current, invitation.Role)) return new(CompanyInvitationStatus.Denied);
            var hash = Hash(JsonSerializer.Serialize(new { schema = "core.company.invitation.revoke.v1", command.CompanyId,
                command.InvitationId, command.ExpectedRevision }));
            var prior = await Receipt(db, actor.Issuer, actor.Subject, command.CommandKey, cancellation);
            if (prior is not null) return Replay(prior, hash, command.CompanyId);
            if (invitation.State != "pending" || invitation.Revision != command.ExpectedRevision) return new(CompanyInvitationStatus.Conflict);
            invitation.State = "revoked"; invitation.Revision++;
            invitation.UpdatedAt = clock.GetUtcNow(); invitation.UpdatedBy = actor.Issuer+"#"+actor.Subject;
            Evidence(db, invitation, actor.Subject, "revoked", command.CommandKey);
            AddReceipt(db, invitation, actor.Subject, command.CommandKey, hash, "revoke");
            await db.SaveChangesAsync(cancellation);
            if (!license.IsCurrent) return new(CompanyInvitationStatus.Denied);
            await transaction.CommitAsync(cancellation);
            return Result(CompanyInvitationStatus.Revoked, invitation);
        }
        catch (DbUpdateConcurrencyException) { return new(CompanyInvitationStatus.Conflict); }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException) { return new(CompanyInvitationStatus.Unavailable); }
    }

    internal async Task<CompanyInvitationResult> AcceptAsync(InvitationRecipientObservation recipient, AcceptCompanyInvitation command,
        CommercialLicenseLease license, CancellationToken cancellation = default)
    {
        if (!command.Confirm || command.CommandKey == Guid.Empty || command.ExpectedRevision < 1
            || command.Token is null || !Regex.IsMatch(command.Token, "^[A-Za-z0-9_-]{43}\\z")) return new(CompanyInvitationStatus.Invalid);
        if (!recipient.IsCurrent || !ProductInvitationRecipient.CanonicalEmail(recipient.VerifiedEmail)) return new(CompanyInvitationStatus.Denied);
        try
        {
            var tokenHash = Hash(command.Token); var recipientHash = Hash(recipient.VerifiedEmail);
            await using var lookup = contextFactory("invitation-secret-lookup");
            // Secret possession is only a locator. It does not establish an identity or company grant.
            var initial = await lookup.CompanyInvitations.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && !x.IsArchived, cancellation);
            if (initial is null || initial.RecipientHash != recipientHash) return new(CompanyInvitationStatus.Denied);
            await using var db = contextFactory(initial.TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync(Isolation(db), cancellation);
            await Lock(db, ProductCommercialLicense.Issuer, recipient.Subject, command.CommandKey,
                initial.TenantId, initial.LegalEntityId, cancellation);
            var invitation = await db.CompanyInvitations.SingleAsync(x => x.Id == initial.Id, cancellation);
            if (invitation.Issuer != ProductCommercialLicense.Issuer || invitation.RecipientHash != recipientHash
                || !recipient.IsCurrent || !license.IsCurrent) return new(CompanyInvitationStatus.Denied);
            var binding = await db.WorkspaceBindings.AsNoTracking().SingleOrDefaultAsync(x => x.LegalEntityId == invitation.LegalEntityId && !x.IsArchived, cancellation);
            if (binding is null || binding.ClientId != recipient.ClientId) return new(CompanyInvitationStatus.Denied);
            var inviter = new CompanyTeamActor(invitation.Issuer, invitation.InviterSubject, invitation.InviterLinkId);
            var current = await Access(db, inviter, invitation.LegalEntityId, license, cancellation);
            if (!MayInvite(current, invitation.Role) || current!.PrincipalId != invitation.InviterLocalUserId) return new(CompanyInvitationStatus.Denied);
            var hash = Hash(JsonSerializer.Serialize(new { schema = "core.company.invitation.accept.v1", invitation_id = invitation.Id, command.ExpectedRevision }));
            var prior = await Receipt(db, invitation.Issuer, recipient.Subject, command.CommandKey, cancellation);
            if (prior is not null)
            {
                if (prior.RequestHash != hash || prior.LegalEntityId != invitation.LegalEntityId) return new(CompanyInvitationStatus.Conflict);
                var stillUsable = await (from member in db.CompanyMemberships.AsNoTracking()
                    join user in db.LocalUsers.AsNoTracking() on member.LocalUserId equals user.Id
                    join link in db.ExternalIdentityLinks.AsNoTracking() on user.Id equals link.LocalUserId
                    where member.Id == prior.ResultMembershipId && member.LegalEntityId == invitation.LegalEntityId && member.IsActive && !member.IsArchived
                        && user.IsActive && !user.IsArchived && user.CredentialMode == LocalCredentialModes.Federated
                        && link.IsActive && !link.IsArchived && link.Issuer == invitation.Issuer && link.Subject == recipient.Subject
                    select user.LockedUntil).ToArrayAsync(cancellation);
                return stillUsable.Any(x => x is null || x <= clock.GetUtcNow()) && recipient.IsCurrent && license.IsCurrent
                    ? Replay(prior, hash, invitation.LegalEntityId) : new(CompanyInvitationStatus.Denied);
            }
            if (invitation.State != "pending" || invitation.Revision != command.ExpectedRevision) return new(CompanyInvitationStatus.Conflict);
            if (invitation.ExpiresAt <= clock.GetUtcNow()) return new(CompanyInvitationStatus.Expired);
            var linkRow = await db.ExternalIdentityLinks.SingleOrDefaultAsync(x => x.Issuer == invitation.Issuer && x.Subject == recipient.Subject, cancellation);
            LocalUser userRow;
            if (linkRow is not null)
            {
                userRow = await db.LocalUsers.SingleAsync(x => x.Id == linkRow.LocalUserId, cancellation);
                if (!linkRow.IsActive || linkRow.IsArchived || !userRow.IsActive || userRow.IsArchived
                    || userRow.CredentialMode != LocalCredentialModes.Federated || userRow.LockedUntil > clock.GetUtcNow())
                    return new(CompanyInvitationStatus.Denied);
            }
            else
            {
                userRow = LocalUser.Federated("Invited company member"); userRow.TenantId = initial.TenantId;
                userRow.CreatedBy = invitation.Issuer+"#"+recipient.Subject; db.LocalUsers.Add(userRow);
                db.ExternalIdentityLinks.Add(new() { TenantId = initial.TenantId, Issuer = invitation.Issuer, Subject = recipient.Subject,
                    LocalUserId = userRow.Id, CreatedBy = userRow.CreatedBy });
            }
            var membership = await db.CompanyMemberships.SingleOrDefaultAsync(x => x.LegalEntityId == invitation.LegalEntityId && x.LocalUserId == userRow.Id, cancellation);
            if (membership is not null && (membership.IsActive || membership.IsArchived || membership.Revision == int.MaxValue))
                return new(CompanyInvitationStatus.Conflict); // Invitation is not an indirect role change.
            var beforeMembership = membership is null ? null : JsonSerializer.Serialize(new { membership.Role, membership.IsActive, membership.Revision });
            if (membership is null)
            {
                membership = new() { TenantId = initial.TenantId, LegalEntityId = invitation.LegalEntityId,
                    LocalUserId = userRow.Id, Role = invitation.Role, CreatedBy = invitation.Issuer+"#"+recipient.Subject };
                db.CompanyMemberships.Add(membership);
            }
            else { membership.IsActive = true; membership.Role = invitation.Role; membership.Revision++;
                membership.UpdatedAt = clock.GetUtcNow(); membership.UpdatedBy = invitation.Issuer+"#"+recipient.Subject; }
            invitation.State = "accepted"; invitation.Revision++; invitation.AcceptedSubject = recipient.Subject;
            invitation.AcceptedMembershipId = membership.Id; invitation.UpdatedAt = clock.GetUtcNow();
            invitation.UpdatedBy = invitation.Issuer+"#"+recipient.Subject;
            var memberPayload = JsonSerializer.Serialize(new { invitation_id = invitation.Id, company_id = invitation.LegalEntityId,
                membership_id = membership.Id, membership.Role, membership.IsActive, membership.Revision, operational_access_granted = false });
            db.AuditEntries.Add(new() { EntityType = "CoreCompanyMembership", EntityId = membership.Id.ToString("D"),
                Action = "core.company.member.invitation.accepted", ActorSubjectId = invitation.UpdatedBy,
                BeforeJson = beforeMembership, AfterJson = memberPayload, CorrelationId = Hash(command.CommandKey.ToString("D")), CreatedBy = invitation.UpdatedBy });
            db.OutboxMessages.Add(new() { PublicId = Guid.CreateVersion7().ToString("D"),
                EventType = "core.company.member.invitation.accepted.v1", SubjectId = membership.Id.ToString("D"),
                PayloadJson = memberPayload, CorrelationId = Hash(command.CommandKey.ToString("D")), CreatedBy = invitation.UpdatedBy });
            Evidence(db, invitation, recipient.Subject, "accepted", command.CommandKey);
            AddReceipt(db, invitation, recipient.Subject, command.CommandKey, hash, "accept");
            await db.SaveChangesAsync(cancellation);
            if (!recipient.IsCurrent || !license.IsCurrent) return new(CompanyInvitationStatus.Denied);
            await transaction.CommitAsync(cancellation);
            return Result(CompanyInvitationStatus.Accepted, invitation);
        }
        catch (DbUpdateConcurrencyException) { return new(CompanyInvitationStatus.Conflict); }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException) { return new(CompanyInvitationStatus.Unavailable); }
    }

    private Task<CoreCompanyAccessDecision?> Access(HrmDbContext db, CompanyTeamActor actor, Guid company, CommercialLicenseLease lease, CancellationToken ct)
        => new CoreCompanyAccess(db, clock).ReadAsync(actor.Issuer, actor.Subject, actor.LinkId, company, lease, ct);
    private static bool MayInvite(CoreCompanyAccessDecision? actor, string role) => actor is not null
        && (actor.Role == CoreCompanyRoles.Owner || actor.Role == CoreCompanyRoles.Administrator && role == CoreCompanyRoles.Member);
    private static IsolationLevel Isolation(HrmDbContext db) => db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
    private static async Task Lock(HrmDbContext db, string issuer, string subject, Guid key, string tenant, Guid company, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var keyScope = "core.company.invitation.command.v1\n"+issuer+"\n"+subject+"\n"+key.ToString("D");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({keyScope}, 0))", ct);
        var companyScope = "core.company.team.v1\n"+tenant+"\n"+company.ToString("D");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({companyScope}, 0))", ct);
    }
    private static Task<CoreCompanyInvitationReceipt?> Receipt(HrmDbContext db, string issuer, string subject, Guid key, CancellationToken ct)
        => db.CompanyInvitationReceipts.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.ActorIssuer == issuer && x.ActorSubject == subject && x.CommandKey == key, ct);
    private static CompanyInvitationResult Replay(CoreCompanyInvitationReceipt receipt, string hash, Guid company)
        => receipt.RequestHash == hash && receipt.LegalEntityId == company
            ? new(CompanyInvitationStatus.Existing, receipt.InvitationId, receipt.ResultState, receipt.ResultRevision, receipt.ResultMembershipId)
            : new(CompanyInvitationStatus.Conflict);
    private static CompanyInvitationResult Result(CompanyInvitationStatus status, CoreCompanyInvitation row)
        => new(status, row.Id, row.State, row.Revision, row.AcceptedMembershipId);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void AddReceipt(HrmDbContext db, CoreCompanyInvitation row, string subject, Guid key, string hash, string operation)
        => db.CompanyInvitationReceipts.Add(new() { TenantId = row.TenantId, LegalEntityId = row.LegalEntityId, InvitationId = row.Id,
            ActorIssuer = row.Issuer, ActorSubject = subject, CommandKey = key, RequestHash = hash, Operation = operation,
            ResultState = row.State, ResultRevision = row.Revision, ResultMembershipId = row.AcceptedMembershipId, CreatedBy = row.Issuer+"#"+subject });
    private static void Evidence(HrmDbContext db, CoreCompanyInvitation row, string subject, string action, Guid key)
    {
        var payload = JsonSerializer.Serialize(new { invitation_id = row.Id, company_id = row.LegalEntityId,
            row.State, row.Revision, row.Role, membership_id = row.AcceptedMembershipId, operational_access_granted = false });
        var correlation = Hash(key.ToString("D")); var actor = row.Issuer+"#"+subject;
        db.AuditEntries.Add(new() { EntityType = "CoreCompanyInvitation", EntityId = row.Id.ToString("D"),
            Action = "core.company.invitation."+action, ActorSubjectId = actor,
            BeforeJson = action == "issued" ? null : JsonSerializer.Serialize(new { State = "pending", Revision = 1 }),
            AfterJson = payload, CorrelationId = correlation, CreatedBy = actor });
        db.OutboxMessages.Add(new() { PublicId = Guid.CreateVersion7().ToString("D"), EventType = "core.company.invitation."+action+".v1",
            SubjectId = row.Id.ToString("D"), PayloadJson = payload, CorrelationId = correlation, CreatedBy = actor });
    }
}
