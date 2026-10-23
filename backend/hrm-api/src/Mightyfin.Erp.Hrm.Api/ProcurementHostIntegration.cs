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
        services.AddSingleton<IProcurementSourcingPolicyIdentitySchemaReadiness, ProcurementSourcingPolicyIdentitySchemaReadiness>();
        var executionConnection = configuration.GetConnectionString("ProcurementApprovalExecution");
        var approvalOptions = configuration.GetSection("Procurement:Approvals").Get<ProcurementApprovalExecutionOptions>()
            ?? new ProcurementApprovalExecutionOptions();
        approvalOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(approvalOptions);
        services.AddSingleton<IProcurementPrincipalApprovalSchemaReadiness,UnavailableProcurementPrincipalApprovalSchemaReadiness>();
        var orderOptions = configuration.GetSection("Procurement:Orders").Get<ProcurementOrderExecutionOptions>()
            ?? new ProcurementOrderExecutionOptions();
        orderOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(orderOptions);
        var sourcingOptions = configuration.GetSection("Procurement:SourcingDrafts").Get<ProcurementSourcingDraftExecutionOptions>() ?? new();
        sourcingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(sourcingOptions);
        services.AddScoped<IProcurementSourcingDraftSchemaReadiness, ProcurementSourcingDraftSchemaReadiness>();
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
        var matchingOptions = configuration.GetSection("Procurement:InvoiceMatching").Get<ProcurementInvoiceMatchingExecutionOptions>() ?? new();
        matchingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(matchingOptions);
        services.AddScoped<IProcurementInvoiceMatchingSchemaReadiness, ProcurementInvoiceMatchingSchemaReadiness>();
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
        ProcurementRecurringPurchaseRoutes.MapRoutes(app);
        ProcurementManualPaymentRoutes.MapReadRoutes(app);
        ProcurementManualPaymentCommandRoutes.MapRoutes(app);
        ProcurementSourcingDraftRoutes.MapRoutes(app);
        ProcurementSourcingExecutionRoutes.MapRoutes(app);
        ProcurementInvoiceIntakeRoutes.MapRoutes(app);
        ProcurementInvoiceIntakeRoutes.MapCreditRoutes(app);
        ProcurementInvoiceMatchingRoutes.MapRoutes(app);
        ProcurementInvoiceUnitReviewRoutes.MapRoutes(app);
        if (app.Services.GetRequiredService<CoreDocumentFeature>().Enabled) ProcurementInvoiceDocumentRoutes.MapRoutes(app);
        ProcurementOrderDraftRoutes.MapRoutes(app);
        ProcurementOrderExecutionRoutes.MapRoutes(app);
        ProcurementReceivingRoutes.MapRoutes(app);
        MapMetadataRoute(app);
    }

    /// <summary>Maps module availability using the same checks as the complete host.</summary>
    public static void MapMetadataRoute(WebApplication app)
    {
        app.MapGet("/api/procurement/v1/meta", async (ProcurementRequestScope scope,
            ProcurementDbContext db, IServiceProvider services, CancellationToken ct) =>
        {
            try
            {
                if (!await db.Database.CanConnectAsync(ct))
                    return Results.Problem(statusCode: 503, title: "Procurement database unavailable.");
                var pending = db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync(ct)).Any();
                var financeCheckerReads = !pending && await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, services, false, ct);
                var financeCheckerCapture = financeCheckerReads && await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, services, true, ct);
                var receivingOptions = services.GetRequiredService<ProcurementReceivingExecutionOptions>();
                var receivingDrafts = !pending && receivingOptions.DraftsEnabled && receivingOptions.ExecutionDatabaseConfigured
                    && await services.GetRequiredService<IProcurementReceivingSchemaReadiness>().IsReadyAsync(ct);
                var originalUnitReview = !pending && await ProcurementInvoiceUnitReviewCapability.ReadyAsync(services, ct);
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
                        recurringPurchaseDraft = !pending,
                        sourcingDraft = !pending,
                        // Separate future governed holds/releases from existing unlinked draft CRUD.
                        sourcingSourceDrafts = !pending
                            && services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().ExecutionDatabaseConfigured
                            && await services.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>().IsReadyAsync(ct),
                        // Capability describes configured availability; each command still
                        // checks current identity, record scope, policy, authority and Finance.
                        budgetEvidence = financeCheckerReads,
                        budgetRecording = financeCheckerCapture,
                        // Separate successor contract; existing worker-based clients do not
                        // infer compatibility merely from general approval availability.
                        principalApprovals = !pending && await ProcurementPrincipalApprovalCapability.ReadyAsync(services,scope.LegalEntityId,ct),
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
    public string? CorrelationId { get; private set; }

    public void Set(string tenant, Guid entity, string subject, Guid? location, Guid? orgUnit, bool confined, string correlation,
        bool explicitAllBranches = false)
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
        if (http.Request.Headers.ContainsKey("X-Shell-Branch-Scope") && (!explicitAllBranches
            || !(string.Equals(http.Request.Path.Value, "/api/procurement/v1/context", StringComparison.OrdinalIgnoreCase)
                || http.Request.Path.StartsWithSegments("/api/procurement/v1/manual-payments"))
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
                .Select(x => new { x.Id, x.LegalEntityId })
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
            var first = assignedLocations
                .Where(x => !requestedEntity.HasValue || x.LegalEntityId == requestedEntity.Value)
                .OrderBy(x => x.Id).FirstOrDefault();
            if (first is null)
            {
                await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
                return;
            }
            if (!explicitAllBranches) effectiveLocation = first.Id;
            entityFromLocation = first.LegalEntityId;
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
        scope.Set(tenant, selectedEntity, principal.Id.ToString("D"), effectiveLocation, effectiveOrgUnit, confined,
            http.Response.Headers["X-Request-Id"].FirstOrDefault() ?? http.TraceIdentifier, explicitAllBranches);
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
}
