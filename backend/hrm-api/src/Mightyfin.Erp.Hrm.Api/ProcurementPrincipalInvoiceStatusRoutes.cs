using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Purpose-scoped current typed invoice pointers for the held P31 UI.
/// A status is never a comparison, reviewer decision or Finance permission.</summary>
public static class ProcurementPrincipalInvoiceStatusRoutes
{
    public static void MapRoutes(WebApplication app)
        => app.MapGet("/api/procurement/v1/invoices/{id:guid}/principal-matching/status", Status)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");

    private static async Task<IResult> Status(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceMatchingUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadReadyAsync(db, token))
                throw new InvalidOperationException("PrincipalInvoiceMatchingSchemaUnavailable");
            var (invoice, branches) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                db, core, scope, http, auth, id, token);
            var state = await db.PrincipalInvoiceStates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierInvoiceId == id, token);
            if (state is null)
            {
                var legacy = invoice.CurrentMappingVersionId is not null
                    || invoice.LastMatchResultId is not null
                    || invoice.ActiveMatchClaimResultId is not null;
                return new { invoiceId = id, invoiceVersion = invoice.Version,
                    sourceSchemaVersion = 2, mappingRevision = 0, stateVersion = 0L,
                    state = legacy ? "LegacyWorkflow" : "Unmapped", mappingVersionId = (Guid?)null,
                    reviewId = (Guid?)null, reviewOutcomeId = (Guid?)null,
                    reviewState = "NotOpened", comparisonId = (Guid?)null,
                    exposureVersion = (long?)null, isAuthority = false,
                    canPostToFinance = false };
            }
            if (state.TenantId != scope.TenantId || state.LegalEntityId != scope.LegalEntityId
                || state.BranchId != invoice.BranchRef || state.SupplierId != invoice.SupplierId
                || state.CurrencyCode != invoice.CurrencyCode || state.InvoiceVersion != invoice.Version)
                throw new InvalidOperationException("PrincipalInvoiceCurrentStateChanged");
            var mapping = await db.PrincipalInvoiceMappingVersions.AsNoTracking()
                .SingleAsync(x => x.Id == state.CurrentMappingId && x.SupplierInvoiceId == id, token);
            var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                branches, invoice, mapping.PurchaseOrderId, mapping.PrincipalIssuedVersionId, token);
            var originals = await db.SupplierInvoiceLines.AsNoTracking().Where(x =>
                x.SupplierInvoiceId == id && x.IsActive).OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var normalized = await db.PrincipalInvoiceMappingLines.AsNoTracking().Where(x =>
                x.MappingVersionId == mapping.Id).ToArrayAsync(token);
            _ = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, originals,
                source.Order, source.Acceptances, source.AcceptanceSourceStates, normalized);
            var review = await db.PrincipalInvoiceUnitReviewSources.AsNoTracking()
                .SingleOrDefaultAsync(x => x.MappingVersionId == mapping.Id, token);
            var outcome = review is null ? null : await db.PrincipalInvoiceUnitReviewOutcomes.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ReviewSourceId == review.Id, token);
            var exposure = await db.PrincipalInvoiceExposureStates.AsNoTracking()
                .SingleAsync(x => x.PurchaseOrderId == mapping.PurchaseOrderId
                    && x.PrincipalIssuedVersionId == mapping.PrincipalIssuedVersionId, token);
            if (exposure.BranchId != state.BranchId || exposure.SupplierId != state.SupplierId
                || exposure.CurrencyCode != state.CurrencyCode)
                throw new InvalidOperationException("PrincipalInvoiceExposureSourceChanged");
            if (state.State is ("Reviewed" or "ComparisonBlocked") && outcome?.State != "Verified"
                || state.State == "Compared" && state.ActiveClaimComparisonId is null)
                throw new InvalidOperationException("PrincipalInvoiceStateOutcomeChanged");
            return new { invoiceId = id, invoiceVersion = invoice.Version,
                sourceSchemaVersion = 2, mappingRevision = state.MappingRevision,
                stateVersion = state.Version, state = state.State,
                mappingVersionId = (Guid?)mapping.Id, reviewId = (Guid?)review?.Id,
                reviewOutcomeId = (Guid?)outcome?.Id,
                reviewState = outcome?.State ?? (review is null ? "NotOpened" : "Pending"),
                comparisonId = state.LastComparisonId,
                exposureVersion = (long?)exposure.Version,
                isAuthority = false, canPostToFinance = false };
        }, ct);
    }
}
