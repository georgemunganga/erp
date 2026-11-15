using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class NativeAttendanceHttpTests
{
    [Theory]
    [InlineData("GET", "/hrm/me")]
    [InlineData("GET", "/hrm/me/attendance")]
    [InlineData("GET", "/hrm/me/attendance/today")]
    [InlineData("POST", "/hrm/me/attendance/clock-in")]
    [InlineData("POST", "/hrm/me/attendance/clock-out")]
    public async Task Mapped_handlers_execute_their_result_and_refuse_browser_cookies(string method, string path)
    {
        await using var app = await Start(true);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), NativeAttendanceApi.Prefix + path);
        request.Headers.Add("Cookie", "synthetic-browser-session=not-native-authority");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sign-in-required", body.GetProperty("error").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Existing_installations_publish_no_native_routes_when_the_feature_is_off()
    {
        await using var app = await Start(false);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(NativeAttendanceApi.Prefix + "/hrm/me");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplication> Start(bool enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ERP:MobileAttendance:Enabled"] = enabled.ToString(),
        });
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("native-attendance", policy => {
            policy.PermitLimit = 100; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0;
        }));
        var app = builder.Build();
        app.UseRouting(); app.UseRateLimiter(); NativeAttendanceApi.Map(app);
        await app.StartAsync(); return app;
    }
}
