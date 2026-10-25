using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Default-off P37 draft-only adapter. No submit, supplier send or counter mutation.</summary>
public static partial class ProcurementPrincipalReceivingRoutes
{
    private const string ReturnRead = "procurement-return-read";
    private const string ReturnCreate = "procurement-return-create";
    private const string ReturnEdit = "procurement-return-edit";
    public sealed record ReturnDraftInput(Guid AcceptanceFactId, string Quantity, string Reason, string? EvidenceReference);
    public sealed record ReturnRevisionInput(long ExpectedRevision, string Quantity, string Reason, string? EvidenceReference);
    public sealed record ReturnCancelInput(long ExpectedRevision, string Reason);

    /// <summary>Normal composition maps guarded routes; explicit options, real readiness and current action are checked per call.</summary>
    public static void MapReturnRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/returns/principal-drafts");
        group.MapGet("/eligible-goods", ReturnEligible).RequireAuthorization(ReturnCreate);
        group.MapGet("", ReturnList).RequireAuthorization(ReturnRead);
        group.MapGet("/commands/{key:guid}", ReturnRecover).RequireAuthorization("procurement-access");
        group.MapGet("/{id:guid}", ReturnDetail).RequireAuthorization(ReturnRead);
        group.MapPost("", ReturnCreateDraft).RequireAuthorization(ReturnCreate);
        group.MapPut("/{id:guid}", ReturnReviseDraft).RequireAuthorization(ReturnEdit);
        group.MapPost("/{id:guid}/cancel", ReturnCancelDraft).RequireAuthorization(ReturnEdit);
    }

    private static async Task<IResult> ReturnRun(HttpContext http, ProcurementRequestScope scope, string action,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, PrincipalReceivingActor, CancellationToken, Task<Reply>> operation,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl="private, no-store";
        try
        {
            var options=http.RequestServices.GetService<ProcurementPrincipalGoodsReturnOptions>();
            var readiness=http.RequestServices.GetService<IProcurementPrincipalGoodsReturnReadiness>();
            if(options is not {Enabled:true,ExecutionDatabaseConfigured:true} || readiness is null)
                throw new Problem("ReturnDraftUnavailable",503);
            // Own P37 writes do not mutate receiving/PO/PR counters. The concrete
            // provider checks P30 SOURCE READ on this exact transaction and its
            // separate return-write matrix. Existing Receiving Run stays unchanged.
            var result=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(
                async(db,cases,token)=>
                {
                    if(!await readiness.IsReadyAsync(db,!HttpMethods.IsGet(http.Request.Method),token))
                        throw new Problem("ReturnDraftUnavailable",503);
                    await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                    var actor=await ProcurementPrincipalReceivingActor.CurrentAsync(core,scope,http,
                        http.RequestServices.GetRequiredService<IAuthorizationService>(),action,token);
                    return await operation(db,cases,core,actor,token);
                },ct);
            return Results.Content(result.Body,"application/json",statusCode:result.Status);
        }
        catch(OperationalPolicyPublicationProblem error) { return Error(error.Code,error.Status); }
        catch(Exception error) when(error is Problem or InvalidOperationException or UnauthorizedAccessException
            or KeyNotFoundException or ArgumentException or JsonException or DbUpdateException or OverflowException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status=error is Problem problem?problem.Status:ProcurementPostgresFailures.IsConcurrency(error)?409:
                ProcurementPostgresFailures.IsStoreFailure(error)?503:error is UnauthorizedAccessException?403:409;
            return Error(error is Problem?error.Message:status==503?"ReturnDraftSaveUnconfirmed":"ReturnDraftNeedsReview",status);
        }
    }

    private static async Task<(PrincipalReceivingSourceState State, PrincipalReceivingAcceptance Acceptance, PrincipalReceivingFact Fact, ReturnSourceLabels Labels)>
        AcceptedGoods(ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
            PrincipalReceivingActor actor, Guid factId, CancellationToken ct)
    {
        var branches = actor.AllowedBranches.ToArray();
        var selected = await (from fact in db.PrincipalReceivingFacts.AsNoTracking()
            join state in db.PrincipalReceivingSourceStates.AsNoTracking() on fact.SourceStateId equals state.Id
            where fact.Id == factId && fact.Kind == "Acceptance" && fact.Operation == "Accept" &&
                state.Kind == "Goods" && branches.Contains(state.BranchId) && fact.BranchId == state.BranchId
            select state).SingleOrDefaultAsync(ct)
            ?? throw new Problem("ReturnSourceNotFound", 404);
        var source = PrincipalReceivingStorageCodec.Source(selected);
        var loaded = await SourceHistory(db, cases, scope, actor, source, false, ct);
        var acceptance = loaded.Ledger.Acceptances.SingleOrDefault(x => x.Id == factId && x.ObservationId is not null)
            ?? throw new Problem("ReturnAcceptedGoodsRequired", 409);
        var factRow = db.PrincipalReceivingFacts.Local.Single(x => x.Id == factId);
        var observation = loaded.Ledger.Observations.Single(x => x.Id == acceptance.ObservationId);
        if (loaded.State.Id != selected.Id || VerifiedReturnReceiptId(selected, factRow, acceptance, observation) is null)
            throw new Problem("ReturnAcceptedGoodsRequired", 409);
        return (loaded.State, acceptance, factRow, VerifiedReturnLabels(loaded.Retained, observation));
    }

    private static async Task<(PrincipalGoodsReturnDraft Head, PrincipalGoodsReturnRevision Revision, ReturnSourceLabels Labels)> ReturnHead(
        ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        PrincipalReceivingActor actor, Guid id, bool creator, CancellationToken ct)
    {
        var branches = actor.AllowedBranches.ToArray();
        var head = await db.PrincipalGoodsReturnDrafts.SingleOrDefaultAsync(x => x.Id == id && branches.Contains(x.BranchId), ct)
            ?? throw new Problem("ReturnDraftNotFound", 404);
        if (creator && head.CreatorPrincipalId != actor.PrincipalId) throw new Problem("ReturnDraftNotFound", 404);
        var source = await AcceptedGoods(db, cases, scope, actor, head.AcceptanceFactId, ct);
        if (source.State.Id != head.SourceStateId || source.State.SourceSha256 != head.SourceSha256 ||
            source.Fact.PayloadSha256 != head.AcceptanceSha256 || source.Acceptance.Quantity != head.AcceptedFactQuantity ||
            source.Fact.DraftId != head.ReceiptId || source.State.PurchaseOrderId != head.PurchaseOrderId ||
            source.State.PurchaseOrderLineId != head.PurchaseOrderLineId || source.State.IssuedVersionId != head.IssuedVersionId)
            throw new Problem("ReturnOriginalSourceChanged", 409);
        var revisions = await db.PrincipalGoodsReturnRevisions.Where(x => x.DraftId == id).OrderBy(x => x.Revision).ToArrayAsync(ct);
        if (revisions.Length != head.Revision || revisions[^1].Id != head.CurrentRevisionId ||
            revisions[^1].State != head.State || revisions.Any(x => x.Revision < 1 ||
                x.ProposalSha256 != PrincipalGoodsReturnRules.Fingerprint(head.Id, x.Revision,
                    x.Quantity, x.Reason, x.EvidenceReference, x.State)))
            throw new Problem("ReturnDraftHistoryInvalid", 409);
        for (var i = 0; i < revisions.Length; i++)
        {
            if (revisions[i].Revision != i + 1 || revisions[i].ActorPrincipalId != head.CreatorPrincipalId ||
                revisions[i].Quantity > head.AcceptedFactQuantity ||
                (i < revisions.Length - 1 && revisions[i].State != "Draft"))
                throw new Problem("ReturnDraftHistoryInvalid", 409);
            if (revisions[i].State == "Cancelled" &&
                (i == 0 || !PrincipalGoodsReturnRules.CancellationPreservesPrior(revisions[i - 1], revisions[i])))
                throw new Problem("ReturnDraftHistoryInvalid", 409);
        }
        var receipts = await db.PrincipalGoodsReturnCommandReceipts.AsNoTracking()
            .Where(x => x.DraftId == id).ToArrayAsync(ct);
        if (receipts.Length != revisions.Length || revisions.Any(x => !receipts.Any(r =>
            PrincipalGoodsReturnRules.MatchesReceipt(head, x, r))))
            throw new Problem("ReturnDraftHistoryInvalid", 409);
        return (head, revisions[^1], source.Labels);
    }

    private static Reply ReturnComplete(ProcurementDbContext db, PrincipalGoodsReturnDraft head,
        PrincipalGoodsReturnRevision revision, string operation, Guid key, string actor, string requestJson, int status)
    {
        var body = PrincipalGoodsReturnRules.ResponseJson(head, revision);
        db.AddScoped(new PrincipalGoodsReturnCommandReceipt
        {
            DraftId = head.Id, BranchId = head.BranchId, CommandKey = key, Operation = operation,
            ActorPrincipalId = actor, RequestJson = requestJson,
            RequestSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(requestJson), ResponseJson = body,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(body), ResponseStatus = status, ResultRevision = head.Revision
        });
        db.AddScoped(new OutboxEvent { AggregateType = "PrincipalGoodsReturnDraft", AggregateId = head.Id,
            EventType = $"procurement.goods_return.draft_{operation.ToLowerInvariant()}.v1",
            IdempotencyKey = $"goods-return.draft.{key:N}.v1",
            PayloadJson = PrincipalGoodsReturnRules.OutboxPayloadJson(head.Id, operation, key, head.Revision, actor) });
        return new(status, body);
    }

    private static decimal ReturnQuantity(string text, decimal accepted)
    {
        try { return PrincipalGoodsReturnRules.Quantity(text, accepted); }
        catch (Exception error) when (error is ArgumentException or JsonException or OverflowException)
        { throw new Problem("ReturnDraftQuantityInvalid", 422); }
    }

    private static async Task<Reply?> ReturnReplay(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, PrincipalReceivingActor actor, string operation, Guid key, string? requestSha, CancellationToken ct)
    {
        var row = await db.PrincipalGoodsReturnCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == key, ct);
        if (row is null) return null;
        if (row.ActorPrincipalId != actor.PrincipalId || !actor.AllowedBranches.Contains(row.BranchId))
            throw new Problem("ReturnCommandNotFound", 404);
        if (row.Operation != operation || requestSha is not null && row.RequestSha256 != requestSha)
            throw new Problem("ReturnCommandKeyConflict", 409);
        _ = await ReturnHead(db, cases, scope, actor, row.DraftId, true, ct);
        return new(row.ResponseStatus, row.ResponseJson);
    }

    private static Task<IResult> ReturnEligible(HttpContext http, ProcurementRequestScope scope, CancellationToken ct, int offset = 0)
        => ReturnRun(http, scope, ReturnCreate, async (db, cases, _, actor, token) =>
        {
            if (offset is < 0 or > 10000) throw new Problem("ReturnSourcePageInvalid", 422);
            var branches = actor.AllowedBranches.ToArray();
            var ids = await (from fact in db.PrincipalReceivingFacts.AsNoTracking()
                join state in db.PrincipalReceivingSourceStates.AsNoTracking() on fact.SourceStateId equals state.Id
                where fact.Kind == "Acceptance" && fact.Operation == "Accept" && state.Kind == "Goods" &&
                    branches.Contains(state.BranchId) && fact.BranchId == state.BranchId && fact.Quantity > 0
                orderby fact.Id select fact.Id).Skip(offset).Take(26).ToArrayAsync(token);
            var items = new List<object>();
            foreach (var id in ids.Take(25))
            {
                var loaded = await AcceptedGoods(db, cases, scope, actor, id, token);
                items.Add(new { acceptanceFactId = id, receiptId = loaded.Fact.DraftId,
                    purchaseOrderId = loaded.State.PurchaseOrderId, purchaseOrderLineId = loaded.State.PurchaseOrderLineId,
                    branchId = loaded.State.BranchId, acceptedFactQuantityExact = Exact(loaded.Acceptance.Quantity),
                    labels = loaded.Labels });
            }
            return new Reply(200, JsonSerializer.Serialize(new { items, nextOffset = ids.Length > 25 ? offset + 25 : (int?)null,
                hasMore = ids.Length > 25 }, Json));
        }, ct);

    private static Task<IResult> ReturnList(HttpContext http, ProcurementRequestScope scope, CancellationToken ct, int offset = 0)
        => ReturnRun(http, scope, ReturnRead, async (db, cases, _, actor, token) =>
        {
            if (offset is < 0 or > 10000) throw new Problem("ReturnDraftPageInvalid", 422);
            var editAuthorized = (await http.RequestServices.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(http.User, ReturnEdit)).Succeeded;
            var branches = actor.AllowedBranches.ToArray();
            var ids = await db.PrincipalGoodsReturnDrafts.AsNoTracking().Where(x => branches.Contains(x.BranchId))
                .OrderBy(x => x.Id).Select(x => x.Id).Skip(offset).Take(26).ToArrayAsync(token);
            var items = new List<object>();
            foreach (var id in ids.Take(25)) { var value = await ReturnHead(db, cases, scope, actor, id, false, token);
                items.Add(ReturnView(value.Head, value.Revision, actor, editAuthorized, value.Labels)); }
            return new Reply(200, JsonSerializer.Serialize(new { items, nextOffset = ids.Length > 25 ? offset + 25 : (int?)null,
                hasMore = ids.Length > 25 }, Json));
        }, ct);

    private static Task<IResult> ReturnDetail(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => ReturnRun(http, scope, ReturnRead, async (db, cases, _, actor, token) =>
        { var value = await ReturnHead(db, cases, scope, actor, id, false, token);
            var editAuthorized = (await http.RequestServices.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(http.User, ReturnEdit)).Succeeded;
            return new Reply(200, JsonSerializer.Serialize(ReturnView(value.Head, value.Revision, actor, editAuthorized, value.Labels), Json)); }, ct);

    private static Task<IResult> ReturnCreateDraft(ReturnDraftInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => ReturnRun(http, scope, ReturnCreate, async (db, cases, _, actor, token) =>
        {
            if (input is null) throw new Problem("ReturnDraftInputRequired", 422);
            var key = Key(http); var request = JsonSerializer.Serialize(new { operation = "Create", input }, Json);
            var hash = ProcurementEvidenceEncoding.PolicyJsonDigest(request);
            if (await ReturnReplay(db, cases, scope, actor, "Create", key, hash, token) is { } replay) return replay;
            var selected = await AcceptedGoods(db, cases, scope, actor, input.AcceptanceFactId, token);
            var quantity = ReturnQuantity(input.Quantity, selected.Acceptance.Quantity);
            var reason = input.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 2000 || input.EvidenceReference?.Length > 300)
                throw new Problem("ReturnDraftReasonRequired", 422);
            var head = new PrincipalGoodsReturnDraft { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                BranchId = selected.State.BranchId, SourceStateId = selected.State.Id, AcceptanceFactId = selected.Fact.Id,
                ReceiptId = selected.Fact.DraftId, PurchaseOrderId = selected.State.PurchaseOrderId,
                PurchaseOrderLineId = selected.State.PurchaseOrderLineId, IssuedVersionId = selected.State.IssuedVersionId,
                SourceSha256 = selected.State.SourceSha256, AcceptanceSha256 = selected.Fact.PayloadSha256,
                AcceptedFactQuantity = selected.Acceptance.Quantity, CreatorPrincipalId = actor.PrincipalId,
                CreatorWorkerAliasId = actor.WorkerAliasId };
            var revision = new PrincipalGoodsReturnRevision { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                DraftId = head.Id, Revision = 1, CommandKey = key, ActorPrincipalId = actor.PrincipalId,
                Quantity = quantity, Reason = reason, EvidenceReference = input.EvidenceReference?.Trim(),
                ProposalSha256 = PrincipalGoodsReturnRules.Fingerprint(head.Id, 1, quantity, reason, input.EvidenceReference?.Trim(), "Draft") };
            head.CurrentRevisionId = revision.Id; db.AddScoped(head); db.AddScoped(revision);
            return ReturnComplete(db, head, revision, "Create", key, actor.PrincipalId, request, 201);
        }, ct);

    private static Task<IResult> ReturnReviseDraft(Guid id, ReturnRevisionInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => ReturnRun(http, scope, ReturnEdit, async (db, cases, _, actor, token) =>
        {
            if (input is null) throw new Problem("ReturnDraftInputRequired", 422);
            var key = Key(http); var request = JsonSerializer.Serialize(new { operation = "Revise", id, input }, Json);
            var hash = ProcurementEvidenceEncoding.PolicyJsonDigest(request);
            if (await ReturnReplay(db, cases, scope, actor, "Revise", key, hash, token) is { } replay) return replay;
            var (head, _, _) = await ReturnHead(db, cases, scope, actor, id, true, token);
            if (head.State != "Draft" || head.Revision != input.ExpectedRevision) throw new Problem("ReturnDraftChanged", 409);
            var quantity = ReturnQuantity(input.Quantity, head.AcceptedFactQuantity);
            var reason = input.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 2000 || input.EvidenceReference?.Length > 300)
                throw new Problem("ReturnDraftReasonRequired", 422);
            var next = checked(head.Revision + 1);
            var revision = new PrincipalGoodsReturnRevision { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                DraftId = head.Id, Revision = next, CommandKey = key, ActorPrincipalId = actor.PrincipalId,
                Quantity = quantity, Reason = reason, EvidenceReference = input.EvidenceReference?.Trim(),
                ProposalSha256 = PrincipalGoodsReturnRules.Fingerprint(id, next, quantity, reason, input.EvidenceReference?.Trim(), "Draft") };
            head.Revision = next; head.CurrentRevisionId = revision.Id; db.AddScoped(revision);
            return ReturnComplete(db, head, revision, "Revise", key, actor.PrincipalId, request, 200);
        }, ct);

    private static Task<IResult> ReturnCancelDraft(Guid id, ReturnCancelInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => ReturnRun(http, scope, ReturnEdit, async (db, cases, _, actor, token) =>
        {
            if (input is null) throw new Problem("ReturnDraftInputRequired", 422);
            var key = Key(http); var request = JsonSerializer.Serialize(new { operation = "Cancel", id, input }, Json);
            var hash = ProcurementEvidenceEncoding.PolicyJsonDigest(request);
            if (await ReturnReplay(db, cases, scope, actor, "Cancel", key, hash, token) is { } replay) return replay;
            var (head, prior, _) = await ReturnHead(db, cases, scope, actor, id, true, token);
            if (head.State != "Draft" || head.Revision != input.ExpectedRevision) throw new Problem("ReturnDraftChanged", 409);
            var reason = input.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 2000) throw new Problem("ReturnDraftReasonRequired", 422);
            var next = checked(head.Revision + 1);
            var revision = new PrincipalGoodsReturnRevision { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                DraftId = head.Id, Revision = next, CommandKey = key, ActorPrincipalId = actor.PrincipalId,
                Quantity = prior.Quantity, Reason = reason, EvidenceReference = prior.EvidenceReference, State = "Cancelled",
                ProposalSha256 = PrincipalGoodsReturnRules.Fingerprint(id, next, prior.Quantity, reason, prior.EvidenceReference, "Cancelled") };
            head.Revision = next; head.CurrentRevisionId = revision.Id; head.State = "Cancelled"; db.AddScoped(revision);
            return ReturnComplete(db, head, revision, "Cancel", key, actor.PrincipalId, request, 200);
        }, ct);

    private static Task<IResult> ReturnRecover(Guid key, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => ReturnRun(http, scope, "procurement-access", async (db, cases, core, actor, token) =>
        {
            var row = await db.PrincipalGoodsReturnCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.CommandKey == key, token)
                ?? throw new Problem("ReturnCommandNotFound", 404);
            if (row.ActorPrincipalId != actor.PrincipalId || !actor.AllowedBranches.Contains(row.BranchId))
                throw new Problem("ReturnCommandNotFound", 404);
            var originalAction = row.Operation == "Create" ? ReturnCreate : row.Operation is "Revise" or "Cancel" ? ReturnEdit
                : throw new Problem("ReturnCommandNotFound", 404);
            var originalActor = await ProcurementPrincipalReceivingActor.CurrentAsync(core, scope, http,
                http.RequestServices.GetRequiredService<IAuthorizationService>(), originalAction, token);
            var recorded = await ReturnReplay(db, cases, scope, originalActor, row.Operation, key, null, token)
                ?? throw new Problem("ReturnCommandNotFound", 404);
            return new Reply(200, recorded.Body);
        }, ct);
}

public sealed class ProcurementPrincipalGoodsReturnOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementPrincipalGoodsReturnReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext db, bool write, CancellationToken ct);
    Task<bool> IsConfiguredAsync(CancellationToken ct) => Task.FromResult(false);
}
