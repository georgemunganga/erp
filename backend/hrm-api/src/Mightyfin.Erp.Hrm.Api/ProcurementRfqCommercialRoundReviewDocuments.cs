using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialReviewDocumentGrant(Guid EventId,
        IReadOnlyDictionary<Guid, string> Hashes);

    private static async Task<CommercialReviewDocumentGrant> CurrentCommercialReviewDocuments(
        ProcurementDbContext db,
        Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases,
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,
        IAuthorizationService auth, HttpContext http, ProcurementRequestScope scope,
        Guid id, Guid assignmentId, bool download, CancellationToken ct)
    {
        var review = await CurrentCommercialReview(db, cases, core, auth,
            http, scope, id, assignmentId, ct);
        var draft = PrincipalRfqCommercialDraft.Restore(review.Head, review.Revision);
        var expected = draft.Requirements.ToDictionary(x => x.VersionId, x => x.Sha256);
        if (expected.Count > 100 || expected.Count != review.Prepared.DocumentCount)
            throw new Problem("RfqCommercialRequirementsChanged", 409);
        if (expected.Count == 0) return new(review.Opening.SourcingEventId, expected);
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        if (http.RequestServices.GetService<CoreDocumentFeature>() is not { Enabled: true }
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                config.GetConnectionString("CoreDocuments"),
                db.Database.GetDbConnection().ConnectionString)
            || !await ProcurementInvoiceReviewDocumentReadiness.ReadyAsync(
                db, scope, write: false, retire: false, ct, download: download))
            throw new Problem("RfqCommercialRequirementsUnavailable", 503);
        await using var documents = new DocumentDbContext(
            new DbContextOptionsBuilder<DocumentDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(
            db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var ids = expected.Keys.ToArray();
        var current = await (from version in documents.Versions.AsNoTracking()
            join owner in documents.Documents.AsNoTracking()
                on version.DocumentId equals owner.Id
            where ids.Contains(version.Id)
                && version.TenantId == scope.TenantId
                && version.LegalEntityId == scope.LegalEntityId
                && owner.TenantId == scope.TenantId
                && owner.LegalEntityId == scope.LegalEntityId
                && owner.OwnerModule == "procurement"
                && owner.OwnerType == PrincipalRfqPrepublicationPlan.DocumentOwnerType
                && owner.OwnerId == review.Opening.SourcingEventId
                && !owner.IsRetired
                && version.ScanState == DocumentScanState.Available
            select new { version.Id, version.Sha256 }).ToArrayAsync(ct);
        if (current.Length != expected.Count || current.Any(x =>
                !expected.TryGetValue(x.Id, out var sha) || sha != x.Sha256))
            throw new Problem("RfqCommercialRequirementsChanged", 409);
        return new(review.Opening.SourcingEventId, expected);
    }

    private static Task<IResult> CommercialRoundReviewDocuments(Guid id,
        Guid assignmentId, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            var grant = await CurrentCommercialReviewDocuments(db, cases,
                core, auth, http, scope, id, assignmentId, false, token);
            if (grant.Hashes.Count == 0)
                return new Reply(System.Text.Json.JsonSerializer.Serialize(new
                {
                    roundId = id, assignmentId, eventId = grant.EventId,
                    items = Array.Empty<object>(),
                    availabilityMeaning = "NoRequirementDocuments",
                    sealedOffersVisible = false,
                }, Json));
            var owner = new DocumentOwner("procurement",
                PrincipalRfqPrepublicationPlan.DocumentOwnerType, grant.EventId);
            var service = CommercialReviewDocumentService(http, scope, grant);
            var items = await service.ListAsync(owner, token);
            return new Reply(System.Text.Json.JsonSerializer.Serialize(new
            {
                roundId = id, assignmentId, eventId = grant.EventId,
                items, availabilityMeaning = "PinnedRequirementsOnly",
                sealedOffersVisible = false,
            }, Json));
        }, ct);

    private static async Task<IResult> CommercialRoundReviewDocumentDownload(Guid id,
        Guid assignmentId, Guid versionId, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (versionId == Guid.Empty)
            return Error("RfqCommercialRequirementNotFound", 404);
        CommercialReviewDocumentGrant? grant = null;
        var authorized = await Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            var current = await CurrentCommercialReviewDocuments(db, cases,
                core, auth, http, scope, id, assignmentId, true, token);
            if (!current.Hashes.ContainsKey(versionId))
                throw new Problem("RfqCommercialRequirementNotFound", 404);
            grant = current;
            return new Reply("{}", 200);
        }, ct);
        if (grant is null || authorized is not IStatusCodeHttpResult { StatusCode: 200 })
            return authorized;
        try
        {
            // Blob IO follows the completed scoped transaction. Core rechecks
            // current availability, exact SHA and appends its download audit.
            var file = await CommercialReviewDocumentService(http, scope, grant)
                .DownloadAsync(new("procurement",
                    PrincipalRfqPrepublicationPlan.DocumentOwnerType, grant.EventId),
                    versionId, ct);
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Content, file.MediaType, file.FileName,
                enableRangeProcessing: false);
        }
        catch (DocumentProblemException error)
        {
            return Results.Json(new { code = error.Code,
                message = "The pinned requirement document is unavailable." },
                statusCode: error.Code == "document-not-found" ? 404
                    : error.Code == "document-content-changed" ? 503 : 409);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (IOException)
        {
            return Results.Problem(statusCode: 503,
                title: "The requirement document could not be read.");
        }
    }

    private static RecordDocumentService CommercialReviewDocumentService(
        HttpContext http, ProcurementRequestScope scope,
        CommercialReviewDocumentGrant grant)
        => new(scope, new CommercialReviewPurpose(scope, grant.EventId),
            new CommercialPinnedRecords(
                http.RequestServices.GetRequiredService<IDocumentRecords>(),
                grant.EventId, grant.Hashes),
            http.RequestServices.GetRequiredService<IDocumentBlobStore>(),
            http.RequestServices.GetRequiredService<IDocumentScanner>(),
            http.RequestServices.GetRequiredService<DocumentLimits>());

    private sealed class CommercialReviewPurpose(ProcurementRequestScope scope,
        Guid eventId) : IDocumentOwnerAccess
    {
        private readonly string tenant = scope.TenantId;
        private readonly Guid company = scope.LegalEntityId;
        private readonly string principal = scope.SubjectId;
        public Task RequireAsync(DocumentOwner owner, DocumentAction action,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.TenantId != tenant || scope.LegalEntityId != company
                || scope.SubjectId != principal || action != DocumentAction.Read
                || owner != new DocumentOwner("procurement",
                    PrincipalRfqPrepublicationPlan.DocumentOwnerType, eventId))
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }

    // This is a purpose authorizer over Core's existing document store. It
    // enumerates only retained selected versions; it cannot scan/upload/edit.
    internal sealed class CommercialPinnedRecords(IDocumentRecords inner, Guid eventId,
        IReadOnlyDictionary<Guid, string> hashes) : IDocumentRecords
    {
        private DocumentOwner Owner => new("procurement",
            PrincipalRfqPrepublicationPlan.DocumentOwnerType, eventId);
        public Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document,
            CoreDocumentVersion version, int maxFiles, CancellationToken ct)
            => throw new UnauthorizedAccessException();
        public async Task<RegisteredDocument?> FindAsync(DocumentOwner owner,
            Guid versionId, CancellationToken ct)
        {
            if (owner != Owner || !hashes.TryGetValue(versionId, out var sha))
                return null;
            var value = await inner.FindAsync(owner, versionId, ct);
            return value is not null && value.Version.Id == versionId
                && value.Version.Sha256 == sha
                && value.Version.IsAvailableFor(value.Document)
                ? value : null;
        }
        public async Task<IReadOnlyList<RegisteredDocument>> ListAsync(
            DocumentOwner owner, CancellationToken ct)
        {
            if (owner != Owner) throw new UnauthorizedAccessException();
            var rows = new List<RegisteredDocument>();
            foreach (var id in hashes.Keys.Order())
                rows.Add(await FindAsync(owner, id, ct)
                    ?? throw new KeyNotFoundException());
            return rows;
        }
        public async Task SaveStateAsync(RegisteredDocument record, string action,
            DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct)
        {
            if (action != "DownloadAuthorized" || evidence is not null
                || failureCode is not null
                || !hashes.TryGetValue(record.Version.Id, out var sha)
                || record.Version.Sha256 != sha
                || record.Document.OwnerModule != Owner.Module
                || record.Document.OwnerType != Owner.Type
                || record.Document.OwnerId != eventId
                || !record.Version.IsAvailableFor(record.Document))
                throw new UnauthorizedAccessException();
            var current = await FindAsync(Owner, record.Version.Id, ct)
                ?? throw new UnauthorizedAccessException();
            await inner.SaveStateAsync(current, action, null, null, ct);
        }
    }
}
