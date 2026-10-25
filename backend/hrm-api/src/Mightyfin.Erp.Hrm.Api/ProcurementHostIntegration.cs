using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Composes Procurement into the existing ERP process and authentication pipeline.</summary>
public static class ProcurementHostIntegration
{
    // Released draft-only callers do not opt into governed execution merely by composing services.
    // The production entrypoint passes its explicit IConfiguration to the overload below.
    public static void AddServices(IServiceCollection services, string connection)
        => AddServices(services, connection, new ConfigurationBuilder().Build());

    public static void AddServices(IServiceCollection services, string connection, IConfiguration configuration)
    {
        services.AddScoped<ProcurementRequestScope>();
        services.AddSingleton<IProcurementSpendingAuthorityIdentitySchemaReadiness, ProcurementSpendingAuthorityIdentitySchemaReadiness>();
        services.AddSingleton<IProcurementOperationalPolicyIdentitySchemaReadiness, ProcurementOperationalPolicyIdentitySchemaReadiness>();
        // Default-off, with separate Record policy and Execution review contracts.
        // Captured physical input alone cannot bypass either finite privilege contract.
        services.AddScoped<IProcurementContractReviewSchemaReadiness, ProcurementContractReviewStoreReadiness>();
        services.AddSingleton<IProcurementSourcingPolicyIdentitySchemaReadiness, ProcurementSourcingPolicyIdentitySchemaReadiness>();
        var executionConnection = configuration.GetConnectionString("ProcurementApprovalExecution");
        var contractReviewOptions = configuration.GetSection("Procurement:ContractReview")
            .Get<ProcurementContractReviewOptions>() ?? new ProcurementContractReviewOptions();
        contractReviewOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(contractReviewOptions);
        var approvalOptions = configuration.GetSection("Procurement:Approvals").Get<ProcurementApprovalExecutionOptions>()
            ?? new ProcurementApprovalExecutionOptions();
        approvalOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(approvalOptions);
        // The concrete P25 inspector is also available to established worker
        // workflows on a P25 database. The principal interface remains off
        // unless the explicit successor option is enabled.
        services.AddScoped<ProcurementPrincipalApprovalStoreReadiness>();
        // The successor remains default-off. When explicitly selected, its real
        // scoped provider still verifies P25, exact schema and finite SQL roles
        // for every read/record/execute decision; a setting alone grants nothing.
        if (approvalOptions.PrincipalExecutionEnabled)
            services.AddScoped<IProcurementPrincipalApprovalSchemaReadiness>(sp =>
                sp.GetRequiredService<ProcurementPrincipalApprovalStoreReadiness>());
        else
            services.AddSingleton<IProcurementPrincipalApprovalSchemaReadiness,UnavailableProcurementPrincipalApprovalSchemaReadiness>();
        var orderOptions = configuration.GetSection("Procurement:Orders").Get<ProcurementOrderExecutionOptions>()
            ?? new ProcurementOrderExecutionOptions();
        orderOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(orderOptions);
        var principalOrderOptions = configuration.GetSection("Procurement:PrincipalDirectOrderDrafts")
            .Get<ProcurementPrincipalOrderDraftOptions>() ?? new ProcurementPrincipalOrderDraftOptions();
        principalOrderOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(principalOrderOptions);
        // The actual provider stays closed until the owning migration, captured constraints and finite privileges match.
        services.AddScoped<IProcurementPrincipalOrderDraftReadiness, ProcurementPrincipalOrderDraftReadiness>();
        var revisionOptions = configuration.GetSection("Procurement:PrincipalOrderDraftRevisions")
            .Get<PrincipalOrderRevisionOptions>() ?? new PrincipalOrderRevisionOptions();
        services.AddSingleton(revisionOptions);
        // The owning-context provider stays false until the genuine capture and
        // finite privileges match. A setting alone never admits revisions.
        services.AddScoped<IPrincipalOrderRevisionReadiness, ProcurementPrincipalOrderRevisionReadiness>();
        var sourcingOptions = configuration.GetSection("Procurement:SourcingDrafts").Get<ProcurementSourcingDraftExecutionOptions>() ?? new();
        sourcingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(sourcingOptions);
        services.AddScoped<IProcurementSourcingDraftSchemaReadiness, ProcurementSourcingDraftSchemaReadiness>();
        services.AddSingleton(configuration.GetSection("Procurement:SourcingPublication")
            .Get<ProcurementPrincipalSourcingPublicationOptions>() ?? new());
        services.AddScoped<IProcurementPrincipalSourcingPublicationReadiness,
            ProcurementPrincipalSourcingPublicationReadiness>();
        // P32 stays default-off. Its owning-transaction provider requires the
        // exact captured schema, finite role, Core document and case contract;
        // configuration alone cannot admit a commercial round.
        services.AddSingleton(configuration.GetSection("Procurement:RfqCommercialRounds")
            .Get<ProcurementRfqCommercialOptions>() ?? new());
        services.AddScoped<IProcurementRfqCommercialReadiness,
            ProcurementRfqCommercialStoreReadiness>();
        // P36 remains default-off and physically unavailable until its own
        // exact PostgreSQL catalog capture and finite privileges are reviewed.
        services.AddScoped<IProcurementRfqRecommendationDraftReadiness,
            ProcurementRfqRecommendationStoreReadiness>();
        services.AddScoped<IProcurementOrderSchemaReadiness, ProcurementOrderSchemaReadiness>();
        services.AddScoped<IProcurementApprovalSchemaReadiness, ProcurementApprovalSchemaReadiness>();
        services.AddSingleton<IProcurementFinanceCheckerSchemaReadiness, ProcurementFinanceCheckerSchemaReadiness>();
        services.AddScoped(sp => new Mightyfin.Erp.Procurement.Infrastructure.Approvals.ProcurementApprovalTransaction(
            // The normal host connection may be composed while disabled, but every execution
            // route refuses to run until the explicit least-privilege connection is configured.
            string.IsNullOrWhiteSpace(executionConnection) ? connection : executionConnection, sp.GetRequiredService<IProcurementScope>(), sp.GetRequiredService<IApprovalScope>()));
        var receivingOptions = configuration.GetSection("Procurement:Receiving").Get<ProcurementReceivingExecutionOptions>() ?? new();
        receivingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(receivingOptions);
        services.AddScoped<IProcurementReceivingSchemaReadiness, ProcurementReceivingSchemaReadiness>();
        var principalReceivingOptions = configuration.GetSection("Procurement:PrincipalReceiving")
            .Get<ProcurementPrincipalReceivingOptions>() ?? new();
        principalReceivingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(principalReceivingOptions);
        services.AddScoped<IProcurementPrincipalReceivingReadiness, ProcurementPrincipalReceivingReadiness>();
        // P37 remains opt-in. The concrete provider inspects its own captured
        // schema and finite grants on the configured execution connection.
        ProcurementPrincipalGoodsReturnComposition.AddFoundation(services, configuration);
        var matchingOptions = configuration.GetSection("Procurement:InvoiceMatching").Get<ProcurementInvoiceMatchingExecutionOptions>() ?? new();
        matchingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(matchingOptions);
        services.AddScoped<IProcurementInvoiceMatchingSchemaReadiness, ProcurementInvoiceMatchingSchemaReadiness>();
        var principalInvoiceOptions = configuration.GetSection("Procurement:PrincipalInvoiceMatching")
            .Get<ProcurementPrincipalInvoiceMatchingOptions>() ?? new();
        services.AddSingleton(principalInvoiceOptions);
        var principalInvoiceSourceOptions = configuration.GetSection("Procurement:PrincipalInvoiceSources")
            .Get<ProcurementPrincipalInvoiceSourceOptions>() ?? new();
        services.AddSingleton(principalInvoiceSourceOptions);
        services.AddScoped<IProcurementPrincipalInvoiceSchemaReadiness, ProcurementPrincipalInvoiceStoreReadiness>();
        var unitReviewOptions = configuration.GetSection("Procurement:InvoiceUnitReview").Get<ProcurementInvoiceUnitReviewOptions>() ?? new();
        unitReviewOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(unitReviewOptions);
        var paymentOptions = configuration.GetSection("Procurement:ManualPayments").Get<ProcurementManualPaymentOptions>() ?? new();
        paymentOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(paymentOptions);
        services.AddScoped<IProcurementManualPaymentGovernanceReadiness>(sp => new ProcurementManualPaymentGovernanceReadiness(
            executionConnection ?? "", sp.GetRequiredService<ProcurementRequestScope>(),
            sp.GetRequiredService<ILogger<ProcurementManualPaymentGovernanceReadiness>>()));
        services.AddSingleton<IProcurementInvoiceUnitReviewSchemaReadiness, ProcurementInvoiceUnitReviewSchemaReadiness>();
        services.AddScoped<IInvoiceHistoricalExposureOwner, UnavailableInvoiceHistoricalExposureOwner>();
        services.AddScoped<IInvoiceMappingEvidenceReviewer, UnavailableInvoiceMappingEvidenceReviewer>();
        ProcurementInvoiceDocumentRoutes.AddServices(services, configuration);
        services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        services.AddDbContext<ProcurementDbContext>(options =>
            options.UseNpgsql(connection, provider =>
                provider.MigrationsHistoryTable("__procurement_migrations", "procurement")));
        // The Core approval model shares the ERP database but owns a separate
        // schema and migration history. Its workflow endpoints stay disabled
        // until Procurement's Finance and policy guards are connected.
        services.AddScoped<IApprovalScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        services.AddDbContext<ApprovalDbContext>(options =>
            options.UseNpgsql(connection, provider =>
                provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval")));
        services.AddHealthChecks().AddNpgSql(connection, name: "procurement-db");
    }

    public static void MapRoutes(WebApplication app)
    {
        ProcurementContextRoutes.MapRoutes(app);
        ProcurementVendorRoutes.MapRoutes(app);
        ProcurementItemEndpoints.MapRoutes(app);
        ProcurementRequestRoutes.Map(app);
        ProcurementBudgetRoutes.Map(app);
        ProcurementPolicyEvaluationRoutes.Map(app);
        ProcurementApprovalReadinessRoutes.Map(app);
        ProcurementApprovalExecutionRoutes.MapRoutes(app);
        ProcurementPlanRoutes.MapRoutes(app);
        ProcurementConfigurationRoutes.MapRoutes(app);
        ProcurementSpendingAuthorityRoutes.MapRoutes(app);
        ProcurementPolicyDraftRoutes.MapRoutes(app);
        ProcurementContractDraftRoutes.MapRoutes(app);
        MapContractReviewRoutes(app);
        ProcurementRecurringPurchaseRoutes.MapRoutes(app);
        ProcurementManualPaymentRoutes.MapReadRoutes(app);
        ProcurementManualPaymentCommandRoutes.MapRoutes(app);
        ProcurementSourcingDraftRoutes.MapRoutes(app);
        ProcurementSourcingExecutionRoutes.MapRoutes(app);
        ProcurementInvoiceIntakeRoutes.MapRoutes(app);
        ProcurementInvoiceIntakeRoutes.MapCreditRoutes(app);
        ProcurementInvoiceMatchingRoutes.MapRoutes(app);
        ProcurementInvoiceUnitReviewRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceSourceRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceMappingRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceReviewRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceComparisonRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceDiscrepancyRoutes.MapRoutes(app);
        ProcurementPrincipalInvoiceStatusRoutes.MapRoutes(app);
        if (app.Services.GetRequiredService<CoreDocumentFeature>().Enabled) ProcurementInvoiceDocumentRoutes.MapRoutes(app);
        ProcurementOrderDraftRoutes.MapRoutes(app);
        ProcurementPrincipalOrderDraftRoutes.MapRoutes(app);
        ProcurementOrderExecutionRoutes.MapRoutes(app);
        ProcurementReceivingRoutes.MapRoutes(app);
        ProcurementPrincipalReceivingRoutes.MapRoutes(app);
        ProcurementPrincipalReceivingRoutes.MapReturnRoutes(app);
        MapMetadataRoute(app);
    }

    // One composition seam, shared by complete-host mapping and negative HTTP
    // admission tests; the default-off condition is not reimplemented in fixtures.
    internal static void MapContractReviewRoutes(WebApplication app)
    {
        if (app.Services.GetService<ProcurementContractReviewOptions>() is { Enabled: true })
            ProcurementContractReviewRoutes.MapRoutes(app);
    }

    /// <summary>Maps module availability using the same checks as the complete host.</summary>
    public static void MapMetadataRoute(WebApplication app)
    {
        app.MapGet("/api/procurement/v1/meta", async (ProcurementRequestScope scope,
            ProcurementDbContext db, IServiceProvider services, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct))
                    return Results.Problem(statusCode: 503, title: "Procurement database unavailable.");
                var performance = services.GetService<IConfiguration>();
                var pending = ProcurementModelParityDiagnostics.HasPendingModelChanges(db, performance, "meta.model-parity")
                    || await ProcurementModelParityDiagnostics.MeasureAsync(performance, "meta.applied-migrations",
                        async () => (await db.Database.GetPendingMigrationsAsync(ct)).Any());
                var financeCheckerReads = !pending && await ProcurementModelParityDiagnostics.MeasureAsync(
                    performance, "meta.finance-read", () => ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, services, false, ct));
                var financeCheckerCapture = financeCheckerReads && await ProcurementModelParityDiagnostics.MeasureAsync(
                    performance, "meta.finance-capture", () => ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, services, true, ct));
                var receivingOptions = services.GetRequiredService<ProcurementReceivingExecutionOptions>();
                var receivingDrafts = !pending && receivingOptions.DraftsEnabled && receivingOptions.ExecutionDatabaseConfigured
                    && await services.GetRequiredService<IProcurementReceivingSchemaReadiness>().IsReadyAsync(ct);
                var originalUnitReview = !pending && await ProcurementInvoiceUnitReviewCapability.ReadyAsync(services, ct);
                var principalInvoiceMatching = !pending
                    && services.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is { Enabled: true }
                    && services.GetService<ProcurementPrincipalInvoiceSourceOptions>() is { Enabled: true }
                    && services.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is { } principalInvoiceReadiness
                    && await principalInvoiceReadiness.IsReadyAsync(ct);
                var now = DateTimeOffset.UtcNow;
                var receivingPolicies = receivingDrafts && receivingOptions.PositiveExecutionEnabled
                    ? await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == "RECEIVING" && x.State == "Published"
                        && x.PublishedAt != null && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo)).Take(2).ToArrayAsync(ct)
                    : [];
                var receivingPositive = false;
                ReceivingRuleDocument? receivingRules = null;
                if (receivingPolicies.Length == 1 && ReceivingPolicyRules.Validate(receivingPolicies[0].RulesJson).Count == 0)
                {
                    try
                    {
                        receivingRules = ReceivingPolicyRules.Parse(receivingPolicies[0].RulesJson);
                        receivingPositive = receivingRules.Rules.All(rule => ReceivingPolicyRules.Project(receivingPolicies[0], rule).Published);
                    }
                    catch (InvalidOperationException) { receivingPositive = false; }
                }
                var principalApprovals = !pending && await ProcurementModelParityDiagnostics.MeasureAsync(
                    performance, "meta.principal-approvals", () => ProcurementPrincipalApprovalCapability.ReadyAsync(
                        services, scope.LegalEntityId, ct));
                var sourcingOptions = services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>();
                var sourcingSourceDrafts = !pending && sourcingOptions.Enabled
                    && sourcingOptions.ExecutionDatabaseConfigured
                    && await ProcurementModelParityDiagnostics.MeasureAsync(performance, "meta.sourcing-schema",
                        () => services.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>().IsReadyAsync(ct));
                var principalSourcingSourceDrafts = await ProcurementModelParityDiagnostics.MeasureAsync(
                    performance, "meta.principal-sourcing", () => ProcurementPrincipalSourcingCapability.ReadyAsync(
                        services, scope, pending, sourcingSourceDrafts, ct));
                var directOrderOptions = services.GetService<ProcurementPrincipalOrderDraftOptions>();
                var principalDirectOrderDrafts = !pending && directOrderOptions is
                        { Enabled: true, ExecutionDatabaseConfigured: true }
                    && services.GetService<IProcurementPrincipalOrderDraftReadiness>() is { } directReadiness
                    && await directReadiness.IsReadyAsync(ct);
                var principalOrderGovernance = principalDirectOrderDrafts
                    && services.GetRequiredService<ProcurementOrderExecutionOptions>() is
                        { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true };
                var principalOrderDraftRevision = await PrincipalOrderRevisionCapability.ReadyAsync(
                    services, db, pending, principalDirectOrderDrafts, ct);
                var contractInternalReview = !pending
                    && services.GetService<ProcurementContractReviewOptions>() is
                        { Enabled: true, ExecutionDatabaseConfigured: true }
                    && services.GetService<IProcurementContractReviewSchemaReadiness>() is { } contractReviewReadiness
                    && await contractReviewReadiness.IsConfiguredAsync(ct);
                var principalReceivingOptions = services.GetService<ProcurementPrincipalReceivingOptions>();
                var principalReceiving = !pending && principalReceivingOptions is
                        { Enabled: true, ExecutionDatabaseConfigured: true }
                    && services.GetService<IProcurementPrincipalReceivingReadiness>() is { } receivingReadiness
                    && await receivingReadiness.IsReadyAsync(ct);
                var principalGoodsReturnDrafts = await ProcurementPrincipalGoodsReturnComposition
                    .ReadyAsync(services, pending, ct);
                var publicationOptions = services.GetService<ProcurementPrincipalSourcingPublicationOptions>();
                var principalSourcingPublication = sourcingSourceDrafts && publicationOptions is { Enabled: true }
                    && services.GetRequiredService<ProcurementApprovalExecutionOptions>() is
                        { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true }
                    && await ProcurementPrincipalApprovalCapability.ConfiguredStoreReadyAsync(services, ct)
                    && services.GetService<IProcurementPrincipalSourcingPublicationReadiness>() is { } publicationReadiness
                    && await publicationReadiness.IsReadyAsync(ct)
                    && await ProcurementPrincipalPeople.Resolve(services.GetRequiredService<HrmDbContext>(),
                        scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct) is { } publicationPerson
                    && (scope.WorkLocationId is { } selectedPublicationBranch
                        ? publicationPerson.OrganisationWide
                            || publicationPerson.AllowedBranches.Contains(selectedPublicationBranch)
                        : publicationPerson.OrganisationWide
                            || publicationPerson.AllowedBranches.Count > 0)
                    && (publicationPerson.Permissions.Contains("procurement-sourcing-submit")
                        || publicationPerson.Permissions.Contains("procurement-sourcing-publish"));
                var principalRfqRecommendationDrafts = await ProcurementRfqRecommendationCapability
                    .ReadyAsync(services, db, scope, http, pending, ct);
                return Results.Ok(new
                {
                    module = "procurement",
                    version = 1,
                    receivingSchemas = new { source = 1, policy = 1 },
                    tenantId = scope.TenantId,
                    legalEntityId = scope.LegalEntityId,
                    workLocationId = scope.WorkLocationId,
                    orgUnitId = scope.OrgUnitId,
                    confined = scope.IsConfined,
                    schemaReady = !pending,
                    phase = "draft-crud",
                    crudEnabled = !pending,
                    capabilities = new
                    {
                        vendorDraft = !pending,
                        itemDraft = !pending,
                        requestDraft = !pending,
                        planningDraft = !pending,
                        configurationDraft = !pending,
                        contractDraft = !pending,
                        contractDraftRates = !pending,
                        contractInternalReview,
                        recurringPurchaseDraft = !pending,
                        sourcingDraft = !pending,
                        // Separate future governed holds/releases from existing unlinked draft CRUD.
                        sourcingSourceDrafts,
                        principalSourcingSourceDrafts,
                        principalSourcingPublication,
                        principalRfqRecommendationDrafts, // Availability only; named-reader and retained proof remain per round.
                        // Capability describes configured availability; each command still
                        // checks current identity, record scope, policy, authority and Finance.
                        budgetEvidence = financeCheckerReads,
                        budgetRecording = financeCheckerCapture,
                        // Separate successor contract; existing worker-based clients do not
                        // infer compatibility merely from general approval availability.
                        principalApprovals,
                        approvals = !pending && financeCheckerReads
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>().ExecutionDatabaseConfigured
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>()
                                .HasCurrentManualEvidencePolicy(scope.LegalEntityId, DateTimeOffset.UtcNow)
                            && await services.GetRequiredService<IProcurementApprovalSchemaReadiness>().IsReadyAsync(ct),
                        sourcing = false,
                        contracts = false,
                        orders = !pending && financeCheckerReads
                            && services.GetRequiredService<ProcurementOrderExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementOrderExecutionOptions>().ExecutionDatabaseConfigured
                            && services.GetRequiredService<ProcurementOrderExecutionOptions>()
                                .HasCurrentManualCommitmentPolicy(scope.LegalEntityId, DateTimeOffset.UtcNow)
                            && services.GetRequiredService<ProcurementOrderExecutionOptions>().HasApprovedDeliveryPolicy
                            && await services.GetRequiredService<IProcurementOrderSchemaReadiness>().IsReadyAsync(ct),
                        orderDraft = !pending,
                        principalDirectOrderDrafts,
                        principalOrderGovernance,
                        principalOrderDraftRevision,
                        principalReceiving,
                        principalGoodsReturnDrafts, // Availability only; each route rechecks actor, scope and retained source.
                        receiving = false, // Existing UI is not yet a compatible live receiving adapter.
                        receivingDrafts,
                        receivingGoods = receivingPositive && receivingRules!.Rules.Any(x => x.Kind.ToString() == "Goods"),
                        receivingInspection = receivingPositive && receivingRules!.Rules.Any(x => x.InspectionRequired),
                        receivingServices = receivingPositive && receivingRules!.Rules.Any(x => x.Kind.ToString() == "Service"),
                        receivingCorrections = false,
                        invoiceIntake = !pending,
                        vendorCreditDraft = !pending,
                        manualPaymentWorkflow = !pending
                            && services.GetRequiredService<ProcurementManualPaymentOptions>().Enabled
                            && services.GetRequiredService<ProcurementManualPaymentOptions>().ExecutionDatabaseConfigured
                            && await services.GetRequiredService<IProcurementManualPaymentGovernanceReadiness>().IsReadyAsync(ct),
                        invoiceDocuments = !pending && app.Services.GetRequiredService<CoreDocumentFeature>().Enabled
                            && await services.GetRequiredService<IDocumentSchemaReadiness>().IsReadyAsync(ct),
                        invoiceComparison = !pending
                            && services.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().ExecutionDatabaseConfigured
                            && await services.GetRequiredService<IProcurementInvoiceMatchingSchemaReadiness>().IsReadyAsync(ct),
                        invoiceOriginalUnitReview = originalUnitReview, // Configured availability only; per-record policy/actor/document guards still apply.
                        principalInvoiceMatching, // Separate default-off PO2 contract; never inferred from legacy comparison.
                        invoiceQualifiedClaims = false, // No authenticated complete Finance history owner is installed.
                        invoiceValidation = false,
                        financeHandoff = false,
                        supplierPortal = false,
                    },
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Problem(statusCode: 503, title: "Procurement database unavailable.");
            }
        }).RequireAuthorization("procurement-access");
    }
}

