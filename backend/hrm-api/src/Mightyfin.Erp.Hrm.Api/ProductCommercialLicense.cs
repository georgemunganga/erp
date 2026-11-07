using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal enum CommercialLicenseStatus { Unavailable, Denied, Authorized }
internal sealed record CommercialLicenseResult(CommercialLicenseStatus Status, CommercialLicenseLease? Lease = null);

/// <summary>Commercial entitlement only. Core must separately authorize the person and workspace.</summary>
internal sealed class CommercialLicenseLease
{
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly TimeSpan lifetime;
    private readonly FrozenSet<string> modules;
    internal string EnrollmentId { get; }
    internal DateTimeOffset ExpiresAt { get; }
    internal CommercialLicenseLease(string enrollment, IEnumerable<string> modules, DateTimeOffset expiry,
        TimeProvider clock, long started, TimeSpan lifetime)
    {
        EnrollmentId = enrollment;
        this.modules = modules.ToFrozenSet(StringComparer.Ordinal);
        ExpiresAt = expiry;
        this.clock = clock;
        this.started = started;
        this.lifetime = lifetime;
    }
    internal bool IsCurrent => clock.GetUtcNow() < ExpiresAt
        && clock.GetElapsedTime(started) is var elapsed && elapsed >= TimeSpan.Zero && elapsed < lifetime;
    internal bool AllowsModule(string module) => IsCurrent && modules.Contains(module);
}

