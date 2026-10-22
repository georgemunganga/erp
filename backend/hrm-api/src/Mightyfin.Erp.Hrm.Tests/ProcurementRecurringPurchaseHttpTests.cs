using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped HTTP/in-memory evidence; PostgreSQL constraints and migration are separate P24 gates.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    private const string RecurringPath = "/api/procurement/v1/recurring-purchases";

    [Fact]
    public async Task Recurring_purchase_create_update_close_is_owned_exact_and_never_posts_payable()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors();
        var supplier = await fixture.AddInvoiceSupplier();
        object draft(string amount, string title = "Monthly service") => new
        {
            supplierId = supplier, supplierContractId = (Guid?)null, title, frequency = "Monthly",
            firstCycleOn = "2026-11-01", lastCycleOn = "2027-10-01", nextCycleOn = "2026-11-01",
            expectedAmount = amount, currencyCode = "ZMW"
        };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            draft("12345678901234.5678"), "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal("12345678901234.5678", created.GetProperty("exactExpectedAmount").GetString());
        Assert.NotEqual(Guid.Empty, created.GetProperty("ownerWorkerId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Other,
                role: "procurement_buyer")).StatusCode);
        var ownList = await Send(fixture.Client, HttpMethod.Get, RecurringPath, fixture.Owner, role: "procurement_buyer");
        Assert.Equal(1, (await ownList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        await fixture.SetRecurringEditPermission(false);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
                new { version = 1, draft = draft("4") }, "employee")).StatusCode);
        await fixture.SetRecurringEditPermission(true);
        var savedResponse = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1, draft = draft("0.0001", "Revised monthly service") }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("0.0001", saved.GetProperty("exactExpectedAmount").GetString());
        Assert.Equal(2, saved.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
                new { version = 1, reason = "Stale" }, "procurement_buyer")).StatusCode);
        var closeResponse = await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
            new { version = 2, reason = "Contract ended" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
                new { version = 3, draft = draft("5") }, "procurement_buyer")).StatusCode);
        var footprint = await fixture.RecurringFootprint(id);
        Assert.Equal(1, footprint.Records);
        Assert.Equal(0, footprint.Invoices);
        Assert.Equal(0, footprint.Handoffs);
        Assert.True(footprint.Audit >= 3);
    }

    [Fact]
    public async Task Recurring_purchase_unchanged_put_consumes_one_version_and_rejects_replay_without_audit()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors();
        var supplier = await fixture.AddInvoiceSupplier();
        var draft = new { supplierId = supplier, supplierContractId = (Guid?)null,
            title = "Unchanged monthly service", frequency = "Monthly", firstCycleOn = "2026-11-01",
            lastCycleOn = (string?)null, nextCycleOn = "2026-11-01", expectedAmount = "10.0001", currencyCode = "ZMW" };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath,
            fixture.Owner, draft, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var path = $"{RecurringPath}/{id}";
        Assert.Equal(1, created.GetProperty("version").GetInt64());

        var savedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, saved.GetProperty("version").GetInt64());
        var readResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "procurement_buyer");
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, read.GetProperty("version").GetInt64());
        Assert.Equal("Unchanged monthly service", read.GetProperty("title").GetString());
        Assert.Equal("10.0001", read.GetProperty("exactExpectedAmount").GetString());
        var updates = await fixture.RecurringUpdateAudits(id);
        using var updateDocument = JsonDocument.Parse(Assert.Single(updates));
        var update = updateDocument.RootElement;
        Assert.Equal(1, update.GetProperty("before").GetProperty("Version").GetInt64());
        Assert.Equal(2, update.GetProperty("after").GetProperty("Version").GetInt64());
        var footprint = await fixture.RecurringFootprint(id);

        var replay = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(footprint, await fixture.RecurringFootprint(id));
        Assert.Equal(updates, await fixture.RecurringUpdateAudits(id));
        var afterResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "procurement_buyer");
        var after = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, after.GetProperty("version").GetInt64());
        Assert.Equal("Unchanged monthly service", after.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    public void Recurring_purchase_rejects_nonpositive_or_imprecise_amounts(string amount)
    {
        var input = new RecurringPurchaseCommitmentInput(Guid.NewGuid(), null, "Monthly service", "Monthly",
            new DateOnly(2026, 11, 1), null, new DateOnly(2026, 11, 1), "ZMW", decimal.Parse(amount,
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(input));
    }

    [Fact]
    public void Recurring_purchase_dates_and_frequency_are_explicit()
    {
        var valid = new RecurringPurchaseCommitmentInput(Guid.NewGuid(), null, "Annual licence", "Yearly",
            new DateOnly(2026, 11, 1), new DateOnly(2028, 11, 1), new DateOnly(2027, 11, 1), "ZMW", 100m);
        Assert.Null(RecurringPurchaseCommitmentRules.Validate(valid));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { Frequency = "EveryDay" }));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { NextCycleOn = new DateOnly(2029, 1, 1) }));
        Assert.NotNull(RecurringPurchaseCommitmentRules.Validate(valid with { LastCycleOn = new DateOnly(2026, 1, 1) }));
    }

    [Fact]
    public async Task Recurring_purchase_missing_draft_is_validation_error_without_mutation()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors(); var supplier = await fixture.AddInvoiceSupplier();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Weekly service", frequency = "Weekly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "10.0001", currencyCode = "ZMW" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var before = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = before.GetProperty("id").GetGuid(); var footprint = await fixture.RecurringFootprint(id);
        var missing = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1 }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        var body = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Equal(footprint, await fixture.RecurringFootprint(id));
        var current = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer");
        Assert.Equal(1, (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Recurring_purchase_selected_branch_company_and_inactive_Core_actor_fail_closed()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors(); var supplier = await fixture.AddInvoiceSupplier();
        var originalBranch = await fixture.SourcingBranch();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Monthly service", frequency = "Monthly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "10.0001", currencyCode = "ZMW" },
            "procurement_buyer", selectedLocation: originalBranch);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        var makerWorker = original.GetProperty("ownerWorkerId").GetGuid();
        Assert.Equal(originalBranch, original.GetProperty("workLocationId").GetGuid());
        var otherBranch = await fixture.AddRecurringBranch();
        var branchRead = await fixture.RecurringGetAtBranch(id, otherBranch);
        Assert.Equal(HttpStatusCode.NotFound, branchRead.StatusCode);
        var branchEdit = await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{id}", fixture.Owner,
            new { version = 1, draft = new { supplierId = supplier, title = "Cross branch", frequency = "Monthly",
                firstCycleOn = "2026-11-01", nextCycleOn = "2026-11-01", expectedAmount = "11", currencyCode = "ZMW" } },
            "procurement_buyer", selectedLocation: otherBranch);
        Assert.Equal(HttpStatusCode.NotFound, branchEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner,
                company: fixture.OtherCompany, role: "procurement_buyer")).StatusCode);
        await fixture.ConfineRecurringOwner(otherBranch);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner,
                role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.RecurringGetAtBranch(id, originalBranch)).StatusCode);
        await fixture.DeactivatePrincipalAccount(fixture.Owner);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer")).StatusCode);
        var stored = await fixture.RecurringStored(id);
        Assert.Equal(makerWorker, stored.OwnerWorkerId);
        Assert.Equal(originalBranch, stored.WorkLocationId);
        Assert.Equal("Draft", stored.Status);
    }

    [Fact]
    public async Task Recurring_purchase_closed_history_remains_readable_and_currently_scoped()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors(); var supplier = await fixture.AddInvoiceSupplier();
        var created = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            new { supplierId = supplier, title = "Annual service", frequency = "Yearly", firstCycleOn = "2026-11-01",
                nextCycleOn = "2026-11-01", expectedAmount = "50", currencyCode = "ZMW" }, "procurement_buyer");
        var original = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = original.GetProperty("id").GetGuid();
        var closed = await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{id}/close", fixture.Owner,
            new { version = 1, reason = "Supplier arrangement ended" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var read = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{id}", fixture.Owner, role: "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var history = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", history.GetProperty("status").GetString());
        Assert.Equal("Supplier arrangement ended", history.GetProperty("closedReason").GetString());
        Assert.Equal(2, history.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Recurring_contract_reference_rejects_supplier_reparenting_even_after_plan_closure()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors();
        var supplierA = await fixture.AddInvoiceSupplier();
        var supplierB = await fixture.AddInvoiceSupplier();
        var contractDraft = new ContractDraftInput(supplierA, "Annual supplies", "Framework", "ZMW",
            1000m, new DateOnly(2026, 11, 1), new DateOnly(2027, 10, 31), null);
        const string contracts = "/api/procurement/v1/contracts";
        var createdContract = await SendJson(fixture.Client, HttpMethod.Post, contracts, fixture.Owner,
            contractDraft, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdContract.StatusCode);
        var contract = await createdContract.Content.ReadFromJsonAsync<JsonElement>();
        var contractId = contract.GetProperty("id").GetGuid();
        var contractPath = $"{contracts}/{contractId}";
        object plan(Guid supplier, string amount = "10.0001") => new
        {
            supplierId = supplier, supplierContractId = contractId, title = "Monthly supplies", frequency = "Monthly",
            firstCycleOn = "2026-11-01", nextCycleOn = "2026-11-01", expectedAmount = amount, currencyCode = "ZMW"
        };
        var untouched = await fixture.RecurringAreaFootprint();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
                plan(supplierB), "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Other,
                plan(supplierA), "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
                plan(supplierA), "procurement_buyer", company: fixture.OtherCompany)).StatusCode);
        var rejectedPrecision = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            plan(supplierA, "0.00001"), "procurement_buyer");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedPrecision.StatusCode);
        var numericBody = JsonSerializer.Serialize(plan(supplierA))
            .Replace("\"expectedAmount\":\"10.0001\"", "\"expectedAmount\":0.00001", StringComparison.Ordinal);
        Assert.Contains("\"expectedAmount\":0.00001", numericBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await RawOriginalValue(fixture, RecurringPath, numericBody, "procurement_buyer")).StatusCode);
        Assert.Equal(untouched, await fixture.RecurringAreaFootprint());

        var createdPlan = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner,
            plan(supplierA), "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdPlan.StatusCode);
        var recurring = await createdPlan.Content.ReadFromJsonAsync<JsonElement>();
        var recurringId = recurring.GetProperty("id").GetGuid();
        var footprint = await fixture.RecurringFootprint(recurringId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendJson(fixture.Client, HttpMethod.Put, $"{RecurringPath}/{recurringId}", fixture.Owner,
                new { version = 1, draft = plan(supplierB) }, "procurement_buyer")).StatusCode);
        Assert.Equal(footprint, await fixture.RecurringFootprint(recurringId));
        var reparent = new ProcurementContractDraftRoutes.VersionedContractDraft(
            contract.GetProperty("version").GetInt64(), contractDraft with { SupplierId = supplierB });
        var blocked = await SendJson(fixture.Client, HttpMethod.Put, contractPath, fixture.Owner,
            reparent, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("contract-referenced", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, await fixture.RequestAuditCount(contractId, "DraftUpdated"));
        Assert.Equal(footprint, await fixture.RecurringFootprint(recurringId));
        Assert.Equal(supplierA, (await (await Send(fixture.Client, HttpMethod.Get, contractPath, fixture.Owner,
            role: "procurement_buyer")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplierId").GetGuid());

        var closed = await SendJson(fixture.Client, HttpMethod.Post, $"{RecurringPath}/{recurringId}/close",
            fixture.Owner, new { version = 1, reason = "Schedule ended" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        blocked = await SendJson(fixture.Client, HttpMethod.Put, contractPath, fixture.Owner,
            reparent, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(0, await fixture.RequestAuditCount(contractId, "DraftUpdated"));
        var savedPlan = await Send(fixture.Client, HttpMethod.Get, $"{RecurringPath}/{recurringId}",
            fixture.Owner, role: "procurement_buyer");
        var retained = await savedPlan.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(supplierA, retained.GetProperty("supplierId").GetGuid());
        Assert.Equal(contractId, retained.GetProperty("supplierContractId").GetGuid());
        Assert.Equal("Closed", retained.GetProperty("status").GetString());
        var safeEdit = await SendJson(fixture.Client, HttpMethod.Put, contractPath, fixture.Owner,
            new ProcurementContractDraftRoutes.VersionedContractDraft(contract.GetProperty("version").GetInt64(),
                contractDraft with { Title = "Annual supplies revised" }), "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, safeEdit.StatusCode);
        Assert.Equal(supplierA, (await safeEdit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplierId").GetGuid());
    }

    private sealed partial class Fixture
    {
        private const string RecurringActions = "procurement-access,procurement-recurring-read,procurement-recurring-create,procurement-recurring-edit,procurement-contract-read,procurement-contract-create,procurement-contract-edit";
        public async Task ConfigureRecurringActors(bool withoutEmployee = false)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            foreach (var account in await core.LocalUsers.Where(x => x.Id == Owner || x.Id == Other).ToArrayAsync())
            {
                account.RolesCsv = "procurement_buyer";
                if (withoutEmployee) account.WorkerId = null;
            }
            var role = await core.TenantRoleAssignments.SingleOrDefaultAsync(x => x.RoleKey == "procurement_buyer");
            if (role is null)
            {
                role = new TenantRoleAssignment { RoleKey = "procurement_buyer", RoleName = "Synthetic recurring planner", Active = true };
                core.TenantRoleAssignments.Add(role);
            }
            role.Active = true; role.PermissionsCsv = RecurringActions;
            var branch = await core.WorkLocations.Where(x => x.LegalEntityId == company).Select(x => x.Id).SingleAsync();
            foreach (var actor in new[] { Owner, Other })
                if (!await core.UserBranchAssignments.AnyAsync(x => x.UserId == actor && x.LocationId == branch && !x.IsArchived))
                    core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = actor, LocationId = branch });
            await core.SaveChangesAsync();
        }
        public async Task SetRecurringEditPermission(bool allowed)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_buyer")).PermissionsCsv = allowed
                ? RecurringActions : RecurringActions.Replace(",procurement-recurring-edit", "", StringComparison.Ordinal);
            await core.SaveChangesAsync();
        }

        public async Task<(int Plans, int Audits, int Outbox)> RecurringAreaFootprint()
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-area-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.RecurringPurchaseCommitments.CountAsync(), await db.AuditEvents.CountAsync(),
                await db.OutboxEvents.CountAsync());
        }
        public async Task<Guid> AddRecurringBranch()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            var branch = new WorkLocation { Code = "RPC-OTHER", Name = "Other recurring branch", LegalEntityId = company };
            core.WorkLocations.Add(branch);
            core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = Owner, LocationId = branch.Id });
            await core.SaveChangesAsync(); return branch.Id;
        }
        public Task<HttpResponseMessage> RecurringGetAtBranch(Guid id, Guid branch)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{RecurringPath}/{id}");
            request.Headers.Add("X-Test-User", Owner.ToString());
            request.Headers.Add("X-Test-Role", "procurement_buyer");
            request.Headers.Add("X-Shell-Location", branch.ToString());
            return Client.SendAsync(request);
        }
        public async Task ConfineRecurringOwner(Guid branch)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            foreach (var assignment in await core.UserBranchAssignments.Where(x => x.UserId == Owner && !x.IsArchived && x.LocationId != branch).ToArrayAsync())
                assignment.IsArchived = true;
            if (!await core.UserBranchAssignments.AnyAsync(x => x.UserId == Owner && x.LocationId == branch && !x.IsArchived))
                core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = Owner, LocationId = branch });
            await core.SaveChangesAsync();
        }
        public async Task<(Guid? OwnerWorkerId, Guid? WorkLocationId, string Status)> RecurringStored(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-stored-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = await db.RecurringPurchaseCommitments.AsNoTracking().SingleAsync(x => x.Id == id);
            return (row.OwnerWorkerId, row.WorkLocationId, row.Status);
        }
        public async Task<string[]> RecurringUpdateAudits(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-update-audit-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return await db.AuditEvents.AsNoTracking().Where(x => x.RecordId == id && x.Action == "PlanUpdated")
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.ChangedFieldsJson).ToArrayAsync();
        }
        public async Task<(int Records, int Invoices, int Handoffs, int Audit)> RecurringFootprint(Guid id)
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Api.ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString(), null, null, false, "recurring-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.RecurringPurchaseCommitments.CountAsync(x => x.Id == id),
                await db.SupplierInvoices.CountAsync(), await db.FinanceInvoiceHandoffs.CountAsync(),
                await db.AuditEvents.CountAsync(x => x.RecordId == id));
        }
    }
}
