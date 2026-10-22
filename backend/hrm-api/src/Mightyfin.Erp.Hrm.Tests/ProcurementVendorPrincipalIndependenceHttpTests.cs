using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string VendorProofActions = "procurement-access,procurement-vendor-create,procurement-vendor-read,procurement-vendor-edit,procurement-vendor-manage,procurement-vendor-approve,procurement-vendor-suspend";

    [Theory]
    [InlineData("return")][InlineData("approve")][InlineData("reject")][InlineData("qualification")]
    public async Task Distinct_Core_account_sharing_original_supplier_worker_cannot_review_own_persons_proposal(string operation)
    {
        await using var f = await Fixture.Start();
        await f.ConfigureVendorProofPrincipals(workerless: false, alias: true);
        var draft = await NewPrincipalVendor(f);
        var id = draft.GetProperty("id").GetGuid(); var path = "/api/procurement/v1/vendors/" + id;
        var qualification = await SendJson(f.Client, HttpMethod.Post, path + "/qualifications", f.Owner,
            new { version = draft.GetProperty("version").GetInt64(), qualification = new { qualificationType = "Synthetic registration", referenceNumber = "SYNTHETIC-NOT-REAL" } });
        Assert.Equal(HttpStatusCode.Created, qualification.StatusCode);
        var q = await qualification.Content.ReadFromJsonAsync<JsonElement>();
        var submitted = await SubmitPrincipalVendor(f, id, q.GetProperty("version").GetInt64());
        var version = submitted.GetProperty("version").GetInt64();
        var detail = await Send(f.Client, HttpMethod.Get, path, f.Other);
        var flags = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(flags.GetProperty("canApprove").GetBoolean());
        Assert.False(flags.GetProperty("canReject").GetBoolean());
        Assert.False(flags.GetProperty("canReturnForInformation").GetBoolean());
        var before = await f.VendorPrincipalProofFootprint();
        var suffix = operation == "qualification" ? "/qualifications/" + q.GetProperty("id").GetGuid() + "/review" : "/" + operation;
        object input = operation == "qualification" ? new { version, decision = "Verified", reason = "Alias must not verify" }
            : operation == "approve" ? new { version, reason = "Alias must not approve", duplicatesReviewed = true }
            : new { version, reason = "Alias must not review" };
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(f.Client, HttpMethod.Post, path + suffix, f.Other, input)).StatusCode);
        Assert.Equal(before, await f.VendorPrincipalProofFootprint());
    }

    [Theory]
    [InlineData("suspend")][InlineData("reactivate")]
    public async Task Known_original_proposer_worker_alias_cannot_manage_supplier_hold(string operation)
    {
        await using var f = await Fixture.Start();
        await f.ConfigureVendorProofPrincipals(workerless: false, alias: true);
        var draft = await NewPrincipalVendor(f); var id = draft.GetProperty("id").GetGuid();
        var path = "/api/procurement/v1/vendors/" + id;
        var submitted = await SubmitPrincipalVendor(f, id, draft.GetProperty("version").GetInt64());
        var approvedResponse = await SendJson(f.Client, HttpMethod.Post, path + "/approve", f.Reviewer,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Independent reviewer", duplicatesReviewed = true });
        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);
        var current = await approvedResponse.Content.ReadFromJsonAsync<JsonElement>();
        if (operation == "reactivate")
        {
            var suspended = await SendJson(f.Client, HttpMethod.Post, path + "/suspend", f.Reviewer,
                new { version = current.GetProperty("version").GetInt64(), reason = "Independent synthetic hold", existingOrdersReviewed = true });
            Assert.Equal(HttpStatusCode.OK, suspended.StatusCode); current = await suspended.Content.ReadFromJsonAsync<JsonElement>();
        }
        var detail = await Send(f.Client, HttpMethod.Get, path, f.Other);
        Assert.False((await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(operation == "suspend" ? "canSuspend" : "canReactivate").GetBoolean());
        var before = await f.VendorPrincipalProofFootprint(); var version = current.GetProperty("version").GetInt64();
        object input = operation == "suspend" ? new { version, reason = "Known maker alias", existingOrdersReviewed = true }
            : new { version, reason = "Known maker alias", dueDiligenceReviewed = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(f.Client, HttpMethod.Post, path + "/" + operation, f.Other, input)).StatusCode);
        Assert.Equal(before, await f.VendorPrincipalProofFootprint());
    }

    [Fact]
    public async Task Distinct_workerless_Core_principals_can_return_revise_and_independently_approve_supplier()
    {
        await using var f = await Fixture.Start(); await f.ConfigureVendorProofPrincipals(workerless: true, alias: false);
        var draft = await NewPrincipalVendor(f); var id = draft.GetProperty("id").GetGuid(); var path = "/api/procurement/v1/vendors/" + id;
        var submitted = await SubmitPrincipalVendor(f, id, draft.GetProperty("version").GetInt64());
        var selfBefore = await f.VendorPrincipalProofFootprint();
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(f.Client, HttpMethod.Post, path + "/approve", f.Owner,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Self forbidden even without workers", duplicatesReviewed = true })).StatusCode);
        Assert.Equal(selfBefore, await f.VendorPrincipalProofFootprint());
        var returnedResponse = await SendJson(f.Client, HttpMethod.Post, path + "/return", f.Other,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Clarify display name" });
        Assert.Equal(HttpStatusCode.OK, returnedResponse.StatusCode); var returned = await returnedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var revisedResponse = await SendJson(f.Client, HttpMethod.Put, path, f.Owner,
            new { version = returned.GetProperty("version").GetInt64(), draft = PrincipalVendorDraft("Clarified supplier") });
        Assert.Equal(HttpStatusCode.OK, revisedResponse.StatusCode); var revised = await revisedResponse.Content.ReadFromJsonAsync<JsonElement>();
        submitted = await SubmitPrincipalVendor(f, id, revised.GetProperty("version").GetInt64());
        var detail = await Send(f.Client, HttpMethod.Get, path, f.Other);
        Assert.True((await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("canApprove").GetBoolean());
        var approved = await SendJson(f.Client, HttpMethod.Post, path + "/approve", f.Other,
            new { version = submitted.GetProperty("version").GetInt64(), reason = "Independent Core reviewer without employee", duplicatesReviewed = true });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await f.VerifyWorkerlessVendorResult(id);
    }

    private static object PrincipalVendorDraft(string name) => new { legalName = "Principal supplier", displayName = name,
        countryCode = "ZM", categoryCode = "GENERAL", primaryContact = new { name = "Contact", email = "contact@example.test" } };
    private static async Task<JsonElement> NewPrincipalVendor(Fixture f)
    {
        var created = await SendJson(f.Client, HttpMethod.Post, "/api/procurement/v1/vendors", f.Owner, PrincipalVendorDraft("Principal supplier"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); return await created.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> SubmitPrincipalVendor(Fixture f, Guid id, long version)
    {
        var response = await SendJson(f.Client, HttpMethod.Post, "/api/procurement/v1/vendors/" + id + "/submit", f.Owner,
            new { version, businessReason = "Explicit mapped principal supplier proof" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private sealed partial class Fixture
    {
        public async Task ConfigureVendorProofPrincipals(bool workerless, bool alias)
        {
            foreach (var actor in new[] { Owner, Other, Reviewer }) await ConfigureCoreActions(actor, VendorProofActions, "vendor_principal_proof");
            using var service = app.Services.CreateScope(); var core = service.ServiceProvider.GetRequiredService<HrmDbContext>();
            var maker = await core.LocalUsers.SingleAsync(x => x.Id == Owner); var reviewer = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            if (alias) reviewer.WorkerId = maker.WorkerId;
            if (workerless) { maker.WorkerId = null; reviewer.WorkerId = null; }
            await core.SaveChangesAsync();
        }
        public async Task<string> VendorPrincipalProofFootprint()
        {
            using var service = app.Services.CreateScope();
            service.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a", company, Owner.ToString("D"), null, null, false, "vendor-principal-proof");
            var db = service.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { suppliers = await db.Suppliers.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                qualifications = await db.SupplierQualifications.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                decisions = await db.SupplierDecisions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                audits = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), outbox = await db.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync() });
        }
        public async Task VerifyWorkerlessVendorResult(Guid id)
        {
            using var service = app.Services.CreateScope();
            service.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a", company, Owner.ToString("D"), null, null, false, "vendor-principal-proof");
            var db = service.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = await db.Suppliers.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Null(row.ProposedByWorkerId); Assert.Equal(Owner.ToString("D"), row.CreatedBy);
            Assert.Equal("Active", row.Status); Assert.True(row.IsOrderable); Assert.Equal(Other.ToString("D"), row.StatusDecidedBy);
            Assert.Equal(new[] { "Submitted", "More Information Required", "Submitted", "Active" },
                await db.SupplierDecisions.Where(x => x.SupplierId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.ToStatus).ToArrayAsync());
        }
    }
}
