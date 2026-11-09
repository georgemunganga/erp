using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owner-confirmed immutable grant decisions; never creates an ERP session or legacy role assignment.</summary>
internal sealed class CoreModuleAccessCommands(Func<string,HrmDbContext> contexts,TimeProvider clock)
{
    internal async Task<ModuleAccessResult> ExecuteAsync(OperationalSubjectObservation person,CompanyTeamActor actor,
        ModuleAccessCommand command,CommercialLicenseLease license,CancellationToken ct=default)
    {
        var normalized=NormalizedModuleAccess.Read(command);
        if(normalized is null)return new(ModuleAccessStatus.Invalid);
        if(!person.IsCurrent || person.Subject!=actor.Subject || actor.Issuer!=ProductCommercialLicense.Issuer || !license.IsCurrent)return new(ModuleAccessStatus.Denied);
        try {
            await using var lookup=contexts("untrusted-module-access-lookup");
            var first=await new CoreCompanyAccess(lookup,clock).FindCandidateAsync(actor.Issuer,actor.Subject,actor.LinkId,command.CompanyId,ct);
            if(first is null)return new(ModuleAccessStatus.Denied);
            var initial=await new CoreCompanyAccess(lookup,clock).ReadAsync(actor.Issuer,actor.Subject,actor.LinkId,command.CompanyId,license,ct);
            if(initial is null || initial.Role!=CoreCompanyRoles.Owner || !license.AllowsWorkspaceModule(first.Slot,command.ModuleCode))return new(ModuleAccessStatus.Denied);
            await using var db=contexts(initial.TenantId);
            await using var transaction=await db.Database.BeginTransactionAsync(db.Database.IsNpgsql()?IsolationLevel.ReadCommitted:IsolationLevel.Serializable,ct);
            if(db.Database.IsNpgsql()) {
                var key="core.module.access.command.v1\n"+actor.Issuer+"\n"+actor.Subject+"\n"+command.CommandKey.ToString("D");
                var company="core.company.team.v1\n"+initial.TenantId+"\n"+command.CompanyId.ToString("D");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({company},0))",ct);
            }
            var access=new CoreCompanyAccess(db,clock);
            var issuer=await access.FindCandidateAsync(actor.Issuer,actor.Subject,actor.LinkId,command.CompanyId,ct);
            var current=await access.ReadAsync(actor.Issuer,actor.Subject,actor.LinkId,command.CompanyId,license,ct);
            if(current is null || issuer is null || current.Role!=CoreCompanyRoles.Owner || !license.AllowsWorkspaceModule(issuer.Slot,command.ModuleCode)
                || !person.IsCurrent)return new(ModuleAccessStatus.Denied);
            var hash=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{schema="core.module.access.v1",command.CompanyId,
                command.MembershipId,command.MembershipRevision,command.ModuleCode,command.ExpectedRevision,command.Revoke,
                roles=normalized.Roles,actions=normalized.Actions,command.ScopeKind,locations=normalized.Locations,command.AllCompanyLocationsConfirmed,command.Confirm})));
            var receipt=await db.ModuleAccessReceipts.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.ActorIssuer==actor.Issuer && x.ActorSubject==actor.Subject && x.CommandKey==command.CommandKey,ct);
            if(receipt is not null) {
                if(receipt.RequestHash!=hash || receipt.TenantId!=current.TenantId || receipt.LegalEntityId!=command.CompanyId || receipt.ActingMembershipId!=issuer.MembershipId
                    || receipt.MembershipId!=command.MembershipId || receipt.ModuleCode!=command.ModuleCode)return new(ModuleAccessStatus.Conflict);
                var recorded=await db.ModuleAccessVersions.AsNoTracking().SingleAsync(x=>x.Id==receipt.ResultVersionId,ct);
                return person.IsCurrent && license.IsCurrent?new(ModuleAccessStatus.Existing,recorded.Id,recorded.Revision,recorded.State):new(ModuleAccessStatus.Denied);
            }
            var target=await db.CompanyMemberships.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==command.MembershipId && x.LegalEntityId==command.CompanyId && !x.IsArchived,ct);
            if(target is null)return new(ModuleAccessStatus.Denied);
            if(target.Revision!=command.MembershipRevision)return new(ModuleAccessStatus.Conflict);
            if(!command.Revoke) {
                var principal=await db.LocalUsers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==target.LocalUserId && x.IsActive && !x.IsArchived && x.CredentialMode==LocalCredentialModes.Federated,ct);
                if(!target.IsActive || principal is null || principal.LockedUntil is {} until && until>clock.GetUtcNow()
                    || !await db.ExternalIdentityLinks.AsNoTracking().AnyAsync(x=>x.LocalUserId==target.LocalUserId && x.IsActive && !x.IsArchived && x.Issuer==actor.Issuer,ct))return new(ModuleAccessStatus.Denied);
            }
            var prior=await db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==current.TenantId && x.LegalEntityId==command.CompanyId
                && x.MembershipId==command.MembershipId && x.ModuleCode==command.ModuleCode).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
            if((prior?.Revision??0)!=command.ExpectedRevision || command.ExpectedRevision==int.MaxValue || prior?.IsArchived==true)return new(ModuleAccessStatus.Conflict);
            if(command.Revoke && prior is null)return new(ModuleAccessStatus.Denied);
            if(command.ScopeKind=="locations") {
                var owned=await db.WorkLocations.AsNoTracking().Where(x=>normalized.Locations.Contains(x.Id) && x.LegalEntityId==command.CompanyId && !x.IsArchived).Select(x=>x.Id).ToArrayAsync(ct);
                if(owned.Length!=normalized.Locations.Length)return new(ModuleAccessStatus.Denied);
            }
            var oldLocations=prior is null?[]:await db.ModuleAccessLocations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==current.TenantId && x.LegalEntityId==command.CompanyId && x.GrantVersionId==prior.Id).Select(x=>x.LocationId).ToArrayAsync(ct);
            var state=command.Revoke?"revoked":"granted";
            var roles=string.Join(',',normalized.Roles);var actions=string.Join(',',normalized.Actions);
            var changed=prior is null || prior.State!=state || prior.MembershipRevision!=target.Revision || prior.RoleKeysCsv!=roles || prior.ActionCodesCsv!=actions
                || prior.ScopeKind!=command.ScopeKind || !oldLocations.Order().SequenceEqual(normalized.Locations);
            CoreModuleAccessVersion result;
            var evidenceActor=actor.Issuer+"#"+actor.Subject;
            if(changed) {
                var actorMember=await db.CompanyMemberships.AsNoTracking().SingleAsync(x=>x.Id==issuer.MembershipId && x.LegalEntityId==command.CompanyId,ct);
                result=new(){LegalEntityId=command.CompanyId,MembershipId=target.Id,MembershipRevision=target.Revision,ModuleCode=command.ModuleCode,
                    Revision=(prior?.Revision??0)+1,PreviousVersionId=prior?.Id,PreviousRevision=prior?.Revision??0,State=state,
                    RoleKeysCsv=roles,ActionCodesCsv=actions,ScopeKind=command.ScopeKind,ActingMembershipId=issuer.MembershipId,ActingMembershipRevision=actorMember.Revision,CreatedBy=evidenceActor};
                db.ModuleAccessVersions.Add(result);
                foreach(var location in normalized.Locations)db.ModuleAccessLocations.Add(new(){LegalEntityId=command.CompanyId,GrantVersionId=result.Id,LocationId=location,CreatedBy=evidenceActor});
                var after=JsonSerializer.Serialize(new{version_id=result.Id,company_id=command.CompanyId,membership_id=target.Id,module=command.ModuleCode,
                    result.Revision,result.MembershipRevision,result.State,roles=normalized.Roles,actions=normalized.Actions,scope=command.ScopeKind,locations=normalized.Locations});
                var correlation=Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(command.CommandKey.ToString("D"))));
                db.AuditEntries.Add(new(){EntityType="CoreModuleAccessVersion",EntityId=result.Id.ToString("D"),Action="core.company.module.access.changed",
                    ActorSubjectId=evidenceActor,BeforeJson=prior is null?null:JsonSerializer.Serialize(new{version_id=prior.Id,prior.Revision}),AfterJson=after,CorrelationId=correlation,CreatedBy=evidenceActor});
                db.OutboxMessages.Add(new(){PublicId=Guid.CreateVersion7().ToString("D"),EventType="core.company.module.access.changed.v1",SubjectId=result.Id.ToString("D"),
                    CorrelationId=correlation,PayloadJson=JsonSerializer.Serialize(new{version_id=result.Id,company_id=command.CompanyId,membership_id=target.Id,module=command.ModuleCode,result.Revision,result.State}),
                    CreatedBy=evidenceActor,AvailableAt=clock.GetUtcNow()});
            }else result=prior!;
            db.ModuleAccessReceipts.Add(new(){LegalEntityId=command.CompanyId,MembershipId=target.Id,ModuleCode=command.ModuleCode,ActingMembershipId=issuer.MembershipId,
                ActorIssuer=actor.Issuer,ActorSubject=actor.Subject,CommandKey=command.CommandKey,RequestHash=hash,ResultVersionId=result.Id,ResultRevision=result.Revision,CreatedBy=evidenceActor});
            await db.SaveChangesAsync(ct);
            if(!person.IsCurrent || !license.IsCurrent)return new(ModuleAccessStatus.Denied);
            await transaction.CommitAsync(ct);
            return new(changed?ModuleAccessStatus.Changed:ModuleAccessStatus.Unchanged,result.Id,result.Revision,result.State);
        }catch(DbUpdateConcurrencyException){return new(ModuleAccessStatus.Conflict);}
        catch(DbUpdateException error)when(error.InnerException is PostgresException postgres && postgres.SqlState==PostgresErrorCodes.UniqueViolation){return new(ModuleAccessStatus.Conflict);}
        catch(Exception error)when(error is DbException or DbUpdateException){return new(ModuleAccessStatus.Unavailable);}
    }
}
