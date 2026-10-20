using System.Security.Claims;
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
    public static void AddServices(IServiceCollection services, string connection, IConfiguration configuration)
    {
        services.AddScoped<ProcurementRequestScope>();
        var executionConnection = configuration.GetConnectionString("ProcurementApprovalExecution");
        var approvalOptions = configuration.GetSection("Procurement:Approvals").Get<ProcurementApprovalExecutionOptions>()
            ?? new ProcurementApprovalExecutionOptions();
        approvalOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(approvalOptions);
        var orderOptions = configuration.GetSection("Procurement:Orders").Get<ProcurementOrderExecutionOptions>()
            ?? new ProcurementOrderExecutionOptions();
        orderOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(orderOptions);
        var sourcingOptions = configuration.GetSection("Procurement:SourcingDrafts").Get<ProcurementSourcingDraftExecutionOptions>() ?? new();
        sourcingOptions.ExecutionDatabaseConfigured = !string.IsNullOrWhiteSpace(executionConnection);
        services.AddSingleton(sourcingOptions);
        services.AddScoped<IProcurementSourcingDraftSchemaReadiness, UnavailableProcurementSourcingDraftSchemaReadiness>();
        services.AddScoped<IProcurementOrderSchemaReadiness, ProcurementOrderSchemaReadiness>();
        services.AddScoped<IProcurementApprovalSchemaReadiness, ProcurementApprovalSchemaReadiness>();
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
        ProcurementSourcingDraftRoutes.MapRoutes(app);
        ProcurementSourcingExecutionRoutes.MapRoutes(app);
        ProcurementInvoiceIntakeRoutes.MapRoutes(app);
        ProcurementInvoiceMatchingRoutes.MapRoutes(app);
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
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Any();
                var receivingOptions = services.GetRequiredService<ProcurementReceivingExecutionOptions>();
                var receivingDrafts = !pending && receivingOptions.DraftsEnabled && receivingOptions.ExecutionDatabaseConfigured
                    && await services.GetRequiredService<IProcurementReceivingSchemaReadiness>().IsReadyAsync(ct);
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
                        sourcingDraft = !pending,
                        // Separate future governed holds/releases from existing unlinked draft CRUD.
                        sourcingSourceDrafts = !pending
                            && services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().ExecutionDatabaseConfigured
                            && await services.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>().IsReadyAsync(ct),
                        // Capability describes configured availability; each command still
                        // checks current identity, record scope, policy, authority and Finance.
                        approvals = !pending
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>().ExecutionDatabaseConfigured
                            && services.GetRequiredService<ProcurementApprovalExecutionOptions>()
                                .HasCurrentManualEvidencePolicy(scope.LegalEntityId, DateTimeOffset.UtcNow)
                            && await services.GetRequiredService<IProcurementApprovalSchemaReadiness>().IsReadyAsync(ct),
                        sourcing = false,
                        contracts = false,
                        orders = !pending
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
                        invoiceDocuments = !pending && app.Services.GetRequiredService<CoreDocumentFeature>().Enabled
                            && await services.GetRequiredService<IDocumentSchemaReadiness>().IsReadyAsync(ct),
                        invoiceComparison = !pending
                            && services.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().Enabled
                            && services.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().ExecutionDatabaseConfigured
                            && await services.GetRequiredService<IProcurementInvoiceMatchingSchemaReadiness>().IsReadyAsync(ct),
                        invoiceQualifiedClaims = false, // No production original-unit reviewer or authenticated Finance history owner is installed.
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
    public Guid? WorkerId { get; private set; }
    public bool IsConfined { get; private set; }
    public string? CorrelationId { get; private set; }

    public void Set(string tenant, Guid entity, string subject, Guid? location, Guid? orgUnit, bool confined, string correlation)
    {
        if (resolved || string.IsNullOrWhiteSpace(tenant) || entity == Guid.Empty || string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("A valid Procurement scope may be set only once.");
        tenantId = tenant;
        legalEntityId = entity;
        subjectId = subject;
        WorkLocationId = location;
        OrgUnitId = orgUnit;
        IsConfined = confined;
        CorrelationId = correlation;
        resolved = true;
    }

    public void AssignWorker(Guid workerId)
    {
        if (!resolved || WorkerId.HasValue || workerId == Guid.Empty)
            throw new InvalidOperationException("A resolved Procurement scope may receive one worker mapping.");
        WorkerId = workerId;
    }
}

/// <summary>Resolves entity and branch from authenticated ERP identity and Organization data.</summary>
public sealed class ProcurementScopeMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext http, ProcurementRequestScope scope, HrmDbContext db)
    {
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
            await Deny(http, StatusCodes.Status403Forbidden, "A workforce tenant and user are required.");
            return;
        }
        if (!Guid.TryParse(subject, out var userId)
            && !(environment.IsDevelopment() && http.User.Identity.AuthenticationType == "dev"))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "This account needs a workforce user mapping.");
            return;
        }

        var requestedEntityText = http.Request.Headers["X-Shell-Entity"].FirstOrDefault()?.Trim();
        var requestedLocationText = http.Request.Headers["X-Shell-Location"].FirstOrDefault()?.Trim();
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
                    .Where(x => x.Id == requestedLocation.Value && !x.IsArchived && x.Status == "active")
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
            effectiveLocation = first.Id;
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

        // Without a shell selection, start in the active worker's HRMS company.
        // The tenant default may belong to a different company and is not an
        // authorization grant for this employee.
        var workerHomeEntity = !requestedEntity.HasValue && !entityFromLocation.HasValue
            ? (await ProcurementWorkforce.ResolveActiveWorkerHome(db, tenant, subject,
                http.RequestAborted))?.LegalEntityId
            : null;
        var selectedEntity = requestedEntity ?? entityFromLocation ?? workerHomeEntity
            ?? legalEntities.OrderBy(x => x.IsDefault ? 0 : 1).ThenBy(x => x.RegisteredName).First().Id;
        if (!legalEntities.Any(x => x.Id == selectedEntity))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "The selected company is unavailable.");
            return;
        }
        scope.Set(tenant, selectedEntity, subject, effectiveLocation, effectiveOrgUnit, confined,
            http.Response.Headers["X-Request-Id"].FirstOrDefault() ?? http.TraceIdentifier);
        if (!http.Request.Path.Equals("/api/procurement/v1/meta", StringComparison.OrdinalIgnoreCase))
        {
            var workerId = await ProcurementWorkforce.ResolveActiveWorker(db, scope, http.RequestAborted);
            if (workerId is null)
            {
                await Deny(http, StatusCodes.Status403Forbidden,
                    "An active employee in this company is required to use Procurement.");
                return;
            }
            scope.AssignWorker(workerId.Value);
        }
        await next(http);
    }

    private static async Task Deny(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ApiError(status == 400 ? "invalid-scope" : "forbidden", message, []),
            http.RequestAborted);
    }
}
