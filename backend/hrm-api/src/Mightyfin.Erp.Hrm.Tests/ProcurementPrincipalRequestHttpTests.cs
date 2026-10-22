using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped routes and Core identity/scope middleware. No fabricated worker identity.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    private const string PrincipalRequests = "/api/procurement/v1/requests";

    [Fact]
    public async Task Active_system_user_without_employee_can_create_reload_edit_submit_and_withdraw_own_request()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, linkedEmployee: false);
        await fixture.ConfigurePrincipalRequester(fixture.Other, linkedEmployee: false);
        var needed = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests, fixture.Owner,
            new { purpose = "System user need", neededBy = needed, requesterPrincipalId = fixture.Other,
                requesterWorkerId = Guid.NewGuid(), lines = new[] {
                    new { description = "Office supplies", unitCode = "each", requestedQuantity = "2", estimatedUnitPrice = "10.0001" } } });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{PrincipalRequests}/{id}";
        Assert.Equal(fixture.Owner, created.GetProperty("requesterPrincipalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("requesterWorkerId").ValueKind);
        Assert.Equal("ZMW", created.GetProperty("currencyCode").GetString());
        Assert.Equal(1, created.GetProperty("version").GetInt64());
        var original = await fixture.PrincipalRequestStored(id);
        Assert.Equal(fixture.Owner, original.RequesterPrincipalId);
        Assert.Null(original.RequesterWorkerId);
        Assert.Equal(fixture.Owner.ToString("D"), original.CreatedBy);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other)).StatusCode);
        var otherList = await Send(fixture.Client, HttpMethod.Get, PrincipalRequests, fixture.Other);
        Assert.Equal(0, (await otherList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, company: fixture.OtherCompany)).StatusCode);

        var line = created.GetProperty("lines")[0].GetProperty("id").GetGuid();
        object edit(long version) => new { version, purpose = "Updated system user need", neededBy = needed,
            lines = new[] { new { id = line, description = "Office supplies", unitCode = "each", requestedQuantity = "3", estimatedUnitPrice = "10.0001" } } };
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner, edit(1));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("requesterWorkerId").ValueKind);
        var beforeReplay = await fixture.OriginalValueFootprint();
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner, edit(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Other, edit(2))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Post, path + "/withdraw",
            fixture.Other, new { version = 2, reason = "Not mine" })).StatusCode);
        Assert.Equal(beforeReplay, await fixture.OriginalValueFootprint());
        var readResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated system user need", read.GetProperty("purpose").GetString());
        Assert.Equal(2, read.GetProperty("version").GetInt64());
        var submittedResponse = await SendJson(fixture.Client, HttpMethod.Post, path + "/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(2));
        Assert.Equal(HttpStatusCode.OK, submittedResponse.StatusCode);
        var submitted = await submittedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        Assert.Equal("PendingPolicyAndBudget", submitted.GetProperty("approvalState").GetString());
        var withdrawnResponse = await SendJson(fixture.Client, HttpMethod.Post, path + "/withdraw", fixture.Owner,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Requirement cancelled" });
        Assert.Equal(HttpStatusCode.OK, withdrawnResponse.StatusCode);
        var withdrawn = await withdrawnResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Withdrawn", withdrawn.GetProperty("status").GetString());
        var stored = await fixture.PrincipalRequestStored(id);
        Assert.Equal(fixture.Owner, stored.RequesterPrincipalId); Assert.Null(stored.RequesterWorkerId);
        Assert.Equal(original.CreatedBy, stored.CreatedBy);
        Assert.Equal(0, await fixture.PrincipalRequestAuthorityEffects(id));
    }

    [Fact]
    public async Task Deactivating_employee_metadata_does_not_revoke_active_system_user_request_access()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, linkedEmployee: true);
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests,
            fixture.Owner, Draft("Before employee offboarding"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{PrincipalRequests}/{id}";
        var genuineWorker = created.GetProperty("requesterWorkerId").GetGuid();
        await fixture.DeactivatePrincipalEmployee(fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner)).StatusCode);
        var line = created.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, purpose = "After employee offboarding", currencyCode = "ZMW",
                lines = new[] { new { id = line, description = "Laptop", unitCode = "each", requestedQuantity = "2", estimatedUnitPrice = "100" } } });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(genuineWorker, updated.GetProperty("requesterWorkerId").GetGuid());
        Assert.Equal(fixture.Owner, updated.GetProperty("requesterPrincipalId").GetGuid());
        var newResponse = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests,
            fixture.Owner, Draft("New need without an active employee"));
        Assert.Equal(HttpStatusCode.Created, newResponse.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await newResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requesterWorkerId").ValueKind);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post, path + "/withdraw", fixture.Owner,
            new { version = updated.GetProperty("version").GetInt64(), reason = "No longer required" });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(genuineWorker, (await fixture.PrincipalRequestStored(id)).RequesterWorkerId);
    }

    [Fact]
    public async Task Legacy_canonical_creator_can_edit_without_worker_rebinding_but_unknown_creator_stays_denied()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, linkedEmployee: false);
        var id = await fixture.SeedPrincipalLegacyRequest(canonicalCreator: true);
        var unknown = await fixture.SeedPrincipalLegacyRequest(canonicalCreator: false);
        var original = await fixture.PrincipalRequestStored(id);
        var path = $"{PrincipalRequests}/{id}";
        var readResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, read.GetProperty("requesterPrincipalId").ValueKind);
        var line = read.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, purpose = "Legacy need revised by original system user", currencyCode = "ZMW",
                lines = new[] { new { id = line, description = "Legacy supplies", unitCode = "each", requestedQuantity = "2", estimatedUnitPrice = "10" } } });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("requesterPrincipalId").ValueKind);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post, path + "/withdraw", fixture.Owner,
            new { version = updated.GetProperty("version").GetInt64(), reason = "Legacy requirement ended" });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var retained = await fixture.PrincipalRequestStored(id);
        Assert.Null(retained.RequesterPrincipalId);
        Assert.Equal(original.RequesterWorkerId, retained.RequesterWorkerId);
        Assert.Equal(original.CreatedBy, retained.CreatedBy);
        var before = await fixture.OriginalValueFootprint();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{PrincipalRequests}/{unknown}", fixture.Owner)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{PrincipalRequests}/{unknown}/withdraw", fixture.Owner,
                new { version = 1, reason = "Noncanonical creator must not be guessed" })).StatusCode);
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    [Fact]
    public async Task Current_Core_permission_and_account_revocation_deny_even_when_old_request_claims_allow_action()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, linkedEmployee: false);
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests,
            fixture.Owner, Draft("Permission controlled need"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{PrincipalRequests}/{id}";
        var before = await fixture.OriginalValueFootprint();
        await fixture.RevokePrincipalRequestEdit();
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Post, path + "/withdraw",
            fixture.Owner, new { version = 1, reason = "Claims cannot restore revoked access" },
            actions: "procurement-access,procurement-request-read,procurement-request-edit")).StatusCode);
        await fixture.DeactivatePrincipalAccount(fixture.Owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner)).StatusCode);
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    private sealed partial class Fixture
    {
        public async Task ConfigurePrincipalRequester(Guid actor, bool linkedEmployee)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var account = await core.LocalUsers.SingleAsync(x => x.Id == actor);
            account.RolesCsv = "employee"; account.IsActive = true;
            if (!linkedEmployee) account.WorkerId = null;
            var role = await core.TenantRoleAssignments.SingleOrDefaultAsync(x => x.RoleKey == "employee");
            if (role is null)
            {
                role = new TenantRoleAssignment { RoleKey = "employee", RoleName = "Synthetic system requester", Active = true };
                core.TenantRoleAssignments.Add(role);
            }
            role.Active = true;
            role.PermissionsCsv = "procurement-access,procurement-request-read,procurement-request-create,procurement-request-edit,procurement-request-submit";
            var branch = await core.WorkLocations.Where(x => x.LegalEntityId == company).Select(x => x.Id).SingleAsync();
            if (!await core.UserBranchAssignments.AnyAsync(x => x.UserId == actor && x.LocationId == branch))
                core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = actor, LocationId = branch });
            await core.SaveChangesAsync();
        }
        public async Task<Guid> SeedPrincipalLegacyRequest(bool canonicalCreator)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var actualWorker = await core.Workers.Where(x => x.SubjectId == Owner.ToString("D")).Select(x => x.Id).SingleAsync();
            var actualBranch = await core.WorkLocations.Where(x => x.LegalEntityId == company).Select(x => x.Id).SingleAsync();
            // Explicit synthetic legacy fixture: preserve its genuine worker and original
            // attribution. It does not mint a new principal identity for historical rows.
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(canonicalCreator ? "D" : "N"), null, null, false, "legacy-principal-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = new PurchaseRequest { Number = "PR-LEGACY-" + Guid.NewGuid().ToString("N"),
                Purpose = "Legacy need", CurrencyCode = "ZMW", RequesterPrincipalId = null,
                RequesterWorkerId = actualWorker, BranchRef = actualBranch };
            request.Lines.Add(new PurchaseRequestLine { PurchaseRequestId = request.Id, LineNumber = 1,
                Description = "Legacy supplies", UnitCode = "each", RequestedQuantity = 1, EstimatedUnitPrice = 10 });
            db.AddScoped(request); await db.SaveChangesAsync(); return request.Id;
        }
        public async Task DeactivatePrincipalEmployee(Guid actor)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var account = await core.LocalUsers.SingleAsync(x => x.Id == actor);
            (await core.Workers.SingleAsync(x => x.Id == account.WorkerId)).Status = "terminated";
            await core.SaveChangesAsync();
        }
        public async Task DeactivatePrincipalAccount(Guid actor)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == actor)).IsActive = false;
            await core.SaveChangesAsync();
        }
        public async Task RevokePrincipalRequestEdit()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "employee")).PermissionsCsv = "procurement-access,procurement-request-read";
            await core.SaveChangesAsync();
        }
        public async Task<PurchaseRequest> PrincipalRequestStored(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString("D"), null, null, false, "principal-request-read-test");
            return await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().PurchaseRequests.AsNoTracking()
                .SingleAsync(x => x.Id == id);
        }
        public async Task<int> PrincipalRequestAuthorityEffects(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString("D"), null, null, false, "principal-request-effects-test");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.ApprovalSubmissions.CountAsync(x => x.PurchaseRequestId == id)
                + await db.BudgetCheckSnapshots.CountAsync(x => x.PurchaseRequestId == id && x.ReservationStatus == "Confirmed")
                + await db.FinanceInvoiceHandoffs.CountAsync();
        }
    }
}