/// <summary>Fixed service-to-service channel. Never use a browser-supplied payer as company authority.</summary>
internal sealed class ProductCommercialLicense(HttpClient http, TimeProvider clock, string clientId, string secret)
{
    internal const string Issuer = "https://commercial-api.greenwebb.tech";
    internal const string HttpClientName = "kuubu-commercial-license";
    private const string Prefix = Issuer+"/api/v1/product-identity";
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private MachineToken? token;
    // Laravel HasUlids emits lowercase keys. Preserve that exact provider ID;
    // never case-fold a browser or binding reference into a different DB key.
    private static readonly Regex EnrollmentPattern = new("^[0-7][0-9a-hjkmnp-tv-z]{25}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex ModulePattern = new("^[a-z][a-z0-9_-]{0,63}\\z", RegexOptions.CultureInvariant);
    private sealed record MachineToken(string Value, DateTimeOffset Expiry, long Started, TimeSpan Lifetime);

    internal async Task<CommercialLicenseResult> ReadAsync(string enrollment, CancellationToken cancellation = default)
    {
        // This reference is internal input from the future Core-owned binding, not an API grant.
        if (!EnrollmentPattern.IsMatch(enrollment)) return new(CommercialLicenseStatus.Unavailable);
        try
        {
            var currentToken = await GetToken(cancellation);
            if (currentToken is null) return new(CommercialLicenseStatus.Unavailable);
            var start = clock.GetUtcNow();
            var timestamp = clock.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Get, Prefix+"/licensing/enrollments/"+enrollment);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken.Value);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Interlocked.CompareExchange(ref token, null, currentToken);
                return new(CommercialLicenseStatus.Unavailable);
            }
            if (response.StatusCode == HttpStatusCode.NotFound) return new(CommercialLicenseStatus.Denied);
            if (response.StatusCode != HttpStatusCode.OK) return new(CommercialLicenseStatus.Unavailable);
            using var json = await ReadJson(response, timeout.Token);
            var data = Property(json.RootElement, "data");
            if (Property(data, "schema_version").GetInt32() != 1
                || Property(data, "issuer").GetString() != Issuer
                || Property(data, "product_code").GetString() != "kuubu"
                || Property(data, "product_enrollment_id").GetString() != enrollment
                || Property(data, "operational_access_granted").GetBoolean())
                return new(CommercialLicenseStatus.Unavailable);
            var observed = Date(Property(data, "observed_at"));
            var deadline = Date(Property(data, "lease_until"));
            var now = clock.GetUtcNow();
            var elapsed = clock.GetElapsedTime(timestamp);
            if (observed < start.AddSeconds(-5) || observed > now.AddSeconds(5)
                || elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromSeconds(60))
                return new(CommercialLicenseStatus.Unavailable);
            var moduleData = Property(data, "module_codes");
            if (moduleData.ValueKind != JsonValueKind.Array || moduleData.GetArrayLength() > 128)
                return new(CommercialLicenseStatus.Unavailable);
            var modules = moduleData.EnumerateArray().Select(x => x.GetString()).ToArray();
            if (modules.Any(x => x is null || !ModulePattern.IsMatch(x)) || modules.Distinct(StringComparer.Ordinal).Count() != modules.Length)
                return new(CommercialLicenseStatus.Unavailable);
            if (!Property(data, "commercial_access_authorized").GetBoolean())
                return modules.Length == 0 && Property(data, "access_until").ValueKind == JsonValueKind.Null && deadline == observed
                    ? new(CommercialLicenseStatus.Denied) : new(CommercialLicenseStatus.Unavailable);
            var accessUntil = Date(Property(data, "access_until"));
            if (deadline <= observed || deadline > observed.AddSeconds(60) || deadline > accessUntil || accessUntil <= now)
                return new(CommercialLicenseStatus.Unavailable);
            // Conservative local cap includes the full resource round-trip; monotonic time
            // prevents a backward wall-clock correction from prolonging the commercial lease.
            var expiry = deadline < start.AddSeconds(60) ? deadline : start.AddSeconds(60);
            var lease = new CommercialLicenseLease(enrollment, modules.Select(x => x!), expiry, clock, timestamp, expiry-start);
            return lease.IsCurrent ? new(CommercialLicenseStatus.Authorized, lease) : new(CommercialLicenseStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return new(CommercialLicenseStatus.Unavailable); }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException or ArgumentOutOfRangeException)
        { return new(CommercialLicenseStatus.Unavailable); }
    }

    private bool Usable(MachineToken? value) => value is not null && clock.GetUtcNow() < value.Expiry
        && clock.GetElapsedTime(value.Started) is var elapsed && elapsed >= TimeSpan.Zero && elapsed < value.Lifetime;

    private async Task<MachineToken?> GetToken(CancellationToken cancellation)
    {
        var cached = Volatile.Read(ref token);
        if (Usable(cached)) return cached;
        await tokenLock.WaitAsync(cancellation);
        try
        {
            cached = Volatile.Read(ref token);
            if (Usable(cached)) return cached;
            var start = clock.GetUtcNow();
            var timestamp = clock.GetTimestamp();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post, Prefix+"/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                    ["grant_type"] = "client_credentials", ["client_id"] = clientId,
                    ["client_secret"] = secret, ["scope"] = "product:license" })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK) return null;
            using var json = await ReadJson(response, timeout.Token);
            var body = json.RootElement;
            var value = Property(body, "access_token").GetString();
            var seconds = Property(body, "expires_in").GetInt32();
            if (Property(body, "token_type").GetString() != "Bearer" || seconds is < 6 or > 120
                || string.IsNullOrEmpty(value) || value.Length > 16384
                || !Regex.IsMatch(value, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant)
                || body.TryGetProperty("refresh_token", out _)
                || (body.TryGetProperty("scope", out var scope) && scope.GetString() != "product:license")) return null;
            var created = new MachineToken(value, start.AddSeconds(seconds-5), timestamp, TimeSpan.FromSeconds(seconds-5));
            if (!Usable(created)) return null;
            Volatile.Write(ref token, created);
            return created;
        }
        finally { tokenLock.Release(); }
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
            throw new JsonException("Invalid commercial response.");
        await response.Content.LoadIntoBufferAsync(65536, cancellation);
        var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellation), new JsonDocumentOptions { MaxDepth = 8 });
        try { RejectDuplicateProperties(json.RootElement); return json; }
        catch { json.Dispose(); throw; }
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Ambiguous commercial response.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
    private static JsonElement Property(JsonElement element, string name) => element.GetProperty(name);
    private static DateTimeOffset Date(JsonElement value)
    {
        var text = value.GetString();
        if (text is null || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})\z", RegexOptions.CultureInvariant)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new JsonException("Invalid commercial date.");
        return date;
    }
}

internal static class ProductCommercialLicensingRegistration
{
    internal static IServiceCollection AddProductCommercialLicense(this IServiceCollection services, IConfiguration config)
    {
        if (!ProductIdentity.Enabled(config) || !config.GetValue<bool>("ERP:ProductLicensing:Enabled")) return services;
        var id = config["ERP:ProductLicensing:ClientId"];
        var credential = config["ERP:ProductLicensing:ClientSecret"];
        if (!Guid.TryParseExact(id, "D", out var licensingId) || string.IsNullOrWhiteSpace(credential) || credential.Length > 512
            || (Guid.TryParse(config["ERP:ProductIdentity:ClientId"], out var identityId) && identityId == licensingId))
            throw new InvalidOperationException("A separate confidential commercial licensing client is required.");
        services.AddHttpClient(ProductCommercialLicense.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddSingleton(provider => new ProductCommercialLicense(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(ProductCommercialLicense.HttpClientName), TimeProvider.System, id!, credential));
        return services;
    }

}
