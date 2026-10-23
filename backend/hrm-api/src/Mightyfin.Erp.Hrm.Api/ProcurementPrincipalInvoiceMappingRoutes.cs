using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Separate schema-2 invoice mapping command. This route is unregistered
/// and requires an actual P31 owning-store readiness implementation before use.
/// It does not approve an invoice, claim capacity or post to Finance.</summary>
public static class ProcurementPrincipalInvoiceMappingRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record MapInput(long InvoiceVersion, int ExpectedMappingRevision,
        long ExpectedStateVersion, Guid PurchaseOrderId, Guid PrincipalIssuedVersionId,
        PrincipalInvoiceMappingInputLine[] Lines);

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoices");
        group.MapPost("/{id:guid}/principal-matching/mappings", Map)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match", "procurement-invoice-capture");
        group.MapGet("/{id:guid}/principal-matching/commands/{key:guid}", Recover)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
    }

    private static bool Available(HttpContext http) =>
        http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is { Enabled: true }
        && http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not null;

    private static async Task<IResult> Map(Guid id, MapInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http) || !await http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceMatchingUnavailable" }, statusCode: 503);
        if (input is null || input.Lines is not { Length: > 0 and <= 200 }
            || input.InvoiceVersion < 1 || input.ExpectedMappingRevision < 0
            || input.ExpectedStateVersion < 0
            || !Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
            return Results.Json(new { code = "PrincipalInvoiceMappingInputInvalid" }, statusCode: 422);
        var requestSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new { schemaVersion = 2, id, input }, Json));
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadyAsync(db, token))
                throw new InvalidOperationException("PrincipalInvoiceMatchingSchemaUnavailable");
            var (invoice, branches) = await ProcurementPrincipalInvoiceSourceRoutes.Current(db, core, scope, http, auth, id, token);
            var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            if (actor is null || actor.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || !actor.Permissions.Contains("procurement-invoice-capture")
                || !(await auth.AuthorizeAsync(http.User, "procurement-invoice-capture")).Succeeded)
                throw new UnauthorizedAccessException();
            var existing = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Operation == "Map" && x.CommandKey == key, token);
            if (existing is not null)
            {
                if (existing.SupplierInvoiceId != id || existing.ActorPrincipalId != scope.SubjectId
                    || existing.ActorWorkerAliasId != scope.CoreLinkedWorkerId || existing.RequestSha256 != requestSha
                    || existing.BranchId != invoice.BranchRef
                    || !await ProcurementPrincipalInvoiceSourceBridge.QueryBound(db, scope, branches, invoice)
                        .AnyAsync(x => x.Id == input.PurchaseOrderId, token)
                    || existing.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(existing.ResponseJson))
                    throw new InvalidOperationException("PrincipalInvoiceCommandConflict");
                return JsonSerializer.Deserialize<JsonElement>(existing.ResponseJson, Json);
            }
            if (invoice.Version != input.InvoiceVersion || invoice.CurrentMappingVersionId is not null
                || invoice.ActiveMatchClaimResultId is not null || invoice.LastMatchResultId is not null)
                throw new InvalidOperationException("PrincipalInvoiceOriginalOrLegacyClaimChanged");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", token);
            var state = await db.PrincipalInvoiceStates.SingleOrDefaultAsync(x => x.SupplierInvoiceId == id, token);
            if (state is null)
            {
                if (input.ExpectedMappingRevision != 0 || input.ExpectedStateVersion != 0)
                    throw new InvalidOperationException("PrincipalInvoiceMappingStateChanged");
            }
            else
            {
                if (state.Version != input.ExpectedStateVersion
                    || state.MappingRevision != input.ExpectedMappingRevision
                    || state.State != "Mapped" || state.LastComparisonId is not null
                    || state.ActiveClaimComparisonId is not null
                    || state.InvoiceVersion != invoice.Version
                    || state.BranchId != invoice.BranchRef || state.SupplierId != invoice.SupplierId
                    || state.CurrencyCode != invoice.CurrencyCode
                    || state.CurrentMappingId is not { } oldMapId)
                    throw new InvalidOperationException("PrincipalInvoiceMappingStateChanged");
                var oldReview = await db.PrincipalInvoiceUnitReviewSources.SingleOrDefaultAsync(x =>
                    x.MappingVersionId == oldMapId && x.SupplierInvoiceId == id, token)
                    ?? throw new InvalidOperationException("PrincipalInvoicePreviousReviewRequired");
                var oldOutcome = await db.PrincipalInvoiceUnitReviewOutcomes.SingleOrDefaultAsync(x =>
                    x.ReviewSourceId == oldReview.Id
                        && (x.State == "Returned" || x.State == "Rejected"), token)
                    ?? throw new InvalidOperationException("PrincipalInvoiceReturnedReviewRequired");
                var coreCase = await cases.Cases.SingleOrDefaultAsync(x => x.Id == oldReview.CoreCaseId
                    && x.SourceId == oldReview.Id
                    && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType, token);
                if (coreCase is null || coreCase.State != oldOutcome.State
                    || coreCase.CompletedAt != oldOutcome.CompletedAt)
                    throw new InvalidOperationException("PrincipalInvoicePreviousCoreDecisionChanged");
            }
            var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                branches, invoice, input.PurchaseOrderId, input.PrincipalIssuedVersionId, token);
            // The exposure owner is one row per immutable issued source, shared by
            // every invoice against that source. Serialize its creation on the
            // owning PO before reading history; an absent row is Unknown, not
            // evidence of zero prior billing.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={input.PurchaseOrderId} AND branch_ref={invoice.BranchRef} FOR UPDATE", token);
            var exposure = await db.PrincipalInvoiceExposureStates.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.PurchaseOrderId == input.PurchaseOrderId
                    && x.PrincipalIssuedVersionId == input.PrincipalIssuedVersionId, token);
            if (exposure is null)
                db.AddScoped(new PrincipalInvoiceExposureState
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    PurchaseOrderId = input.PurchaseOrderId,
                    PrincipalIssuedVersionId = input.PrincipalIssuedVersionId,
                    SupplierId = invoice.SupplierId, BranchId = invoice.BranchRef!.Value,
                    CurrencyCode = invoice.CurrencyCode, Sequence = 0, CoverageState = "Unknown"
                });
            else if (exposure.SupplierId != invoice.SupplierId || exposure.BranchId != invoice.BranchRef
                || exposure.CurrencyCode != invoice.CurrencyCode)
                throw new InvalidOperationException("PrincipalInvoiceExposureSourceChanged");
            await RequireOriginalDocuments(db, scope, http, id, input.Lines, token);
            var originalLines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var nextRevision = checked(input.ExpectedMappingRevision + 1);
            var mapping = PrincipalInvoiceMappingEvidence.Create(invoice, originalLines, source.Order,
                source.Acceptances, source.AcceptanceSourceStates, input.Lines, nextRevision,
                scope.SubjectId, scope.CoreLinkedWorkerId, now);
            if (state is null)
            {
                state = new PrincipalInvoiceState
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    SupplierInvoiceId = id, BranchId = invoice.BranchRef!.Value,
                    SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode,
                    InvoiceVersion = invoice.Version, MappingRevision = nextRevision,
                    CurrentMappingId = mapping.Id, State = "Mapped"
                };
                db.AddScoped(state);
            }
            else
            {
                state.MappingRevision = nextRevision;
                state.CurrentMappingId = mapping.Id;
            }
            db.AddScoped(mapping);
            foreach (var line in input.Lines)
                db.AddScoped(new Mightyfin.Erp.Procurement.Domain.PrincipalInvoiceMappingLine
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    MappingVersionId = mapping.Id, SupplierInvoiceId = id,
                    SupplierInvoiceLineId = line.InvoiceLineId,
                    PurchaseOrderId = mapping.PurchaseOrderId,
                    PrincipalIssuedVersionId = mapping.PrincipalIssuedVersionId,
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    PrincipalReceivingSourceStateId = line.PrincipalReceivingSourceStateId,
                    PrincipalAcceptanceFactId = line.PrincipalAcceptanceFactId,
                    AcceptanceSequence = line.AcceptanceSequence,
                    InvoiceUnitCode = line.InvoiceUnitCode,
                    DocumentVersionId = line.DocumentVersionId,
                    DocumentLineReference = line.DocumentLineReference
                });
            var response = JsonSerializer.Serialize(new
            {
                invoiceId = id, invoiceVersion = invoice.Version,
                mappingVersionId = mapping.Id, mapping.MappingRevision,
                mapping.MappingSha256, commandKey = key, sourceSchemaVersion = 2,
                state = "Mapped", isUnitVerified = false, matchingAuthorityAvailable = false,
                canAllocate = false, canPostToFinance = false
            }, Json);
            db.AddScoped(new PrincipalInvoiceCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SupplierInvoiceId = id, Operation = "Map", CommandKey = key,
                ActorPrincipalId = scope.SubjectId, ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                BranchId = invoice.BranchRef.Value, RequestSha256 = requestSha,
                ResponseJson = response, ResponseSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(response),
                ResponseStatus = 200, CompletedAt = now
            });
            await db.SaveChangesAsync(token);
            return JsonSerializer.Deserialize<JsonElement>(response, Json);
        }, ct);
    }

    private static Task<IResult> Recover(Guid id, Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http)) return Task.FromResult<IResult>(Results.Json(new { code = "PrincipalInvoiceMatchingUnavailable" }, statusCode: 503));
        return ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            var (invoice, _) = await ProcurementPrincipalInvoiceSourceRoutes.Current(db, core, scope, http, auth, id, token);
            if (!await http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadyAsync(db, token))
                throw new InvalidOperationException("PrincipalInvoiceMatchingSchemaUnavailable");
            var row = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SupplierInvoiceId == id && x.CommandKey == key, token)
                ?? throw new KeyNotFoundException();
            if (row.ActorPrincipalId != scope.SubjectId || row.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || row.BranchId != invoice.BranchRef || row.Operation != "Map"
                || row.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(row.ResponseJson))
                throw new KeyNotFoundException();
            var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            if (actor is null || !actor.Permissions.Contains("procurement-invoice-capture")
                || !(await auth.AuthorizeAsync(http.User, "procurement-invoice-capture")).Succeeded)
                throw new UnauthorizedAccessException();
            using var parsed = JsonDocument.Parse(row.ResponseJson);
            var response = parsed.RootElement;
            var mappingId = response.GetProperty("mappingVersionId").GetGuid();
            var mapping = await db.PrincipalInvoiceMappingVersions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == mappingId && x.SupplierInvoiceId == id, token)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingReceiptWithoutEffect");
            if (response.GetProperty("invoiceId").GetGuid() != id
                || response.GetProperty("invoiceVersion").GetInt64() != mapping.InvoiceVersion
                || response.GetProperty("mappingRevision").GetInt32() != mapping.MappingRevision
                || response.GetProperty("commandKey").GetGuid() != key
                || response.GetProperty("sourceSchemaVersion").GetInt32() != 2
                || !await ProcurementPrincipalInvoiceSourceBridge.QueryBound(db, scope,
                    new HashSet<Guid> { row.BranchId }, invoice).AnyAsync(x => x.Id == mapping.PurchaseOrderId, token))
                throw new InvalidOperationException("PrincipalInvoiceMappingReceiptChanged");
            return JsonSerializer.Deserialize<JsonElement>(row.ResponseJson, Json);
        }, ct);
    }

    private static async Task RequireOriginalDocuments(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, Guid invoiceId, IReadOnlyList<PrincipalInvoiceMappingInputLine> lines, CancellationToken ct)
    {
        var ids = lines.Select(x => x.DocumentVersionId).Distinct().ToArray();
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        if (ids.Length == 0 || ids.Any(x => x == Guid.Empty)
            || !http.RequestServices.GetRequiredService<CoreDocumentFeature>().Enabled
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(config.GetConnectionString("CoreDocuments"),
                db.Database.GetDbConnection().ConnectionString))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentOwnerUnavailable");
        await using var docs = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await docs.Database.UseTransactionAsync(db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var found = await (from version in docs.Versions
            join owner in docs.Documents on version.DocumentId equals owner.Id
            where ids.Contains(version.Id) && owner.OwnerModule == "procurement"
                && owner.OwnerType == "supplier_invoice" && owner.OwnerId == invoiceId
                && !owner.IsRetired && version.ScanState == DocumentScanState.Available
            select version.Id).Distinct().CountAsync(ct);
        if (found != ids.Length) throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentUnavailable");
    }
}

public sealed class ProcurementPrincipalInvoiceMatchingOptions
{
    public bool Enabled { get; set; }
}

public interface IProcurementPrincipalInvoiceSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
    Task<bool> IsReadyAsync(ProcurementDbContext db, CancellationToken ct);
}
