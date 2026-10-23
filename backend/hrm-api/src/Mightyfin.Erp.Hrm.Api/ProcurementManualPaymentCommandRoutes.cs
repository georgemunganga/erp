using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Unregistered P28 command adapter. The host must explicitly register these routes
/// only after the owning schema, finite Core grants and release checks are ready.</summary>
public static partial class ProcurementManualPaymentCommandRoutes
{
    private sealed class PaymentProblem(string code, int status) : Exception(code)
    { internal int Status => status; }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateInput(Guid BranchId, Guid InvoiceId,
        [property: JsonConverter(typeof(ManualPaymentAmountJsonConverter))] decimal Amount,
        string ApproverPrincipalId, string RecorderPrincipalId, string ConfirmerPrincipalId,
        string? Purpose, string? Title);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReviseInput(long ExpectedVersion,
        [property: JsonConverter(typeof(ManualPaymentAmountJsonConverter))] decimal Amount,
        string ApproverPrincipalId, string RecorderPrincipalId, string ConfirmerPrincipalId,
        string? Purpose, string? Title);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TransitionInput(long ExpectedVersion, string? Reason, DateOnly? PaidOn,
        string? ExternalMethod, string? ExternalReference,
        [property: JsonConverter(typeof(ManualPaymentOptionalAmountJsonConverter))] decimal? Amount,
        Guid? EvidenceDocumentRefId);

