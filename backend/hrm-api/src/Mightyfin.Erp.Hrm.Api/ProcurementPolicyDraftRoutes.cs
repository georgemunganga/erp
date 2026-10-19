using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Company-scoped policy authoring. Drafts are never effective purchase authority.</summary>
public static class ProcurementPolicyDraftRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/configuration/policies");
        group.MapGet("", List).RequireAuthorization("procurement-configuration-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-configuration-read");
        group.MapPost("", Create).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/validate", ValidateRules).RequireAuthorization("procurement-configuration-edit");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-configuration-edit");
        group.MapPost("/{id:guid}/submit-review", SubmitReview).RequireAuthorization("procurement-configuration-edit");
    }

    private static IResult ValidateRules(PolicyValidationRequest input)
    {
        var issues = PolicyRuleValidator.Validate(input.RulesJson);
        return Results.Ok(new { isValid = issues.Count == 0, issues });
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
        return policy is null ? Problem("not-found", "Buying policy not found.", 404) : Results.Ok(View(policy));
    }

    private static async Task<IResult> Create(PolicyDraft input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var issue = Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var key = input.PolicyKey!.Trim().ToUpperInvariant();
        var next = (await db.PolicyVersions.AsNoTracking()
            .Where(x => x.PolicyKey == key).MaxAsync(x => (int?)x.PolicyVersionNumber, ct) ?? 0) + 1;
        var policy = new PolicyVersion
        {
            PolicyKey = key, PolicyVersionNumber = next, State = "Draft",
            EffectiveFrom = input.EffectiveFrom.ToUniversalTime(),
            EffectiveTo = input.EffectiveTo?.ToUniversalTime(),
            RulesJson = input.RulesJson!.Trim(),
        };
        db.AddScoped(policy);
        AddAudit(db, policy, scope.SubjectId, "DraftCreated");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return Problem("version-conflict", "Another policy draft was added. Refresh and try again.", 409); }
        return Results.Created($"/api/procurement/v1/configuration/policies/{policy.Id}", View(policy));
    }

    private static async Task<IResult> Update(Guid id, VersionedPolicyDraft input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (policy is null) return Problem("not-found", "Buying policy not found.", 404);
        if (policy.Version != input.Version)
            return Problem("version-conflict", "Refresh this policy draft before saving.", 409);
        if (policy.State != "Draft")
            return Problem("policy-locked", "Only a draft policy can be changed.", 409);
        var issue = Validate(input.Draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (!string.Equals(policy.PolicyKey, input.Draft.PolicyKey!.Trim(), StringComparison.OrdinalIgnoreCase))
            return Problem("key-locked", "A policy key cannot be changed. Start another draft.", 409);
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
        CancellationToken ct)
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
        policy.State = "PendingReview";
        policy.ReviewRequestedAt = DateTimeOffset.UtcNow;
        policy.ReviewRequestedBy = scope.SubjectId;
        policy.ReviewReason = input.Reason.Trim();
        AddAudit(db, policy, scope.SubjectId, "ReviewRequested");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this policy draft before sending it for review.", 409); }
        return Results.Ok(View(policy));
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
        string actor, string action)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policy.RulesJson)));
        db.AddScoped(new AuditEvent
        {
            RecordType = "PolicyVersion", RecordId = policy.Id, Action = action,
            ActorSubjectId = actor,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                policy.PolicyKey, policy.PolicyVersionNumber, policy.EffectiveFrom,
                policy.EffectiveTo, rulesSha256 = hash,
            }),
        });
    }

    private static object View(PolicyVersion x) => new
    {
        x.Id, x.PolicyKey, x.PolicyVersionNumber, x.State, x.EffectiveFrom,
        x.EffectiveTo, x.RulesJson, x.Version, x.CreatedAt, x.UpdatedAt,
        x.ReviewRequestedAt, x.ReviewRequestedBy, x.ReviewReason,
        x.PublishedAt, x.PublishedBy, x.PublicationReason,
    };
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record PolicyDraft(string? PolicyKey, DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo, string? RulesJson);
    public sealed record VersionedPolicyDraft(long Version, PolicyDraft Draft);
    public sealed record PolicyValidationRequest(string? RulesJson);
    public sealed record ReviewSubmission(long Version, string? Reason);
}
