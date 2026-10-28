using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
            catch (OperationalIdentityProblem problem) { return Problem(problem.Code, problem.Message, problem.Status); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("OperationalPolicy", StringComparison.Ordinal))
            { return OperationalIdentityFailure(error); }
        });
        group.MapGet("", List).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/effective", Effective).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/lookups/sourcing-reviewers", SourcingReviewers).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/lookups/receiving/{kind}", ReceivingPolicyLookup).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/lookups/invoice-unit-reviewers", UnitReviewers).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/lookups/invoice-owner-baseline-reviewers", InvoiceOwnerBaselineReviewers).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-configuration-read");
        group.MapPost("", Create).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/validate", ValidateRules).RequireAuthorization("procurement-configuration-edit");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/retire", Retire).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/preview", Preview).RequireAuthorization("procurement-configuration-read");
        group.MapPost("/{id:guid}/submit-review", SubmitReview).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/publish", Publish).RequireAuthorization("procurement-configuration-approve");
        group.MapPost("/{id:guid}/return-for-changes", ReturnForChanges).RequireAuthorization("procurement-configuration-approve");
    }

    private static IResult ValidateRules(PolicyValidationRequest input)
    {
        if (ManualPaymentReviewPolicyRules.IsDocument(input.RulesJson))
        {
            var draftIssues = ManualPaymentReviewPolicyRules.Validate(input.RulesJson, draft: true);
            var paymentReviewIssues = ManualPaymentReviewPolicyRules.Validate(input.RulesJson);
            return Results.Ok(new { isValid = draftIssues.Count == 0, issues = draftIssues,
                reviewReady = paymentReviewIssues.Count == 0, reviewIssues = paymentReviewIssues });
        }
        if (SourcingPublicationPolicyRules.IsSourcingDocument(input.RulesJson))
        {
            var syntax = SourcingPublicationPolicyRules.ValidateDraft(input.RulesJson);
            var review = SourcingPublicationPolicyRules.Validate(input.RulesJson);
            return Results.Ok(new { isValid = syntax.Count == 0, issues = syntax,
                reviewConfigurationComplete = syntax.Count == 0 && review.Count == 0, reviewIssues = review,
                supportedSourcingRuleSchemas = SourcingPublicationPolicyRules.SupportedSchemaVersions,
                canPublish = false, isAuthority = false });
        }
        if (InvoiceUnitReviewPolicyRules.IsDocument(input.RulesJson))
            return Results.Ok(new { isValid = InvoiceUnitReviewPolicyRules.Validate(input.RulesJson, true).Count == 0,
                issues = InvoiceUnitReviewPolicyRules.Validate(input.RulesJson, true), reviewIssues = InvoiceUnitReviewPolicyRules.Validate(input.RulesJson),
                reviewConfigurationComplete = InvoiceUnitReviewPolicyRules.Validate(input.RulesJson).Count == 0, canPublish = false, isAuthority = false });
        if (InvoiceProcurementBaselineReviewPolicyRules.IsDocument(input.RulesJson))
            return Results.Ok(new { isValid = InvoiceProcurementBaselineReviewPolicyRules.Validate(input.RulesJson).Count == 0,
                issues = InvoiceProcurementBaselineReviewPolicyRules.Validate(input.RulesJson),
                canPublish = false, isAuthority = false });
        var issues = InvoiceMatchingPolicyRules.IsMatchingDocument(input.RulesJson) ? InvoiceMatchingPolicyRules.Validate(input.RulesJson) : ReceivingPolicyRules.IsReceivingDocument(input.RulesJson) ? (PrincipalReceivingPolicyRules.IsDocument(input.RulesJson) ? PrincipalReceivingPolicyRules.Validate(input.RulesJson) : ReceivingPolicyRules.Validate(input.RulesJson)) : PolicyRuleValidator.Validate(input.RulesJson);
        return Results.Ok(new { isValid = issues.Count == 0, issues });
    }

    internal static IReadOnlyList<PolicyRuleIssue> PublicationIssues(PolicyVersion policy) => policy.PolicyKey switch
    {
        ManualPaymentReviewPolicyRules.PolicyKey => ManualPaymentReviewPolicyRules.Validate(policy.RulesJson),
        InvoiceRetainedUnitReview.ReviewPolicyKey => InvoiceUnitReviewPolicyRules.Validate(policy.RulesJson)
            .Select(message => new PolicyRuleIssue("stages", message)).ToArray(),
        SourcingPublicationPolicyRules.PolicyKey => SourcingPublicationPolicyRules.AnalyzeCoverage(policy.RulesJson),
        InvoiceMatchingPolicyRules.PolicyKey => InvoiceMatchingPolicyRules.Validate(policy.RulesJson),
        InvoiceProcurementBaselineReviewPolicyRules.PolicyKey => InvoiceProcurementBaselineReviewPolicyRules.Validate(policy.RulesJson),
        "RECEIVING" => PrincipalReceivingPolicyRules.IsDocument(policy.RulesJson) ? PrincipalReceivingPolicyRules.Validate(policy.RulesJson) : ReceivingPolicyRules.Validate(policy.RulesJson),
        _ => PolicyRulePreview.AnalyzeCoverage(policy.RulesJson),
    };

    private static async Task<IResult> Preview(Guid id, PolicyScenario scenario,
        ProcurementDbContext db, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if (policy.PolicyKey == ManualPaymentReviewPolicyRules.PolicyKey) return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "PaymentReviewRequiresInvoiceAndIndependentCase", issues = ManualPaymentReviewPolicyRules.Validate(policy.RulesJson) });
        if (policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey) return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "InvoiceUnitReviewRequiresOriginalDocumentAndNamedIndependentCase", issues = InvoiceUnitReviewPolicyRules.Validate(policy.RulesJson) });
        if (policy.PolicyKey == InvoiceProcurementBaselineReviewPolicyRules.PolicyKey) return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "InvoiceOwnerBaselineRequiresCompleteInventoryAndIndependentCoreCase", issues = InvoiceProcurementBaselineReviewPolicyRules.Validate(policy.RulesJson) });
        if (policy.PolicyKey == "RECEIVING") return Results.Ok(new { policyId = policy.Id, isAuthority = false,
            status = "ReceivingPreviewRequiresSealedSource", issues = PrincipalReceivingPolicyRules.IsDocument(policy.RulesJson) ? PrincipalReceivingPolicyRules.Validate(policy.RulesJson) : ReceivingPolicyRules.Validate(policy.RulesJson) });
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
        if (normalized is not ("PURCHASE_REQUEST" or "PURCHASE_ORDER" or "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "SOURCING" or "MANUAL_PAYMENT_REVIEW" or "INVOICE_OWNER_BASELINE_REVIEW"))
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

        if (IsOperationalIdentityPolicy(normalized))
        {
            if (normalized == ManualPaymentReviewPolicyRules.PolicyKey
                && (http.RequestServices.GetService<IProcurementManualPaymentGovernanceReadiness>() is not { } payments
                    || !await payments.IsReadyAsync(ct)))
                return Problem("payment-review-unavailable", "Payment review settings are not ready in this workspace yet.", 503);
            await RequireOperationalIdentitySchema(http, db, false, ct);
            if (normalized == InvoiceRetainedUnitReview.ReviewPolicyKey) await RequireUnitReviewPolicySchema(http, db, ct);
            var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == normalized && x.State == "Published"
                && x.EffectiveFrom <= instant && (x.EffectiveTo == null || x.EffectiveTo > instant))
                .OrderByDescending(x => x.PolicyVersionNumber).Take(2).ToArrayAsync(ct);
            if (policies.Length == 0) return Results.Ok(new { status = "MissingPolicy", isAuthority = false, policy = (object?)null, evaluatedAt = instant });
            if (policies.Length > 1) return Results.Ok(new { status = "AmbiguousPolicy", isAuthority = false, policy = (object?)null, evaluatedAt = instant });
            var current = policies[0];
            _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db,
                http.RequestServices.GetRequiredService<ProcurementRequestScope>(), current, ct);
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

    private static async Task<IResult> List(ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, string? key,
        int? page, int? pageSize, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
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
        var ids = items.Select(x => x.Id).ToArray();
        var canEdit = (await authorization.AuthorizeAsync(http.User, "procurement-configuration-edit")).Succeeded;
        var eligible = new List<Guid>();
        if (canEdit)
        {
            var candidates = await RetirableDrafts(db).AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
            foreach (var candidate in candidates)
                if (await CanRetirePolicyAuthorHint(candidate, db, organizationDb, scope, http, ct)) eligible.Add(candidate.Id);
        }
        return Results.Ok(new { items = items.Select(x => new { x.Id, x.PolicyKey, x.PolicyVersionNumber,
            x.State, x.EffectiveFrom, x.EffectiveTo, x.Version, x.CreatedAt,
            draftRetirementVersion = 1, canRetireDraft = eligible.Contains(x.Id) }),
            total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService authorization, CancellationToken ct)
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
        var canRetire = (await authorization.AuthorizeAsync(http.User, "procurement-configuration-edit")).Succeeded
            && await RetirableDrafts(db).AnyAsync(x => x.Id == id, ct)
            && await CanRetirePolicyAuthorHint(policy, db, organizationDb, scope, http, ct);
        return Results.Ok(View(policy, lastReturnReason, canRetire));
    }

    private static async Task<IResult> CreatePolicy(PolicyDraft input, ProcurementDbContext db,
        HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        var issue = Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var key = input.PolicyKey!.Trim().ToUpperInvariant();
        if (key == SourcingPublicationPolicyRules.PolicyKey) await RequireSourcingIdentitySchema(http, db, ct);
        if (key == InvoiceRetainedUnitReview.ReviewPolicyKey && InvoiceUnitReviewPolicyRules.Validate(input.RulesJson!, true).Count != 0)
            return Problem("invalid-unit-review-draft", "Use the explicit original-unit review policy format.", 422);
        if ((key is "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "INVOICE_OWNER_BASELINE_REVIEW") && !await ReceivingConfigurationActor(organizationDb, scope, "procurement-configuration-edit", ct))
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
            var sourcingIssue = await CheckSourcingCompanyRules(policy, db, organizationDb, scope, "procurement-configuration-edit", ct, draftSyntaxOnly: true);
            if (sourcingIssue is not null) return sourcingIssue;
        }
        if (key == InvoiceProcurementBaselineReviewPolicyRules.PolicyKey)
        {
            var baselineIssue = await CheckInvoiceOwnerBaselineCompanyRules(policy, organizationDb,
                scope, "procurement-configuration-edit", ct);
            if (baselineIssue is not null) return baselineIssue;
        }
        ProcurementPrincipalPeople.Person? operationalCreator = null;
        if (IsOperationalIdentityPolicy(key))
        {
            operationalCreator = await ProcurementOperationalPolicyWorkerIdentity.CurrentActorAsync(organizationDb, scope,
                ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
            policy.CreatedBy = operationalCreator.PrincipalId;
            policy.CreatedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        }
        db.AddScoped(policy);
        if (key == SourcingPublicationPolicyRules.PolicyKey)
        {
            var actor = await SourcingCurrentActor(organizationDb, scope, "procurement-configuration-edit", ct);
            using var rules = JsonDocument.Parse(policy.RulesJson);
            var schemaVersion = rules.RootElement.GetProperty("schemaVersion").GetInt32();
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, scope.TenantId, scope.LegalEntityId, "Created", actor.PrincipalId,
                actor.EvidenceWorker(schemaVersion), 1, DateTimeOffset.UtcNow, schemaVersion: schemaVersion));
        }
        if (operationalCreator is not null)
            db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Created",
                operationalCreator.PrincipalId, operationalCreator.EvidenceWorker(2), 1, policy.CreatedAt, schemaVersion: 2)));
        AddAudit(db, policy, scope.SubjectId, "DraftCreated");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return Problem("version-conflict", "Another policy draft was added. Refresh and try again.", 409); }
        return Results.Created($"/api/procurement/v1/configuration/policies/{policy.Id}", View(policy));
    }

    private static async Task<IResult> UpdatePolicy(Guid id, VersionedPolicyDraft input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if ((policy.PolicyKey is "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "INVOICE_OWNER_BASELINE_REVIEW") && !await ReceivingConfigurationActor(organizationDb, scope, "procurement-configuration-edit", ct))
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
            var sourcingIssue = await CheckSourcingCompanyRules(new PolicyVersion { Id = policy.Id,
                PolicyKey = policy.PolicyKey, RulesJson = input.Draft.RulesJson! },
                db, organizationDb, scope, "procurement-configuration-edit", ct, draftSyntaxOnly: true);
            if (sourcingIssue is not null) return sourcingIssue;
        }
        if (policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey && InvoiceUnitReviewPolicyRules.Validate(input.Draft.RulesJson!, true).Count != 0)
            return Problem("invalid-unit-review-draft", "Use the explicit original-unit review policy format.", 422);
        if (policy.PolicyKey == InvoiceProcurementBaselineReviewPolicyRules.PolicyKey)
        {
            var baselineIssue = await CheckInvoiceOwnerBaselineCompanyRules(new PolicyVersion
                { PolicyKey = policy.PolicyKey, RulesJson = input.Draft.RulesJson! },
                organizationDb, scope, "procurement-configuration-edit", ct);
            if (baselineIssue is not null) return baselineIssue;
        }
        if (IsOperationalIdentityPolicy(policy.PolicyKey))
            _ = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(organizationDb, db, scope, policy,
                ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
        policy.EffectiveFrom = input.Draft.EffectiveFrom.ToUniversalTime();
        policy.EffectiveTo = input.Draft.EffectiveTo?.ToUniversalTime();
        policy.RulesJson = input.Draft.RulesJson!.Trim();
        AddAudit(db, policy, scope.SubjectId, "DraftUpdated");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy draft before saving.", 409); }
        return Results.Ok(View(policy));
    }

    // A draft with any review/publication history is retained for controlled correction.
    // All reference queries include inactive/historical rows and the existing scope filters.
    private static IQueryable<PolicyVersion> RetirableDrafts(ProcurementDbContext db) =>
        db.PolicyVersions.Where(x => x.State == "Draft"
            && x.ReviewRequestedAt == null && x.ReviewRequestedBy == null && x.ReviewReason == null
            && x.PublishedAt == null && x.PublishedBy == null && x.PublicationReason == null
            && !db.AuditEvents.Any(a => a.RecordType == "PolicyVersion" && a.RecordId == x.Id
                && a.Action != "DraftCreated" && a.Action != "DraftUpdated"
                && a.Action != "create" && a.Action != "update")
            && !db.PolicyEvaluations.Any(r => r.PolicyVersionId == x.Id)
            && !db.ProcurementMethods.Any(r => r.PolicyVersionId == x.Id)
            && !db.Suppliers.Any(r => r.StatusPolicyVersionId == x.Id)
            && !db.SupplierDecisions.Any(r => r.PolicyVersionId == x.Id)
            && !db.SourcingEvents.Any(r => r.PolicyVersionId == x.Id)
            && !db.InvoiceMatchResults.Any(r => r.PolicyVersionId == x.Id)
            && !db.ApprovalSubmissions.Any(r => r.PolicyVersionId == x.Id)
            && !db.OrderSubmissions.Any(r => r.PolicyVersionId == x.Id)
            && !db.ReceivingSourceStates.Any(r => r.ClassificationPolicyVersionId == x.Id)
            && !db.GoodsReceiptLines.Any(r => r.PhysicalPolicyVersionId == x.Id)
            && !db.ReceiptInspections.Any(r => r.PolicyVersionId == x.Id)
            && !db.ServiceAcceptances.Any(r => r.PolicyVersionId == x.Id)
            && !db.ReceivingAcceptanceEffects.Any(r => r.ReceivingPolicyVersionId == x.Id)
            // Created identity is retained authorship, not downstream use. Any later
            // or unknown stage is review history and prevents draft retirement.
            && !db.OperationalPolicyIdentityRecords.Any(r => r.PolicyVersionId == x.Id && r.Stage != "Created")
            && !db.SourcingPolicyIdentityEvidenceRecords.Any(r => r.PolicyVersionId == x.Id && r.Stage != "Created"));

    private static async Task<IResult> Retire(Guid id, DraftRetirement input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational())
                transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
            if ((policy.PolicyKey is "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "INVOICE_OWNER_BASELINE_REVIEW")
                && !await ReceivingConfigurationActor(organizationDb, scope, "procurement-configuration-edit", ct))
                return Problem("receiving-authority-unavailable", "Your current account cannot configure receiving rules.", 403);
            if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            {
                await RequireSourcingIdentitySchema(http, db, ct);
                _ = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
            }
            if (IsOperationalIdentityPolicy(policy.PolicyKey))
            {
                await RequireOperationalIdentitySchema(http, db, true, ct);
                if (policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey) await RequireUnitReviewPolicySchema(http, db, ct);
                _ = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(organizationDb, db, scope, policy,
                    ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
            }
            if (policy.Version != input.Version)
                return Problem("version-conflict", "Refresh this policy before retiring it.", 409);
            if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
                return Problem("validation-failed", "Enter a retirement reason of up to 1,000 characters.", 422);
            if (!await RetirableDrafts(db).AnyAsync(x => x.Id == id, ct))
                return Problem("policy-retained", "Only an unused draft with no review or publication history can be retired.", 409);
            policy.State = "Retired";
            AddAudit(db, policy, scope.SubjectId, "DraftRetired", input.Reason.Trim());
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(View(policy));
        }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy before retiring it.", 409); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "40001" or "40P01" })
        { return Problem("version-conflict", "Another setting changed. Refresh before saving.", 409); }
        catch (Exception error) when (error.GetBaseException() is PostgresException { SqlState: "40001" or "40P01" })
        { return Problem("version-conflict", "Another setting changed. Refresh before retiring this draft.", 409); }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }

    private static async Task<IResult> SubmitReviewPolicy(Guid id, ReviewSubmission input,
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
        (SourcingPolicyIdentityEvidence Origin, ProcurementPrincipalPeople.Person Actor)? sourcingAuthor = null;
        if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
        {
            await RequireSourcingIdentitySchema(http, db, ct);
            sourcingAuthor = await SourcingOriginalAuthor(db, organizationDb, scope, policy, ct);
        }
        ProcurementOperationalPolicyWorkerIdentity.AuthorProof? operationalAuthor = null;
        if (IsOperationalIdentityPolicy(policy.PolicyKey))
            operationalAuthor = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(organizationDb, db, scope, policy,
                ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, ct);
        var referenceIssue = await CheckCompanyRules(policy, db, organizationDb, scope, ct);
        if (referenceIssue is not null) return referenceIssue;
        policy.State = "PendingReview";
        policy.ReviewRequestedAt = DateTimeOffset.UtcNow;
        policy.ReviewRequestedBy = scope.SubjectId;
        policy.ReviewReason = input.Reason.Trim();
        if (sourcingAuthor is { } author)
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, scope.TenantId, scope.LegalEntityId, "Submitted", author.Actor.PrincipalId,
                author.Actor.EvidenceWorker(author.Origin.SchemaVersion), checked(policy.Version + 1), policy.ReviewRequestedAt.Value,
                author.Origin, schemaVersion: author.Origin.SchemaVersion));
        if (operationalAuthor is not null)
            db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Submitted",
                operationalAuthor.Actor.PrincipalId, operationalAuthor.Actor.EvidenceWorker(operationalAuthor.Created.SchemaVersion), checked(policy.Version + 1),
                policy.ReviewRequestedAt.Value, operationalAuthor.Created, schemaVersion: operationalAuthor.Created.SchemaVersion)));
        AddAudit(db, policy, scope.SubjectId, "ReviewRequested");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy draft before sending it for review.", 409); }
        return Results.Ok(View(policy));
    }

    private static async Task<IResult> PublishPolicy(Guid id, PublicationInput input,
        ProcurementDbContext db, HrmDbContext organizationDb, ProcurementRequestScope scope,
        HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Enter a publication reason of up to 1,000 characters.", 422);
        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
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
            (SourcingPolicyIdentityEvidence Origin, SourcingPolicyIdentityEvidence Submitted, ProcurementPrincipalPeople.Person Actor)? sourcingReviewer = null;
            if (policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey)
            {
                await RequireSourcingIdentitySchema(http, db, ct);
                sourcingReviewer = await SourcingIndependentPolicyReviewer(db, organizationDb, scope, policy, ct);
            }
            ProcurementOperationalPolicyWorkerIdentity.ReviewProof? operationalReviewer = null;
            if (IsOperationalIdentityPolicy(policy.PolicyKey))
                operationalReviewer = await ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(organizationDb, db, scope, policy,
                    ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, ct);
            var coverage = PublicationIssues(policy);
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
                if (IsOperationalIdentityPolicy(key))
                    _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, overlaps[0], ct);
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
                    reviewer.Actor.EvidenceWorker(reviewer.Origin.SchemaVersion), checked(policy.Version + 1), policy.PublishedAt.Value,
                    reviewer.Origin, reviewer.Submitted, reviewer.Origin.SchemaVersion));
            if (operationalReviewer is not null)
                db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Published",
                    operationalReviewer.Actor.PrincipalId, operationalReviewer.Actor.EvidenceWorker(operationalReviewer.Created.SchemaVersion), checked(policy.Version + 1),
                    policy.PublishedAt.Value, operationalReviewer.Created, operationalReviewer.Submitted, operationalReviewer.Created.SchemaVersion)));
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

    private static async Task<IResult> ReturnForChangesPolicy(Guid id, PublicationInput input,
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
        if (IsOperationalIdentityPolicy(policy.PolicyKey))
            _ = await ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(organizationDb, db, scope, policy,
                ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, ct);
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
        if (policy.PolicyKey == ManualPaymentReviewPolicyRules.PolicyKey)
        {
            var paymentIssues = ManualPaymentReviewPolicyRules.Validate(policy.RulesJson);
            if (paymentIssues.Count > 0) return Results.Json(new { code = "invalid-rules", message = "Set the payment review limits before continuing.", issues = paymentIssues }, statusCode: 422);
            if (!await organizationDb.LegalEntities.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                    && x.Id == scope.LegalEntityId && !x.IsArchived, ct))
                return Problem("company-unavailable", "The selected ERP company is unavailable.", 409);
            return null;
        }
        if (policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey) return await CheckUnitReviewCompanyRules(policy, organizationDb, scope, receivingPermission, ct);
        if (policy.PolicyKey == InvoiceProcurementBaselineReviewPolicyRules.PolicyKey) return await CheckInvoiceOwnerBaselineCompanyRules(policy, organizationDb, scope, receivingPermission, ct);
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

    private static object View(PolicyVersion x, string? lastReturnReason = null, bool canRetireDraft = false) => new
    {
        x.Id, x.PolicyKey, x.PolicyVersionNumber, x.State, x.EffectiveFrom,
        x.EffectiveTo, x.RulesJson, x.Version, x.CreatedAt, x.UpdatedAt,
        x.ReviewRequestedAt, x.ReviewRequestedBy, x.ReviewReason,
        x.PublishedAt, x.PublishedBy, x.PublicationReason,
        LastReturnReason = lastReturnReason, DraftRetirementVersion = 1, CanRetireDraft = canRetireDraft,
    };
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record PolicyDraft(string? PolicyKey, DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo, string? RulesJson);
    public sealed record VersionedPolicyDraft(long Version, PolicyDraft Draft);
    public sealed record PolicyValidationRequest(string? RulesJson);
    public sealed record ReviewSubmission(long Version, string? Reason);
    public sealed record DraftRetirement(long Version, string? Reason);
    public sealed record PublicationInput(long Version, string? Reason);
}
