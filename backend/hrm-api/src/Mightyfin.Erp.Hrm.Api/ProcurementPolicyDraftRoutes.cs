using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Company-scoped policy authoring. Drafts are never effective purchase authority.</summary>
public static partial class ProcurementPolicyDraftRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/configuration/policies");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (SourcingIdentityProblem problem) { return Problem(problem.Code, problem.Message, problem.Status); }
        });
        group.MapGet("", List).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/effective", Effective).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/lookups/sourcing-reviewers", SourcingReviewers).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-configuration-read");
        group.MapPost("", Create).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/validate", ValidateRules).RequireAuthorization("procurement-configuration-edit");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/preview", Preview).RequireAuthorization("procurement-configuration-read");
        group.MapPost("/{id:guid}/submit-review", SubmitReview).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/publish", Publish).RequireAuthorization("procurement-configuration-approve");
        group.MapPost("/{id:guid}/return-for-changes", ReturnForChanges).RequireAuthorization("procurement-configuration-approve");
    }

    private static IResult ValidateRules(PolicyValidationRequest input)
    {
        var issues = SourcingPublicationPolicyRules.IsSourcingDocument(input.RulesJson) ? SourcingPublicationPolicyRules.Validate(input.RulesJson) : InvoiceMatchingPolicyRules.IsMatchingDocument(input.RulesJson) ? InvoiceMatchingPolicyRules.Validate(input.RulesJson) : ReceivingPolicyRules.IsReceivingDocument(input.RulesJson) ? ReceivingPolicyRules.Validate(input.RulesJson) : PolicyRuleValidator.Validate(input.RulesJson);
        return Results.Ok(new { isValid = issues.Count == 0, issues });
    }

    private static async Task<IResult> Preview(Guid id, PolicyScenario scenario,
        ProcurementDbContext db, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if (policy.PolicyKey == "RECEIVING") return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "ReceivingPreviewRequiresSealedSource", issues = ReceivingPolicyRules.Validate(policy.RulesJson) });
        if (policy.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey) return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "InvoiceMatchingPreviewRequiresSourceAndOwnerEvidence", issues = InvoiceMatchingPolicyRules.Validate(policy.RulesJson) });
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            return Results.Ok(new { policyId = policy.Id, policy.PolicyVersionNumber, policy.State, isAuthority = false,
                coverageIssues = SourcingPublicationPolicyRules.AnalyzeCoverage(policy.RulesJson),
                decision = SourcingPublicationPolicyRules.Evaluate(policy.RulesJson, scenario) });
        var coverageIssues = PolicyRulePreview.AnalyzeCoverage(policy.RulesJson);
        var decision = PolicyRulePreview.Evaluate(policy.RulesJson, scenario);
        return Results.Ok(new { policyId = policy.Id, policy.PolicyVersionNumber,
            policy.State, isAuthority = false, coverageIssues, decision });
    }

    private static async Task<IResult> Effective(string? key, DateTimeOffset? at,
        ProcurementDbContext db, HttpContext http, CancellationToken ct)
    {
        var normalized = key?.Trim().ToUpperInvariant();
        if (normalized is not ("PURCHASE_REQUEST" or "PURCHASE_ORDER" or "RECEIVING" or "INVOICE_MATCHING" or "SOURCING"))
            return Problem("validation-failed", "Choose purchase requests, purchase orders, receiving, invoice matching or sourcing.", 422);
        var instant = (at ?? DateTimeOffset.UtcNow).ToUniversalTime();
        if (normalized == SourcingPublicationPolicyRules.PolicyKey)
        {
            await RequireSourcingIdentitySchema(http, db, ct);
            var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == normalized && x.State == "Published"
                    && x.EffectiveFrom <= instant && (x.EffectiveTo == null || x.EffectiveTo > instant))
                .OrderByDescending(x => x.PolicyVersionNumber).Take(2).ToArrayAsync(ct);
            if (policies.Length == 0) return Results.Ok(new { status = "MissingPolicy", isAuthority = false, policy = (object?)null, evaluatedAt = instant });
            if (policies.Length > 1) return Results.Ok(new { status = "AmbiguousPolicy", isAuthority = false, policy = (object?)null, evaluatedAt = instant });
            var current = policies[0];
            await VerifySourcingPublishedPolicyIdentity(db, current, ct);
            return Results.Ok(new { status = "Valid", isAuthority = false,
                policy = (object)new { current.Id, current.PolicyKey, current.PolicyVersionNumber, current.EffectiveFrom, current.EffectiveTo, current.PublishedAt }, evaluatedAt = instant });
        }

        var matches = await db.PolicyVersions.AsNoTracking()
            .Where(x => x.PolicyKey == normalized && x.State == "Published"
                && x.EffectiveFrom <= instant && (x.EffectiveTo == null || x.EffectiveTo > instant))
            .OrderByDescending(x => x.PolicyVersionNumber).Take(2)
            .Select(x => new { x.Id, x.PolicyKey, x.PolicyVersionNumber,
                x.EffectiveFrom, x.EffectiveTo, x.PublishedAt })
            .ToListAsync(ct);
        if (matches.Count == 0)
            return Results.Ok(new { status = "MissingPolicy", isAuthority = false,
                policy = (object?)null, evaluatedAt = instant });
        if (matches.Count > 1)
            return Results.Ok(new { status = "AmbiguousPolicy", isAuthority = false,
                policy = (object?)null, evaluatedAt = instant });
        return Results.Ok(new { status = "Valid", isAuthority = false,
            policy = (object)matches[0], evaluatedAt = instant });
    }

    private static async Task<IResult> List(ProcurementDbContext db, string? key,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.PolicyVersions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(key))
        {
            var normalizedKey = key.Trim().ToUpperInvariant();
            query = query.Where(x => x.PolicyKey == normalizedKey);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.PolicyKey).ThenByDescending(x => x.PolicyVersionNumber)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.PolicyKey, x.PolicyVersionNumber, x.State,
                x.EffectiveFrom, x.EffectiveTo, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        var lastReturn = policy.State == "Draft"
            ? await db.AuditEvents.AsNoTracking()
                .Where(x => x.RecordType == "PolicyVersion" && x.RecordId == id
                    && x.Action == "ReturnedForChanges")
                .OrderByDescending(x => x.CreatedAt).Select(x => x.ChangedFieldsJson)
                .FirstOrDefaultAsync(ct)
            : null;
        string? lastReturnReason = null;
        if (!string.IsNullOrWhiteSpace(lastReturn))
        {
            using var evidence = JsonDocument.Parse(lastReturn);
            if (evidence.RootElement.TryGetProperty("reason", out var reason)
                && reason.ValueKind == JsonValueKind.String)
                lastReturnReason = reason.GetString();
        }
        return Results.Ok(View(policy, lastReturnReason));
    }

    private static async Task<IResult> Create(PolicyDraft input, ProcurementDbContext db,
        HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        var issue = Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var key = input.PolicyKey!.Trim().ToUpperInvariant();
        if (key == SourcingPublicationPolicyRules.PolicyKey) await RequireSourcingIdentitySchema(http, db, ct);
        if ((key is "RECEIVING" or "INVOICE_MATCHING") && !await ReceivingConfigurationActor(organizationDb, scope, "procurement-configuration-edit", ct))
            return Problem("receiving-authority-unavailable", "Your current account cannot configure receiving rules.", 403);
        var next = (await db.PolicyVersions.AsNoTracking()
            .Where(x => x.PolicyKey == key).MaxAsync(x => (int?)x.PolicyVersionNumber, ct) ?? 0) + 1;
        var policy = new PolicyVersion
        {
            PolicyKey = key, PolicyVersionNumber = next, State = "Draft",
            EffectiveFrom = input.EffectiveFrom.ToUniversalTime(),
            EffectiveTo = input.EffectiveTo?.ToUniversalTime(),
            RulesJson = input.RulesJson!.Trim(),
        };
        if (key == SourcingPublicationPolicyRules.PolicyKey)
        {
            var sourcingIssue = await CheckSourcingCompanyRules(policy, db, organizationDb, scope, "procurement-configuration-edit", ct);
            if (sourcingIssue is not null) return sourcingIssue;
        }
        db.AddScoped(policy);
        if (key == SourcingPublicationPolicyRules.PolicyKey)
        {
            var actor = await SourcingCurrentActor(organizationDb, scope, "procurement-configuration-edit", ct);
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, scope.TenantId, scope.LegalEntityId, "Created", actor.PrincipalId,
                actor.WorkerId, 1, DateTimeOffset.UtcNow));
        }
        AddAudit(db, policy, scope.SubjectId, "DraftCreated");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return Problem("version-conflict", "Another policy draft was added. Refresh and try again.", 409); }
        return Results.Created($"/api/procurement/v1/configuration/policies/{policy.Id}", View(policy));
    }

    private static async Task<IResult> Update(Guid id, VersionedPolicyDraft input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if ((policy.PolicyKey is "RECEIVING" or "INVOICE_MATCHING") && !await ReceivingConfigurationActor(organizationDb, scope, "procurement-configuration-edit", ct))
            return Problem("receiving-authority-unavailable", "Your current account cannot configure receiving rules.", 403);
        if (policy.Version != input.Version)
            return Problem("version-conflict", "Refresh this policy draft before saving.", 409);
        if (policy.State != "Draft")
            return Problem("policy-locked", "Only a draft policy can be changed.", 409);
        var issue = Validate(input.Draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!string.Equals(policy.PolicyKey, input.Draft.PolicyKey!.Trim(), StringComparison.OrdinalIgnoreCase))
            return Problem("key-locked", "A policy key cannot be changed. Start another draft.", 409);
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
        {
            await RequireSourcingIdentitySchema(http, db, ct);
            _ = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
            // Validate the proposed JSON before mutating the tracked draft.
            var sourcingIssue = await CheckSourcingCompanyRules(new PolicyVersion { PolicyKey = policy.PolicyKey, RulesJson = input.Draft.RulesJson! },
                db, organizationDb, scope, "procurement-configuration-edit", ct);
            if (sourcingIssue is not null) return sourcingIssue;
        }
        policy.EffectiveFrom = input.Draft.EffectiveFrom.ToUniversalTime();
        policy.EffectiveTo = input.Draft.EffectiveTo?.ToUniversalTime();
        policy.RulesJson = input.Draft.RulesJson!.Trim();
        AddAudit(db, policy, scope.SubjectId, "DraftUpdated");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy draft before saving.", 409); }
        return Results.Ok(View(policy));
    }

    private static async Task<IResult> SubmitReview(Guid id, ReviewSubmission input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope,
        HttpContext http, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if (policy.Version != input.Version)
            return Problem("version-conflict", "Refresh this policy draft before sending it for review.", 409);
        if (policy.State != "Draft")
            return Problem("policy-locked", "Only a draft policy can be sent for review.", 409);
        if (policy.CreatedBy != scope.SubjectId)
            return Problem("not-author", "Only the draft author can send it for review.", 403);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Enter a review note of up to 1,000 characters.", 422);
        (SourcingPolicyIdentityEvidence Origin, ProcurementAuthorityIdentity.CanonicalAccount Actor)? sourcingAuthor = null;
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
        {
            await RequireSourcingIdentitySchema(http, db, ct);
            sourcingAuthor = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
        }
        var referenceIssue = await CheckCompanyRules(policy, db, organizationDb, scope, ct);
        if (referenceIssue is not null) return referenceIssue;
        policy.State = "PendingReview";
        policy.ReviewRequestedAt = DateTimeOffset.UtcNow;
        policy.ReviewRequestedBy = scope.SubjectId;
        policy.ReviewReason = input.Reason.Trim();
        if (sourcingAuthor is { } author)
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, scope.TenantId, scope.LegalEntityId, "Submitted", author.Actor.PrincipalId,
                author.Actor.WorkerId, checked(policy.Version + 1), policy.ReviewRequestedAt.Value, author.Origin));
        AddAudit(db, policy, scope.SubjectId, "ReviewRequested");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy draft before sending it for review.", 409); }
        return Results.Ok(View(policy));
    }

    private static async Task<IResult> Publish(Guid id, PublicationInput input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope,
        HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Enter a publication reason of up to 1,000 characters.", 422);
        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational())
                transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
            if (policy.Version != input.Version)
                return Problem("version-conflict", "Refresh this policy before publication.", 409);
            if (policy.State != "PendingReview")
                return Problem("policy-locked", "Only a policy waiting for review can be published.", 409);
            if (policy.CreatedBy == scope.SubjectId || policy.ReviewRequestedBy == scope.SubjectId)
                return Problem("self-approval", "Another authorized person must publish this policy.", 403);
            if (policy.ReviewRequestedAt is null || string.IsNullOrWhiteSpace(policy.ReviewReason))
                return Problem("review-missing", "Send this policy for review before publication.", 409);
            if (policy.EffectiveFrom <= DateTimeOffset.UtcNow)
                return Problem("effective-date", "Choose a future start date in a new draft before publication.", 422);
            var referenceIssue = await CheckCompanyRules(policy, db, organizationDb, scope, ct, "procurement-configuration-approve");
            if (referenceIssue is not null) return referenceIssue;
            (SourcingPolicyIdentityEvidence Origin, SourcingPolicyIdentityEvidence Submitted, ProcurementAuthorityIdentity.CanonicalAccount Actor)? sourcingReviewer = null;
            if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            {
                await RequireSourcingIdentitySchema(http, db, ct);
                sourcingReviewer = await SourcingIndependentPolicyReviewer(db, organizationDb, scope, policy, ct);
            }
            if ((policy.PolicyKey is "RECEIVING" or "INVOICE_MATCHING") && !await ReceivingIndependentReviewer(organizationDb, scope, policy, ct))
                return Problem("self-approval", "Another current authorized worker must independently review this receiving policy.", 403);
            var coverage = policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey ? SourcingPublicationPolicyRules.AnalyzeCoverage(policy.RulesJson) : policy.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey ? InvoiceMatchingPolicyRules.Validate(policy.RulesJson) : policy.PolicyKey == "RECEIVING" ? ReceivingPolicyRules.Validate(policy.RulesJson) : PolicyRulePreview.AnalyzeCoverage(policy.RulesJson);
            if (coverage.Count != 0)
                return Results.Json(new { code = "coverage-failed",
                    message = "Resolve rule coverage and overlaps before publication.", issues = coverage },
                    statusCode: 422);
            var start = policy.EffectiveFrom;
            var end = policy.EffectiveTo;
            var key = policy.PolicyKey;
            var overlaps = await db.PolicyVersions.Where(x => x.Id != id
                    && x.PolicyKey == key && x.State == "Published"
                    && (end == null || x.EffectiveFrom < end)
                    && (x.EffectiveTo == null || x.EffectiveTo > start)).ToListAsync(ct);
            if (overlaps.Count > 1 || overlaps.Count == 1
                && (overlaps[0].EffectiveTo is not null || overlaps[0].EffectiveFrom >= start))
                return Problem("effective-overlap", "A published policy already covers part of this period.", 409);
            if (overlaps.Count == 1)
            {
                // Do not adopt missing or corrupt historical maker evidence through
                // automatic supersession, or turn a known conflict into a save-time 500.
                if (key == SourcingPublicationPolicyRules.PolicyKey)
                    await VerifySourcingPublishedPolicyIdentity(db, overlaps[0], ct);
                // Close the prior open-ended period in the same transaction. Its original
                // decision and the controlled supersession both remain in the audit trail.
                overlaps[0].EffectiveTo = start;
                AddAudit(db, overlaps[0], scope.SubjectId, "Superseded",
                    $"Superseded by policy version {policy.PolicyVersionNumber}.");
            }
            policy.State = "Published";
            policy.PublishedAt = DateTimeOffset.UtcNow;
            policy.PublishedBy = scope.SubjectId;
            policy.PublicationReason = input.Reason.Trim();
            if (sourcingReviewer is { } reviewer)
                db.AddScoped(SourcingPolicyIdentity.Capture(policy, scope.TenantId, scope.LegalEntityId, "Published", reviewer.Actor.PrincipalId,
                    reviewer.Actor.WorkerId, checked(policy.Version + 1), policy.PublishedAt.Value, reviewer.Origin, reviewer.Submitted));
            AddAudit(db, policy, scope.SubjectId, "Published");
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(View(policy));
        }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy before publication.", 409); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Problem("publication-conflict", "Another policy changed. Refresh and review the effective period.", 409); }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }

    private static async Task<IResult> ReturnForChanges(Guid id, PublicationInput input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
        {
            await RequireSourcingIdentitySchema(http, db, ct);
            _ = await SourcingIndependentPolicyReviewer(db, organizationDb, scope, policy, ct);
            if (!await SourcingActorCoversRules(organizationDb, scope, policy.RulesJson, ct))
                return Problem("sourcing-policy-scope", "Another current authorized person with this policy's scope must return it.", 403);
        }
        if ((policy.PolicyKey is "RECEIVING" or "INVOICE_MATCHING") && !await ReceivingIndependentReviewer(organizationDb, scope, policy, ct))
            return Problem("self-approval", "Another current authorized worker must independently review this receiving policy.", 403);
        if (policy.Version != input.Version)
            return Problem("version-conflict", "Refresh this policy before returning it.", 409);
        if (policy.State != "PendingReview")
            return Problem("policy-locked", "Only a policy waiting for review can be returned.", 409);
        if (policy.CreatedBy == scope.SubjectId || policy.ReviewRequestedBy == scope.SubjectId)
            return Problem("self-approval", "Another authorized person must return this policy.", 403);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Enter a return reason of up to 1,000 characters.", 422);
        policy.State = "Draft";
        AddAudit(db, policy, scope.SubjectId, "ReturnedForChanges", input.Reason.Trim());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy before returning it.", 409); }
        return Results.Ok(View(policy, input.Reason.Trim()));
    }

    private static async Task<IResult?> CheckCompanyRules(PolicyVersion policy,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope,
        CancellationToken ct, string receivingPermission = "procurement-configuration-edit")
    {
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey) return await CheckSourcingCompanyRules(policy, db, organizationDb, scope, receivingPermission, ct);
        if (policy.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey) return await CheckInvoiceMatchingCompanyRules(policy, db, organizationDb, scope, receivingPermission, ct);
        if (policy.PolicyKey == "RECEIVING") return await CheckReceivingCompanyRules(policy, db, organizationDb, scope, receivingPermission, ct);
        var issues = PolicyRuleValidator.Validate(policy.RulesJson);
        if (issues.Count != 0)
            return Results.Json(new { code = "invalid-rules", message = "Correct the buying rules before review.", issues },
                statusCode: 422);
        if (policy.PolicyKey is not ("PURCHASE_REQUEST" or "PURCHASE_ORDER"))
            return Problem("unsupported-policy", "Choose purchase requests or purchase orders for this rule.", 422);
        using var document = JsonDocument.Parse(policy.RulesJson);
        var root = document.RootElement;
        var expectedTransaction = policy.PolicyKey == "PURCHASE_ORDER" ? "PurchaseOrder" : "PurchaseRequest";
        if (root.GetProperty("transactionType").GetString() != expectedTransaction)
            return Problem("policy-mismatch", "The rule document must match the selected buying workflow.", 422);
        var companyCurrency = await organizationDb.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived)
            .Select(x => x.Currency).SingleOrDefaultAsync(ct);
        if (companyCurrency is null)
            return Problem("company-unavailable", "The selected ERP company is unavailable.", 409);
        if (!string.Equals(root.GetProperty("currencyCode").GetString(), companyCurrency,
                StringComparison.OrdinalIgnoreCase))
            return Problem("currency-mismatch", "Use the selected company's currency for this draft.", 422);
        foreach (var rule in root.GetProperty("rules").EnumerateArray())
        {
            var conditions = rule.GetProperty("when");
            foreach (var category in conditions.GetProperty("categoryCodes").EnumerateArray())
            {
                var code = category.GetString()!;
                if (!await db.ProcurementCategories.AsNoTracking()
                        .AnyAsync(x => x.Code == code && x.Status == "Active", ct))
                    return Problem("category-unavailable", $"Activate category {code} before review.", 422);
            }
            foreach (var branch in conditions.GetProperty("branchIds").EnumerateArray())
            {
                var branchId = branch.GetGuid();
                if (!await organizationDb.WorkLocations.AsNoTracking()
                        .AnyAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                            && x.Id == branchId && !x.IsArchived, ct))
                    return Problem("branch-unavailable", "Choose an active branch in this company.", 422);
            }
            var method = rule.GetProperty("result").GetProperty("sourcingMethodCode");
            if (method.ValueKind == JsonValueKind.String)
            {
                var code = method.GetString()!;
                if (!await db.ProcurementMethods.AsNoTracking()
                        .AnyAsync(x => x.Code == code && x.Status == "Active", ct))
                    return Problem("method-unavailable", $"Activate buying method {code} before review.", 422);
            }
        }
        return null;
    }

    private static string? Validate(PolicyDraft input)
    {
        if (string.IsNullOrWhiteSpace(input.PolicyKey) || input.PolicyKey.Trim().Length > 100
            || !input.PolicyKey.Trim().All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-'))
            return "Enter a policy key of up to 100 letters, numbers, dashes or underscores.";
        if (input.EffectiveFrom == default || input.EffectiveTo is { } to && to <= input.EffectiveFrom)
            return "Choose a valid effective period.";
        if (string.IsNullOrWhiteSpace(input.RulesJson) || Encoding.UTF8.GetByteCount(input.RulesJson) > 65536)
            return "Enter a policy draft of no more than 64 KB.";
        try
        {
            using var document = JsonDocument.Parse(input.RulesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "The policy draft must be a JSON object.";
        }
        catch (JsonException) { return "The policy draft is not valid JSON."; }
        return null;
    }

    private static void AddAudit(ProcurementDbContext db, PolicyVersion policy,
        string actor, string action, string? reason = null)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policy.RulesJson)));
        db.AddScoped(new AuditEvent
        {
            RecordType = "PolicyVersion", RecordId = policy.Id, Action = action,
            ActorSubjectId = actor,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                policy.PolicyKey, policy.PolicyVersionNumber, policy.State, policy.EffectiveFrom,
                policy.EffectiveTo, policy.ReviewReason, policy.PublicationReason,
                policy.PublishedAt, policy.PublishedBy, reason, rulesSha256 = hash,
            }),
        });
    }

    private static object View(PolicyVersion x, string? lastReturnReason = null) => new
    {
        x.Id, x.PolicyKey, x.PolicyVersionNumber, x.State, x.EffectiveFrom,
        x.EffectiveTo, x.RulesJson, x.Version, x.CreatedAt, x.UpdatedAt,
        x.ReviewRequestedAt, x.ReviewRequestedBy, x.ReviewReason,
        x.PublishedAt, x.PublishedBy, x.PublicationReason,
        LastReturnReason = lastReturnReason,
    };
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record PolicyDraft(string? PolicyKey, DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo, string? RulesJson);
    public sealed record VersionedPolicyDraft(long Version, PolicyDraft Draft);
    public sealed record PolicyValidationRequest(string? RulesJson);
    public sealed record ReviewSubmission(long Version, string? Reason);
    public sealed record PublicationInput(long Version, string? Reason);
}
