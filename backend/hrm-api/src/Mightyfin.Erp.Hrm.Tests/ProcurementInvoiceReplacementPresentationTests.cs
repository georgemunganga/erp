using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Presentation/profile contract tests, not persisted replacement authority.
/// The actual PostgreSQL source proof and original-key replay remain native UAT gates.</summary>
public sealed class ProcurementInvoiceReplacementPresentationTests
{
    private const string Actor = "11111111-1111-4111-8111-111111111111";
    private static readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid();
    private static ProcurementRequestScope Scope(Guid? branch = null, bool all = false)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("replacement-presentation", Company, Actor, all ? null : branch ?? Branch,
            null, false, "presentation", explicitAllBranches: all);
        scope.AssignPrincipal(Guid.Parse(Actor));return scope;
    }
    private static SupplierInvoice Invoice() => new() { TenantId = "replacement-presentation", LegalEntityId = Company,
        BranchRef = Branch, CreatedBy = Actor, SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", Version = 4,
        Status = "Received", MatchingState = "ComparisonBlocked", CurrentMappingVersionId = Guid.NewGuid(),
        LastMatchResultId = Guid.NewGuid() };
    private static ProcurementPrincipalPeople.Person Person(bool wide = true) => new(Actor, null,
        new HashSet<string> { "procurement-invoice-read", "procurement-invoice-capture" }, wide,
        wide ? new HashSet<Guid>() : new HashSet<Guid> { Branch });

    [Fact]
    public void Workerless_maker_with_current_grants_can_request_a_fresh_hint_but_All_cannot_write()
    {
        var invoice = Invoice();var http = new DefaultHttpContext();var actor = Person();
        Assert.Null(actor.WorkerId);
        Assert.True(ProcurementInvoiceIntakeRoutes.ReplacementMakerContext(invoice, Scope(), http.Request, actor));
        Assert.False(ProcurementInvoiceIntakeRoutes.ReplacementMakerContext(invoice, Scope(all: true), http.Request, actor));
        http.Request.Headers["X-Shell-Branch-Scope"] = "all";
        Assert.False(ProcurementInvoiceIntakeRoutes.ReplacementMakerContext(invoice, Scope(), http.Request, actor));
    }

    [Theory]
    [InlineData("read")][InlineData("capture")][InlineData("principal")][InlineData("maker")]
    [InlineData("branch")][InlineData("scope")][InlineData("tenant")][InlineData("unassigned")]
    public void Missing_action_or_record_scope_never_offers_replacement(string fault)
    {
        var invoice = Invoice();var scope = Scope();var actor = Person();
        if (fault == "read") actor = actor with { Permissions = new HashSet<string> { "procurement-invoice-capture" } };
        if (fault == "capture") actor = actor with { Permissions = new HashSet<string> { "procurement-invoice-read" } };
        if (fault == "principal") actor = actor with { PrincipalId = Guid.NewGuid().ToString("D") };
        if (fault == "maker") invoice.CreatedBy = Guid.NewGuid().ToString("D");
        if (fault == "branch") scope = Scope(Guid.NewGuid());
        if (fault == "scope") invoice.LegalEntityId = Guid.NewGuid();
        if (fault == "tenant") invoice.TenantId = "other-tenant";
        if (fault == "unassigned") actor = actor with { OrganisationWide = false, AllowedBranches = new HashSet<Guid>() };
        Assert.False(ProcurementInvoiceIntakeRoutes.ReplacementMakerContext(invoice, scope,
            new DefaultHttpContext().Request, actor));
    }

    [Fact]
    public void Current_read_only_actor_can_read_history_without_receiving_a_new_work_hint()
    {
        var actor = Person() with { Permissions = new HashSet<string> { "procurement-invoice-read" } };
        Assert.True(ProcurementInvoiceIntakeRoutes.ReplacementReadActor(Scope(all: true), actor));
        Assert.False(ProcurementInvoiceIntakeRoutes.ReplacementMakerContext(Invoice(), Scope(),
            new DefaultHttpContext().Request, actor));
    }

    private static InvoiceReplacementSourceWitness Witness(SupplierInvoice displayed, string? fault = null)
    {
        var source = new SupplierInvoice { Id = displayed.Id, TenantId = displayed.TenantId,
            LegalEntityId = displayed.LegalEntityId, SupplierId = displayed.SupplierId,
            BranchRef = displayed.BranchRef, CurrencyCode = displayed.CurrencyCode, CreatedBy = displayed.CreatedBy,
            Version = displayed.Version, Status = displayed.Status, MatchingState = displayed.MatchingState,
            CurrentMappingVersionId = displayed.CurrentMappingVersionId, LastMatchResultId = displayed.LastMatchResultId };
        if (fault == "id") source.Id = Guid.NewGuid();
        if (fault == "version") source.Version++;
        if (fault == "branch") source.BranchRef = Guid.NewGuid();
        if (fault == "supplier") source.SupplierId = Guid.NewGuid();
        if (fault == "currency") source.CurrencyCode = "USD";
        if (fault == "claim") source.ActiveMatchClaimResultId = Guid.NewGuid();
        if (fault == "finance") source.FinancePostingRef = "not-a-procurement-only-source";
        if (fault == "status") source.Status = "Cancelled";
        if (fault == "uncompared") source.MatchingState = "NotCompared";
        var mapping = new InvoiceMatchMappingVersion { Id = displayed.CurrentMappingVersionId!.Value,
            PurchaseOrderId = Guid.NewGuid(), IssuedVersionId = Guid.NewGuid(), SourceSchemaVersion = 3 };
        var result = new InvoiceMatchResult { Id = displayed.LastMatchResultId!.Value,
            PurchaseOrderId = fault == "order" ? Guid.NewGuid() : mapping.PurchaseOrderId,
            IssuedVersionId = mapping.IssuedVersionId, SourceSchemaVersion = fault == "legacy" ? null : 3,
            ExecutionVersion = 2, Outcome = fault == "outcome" ? "Compared" : "Blocked" };
        return new(source, mapping, result, new InvoiceUnitReviewSource(), new InvoiceUnitReviewOutcome(), new string('a', 64));
    }

    [Fact]
    public void Context_uses_verified_mapping_order_when_original_header_PO_is_null()
    {
        var displayed = Invoice();Assert.Null(displayed.PurchaseOrderId);
        var witness = Witness(displayed);
        var context = ProcurementInvoiceIntakeRoutes.ReplacementDisplayedContext(displayed, witness);
        Assert.NotNull(context);Assert.Equal(displayed.Id, context.SourceInvoiceId);
        Assert.Equal(displayed.Version, context.SourceVersion);
        Assert.Equal(witness.Mapping.PurchaseOrderId, context.PurchaseOrderId);
    }

    [Theory]
    [InlineData("id")][InlineData("version")][InlineData("branch")][InlineData("supplier")]
    [InlineData("currency")][InlineData("claim")][InlineData("finance")]
    [InlineData("order")][InlineData("legacy")][InlineData("outcome")][InlineData("status")][InlineData("uncompared")]
    public void Stale_display_or_unverified_source_shape_cannot_form_context(string fault)
    {
        var displayed = Invoice();
        Assert.Null(ProcurementInvoiceIntakeRoutes.ReplacementDisplayedContext(displayed, Witness(displayed, fault)));
    }

    private sealed class ReadOnlyProfile : IProcurementInvoiceReplacementSchemaReadiness
    {
        public int Reads, Writes;
        public Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct)
        { Reads++;return Task.FromResult(true); }
        public Task<bool> CanRecordAsync(ProcurementDbContext db, CancellationToken ct)
        { Writes++;return Task.FromResult(false); }
    }
    private sealed class DeniedAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(System.Security.Claims.ClaimsPrincipal user, object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(System.Security.Claims.ClaimsPrincipal user, object? resource,
            string policyName) => Task.FromResult(AuthorizationResult.Failed());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_invoice_detail_hint_exits_before_storage_when_provider_missing_or_writes_disabled(bool providerPresent)
    {
        var profile = new ReadOnlyProfile();
        var setup = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?> {
                ["Procurement:InvoiceReplacement:Enabled"] = providerPresent ? "false" : "true",
                ["Procurement:InvoiceCurrencyCalculation:ReadEnabled"] = "true",
                ["Procurement:InvoiceCurrencyCalculation:CaptureEnabled"] = "true" }).Build());
        if (providerPresent) setup.AddSingleton<IProcurementInvoiceReplacementSchemaReadiness>(profile);
        using var services = setup.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        Assert.Null(await ProcurementInvoiceIntakeRoutes.ReplacementPresentation(null!, Invoice(), Scope(), http, null!, default));
        Assert.Equal(0, profile.Reads);Assert.Equal(0, profile.Writes);
    }

    [Fact]
    public async Task Read_profile_remains_available_without_INSERT_and_new_work_flags()
    {
        var profile = new ReadOnlyProfile();
        using var services = new ServiceCollection().AddSingleton<IProcurementInvoiceReplacementSchemaReadiness>(profile)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Procurement:InvoiceReplacement:Enabled"] = "false",
                ["Procurement:InvoiceCurrencyCalculation:CaptureEnabled"] = "false" }).Build()).BuildServiceProvider();
        Assert.True(await ProcurementInvoiceIntakeRoutes.ReplacementReadSchemaReady(services, null!, default));
        Assert.Equal(1, profile.Reads);Assert.Equal(0, profile.Writes);
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementWriteSchemaReady(services, null!, default));
        Assert.Equal(1, profile.Writes);
    }

    [Fact]
    public async Task Missing_provider_never_queries_storage_or_advertises_read_capability()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementReadSchemaReady(services, null!, default));
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementWriteSchemaReady(services, null!, default));
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementReadCapability(null!, Scope(), http, false, default));
    }

    [Fact]
    public async Task Missing_current_read_permission_and_pending_schema_keep_metadata_flag_false_before_storage()
    {
        var profile = new ReadOnlyProfile();
        using var services = new ServiceCollection().AddSingleton<IProcurementInvoiceReplacementSchemaReadiness>(profile)
            .AddSingleton<IAuthorizationService, DeniedAuthorization>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementReadCapability(null!, Scope(), http, true, default));
        Assert.False(await ProcurementInvoiceIntakeRoutes.ReplacementReadCapability(null!, Scope(), http, false, default));
        Assert.Equal(0, profile.Reads);Assert.Equal(0, profile.Writes);
    }
}
