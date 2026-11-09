using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record OperationalCompany(Guid CompanyId,Guid LinkId,string CompanyName,string Role);
internal sealed record OperationalCompanyPage(CompanyTeamReadStatus Status,OperationalCompany[]? Items=null,string? Next=null);
internal sealed record OperationalCompanyReview(CompanyTeamReadStatus Status,OperationalCompany? Company=null,string[]? LicensedModules=null);

/// <summary>Explicit person membership discovery and commercial review, never an ERP permission/session grant.</summary>
internal sealed class CoreOperationalCompanies(HrmDbContext db,
    Func<string,CancellationToken,Task<CommercialLicenseResult>> readLicense,TimeProvider clock)
{
    internal async Task<OperationalCompanyPage> DirectoryAsync(OperationalSubjectObservation person,Guid? after,CancellationToken ct=default)
    {
        if(!person.IsCurrent)return new(CompanyTeamReadStatus.Denied);
        try {
            var page=await new CoreCompanyAccess(db,clock).MemberDirectoryAsync(ProductCommercialLicense.Issuer,person.Subject,after,ct);
            return person.IsCurrent?new(CompanyTeamReadStatus.Ok,page.Rows.Select(x=>new OperationalCompany(x.CompanyId,x.LinkId,x.CompanyName,x.Role)).ToArray(),page.Next)
                :new(CompanyTeamReadStatus.Denied);
        }catch(DbException){return new(CompanyTeamReadStatus.Unavailable);}
    }
    internal async Task<OperationalCompanyReview> ReviewAsync(OperationalSubjectObservation person,Guid link,Guid company,CancellationToken ct=default)
    {
        if(!person.IsCurrent)return new(CompanyTeamReadStatus.Denied);
        try {
            var reader=new CoreCompanyAccess(db,clock);
            var candidate=await reader.FindCandidateAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,ct);
            if(candidate is null)return new(CompanyTeamReadStatus.Denied);
            var license=await readLicense(candidate.EnrollmentId,ct);
            if(!person.IsCurrent)return new(CompanyTeamReadStatus.Denied);
            if(license.Status!=CommercialLicenseStatus.Authorized || license.Lease is null)
                return new(license.Status==CommercialLicenseStatus.Denied?CompanyTeamReadStatus.Denied:CompanyTeamReadStatus.Unavailable);
            var access=await reader.ReadAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,license.Lease,ct);
            if(access is null || !person.IsCurrent || !license.Lease.IsCurrent)return new(CompanyTeamReadStatus.Denied);
            // Catalog allowances are informational; each module still needs an
            // implemented route and an explicit person/action/branch grant.
            var modules=license.Lease.ModuleCodes;
            return new(CompanyTeamReadStatus.Ok,new(candidate.CompanyId,candidate.LinkId,candidate.CompanyName,access.Role),modules);
        }catch(DbException){return new(CompanyTeamReadStatus.Unavailable);}
    }
    internal static bool Enabled(IConfiguration config)=>ProductOperationalFlow.Enabled(config)
        && config.GetValue<bool>("ERP:ProductLicensing:Enabled") && config.GetValue<bool>("ERP:OperationalCompanies:Enabled");
    internal static void Register(IServiceCollection services,IConfiguration config)
    {
        if(!config.GetValue<bool>("ERP:OperationalCompanies:Enabled"))return;
        if(!Enabled(config))throw new InvalidOperationException("Operational company discovery requires identity and licensing.");
        services.AddScoped(provider=>new CoreOperationalCompanies(provider.GetRequiredService<HrmDbContext>(),
            provider.GetRequiredService<ProductCommercialLicense>().ReadAsync,TimeProvider.System));
    }
    internal static void Map(RouteGroupBuilder group,IConfiguration config)
    {
        group.MapGet("/companies",async (HttpContext http)=> {
            if(!Enabled(config))return Results.StatusCode(503);
            Guid? after=null;
            if(http.Request.Query.TryGetValue("after",out var cursor)){if(!Canonical(cursor.ToString(),out var id))return Results.BadRequest();after=id;}
            var proof=await ProductOperationalFlow.ObserveAsync(http);
            if(proof.Status!=OperationalCredentialStatus.Verified || proof.Observation is null)return ProductOperationalFlow.Failure(proof.Status);
            var page=await http.RequestServices.GetRequiredService<CoreOperationalCompanies>().DirectoryAsync(proof.Observation,after,http.RequestAborted);
            if(page.Status!=CompanyTeamReadStatus.Ok)return page.Status==CompanyTeamReadStatus.Denied?Results.Unauthorized():Results.StatusCode(503);
            return Results.Ok(new{companies=page.Items!.Select(x=>new{company_id=x.CompanyId,link_id=x.LinkId,name=x.CompanyName,role=x.Role}),
                next_cursor=page.Next,operational_access_granted=false});
        }).WithMetadata(new OperationalQueryPolicy(["after"])).AllowAnonymous();
        group.MapGet("/companies/{companyId}",async(HttpContext http,string companyId)=> {
            if(!Enabled(config))return Results.StatusCode(503);
            if(!Canonical(companyId,out var company) || !Canonical(http.Request.Query["link_id"].ToString(),out var link))return Results.BadRequest();
            var proof=await ProductOperationalFlow.ObserveAsync(http);
            if(proof.Status!=OperationalCredentialStatus.Verified || proof.Observation is null)return ProductOperationalFlow.Failure(proof.Status);
            var review=await http.RequestServices.GetRequiredService<CoreOperationalCompanies>().ReviewAsync(proof.Observation,link,company,http.RequestAborted);
            if(review.Status!=CompanyTeamReadStatus.Ok)return review.Status==CompanyTeamReadStatus.Denied?Results.StatusCode(403):Results.StatusCode(503);
            var row=review.Company!;
            return Results.Ok(new{company=new{company_id=row.CompanyId,link_id=row.LinkId,name=row.CompanyName,role=row.Role},
                licensed_modules=review.LicensedModules,operational_access_granted=false,next_step="module_access_assignment"});
        }).WithMetadata(new OperationalQueryPolicy(["link_id"])).AllowAnonymous();
    }
    private static bool Canonical(string value,out Guid id)=>Guid.TryParseExact(value,"D",out id) && id!=Guid.Empty && id.ToString("D")==value;
}
internal sealed record OperationalQueryPolicy(string[] Allowed);
