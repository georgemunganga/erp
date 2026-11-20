using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum ProductSessionStatus { Issued, Existing, Revoked, Denied, Invalid, Conflict, Unavailable, Limited }
internal sealed record CoreProductSessionIssue(CoreErpSessionSelection Selection, Guid CommandKey,
    [property: System.Text.Json.Serialization.JsonIgnore] string CredentialHandle, [property: System.Text.Json.Serialization.JsonIgnore] string BrowserBinding, DateTimeOffset CredentialExpiresAt)
{ public override string ToString() => nameof(CoreProductSessionIssue); }
internal sealed record CoreIssuedProductSession(Guid Id, [property: System.Text.Json.Serialization.JsonIgnore] string Token, DateTimeOffset ExpiresAt)
{ public override string ToString() => nameof(CoreIssuedProductSession); }
internal sealed record CoreProductSessionResult(ProductSessionStatus Status, CoreIssuedProductSession? Session = null);

/// <summary>Durable selected sessions, encrypted retry recovery and append-only revocation.
/// No route/cookie/default-auth registration is performed by this component.</summary>
internal sealed class CoreProductSessionRegistry(Func<string,HrmDbContext> contexts, IDataProtectionProvider protection,
    TimeProvider clock, int recentIssueLimit = 30)
{
    private static string Hash(string value) => ProductInvitationIntentStore.Hash(value);
    private static bool Opaque(string? value) => ProductInvitationIntentStore.Opaque(value);
    private static bool Identity(string subject, string client) => subject is not null
        && Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
        && Guid.TryParseExact(client, "D", out var id) && id != Guid.Empty && id.ToString("D") == client;
    private IDataProtector Protector(CoreProductSession row) => protection.CreateProtector("Kuubu.ERP.SessionToken.v1",
        row.Id.ToString("D"), row.TenantId, row.PrincipalId.ToString("D"), row.ClientId, row.Subject,
        row.CredentialHash, row.BrowserHash, row.GrantVersionId.ToString("D"), row.ScopeHash, row.IssuedAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture), row.ExpiresAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static string ScopeHash(CoreProductSession row,IEnumerable<Guid> locations) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        schema=row.PolicyVersion,row.Issuer,row.Subject,row.ClientId,row.TenantId,row.PrincipalId,row.LinkId,row.LegalEntityId,row.MembershipId,
        row.ModuleCode,row.GrantVersionId,row.GrantRevision,row.ScopeKind,locations=locations.Order().ToArray() })));
    private static string Entry(string module) => module == "hrm" ? "hrm-access" : "procurement-access";
    private static ProductSessionStatus Failure(ModuleAccessStatus status) => status switch {
        ModuleAccessStatus.Invalid => ProductSessionStatus.Invalid, ModuleAccessStatus.Conflict => ProductSessionStatus.Conflict,
        ModuleAccessStatus.Unavailable => ProductSessionStatus.Unavailable, _ => ProductSessionStatus.Denied };
    private static async Task Lock(HrmDbContext db, string tenant, string subject, string client, Guid company, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var actor = "core.product.session.v1\n" + client + "\n" + subject;
        var target = "core.company.team.v1\n" + tenant + "\n" + company.ToString("D");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor},0))",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({target},0))",ct);
    }
    internal async Task<CoreProductSessionResult> IssueAsync(OperationalSubjectObservation person, CommercialLicenseLease license,
        CoreProductSessionIssue request, CancellationToken ct = default)
    {
        if (recentIssueLimit is <1 or >300) throw new InvalidOperationException("Invalid product session issue budget.");
        if (!Identity(person.Subject,person.ClientId) || request.CommandKey == Guid.Empty
            || !Opaque(request.CredentialHandle) || !Opaque(request.BrowserBinding)
            || request.CredentialExpiresAt.Offset != TimeSpan.Zero || request.Selection.LocationIds is null
            || request.Selection.LocationIds.Length > 128) return new(ProductSessionStatus.Invalid);
        var selection = request.Selection with { LocationIds = request.Selection.LocationIds.ToArray() };
        if (!person.IsCurrent || !license.IsCurrent || request.CredentialExpiresAt <= clock.GetUtcNow()) return new(ProductSessionStatus.Denied);
        try {
            await using var lookup = contexts("untrusted-session-issue");
            var initial = await new CoreErpSessionScopeAuthority(lookup,clock).SelectAsync(person,license,selection,ct);
            if (initial.Binding is not { } first || initial.Status != ModuleAccessStatus.Authorized) return new(Failure(initial.Status));
            await using var db = contexts(first.TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync(db.Database.IsNpgsql()?IsolationLevel.ReadCommitted:IsolationLevel.Serializable,ct);
            // This unit owns the entire transaction and always rolls it back on
            // failure. Avoid subtransaction xmin values in the insertion seal.
            db.Database.AutoSavepointsEnabled = false;
            await Lock(db,first.TenantId,person.Subject,person.ClientId,selection.CompanyId,ct);
            var authority = new CoreErpSessionScopeAuthority(db,clock);
            var current = await authority.SelectAsync(person,license,selection,ct);
            if (current.Binding is not { } bound || current.Status != ModuleAccessStatus.Authorized) return new(Failure(current.Status));
            if (bound.TenantId != first.TenantId || bound.PrincipalId != first.PrincipalId) return new(ProductSessionStatus.Denied);
            var credential = Hash(request.CredentialHandle); var browser = Hash(request.BrowserBinding);
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                schema=1,selection.CompanyId,selection.LinkId,selection.Module,selection.ExpectedGrantVersionId,selection.ExpectedGrantRevision,
                selection.AllCompanyLocations,locations=bound.LocationIds,selection.ConfirmCompanyWide,credential,browser,deadline=request.CredentialExpiresAt.UtcTicks })));
            var existing = await db.ProductSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x =>
                x.Issuer == ProductCommercialLicense.Issuer && x.Subject == person.Subject && x.ClientId == person.ClientId && x.CommandKey == request.CommandKey,ct);
            if (existing is not null) {
                if (existing.TenantId != bound.TenantId || existing.RequestHash != requestHash || existing.CredentialHash != credential || existing.BrowserHash != browser)
                    return new(ProductSessionStatus.Conflict);
                var proof = await ResolveIn(db,existing,person,license,request.CredentialHandle,request.BrowserBinding,
                    new(bound.CompanyId,bound.Module,Entry(bound.Module),bound.AllCompanyLocations,bound.LocationIds.ToArray()),ct);
                if (proof.Scope is null || proof.Status != ModuleAccessStatus.Authorized) return new(Failure(proof.Status));
                var recovered = Protector(existing).Unprotect(existing.ProtectedToken);
                if (!Opaque(recovered) || Hash(recovered) != existing.TokenHash) return new(ProductSessionStatus.Unavailable);
                if (!proof.Scope.IsCurrent || !person.IsCurrent || !license.IsCurrent || clock.GetUtcNow() >= existing.ExpiresAt) return new(ProductSessionStatus.Denied);
                return new(ProductSessionStatus.Existing,new(existing.Id,recovered,existing.ExpiresAt));
            }
            var now = clock.GetUtcNow(); var recent = now.AddMinutes(-10);
            if (await db.ProductSessions.IgnoreQueryFilters().CountAsync(x => x.Issuer == ProductCommercialLicense.Issuer
                && x.Subject == person.Subject && x.ClientId == person.ClientId && x.IssuedAt > recent,ct) >= recentIssueLimit)
                return new(ProductSessionStatus.Limited);
            var deadline = request.CredentialExpiresAt < now.AddHours(8) ? request.CredentialExpiresAt : now.AddHours(8);
            if (deadline <= now || !person.IsCurrent || !license.IsCurrent) return new(ProductSessionStatus.Denied);
            var prior = await db.ProductSessions.IgnoreQueryFilters().Where(x => x.Issuer == ProductCommercialLicense.Issuer
                && x.Subject == person.Subject && x.ClientId == person.ClientId && x.CredentialHash == credential && x.BrowserHash == browser
                && x.ExpiresAt > now && !db.ProductSessionRevocations.IgnoreQueryFilters().Any(r => r.SessionId == x.Id)).ToArrayAsync(ct);
            foreach (var old in prior) {
                // One opaque ERP cookie selects one workspace/module. Historical
                // retries cannot reactivate a replaced selection.
                if (old.TenantId == bound.TenantId) Revoke(db,old,"replaced",now);
                else await RevokeAcrossTenant(db,old,now,ct);
            }
            var grant = await db.ModuleAccessVersions.AsNoTracking().SingleAsync(x => x.Id == bound.GrantVersionId,ct);
            var token = ProductInvitationIntentStore.Random();
            var row = new CoreProductSession { TenantId=bound.TenantId,Issuer=ProductCommercialLicense.Issuer,Subject=person.Subject,ClientId=person.ClientId,
                PrincipalId=bound.PrincipalId,LinkId=bound.LinkId,LegalEntityId=bound.CompanyId,MembershipId=grant.MembershipId,ModuleCode=bound.Module,
                GrantVersionId=bound.GrantVersionId,GrantRevision=bound.GrantRevision,ScopeKind=bound.AllCompanyLocations?"company":"locations",
                TokenHash=Hash(token),CredentialHash=credential,BrowserHash=browser,CommandKey=request.CommandKey,RequestHash=requestHash,
                IssuedAt=now,ExpiresAt=deadline,CreatedAt=now,CreatedBy=bound.PrincipalId.ToString("D") };
            row.ScopeHash = ScopeHash(row,bound.LocationIds); row.ProtectedToken = Protector(row).Protect(token); db.ProductSessions.Add(row);
            foreach (var location in bound.LocationIds) db.ProductSessionLocations.Add(new(){LegalEntityId=bound.CompanyId,SessionId=row.Id,LocationId=location,CreatedBy=row.CreatedBy});
            Evidence(db,row,"issued",row.Id,now);
            await db.SaveChangesAsync(ct);
            var final = await ResolveIn(db,row,person,license,request.CredentialHandle,request.BrowserBinding,
                new(bound.CompanyId,bound.Module,Entry(bound.Module),bound.AllCompanyLocations,bound.LocationIds.ToArray()),ct);
            if (final.Status != ModuleAccessStatus.Authorized || !person.IsCurrent || !license.IsCurrent || clock.GetUtcNow() >= deadline)
                return new(Failure(final.Status));
            await transaction.CommitAsync(ct);
            return new(ProductSessionStatus.Issued,new(row.Id,token,deadline));
        } catch (Exception error) when (error is CryptographicException or FormatException or ArgumentException) { return new(ProductSessionStatus.Unavailable); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation) { return new(ProductSessionStatus.Conflict); }
        catch (Exception e) when (e is DbException or DbUpdateException) { return new(ProductSessionStatus.Unavailable); }
    }
    internal async Task<CoreErpScopeResult> ResolveAsync(string token, string credential, string browser,
        OperationalSubjectObservation person, CommercialLicenseLease license, CoreErpScopeRequest request, CancellationToken ct=default)
    {
        if (!Opaque(token) || !Opaque(credential) || !Opaque(browser) || !Identity(person.Subject,person.ClientId)) return new(ModuleAccessStatus.Denied);
        try {
            await using var db=contexts("untrusted-session-token");var hash=Hash(token);
            var row=await db.ProductSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash,ct);
            return row is null?new(ModuleAccessStatus.Denied):await ResolveIn(db,row,person,license,credential,browser,request,ct);
        } catch(Exception error)when(error is DbException or CryptographicException or FormatException or ArgumentException){return new(ModuleAccessStatus.Unavailable);}
    }
    internal async Task<CoreErpScopeResult> ResolveIn(HrmDbContext db,CoreProductSession row,OperationalSubjectObservation person,
        CommercialLicenseLease license,string credential,string browser,CoreErpScopeRequest request,CancellationToken ct)
    {
        var now=clock.GetUtcNow();
        if (row.PolicyVersion!=1 || row.IsArchived || row.Issuer!=ProductCommercialLicense.Issuer || row.Subject!=person.Subject || row.ClientId!=person.ClientId
            || row.CredentialHash!=Hash(credential) || row.BrowserHash!=Hash(browser) || now<row.IssuedAt || now>=row.ExpiresAt
            || row.ExpiresAt-row.IssuedAt>TimeSpan.FromHours(8) || row.ScopeKind is not ("company" or "locations")
            || await db.ProductSessionRevocations.IgnoreQueryFilters().AnyAsync(x=>x.SessionId==row.Id,ct)) return new(ModuleAccessStatus.Denied);
        var locations=await db.ProductSessionLocations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SessionId==row.Id).Take(129).ToArrayAsync(ct);
        if(locations.Any(x=>x.IsArchived || x.TenantId!=row.TenantId || x.LegalEntityId!=row.LegalEntityId)) return new(ModuleAccessStatus.Denied);
        var selected=locations.Select(x=>x.LocationId).ToArray();
        if (ScopeHash(row,selected)!=row.ScopeHash) return new(ModuleAccessStatus.Denied);
        var recovered=Protector(row).Unprotect(row.ProtectedToken);
        if(!Opaque(recovered)||Hash(recovered)!=row.TokenHash)return new(ModuleAccessStatus.Unavailable);
        return await new CoreErpSessionScopeAuthority(db,clock).AuthorizeAsync(new(row,selected),person,license,request,ct);
    }
    internal async Task<ProductSessionStatus> RevokeAsync(string token,string credential,string browser,string subject,string client,CancellationToken ct=default)
    {
        if(!Opaque(token)||!Opaque(credential)||!Opaque(browser)||!Identity(subject,client))return ProductSessionStatus.Denied;
        try {
            await using var lookup=contexts("untrusted-session-revoke");var hash=Hash(token);
            var row=await lookup.ProductSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash,ct);
            if(row is null || row.Issuer!=ProductCommercialLicense.Issuer || row.Subject!=subject || row.ClientId!=client || row.CredentialHash!=Hash(credential) || row.BrowserHash!=Hash(browser))return ProductSessionStatus.Denied;
            await using var db=contexts(row.TenantId);await using var tx=await db.Database.BeginTransactionAsync(db.Database.IsNpgsql()?IsolationLevel.ReadCommitted:IsolationLevel.Serializable,ct);
            await Lock(db,row.TenantId,subject,client,row.LegalEntityId,ct);
            if(await db.ProductSessionRevocations.IgnoreQueryFilters().AnyAsync(x=>x.SessionId==row.Id,ct))return ProductSessionStatus.Existing;
            Revoke(db,row,"logout",clock.GetUtcNow());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ProductSessionStatus.Revoked;
        }catch(Exception e)when(e is DbException or DbUpdateException){return ProductSessionStatus.Unavailable;}
    }
    private static async Task RevokeAcrossTenant(HrmDbContext db,CoreProductSession old,DateTimeOffset now,CancellationToken ct)
    {
        // The immutable row came from this verified subject/client/browser's
        // sessions under the actor lock. These parameterized writes remain in
        // the SAME transaction, while retaining the old row's owning tenant.
        var id=Guid.CreateVersion7();var actor=old.PrincipalId.ToString("D");var correlation=Hash(id.ToString("D"));
        var payload=JsonSerializer.Serialize(new{session_id=old.Id,company_id=old.LegalEntityId,grant_version_id=old.GrantVersionId,module=old.ModuleCode,outcome="replaced"});
        var revoke=db.Database.IsNpgsql()
            ? "INSERT INTO hrm.core_product_session_revocations (id,tenant_id,legal_entity_id,session_id,revoked_at,reason,created_at,created_by,is_archived) VALUES ({0},{1},{2},{3},{4},'replaced',{4},{5},FALSE)"
            : "INSERT INTO core_product_session_revocations (id,tenant_id,legal_entity_id,session_id,revoked_at,reason,created_at,created_by,is_archived) VALUES ({0},{1},{2},{3},{4},'replaced',{4},{5},FALSE)";
        await db.Database.ExecuteSqlRawAsync(revoke,new object[]{id,old.TenantId,old.LegalEntityId,old.Id,now,actor},ct);
        var audit=db.Database.IsNpgsql()
            ? "INSERT INTO hrm.audit_entries (id,tenant_id,entity_type,entity_id,action,actor_subject_id,after_json,correlation_id,created_at,created_by,is_archived) VALUES ({0},{1},'CoreProductSession',{2},'core.product.session.replaced',{3},{4},{5},{6},{3},FALSE)"
            : "INSERT INTO audit_entries (id,tenant_id,entity_type,entity_id,action,actor_subject_id,after_json,correlation_id,created_at,created_by,is_archived) VALUES ({0},{1},'CoreProductSession',{2},'core.product.session.replaced',{3},{4},{5},{6},{3},FALSE)";
        await db.Database.ExecuteSqlRawAsync(audit,new object[]{Guid.CreateVersion7(),old.TenantId,old.Id.ToString("D"),actor,payload,correlation,now},ct);
        var outbox=db.Database.IsNpgsql()
            ? "INSERT INTO hrm.outbox_messages (id,tenant_id,public_id,event_type,event_version,environment,subject_id,correlation_id,payload_json,status,publish_attempts,available_at,created_at,created_by,is_archived) VALUES ({0},{1},{2},'core.product.session.replaced.v1','1','production',{3},{4},CAST({5} AS jsonb),'pending',0,{6},{6},{7},FALSE)"
            : "INSERT INTO outbox_messages (id,tenant_id,public_id,event_type,event_version,environment,subject_id,correlation_id,payload_json,status,publish_attempts,available_at,created_at,created_by,is_archived) VALUES ({0},{1},{2},'core.product.session.replaced.v1','1','production',{3},{4},{5},'pending',0,{6},{6},{7},FALSE)";
        await db.Database.ExecuteSqlRawAsync(outbox,new object[]{Guid.CreateVersion7(),old.TenantId,id.ToString("D"),old.Id.ToString("D"),correlation,payload,now,actor},ct);
    }
    private static void Revoke(HrmDbContext db,CoreProductSession row,string reason,DateTimeOffset now)
    {
        var revocation=new CoreProductSessionRevocation{LegalEntityId=row.LegalEntityId,SessionId=row.Id,RevokedAt=now,Reason=reason,CreatedBy=row.PrincipalId.ToString("D"),CreatedAt=now};
        db.ProductSessionRevocations.Add(revocation);Evidence(db,row,reason,revocation.Id,now);
    }
    private static void Evidence(HrmDbContext db,CoreProductSession row,string outcome,Guid evidenceId,DateTimeOffset now)
    {
        var payload=JsonSerializer.Serialize(new{session_id=row.Id,company_id=row.LegalEntityId,grant_version_id=row.GrantVersionId,module=row.ModuleCode,outcome});
        db.AuditEntries.Add(new(){EntityType="CoreProductSession",EntityId=row.Id.ToString("D"),Action="core.product.session."+outcome,
            ActorSubjectId=row.PrincipalId.ToString("D"),AfterJson=payload,CreatedAt=now,CreatedBy=row.PrincipalId.ToString("D")});
        db.OutboxMessages.Add(new(){PublicId=evidenceId.ToString("D"),EventType="core.product.session."+outcome+".v1",SubjectId=row.Id.ToString("D"),
            CorrelationId=Hash(evidenceId.ToString("D")),PayloadJson=payload,AvailableAt=now,CreatedAt=now,CreatedBy=row.PrincipalId.ToString("D")});
    }
}
