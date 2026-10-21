using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Governed internal draft holds only. Production schema readiness remains Unavailable.
/// No publication, Core case, supplier dispatch, bid, award or Finance reservation is created.</summary>
public static partial class ProcurementSourcingExecutionRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed class Problem(string code, int status) : Exception(code) { public int Status { get; } = status; }
    private sealed record Reply(string Body, int Status = 200);
    public sealed record Selection(Guid PurchaseRequestId, Guid PurchaseRequestLineId, long ExpectedRequestVersion,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal Quantity);
    public sealed record CreateInput(string Title, Selection[] Sources);
    public sealed record CancelInput(long ExpectedEventVersion, string OriginalDraftFingerprint, string Reason);

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/sourcing");
        group.MapPost("/from-approved-sources", Create).RequireAuthorization("procurement-sourcing-convert");
        group.MapGet("/approved-sources", ApprovedSources).RequireAuthorization("procurement-sourcing-convert");
        group.MapGet("/source-drafts", OwnedDrafts).RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/source-cancel", Cancel).RequireAuthorization("procurement-sourcing-cancel");
        group.MapGet("/{id:guid}/source-draft", Read).RequireAuthorization("procurement-access");
        group.MapGet("/command-receipts/{operation}/{key:guid}", Recover).RequireAuthorization("procurement-access");
    }
    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<Reply>> command, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var options = http.RequestServices.GetRequiredService<ProcurementSourcingDraftExecutionOptions>();
            if (!options.Enabled || !options.ExecutionDatabaseConfigured
                || !await http.RequestServices.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>().IsReadyAsync(ct))
                throw new Problem("SourcingDraftExecutionUnavailable", 503);
            if (scope.OrgUnitId is not null) throw new Problem("RfqDepartmentScopeUnsupported", 503);
            var result = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, cases, token) =>
            {
                if (!await http.RequestServices.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>()
                        .IsReadyAsync(db, !HttpMethods.IsGet(http.Request.Method), token))
                    throw new Problem("SourcingDraftExecutionUnavailable", 503);
                await RequireFinanceSchema(db, http.RequestServices.GetRequiredService<IProcurementFinanceCheckerSchemaReadiness>(), token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                return await command(db, cases, core, http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (ApprovedDemandLoadException error)
        {
            var failure = ProcurementDemandSourceFailures.ForSourcing(error.Failure);
            return Error(failure.Code, failure.Status);
        }
        catch (Exception error) when (error is Problem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or NpgsqlException or OverflowException or FormatException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var pg = ProcurementPostgresFailures.Postgres(error);
            var status = error is Problem problem ? problem.Status
                : ProcurementPostgresFailures.IsConcurrency(error) || pg?.SqlState == "23505" ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503
                : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404
                : error is JsonException or ArgumentException or FormatException ? 422 : 409;
            return Error(error is Problem ? error.Message : status == 503 ? "SourcingSaveUnconfirmed" : "SourcingEvidenceNeedsReview", status);
        }
    }
    // The coordinator's exact owning Procurement context is authoritative. Never substitute
    // the ordinary host connection when execution uses its dedicated finite connection.
    internal static async Task RequireFinanceSchema(ProcurementDbContext owner,
        IProcurementFinanceCheckerSchemaReadiness readiness, CancellationToken ct)
    {
        if (!await readiness.IsReadyAsync(owner, false, ct))
            throw new Problem("SourcingFinanceCheckerSchemaUnavailable", 503);
    }

    internal static IResult Error(string code, int status) => Results.Json(new { code,
        message = status == 503 ? "Quote requests are not ready, or this save could not be confirmed. Keep the original save reference and check again."
            : "Refresh the quote request and check your access, selected sources and available quantities.", details = Array.Empty<string>() }, statusCode: status);
    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) && key != Guid.Empty
        ? key : throw new Problem("SourcingCommandKeyRequired", 400);
    private static string Normalize(string? value) => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Fingerprint(object input) => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(input, Json));

    private static async Task<RfqCurrentActor> Actor(HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, string permission, Guid? branch, bool target, CancellationToken ct)
    {
        if (scope.OrgUnitId is not null) throw new Problem("RfqDepartmentScopeUnsupported", 503);
        if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, permission, ct) is null)
            throw new Problem("SourcingActionDenied", 403);
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct)
            ?? throw new Problem("SourcingCanonicalPersonRequired", 403);
        if (await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId, scope.LegalEntityId, account.WorkerId, ct) is null)
            throw new Problem("SourcingCanonicalPersonRequired", 403);
        Guid[] branches;
        try { branches = await ProcurementExecutionRecordScope.Branches(core, scope, branch, target, ct); }
        catch (KeyNotFoundException) { throw new Problem("SourcingNotFound", 404); }
        return new(account.PrincipalId, account.WorkerId, scope.TenantId, scope.LegalEntityId, scope.WorkLocationId,
            branches.ToHashSet(), branches.Length == 0 && !scope.IsConfined, new HashSet<string> { permission }, true, new HashSet<Guid>());
    }
    private static IQueryable<SourcingEvent> Owned(ProcurementDbContext db, RfqCurrentActor actor) =>
        from ev in db.SourcingEvents
        join original in db.SourcingDraftEvidenceRecords on ev.SourceEvidenceId equals (Guid?)original.Id
        where ev.ExecutionVersion == 1 && ev.OwnerSubjectId == actor.SubjectId && ev.MakerWorkerId == actor.WorkerId
            && original.SourcingEventId == ev.Id && original.MakerSubjectId == ev.OwnerSubjectId && original.MakerWorkerId == ev.MakerWorkerId
            && original.BranchRef == ev.BranchRef && original.OriginalDraftFingerprint == ev.OriginalDraftFingerprint
        select ev;
    private static async Task<SourcingEvent> OwnedEvent(ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, RfqCurrentActor actor, string permission, Guid id, CancellationToken ct)
    {
        var ev = await Owned(db, actor).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new Problem("SourcingNotFound", 404);
        _ = await Actor(core, scope, http, auth, permission, ev.BranchRef, true, ct);
        return ev;
    }
    private static async Task<Reply?> Replay(ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, RfqCurrentActor actor, string permission, string operation, Guid key, string? fingerprint, Guid? target, CancellationToken ct)
    {
        var receipt = await db.SourcingCommandReceipts.SingleOrDefaultAsync(x => x.Operation == operation && x.CommandKey == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorPrincipalId != actor.SubjectId || receipt.ActorWorkerId != actor.WorkerId) throw new Problem("SourcingReceiptNotFound", 404);
        _ = await OwnedEvent(db, core, scope, http, auth, actor, permission, receipt.SourcingEventId, ct);
        if (fingerprint is not null && receipt.CommandFingerprint != fingerprint || target is { } id && receipt.SourcingEventId != id)
            throw new Problem("SourcingCommandKeyConflict", 409);
        if (receipt.ResponseSchemaVersion != 1 || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            || receipt.ResponseStatusCode != (operation == "Create" ? 201 : 200)) throw new Problem("SourcingReceiptNeedsReview", 409);
        return new(receipt.ResponseJson, receipt.ResponseStatusCode);
    }
    private static async Task<Reply> Save(ProcurementDbContext db, ProcurementRequestScope scope, RfqCurrentActor actor,
        SourcingEvent ev, string operation, Guid key, string fingerprint, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var body = JsonSerializer.Serialize(new { eventId = ev.Id, ev.Number, status = ev.Status, version = ev.Version,
            originalDraftFingerprint = ev.OriginalDraftFingerprint, commandKey = key, recordedHistoricalResult = true,
            canPublish = false, supplierDeliveryState = "NotStarted", financeReservationChanged = false }, Json);
        db.AddScoped(new SourcingCommandReceipt { SourcingEventId = ev.Id, Operation = operation, CommandKey = key,
            ActorPrincipalId = scope.SubjectId, ActorWorkerId = actor.WorkerId, ResultVersion = ev.Version,
            ResponseStatusCode = operation == "Create" ? 201 : 200, CommandFingerprint = fingerprint,
            ResponseJson = body, ResponseSha256 = ProcurementEvidenceEncoding.Digest(body), CompletedAt = DateTimeOffset.UtcNow });
        return new(body, operation == "Create" ? 201 : 200);
    }
    private static void Record(ProcurementDbContext db, ProcurementRequestScope scope, SourcingEvent ev, string action, Guid key, object fields)
    {
        db.AddScoped(new AuditEvent { RecordType = "SourcingEvent", RecordId = ev.Id, Action = action,
            ActorSubjectId = scope.SubjectId, ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                executionVersion = ev.ExecutionVersion, sourceEvidenceId = ev.SourceEvidenceId,
                originalDraftFingerprint = ev.OriginalDraftFingerprint, commandKey = key, changes = fields,
            }, Json), CorrelationId = scope.CorrelationId });
        db.AddScoped(new OutboxEvent { AggregateType = "SourcingEvent", AggregateId = ev.Id,
            EventType = action == "DraftReserved" ? "sourcing.draft_reserved.v1" : "sourcing.draft_cancelled.v1",
            IdempotencyKey = $"rfq.{action}.{key:N}.v1", PayloadJson = JsonSerializer.Serialize(new { eventId = ev.Id, commandKey = key,
                internallyPublished = false, supplierDeliveryConfirmed = false, financeReservationChanged = false }, Json) });
    }
}
