using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Finance's evidence-backed budget decision. This never approves a purchase.</summary>
public static class ProcurementBudgetRoutes
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/requests");
        group.MapGet("/budget-queue", Queue).RequireAuthorization("procurement-budget-check");
        group.MapGet("/{id:guid}/budget-status", OwnStatus).RequireAuthorization("procurement-request-read");
        group.MapGet("/{id:guid}/budget-checks", List).RequireAuthorization("procurement-budget-check");
        group.MapPost("/{id:guid}/budget-checks", Record).RequireAuthorization("procurement-budget-check");
    }

    private static async Task<IResult> Queue(ProcurementDbContext db, ProcurementRequestScope scope,
        int? limit, int? offset,
        CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var skip = Math.Clamp(offset ?? 0, 0, 100_000);
        var query = db.PurchaseRequests.AsNoTracking()
            .Where(x => x.Status == "Submitted" && x.CreatedBy != scope.SubjectId);
        if (scope.IsConfined)
            query = query.Where(x => x.BranchRef == scope.WorkLocationId);
        var count = await query.CountAsync(ct);
        var records = await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip(skip).Take(take).Select(x => new
            {
                x.Id, x.Number, x.Purpose, x.CurrencyCode, x.SubmittedAt,
                x.Version, x.ApprovalState, x.CreatedBy,
                EstimatedAmount = x.Lines.Where(line => line.ApprovalStatus != "Cancelled")
                    .Sum(line => line.RequestedQuantity * line.EstimatedUnitPrice),
            }).ToListAsync(ct);
        return Results.Ok(new { records, count, limit = take, offset = skip });
    }

    private static async Task<IResult> List(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!await db.PurchaseRequests.AsNoTracking().AnyAsync(x => x.Id == id
                && (!scope.IsConfined || x.BranchRef == scope.WorkLocationId), ct))
            return Error("not-found", "Request not found.", 404);
        var records = await db.BudgetCheckSnapshots.AsNoTracking()
            .Where(x => x.PurchaseRequestId == id).OrderByDescending(x => x.CheckedAt)
            .Select(x => new { x.Id, x.RequestVersion, x.FinanceCheckRef, x.Status,
                x.CheckedAmount, x.CurrencyCode, x.CheckedAt, x.CheckedBySubjectId,
                x.EvidenceRef, x.DecisionReason, x.ReservationRef, x.ReservationStatus })
            .ToListAsync(ct);
        return Results.Ok(new { records });
    }

    private static async Task<IResult> OwnStatus(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == scope.SubjectId, ct);
        if (request is null || scope.IsConfined && request.BranchRef != scope.WorkLocationId)
            return Error("not-found", "Request not found.", 404);
        var latest = await db.BudgetCheckSnapshots.AsNoTracking()
            .Where(x => x.PurchaseRequestId == id && x.RequestVersion == (request.SubmittedVersion ?? request.Version))
            .OrderByDescending(x => x.CheckedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return Results.Ok(new
        {
            requestId = id, requestVersion = request.Version, submittedVersion = request.SubmittedVersion,
            status = latest?.Status ?? "Pending",
            checkedAt = latest?.CheckedAt,
            reservationConfirmed = latest?.Status == "Available"
                && latest.ReservationStatus == "Confirmed",
        });
    }

    private static async Task<IResult> Record(Guid id, BudgetDecision input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        try
        {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, ct);
        var request = await db.PurchaseRequests.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null || scope.IsConfined && request.BranchRef != scope.WorkLocationId)
            return Error("not-found", "Request not found.", 404);
        if (request.CreatedBy == scope.SubjectId)
            return Error("self-review", "The requester cannot confirm their own funding.", 403);
        if (request.Status != "Submitted")
            return Error("request-locked", "Only submitted requests can be checked.", 409);
        if (request.Version != input.RequestVersion)
            return Error("version-conflict", "Refresh this request before recording a budget decision.", 409);
        var financeRef = input.FinanceCheckRef?.Trim();
        var evidenceRef = input.EvidenceRef?.Trim();
        var reason = input.DecisionReason?.Trim();
        var reservationRef = input.ReservationRef?.Trim();
        var reservationStatus = input.ReservationStatus?.Trim();
        if (string.IsNullOrWhiteSpace(financeRef) || financeRef.Length > 200
            || string.IsNullOrWhiteSpace(evidenceRef) || evidenceRef.Length > 500
            || reason?.Length > 1000 || reservationRef?.Length > 200)
            return Error("validation-failed", "Enter a Finance reference and evidence reference within their field limits.", 422);
        if (input.Status is not ("Available" or "Insufficient" or "NotConfigured" or "PartiallyAvailable"))
            return Error("validation-failed", "Choose a supported Finance budget outcome.", 422);
        if (input.Status == "Available" && (string.IsNullOrWhiteSpace(reservationRef)
            || reservationStatus != "Confirmed"))
            return Error("validation-failed", "Available budget needs a confirmed Finance reservation reference.", 422);
        if (input.Status != "Available" && string.IsNullOrWhiteSpace(reason))
            return Error("validation-failed", "Explain why the budget is not fully available.", 422);
        decimal estimatedTotal;
        try { estimatedTotal = request.Lines.Where(x => x.ApprovalStatus != "Cancelled")
            .Sum(x => x.RequestedQuantity * x.EstimatedUnitPrice); }
        catch (OverflowException) { return Error("validation-failed", "Request amount is too large.", 422); }
        if (input.CheckedAmount < estimatedTotal || input.CheckedAmount > 99_999_999_999_999.9999m
            || decimal.Round(input.CheckedAmount, 4) != input.CheckedAmount
            || !string.Equals(input.CurrencyCode?.Trim(), request.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            return Error("validation-failed", "Check the full request amount in its selected currency.", 422);

        var businessVersion = request.SubmittedVersion ?? request.Version;
        var prior = await db.BudgetCheckSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.FinanceCheckRef == financeRef, ct);
        if (prior is not null)
        {
            if (prior.PurchaseRequestId == id && prior.RequestVersion == businessVersion
                && prior.Status == input.Status && prior.CheckedAmount == input.CheckedAmount
                && prior.CurrencyCode == request.CurrencyCode && prior.EvidenceRef == evidenceRef
                && prior.DecisionReason == reason && prior.ReservationRef == reservationRef
                && prior.ReservationStatus == reservationStatus)
                return Results.Ok(ToResult(prior));
            return Error("finance-reference-conflict", "This Finance check reference has already been used.", 409);
        }
        var decision = new BudgetCheckSnapshot
        {
            PurchaseRequestId = id, RequestVersion = businessVersion, FinanceCheckRef = financeRef,
            Status = input.Status, CheckedAmount = input.CheckedAmount,
            CurrencyCode = request.CurrencyCode, CheckedAt = DateTimeOffset.UtcNow,
            CheckedBySubjectId = scope.SubjectId, EvidenceRef = evidenceRef,
            DecisionReason = reason, ReservationRef = reservationRef,
            ReservationStatus = reservationStatus,
        };
        db.AddScoped(decision);
        db.AddScoped(new AuditEvent
        {
            RecordType = "PurchaseRequest", RecordId = id, Action = "BudgetChecked",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                requestVersion = businessVersion, sourceRevision = request.Version, financeRef, input.Status,
                input.CheckedAmount, request.CurrencyCode, evidenceRef,
                reservationRef, reservationStatus, reason,
            }),
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException) { return Error("finance-reference-conflict", "This Finance check reference has already been used.", 409); }
        catch (PostgresException error) when (error.SqlState is "40001" or "40P01")
        { return Error("budget-check-conflict", "The request or funding changed. Refresh before recording the Finance check.", 409); }
        return Results.Created($"/api/procurement/v1/requests/{id}/budget-checks", ToResult(decision));
        }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Error("budget-check-conflict", "The request or funding changed. Refresh before recording the Finance check.", 409); }
    }

    private static object ToResult(BudgetCheckSnapshot x) => new
    {
        x.Id, x.PurchaseRequestId, x.RequestVersion, x.FinanceCheckRef, x.Status,
        x.CheckedAmount, x.CurrencyCode, x.CheckedAt, x.CheckedBySubjectId,
        x.EvidenceRef, x.DecisionReason, x.ReservationRef, x.ReservationStatus,
    };
    private static IResult Error(string code, string message, int status) =>
        Results.Json(new ApiError(code, message, []), statusCode: status);

    public sealed record BudgetDecision(long RequestVersion, string? FinanceCheckRef,
        string? Status, decimal CheckedAmount, string? CurrencyCode, string? EvidenceRef,
        string? DecisionReason, string? ReservationRef, string? ReservationStatus);
}
