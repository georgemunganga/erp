using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private const string FixtureBuyerRole = "probe_upstream_buyer";
    // Synthetic setup authority only, explicitly stored in Core. No administrator
    // wildcard, operational decision, authority review, Finance, bank or payment grant.
    private static string[] FixtureBuyerActions(bool orders, bool receiving, bool matching)
    {
        var actions = new List<string> { "procurement-access", "procurement-request-read", "procurement-request-create",
            "procurement-request-edit", "procurement-request-submit", "procurement-policy-evaluate",
            "procurement-approval-open", "procurement-approval-read", "procurement-authority-read",
            "procurement-authority-configure", "procurement-configuration-read", "procurement-configuration-edit" };
        if (orders) actions.AddRange(["procurement-po-read", "procurement-po-create", "procurement-po-edit",
            "procurement-po-convert", "procurement-po-submit", "procurement-po-issue", "procurement-po-amend"]);
        // Existing policy maker-self-publication negative must reach the actual
        // independence guard; missing permission alone must not make it pass.
        if (receiving) actions.AddRange(["procurement-receipt-view", "procurement-configuration-approve"]);
        if (matching) actions.AddRange(["procurement-invoice-read", "procurement-invoice-capture",
            "procurement-invoice-match", "procurement-vendor-read"]);
        return actions.ToArray();
    }

    private async Task VerifyUpstreamBuyerCoreGrants()
    {
        var context = await Get(Root + "/context", buyer);
        var actions = context.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        Require(actions.SetEquals(FixtureBuyerActions(true, true, true)), "Upstream buyer context differs from its finite stored role.");
        Guid ungranted;
        int authoritiesBefore;
        using (var services = Scope(buyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            Require(!await core.TenantRoleAssignments.AnyAsync(x => x.RoleKey == "erp_admin" && x.Active && !x.IsArchived),
                "Ungrantable administrator-role negative requires no stored role assignment.");
            var user = new LocalUser { Email = "ungranted-admin@example.test", NormalizedEmail = "UNGRANTED-ADMIN@EXAMPLE.TEST",
                DisplayName = "Synthetic ungranted administrator", PasswordHash = "synthetic-not-a-credential",
                RolesCsv = "erp_admin", IsActive = true, WorkerId = null };
            core.LocalUsers.Add(user); await core.SaveChangesAsync(); ungranted = user.Id;
            authoritiesBefore = await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().SpendingAuthorityGrants.CountAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, Root + "/context", ungranted);
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, Root + "/configuration/spending-authorities", ungranted,
            new { authorityReference = "SYNTHETIC-UNGRANTED-ADMIN", principalId = manager1.ToString("D"), action = "PurchaseRequest.Approve" });
        using (var services = Scope(buyer))
            Require(authoritiesBefore == await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().SpendingAuthorityGrants.CountAsync(),
                "Unconfigured administrator role created spending authority.");
    }
}
