using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Sidecar entry point: no migrations, bootstrap, browser routes, email or scheduled jobs.</summary>
internal static class OnPremMobileOnlyHost
{
    internal static bool Enabled(IConfiguration c)=>c.GetValue<bool>("ERP:OnPremMobile:Only");
    internal static async Task RunAsync(WebApplicationBuilder builder,string[] args)
    {
        if(!OnPremMobileOptions.Enabled(builder.Configuration) || args.Length!=0)
            throw new InvalidOperationException("Mobile-only hosting requires its explicit standalone profile and no operation flags.");
        // Keep the framework web host; scheduled business jobs belong to the existing ERP process.
        foreach(var descriptor in builder.Services.Where(x=>x.ServiceType==typeof(IHostedService)
            && (x.ImplementationType?.Name=="MonthlyLeaveAccrualWorker"
                || x.ImplementationType?.Namespace?.StartsWith("Mightyfin.",StringComparison.Ordinal)==true)).ToArray())builder.Services.Remove(descriptor);
        var app=builder.Build();var options=app.Services.GetRequiredService<OnPremMobileOptions>();
        var contexts=app.Services.GetRequiredService<Func<string,HrmDbContext>>();
        foreach(var company in options.Companies) {
            await using var db=contexts(company.Tenant);
            // Verify the additive session table and the explicitly configured company.
            // The sidecar must never run the full ERP migration or initial-admin bootstrap.
            await db.LocalMobileSessions.AsNoTracking().Take(1).ToArrayAsync();
            if(!await db.LegalEntities.AnyAsync(x=>x.Id==company.Id && !x.IsArchived))
                throw new InvalidOperationException("An enabled mobile company is not present in its configured tenant.");
        }
        app.UseRouting();OnPremMobileApi.UseBoundary(app);app.UseRateLimiter();OnPremMobileApi.Map(app);
        app.MapGet("/health/live",()=>Results.Ok(new{status="live",profile="local-mobile-only"}));
        app.MapGet("/health/ready",async()=>{
            try {
                await using var db=contexts(options.Companies[0].Tenant);
                await db.LocalMobileSessions.AsNoTracking().AnyAsync();
                return Results.Json(new{status="ready",profile="local-mobile-only"});
            }catch(System.Data.Common.DbException){return Results.Json(new{status="unavailable"},statusCode:503);}
        });
        await app.RunAsync();
    }
}
