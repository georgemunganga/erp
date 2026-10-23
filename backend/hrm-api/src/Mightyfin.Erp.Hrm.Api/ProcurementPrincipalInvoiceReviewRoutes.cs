using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Held schema-2 Core original-unit review. Deliberately not mapped by
/// Program or the module host until P31 DDL, documents and finite roles pass.</summary>
public static partial class ProcurementPrincipalInvoiceReviewRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record OpenInput(long InvoiceVersion, int MappingRevision, string? Reason);
    public sealed record DecisionInput(long InvoiceVersion, long CaseVersion,
        long AssignmentVersion, string Action, string? Reason, string? Attestation);

    public static void MapRoutes(WebApplication app)
    {
        app.MapPost("/api/procurement/v1/invoices/{id:guid}/principal-unit-reviews", Open)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match",
                InvoiceUnitReviewTransitions.RequestPermission);
        app.MapPost("/api/procurement/v1/principal-unit-reviews/{id:guid}/assignments/{assignmentId:guid}/decisions", Decide)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        app.MapGet("/api/procurement/v1/principal-unit-reviews/{id:guid}/commands/{key:guid}", Recover)
            .RequireAuthorization("procurement-access");
        app.MapGet("/api/procurement/v1/principal-unit-reviews/tasks", Tasks)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        app.MapGet("/api/procurement/v1/principal-unit-reviews/{id:guid}", Detail)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        app.MapGet("/api/procurement/v1/principal-unit-reviews/{id:guid}/documents", Documents)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        app.MapGet("/api/procurement/v1/principal-unit-reviews/{id:guid}/documents/{versionId:guid}/download", Download)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
    }

    private static async Task<IResult> Open(Guid id, OpenInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        if (input is null || input.InvoiceVersion < 1 || input.MappingRevision < 1
            || !Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key)
            || key == Guid.Empty || input.Reason?.Length > 2000)
            return Results.Json(new { code = "PrincipalInvoiceReviewInputInvalid" }, statusCode: 422);
        var requestSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(
            new { schemaVersion = 2, operation = "OpenReview", invoiceId = id, input }, Json));
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token)) throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
            var (invoice, branches) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                db, core, scope, http, auth, id, token);
            var maker = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token) ?? throw new UnauthorizedAccessException();
            if (maker.WorkerId != scope.CoreLinkedWorkerId
                || !maker.Permissions.Contains(InvoiceUnitReviewTransitions.RequestPermission)
                || !(await auth.AuthorizeAsync(http.User, InvoiceUnitReviewTransitions.RequestPermission)).Succeeded)
                throw new UnauthorizedAccessException();
            var existing = await db.PrincipalInvoiceCommandReceipts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Operation == "OpenReview" && x.CommandKey == key, token);
            if (existing is not null)
            {
                if (existing.SupplierInvoiceId != id || existing.ActorPrincipalId != scope.SubjectId
                    || existing.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                    || existing.BranchId != invoice.BranchRef || existing.RequestSha256 != requestSha
                    || existing.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(existing.ResponseJson))
                    throw new InvalidOperationException("PrincipalInvoiceReviewCommandConflict");
                return JsonSerializer.Deserialize<JsonElement>(existing.ResponseJson, Json);
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} AND branch_ref={invoice.BranchRef} FOR UPDATE", token);
            if (invoice.Version != input.InvoiceVersion || invoice.FinancePostingRef is not null
                || invoice.ActiveMatchClaimResultId is not null || invoice.CurrentMappingVersionId is not null)
                throw new InvalidOperationException("PrincipalInvoiceOriginalChanged");
            var state = await db.PrincipalInvoiceStates.SingleOrDefaultAsync(x => x.SupplierInvoiceId == id, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingRequired");
            var mapping = await db.PrincipalInvoiceMappingVersions.SingleOrDefaultAsync(
                x => x.Id == state.CurrentMappingId && x.SupplierInvoiceId == id, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingChanged");
            if (state.InvoiceVersion != input.InvoiceVersion || state.MappingRevision != input.MappingRevision
                || await db.PrincipalInvoiceUnitReviewSources.AnyAsync(x => x.MappingVersionId == mapping.Id, token))
                throw new InvalidOperationException("PrincipalInvoiceReviewAlreadyOpenedOrChanged");
            var normalized = await db.PrincipalInvoiceMappingLines.Where(x => x.MappingVersionId == mapping.Id)
                .ToArrayAsync(token);
            var originals = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                branches, invoice, mapping.PurchaseOrderId, mapping.PrincipalIssuedVersionId, token);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var matching = await CurrentPolicy(db, scope, http, InvoiceMatchingPolicyRules.PolicyKey, now, token);
            var review = await CurrentPolicy(db, scope, http, InvoiceRetainedUnitReview.ReviewPolicyKey, now, token);
            var reviewers = new List<InvoiceUnitReviewPolicyRules.Person>();
            foreach (var principal in InvoiceUnitReviewPolicyRules.Parse(review.RulesJson).Stages
                .SelectMany(x => x.Principals).Distinct(StringComparer.Ordinal))
            {
                var person = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principal, token)
                    ?? throw new InvalidOperationException("PrincipalInvoiceReviewerUnavailable");
                if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        maker.PrincipalId, maker.WorkerId, person.PrincipalId, person.WorkerId, token))
                    throw new InvalidOperationException("PrincipalInvoiceIndependentReviewerRequired");
                reviewers.Add(person);
            }
            var hashes = await ProcurementPrincipalInvoiceExposureReader.CurrentDocumentHashes(db,
                scope, http.RequestServices, id, normalized.Select(x => x.DocumentVersionId).Distinct().ToArray(), token);
            var plan = PrincipalInvoiceUnitReviewTransitions.PrepareOpen(invoice, originals, state, mapping,
                normalized, source.Order, source.Acceptances, source.AcceptanceSourceStates,
                matching, review, maker, reviewers, hashes, now);
            cases.AddCase(plan.Case);
            await cases.SaveChangesAsync(token);
            db.AddScoped(plan.Source);
            foreach (var pin in plan.Pins) db.AddScoped(pin);
            var response = JsonSerializer.Serialize(new { invoiceId = id, invoiceVersion = invoice.Version,
                reviewId = plan.Source.Id,
                coreCaseId = plan.Case.Id, commandKey = key, state = "Pending", sourceSchemaVersion = 2,
                isUnitVerified = false, canCompare = false, canPostToFinance = false }, Json);
            db.AddScoped(new PrincipalInvoiceCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, SupplierInvoiceId = id,
                Operation = "OpenReview", CommandKey = key, ActorPrincipalId = scope.SubjectId,
                ActorWorkerAliasId = scope.CoreLinkedWorkerId, BranchId = invoice.BranchRef!.Value,
                RequestSha256 = requestSha, ResponseJson = response,
                ResponseSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(response),
                ResponseStatus = 200, CompletedAt = now
            });
            await db.SaveChangesAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(response, Json);
        }, ct);
    }

    internal static async Task<PolicyVersion> CurrentPolicy(ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, string policyKey, DateTimeOffset now,
        CancellationToken ct)
    {
        var rows = await db.PolicyVersions.Where(x => x.PolicyKey == policyKey
            && x.State == "Published" && x.EffectiveFrom <= now
            && (x.EffectiveTo == null || now < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("PrincipalInvoicePublishedPolicyUnavailable");
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope,
            http.RequestServices, rows[0], ct);
        return rows[0];
    }
}
