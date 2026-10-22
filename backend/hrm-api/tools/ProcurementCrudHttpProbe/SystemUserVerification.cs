using System.Net;
using System.Text.Json;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    static async Task SystemUserContext(HttpClient client)
    {
        stage = "system-user-without-employee-context";
        using var request = new HttpRequestMessage(HttpMethod.Get, Root + "/context");
        request.Headers.Add("X-Crud-Actor", SystemActor.ToString("D"));
        request.Headers.Add("X-Shell-Entity", Company.ToString("D"));
        request.Headers.Add("X-Shell-Location", Branch.ToString("D"));
        using var response = await client.SendAsync(request); Interlocked.Increment(ref requests);
        Check(response.StatusCode == HttpStatusCode.OK, "Actual system-only Core user context was denied.");
        var context = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Check(context.GetProperty("principalId").GetGuid() == SystemActor
            && context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
            "System user context invented an employee or changed its actual Core identity.");
        var grants = context.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        Check(grants.Contains("procurement-access") && grants.Contains("procurement-vendor-create")
            && grants.Contains("procurement-request-create"), "Current actual Core grants were narrowed by employee absence.");
        Check(!context.TryGetProperty("adminViewOnly", out var viewOnly) || viewOnly.ValueKind == JsonValueKind.False,
            "Authorized system-only user was silently made read-only.");
    }
}
