using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    // A new request contract preserves the retained legacy request fingerprint.
    public sealed record PrincipalMappingInput(long Version, Guid PurchaseOrderId,
        Guid IssuedVersionId, PrincipalInvoiceMappingLine[] Lines);

    private static Task<IResult> RunPrincipal(HttpContext http, ProcurementRequestScope scope,
        bool write, Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext,
            IAuthorizationService, CancellationToken, Task<string>> work, CancellationToken ct)
        => !http.RequestServices.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().PrincipalSourcesEnabled
            ? Task.FromResult<IResult>(Results.Json(new { code = "PrincipalInvoiceMatchingUnavailable",
                message = "Invoice matching is not ready. Ask your administrator to check its configuration." }, statusCode: 503))
            : Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (!write && http.Request.Headers.ContainsKey("X-Shell-Branch-Scope") && !scope.IsExplicitAllBranches)
                throw new MatchingProblem("InvoiceMatchingSelectedBranchRequired", 422);
            await RequirePrincipalMatching(http, scope, db, cases, write, token);
            return await work(db, cases, core, auth, token);
        }, ct, deferSchema: true);

    private static async Task RequirePrincipalMatching(HttpContext http, ProcurementRequestScope scope,
        ProcurementDbContext db, ApprovalDbContext cases, bool write, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>().PrincipalSourcesEnabled)
            throw new MatchingProblem("PrincipalInvoiceMatchingUnavailable", 503);
        // Company All is a read context; a write still belongs to one real branch.
        if (write && (scope.IsExplicitAllBranches || http.Request.Headers.ContainsKey("X-Shell-Branch-Scope")))
            throw new MatchingProblem("InvoiceMatchingSelectedBranchRequired", 422);
        var baseline = http.RequestServices.GetRequiredService<IProcurementInvoiceOwnerBaselineReadiness>();
        if (!await baseline.CanReadAsync(db, ct)
            || !await ProcurementPrincipalInvoiceMatchingReadiness.IsReadyAsync(db, cases, write,
                http.RequestServices.GetRequiredService<IConfiguration>(), ct))
            throw new MatchingProblem("PrincipalInvoiceMatchingSchemaUnavailable", 503);
    }

    internal static async Task<HashSet<Guid>> PrincipalMatchingBranches(HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct) ?? throw new UnauthorizedAccessException();
        return (await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
            && (actor.OrganisationWide || actor.AllowedBranches.Contains(x.Id)))
            .Select(x => x.Id).ToArrayAsync(ct)).ToHashSet();
    }

    private static Task<IResult> MapPrincipal(Guid id, PrincipalMappingInput input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunPrincipal(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var key = Key(http);
            var fingerprint = Fingerprint(new { schemaVersion = 3, id, input });
            if (input.Lines is not { Length: > 0 and <= 200 })
                throw new MatchingProblem("InvoiceMappingLinesRequired", 422);
            var invoice = await Invoice(db, scope, id, token);
            var worker = await Actor(core, scope, http, auth, "procurement-invoice-capture", invoice.BranchRef, token);
            _ = await Target(db, core, scope, http, auth, "procurement-invoice-capture", invoice, input.PurchaseOrderId, token);
            if (await Replay(db, scope, "InvoiceMapping", key, fingerprint, id, input.PurchaseOrderId, token) is { } replay)
                return replay;
            var allowed = await PrincipalMatchingBranches(core, scope, token);
            // Upstream retained PR/PO/source locks precede the invoice write lock.
            var witness = await ProcurementPrincipalInvoiceIssuedWitness.LoadAsync(db, cases, scope,
                allowed, input.PurchaseOrderId, input.IssuedVersionId, token);
            var issued = await ProcurementPrincipalInvoiceIssuedWitness.LoadReviewIssuedOrderAsync(db, cases,
                scope, allowed, input.PurchaseOrderId, input.IssuedVersionId, token);
            invoice = await Invoice(db, scope, id, token, true);
            await db.Entry(invoice).ReloadAsync(token);
            if (invoice.Version != input.Version || invoice.ActiveMatchClaimResultId is not null)
                throw new MatchingProblem("InvoiceMappingVersionOrActiveClaimChanged", 409);
            if (await ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLockedAsync(db, invoice, true, token, input.PurchaseOrderId))
                throw new MatchingProblem(ProcurementPrincipalInvoiceRecordedComparisonGuard.Code, 409);
            var lines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var payload = PrincipalInvoiceMappingEvidence.Create(invoice, lines, input.PurchaseOrderId,
                input.IssuedVersionId, input.Lines, scope.SubjectId, worker);
            if (payload.Snapshot.Lines.Any(x => !issued.Lines.Any(y => y.Id == x.PurchaseOrderLineId)))
                throw new MatchingProblem("PrincipalInvoiceIssuedLineUnavailable", 409);
            var selected = payload.Snapshot.Lines.Where(x => x.PrincipalReceivingSourceStateId.HasValue)
                .Select(x => x.PrincipalReceivingSourceStateId!.Value).ToHashSet();
            if (selected.Any(x => !witness.Sources.ContainsKey(x)))
                throw new MatchingProblem("PrincipalInvoiceAcceptedFactUnavailable", 409);
            var histories = await db.LoadPrincipalInvoiceAcceptedSourcesAsync(
                witness.Sources.Where(x => selected.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value), token);
            PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot, histories);
            var mapping = new InvoiceMatchMappingVersion
            {
                SupplierInvoiceId = id, PurchaseOrderId = input.PurchaseOrderId,
                IssuedVersionId = input.IssuedVersionId, MappingRevision = payload.Snapshot.Revision,
                InvoiceOriginalSha256 = payload.Snapshot.InvoiceOriginalSha256,
                MappingJson = payload.Json, MappingSha256 = payload.Sha256,
                ActorPrincipalId = scope.SubjectId, ActorWorkerId = worker,
                IdentitySchemaVersion = 2, SourceSchemaVersion = 3, ReviewedAt = Now()
            };
            db.AddScoped(mapping);
            invoice.CurrentMappingVersionId = mapping.Id;
            invoice.MappingRevision = mapping.MappingRevision;
            invoice.MatchingState = "MappingUnverified";
            var evidence = JsonSerializer.Serialize(new { invoiceId = id, mappingId = mapping.Id,
                mapping.MappingRevision, mapping.MappingSha256, sourceSchemaVersion = 3, commandKey = key,
                canPostToFinance = false }, Json);
            db.AddScoped(new AuditEvent { RecordType = "SupplierInvoice", RecordId = id,
                ActorSubjectId = scope.SubjectId, Action = "PrincipalInvoiceMappingProposed",
                CorrelationId = key.ToString("D"), ChangedFieldsJson = evidence });
            db.AddScoped(new OutboxEvent { AggregateType = "SupplierInvoice", AggregateId = id,
                EventType = "supplier_invoice.mapping_proposed.v3", EventVersion = 3, IdempotencyKey = key.ToString("D"), PayloadJson = evidence });
            await db.SaveChangesAsync(token);
            var response = JsonSerializer.Serialize(new { invoiceId = id, commandKey = key,
                commandPermission = "procurement-invoice-capture", commandSchemaVersion = 3,
                sourceSchemaVersion = 3, recordedHistoricalResult = true, isCurrentApproval = false,
                canPostToFinance = false, result = new { mapping.Id, mapping.MappingRevision,
                    mapping.MappingSha256, mapping.VerificationState, invoice.Version, sourceSchemaVersion = 3 } }, Json);
            db.AddScoped(new ProcurementCommercialCommandReceipt { PurchaseOrderId = input.PurchaseOrderId,
                ActorPrincipalId = scope.SubjectId, Operation = "InvoiceMapping", CommandKey = key,
                Fingerprint = fingerprint, ResponseJson = response, CompletedAt = Now() });
            return response;
        }, ct);
}
