using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Held schema-2 commercial comparison. No AP or Finance effect is
/// produced. Registration requires genuine P31 DDL/readiness and runtime proof.</summary>
public static class ProcurementPrincipalInvoiceComparisonRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record CompareInput(long InvoiceVersion, Guid MappingVersionId,
        Guid ReviewOutcomeId, long ExposureVersion, string? Reason);

    public static void MapRoutes(WebApplication app)
    {
        app.MapPost("/api/procurement/v1/invoices/{id:guid}/principal-matching/comparisons", Compare)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
        app.MapGet("/api/procurement/v1/invoices/{id:guid}/principal-matching/comparisons/commands/{key:guid}", Recover)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
    }

    private static async Task<IResult> Compare(Guid id, CompareInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceComparisonUnavailable" }, statusCode: 503);
        if (input is null || input.InvoiceVersion < 1 || input.MappingVersionId == Guid.Empty
            || input.ReviewOutcomeId == Guid.Empty || input.ExposureVersion < 1
            || input.Reason?.Length > 2000
            || !Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key)
            || key == Guid.Empty)
            return Results.Json(new { code = "PrincipalInvoiceComparisonInputInvalid" }, statusCode: 422);
        var requestSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(
            new { schemaVersion = 2, operation = "Compare", invoiceId = id, input }, Json));
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token)) throw new InvalidOperationException("PrincipalInvoiceComparisonSchemaUnavailable");
            var (invoice, branches) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                db, core, scope, http, auth, id, token);
            var existing = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Operation == "Compare" && x.CommandKey == key, token);
            if (existing is not null)
            {
                if (existing.SupplierInvoiceId != id || existing.ActorPrincipalId != scope.SubjectId
                    || existing.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                    || existing.BranchId != invoice.BranchRef || existing.RequestSha256 != requestSha
                    || existing.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(existing.ResponseJson))
                    throw new InvalidOperationException("PrincipalInvoiceComparisonCommandConflict");
                return JsonSerializer.Deserialize<JsonElement>(existing.ResponseJson, Json);
            }
            var state = await db.PrincipalInvoiceStates.SingleOrDefaultAsync(x => x.SupplierInvoiceId == id, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingRequired");
            var mapping = await db.PrincipalInvoiceMappingVersions.SingleOrDefaultAsync(x =>
                x.Id == input.MappingVersionId && x.SupplierInvoiceId == id, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingChanged");
            if (invoice.Version != input.InvoiceVersion || state.InvoiceVersion != invoice.Version
                || state.CurrentMappingId != mapping.Id || state.MappingRevision != mapping.MappingRevision
                || state.State != "Reviewed"
                || state.ActiveClaimComparisonId is not null || invoice.ActiveMatchClaimResultId is not null
                || invoice.CurrentMappingVersionId is not null || invoice.FinancePostingRef is not null)
                throw new InvalidOperationException("PrincipalInvoiceOriginalOrClaimChanged");
            var outcome = await db.PrincipalInvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x =>
                x.Id == input.ReviewOutcomeId && x.State == "Verified", token)
                ?? throw new InvalidOperationException("PrincipalInvoiceOriginalUnitsNotVerified");
            var review = await db.PrincipalInvoiceUnitReviewSources.SingleAsync(x =>
                x.Id == outcome.ReviewSourceId && x.MappingVersionId == mapping.Id
                    && x.SupplierInvoiceId == id, token);
            var pins = await db.PrincipalInvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == review.Id)
                .ToArrayAsync(token);
            var attestations = await db.PrincipalInvoiceUnitReviewAttestations.Where(x => x.ReviewSourceId == review.Id)
                .ToArrayAsync(token);
            var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleAsync(x => x.Id == review.CoreCaseId && x.SourceId == review.Id
                    && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType, token);
            var decisions = await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToArrayAsync(token);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var policy = await ProcurementPrincipalInvoiceReviewRoutes.CurrentPolicy(db, scope, http,
                InvoiceMatchingPolicyRules.PolicyKey, now, token);
            if (review.MatchingPolicyId != policy.Id)
                throw new InvalidOperationException("PrincipalInvoiceReviewPolicyChanged");
            var reviewPolicy = await db.PolicyVersions.SingleAsync(x => x.Id == review.ReviewPolicyId, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope,
                http.RequestServices, reviewPolicy, token);
            var original = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var mapped = await db.PrincipalInvoiceMappingLines.Where(x => x.MappingVersionId == mapping.Id)
                .ToArrayAsync(token);
            var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                branches, invoice, mapping.PurchaseOrderId, mapping.PrincipalIssuedVersionId, token);
            var snapshot = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, original,
                source.Order, source.Acceptances, source.AcceptanceSourceStates, mapped);
            var opening = PrincipalInvoiceUnitReviewTransitions.Verify(review, mapping, workflow, pins);
            var hashes = await ProcurementPrincipalInvoiceExposureReader.CurrentDocumentHashes(db, scope,
                http.RequestServices, id, opening.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray(), token);
            var verifiedUnits = PrincipalInvoiceUnitReviewTerminal.Verify(outcome, review, mapping,
                workflow, pins, attestations, decisions, policy, reviewPolicy, hashes, now);
            var exposure = await db.PrincipalInvoiceExposureStates.IgnoreQueryFilters().SingleAsync(x =>
                x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.PurchaseOrderId == mapping.PurchaseOrderId
                    && x.PrincipalIssuedVersionId == mapping.PrincipalIssuedVersionId, token);
            if (exposure.Version != input.ExposureVersion || exposure.BranchId != invoice.BranchRef
                || exposure.SupplierId != invoice.SupplierId || exposure.CurrencyCode != invoice.CurrencyCode)
                throw new InvalidOperationException("PrincipalInvoiceExposureChanged");
            var policySha = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson);
            var owned = await ProcurementPrincipalInvoiceExposureReader.ReadLockedAsync(db, cases, scope,
                branches, invoice, exposure, policySha, http.RequestServices, now, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} AND branch_ref={invoice.BranchRef} FOR UPDATE", token);
            var duplicates = await db.SupplierInvoices.IgnoreQueryFilters().AsNoTracking().Where(x =>
                x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.SupplierId == invoice.SupplierId
                    && x.NormalizedInvoiceNumber == invoice.NormalizedInvoiceNumber
                    && x.CurrencyCode == invoice.CurrencyCode)
                .Select(x => new InvoiceDuplicateIdentity(x.Id, x.TenantId, x.LegalEntityId,
                    x.SupplierId, x.NormalizedInvoiceNumber, x.CurrencyCode)).ToArrayAsync(token);
            var duplicateWatermark = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(
                duplicates.OrderBy(x => x.InvoiceId), Json));
            var ownerContext = new InvoiceExposureReadContext(scope.TenantId, scope.LegalEntityId,
                invoice.BranchRef, source.Order.Id, source.Order.RevisionId,
                invoice.SupplierId, invoice.CurrencyCode, policySha, now);
            var preview = PrincipalInvoiceComparisonRules.Evaluate(invoice, original, state, mapping,
                snapshot, source.Order, source.Acceptances, source.AcceptanceSourceStates,
                verifiedUnits, InvoiceMatchingPolicyRules.Project(policy), owned.Owner,
                ownerContext, owned.Consumption,
                new(duplicateWatermark, true, duplicates), now);
            if (!preview.CanClaim || preview.Lines.Count != original.Length)
                throw new InvalidOperationException("PrincipalInvoiceComparisonNeedsResolution");
            var evidence = JsonSerializer.Serialize(new { schemaVersion = 2, invoiceId = id,
                mappingVersionId = mapping.Id, outcomeId = outcome.Id, issuedVersionId = mapping.PrincipalIssuedVersionId,
                policyId = policy.Id, policySha256 = policySha, ownerReference = owned.Owner.OwnerReference,
                ownerWatermark = owned.Owner.Watermark, ownerEvidenceSha256 = owned.Owner.EvidenceSha256,
                consumptionWatermark = owned.Consumption.Watermark,
                duplicatesWatermark = duplicateWatermark, preview.Lines, reason = input.Reason }, Json);
            var comparison = new PrincipalInvoiceComparison
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SupplierInvoiceId = id, MappingVersionId = mapping.Id,
                PurchaseOrderId = mapping.PurchaseOrderId,
                PrincipalIssuedVersionId = mapping.PrincipalIssuedVersionId,
                ReviewSourceId = review.Id, ReviewOutcomeId = outcome.Id,
                MatchingPolicyId = policy.Id, ExposureStateId = exposure.Id,
                InvoiceVersion = invoice.Version, OriginalInvoiceSha256 = mapping.OriginalInvoiceSha256,
                PolicyRulesSha256 = policySha, Outcome = "Compared", EvidenceJson = evidence,
                EvidenceSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(evidence),
                OwnerReference = owned.Owner.OwnerReference!, OwnerWatermark = owned.Owner.Watermark!,
                ActorPrincipalId = scope.SubjectId, ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                CommandKey = key, ComparedAt = now
            };
            db.AddScoped(comparison);
            foreach (var line in preview.Lines)
            {
                var binding = mapped.Single(x => x.SupplierInvoiceLineId == line.InvoiceLineId);
                db.AddScoped(new PrincipalInvoiceClaim
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    SupplierInvoiceId = id, SupplierInvoiceLineId = line.InvoiceLineId,
                    MappingVersionId = mapping.Id, ComparisonId = comparison.Id,
                    ExposureStateId = exposure.Id, PurchaseOrderId = mapping.PurchaseOrderId,
                    PrincipalIssuedVersionId = mapping.PrincipalIssuedVersionId,
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    PrincipalReceivingSourceStateId = binding.PrincipalReceivingSourceStateId,
                    PrincipalAcceptanceFactId = binding.PrincipalAcceptanceFactId,
                    AcceptanceSequence = binding.AcceptanceSequence,
                    SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode,
                    UnitCode = binding.InvoiceUnitCode,
                    Quantity = Exact(line.QuantityExact), NetAmount = Exact(line.NetExact),
                    TaxAmount = Exact(line.TaxExact), ExposureSequence = checked(exposure.Sequence + 1),
                    OwnerCoverageReference = comparison.OwnerReference
                });
            }
            exposure.Sequence = checked(exposure.Sequence + 1);
            exposure.CoverageState = "Verified";
            exposure.OwnerReference = owned.Owner.OwnerReference;
            exposure.OwnerWatermark = owned.Owner.Watermark;
            exposure.OwnerEvidenceSha256 = owned.Owner.EvidenceSha256;
            exposure.OwnerValidUntil = owned.Owner.ValidUntil;
            state.State = "Compared"; state.LastComparisonId = comparison.Id;
            state.ActiveClaimComparisonId = comparison.Id;
            var response = JsonSerializer.Serialize(new { invoiceId = id,
                invoiceVersion = invoice.Version, comparisonId = comparison.Id,
                mappingVersionId = mapping.Id, commandKey = key, state = "Compared",
                sourceSchemaVersion = 2, commercialClaimRecorded = true,
                canPostToFinance = false, financePostingId = (Guid?)null }, Json);
            db.AddScoped(new PrincipalInvoiceCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SupplierInvoiceId = id, Operation = "Compare", CommandKey = key,
                ActorPrincipalId = scope.SubjectId, ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                BranchId = invoice.BranchRef!.Value, RequestSha256 = requestSha,
                ResponseJson = response, ResponseSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(response),
                ResponseStatus = 200, CompletedAt = now
            });
            db.AddScoped(new AuditEvent
            {
                RecordType = nameof(PrincipalInvoiceComparison), RecordId = comparison.Id,
                ActorSubjectId = scope.SubjectId, Action = "PrincipalInvoiceCompared",
                ChangedFieldsJson = JsonSerializer.Serialize(new { invoiceId = id, comparisonId = comparison.Id,
                    commandKey = key, outcome = "Compared", canPostToFinance = false }, Json)
            });
            await db.SaveChangesAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(response, Json);
        }, ct);
    }

    private static decimal Exact(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var result))
            throw new InvalidOperationException("PrincipalInvoiceExactComparisonAmountInvalid");
        return result;
    }

    private static async Task<IResult> Recover(Guid id, Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (key == Guid.Empty || http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>()
                is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceComparisonUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token)) throw new InvalidOperationException("PrincipalInvoiceComparisonSchemaUnavailable");
            var (invoice, branches) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                db, core, scope, http, auth, id, token);
            var receipt = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Operation == "Compare" && x.CommandKey == key
                    && x.SupplierInvoiceId == id, token) ?? throw new KeyNotFoundException();
            if (receipt.ActorPrincipalId != scope.SubjectId
                || receipt.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || receipt.BranchId != invoice.BranchRef || receipt.RequestSha256 is not { Length: 64 }
                || receipt.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson))
                throw new KeyNotFoundException();
            var comparison = await db.PrincipalInvoiceComparisons.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierInvoiceId == id && x.CommandKey == key, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceComparisonReceiptWithoutEffect");
            using var parsed = JsonDocument.Parse(receipt.ResponseJson);
            var response = parsed.RootElement;
            if (comparison.ActorPrincipalId != scope.SubjectId
                || comparison.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || comparison.Outcome != "Compared"
                || !await ProcurementPrincipalInvoiceSourceBridge.QueryBound(db, scope, branches, invoice)
                    .AnyAsync(x => x.Id == comparison.PurchaseOrderId, token)
                || response.GetProperty("invoiceId").GetGuid() != id
                || response.GetProperty("comparisonId").GetGuid() != comparison.Id
                || response.GetProperty("mappingVersionId").GetGuid() != comparison.MappingVersionId
                || response.GetProperty("invoiceVersion").GetInt64() != comparison.InvoiceVersion
                || response.GetProperty("commandKey").GetGuid() != key
                || response.GetProperty("sourceSchemaVersion").GetInt32() != 2
                || response.GetProperty("canPostToFinance").GetBoolean())
                throw new InvalidOperationException("PrincipalInvoiceComparisonReceiptChanged");
            return JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson, Json);
        }, ct);
    }
}
