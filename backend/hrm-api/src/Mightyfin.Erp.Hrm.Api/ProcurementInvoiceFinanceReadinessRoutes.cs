using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explains existing invoice evidence and missing AP prerequisites; never prepares a handoff.</summary>
public static class ProcurementInvoiceFinanceReadinessRoutes
{
    public static void MapRoutes(WebApplication app) => app.MapGet(
        "/api/procurement/v1/invoices/{id:guid}/finance-readiness", Read).RequireAuthorization("procurement-invoice-read");

    private static async Task<IResult> Read(Guid id, HttpContext http, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        // EXACT existing invoice-reader permission/scope. No new employee/canonical-person or machine-identity requirement.
        var query = ProcurementInvoiceIntakeRoutes.ReadableInvoices(db, scope);
        // Original intake columns only, so unavailable P26 schema is distinct from no comparison.
        var invoice = await query.Where(x => x.Id == id).Select(x => new SupplierInvoice
        {
            Id = x.Id, TenantId = x.TenantId, LegalEntityId = x.LegalEntityId, BranchRef = x.BranchRef,
            Version = x.Version, SupplierId = x.SupplierId, PurchaseOrderId = x.PurchaseOrderId,
            InvoiceNumber = x.InvoiceNumber, NormalizedInvoiceNumber = x.NormalizedInvoiceNumber,
            InvoiceDate = x.InvoiceDate, DueDate = x.DueDate, CurrencyCode = x.CurrencyCode,
            NetAmount = x.NetAmount, TaxAmount = x.TaxAmount, GrossAmount = x.GrossAmount, Status = x.Status,
            EvidenceDocumentRefId = x.EvidenceDocumentRefId, CreditForInvoiceRef = x.CreditForInvoiceRef,
        }).SingleOrDefaultAsync(ct);
        if (invoice is null) return Results.NotFound(new { code = "not-found", message = "Invoice not found." });
        var lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == id && x.IsActive)
            .OrderBy(x => x.LineNumber).ToArrayAsync(ct);
        var readiness = http.RequestServices.GetService<IProcurementInvoiceMatchingSchemaReadiness>();
        var schemaAvailable = readiness is not null && await readiness.IsReadyAsync(ct);
        Guid? mappingId = null; Guid? resultId = null; int? unresolved = null;
        InvoiceMatchMappingVersion? mapping = null; InvoiceMatchResult? result = null;
        if (schemaAvailable)
        {
            var pointers = await query.Where(x => x.Id == id)
                .Select(x => new { x.CurrentMappingVersionId, x.LastMatchResultId }).SingleOrDefaultAsync(ct);
            if (pointers is null) return Results.NotFound(new { code = "not-found", message = "Invoice not found." });
            mappingId = pointers.CurrentMappingVersionId; resultId = pointers.LastMatchResultId;
            mapping = await db.InvoiceMatchMappingVersions.AsNoTracking().SingleOrDefaultAsync(
                x => x.SupplierInvoiceId == id && x.Id == mappingId, ct);
            result = await db.InvoiceMatchResults.AsNoTracking().SingleOrDefaultAsync(
                x => x.SupplierInvoiceId == id && x.Id == resultId, ct);
            unresolved = await db.InvoiceExceptions.AsNoTracking().CountAsync(x => x.SupplierInvoiceId == id
                && x.Status != "Resolved" && x.Status != "Closed" && x.Status != "Cancelled", ct);
        }
        var currentVersion = await query.Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct);
        if (currentVersion is null) return Results.NotFound(new { code = "not-found", message = "Invoice not found." });
        if (currentVersion != invoice.Version) return Results.Conflict(new
        { code = "InvoiceReadinessChanged", message = "The invoice changed while its review information loaded. Refresh it." });
        return Results.Ok(InvoiceApReadinessProjection.Build(invoice, lines, schemaAvailable,
            mappingId, resultId, mapping, result, unresolved));
    }
}
