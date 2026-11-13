using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed class OperationalTokenPair(string access,string refresh,DateTimeOffset expires)
{
    internal string Access {get;}=access;
    internal string Refresh {get;}=refresh;
    internal DateTimeOffset ExpiresAt {get;}=expires;
    internal static bool ValidAccess(string? value)=>value is {Length:>0 and <=16384}
        && Regex.IsMatch(value,@"\A[A-Za-z0-9._~-]+\z",RegexOptions.CultureInvariant);
    internal static bool ValidRefresh(string? value)=>value is {Length:>0 and <=16384} && value.All(c=>c is >= '!' and <= '~');
    public override string ToString()=>nameof(OperationalTokenPair);
}
internal enum OperationalRefreshStatus { Refreshed,Denied,Unavailable }
internal sealed record OperationalRefreshResult(OperationalRefreshStatus Status,OperationalTokenPair? Tokens=null);

/// <summary>Rotating confidential-client refresh only. A fresh person observation is still mandatory.</summary>
internal sealed class ProductOperationalRefresh(HttpClient http,TimeProvider clock,string client,string secret)
{
    internal const string Endpoint=ProductCommercialLicense.Issuer+"/api/v1/product-identity/token";
    internal async Task<OperationalRefreshResult> RefreshAsync(string token,CancellationToken cancellation=default)
    {
        if(!Guid.TryParseExact(client,"D",out var id) || id==Guid.Empty || id.ToString("D")!=client
            || string.IsNullOrWhiteSpace(secret) || secret.Length>512 || !OperationalTokenPair.ValidRefresh(token))return new(OperationalRefreshStatus.Unavailable);
        try {
            var start=clock.GetUtcNow();var timestamp=clock.GetTimestamp();
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellation);budget.CancelAfter(TimeSpan.FromSeconds(10));
            using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint){Content=new FormUrlEncodedContent(new Dictionary<string,string>{
                ["grant_type"]="refresh_token",["client_id"]=client,["client_secret"]=secret,["refresh_token"]=token})};
            request.Headers.Accept.ParseAdd("application/json");
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,budget.Token);
            if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)return new(OperationalRefreshStatus.Denied);
            if(response.StatusCode!=HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType!="application/json")return new(OperationalRefreshStatus.Unavailable);
            await response.Content.LoadIntoBufferAsync(65536,budget.Token);
            using var json=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(budget.Token),new JsonDocumentOptions{MaxDepth=3});
            ProductWorkspaceSetupAuthority.RejectDuplicates(json.RootElement);var root=json.RootElement;
            var fields=root.EnumerateObject().Select(x=>x.Name).ToArray();
            if(fields.Length is <4 or >5 || fields.Any(x=>x is not ("access_token" or "refresh_token" or "token_type" or "expires_in" or "scope"))
                || root.GetProperty("token_type").GetString()!="Bearer" || !root.GetProperty("expires_in").TryGetInt32(out var seconds) || seconds is <1 or >300
                || root.TryGetProperty("scope",out var scope) && scope.GetString() is not ("product:identity product:erp-session" or "product:erp-session product:identity"))return new(OperationalRefreshStatus.Unavailable);
            var access=root.GetProperty("access_token").GetString();var refresh=root.GetProperty("refresh_token").GetString();
            var elapsed=clock.GetElapsedTime(timestamp);var expires=start.AddSeconds(seconds);
            if(!OperationalTokenPair.ValidAccess(access) || !OperationalTokenPair.ValidRefresh(refresh) || refresh==token
                || elapsed<TimeSpan.Zero || elapsed>=TimeSpan.FromSeconds(seconds) || clock.GetUtcNow()<start || expires<=clock.GetUtcNow())return new(OperationalRefreshStatus.Unavailable);
            return new(OperationalRefreshStatus.Refreshed,new(access!,refresh!,expires));
        }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){return new(OperationalRefreshStatus.Unavailable);}
        catch(Exception error)when(error is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException
            or OverflowException or ArgumentOutOfRangeException){return new(OperationalRefreshStatus.Unavailable);}
    }
}
