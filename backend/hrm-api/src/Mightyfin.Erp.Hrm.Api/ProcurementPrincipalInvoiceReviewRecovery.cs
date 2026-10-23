using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceReviewRoutes
{
    private static async Task<IResult> Recover(Guid id, Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (key == Guid.Empty || http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>()
                is not { Enabled: true }
            || http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not { } ready
            || !await ready.IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceReviewUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!await ready.IsReadyAsync(db, token)) throw new InvalidOperationException("PrincipalInvoiceReviewSchemaUnavailable");
            var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token) ?? throw new UnauthorizedAccessException();
            if (actor.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || scope.OrgUnitId is not null
                || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
                throw new UnauthorizedAccessException();
            var declared = actor.AllowedBranches.ToArray();
            var visible = await core.WorkLocations.AsNoTracking().Where(x =>
                x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
                    && (actor.OrganisationWide || declared.Contains(x.Id))
                    && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId))
                .Select(x => x.Id).ToArrayAsync(token);
            if (visible.Length == 0) throw new UnauthorizedAccessException();
            var source = await db.PrincipalInvoiceUnitReviewSources.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && visible.Contains(x.BranchId), token)
                ?? throw new KeyNotFoundException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, source.BranchId, true, token);
            var row = await db.PrincipalInvoiceCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.SupplierInvoiceId == source.SupplierInvoiceId && x.CommandKey == key
                    && (x.Operation == "OpenReview" || x.Operation == "DecideReview"), token)
                ?? throw new KeyNotFoundException();
            var permission = row.Operation == "OpenReview"
                ? InvoiceUnitReviewTransitions.RequestPermission
                : InvoiceRetainedUnitReview.ReviewPermission;
            if (!actor.Permissions.Contains(permission)
                || !(await auth.AuthorizeAsync(http.User, permission)).Succeeded
                || row.ActorPrincipalId != scope.SubjectId
                || row.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || row.BranchId != source.BranchId || row.RequestSha256 is not { Length: 64 }
                || row.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(row.ResponseJson))
                throw new KeyNotFoundException();
            using var parsed = JsonDocument.Parse(row.ResponseJson);
            var response = parsed.RootElement;
            if (response.GetProperty("reviewId").GetGuid() != source.Id
                || response.GetProperty("invoiceId").GetGuid() != source.SupplierInvoiceId
                || response.GetProperty("commandKey").GetGuid() != key
                || response.GetProperty("sourceSchemaVersion").GetInt32() != 2)
                throw new InvalidOperationException("PrincipalInvoiceReviewReceiptChanged");
            var mapping = await db.PrincipalInvoiceMappingVersions.AsNoTracking().SingleAsync(x =>
                x.Id == source.MappingVersionId && x.SupplierInvoiceId == source.SupplierInvoiceId, token);
            if (response.GetProperty("invoiceVersion").GetInt64() != mapping.InvoiceVersion)
                throw new InvalidOperationException("PrincipalInvoiceReviewReceiptVersionChanged");
            if (row.Operation == "OpenReview")
            {
                if (source.MakerPrincipalId != scope.SubjectId
                    || source.MakerWorkerAliasId != scope.CoreLinkedWorkerId
                    || response.GetProperty("coreCaseId").GetGuid() != source.CoreCaseId)
                    throw new InvalidOperationException("PrincipalInvoiceReviewOpeningReceiptChanged");
            }
            else
            {
                var decisionId = response.GetProperty("decisionId").GetGuid();
                var decision = await cases.Decisions.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == decisionId && x.CaseId == source.CoreCaseId
                        && x.ActorSubjectId == scope.SubjectId, token);
                if (decision is null || response.GetProperty("coreCaseId").GetGuid() != source.CoreCaseId)
                    throw new InvalidOperationException("PrincipalInvoiceReviewDecisionReceiptChanged");
                var terminalId = response.GetProperty("outcomeId");
                if (terminalId.ValueKind != JsonValueKind.Null
                    && !await db.PrincipalInvoiceUnitReviewOutcomes.AsNoTracking().AnyAsync(x =>
                        x.Id == terminalId.GetGuid() && x.ReviewSourceId == source.Id
                            && x.FinalDecisionId == decisionId, token))
                    throw new InvalidOperationException("PrincipalInvoiceReviewOutcomeReceiptChanged");
            }
            return JsonSerializer.Deserialize<JsonElement>(row.ResponseJson, Json);
        }, ct);
    }
}