    public static void MapRoutes(WebApplication app)
    {
        var payments = app.MapGroup("/api/procurement/v1/manual-payments");
        payments.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        payments.MapPost("", Create).RequireAuthorization("procurement-payment-create");
        payments.MapPut("/{id:guid}", Revise).RequireAuthorization("procurement-payment-edit");
        payments.MapPost("/{id:guid}/submit", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Submit", body, http, scope, ct)).RequireAuthorization("procurement-payment-submit");
        payments.MapPost("/{id:guid}/approve", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Approve", body, http, scope, ct)).RequireAuthorization("procurement-payment-approve");
        payments.MapPost("/{id:guid}/return", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Return", body, http, scope, ct)).RequireAuthorization("procurement-payment-approve");
        payments.MapPost("/{id:guid}/reject", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Reject", body, http, scope, ct)).RequireAuthorization("procurement-payment-approve");
        payments.MapPost("/{id:guid}/record", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Record", body, http, scope, ct)).RequireAuthorization("procurement-payment-record");
        payments.MapPost("/{id:guid}/confirm", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Confirm", body, http, scope, ct)).RequireAuthorization("procurement-payment-confirm");
        payments.MapPost("/{id:guid}/dispute", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Dispute", body, http, scope, ct)).RequireAuthorization("procurement-payment-confirm");
        payments.MapPost("/{id:guid}/cancel", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => Transition(id, "Cancel", body, http, scope, ct)).RequireAuthorization("procurement-payment-cancel");
    }

    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<string>> command,
        CancellationToken ct, bool created = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            var readiness = http.RequestServices.GetService<IProcurementManualPaymentGovernanceReadiness>();
            var coordinator = http.RequestServices.GetService<ProcurementApprovalTransaction>();
            if (readiness is null || coordinator is null || !await readiness.IsReadyAsync(ct))
                throw new PaymentProblem("payment-review-unavailable", 503);
            var result = await coordinator.ExecuteManualPaymentAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                return await command(db, cases, core, http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
            }, ct);
            return Results.Content(result, "application/json", statusCode: created ? 201 : 200);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is PaymentProblem or InvalidOperationException or ArgumentException
            or UnauthorizedAccessException or KeyNotFoundException or JsonException or DbUpdateException
            or System.Data.Common.DbException or FormatException or OverflowException)
        {
            var status = error is PaymentProblem problem ? problem.Status
                : error is UnauthorizedAccessException ? 403
                : error is KeyNotFoundException ? 404
                : ProcurementPostgresFailures.IsConcurrency(error) || error is DbUpdateConcurrencyException ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503
                : error is InvalidOperationException { Message: "ManualPaymentDraftInvalid" or "ManualPaymentRecordingInvalid"
                    or "ManualPaymentReasonRequired" } ? 422 : 409;
            var code = error is PaymentProblem known ? known.Message : status == 503
                ? "payment-outcome-unavailable" : status == 403 ? "payment-access-denied"
                    : status == 404 ? "payment-not-found" : "payment-state-changed";
            var message = status == 503
                ? "The payment outcome could not be confirmed. Refresh this payment or recover it using the command key."
                : status == 400 ? "Provide one valid Idempotency-Key UUID for this payment command."
                : status == 422 ? "Enter valid payment details and exact amounts before trying again."
                : status == 403 ? "Your current account cannot perform this payment action."
                    : status == 404 ? "Payment request not found."
                        : "Review the current payment, invoice and approval task before trying again.";
            return Results.Json(new { code, message }, statusCode: status);
        }
    }

    private static Guid CommandKey(HttpContext http)
        => Guid.TryParseExact(http.Request.Headers["Idempotency-Key"].ToString(), "D", out var key) && key != Guid.Empty
            ? key : throw new PaymentProblem("payment-command-key-required", 400);
    private static string Fingerprint(string action, Guid? target, JsonElement body)
    {
        var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new { action, target, body }));
        return digest.Length == 64 ? digest : throw new PaymentProblem("payment-input-invalid", 422);
    }
    private static DateTimeOffset Now(IServiceProvider services)
        => ProcurementEvidenceEncoding.DatabaseTimestamp(services.GetRequiredService<TimeProvider>().GetUtcNow())!.Value;
    private static T Parse<T>(JsonElement body) where T : class
    {
        var parsed = ProcurementOriginalValueInput.Read<T>(body);
        return parsed.Value ?? throw new PaymentProblem("payment-input-invalid", 422);
    }
    private static string Permission(string action) => action switch
    {
        "Create" => "procurement-payment-create", "Revise" => "procurement-payment-edit",
        "Submit" => "procurement-payment-submit", "Approve" or "Return" or "Reject" => "procurement-payment-approve",
        "Record" => "procurement-payment-record", "Confirm" or "Dispute" => "procurement-payment-confirm",
        "Cancel" => "procurement-payment-cancel", _ => throw new PaymentProblem("payment-action-invalid", 422),
    };
    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService authorization, Guid branch, string action, CancellationToken ct)
    {
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        var permission = Permission(action);
        if (person is null || scope.PrincipalId?.ToString("D") != person.PrincipalId
            || scope.CoreLinkedWorkerId != person.CoreLinkedWorkerId || !person.Permissions.Contains(permission)
            || !(await authorization.AuthorizeAsync(http.User, null, permission)).Succeeded)
            throw new UnauthorizedAccessException();
        _ = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, ct);
        if (!person.OrganisationWide && !person.AllowedBranches.Contains(branch)) throw new KeyNotFoundException();
        return person;
    }
    private static async Task<ManualPaymentAssignee> Assignee(HrmDbContext core, ProcurementRequestScope scope,
        Guid branch, string principal, string action, CancellationToken ct)
    {
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, principal, ct);
        var permission = Permission(action);
        if (person is null || !person.Permissions.Contains(permission)
            || !person.OrganisationWide && !person.AllowedBranches.Contains(branch))
            throw new PaymentProblem("payment-assignee-unavailable", 422);
        return new(person.PrincipalId, scope.TenantId, scope.LegalEntityId,
            person.OrganisationWide ? new HashSet<Guid> { branch } : person.AllowedBranches,
            person.Permissions, true) { CoreLinkedWorkerId = person.CoreLinkedWorkerId };
    }
    private static ManualPaymentActor DomainActor(ProcurementPrincipalPeople.Person person,
        ProcurementRequestScope scope, Guid branch)
        => new(person.PrincipalId, scope.TenantId, scope.LegalEntityId, branch,
            person.OrganisationWide ? new HashSet<Guid> { branch } : person.AllowedBranches,
            person.Permissions, true) { CoreLinkedWorkerId = person.CoreLinkedWorkerId };

    private static async Task<ManualPaymentRequest> Current(ProcurementDbContext db, ProcurementRequestScope scope,
        Guid id, CancellationToken ct)
    {
        var peek = await db.ManualPaymentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
        // The invoice is the stable lock anchor for capacity, credits and Finance settlement.
        // The caller locks it before this method locks the request itself.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.manual_payment_requests WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} AND branch_id={peek.BranchId} FOR UPDATE", ct);
        return await db.ManualPaymentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();
    }
    private static async Task<ApprovalCase> CoreCase(ApprovalDbContext cases, ManualPaymentWorkflowCaseLink link,
        CancellationToken ct) => await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
        .SingleOrDefaultAsync(x => x.Id == link.CoreApprovalCaseId && x.SourceModule == "procurement"
            && x.SourceId == link.ManualPaymentRequestId && x.SourceVersion == link.OpenedRequestVersion, ct)
        ?? throw new PaymentProblem("payment-approval-task-unavailable", 409);
    private static async Task<ManualPaymentWorkflowCaseLink> CaseLink(ProcurementDbContext db, Guid requestId,
        string purpose, CancellationToken ct) => await db.ManualPaymentWorkflowCases.AsNoTracking()
        .Where(x => x.ManualPaymentRequestId == requestId && x.Purpose == purpose)
        .OrderByDescending(x => x.OpenedRequestVersion).FirstOrDefaultAsync(ct)
        ?? throw new PaymentProblem("payment-approval-task-unavailable", 409);

    private static async Task<string?> Replay(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, IAuthorizationService authorization, HttpContext http,
        Guid key, string action, string fingerprint, Guid? target, CancellationToken ct)
    {
        var receipt = await db.ManualPaymentReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, ct);
        if (receipt is null) return null;
        var original = await db.ManualPaymentRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == receipt.ManualPaymentRequestId, ct)
            ?? throw new KeyNotFoundException();
        _ = await Actor(core, scope, http, authorization, original.BranchId, action, ct);
        if (target is { } requested && original.Id != requested || receipt.Action != action
            || receipt.BranchId != original.BranchId || receipt.RequestSha256 != fingerprint)
            throw new PaymentProblem("payment-command-key-conflict", 409);
        return receipt.ResponseJson;
    }

    private static void Persist(ProcurementDbContext db, ProcurementRequestScope scope, ManualPaymentChange change,
        Guid commandKey, string fingerprint, string response, bool create)
    {
        if (create) db.AddScoped(change.Request);
        else
        {
            var current = db.ManualPaymentRequests.Single(x => x.Id == change.Request.Id);
            db.Entry(current).CurrentValues.SetValues(change.Request);
        }
        db.AddScoped(change.Decision);
        db.AddScoped(new ManualPaymentCommandReceipt { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, BranchId = change.Request.BranchId,
            ManualPaymentRequestId = change.Request.Id, ActorPrincipalId = scope.SubjectId,
            CommandKey = commandKey, Action = change.Decision.Action, RequestSha256 = fingerprint,
            ResultVersion = change.Decision.ToVersion, ResponseJson = response });
        db.AddScoped(new OutboxEvent { AggregateType = nameof(ManualPaymentRequest), AggregateId = change.Request.Id,
            EventType = "procurement.manual_payment.state_changed.v1", IdempotencyKey = commandKey.ToString("D"),
            PayloadJson = JsonSerializer.Serialize(new { requestId = change.Request.Id,
                action = change.Decision.Action, status = change.Request.Status, version = change.Request.Version }) });
        // ProcurementDbContext appends its normal record-level audit for every owner/decision/receipt/link.
    }
    private static string Response(ManualPaymentChange change, Guid key)
        => JsonSerializer.Serialize(new { id = change.Request.Id, version = change.Decision.ToVersion,
            status = change.Request.Status, action = change.Decision.Action, commandKey = key,
            amountExact = AuthorityAmountJsonConverter.Format(change.Request.Amount) });
}

/// <summary>Exact numeric(18,4) manual payment transport; both JSON numbers and strings
/// are checked before CLR decimal construction. Output always uses a decimal string.</summary>
public sealed class ManualPaymentAmountJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "ManualPaymentAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(AuthorityAmountJsonConverter.Format(value));
}
public sealed class ManualPaymentOptionalAmountJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : ExactDecimalJsonCodec.Read(ref reader, 18, 4, "ManualPaymentAmount");
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    { if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(AuthorityAmountJsonConverter.Format(value.Value)); }
}
