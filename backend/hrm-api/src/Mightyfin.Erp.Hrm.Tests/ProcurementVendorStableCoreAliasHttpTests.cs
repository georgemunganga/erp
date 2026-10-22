using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    public static IEnumerable<object[]> SupplierKnownAliasWithoutActiveHrCases()
    {
        foreach (var operation in new[] { "return", "approve", "reject", "qualification", "suspend", "reactivate" })
        {
            foreach (var profile in new[] { "inactive", "archived", "other-company", "missing" })
                yield return [operation, profile, false];
            // New records must retain a genuine existing Core link even when
            // active HR projection was already unavailable at creation time.
            yield return [operation, "inactive", true];
        }
    }

    [Theory]
    [MemberData(nameof(SupplierKnownAliasWithoutActiveHrCases))]
    public async Task Known_Core_worker_alias_remains_a_supplier_decision_veto_without_active_HR_projection(
        string operation, string profile, bool unavailableBeforeCreation)
    {
        await using var f = await Fixture.Start();
        await f.ConfigureVendorProofPrincipals(workerless: false, alias: true);
        var originalWorker = await f.SupplierMakerCoreWorker();
        if (unavailableBeforeCreation) await f.MakeSupplierMakerHrProjectionUnavailable(profile);
        var draft = await NewPrincipalVendor(f);
        var id = draft.GetProperty("id").GetGuid();
        var path = "/api/procurement/v1/vendors/" + id;
        if (!unavailableBeforeCreation) await f.MakeSupplierMakerHrProjectionUnavailable(profile);
        await f.AssertSupplierCreationAlias(id, originalWorker);

        // Owning current Core permissions still allow context and basic CRUD.
        // The optional Core link must not be exposed as active employee proof.
        var contextResponse = await Send(f.Client, HttpMethod.Get, "/api/procurement/v1/context", f.Other);
        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
        var context = await contextResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(f.Other, context.GetProperty("principalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, context.GetProperty("workerId").ValueKind);
        Assert.False(context.GetProperty("adminViewOnly").GetBoolean());
        Assert.DoesNotContain("coreLinkedWorkerId", context.EnumerateObject().Select(x => x.Name));

        var qualificationResponse = await SendJson(f.Client, HttpMethod.Post, path + "/qualifications", f.Owner,
            new { version = draft.GetProperty("version").GetInt64(), qualification = new { qualificationType = "Synthetic alias evidence", referenceNumber = "SYNTHETIC-NOT-REAL" } });
        Assert.Equal(HttpStatusCode.Created, qualificationResponse.StatusCode);
        var qualification = await qualificationResponse.Content.ReadFromJsonAsync<JsonElement>();
        var current = await SubmitPrincipalVendor(f, id, qualification.GetProperty("version").GetInt64());
        if (operation is "suspend" or "reactivate")
        {
            var verified = await SendJson(f.Client, HttpMethod.Post, path + "/qualifications/" + qualification.GetProperty("id").GetGuid() + "/review", f.Reviewer,
                new { version = current.GetProperty("version").GetInt64(), decision = "Verified", reason = "Independent qualification verification" });
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            var verification = await verified.Content.ReadFromJsonAsync<JsonElement>();
            current = JsonSerializer.SerializeToElement(new { version = verification.GetProperty("supplierVersion").GetInt64() });
            var approved = await SendJson(f.Client, HttpMethod.Post, path + "/approve", f.Reviewer,
                new { version = current.GetProperty("version").GetInt64(), reason = "Independent actual Core reviewer", duplicatesReviewed = true });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            current = await approved.Content.ReadFromJsonAsync<JsonElement>();
            if (operation == "reactivate")
            {
                var suspended = await SendJson(f.Client, HttpMethod.Post, path + "/suspend", f.Reviewer,
                    new { version = current.GetProperty("version").GetInt64(), reason = "Independent synthetic hold", existingOrdersReviewed = true });
                Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
                current = await suspended.Content.ReadFromJsonAsync<JsonElement>();
            }
        }
        var detailResponse = await Send(f.Client, HttpMethod.Get, path, f.Other);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var flag in new[] { "canApprove", "canReject", "canReturnForInformation", "canSuspend", "canReactivate" })
            Assert.False(detail.GetProperty(flag).GetBoolean());
        var before = await f.VendorPrincipalProofFootprint();
        var version = current.GetProperty("version").GetInt64();
        var suffix = operation == "qualification" ? "/qualifications/" + qualification.GetProperty("id").GetGuid() + "/review" : "/" + operation;
        object input = operation switch
        {
            "qualification" => new { version, decision = "Verified", reason = "Known alias must not verify" },
            "approve" => new { version, reason = "Known alias must not approve", duplicatesReviewed = true },
            "suspend" => new { version, reason = "Known alias must not suspend", existingOrdersReviewed = true },
            "reactivate" => new { version, reason = "Known alias must not reactivate", dueDiligenceReviewed = true },
            _ => new { version, reason = "Known alias must not decide" },
        };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendJson(f.Client, HttpMethod.Post, path + suffix, f.Other, input)).StatusCode);
        Assert.Equal(before, await f.VendorPrincipalProofFootprint());
    }

    private sealed partial class Fixture
    {
        public async Task<Guid> SupplierMakerCoreWorker()
        {
            using var service = app.Services.CreateScope();
            return (await service.ServiceProvider.GetRequiredService<HrmDbContext>().LocalUsers.SingleAsync(x => x.Id == Owner)).WorkerId!.Value;
        }

        public async Task MakeSupplierMakerHrProjectionUnavailable(string profile)
        {
            using var service = app.Services.CreateScope();
            var core = service.ServiceProvider.GetRequiredService<HrmDbContext>();
            var maker = await core.LocalUsers.SingleAsync(x => x.Id == Owner);
            var alias = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            Assert.NotNull(maker.WorkerId); Assert.Equal(maker.WorkerId, alias.WorkerId);
            var worker = await core.Workers.SingleAsync(x => x.Id == maker.WorkerId);
            switch (profile)
            {
                case "inactive": worker.Status = "terminated"; break;
                case "archived": worker.IsArchived = true; break;
                case "other-company":
                    var foreignBranch = new WorkLocation { Code = "ALIAS-FOREIGN", Name = "Synthetic other-company employee home", LegalEntityId = OtherCompany };
                    core.WorkLocations.Add(foreignBranch); worker.LocationId = foreignBranch.Id; break;
                case "missing": core.Workers.Remove(worker); break;
                default: throw new ArgumentOutOfRangeException(nameof(profile));
            }
            await core.SaveChangesAsync();
            // Preserve the actual existing owning Core reference; do not invent
            // another worker, change action grants or copy a request header.
            Assert.True(maker.IsActive); Assert.True(alias.IsActive);
            Assert.Equal(maker.WorkerId, alias.WorkerId);
        }

        public async Task AssertSupplierCreationAlias(Guid id, Guid expectedWorker)
        {
            using var service = app.Services.CreateScope();
            service.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Owner.ToString("D"), null, null, false, "supplier-alias-provenance");
            var row = await service.ServiceProvider.GetRequiredService<ProcurementDbContext>().Suppliers.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(expectedWorker, row.ProposedByWorkerId);
            Assert.Equal(Owner.ToString("D"), row.CreatedBy);
        }
    }
}
