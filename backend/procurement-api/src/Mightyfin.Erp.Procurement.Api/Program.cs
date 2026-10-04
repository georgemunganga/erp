using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
var migrateOnly = args.Contains("--apply-migrations-only", StringComparer.OrdinalIgnoreCase);
var connection = builder.Configuration.GetConnectionString("Procurement")
    ?? throw new InvalidOperationException("ConnectionStrings:Procurement is required.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProcurementScope, PrincipalProcurementScope>();
builder.Services.AddDbContext<ProcurementDbContext>(options =>
    options.UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__procurement_migrations", "procurement")));

if (!migrateOnly)
{
    var authMode = builder.Configuration["ERP:AuthMode"] ?? "oidc";
    if (authMode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Procurement authentication cannot be disabled outside Development.");
        builder.Services.AddAuthentication("development")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthHandler>("development", _ => { });
    }
    else if (authMode.Equals("oidc", StringComparison.OrdinalIgnoreCase))
    {
        var authority = builder.Configuration["ERP:OidcAuthority"]
            ?? throw new InvalidOperationException("ERP:OidcAuthority is required for Procurement OIDC.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["ERP:OidcAudience"]),
                    ValidAudience = builder.Configuration["ERP:OidcAudience"],
                    NameClaimType = "preferred_username",
                };
            });
    }
    else throw new InvalidOperationException("ERP:AuthMode must be oidc or Development-only disabled.");
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("procurement-scope", policy => policy.RequireAuthenticatedUser()
            .RequireClaim("tenant")
            .RequireClaim("legal_entity_id"));
    });
}

var app = builder.Build();
if (migrateOnly)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ProcurementDbContext>().Database.MigrateAsync();
    Console.WriteLine("Procurement migrations applied.");
    return;
}

app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", module = "procurement" }));
app.MapGet("/health/ready", async (ProcurementDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapGet("/api/procurement/v1/meta", (IProcurementScope scope) => Results.Ok(new
{
    module = "procurement", version = 1, scope.TenantId, scope.LegalEntityId,
    phase = "foundation", crudEnabled = false,
})).RequireAuthorization("procurement-scope");

await app.RunAsync();

public partial class Program;

internal sealed class PrincipalProcurementScope(IHttpContextAccessor accessor) : IProcurementScope
{
    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new InvalidOperationException("A request principal is required.");
    public string TenantId => User.FindFirstValue("tenant")
        ?? throw new InvalidOperationException("Tenant claim is required.");
    public Guid LegalEntityId => Guid.TryParse(User.FindFirstValue("legal_entity_id"), out var id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Legal entity claim is required.");
    public Guid? WorkLocationId => null;
    public Guid? OrgUnitId => null;
    public bool IsConfined => false;
    public string SubjectId => User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is required.");
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}

internal sealed class DevelopmentAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        // Explicit Development-only identity; no user-supplied header can choose a scope.
        var claims = new[]
        {
            new Claim("sub", "procurement-dev"),
            new Claim("tenant", "procurement-dev"),
            new Claim("legal_entity_id", "00000000-0000-0000-0000-000000000001"),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