public sealed class ProcurementRequestScope : IProcurementScope, IApprovalScope, Mightyfin.Erp.Core.Documents.Domain.IDocumentScope
{
    private bool resolved;
    private string tenantId = "";
    private Guid legalEntityId;
    private string subjectId = "";

    public string TenantId => resolved ? tenantId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public Guid LegalEntityId => resolved ? legalEntityId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public string SubjectId => resolved ? subjectId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public Guid? WorkLocationId { get; private set; }
    public Guid? OrgUnitId { get; private set; }
    public Guid? PrincipalId { get; private set; }
    public Guid? WorkerId { get; private set; }
    // Current owning Core account link retained only as optional known-person
    // alias metadata. Unlike WorkerId, this does not assert active employment,
    // home-company membership, access, or operational approval authority.
    public Guid? CoreLinkedWorkerId { get; private set; }
    // Retained response compatibility only. System users are never narrowed by HR linkage.
    public bool AdminViewOnly => false;
    public bool IsConfined { get; private set; }
    // Set only by middleware after validating an explicit company and current Core
    // branch assignments. A missing branch header alone never selects this scope.
    public bool IsExplicitAllBranches { get; private set; }
    // Current Core assignment intersection for the selected company. Never supplied by a client.
    public IReadOnlyList<Guid> PermittedWorkLocationIds { get; private set; } = [];
    public string? CorrelationId { get; private set; }

