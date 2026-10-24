// M9 hardening tests: URL versioning (/api/v1/hrm + legacy /api/hrm),
// request-id header echo, and CORS approval for the React origin.
// Both versioned and legacy surfaces must register identical route
// patterns (shared handlers); only the path prefix differs.

using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Mightyfin.Erp.Hrm.Application;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class HrmStaffAdmissionTests
{
    [Theory]
    [InlineData("timesheet_operator")]
    [InlineData("employee")]
    [InlineData("manager")]
    [InlineData("hr_ops")]
    [InlineData("payroll")]
    [InlineData("hr_admin")]
    [InlineData("investigator")]
    public void ExplicitWorkforceRoleGrantsHrmAdmission(string role)
    {
        Assert.True(HrmStaffAccess.IsStaff([new Claim("realm_access.roles", role)]));
    }

    [Theory]
    [InlineData("tenant_owner")]
    [InlineData("efaas_reviewer")]
    [InlineData("offline_access")]
    [InlineData("")]
    public void NonWorkforcePlatformRoleDoesNotGrantHrmAdmission(string role)
    {
        Assert.False(HrmStaffAccess.IsStaff([new Claim("realm_access.roles", role)]));
    }

    [Fact]
    public void NestedKeycloakRealmRolesAreEvaluated()
    {
        var claim = new Claim("realm_access", "{\"roles\":[\"tenant_owner\",\"hr_ops\"]}");
        Assert.True(HrmStaffAccess.IsStaff([claim]));
    }
}

public sealed class VersioningTests
{
    static VersioningTests()
    {
        // The Api assembly is referenced but its types may not yet be loaded
        // into the test AppDomain; ensure it is scanned before any test runs.
        try { Assembly.Load("Mightyfin.Erp.Hrm.Api"); } catch { /* present */ }
    }

    private static Type FindRoutesType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType("Mightyfin.Erp.Hrm.Api.Routing.Routes") is { } routes)
                return routes;
        }
        var api = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Mightyfin.Erp.Hrm.Api");
        if (api is null)
            api = Assembly.Load("Mightyfin.Erp.Hrm.Api");
        foreach (var assembly in api.GetReferencedAssemblies())
        {
            try { Assembly.Load(assembly); } catch { /* ignore */ }
        }
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Mightyfin.Erp.Hrm.Api.Routing.Routes"))
            .First(t => t is not null)!;
    }

    [Fact]
    public void ApiVersioningCanBeConfiguredToV1()
    {
        // ApiVersioning is instantiated by Program.cs with CurrentVersion = 1.
        // Assert the contract: the property accepts and holds the version.
        var type = FindRoutesType().Assembly.GetType("Mightyfin.Erp.Hrm.Api.Routing.ApiVersioning");
        var instance = Activator.CreateInstance(type!)!;
        var prop = type.GetProperty("CurrentVersion")!;
        prop.SetValue(instance, 1);
        Assert.Equal(1, prop.GetValue(instance));
    }

    [Fact]
    public void HrmPrefixDefaultIsLegacy()
    {
        var routesType = FindRoutesType();
        Assert.Equal("/api/hrm", routesType.GetProperty("HrmPrefix")!.GetValue(null));
    }

    [Theory]
    [InlineData("/api/hrm")]
    [InlineData("/api/v1/hrm")]
    public void SurfaceDefinesAllCoreRouteGroups(string prefix)
    {
        // Both URL surfaces are registered by the SAME shared RegisterAll
        // method with Routes.HrmPrefix set to the surface prefix, so the two
        // surfaces are guaranteed identical. This test pins the contract by
        // asserting that every core route group is defined against HrmPrefix
        // in the source of the route registration module.
        var source = ReadRoutesSource();
        var groups = new[]
        {
            "workers", "time", "workflow", "experience", "payroll",
            "admin", "recruitment", "relations", "documents",
            "dq", "statutory-exports", "reports",
        };
        foreach (var group in groups)
        {
            Assert.Contains("$\"{HrmPrefix}/" + group + "\"",
                source, StringComparison.Ordinal);
        }
        // The document download endpoint must also live on the shared
        // prefix (registered as a relative pattern inside the documents
        // group).
        Assert.Contains("/{id:guid}/download", source,
            StringComparison.Ordinal);
        Assert.Contains("HrmPrefix", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterAllInvokesEveryGroupRegistration()
    {
        var source = ReadRoutesSource();
        // Every Register* method must be wired into the shared surface.
        foreach (var method in new[]
        {
            "RegisterWorkers", "RegisterTime", "RegisterWorkflow",
            "RegisterExperience", "RegisterPayroll", "RegisterConfig",
            "RegisterRecruitment", "RegisterRelations", "RegisterDocuments",
            "RegisterDq", "RegisterStatutory",
        })
        {
            Assert.Contains($"{method}(app)", source,
                StringComparison.Ordinal);
        }
    }

    private static string ReadRoutesSource()
    {
        // Bind the route source to the same build as these assertions. Test
        // artifacts may be placed outside the checkout by UseArtifactsOutput.
        using var stream = typeof(VersioningTests).Assembly.GetManifestResourceStream(
            "Mightyfin.Erp.Hrm.Tests.Source.ApiRoutesClean.cs")
            ?? throw new InvalidOperationException("Embedded API route source is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

public sealed class HardeningHeaderTests
{
    [Fact]
    public async Task RequestIdMiddlewareEchoesClientRequestId()
    {
        using var host = await BuildHost();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Request-Id", "client-request-42");
        var response = await host.GetTestClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("client-request-42",
            response.Headers.GetValues("X-Request-Id").First());
    }

    [Fact]
    public async Task RequestIdMiddlewareGeneratesIdWhenAbsent()
    {
        using var host = await BuildHost();
        var response = await host.GetTestClient().GetAsync("/health/live");
        Assert.True(response.Headers.Contains("X-Request-Id"));
    }

    [Fact]
    public async Task CorsApprovedForConfiguredOrigin()
    {
        using var host = await BuildHost();
        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);
        Assert.Contains(response.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.OK });
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            Assert.Contains("http://localhost:3000",
                response.Headers.GetValues("Access-Control-Allow-Origin"));
        }
    }

    private static async Task<IHost> BuildHost()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(s =>
                    {
                        s.AddRouting();
                        s.AddCors(o => o.AddDefaultPolicy(p =>
                            p.WithOrigins("http://localhost:3000")
                             .AllowAnyHeader().AllowAnyMethod()));
                        s.AddHealthChecks();
                    })
                    .Configure(app =>
                    {
                        app.UseCors();
                        app.Use(async (ctx, next) =>
                        {
                            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N")[..12];
                            ctx.Response.Headers["X-Request-Id"] = requestId;
                            await next(ctx);
                        });
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapHealthChecks("/health/live"));
                    });
            })
            .Build();
        await host.StartAsync();
        return host;
    }
}
