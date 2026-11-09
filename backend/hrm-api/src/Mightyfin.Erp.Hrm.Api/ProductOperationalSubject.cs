using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal enum OperationalSubjectStatus { Unavailable,Denied,Verified }
internal sealed record OperationalSubjectResult(OperationalSubjectStatus Status,OperationalSubjectObservation? Observation=null);

/// <summary>Current enrolled person only. Company membership, module grants and licensing remain separate.</summary>
internal sealed class OperationalSubjectObservation(string subject,string client,DateTimeOffset expires,
    TimeProvider clock,long started,TimeSpan lifetime)
{
    internal string Subject {get;}=subject;
    internal string ClientId {get;}=client;
    internal DateTimeOffset ExpiresAt {get;}=expires;
    internal OperationalSubjectObservation Constrain(DateTimeOffset deadline)
    {
        var cap=deadline<ExpiresAt?deadline:ExpiresAt;
        return new(Subject,ClientId,cap,clock,started,cap-(ExpiresAt-lifetime));
    }
    internal bool IsCurrent=>clock.GetUtcNow()<ExpiresAt && clock.GetElapsedTime(started) is var elapsed
        && elapsed>=TimeSpan.Zero && elapsed<lifetime;
}

/// <summary>Uses a server-held, separately scoped person token; never a cached or browser-asserted identity.</summary>
internal sealed class ProductOperationalSubject(HttpClient http,TimeProvider clock,string client)
{
    internal const string Endpoint=ProductCommercialLicense.Issuer+"/api/v1/product-identity/operational-subject";
    private static readonly HashSet<string> Fields=["schema_version","issuer","subject","client_id","product_code","purpose","verified","observed_at","proof_until","operational_access_granted"];
    internal async Task<OperationalSubjectResult> ReadAsync(string subject,string token,CancellationToken cancellation=default)
    {
        if(!Guid.TryParseExact(client,"D",out var id) || id==Guid.Empty || id.ToString("D")!=client
            || subject is null || !Regex.IsMatch(subject,@"\A[1-9][0-9]{0,18}\z",RegexOptions.CultureInvariant)
            || token is null || token.Length is <1 or >16384 || !Regex.IsMatch(token,@"\A[A-Za-z0-9._~-]+\z",RegexOptions.CultureInvariant))
            return new(OperationalSubjectStatus.Unavailable);
        try {
            var start=clock.GetUtcNow();var timestamp=clock.GetTimestamp();
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellation);budget.CancelAfter(TimeSpan.FromSeconds(10));
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,budget.Token);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)return new(OperationalSubjectStatus.Denied);
            if(response.StatusCode!=HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType!="application/json")return new(OperationalSubjectStatus.Unavailable);
            await response.Content.LoadIntoBufferAsync(8192,budget.Token);
            using var json=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(budget.Token),new JsonDocumentOptions{MaxDepth=4});
            ProductWorkspaceSetupAuthority.RejectDuplicates(json.RootElement);
            if(json.RootElement.EnumerateObject().Count()!=1)return new(OperationalSubjectStatus.Unavailable);
            var data=json.RootElement.GetProperty("data");var fields=data.EnumerateObject().Select(x=>x.Name).ToArray();
            if(fields.Length!=Fields.Count || fields.Any(x=>!Fields.Contains(x))
                || data.GetProperty("schema_version").GetInt32()!=1 || data.GetProperty("issuer").GetString()!=ProductCommercialLicense.Issuer
                || data.GetProperty("subject").GetString()!=subject || data.GetProperty("client_id").GetString()!=client
                || data.GetProperty("product_code").GetString()!="kuubu" || data.GetProperty("purpose").GetString()!="erp-session-v1"
                || !data.GetProperty("verified").GetBoolean() || data.GetProperty("operational_access_granted").GetBoolean())return new(OperationalSubjectStatus.Unavailable);
            var observed=Date(data.GetProperty("observed_at"));var deadline=Date(data.GetProperty("proof_until"));
            var now=clock.GetUtcNow();var elapsed=clock.GetElapsedTime(timestamp);
            if(observed<start.AddSeconds(-5) || observed>now.AddSeconds(5) || deadline<=observed || deadline>observed.AddSeconds(30)
                || elapsed<TimeSpan.Zero || elapsed>=TimeSpan.FromSeconds(30))return new(OperationalSubjectStatus.Unavailable);
            var expires=deadline<start.AddSeconds(30)?deadline:start.AddSeconds(30);
            var proof=new OperationalSubjectObservation(subject,client,expires,clock,timestamp,expires-start);
            return proof.IsCurrent?new(OperationalSubjectStatus.Verified,proof):new(OperationalSubjectStatus.Unavailable);
        }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){return new(OperationalSubjectStatus.Unavailable);}
        catch(Exception error)when(error is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or ArgumentOutOfRangeException){return new(OperationalSubjectStatus.Unavailable);}
    }
    private static DateTimeOffset Date(JsonElement value)
    {
        var text=value.GetString();if(text is null || !Regex.IsMatch(text,@"(?:Z|[+-][0-9]{2}:[0-9]{2})\z",RegexOptions.CultureInvariant))throw new FormatException();
        return DateTimeOffset.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.None);
    }
}
