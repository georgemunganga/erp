using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceReviewRoutes
{
    private sealed class PrincipalPurpose(Guid invoice, string tenant, Guid company,
        string principal, ProcurementRequestScope scope) : IDocumentOwnerAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct)
        {
            if (scope.TenantId != tenant || scope.LegalEntityId != company || scope.SubjectId != principal
                || action != DocumentAction.Read || owner.Module != "procurement"
                || owner.Type != "supplier_invoice" || owner.Id != invoice)
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }

    private sealed record PrincipalDocumentAccess(Guid InvoiceId, IReadOnlyDictionary<Guid, string> Hashes);

    private static async Task<PrincipalDocumentAccess> CurrentReviewDocuments(
        ProcurementDbContext db, Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases,
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,
        IAuthorizationService auth, HttpContext http, ProcurementRequestScope scope,
        Guid reviewId, CancellationToken token)
    {
        var branches = await ReviewerBranches(core, scope, http, auth, token);
        var source = await db.PrincipalInvoiceUnitReviewSources.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reviewId && branches.Contains(x.BranchId)
                && db.PrincipalInvoiceUnitReviewAssignments.Any(pin =>
                    pin.ReviewSourceId == x.Id && pin.ReviewerPrincipalId == scope.SubjectId), token)
            ?? throw new KeyNotFoundException();
        _ = await ProcurementExecutionRecordScope.Branches(core, scope, source.BranchId, true, token);
        var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x =>
            x.Id == source.SupplierInvoiceId && x.BranchRef == source.BranchId, token);
        var state = await db.PrincipalInvoiceStates.AsNoTracking().SingleAsync(x =>
            x.SupplierInvoiceId == invoice.Id && x.CurrentMappingId == source.MappingVersionId
                && x.State == "Mapped", token);
        var mapping = await db.PrincipalInvoiceMappingVersions.AsNoTracking().SingleAsync(x =>
            x.Id == source.MappingVersionId && x.SupplierInvoiceId == invoice.Id, token);
        var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
            .SingleAsync(x => x.Id == source.CoreCaseId && x.SourceId == source.Id
                && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType
                && x.State == "Pending", token);
        var pins = await db.PrincipalInvoiceUnitReviewAssignments.AsNoTracking()
            .Where(x => x.ReviewSourceId == source.Id).ToArrayAsync(token);
        var active = workflow.Stages.Where(x => x.State == "Active")
            .SelectMany(x => x.Assignments).Any(x => x.State == "Pending"
                && x.AssigneeSubjectId == scope.SubjectId
                && pins.Any(pin => pin.CoreAssignmentId == x.Id
                    && pin.ReviewerPrincipalId == scope.SubjectId));
        if (!active) throw new KeyNotFoundException();
        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                source.MakerPrincipalId, source.MakerWorkerAliasId, scope.SubjectId,
                scope.CoreLinkedWorkerId, token))
            throw new UnauthorizedAccessException();
        var original = await db.SupplierInvoiceLines.AsNoTracking().Where(x =>
            x.SupplierInvoiceId == invoice.Id && x.IsActive).OrderBy(x => x.LineNumber)
            .ToArrayAsync(token);
        if (state.InvoiceVersion != invoice.Version
            || source.OriginalInvoiceSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, original))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
        var retained = PrincipalInvoiceUnitReviewTransitions.Verify(source, mapping, workflow, pins);
        await ProcurementPrincipalInvoiceDocumentUses.RequireAsync(db, scope, source, retained, token);
        var expected = new Dictionary<Guid, string>();
        foreach (var line in retained.Lines)
        {
            if (line.DocumentVersionId == Guid.Empty
                || line.DocumentSha256 is not { Length: 64 }
                || expected.TryGetValue(line.DocumentVersionId, out var earlier)
                    && earlier != line.DocumentSha256)
                throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
            expected[line.DocumentVersionId] = line.DocumentSha256;
        }
        if (expected.Count == 0 || expected.Count > 200)
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentMissing");
        var current = await ProcurementPrincipalInvoiceExposureReader.CurrentDocumentHashes(db,
            scope, http.RequestServices, invoice.Id, expected.Keys.ToArray(), token);
        if (current.Count != expected.Count || expected.Any(x =>
            !current.TryGetValue(x.Key, out var hash) || hash != x.Value))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
        return new PrincipalDocumentAccess(invoice.Id, expected);
    }

    private static RecordDocumentService PrincipalDocuments(HttpContext http,
        ProcurementRequestScope scope, PrincipalDocumentAccess access)
        => new(scope, new PrincipalPurpose(access.InvoiceId, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, scope),
            new ProcurementInvoiceUnitReviewRoutes.PinnedRecords(
                http.RequestServices.GetRequiredService<IDocumentRecords>(),
                access.InvoiceId, access.Hashes),
            http.RequestServices.GetRequiredService<IDocumentBlobStore>(),
            http.RequestServices.GetRequiredService<IDocumentScanner>(),
            http.RequestServices.GetRequiredService<DocumentLimits>());

    private static async Task<IResult> Documents(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, cases, core, auth, token) =>
            {
                if (!await ready.IsReadReadyAsync(db, token))
                    throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
                var access = await CurrentReviewDocuments(db, cases, core, auth, http, scope, id, token);
                var service = PrincipalDocuments(http, scope, access);
                return new { reviewId = id, invoiceId = access.InvoiceId,
                    items = await service.ListAsync(new("procurement", "supplier_invoice", access.InvoiceId), token),
                    availabilityMeaning = "PinnedOriginalDocumentVersionsOnly",
                    canPostToFinance = false };
            }, ct);
    }

    private static async Task<IResult> Download(Guid id, Guid versionId,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsDownloadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        PrincipalDocumentAccess? granted = null;
        var result = await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, cases, core, auth, token) =>
            {
                if (!await ready.IsDownloadReadyAsync(db, token))
                    throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
                var access = await CurrentReviewDocuments(db, cases, core, auth, http, scope, id, token);
                if (!access.Hashes.ContainsKey(versionId)) throw new KeyNotFoundException();
                granted = access;
                return new { reviewId = id, invoiceId = access.InvoiceId };
            }, ct);
        if (granted is null || result is IStatusCodeHttpResult { StatusCode: >= 400 }) return result;
        try
        {
            // The document owner rechecks availability and pinned content after
            // the scoped transaction. Blob I/O does not run inside a retry.
            var file = await PrincipalDocuments(http, scope, granted).DownloadAsync(
                new("procurement", "supplier_invoice", granted.InvoiceId), versionId, ct);
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Content, file.MediaType, file.FileName,
                enableRangeProcessing: false);
        }
        catch (DocumentProblemException error)
        {
            return Results.Json(new { code = error.Code, message = "The pinned original document is unavailable." },
                statusCode: error.Code == "document-not-found" ? 404
                    : error.Code == "document-content-changed" ? 503 : 409);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (IOException)
        {
            return Results.Problem(statusCode: 503,
                title: "The original document could not be read.");
        }
    }
}
