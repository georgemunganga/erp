using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static async Task<IReadOnlyDictionary<Guid, string>> AvailableHashes(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, Guid invoiceId, InvoiceMappingSnapshot mapping, CancellationToken ct)
    {
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        if (!http.RequestServices.GetRequiredService<CoreDocumentFeature>().Enabled
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(config.GetConnectionString("CoreDocuments"), db.Database.GetDbConnection().ConnectionString))
            throw new ReviewProblem("InvoiceUnitReviewDocumentOwnerUnavailable", 503);
        var ids = mapping.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray();
        if (ids.Length == 0 || ids.Any(x => x == Guid.Empty)) throw new ReviewProblem("InvoiceOriginalDocumentVersionRequired", 409);
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var rows = await (from version in documents.Versions join owner in documents.Documents on version.DocumentId equals owner.Id
            where ids.Contains(version.Id) && owner.OwnerModule == "procurement" && owner.OwnerType == "supplier_invoice" && owner.OwnerId == invoiceId && !owner.IsRetired
                && version.ScanState == DocumentScanState.Available
            select new { version.Id, version.Sha256 }).ToArrayAsync(ct);
        if (rows.Length != ids.Length) throw new ReviewProblem("InvoiceOriginalDocumentsNotAvailable", 409);
        return rows.ToDictionary(x => x.Id, x => x.Sha256);
    }
    private static Task<IResult> Documents(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunResult(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (source, invoice) = await Source(db, scope, id, false, token); var workflow = await Workflow(cases, scope, source, false, token);
            var person = await Actor(core, scope, http, auth, source.BranchRef, [InvoiceRetainedUnitReview.ReviewPermission, InvoiceUnitReviewTransitions.RequestPermission], token);
            var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            RequireDocumentPurpose(source, invoice, workflow, pins, person);
            var mapping = await Mapping(db, invoice, token); var originals = await Originals(db, invoice.Id, token);
            var hashes = await AvailableHashes(db, scope, http, invoice.Id, InvoiceMappingEvidence.Verify(mapping, invoice, originals), token);
            var retained = InvoiceUnitReviewStorage.Read(source);
            if (retained.Lines.Any(x => !hashes.TryGetValue(x.DocumentVersionId, out var sha) || sha != x.DocumentSha256)) throw new ReviewProblem("InvoiceOriginalDocumentChanged", 409);
            var service = PurposeDocuments(http, db, scope, invoice.Id, hashes);
            return Results.Ok(new { reviewId = id, invoiceId = invoice.Id, items = await service.ListAsync(new("procurement", "supplier_invoice", invoice.Id), token),
                availabilityMeaning = "PinnedOriginalDocumentVersionsOnly", isUnitVerification = false, canPostToFinance = false });
        }, ct);
    private static async Task<IResult> Download(Guid id, Guid versionId, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        RecordDocumentService? authorized = null; Guid invoiceId = Guid.Empty;
        var result = await RunResult(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (source, invoice) = await Source(db, scope, id, false, token); var workflow = await Workflow(cases, scope, source, false, token);
            var person = await Actor(core, scope, http, auth, source.BranchRef, [InvoiceRetainedUnitReview.ReviewPermission, InvoiceUnitReviewTransitions.RequestPermission], token);
            var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            RequireDocumentPurpose(source, invoice, workflow, pins, person);
            var mapping = await Mapping(db, invoice, token); var originals = await Originals(db, invoice.Id, token);
            var hashes = await AvailableHashes(db, scope, http, invoice.Id, InvoiceMappingEvidence.Verify(mapping, invoice, originals), token);
            var retained = InvoiceUnitReviewStorage.Read(source);
            if (!retained.Lines.Any(x => x.DocumentVersionId == versionId && hashes.TryGetValue(versionId, out var sha) && sha == x.DocumentSha256)) throw new KeyNotFoundException();
            invoiceId = invoice.Id; authorized = PurposeDocuments(http, db, scope, invoice.Id, hashes);
            return Results.Ok(); // Authorisation linearizes under the paired branch fence.
        }, ct);
        if (result is not IStatusCodeHttpResult { StatusCode: 200 } || authorized is null) return result;
        try
        {
            // Blob I/O is outside the database retry callback: never leak a stream on a
            // serializable retry. Core rechecks current document availability/content hash.
            var file = await authorized.DownloadAsync(new("procurement", "supplier_invoice", invoiceId), versionId, ct);
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Content, file.MediaType, file.FileName, enableRangeProcessing: false);
        }
        catch (DocumentProblemException error)
        { return Results.Json(new { code = error.Code, message = "The pinned original document is unavailable." }, statusCode: error.Code == "document-not-found" ? 404 : error.Code == "document-content-changed" ? 503 : 409); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (IOException) { return Results.Problem(statusCode: 503, title: "The original document could not be read."); }
    }
    private static void RequireDocumentPurpose(Mightyfin.Erp.Procurement.Domain.InvoiceUnitReviewSource source,
        Mightyfin.Erp.Procurement.Domain.SupplierInvoice invoice, Mightyfin.Erp.Core.Approvals.Domain.ApprovalCase workflow,
        IReadOnlyList<Mightyfin.Erp.Procurement.Domain.InvoiceUnitReviewAssignmentEvidence> pins, InvoiceUnitReviewPolicyRules.Person person)
    {
        if (invoice.CurrentMappingVersionId != source.MappingVersionId || invoice.MappingRevision != source.MappingRevision) throw new KeyNotFoundException();
        var maker = person.PrincipalId == source.MakerPrincipalId && person.WorkerId == source.MakerWorkerId
            && person.Permissions.Contains(InvoiceUnitReviewTransitions.RequestPermission) && person.Permissions.Contains("procurement-invoice-read");
        var assigned = workflow.State == "Pending" && person.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
            && workflow.Stages.Where(x => x.State == "Active").SelectMany(x => x.Assignments).Any(x => x.State == "Pending"
                && x.AssigneeSubjectId == person.PrincipalId && pins.Any(p => p.CoreAssignmentId == x.Id && p.WorkerId == person.WorkerId && p.PrincipalId == person.PrincipalId));
        if (!maker && !assigned) throw new UnauthorizedAccessException();
    }
    private static RecordDocumentService PurposeDocuments(HttpContext http, ProcurementDbContext transaction, ProcurementRequestScope scope, Guid invoice,
        IReadOnlyDictionary<Guid, string> hashes) => new(scope, new PurposeAccess(invoice, scope.TenantId, scope.LegalEntityId, scope.SubjectId, scope),
            new PinnedRecords(http.RequestServices.GetRequiredService<IDocumentRecords>(), invoice, hashes),
            http.RequestServices.GetRequiredService<IDocumentBlobStore>(), http.RequestServices.GetRequiredService<IDocumentScanner>(),
            http.RequestServices.GetRequiredService<DocumentLimits>());
    // Private short-lived access object created only after actual current assignment/scope proof,
    // under the owning branch fence, usable only in this exact scoped HTTP operation.
    // Blob read follows the successful authorization transaction, never its retry callback.
    // No global invoice-read grant is installed.
    private sealed class PurposeAccess(Guid invoice, string tenant, Guid company, string principal, ProcurementRequestScope scope) : IDocumentOwnerAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct)
        {
            if (scope.TenantId != tenant || scope.LegalEntityId != company || scope.SubjectId != principal || action != DocumentAction.Read || owner.Module != "procurement" || owner.Type != "supplier_invoice" || owner.Id != invoice)
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }
    internal sealed class PinnedRecords(IDocumentRecords inner, Guid invoice, IReadOnlyDictionary<Guid, string> hashes) : IDocumentRecords
    {
        private bool Owner(DocumentOwner owner) => owner.Module == "procurement" && owner.Type == "supplier_invoice" && owner.Id == invoice;
        public Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document, CoreDocumentVersion version, int maxFiles, CancellationToken ct) => throw new UnauthorizedAccessException();
        public async Task SaveStateAsync(RegisteredDocument record, string action, DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct)
        {
            // Core download audit is required by the actual owner service. It grants no
            // upload/scan/state transition capability and may reference only the exact pin.
            var owner = new DocumentOwner("procurement", "supplier_invoice", invoice);
            if (action != "DownloadAuthorized" || evidence is not null || failureCode is not null
                || !hashes.TryGetValue(record.Version.Id, out var sha) || record.Version.Sha256 != sha
                || record.Document.OwnerModule != owner.Module || record.Document.OwnerType != owner.Type || record.Document.OwnerId != invoice
                || !record.Version.IsAvailableFor(record.Document)) throw new UnauthorizedAccessException();
            var current = await FindAsync(owner, record.Version.Id, ct) ?? throw new UnauthorizedAccessException();
            await inner.SaveStateAsync(current, action, null, null, ct);
        }
        public async Task<RegisteredDocument?> FindAsync(DocumentOwner owner, Guid versionId, CancellationToken ct)
        {
            if (!Owner(owner) || !hashes.TryGetValue(versionId, out var sha)) return null;
            var value = await inner.FindAsync(owner, versionId, ct);
            return value is not null && value.Version.Id == versionId && value.Version.Sha256 == sha && value.Version.IsAvailableFor(value.Document) ? value : null;
        }
        public async Task<IReadOnlyList<RegisteredDocument>> ListAsync(DocumentOwner owner, CancellationToken ct)
        {
            if (!Owner(owner)) throw new UnauthorizedAccessException(); var rows = new List<RegisteredDocument>();
            foreach (var id in hashes.Keys.Order()) rows.Add(await FindAsync(owner, id, ct) ?? throw new KeyNotFoundException());
            return rows;
        }
    }
}
