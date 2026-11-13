using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record SetupPayer(string CustomerId, string Name, string EnrollmentId, int WorkspaceLimit);
internal sealed record SetupPayerPage(IReadOnlyList<SetupPayer> Customers, string? NextCursor);
internal sealed record SetupPayerResult(WorkspaceAuthorityStatus Status, SetupPayerPage? Page = null);

internal sealed class ProductSetupPayers(HttpClient http, string clientId)
{
    private static bool Id(string value) => Regex.IsMatch(value, "^[0-7][0-9a-hjkmnp-tv-z]{25}\\z", RegexOptions.CultureInvariant);
    internal async Task<SetupPayerResult> ReadAsync(ProductSetupGrant grant, string? after, CancellationToken ct)
    {
        if (grant.ClientId != clientId || (after is not null && !Id(after))) return new(WorkspaceAuthorityStatus.Unavailable);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var url = ProductCommercialLicense.Issuer+"/api/v1/product-identity/workspace-setup-payers";
            if (after is not null) url += "?after="+after;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", grant.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(WorkspaceAuthorityStatus.Denied);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json") return new(WorkspaceAuthorityStatus.Unavailable);
            await response.Content.LoadIntoBufferAsync(65536, timeout.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(timeout.Token), new JsonDocumentOptions { MaxDepth = 8 });
            ProductWorkspaceSetupAuthority.RejectDuplicates(json.RootElement);
            var data = json.RootElement.GetProperty("data");
            if (data.GetProperty("schema_version").GetInt32() != 1 || data.GetProperty("issuer").GetString() != ProductCommercialLicense.Issuer
                || data.GetProperty("subject").GetString() != grant.Subject || data.GetProperty("client_id").GetString() != clientId
                || data.GetProperty("product_code").GetString() != "kuubu" || data.GetProperty("operational_access_granted").GetBoolean()) return new(WorkspaceAuthorityStatus.Unavailable);
            var customers = data.GetProperty("customers");
            if (customers.ValueKind != JsonValueKind.Array || customers.GetArrayLength() > 20) return new(WorkspaceAuthorityStatus.Unavailable);
            var page = new List<SetupPayer>(); var previous = after;
            foreach (var row in customers.EnumerateArray())
            {
                var id = row.GetProperty("customer_id").GetString(); var name = row.GetProperty("name").GetString();
                var enrollment = row.GetProperty("product_enrollment_id").GetString(); var capacity = row.GetProperty("workspace_limit");
                if (id is null || !Id(id) || enrollment is null || !Id(enrollment)
                    || string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl)
                    || (previous is not null && string.CompareOrdinal(id, previous) <= 0)
                    || capacity.ValueKind != JsonValueKind.Number || !capacity.TryGetInt32(out var limit) || limit is < 1 or > 10000) return new(WorkspaceAuthorityStatus.Unavailable);
                page.Add(new(id, name, enrollment, limit)); previous = id;
            }
            var cursor = data.GetProperty("next_cursor"); var next = cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString();
            if (next is not null && (page.Count != 20 || !Id(next) || next != previous)) return new(WorkspaceAuthorityStatus.Unavailable);
            return new(WorkspaceAuthorityStatus.Verified, new SetupPayerPage(page, next));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(WorkspaceAuthorityStatus.Unavailable); }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException) { return new(WorkspaceAuthorityStatus.Unavailable); }
    }
}
