using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal enum WorkspaceAuthorityStatus { Unavailable, Denied, Verified }
internal sealed record WorkspaceAuthorityResult(WorkspaceAuthorityStatus Status, WorkspaceSetupObservation? Observation = null);

/// <summary>Current payer control only: never a company, capacity or operational grant.</summary>
internal sealed class WorkspaceSetupObservation(string subject, string clientId, string customerId, string enrollmentId,
    int workspaceLimit, DateTimeOffset deadline, TimeProvider clock, long started, TimeSpan lifetime)
{
    internal string Subject { get; } = subject;
    internal string ClientId { get; } = clientId;
    internal string CustomerId { get; } = customerId;
    internal string EnrollmentId { get; } = enrollmentId;
    internal int WorkspaceLimit { get; } = workspaceLimit;
    internal DateTimeOffset ExpiresAt { get; } = deadline;
    internal bool IsCurrent => clock.GetUtcNow() < ExpiresAt
        && clock.GetElapsedTime(started) is var elapsed && elapsed >= TimeSpan.Zero && elapsed < lifetime;
}

/// <summary>Use only a server-held, separately consented person token; no token is retained.</summary>
internal sealed class ProductWorkspaceSetupAuthority(HttpClient http, TimeProvider clock, string clientId)
{
    internal const string HttpClientName = "kuubu-workspace-authority";
    private const string Prefix = ProductCommercialLicense.Issuer+"/api/v1/product-identity/customers/";
    private static bool ProviderId(string value) => Regex.IsMatch(value, "^[0-7][0-9a-hjkmnp-tv-z]{25}\\z", RegexOptions.CultureInvariant);

    internal async Task<WorkspaceAuthorityResult> ReadAsync(string subject, string customer, string accessToken,
        CancellationToken cancellation = default)
    {
        if (!Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
            || !ProviderId(customer) || accessToken.Length is < 1 or > 16384
            || !Regex.IsMatch(accessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant))
            return new(WorkspaceAuthorityStatus.Unavailable);
        try
        {
            var start = clock.GetUtcNow();
            var timestamp = clock.GetTimestamp();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, Prefix+customer+"/workspace-setup-authority");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return new(WorkspaceAuthorityStatus.Denied);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json")
                return new(WorkspaceAuthorityStatus.Unavailable);
            await response.Content.LoadIntoBufferAsync(65536, timeout.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(timeout.Token), new JsonDocumentOptions { MaxDepth = 8 });
            RejectDuplicates(json.RootElement);
            var data = json.RootElement.GetProperty("data");
            var capacity = data.GetProperty("workspace_limit");
            if (capacity.ValueKind != JsonValueKind.Number || !capacity.TryGetInt32(out var workspaceLimit)
                || workspaceLimit is < 0 or > 10000) return new(WorkspaceAuthorityStatus.Unavailable);
            var enrollment = data.GetProperty("product_enrollment_id").GetString();
            if (data.GetProperty("schema_version").GetInt32() != 1
                || data.GetProperty("issuer").GetString() != ProductCommercialLicense.Issuer
                || data.GetProperty("subject").GetString() != subject
                || data.GetProperty("client_id").GetString() != clientId
                || data.GetProperty("customer_id").GetString() != customer
                || data.GetProperty("product_code").GetString() != "kuubu"
                || data.GetProperty("action").GetString() != "request_workspace_creation"
                || !data.GetProperty("payer_control_verified").GetBoolean()
                || data.GetProperty("operational_access_granted").GetBoolean()
                || enrollment is null || !ProviderId(enrollment)) return new(WorkspaceAuthorityStatus.Unavailable);
            var observed = Date(data.GetProperty("observed_at"));
            var deadline = Date(data.GetProperty("authority_until"));
            var now = clock.GetUtcNow();
            var elapsed = clock.GetElapsedTime(timestamp);
            if (observed < start.AddSeconds(-5) || observed > now.AddSeconds(5)
                || deadline <= observed || deadline > observed.AddSeconds(60)
                || elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromSeconds(60)) return new(WorkspaceAuthorityStatus.Unavailable);
            var expiry = deadline < start.AddSeconds(60) ? deadline : start.AddSeconds(60);
            var observation = new WorkspaceSetupObservation(subject, clientId, customer, enrollment, workspaceLimit, expiry, clock, timestamp, expiry-start);
            return observation.IsCurrent ? new(WorkspaceAuthorityStatus.Verified, observation) : new(WorkspaceAuthorityStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return new(WorkspaceAuthorityStatus.Unavailable); }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException or ArgumentOutOfRangeException)
        { return new(WorkspaceAuthorityStatus.Unavailable); }
    }

    internal static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Ambiguous workspace authority.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicates(child);
    }

    private static DateTimeOffset Date(JsonElement value)
    {
        var text = value.GetString();
        if (text is null || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})\z", RegexOptions.CultureInvariant)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new JsonException("Invalid workspace authority date.");
        return date.ToUniversalTime();
    }
}

internal static class ProductWorkspaceSetupRegistration
{
    internal static IServiceCollection AddProductWorkspaceSetup(this IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue<bool>("ERP:WorkspaceSetup:Enabled")) return services;
        var id = config["ERP:ProductIdentity:ClientId"];
        if (!ProductIdentity.Enabled(config) || !config.GetValue<bool>("ERP:ProductLicensing:Enabled")
            || !Guid.TryParseExact(id, "D", out _))
            throw new InvalidOperationException("Workspace setup requires configured product identity and commercial licensing.");
        services.AddHttpClient(ProductWorkspaceSetupAuthority.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddSingleton(provider => new ProductWorkspaceSetupAuthority(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(ProductWorkspaceSetupAuthority.HttpClientName), TimeProvider.System, id!));
        services.AddSingleton(provider => new ProductSetupPayers(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(ProductWorkspaceSetupAuthority.HttpClientName), id!));
        return services;
    }
}
