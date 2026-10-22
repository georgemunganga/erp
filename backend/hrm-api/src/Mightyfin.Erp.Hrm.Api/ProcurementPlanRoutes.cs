using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Own-department procurement forecasts. These drafts do not reserve budget or authorize buying.</summary>
public static class ProcurementPlanRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/plans");
        group.MapGet("", List).RequireAuthorization("procurement-plan-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-plan-read");
        group.MapPost("", Create).Accepts<ProcurementPlanInput>("application/json").RequireAuthorization("procurement-plan-create");
        group.MapPut("/{id:guid}", Update).Accepts<UpdatePlan>("application/json").RequireAuthorization("procurement-plan-edit");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-plan-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.ProcurementPlans.AsNoTracking().Where(x => x.OwnerSubjectId == scope.SubjectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.FinancialPeriodRef, x.DepartmentRef,
                x.CurrencyCode, x.Status, x.RevisionNumber, x.Version, x.CreatedAt,
                LineCount = db.ProcurementPlanLines.Count(line => line.ProcurementPlanId == x.Id && line.Status == "Active") })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var plan = await db.ProcurementPlans.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        return plan is null ? Problem("not-found", "Plan not found.", 404) : Results.Ok(Detail(plan));
    }

    private static async Task<IResult> Create(JsonElement body, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<ProcurementPlanInput>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = ProcurementPlanRules.Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (input.Lines!.Any(x => x.Id.HasValue))
            return Problem("validation-failed", "New plan lines cannot carry an existing line ID.", 422);
        var referenceIssue = await ValidateReferences(input.Lines!, db, ct);
        if (referenceIssue is not null) return Problem("validation-failed", referenceIssue, 422);
        var plan = new ProcurementPlan
        {
            Number = "PLAN-" + Guid.CreateVersion7().ToString("N"),
            FinancialPeriodRef = input.FinancialPeriodRef!.Trim(),
            CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(),
            FundingSourceRef = Clean(input.FundingSourceRef),
            DepartmentRef = scope.OrgUnitId,
            OwnerSubjectId = scope.SubjectId,
            Status = "Draft",
        };
        for (var i = 0; i < input.Lines!.Count; i++)
            plan.Lines.Add(NewLine(plan.Id, i + 1, input.Lines[i]));
        db.AddScoped(plan);
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftCreated", null, Detail(plan));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/plans/{plan.Id}", Detail(plan));
    }

    private static async Task<IResult> Update(Guid id, JsonElement body, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<UpdatePlan>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var plan = await db.ProcurementPlans.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        if (plan.Status != "Draft") return Problem("plan-locked", "Only a draft plan can be changed.", 409);
        if (plan.Version != input.Version) return Problem("version-conflict", "Refresh this plan before saving changes.", 409);
        var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            ? plan.CurrencyCode : input.Draft.CurrencyCode };
        var issue = ProcurementPlanRules.Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var lines = draft.Lines!;
        var referenceIssue = await ValidateReferences(lines, db, ct);
        if (referenceIssue is not null) return Problem("validation-failed", referenceIssue, 422);
        var existing = plan.Lines.ToDictionary(x => x.Id);
        if (lines.Any(x => x.Id.HasValue && (!existing.ContainsKey(x.Id.Value) || existing[x.Id.Value].Status != "Active")))
            return Problem("validation-failed", "A planned need is unavailable or belongs to another plan.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(plan));

        plan.FinancialPeriodRef = draft.FinancialPeriodRef!.Trim();
        plan.CurrencyCode = draft.CurrencyCode!.Trim().ToUpperInvariant();
        plan.FundingSourceRef = Clean(draft.FundingSourceRef);
        plan.UpdatedAt = DateTimeOffset.UtcNow; // conflict token also changes for line-only edits
        var included = lines.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        foreach (var line in plan.Lines.Where(x => x.Status == "Active" && !included.Contains(x.Id)))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Removed from draft plan";
        }
        var nextLine = plan.Lines.Count == 0 ? 1 : plan.Lines.Max(x => x.LineNumber) + 1;
        foreach (var incoming in lines)
        {
            if (incoming.Id is Guid lineId)
                Apply(existing[lineId], incoming);
            else
            {
                var line = NewLine(plan.Id, nextLine++, incoming);
                db.AddScoped(line);
            }
        }
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftUpdated", before, Detail(plan));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this plan before saving changes.", 409); }
        return Results.Ok(Detail(plan));
    }

    private static async Task<IResult> Cancel(Guid id, CancelPlan input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var plan = await db.ProcurementPlans.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        if (plan.Status != "Draft") return Problem("plan-locked", "Only a draft plan can be cancelled here.", 409);
        if (plan.Version != input.Version) return Problem("version-conflict", "Refresh this plan before cancelling it.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a cancellation reason of up to 1,000 characters.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(plan));
        plan.Status = "Cancelled";
        plan.StatusReason = input.Reason.Trim();
        foreach (var line in plan.Lines.Where(x => x.Status == "Active"))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Plan cancelled: " + input.Reason.Trim();
        }
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftCancelled", before, Detail(plan));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this plan before cancelling it.", 409); }
        return Results.Ok(Detail(plan));
    }

    private static async Task<string?> ValidateReferences(IReadOnlyList<ProcurementPlanLineInput> lines,
        ProcurementDbContext db, CancellationToken ct)
    {
        var ids = lines.Where(x => x.CatalogItemId.HasValue).Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        if (ids.Length > 0 && await db.CatalogItems.AsNoTracking().CountAsync(x => ids.Contains(x.Id), ct) != ids.Length)
            return "One or more catalog items are unavailable in this company.";
        return null;
    }

    private static ProcurementPlanLine NewLine(Guid planId, int number, ProcurementPlanLineInput input)
    {
        var line = new ProcurementPlanLine { ProcurementPlanId = planId, LineNumber = number };
        Apply(line, input);
        return line;
    }

    private static void Apply(ProcurementPlanLine line, ProcurementPlanLineInput input)
    {
        line.CatalogItemId = input.CatalogItemId;
        line.Description = input.Description!.Trim();
        line.CategoryCode = Clean(input.CategoryCode);
        line.UnitCode = input.UnitCode!.Trim();
        line.EstimatedQuantity = input.EstimatedQuantity;
        line.EstimatedAmount = input.EstimatedAmount;
        line.ExpectedOn = input.ExpectedOn;
        line.FundingSourceRef = Clean(input.FundingSourceRef);
    }

    private static object Detail(ProcurementPlan plan) => new
    {
        plan.Id, plan.Number, plan.FinancialPeriodRef, plan.DepartmentRef,
        plan.CurrencyCode, plan.Status, plan.StatusReason, plan.OwnerSubjectId,
        plan.RevisionNumber, plan.FundingSourceRef, plan.Version, plan.CreatedAt, plan.UpdatedAt,
        Lines = plan.Lines.OrderBy(x => x.LineNumber).Select(x => new
        {
            x.Id, x.LineNumber, x.CatalogItemId, x.Description, x.CategoryCode, x.UnitCode,
            x.EstimatedQuantity, x.EstimatedAmount, x.ExpectedOn, x.FundingSourceRef,
            x.Status, x.StatusReason, x.Version,
        }),
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record UpdatePlan(long Version, ProcurementPlanInput Draft);
    public sealed record CancelPlan(long Version, string? Reason);
}