    public void Set(string tenant, Guid entity, string subject, Guid? location, Guid? orgUnit, bool confined, string correlation,
        bool explicitAllBranches = false, IReadOnlyList<Guid>? permittedWorkLocationIds = null)
    {
        if (resolved || string.IsNullOrWhiteSpace(tenant) || entity == Guid.Empty || string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("A valid Procurement scope may be set only once.");
        if (explicitAllBranches && (location is not null || orgUnit is not null))
            throw new InvalidOperationException("All branches cannot also select a branch or unit.");
        tenantId = tenant;
        legalEntityId = entity;
        subjectId = subject;
        WorkLocationId = location;
        OrgUnitId = orgUnit;
        IsConfined = confined;
        IsExplicitAllBranches = explicitAllBranches;
        PermittedWorkLocationIds = permittedWorkLocationIds?.Distinct().ToArray() ?? [];
        CorrelationId = correlation;
        resolved = true;
    }

    public void AssignPrincipal(Guid principalId, Guid? coreLinkedWorkerId = null)
    {
        if (!resolved || PrincipalId.HasValue || principalId == Guid.Empty
            || !Guid.TryParse(subjectId, out var subject) || subject != principalId)
            throw new InvalidOperationException("A resolved scope may bind only its verified Core principal once.");
        PrincipalId = principalId;
        CoreLinkedWorkerId = coreLinkedWorkerId is { } linked && linked != Guid.Empty ? linked : null;
    }

    public void AssignWorker(Guid workerId)
    {
        if (!resolved || AdminViewOnly || WorkerId.HasValue || workerId == Guid.Empty)
            throw new InvalidOperationException("A resolved Procurement scope may receive one worker mapping.");
        WorkerId = workerId;
    }
}

/// <summary>Resolves entity and branch from authenticated ERP identity and Organization data.</summary>
public sealed class ProcurementScopeMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext http, ProcurementRequestScope scope, HrmDbContext db)
    {
        // Preserve constructor compatibility; Core account checks do not have a development exception.
        _ = environment;
        if (!http.Request.Path.StartsWithSegments("/api/procurement/v1"))
        {
            await next(http);
            return;
        }

        // Authorization runs before this middleware. Anonymous requests retain the normal 401.
        if (http.User.Identity?.IsAuthenticated != true)
        {
            await next(http);
            return;
        }

        var tenant = http.User.FindFirstValue("tenant");
        var subject = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(subject))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "A Core tenant and system user are required.");
            return;
        }
        var principal = ProcurementCorePrincipals.Cached(http, tenant, subject)
            ?? await ProcurementCorePrincipals.CurrentAsync(db, tenant, subject, http.RequestAborted);
        if (principal is null)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "An active Core system user is required.");
            return;
        }
        var userId = principal.Id;
        var currentClaims = principal.Claims(tenant);
        if (!ProcurementPermissions.Allows("procurement-access", currentClaims))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "Your current Core permissions do not allow Procurement access.");
            return;
        }
        // Named route authorization has already run. Recheck it against current
        // stored Core grants, including revocations after session/claim creation.
        var required = http.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (required is not null && required.Any(x => !string.IsNullOrWhiteSpace(x.Policy)
            && !ProcurementPermissions.Allows(x.Policy, currentClaims)))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "Your current Core permissions do not allow this action.");
            return;
        }
        ProcurementCorePrincipals.Apply(http, tenant, principal);

        var requestedEntityText = http.Request.Headers["X-Shell-Entity"].FirstOrDefault()?.Trim();
        var requestedLocationText = http.Request.Headers["X-Shell-Location"].FirstOrDefault()?.Trim();
        var branchScope = http.Request.Headers["X-Shell-Branch-Scope"].ToString().Trim();
        var explicitAllBranches = branchScope.Equals("all", StringComparison.OrdinalIgnoreCase);
        var allRead = HttpMethods.IsGet(http.Request.Method)
            && (AllReadPath(http.Request.Path, "/api/procurement/v1/vendors")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/items")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/contracts")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/plans")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/configuration/categories")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/configuration/methods")
                || string.Equals(http.Request.Path.Value, "/api/procurement/v1/catalog", StringComparison.OrdinalIgnoreCase)
                || AllReadPath(http.Request.Path, "/api/procurement/v1/requests")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/orders")
                || AllReadPath(http.Request.Path, "/api/procurement/v1/invoices"));
        // Supplier and item masters are company-owned; their existing commands do
        // not create a branch transaction or infer branch authority from this header.
        var allCompanyMaster = AllVendorMasterPath(http.Request.Path, http.Request.Method)
            || AllItemMasterPath(http.Request.Path, http.Request.Method);
        if (http.Request.Headers.ContainsKey("X-Shell-Branch-Scope") && (!explicitAllBranches
            || !(string.Equals(http.Request.Path.Value, "/api/procurement/v1/context", StringComparison.OrdinalIgnoreCase)
                || string.Equals(http.Request.Path.Value, "/api/procurement/v1/meta", StringComparison.OrdinalIgnoreCase)
                || http.Request.Path.StartsWithSegments("/api/procurement/v1/manual-payments")
                || allRead || allCompanyMaster)
            || string.IsNullOrWhiteSpace(requestedEntityText)
            || http.Request.Headers.ContainsKey("X-Shell-Location")))
        {
            await Deny(http, StatusCodes.Status400BadRequest, "Choose one valid company scope and branch selection.");
            return;
        }
        Guid? requestedEntity = null;
        Guid? requestedLocation = null;
        if (!string.IsNullOrEmpty(requestedEntityText))
        {
            if (!Guid.TryParse(requestedEntityText, out var parsed) || parsed == Guid.Empty)
            {
                await Deny(http, StatusCodes.Status400BadRequest, "Choose a valid company.");
                return;
            }
            requestedEntity = parsed;
        }
        if (!string.IsNullOrEmpty(requestedLocationText))
        {
            if (!Guid.TryParse(requestedLocationText, out var parsed) || parsed == Guid.Empty)
            {
                await Deny(http, StatusCodes.Status400BadRequest, "Choose a valid branch.");
                return;
            }
            requestedLocation = parsed;
        }

        var legalEntities = await db.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == tenant && !x.IsArchived)
            .Select(x => new { x.Id, x.IsDefault, x.RegisteredName })
            .ToListAsync(http.RequestAborted);
        if (legalEntities.Count == 0)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "No company is available for this account.");
            return;
        }

        var assignedLocationIds =
            await db.UserBranchAssignments.AsNoTracking()
                .Where(x => x.TenantId == tenant && x.UserId == userId && !x.IsArchived)
                .Select(x => x.LocationId).Distinct().ToListAsync(http.RequestAborted);
        var confined = assignedLocationIds.Count > 0;
        var assignedLocations = confined
            ? await db.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == tenant && !x.IsArchived && assignedLocationIds.Contains(x.Id))
                .Select(x => new { x.Id, x.LegalEntityId, x.Type })
                .ToListAsync(http.RequestAborted)
            : [];
        if (confined && assignedLocations.Count == 0)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "Assigned branches are unavailable.");
            return;
        }

        Guid? effectiveLocation = null;
        Guid? effectiveOrgUnit = null;
        Guid? entityFromLocation = null;
        if (requestedLocation.HasValue)
        {
            var workLocation = await db.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == tenant && x.Id == requestedLocation.Value && !x.IsArchived)
                .Select(x => new { x.Id, x.LegalEntityId })
                .SingleOrDefaultAsync(http.RequestAborted);
            if (workLocation is not null)
            {
                if (confined && !assignedLocationIds.Contains(workLocation.Id))
                {
                    await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that branch.");
                    return;
                }
                effectiveLocation = workLocation.Id;
                entityFromLocation = workLocation.LegalEntityId;
            }
            else if (!confined)
            {
                var orgUnit = await db.OrgUnits.AsNoTracking()
                    .Where(x => x.TenantId == tenant && x.Id == requestedLocation.Value && !x.IsArchived && x.Status == "active")
                    .Join(db.LegalEntities.Where(e => e.TenantId == tenant && !e.IsArchived),
                        x => x.LegalEntityId, e => e.Id, (x, _) => new { x.Id, x.LegalEntityId })
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (orgUnit is not null)
                {
                    effectiveOrgUnit = orgUnit.Id;
                    entityFromLocation = orgUnit.LegalEntityId;
                }
            }
            if (!entityFromLocation.HasValue)
            {
                await Deny(http, StatusCodes.Status403Forbidden, "That branch is unavailable for this account.");
                return;
            }
        }
        else if (confined)
        {
            var company = requestedEntity ?? legalEntities
                .Where(x => assignedLocations.Any(location => location.LegalEntityId == x.Id))
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.RegisteredName).ThenBy(x => x.Id)
                .Select(x => (Guid?)x.Id).FirstOrDefault();
            var permitted = assignedLocations.Where(x => x.LegalEntityId == company).ToArray();
            if (permitted.Length == 0)
            {
                await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
                return;
            }
            if (!explicitAllBranches)
            {
                var headquarters = permitted.Where(x => string.Equals(x.Type, "headquarters", StringComparison.OrdinalIgnoreCase)).ToArray();
                var chosen = headquarters.Length == 1 ? headquarters[0]
                    : permitted.Length == 1 ? permitted[0] : null;
                if (chosen is null)
                {
                    await Deny(http, StatusCodes.Status400BadRequest,
                        "Choose a branch or explicitly select All permitted branches.");
                    return;
                }
                effectiveLocation = chosen.Id;
            }
            entityFromLocation = company;
        }

        if (requestedEntity.HasValue && !legalEntities.Any(x => x.Id == requestedEntity.Value))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
            return;
        }
        if (requestedEntity.HasValue && entityFromLocation.HasValue && requestedEntity.Value != entityFromLocation.Value)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "The company and branch do not match.");
            return;
        }
        if (confined && requestedEntity.HasValue && !assignedLocations.Any(x => x.LegalEntityId == requestedEntity.Value))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
            return;
        }

        // Existing branch assignments are authoritative. Empty assignments retain
        // Core's tenant-wide semantics; an employee home is never a company grant.
        var selectedEntity = requestedEntity ?? entityFromLocation
            ?? legalEntities.OrderBy(x => x.IsDefault ? 0 : 1).ThenBy(x => x.RegisteredName).First().Id;
        if (!legalEntities.Any(x => x.Id == selectedEntity))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "The selected company is unavailable.");
            return;
        }
        Guid[] permittedLocations = explicitAllBranches
            ? confined ? assignedLocations.Where(x => x.LegalEntityId == selectedEntity).Select(x => x.Id).ToArray()
                : await db.WorkLocations.AsNoTracking().Where(x => x.TenantId == tenant && !x.IsArchived
                    && x.LegalEntityId == selectedEntity).Select(x => x.Id).ToArrayAsync(http.RequestAborted)
            : [];
        scope.Set(tenant, selectedEntity, principal.Id.ToString("D"), effectiveLocation, effectiveOrgUnit, confined,
            http.Response.Headers["X-Request-Id"].FirstOrDefault() ?? http.TraceIdentifier,
            explicitAllBranches, permittedLocations);
        scope.AssignPrincipal(principal.Id, principal.WorkerId);
        // Optional projection for existing worker-pinned operational consumers.
        // No mapping, inactive HR profile or another-company home never grants or
        // removes general Core-account access, and no worker identifier is fabricated.
        var workerId = await ProcurementWorkforce.ResolveActiveWorker(db, scope, http.RequestAborted);
        if (workerId is Guid worker) scope.AssignWorker(worker);
        await next(http);
    }

    private static async Task Deny(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ApiError(status == 400 ? "invalid-scope" : "forbidden", message, []),
            http.RequestAborted);
    }

    private static bool AllReadPath(PathString path, string root)
    {
        if (string.Equals(path.Value, root, StringComparison.OrdinalIgnoreCase)) return true;
        return path.StartsWithSegments(root, out var remainder)
            && Guid.TryParse(remainder.Value?.Trim('/'), out var id) && id != Guid.Empty;
    }

    // Finite existing company-master routes. A future nested route must earn its
    // own scope review instead of inheriting a prefix-wide All admission.
    private static bool AllVendorMasterPath(PathString path, string method)
    {
        if (!path.StartsWithSegments("/api/procurement/v1/vendors", out var rest)) return false;
        var parts = rest.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length == 0) return HttpMethods.IsGet(method) || HttpMethods.IsPost(method);
        if (!Guid.TryParse(parts[0], out var vendor) || vendor == Guid.Empty) return false;
        if (parts.Length == 1) return HttpMethods.IsGet(method) || HttpMethods.IsPut(method);
        if (parts.Length == 2)
            return parts[1] switch
            {
                "open-orders" => HttpMethods.IsGet(method),
                "submit" or "return" or "approve" or "reject" or "suspend" or "reactivate" or "withdraw"
                    => HttpMethods.IsPost(method),
                "contacts" or "sites" or "qualifications" => HttpMethods.IsPost(method),
                _ => false
            };
        if (parts.Length >= 3 && (parts[1] == "contacts" || parts[1] == "sites" || parts[1] == "qualifications")
            && Guid.TryParse(parts[2], out var child) && child != Guid.Empty)
            return parts.Length == 3 && HttpMethods.IsPut(method)
                || parts.Length == 4 && HttpMethods.IsPost(method)
                    && (parts[3] == "remove" || parts[1] == "qualifications" && parts[3] == "review");
        return false;
    }

    private static bool AllItemMasterPath(PathString path, string method)
    {
        if (!path.StartsWithSegments("/api/procurement/v1/items", out var rest)) return false;
        var parts = rest.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length == 0) return HttpMethods.IsGet(method) || HttpMethods.IsPost(method);
        if (!Guid.TryParse(parts[0], out var item) || item == Guid.Empty) return false;
        if (parts.Length == 1) return HttpMethods.IsGet(method) || HttpMethods.IsPut(method);
        if (parts.Length == 2)
            return parts[1] switch
            {
                "activate" or "deactivate" => HttpMethods.IsPost(method),
                "catalog-entries" => HttpMethods.IsGet(method) || HttpMethods.IsPost(method),
                _ => false
            };
        if (parts.Length >= 3 && parts[1] == "catalog-entries"
            && Guid.TryParse(parts[2], out var entry) && entry != Guid.Empty)
            return parts.Length == 3 && HttpMethods.IsPut(method)
                || parts.Length == 4 && HttpMethods.IsPost(method)
                    && (parts[3] == "publish" || parts[3] == "withdraw");
        return false;
    }
}
