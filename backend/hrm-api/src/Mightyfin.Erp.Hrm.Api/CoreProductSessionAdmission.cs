using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record CoreProductSessionState(ModuleAccessStatus Status,CoreErpRequestScope? Scope=null,DateTimeOffset? ExpiresAt=null);

/// <summary>Orchestrates current commercial licensing and exact registry authority.
/// Candidate/session rows are discovery only; no result grants module authority
/// until the registry verifies protected binding, current Core and license state.</summary>
internal sealed class CoreProductSessionAdmission(Func<string,HrmDbContext> contexts,IDataProtectionProvider protection,
    Func<string,CancellationToken,Task<CommercialLicenseResult>> readLicense,TimeProvider clock)
{
    private CoreProductSessionRegistry Registry()=>new(contexts,protection,clock);
    private bool Current(OperationalCredentialResult identity)=>identity.Status==OperationalCredentialStatus.Verified
        && identity.Observation is {IsCurrent:true} && identity.Material is {} material && material.ExpiresAt>clock.GetUtcNow()
        && ProductInvitationIntentStore.Opaque(material.Handle) && ProductInvitationIntentStore.Opaque(material.BrowserBinding);
    internal async Task<CoreProductSessionResult> IssueAsync(OperationalCredentialResult identity,CoreErpSessionSelection selection,Guid key,CancellationToken ct=default)
    {
        if(!Current(identity))return new(ProductSessionStatus.Denied);
        var ids=selection.LocationIds?.ToArray();
        if(key==Guid.Empty || selection.CompanyId==Guid.Empty || selection.LinkId==Guid.Empty || selection.Module is not ("hrm" or "procurement")
            || selection.ExpectedGrantVersionId==Guid.Empty || selection.ExpectedGrantRevision<1 || ids is null || ids.Length>128
            || ids.Any(x=>x==Guid.Empty) || ids.Distinct().Count()!=ids.Length
            || selection.AllCompanyLocations!=(ids.Length==0) || selection.ConfirmCompanyWide!=selection.AllCompanyLocations)return new(ProductSessionStatus.Invalid);
        selection=selection with{LocationIds=ids};
        try {
            await using var db=contexts("untrusted-session-admission");
            var candidate=await new CoreCompanyAccess(db,clock).FindCandidateAsync(ProductCommercialLicense.Issuer,identity.Observation!.Subject,selection.LinkId,selection.CompanyId,ct);
            if(candidate is null || !Current(identity))return new(ProductSessionStatus.Denied);
            var license=await readLicense(candidate.EnrollmentId,ct);
            if(!Current(identity))return new(ProductSessionStatus.Denied);
            if(license.Status!=CommercialLicenseStatus.Authorized || license.Lease is null)
                return new(license.Status==CommercialLicenseStatus.Denied?ProductSessionStatus.Denied:ProductSessionStatus.Unavailable);
            var material=identity.Material!;
            return await Registry().IssueAsync(identity.Observation!,license.Lease,new(selection,key,material.Handle,material.BrowserBinding,material.ExpiresAt),ct);
        }catch(DbException){return new(ProductSessionStatus.Unavailable);}
    }
    internal async Task<ModuleAccessReadResult> ReviewAsync(OperationalCredentialResult identity,Guid company,Guid link,string module,CancellationToken ct=default)
    {
        if(!Current(identity))return new(ModuleAccessStatus.Denied);
        if(company==Guid.Empty || link==Guid.Empty || module is not ("hrm" or "procurement"))return new(ModuleAccessStatus.Invalid);
        try {
            await using var db=contexts("untrusted-session-review");var person=identity.Observation!;
            var candidate=await new CoreCompanyAccess(db,clock).FindCandidateAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,ct);
            if(candidate is null || !Current(identity))return new(ModuleAccessStatus.Denied);
            var license=await readLicense(candidate.EnrollmentId,ct);
            if(!Current(identity))return new(ModuleAccessStatus.Denied);
            if(license.Status!=CommercialLicenseStatus.Authorized || license.Lease is null)
                return new(license.Status==CommercialLicenseStatus.Denied?ModuleAccessStatus.Denied:ModuleAccessStatus.Unavailable);
            var result=await new CoreModuleAccessReader(db,clock).ReadAsync(person,link,company,module,license.Lease,ct);
            if(result.Status!=ModuleAccessStatus.Authorized || result.Decision is not {} grant)return new(result.Status);
            // Validate the complete offered scope again; this preview issues no
            // session and cannot replace the user's later scope confirmation.
            var preview=await new CoreErpSessionScopeAuthority(db,clock).SelectAsync(person,license.Lease,
                new(company,link,module,grant.VersionId,grant.Revision,grant.AllCompanyLocations,grant.LocationIds.ToArray(),grant.AllCompanyLocations),ct);
            return preview.Status==ModuleAccessStatus.Authorized && Current(identity) && grant.IsCurrent
                ?result:new(preview.Status==ModuleAccessStatus.Authorized?ModuleAccessStatus.Denied:preview.Status);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
    internal Task<ProductSessionStatus> RevokeAsync(string token,string credential,string browser,string subject,string client,CancellationToken ct=default)
        =>Registry().RevokeAsync(token,credential,browser,subject,client,ct);
    internal async Task<CoreProductSessionState> ReadAsync(OperationalCredentialResult identity,string token,CancellationToken ct=default)
    {
        if(!Current(identity) || !ProductInvitationIntentStore.Opaque(token))return new(ModuleAccessStatus.Denied);
        try {
            await using var db=contexts("untrusted-session-state");
            var material=identity.Material!;var person=identity.Observation!;
            var hash=ProductInvitationIntentStore.Hash(token);var credential=ProductInvitationIntentStore.Hash(material.Handle);var browser=ProductInvitationIntentStore.Hash(material.BrowserBinding);
            var row=await db.ProductSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash
                && x.CredentialHash==credential && x.BrowserHash==browser && x.Issuer==ProductCommercialLicense.Issuer && x.Subject==person.Subject && x.ClientId==person.ClientId,ct);
            if(row is null || row.ExpiresAt<=clock.GetUtcNow() || row.ExpiresAt>material.ExpiresAt || !Current(identity))return new(ModuleAccessStatus.Denied);
            var candidate=await new CoreCompanyAccess(db,clock).FindCandidateAsync(ProductCommercialLicense.Issuer,person.Subject,row.LinkId,row.LegalEntityId,ct);
            if(candidate is null || !Current(identity))return new(ModuleAccessStatus.Denied);
            var license=await readLicense(candidate.EnrollmentId,ct);
            if(!Current(identity))return new(ModuleAccessStatus.Denied);
            if(license.Status!=CommercialLicenseStatus.Authorized || license.Lease is null)
                return new(license.Status==CommercialLicenseStatus.Denied?ModuleAccessStatus.Denied:ModuleAccessStatus.Unavailable);
            var locations=await db.ProductSessionLocations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.SessionId==row.Id).Select(x=>x.LocationId).Take(129).ToArrayAsync(ct);
            if(locations.Length>128)return new(ModuleAccessStatus.Denied);
            var result=await Registry().ResolveAsync(token,material.Handle,material.BrowserBinding,person,license.Lease,
                new(row.LegalEntityId,row.ModuleCode,row.ModuleCode=="hrm"?"hrm-access":"procurement-access",row.ScopeKind=="company",locations),ct);
            return result.Status==ModuleAccessStatus.Authorized && result.Scope is {IsCurrent:true} && Current(identity)
                ?new(result.Status,result.Scope,row.ExpiresAt):new(result.Status==ModuleAccessStatus.Authorized?ModuleAccessStatus.Denied:result.Status);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
}
