using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceReviewRoutes
{
    private static async Task<Guid[]> ReviewerBranches(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, CancellationToken ct)
    {
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null
                && !scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException();
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct) ?? throw new UnauthorizedAccessException();
        if (actor.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
            || !actor.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
            || !(await auth.AuthorizeAsync(http.User,
                InvoiceRetainedUnitReview.ReviewPermission)).Succeeded)
            throw new UnauthorizedAccessException();
        var declared = actor.AllowedBranches.ToArray();
        var branches = await core.WorkLocations.AsNoTracking().Where(x =>
            x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && !x.IsArchived && (actor.OrganisationWide || declared.Contains(x.Id))
                && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId))
            .Select(x => x.Id).ToArrayAsync(ct);
        if (branches.Length == 0) throw new UnauthorizedAccessException();
        return branches;
    }

    private static async Task<IResult> Tasks(int? page, int? pageSize, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50)
            return Results.Json(new { code = "PrincipalInvoiceReviewPageInvalid" }, statusCode: 422);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token))
                throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
            var branches = await ReviewerBranches(core, scope, http, auth, token);
            var query = db.PrincipalInvoiceUnitReviewSources.FromSqlInterpolated($"""
                SELECT DISTINCT r.* FROM procurement.principal_invoice_unit_reviews r
                JOIN procurement.principal_invoice_review_assignments p
                  ON p.tenant_id=r.tenant_id AND p.legal_entity_id=r.legal_entity_id
                    AND p.review_source_id=r.id AND p.core_case_id=r.core_case_id
                JOIN procurement.principal_invoice_states s
                  ON s.tenant_id=r.tenant_id AND s.legal_entity_id=r.legal_entity_id
                    AND s.supplier_invoice_id=r.supplier_invoice_id
                    AND s.current_mapping_id=r.mapping_version_id AND s.state='Mapped'
                JOIN core_approval.cases c ON c."Id"=r.core_case_id
                  AND c."TenantId"=r.tenant_id AND c."LegalEntityId"=r.legal_entity_id
                    AND c."SourceType"={PrincipalInvoiceUnitReviewTransitions.SourceType}
                    AND c."SourceId"=r.id AND c."SourceVersion"=1 AND c."State"='Pending'
                JOIN core_approval.assignments a ON a."Id"=p.core_assignment_id
                  AND a."TenantId"=c."TenantId" AND a."LegalEntityId"=c."LegalEntityId"
                    AND a."CaseId"=c."Id" AND a."AssigneeSubjectId"=p.reviewer_principal_id
                    AND a."PermissionKey"={InvoiceRetainedUnitReview.ReviewPermission}
                    AND a."State"='Pending'
                JOIN core_approval.stages g ON g."Id"=a."StageId" AND g."CaseId"=c."Id"
                  AND g."TenantId"=c."TenantId" AND g."LegalEntityId"=c."LegalEntityId"
                    AND g."State"='Active'
                WHERE r.tenant_id={scope.TenantId} AND r.legal_entity_id={scope.LegalEntityId}
                    AND p.reviewer_principal_id={scope.SubjectId}
                """).AsNoTracking().Where(x => branches.Contains(x.BranchId));
            var total = await query.CountAsync(token);
            var p = page ?? 1; var size = pageSize ?? 20;
            var items = await query.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id)
                .Skip((p - 1) * size).Take(size)
                .Select(x => new { reviewId = x.Id, invoiceId = x.SupplierInvoiceId,
                    mappingVersionId = x.MappingVersionId, branchId = x.BranchId,
                    openedAt = x.OpenedAt, sourceSchemaVersion = 2,
                    canPostToFinance = false }).ToArrayAsync(token);
            return new { items, total, page = p, pageSize = size,
                availabilityMeaning = "CurrentActiveAssignmentsOnly", canPostToFinance = false };
        }, ct);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token))
                throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
            var branches = await ReviewerBranches(core, scope, http, auth, token);
            var source = await db.PrincipalInvoiceUnitReviewSources.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && branches.Contains(x.BranchId)
                    && db.PrincipalInvoiceUnitReviewAssignments.Any(pin =>
                        pin.ReviewSourceId == x.Id && pin.ReviewerPrincipalId == scope.SubjectId), token)
                ?? throw new KeyNotFoundException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, source.BranchId, true, token);
            var invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x =>
                x.Id == source.SupplierInvoiceId && x.BranchRef == source.BranchId, token);
            var state = await db.PrincipalInvoiceStates.AsNoTracking().SingleAsync(x =>
                x.SupplierInvoiceId == invoice.Id, token);
            var mapping = await db.PrincipalInvoiceMappingVersions.AsNoTracking().SingleAsync(x =>
                x.Id == source.MappingVersionId && x.SupplierInvoiceId == invoice.Id, token);
            var pins = await db.PrincipalInvoiceUnitReviewAssignments.AsNoTracking()
                .Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleAsync(x => x.Id == source.CoreCaseId
                    && x.SourceId == source.Id
                    && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType, token);
            var opening = PrincipalInvoiceUnitReviewTransitions.Verify(source, mapping, workflow, pins);
            var originalLines = await db.SupplierInvoiceLines.AsNoTracking()
                .Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var originalCurrent = source.OriginalInvoiceSha256
                == InvoiceMappingEvidence.OriginalDigest(invoice, originalLines);
            var pending = workflow.State == "Pending" && state.CurrentMappingId == mapping.Id
                && originalCurrent;
            var assigned = workflow.Stages.Where(x => x.State == "Active")
                .SelectMany(x => x.Assignments)
                .Where(x => x.State == "Pending" && x.AssigneeSubjectId == scope.SubjectId
                    && pins.Any(pin => pin.CoreAssignmentId == x.Id
                        && pin.ReviewerPrincipalId == scope.SubjectId))
                .Select(x => new { assignmentId = x.Id, assignmentVersion = x.Version }).ToArray();
            if (assigned.Length == 0) throw new KeyNotFoundException();
            var independent = await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                source.MakerPrincipalId, source.MakerWorkerAliasId, scope.SubjectId,
                scope.CoreLinkedWorkerId, token);
            var blockers = new List<string>();
            if (!pending) blockers.Add("ReviewSourceChanged");
            if (!independent) blockers.Add("IndependentReviewerRequired");
            var hashes = await ProcurementPrincipalInvoiceExposureReader.CurrentDocumentHashes(db,
                scope, http.RequestServices, invoice.Id,
                opening.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray(), token);
            if (opening.Lines.Any(x => !hashes.TryGetValue(x.DocumentVersionId, out var sha)
                || sha != x.DocumentSha256)) blockers.Add("OriginalDocumentChanged");
            var active = blockers.Count == 0;
            return new { reviewId = id, invoiceId = invoice.Id, invoiceVersion = invoice.Version,
                mappingVersionId = mapping.Id, sourceSchemaVersion = 2,
                coreCaseId = workflow.Id, caseVersion = workflow.Version,
                caseState = workflow.State, reviewState = "Pending",
                originalDigestCurrent = originalCurrent, currentMapping = state.CurrentMappingId == mapping.Id,
                original = originalCurrent ? new { invoice.InvoiceNumber, invoice.InvoiceDate,
                    invoice.CurrencyCode, netAmount = Exact(invoice.NetAmount),
                    taxAmount = Exact(invoice.TaxAmount), grossAmount = Exact(invoice.GrossAmount),
                    lines = originalLines.Select(x => new { x.Id, x.LineNumber, x.Description,
                        quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitPrice),
                        taxAmount = Exact(x.TaxAmount) }).ToArray() } : null,
                originalDocumentLines = opening.Lines, blockers,
                assignments = active ? assigned : assigned.Where(_ => false).ToArray(),
                actionCandidates = active ? new[] { "Approve", "Return", "Reject" } : [],
                attestation = InvoiceUnitReviewCommands.OriginalUnitAttestation,
                actionCandidateMeaning = "CurrentAssignmentOnlyRecheckedOnDecision",
                canPostToFinance = false };
        }, ct);
    }

    private static string Exact(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}
