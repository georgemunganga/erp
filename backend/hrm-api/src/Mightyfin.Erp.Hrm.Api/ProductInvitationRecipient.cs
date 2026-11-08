using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal enum InvitationRecipientStatus { Unavailable, Denied, Verified }
internal sealed record InvitationRecipientResult(InvitationRecipientStatus Status, InvitationRecipientObservation? Observation = null);

/// <summary>Identity observation only. Never a company membership or admission proof.</summary>
internal sealed class InvitationRecipientObservation(string subject, string clientId, string email,
    DateTimeOffset deadline, TimeProvider clock, long started, TimeSpan lifetime)
{
    internal string Subject { get; } = subject;
    internal string ClientId { get; } = clientId;
    internal string VerifiedEmail { get; } = email;
    internal DateTimeOffset ExpiresAt { get; } = deadline;
    internal bool IsCurrent => clock.GetUtcNow() < ExpiresAt
        && clock.GetElapsedTime(started) is var elapsed && elapsed >= TimeSpan.Zero && elapsed < lifetime;
}

/// <summary>Server-held separately scoped person token. No token, email directory or cached proof.</summary>
internal sealed class ProductInvitationRecipient(HttpClient http, TimeProvider clock, string clientId)
{
    internal const string Endpoint = ProductCommercialLicense.Issuer+"/api/v1/product-identity/invitation-recipient";

    internal async Task<InvitationRecipientResult> ReadAsync(string subject, string accessToken,
        CancellationToken cancellation = default)
    {
        if (!Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
            || accessToken.Length is < 1 or > 16384
            || !Regex.IsMatch(accessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant))
            return new(InvitationRecipientStatus.Unavailable);
        try
        {
            var start = clock.GetUtcNow(); var timestamp = clock.GetTimestamp();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return new(InvitationRecipientStatus.Denied);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json")
                return new(InvitationRecipientStatus.Unavailable);
            await response.Content.LoadIntoBufferAsync(8192, timeout.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(timeout.Token), new JsonDocumentOptions { MaxDepth = 4 });
            ProductWorkspaceSetupAuthority.RejectDuplicates(json.RootElement);
            var data = json.RootElement.GetProperty("data");
            var email = data.GetProperty("verified_email").GetString();
            if (data.GetProperty("schema_version").GetInt32() != 1
                || data.GetProperty("issuer").GetString() != ProductCommercialLicense.Issuer
                || data.GetProperty("subject").GetString() != subject
                || data.GetProperty("client_id").GetString() != clientId
                || data.GetProperty("product_code").GetString() != "kuubu"
                || data.GetProperty("purpose").GetString() != "company-invitation-v1"
                || !data.GetProperty("email_verified").GetBoolean()
                || data.GetProperty("operational_access_granted").GetBoolean()
                || !CanonicalEmail(email)) return new(InvitationRecipientStatus.Unavailable);
            var observed = Date(data.GetProperty("observed_at"));
            var deadline = Date(data.GetProperty("proof_until"));
            var now = clock.GetUtcNow(); var elapsed = clock.GetElapsedTime(timestamp);
            if (observed < start.AddSeconds(-5) || observed > now.AddSeconds(5)
                || deadline <= observed || deadline > observed.AddSeconds(30)
                || elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromSeconds(30))
                return new(InvitationRecipientStatus.Unavailable);
            var expiry = deadline < start.AddSeconds(30) ? deadline : start.AddSeconds(30);
            var observation = new InvitationRecipientObservation(subject, clientId, email!, expiry, clock, timestamp, expiry-start);
            return observation.IsCurrent ? new(InvitationRecipientStatus.Verified, observation) : new(InvitationRecipientStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return new(InvitationRecipientStatus.Unavailable); }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException or ArgumentOutOfRangeException)
        { return new(InvitationRecipientStatus.Unavailable); }
    }

    private static bool CanonicalEmail(string? email) => email is { Length: > 0 and <= 254 }
        && email.All(c => c is >= '!' and <= '~') && email == email.ToLowerInvariant()
        && MailAddress.TryCreate(email, out var address) && address.Address == email;

    private static DateTimeOffset Date(JsonElement value)
    {
        var text = value.GetString();
        if (text is null || !Regex.IsMatch(text, "(?:Z|[+-][0-9]{2}:[0-9]{2})\\z", RegexOptions.CultureInvariant))
            throw new FormatException();
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}
