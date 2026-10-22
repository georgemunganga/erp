using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Tests;
// Actual mapped routes; synthetic auth and InMemory, not PostgreSQL isolation proof.
public sealed partial class ProcurementRequestHttpTests
{
    private const string RetirementPolicies = "/api/procurement/v1/configuration/policies";
    private static async Task<JsonElement> NewRetirementPolicy(Fixture fixture)
    {
        var response = await SendJson(fixture.Client, HttpMethod.Post, RetirementPolicies, fixture.Owner,
            new ProcurementPolicyDraftRoutes.PolicyDraft("PURCHASE_REQUEST", DateTimeOffset.UtcNow.AddDays(2), null, "{}"), "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> ReadRetirementPolicy(Fixture fixture, Guid id)
    {
        var response = await Send(fixture.Client, HttpMethod.Get, $"{RetirementPolicies}/{id}", fixture.Owner, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task Unused_policy_retirement_retains_original_rules_and_does_not_create_authority()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Owner, "procurement_manager");
        var original = await NewRetirementPolicy(fixture);
        var id = original.GetProperty("id").GetGuid(); var version = original.GetProperty("version").GetInt64();
        Assert.True((await ReadRetirementPolicy(fixture, id)).GetProperty("canRetireDraft").GetBoolean());
        var response = await SendJson(fixture.Client, HttpMethod.Post, $"{RetirementPolicies}/{id}/retire", fixture.Owner,
            new ProcurementPolicyDraftRoutes.DraftRetirement(version, "  Duplicate draft  "), "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await ReadRetirementPolicy(fixture, id);
        Assert.Equal("Retired", saved.GetProperty("state").GetString());
        Assert.Equal(version + 1, saved.GetProperty("version").GetInt64());
        Assert.Equal(original.GetProperty("rulesJson").GetString(), saved.GetProperty("rulesJson").GetString());
        Assert.Equal(original.GetProperty("effectiveFrom").GetString(), saved.GetProperty("effectiveFrom").GetString());
        Assert.False(saved.GetProperty("canRetireDraft").GetBoolean());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftRetired"));
        Assert.Equal("Duplicate draft", (await fixture.AuditPayload(id, "DraftRetired")).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Post, $"{RetirementPolicies}/{id}/retire", fixture.Owner,
            new ProcurementPolicyDraftRoutes.DraftRetirement(version + 1, "Again"), "procurement_manager")).StatusCode);
        var method = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/configuration/methods", fixture.Owner,
            new ProcurementConfigurationRoutes.MethodInput("RETIRED", "Unused", null, id), "procurement_manager");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, method.StatusCode);
        var unbound = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/configuration/methods", fixture.Owner,
            new ProcurementConfigurationRoutes.MethodInput("UNBOUND", "Unbound", null, null), "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, unbound.StatusCode);
        var methodRow = await unbound.Content.ReadFromJsonAsync<JsonElement>();
        var rejectedEdit = await SendJson(fixture.Client, HttpMethod.Put,
            $"/api/procurement/v1/configuration/methods/{methodRow.GetProperty("id").GetGuid()}", fixture.Owner,
            new ProcurementConfigurationRoutes.VersionedMethod(methodRow.GetProperty("version").GetInt64(),
                new ProcurementConfigurationRoutes.MethodInput("UNBOUND", "Unbound", null, id)), "procurement_manager");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedEdit.StatusCode);
        Assert.Equal(1, await fixture.RequestAuditCount(id, "DraftRetired"));
        var effective = await Send(fixture.Client, HttpMethod.Get, $"{RetirementPolicies}/effective?key=PURCHASE_REQUEST", fixture.Owner, role: "procurement_manager");
        Assert.Equal("MissingPolicy", (await effective.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
    [Theory]
    [InlineData("stale", HttpStatusCode.Conflict)] [InlineData("blank", HttpStatusCode.UnprocessableEntity)]
    [InlineData("long", HttpStatusCode.UnprocessableEntity)] [InlineData("foreign", HttpStatusCode.Forbidden)]
    [InlineData("read-only", HttpStatusCode.Forbidden)]
    public async Task Policy_retirement_denials_make_no_policy_audit_or_outbox_changes(string denial, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Owner, "procurement_manager");
        var policy = await NewRetirementPolicy(fixture); var id = policy.GetProperty("id").GetGuid();
        if (denial == "read-only")
            await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-configuration-read");
        var footprint = await fixture.PolicyRetirementFootprint(id);
        await fixture.RestrictCoreAccountToCompany(fixture.Owner);
        var response = await SendJson(fixture.Client, HttpMethod.Post, $"{RetirementPolicies}/{id}/retire", fixture.Owner,
            new ProcurementPolicyDraftRoutes.DraftRetirement(denial == "stale" ? 99 : policy.GetProperty("version").GetInt64(),
                denial == "blank" ? " " : denial == "long" ? new string('x', 1001) : "Unused"), "procurement_manager",
            denial == "foreign" ? fixture.OtherCompany : null,
            denial == "read-only" ? "procurement-access,procurement-configuration-read" : null);
        Assert.Equal(expected, response.StatusCode); Assert.Equal(footprint, await fixture.PolicyRetirementFootprint(id));
    }
    [Theory]
    [InlineData("PendingReview")] [InlineData("Published")] [InlineData("returned-metadata")]
    [InlineData("returned-audit-only")] [InlineData("publication-metadata")]
    [InlineData("method")] [InlineData("supplier")] [InlineData("supplier-decision")]
    [InlineData("sourcing")] [InlineData("evaluation")] [InlineData("invoice-match")]
    public async Task Any_retained_review_or_consumer_prevents_draft_retirement(string evidence)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Owner, "procurement_manager");
        var policy = await NewRetirementPolicy(fixture); var id = policy.GetProperty("id").GetGuid();
        await fixture.AddPolicyRetirementBlocker(id, evidence); var current = await ReadRetirementPolicy(fixture, id);
        Assert.False(current.GetProperty("canRetireDraft").GetBoolean()); var footprint = await fixture.PolicyRetirementFootprint(id);
        var response = await SendJson(fixture.Client, HttpMethod.Post, $"{RetirementPolicies}/{id}/retire", fixture.Owner,
            new ProcurementPolicyDraftRoutes.DraftRetirement(current.GetProperty("version").GetInt64(), "Unused"), "procurement_manager");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(footprint, await fixture.PolicyRetirementFootprint(id));
    }
    [Fact]
    public async Task Read_only_policy_hints_never_grant_retirement()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureCoreRole(fixture.Owner, "procurement_manager");
        var policy = await NewRetirementPolicy(fixture); var id = policy.GetProperty("id").GetGuid();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-configuration-read");
        foreach (var path in new[] { RetirementPolicies, $"{RetirementPolicies}/{id}" })
        {
            var response = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, actions: "procurement-access,procurement-configuration-read");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var row = path == RetirementPolicies ? body.GetProperty("items")[0] : body;
            Assert.Equal(1, row.GetProperty("draftRetirementVersion").GetInt32()); Assert.False(row.GetProperty("canRetireDraft").GetBoolean());
        }
    }
    private sealed partial class Fixture
    {
        private ProcurementDbContext RetirementDb(IServiceScope scope)
        {
            scope.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            return scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        }
        public async Task<string> PolicyRetirementFootprint(Guid id)
        {
            using var scope = app.Services.CreateScope(); var db = RetirementDb(scope);
            return JsonSerializer.Serialize(new { policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id),
                audits = await db.AuditEvents.CountAsync(), outbox = await db.OutboxEvents.CountAsync(), methods = await db.ProcurementMethods.CountAsync() });
        }
        public async Task AddPolicyRetirementBlocker(Guid id, string evidence)
        {
            using var scope = app.Services.CreateScope(); var db = RetirementDb(scope); var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
            switch (evidence)
            {
                case "PendingReview": case "Published": policy.State = evidence; break;
                case "returned-metadata": policy.ReviewRequestedBy = Owner.ToString(); break;
                case "publication-metadata": policy.PublicationReason = "Retained"; break;
                case "returned-audit-only": db.AddScoped(new AuditEvent { RecordType = "PolicyVersion", RecordId = id, Action = "ReturnedForChanges", ActorSubjectId = Owner.ToString(), ChangedFieldsJson = "{}" }); break;
                case "method": db.AddScoped(new ProcurementMethod { Code = "REFERENCE", Name = "Reference", PolicyVersionId = id, Status = "Retired" }); break;
                case "supplier": db.AddScoped(new Supplier { Number = "REF", LegalName = "Reference", DisplayName = "Reference", CountryCode = "ZM", StatusPolicyVersionId = id }); break;
                case "supplier-decision": db.AddScoped(new SupplierDecision { SupplierId = Guid.NewGuid(), FromStatus = "Draft", ToStatus = "Approved", ActorSubjectId = Owner.ToString(), Reason = "Retained", PolicyVersionId = id }); break;
                case "sourcing": db.AddScoped(new SourcingEvent { Number = "REF", Title = "Retained", Method = "RFQ", CurrencyCode = "ZMW", OwnerSubjectId = Owner.ToString(), PolicyVersionId = id, Status = "Cancelled" }); break;
                case "evaluation": db.AddScoped(new PolicyEvaluation { PurchaseRequestId = Guid.NewGuid(), RequestVersion = 1, PolicyVersionId = id, PolicyVersionNumber = 1, AmountBasis = "EstimatedLineBase", DecisionJson = "{}", Outcome = "Blocked" }); break;
                case "invoice-match": db.AddScoped(new InvoiceMatchResult { SupplierInvoiceId = Guid.NewGuid(), MatchType = "TwoWay", Outcome = "Blocked", PolicyVersionId = id }); break;
            }
            await db.SaveChangesAsync();
        }
    }
}
