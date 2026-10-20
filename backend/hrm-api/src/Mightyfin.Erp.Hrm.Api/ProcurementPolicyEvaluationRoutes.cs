using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Request-specific rule evidence. These routes never approve spending.</summary>
public static class ProcurementPolicyEvaluationRoutes
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/requests");
        group.MapGet("/policy-queue", Queue).RequireAuthorization("procurement-policy-evaluate");
        group.MapGet("/{id:guid}/policy-status", OwnStatus).RequireAuthorization("procurement-request-read");
        group.MapPost("/{id:guid}/policy-evaluations", Evaluate)
            .RequireAuthorization("procurement-policy-evaluate");
    }

    private static async Task<IResult> Queue(ProcurementDbContext db, ProcurementRequestScope scope,
        int? limit, int? offset,
        CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var skip = Math.Clamp(offset ?? 0, 0, 100_000);
        var query = db.PurchaseRequests.AsNoTracking()
            .Where(x => x.Status == "Submitted" && x.ApprovalInstanceRef == null
                && !db.PolicyEvaluations.Any(e => e.PurchaseRequestId == x.Id
                    && e.RequestVersion == (x.SubmittedVersion ?? x.Version) && e.Outcome == "Matched"));
        if (scope.IsConfined)
            query = query.Where(x => x.BranchRef == scope.WorkLocationId);
        var count = await query.CountAsync(ct);
        var records = await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(skip).Take(take).Select(x => new
            {
                x.Id, x.Number, x.Purpose, x.CurrencyCode, x.SubmittedAt,
                x.Version, x.CreatedBy,
                PolicyOutcome = db.PolicyEvaluations.Where(e => e.PurchaseRequestId == x.Id
                    && e.RequestVersion == (x.SubmittedVersion ?? x.Version)).Select(e => e.Outcome).FirstOrDefault(),
                PolicyResultCode = db.PolicyEvaluations.Where(e => e.PurchaseRequestId == x.Id
                    && e.RequestVersion == (x.SubmittedVersion ?? x.Version)).Select(e => e.ResultCode).FirstOrDefault(),
                LineCount = x.Lines.Count(line => line.ApprovalStatus != "Cancelled"),
                EstimatedBaseAmount = x.Lines.Where(line => line.ApprovalStatus != "Cancelled")
                    .Sum(line => line.RequestedQuantity * line.EstimatedUnitPrice),
            }).ToListAsync(ct);
        return Results.Ok(new { records, count, limit = take, offset = skip });
    }

    private static async Task<IResult> OwnStatus(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == scope.SubjectId, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        var evaluation = await db.PolicyEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseRequestId == id
                && x.RequestVersion == (request.SubmittedVersion ?? request.Version), ct);
        return Results.Ok(new
        {
            requestId = id, requestVersion = request.Version, submittedVersion = request.SubmittedVersion,
            status = request.Status != "Submitted" ? "NotSubmitted"
                : evaluation?.Outcome ?? "Pending",
            isAuthority = false,
            evaluation = evaluation is null ? null : View(evaluation),
        });
    }

    private static async Task<IResult> Evaluate(Guid id, EvaluateInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, ct);
            var request = await db.PurchaseRequests.Include(x => x.Lines)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            if (request is null) return Error("not-found", "Request not found.", 404);
            if (scope.IsConfined && request.BranchRef != scope.WorkLocationId)
                return Error("not-found", "Request not found.", 404);
            if (request.Status != "Submitted")
                return Error("request-locked", "Only a submitted request can be checked.", 409);
            if (request.Version != input.RequestVersion)
                return Error("version-conflict", "Refresh this request before checking its buying rules.", 409);
            var assessment = await ProcurementPolicyEvaluation.Assess(request, db, scope,
                "ManualCheck", ct);
            if (assessment.Evaluation is { } evaluation && db.Entry(evaluation).State == EntityState.Added)
                await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(new
            {
                requestId = id, requestVersion = request.Version,
                status = assessment.Status, isAuthority = false,
                assessment.Message,
                evaluation = assessment.Evaluation is null ? null : View(assessment.Evaluation),
            });
        }
        catch (DbUpdateException)
        { return Error("evaluation-conflict", "Another rule check was recorded. Refresh the request.", 409); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Error("evaluation-conflict", "The request or buying rule changed. Refresh and check again.", 409); }
    }

    private static object View(PolicyEvaluation evaluation) => new
    {
        evaluation.Id, evaluation.RequestVersion, evaluation.PolicyVersionId,
        evaluation.PolicyVersionNumber, evaluation.AmountBasis,
        evaluation.Outcome, evaluation.ResultCode, evaluation.Summary,
        evaluation.EvaluatedAt,
        decision = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evaluation.DecisionJson),
    };

    private static IResult Error(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record EvaluateInput(long RequestVersion);
}
