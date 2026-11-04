using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record RoundCreateInput(long ExpectedEventVersion, RoundPlanDraftInput? Draft);
    private sealed record RoundChangeInput(long ExpectedEventVersion, long ExpectedPlanVersion, RoundPlanDraftInput? Draft, string? Reason);
    private sealed class RoundScalarConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.String ? ExactDecimalJsonCodec.Read(ref reader, 18, 4, "RoundScalar")
                : throw new JsonException("Round scalars require exact decimal text.");
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteStringValue(ProcurementEvidenceEncoding.Decimal(value));
    }
    private static readonly JsonSerializerOptions RoundJson = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new RoundScalarConverter() } };
    private static void MapCommercialRoundRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/round-plan-sources", RoundSources).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/{id:guid}/round-plan/people", RoundPeople).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/round-plans", RoundList).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/{id:guid}/round-plan", RoundRead).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/{id:guid}/round-plan/history", RoundHistory).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/{id:guid}/round-plan/versions/{version:long}", RoundVersion).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/{id:guid}/round-plan/commands/{key:guid}", RoundRecover).RequireAuthorization("procurement-round-plan-read");
        group.MapGet("/round-plan/review-tasks", RoundReviewTasks)
            .RequireAuthorization(CommercialRoundReviewCoreWorkflow.ReviewPermission);
        group.MapGet("/{id:guid}/round-plan/reviews/{submissionId:guid}", RoundReviewDetail)
            .RequireAuthorization(CommercialRoundReviewCoreWorkflow.ReviewPermission);
        group.MapGet("/{id:guid}/round-plan/submit-context", RoundSubmitContext)
            .RequireAuthorization(CommercialRoundReviewCoreWorkflow.SubmitPermission);
        group.MapGet("/{id:guid}/round-plan/review-commands/{key:guid}", RoundReviewRecover)
            .RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/round-plan", RoundCreate).RequireAuthorization("procurement-round-plan-read");
        group.MapPost("/{id:guid}/round-plan/revise", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => RoundChange(id, body, "Revise", http, scope, ct)).RequireAuthorization("procurement-round-plan-read");
        group.MapPost("/{id:guid}/round-plan/cancel", (Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
            => RoundChange(id, body, "Cancel", http, scope, ct)).RequireAuthorization("procurement-round-plan-read");
        group.MapPost("/{id:guid}/round-plan/submit", RoundSubmit)
            .RequireAuthorization(CommercialRoundReviewCoreWorkflow.SubmitPermission);
        group.MapPost("/{id:guid}/round-plan/decisions", RoundDecide)
            .RequireAuthorization(CommercialRoundReviewCoreWorkflow.ReviewPermission);
    }
    internal static bool RoundPlanAllReadPath(PathString path, string method)
    {
        if (!HttpMethods.IsGet(method)) return false;
        var p = path.Value?.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (p is not { Length: >= 5 } || !p[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            || !p[1].Equals("procurement", StringComparison.OrdinalIgnoreCase) || !p[2].Equals("v1", StringComparison.OrdinalIgnoreCase)
            || !p[3].Equals("sourcing", StringComparison.OrdinalIgnoreCase)) return false;
        if (p.Length == 5) return p[4].Equals("round-plans", StringComparison.OrdinalIgnoreCase) || p[4].Equals("round-plan-sources", StringComparison.OrdinalIgnoreCase);
        if (p.Length == 6 && p[4].Equals("round-plan", StringComparison.OrdinalIgnoreCase)
            && p[5].Equals("review-tasks", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.Length < 6) return false;
        if (!Guid.TryParseExact(p[4], "D", out var id) || id == Guid.Empty || !p[5].Equals("round-plan", StringComparison.OrdinalIgnoreCase)) return false;
        return p.Length == 6 || p.Length == 7 && (p[6].Equals("history", StringComparison.OrdinalIgnoreCase) || p[6].Equals("people", StringComparison.OrdinalIgnoreCase))
            || p.Length == 8 && p[6].Equals("versions", StringComparison.OrdinalIgnoreCase) && long.TryParse(p[7], out var version) && version > 0
            || p.Length == 8 && p[6].Equals("commands", StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(p[7], "D", out var key) && key != Guid.Empty
            || p.Length == 8 && p[6].Equals("reviews", StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(p[7], "D", out var submission) && submission != Guid.Empty
            || p.Length == 8 && p[6].Equals("review-commands", StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(p[7], "D", out var reviewKey) && reviewKey != Guid.Empty;
    }
    private static async Task<IResult> RunRound(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<Reply>> action,
        CancellationToken ct, string entryPermission = "procurement-round-plan-read")
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var services = http.RequestServices; var readiness = services.GetRequiredService<IProcurementCommercialRoundReadiness>();
            if (!services.GetRequiredService<ProcurementCommercialRoundOptions>().ReadEnabled || !readiness.HasCapturedContract
                || !services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().ExecutionDatabaseConfigured || scope.OrgUnitId is not null)
                throw new Problem("RoundPlanStoreUnavailable", 503);
            var result = await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, cases, token) => {
                if (!await readiness.ReadyAsync(db, false, token)) throw new Problem("RoundPlanHistoryUnavailable", 503);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var auth = services.GetRequiredService<IAuthorizationService>();
                _ = await PrincipalActor(core, scope, http, auth, entryPermission, null, false, token);
                return await action(db, cases, core, auth, token);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (Exception error) when (error is Problem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or OverflowException or FormatException or DbUpdateException || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem p ? p.Status : ProcurementPostgresFailures.IsStoreFailure(error) ? 503
                : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404
                : error is JsonException or ArgumentException or FormatException ? 422 : 409;
            return Results.Json(new { code = error is Problem known ? known.Message : status == 503 ? "RoundPlanSaveUnconfirmed" : "RoundPlanNeedsReview",
                message = status == 503 ? "This action is not ready or its result could not be confirmed. Keep the original save reference."
                    : "Check the plan entries, current version and your access.", details = Array.Empty<string>() }, statusCode: status);
        }
    }
    private static async Task<SourcingEvent> RoundSource(ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, string permission, Guid id, CancellationToken ct)
    {
        // Reuse genuine principal source identity without adopting M4's maker-only access predicate.
        var source = await QuotationOwnedSources(db).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new Problem("RoundPlanNotFound", 404);
        _ = await PrincipalActor(core, scope, http, auth, permission, source.BranchRef, true, ct);
        if (source.BranchRef is null) throw new Problem("RoundPlanSourceInvalid", 409);
        return source;
    }
    private static void RoundWriteContext(HttpContext http, ProcurementRequestScope scope, SourcingEvent source)
    {
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is null || scope.WorkLocationId != source.BranchRef
            || http.Request.Headers["X-Shell-Branch-Scope"].Any(x => string.Equals(x, "all", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("An explicit current event branch is required.");
    }
    private static async Task RequireRoundRecord(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementCommercialRoundOptions>().RecordEnabled
            || !await http.RequestServices.GetRequiredService<IProcurementCommercialRoundReadiness>().ReadyAsync(db, true, ct))
            throw new Problem("RoundPlanRecordingUnavailable", 503);
    }
    private static T RoundInput<T>(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(body.GetRawText()) > 2000000) throw new JsonException("Round input invalid.");
        RoundNoDuplicateKeys(body);
        return body.Deserialize<T>(RoundJson) ?? throw new JsonException("Round input required.");
    }
    private static void RoundNoDuplicateKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException("Duplicate field."); RoundNoDuplicateKeys(property.Value); }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) RoundNoDuplicateKeys(item);
    }
    private static string RoundHash(string operation, Guid id, object input)
        => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new { schemaVersion = 1, operation, eventId = id, input }, RoundJson));
}
