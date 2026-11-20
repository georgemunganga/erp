using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Guest admission remains address-bounded. Protected workloads use a
/// cryptographically verified browser/identity partition, never a supplied header
/// or an unverified ERP token. Budget admission grants no business authority.</summary>
internal sealed class OperationalRequestBudget
{
    private readonly TimeProvider clock;
    private readonly object sync = new();
    private readonly Dictionary<string, int> sessions = new(StringComparer.Ordinal);
    private long window;
    private int total;
    internal const int PerSession = 600, PerProcess = 6000, MaxSessions = 4096;
    internal ProductInvitationGuestBudget Budget { get; }
    internal OperationalRequestBudget(TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        window = this.clock.GetTimestamp();
        Budget = new(this.clock);
    }
    internal bool TryAuthenticated(string protectedPartition)
    {
        var key = ProductInvitationIntentStore.Hash(protectedPartition);
        lock (sync)
        {
            var now = clock.GetTimestamp();
            if (clock.GetElapsedTime(window, now) >= TimeSpan.FromMinutes(1))
            { sessions.Clear(); total = 0; window = now; }
            var used = sessions.GetValueOrDefault(key);
            if (used >= PerSession || total >= PerProcess || used == 0 && sessions.Count >= MaxSessions) return false;
            sessions[key] = used + 1; total++; return true;
        }
    }
    internal async Task<bool> TryAcquireAsync(HttpContext http, ProductInvitationGuestBudget.Operation operation)
    {
        string? partition = null;
        if (operation == ProductInvitationGuestBudget.Operation.Session)
        {
            var ticket = await http.AuthenticateAsync(ProductOperationalFlow.Scheme);
            var browser = http.RequestServices.GetRequiredService<ProductOperationalBrowser>().Read(http);
            var person = ticket.Principal;
            var handle = person?.FindFirstValue(ProductOperationalFlow.HandleClaim);
            if (ticket.Succeeded && browser is not null && ticket.Properties is { } properties
                && properties.ExpiresUtc > clock.GetUtcNow()
                && properties.Items.TryGetValue(ProductOperationalFlow.PurposeKey, out var purpose) && purpose == ProductOperationalFlow.Purpose
                && person?.FindFirstValue(ProductOperationalFlow.BindingClaim) == browser.Binding
                && person.FindFirstValue(ProductIdentity.IssuerClaim) == ProductCommercialLicense.Issuer
                && ProductInvitationIntentStore.Opaque(handle)
                && ProductAccountHub.MatchesSubject(http, person.FindFirstValue(ProductIdentity.SubjectClaim) ?? ""))
                partition = handle + "\n" + browser.Binding;
        }
        var allowed = partition is not null ? TryAuthenticated(partition) : Budget.TryAcquire(operation, http.Connection.RemoteIpAddress);
        if (!allowed) { http.Response.Headers.RetryAfter = "60"; http.Response.Headers.CacheControl = "no-store"; }
        return allowed;
    }
}
