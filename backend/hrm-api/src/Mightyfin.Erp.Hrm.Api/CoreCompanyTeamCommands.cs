using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum CompanyTeamOperation { ChangeRole, Revoke }
internal enum CompanyTeamStatus { Changed, Unchanged, Existing, Invalid, Denied, Conflict, LastOwner, Unavailable }
internal sealed record CompanyTeamActor(string Issuer, string Subject, Guid LinkId);
internal sealed record CompanyTeamCommand(Guid CompanyId, Guid MembershipId, Guid CommandKey,
    int ExpectedRevision, CompanyTeamOperation Operation, string? Role = null);
internal sealed record CompanyTeamResult(CompanyTeamStatus Status, Guid? MembershipId = null,
    string? Role = null, bool? Active = null, int? Revision = null);

/// <summary>Confirmed governance mutations only; no invitations, credentials or operational admission.</summary>
internal sealed class CoreCompanyTeamCommands(Func<string, HrmDbContext> contextFactory, TimeProvider clock)
{
    internal async Task<CompanyTeamResult> ExecuteAsync(CompanyTeamActor actor, CompanyTeamCommand command,
        CommercialLicenseLease license, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(actor.Issuer) || string.IsNullOrWhiteSpace(actor.Subject))
            return new(CompanyTeamStatus.Denied);
        if (command.CompanyId == Guid.Empty || command.MembershipId == Guid.Empty || command.CommandKey == Guid.Empty
            || command.ExpectedRevision < 1 || command.Operation is not (CompanyTeamOperation.ChangeRole or CompanyTeamOperation.Revoke)
            || (command.Operation == CompanyTeamOperation.ChangeRole ? !CoreCompanyRoles.IsValid(command.Role!) : command.Role is not null))
            return new(CompanyTeamStatus.Invalid);
        try
        {
            await using var lookup = contextFactory("untrusted-team-lookup");
            var initial = await new CoreCompanyAccess(lookup, clock).ReadAsync(actor.Issuer, actor.Subject,
                actor.LinkId, command.CompanyId, license, cancellation);
            if (initial is null || !initial.MayManageTeam) return new(CompanyTeamStatus.Denied);
            await using var db = contextFactory(initial.TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync(
                db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellation);
            if (db.Database.IsNpgsql())
            {
                // Consistent key-then-company lock order. SQL is parameterized;
                // a waiter reads current membership after the previous commit.
                var keyScope = "core.company.team.command.v1\n"+actor.Issuer+"\n"+actor.Subject+"\n"+command.CommandKey.ToString("D");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({keyScope}, 0))", cancellation);
                var companyScope = "core.company.team.v1\n"+initial.TenantId+"\n"+command.CompanyId.ToString("D");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({companyScope}, 0))", cancellation);
            }
            var current = await new CoreCompanyAccess(db, clock).ReadAsync(actor.Issuer, actor.Subject,
                actor.LinkId, command.CompanyId, license, cancellation);
            if (current is null || !current.MayManageTeam) return new(CompanyTeamStatus.Denied);
            var operation = command.Operation == CompanyTeamOperation.ChangeRole ? "role-change" : "revoke";
            var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                schema = "core.company.team.v1", command.CompanyId, command.MembershipId, command.ExpectedRevision,
                operation, command.Role })));
            var member = await db.CompanyMemberships.SingleOrDefaultAsync(x => x.Id == command.MembershipId
                && x.LegalEntityId == command.CompanyId && !x.IsArchived, cancellation);
            if (member is null) return new(CompanyTeamStatus.Denied);
            // Administrators may revoke ordinary members; privileged membership
            // changes and all role assignment remain owner-only.
            if (current.Role != CoreCompanyRoles.Owner && (member.Role != CoreCompanyRoles.Member
                || command.Operation != CompanyTeamOperation.Revoke)) return new(CompanyTeamStatus.Denied);
            var prior = await db.CompanyTeamCommandReceipts.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
                x => x.ActorIssuer == actor.Issuer && x.ActorSubject == actor.Subject && x.CommandKey == command.CommandKey, cancellation);
            if (prior is not null)
            {
                if (!license.IsCurrent) return new(CompanyTeamStatus.Denied);
                return prior.RequestHash == hash && prior.TenantId == current.TenantId
                    && prior.ActorLocalUserId == current.PrincipalId && prior.LegalEntityId == command.CompanyId
                    ? new(CompanyTeamStatus.Existing, prior.MembershipId, prior.ResultRole, prior.ResultActive, prior.ResultRevision)
                    : new(CompanyTeamStatus.Conflict);
            }
            if (member.Revision != command.ExpectedRevision || member.Revision == int.MaxValue)
                return new(CompanyTeamStatus.Conflict);
            if (command.Operation == CompanyTeamOperation.ChangeRole && !member.IsActive)
                return new(CompanyTeamStatus.Denied); // Reactivation needs a separate consented invitation.
            var role = command.Operation == CompanyTeamOperation.ChangeRole ? command.Role! : member.Role;
            var active = command.Operation != CompanyTeamOperation.Revoke;
            if (member.Role == CoreCompanyRoles.Owner && member.IsActive && (!active || role != CoreCompanyRoles.Owner))
            {
                var alternatives = await (from other in db.CompanyMemberships.AsNoTracking()
                    join user in db.LocalUsers.AsNoTracking() on other.LocalUserId equals user.Id
                    where other.LegalEntityId == command.CompanyId && other.Id != member.Id
                        && other.Role == CoreCompanyRoles.Owner && other.IsActive && !other.IsArchived
                        && user.IsActive && !user.IsArchived && user.CredentialMode == LocalCredentialModes.Federated
                        && db.ExternalIdentityLinks.Any(link => link.LocalUserId == user.Id && link.IsActive
                            && !link.IsArchived && link.Issuer == actor.Issuer)
                    select user.LockedUntil).ToArrayAsync(cancellation);
                if (!alternatives.Any(until => until is null || until <= clock.GetUtcNow()))
                    return new(CompanyTeamStatus.LastOwner);
            }
            var changed = member.Role != role || member.IsActive != active;
            var evidenceActor = actor.Issuer+"#"+actor.Subject;
            if (changed)
            {
                var before = JsonSerializer.Serialize(new { member.Role, member.IsActive, member.Revision });
                member.Role = role; member.IsActive = active; member.Revision++;
                member.UpdatedAt = clock.GetUtcNow(); member.UpdatedBy = evidenceActor;
                var after = JsonSerializer.Serialize(new { company_id = command.CompanyId, membership_id = member.Id,
                    member.Role, member.IsActive, member.Revision });
                var correlation = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(command.CommandKey.ToString("D"))));
                db.AuditEntries.Add(new() { EntityType = "CoreCompanyMembership", EntityId = member.Id.ToString("D"),
                    Action = "core.company.member.access.changed", ActorSubjectId = evidenceActor,
                    BeforeJson = before, AfterJson = after, CorrelationId = correlation, CreatedBy = evidenceActor });
                db.OutboxMessages.Add(new() { PublicId = Guid.CreateVersion7().ToString("D"),
                    EventType = "core.company.member.access.changed.v1", SubjectId = member.Id.ToString("D"),
                    CorrelationId = correlation, PayloadJson = after, CreatedBy = evidenceActor, AvailableAt = clock.GetUtcNow() });
            }
            db.CompanyTeamCommandReceipts.Add(new() { LegalEntityId = command.CompanyId, ActorLocalUserId = current.PrincipalId,
                ActorIssuer = actor.Issuer, ActorSubject = actor.Subject, CommandKey = command.CommandKey,
                MembershipId = member.Id, RequestHash = hash, Operation = operation,
                ResultRole = member.Role, ResultActive = member.IsActive, ResultRevision = member.Revision, CreatedBy = evidenceActor });
            await db.SaveChangesAsync(cancellation);
            if (!license.IsCurrent) return new(CompanyTeamStatus.Denied); // rolls back receipt/events too
            await transaction.CommitAsync(cancellation);
            return new(changed ? CompanyTeamStatus.Changed : CompanyTeamStatus.Unchanged, member.Id, member.Role, member.IsActive, member.Revision);
        }
        catch (DbUpdateConcurrencyException) { return new(CompanyTeamStatus.Conflict); }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException)
        { return new(CompanyTeamStatus.Unavailable); }
    }
}
