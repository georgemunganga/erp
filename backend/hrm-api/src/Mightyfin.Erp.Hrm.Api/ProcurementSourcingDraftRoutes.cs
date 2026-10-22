using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Internal sourcing drafts. Publish, invitations, bids and award are separate governed operations.</summary>
public static class ProcurementSourcingDraftRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/sourcing");
        group.MapGet("", List).RequireAuthorization("procurement-sourcing-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-sourcing-read");
        group.MapPost("", Create).RequireAuthorization("procurement-sourcing-create");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-sourcing-edit");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-sourcing-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var own = db.SourcingEvents.AsNoTracking().Where(x => x.ExecutionVersion == 0 && x.OwnerSubjectId == scope.SubjectId);
        var total = await own.CountAsync(ct);
        var items = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.Title, x.Method, x.CurrencyCode,
                x.OpensAt, x.ClosesAt, x.Status, x.Version,
                LineCount = db.SourcingLines.Count(line => line.SourcingEventId == x.Id && line.Status == "Active") })
            .ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var item = await db.SourcingEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.ExecutionVersion == 0 && x.OwnerSubjectId == scope.SubjectId, ct);
        if (item is null) return Problem("not-found", "Sourcing event not found.", 404);
        var lines = await db.SourcingLines.AsNoTracking().Where(x => x.SourcingEventId == id)
            .OrderBy(x => x.LineNumber).ToListAsync(ct);
        return Results.Ok(Detail(item, lines));
    }

    private static async Task<IResult> Create(SourcingDraftInput input, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = ProcurementSourcingDraftRules.Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (input.Lines!.Any(x => x.Id.HasValue))
            return Problem("validation-failed", "New sourcing lines cannot carry existing IDs.", 422);
        var item = new SourcingEvent
        {
            Number = "RF-" + Guid.CreateVersion7().ToString("N"),
            Title = input.Title!.Trim(), Method = input.Method!.Trim(),
            CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(),
            OpensAt = input.OpensAt, ClosesAt = input.ClosesAt,
            OwnerSubjectId = scope.SubjectId, Status = "Draft",
        };
        var lines = input.Lines.Select((line, index) => NewLine(item.Id, index + 1, line)).ToList();
        db.AddScoped(item);
        foreach (var line in lines) db.AddScoped(line);
        ProcurementDraftAudit.Add(db, scope, "SourcingEvent", item.Id,
            "DraftCreated", null, Detail(item, lines));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/sourcing/{item.Id}", Detail(item, lines));
    }

    private static async Task<IResult> Update(Guid id, VersionedSourcingDraft input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var item = await db.SourcingEvents
            .SingleOrDefaultAsync(x => x.Id == id && x.ExecutionVersion == 0 && x.OwnerSubjectId == scope.SubjectId, ct);
        if (item is null) return Problem("not-found", "Sourcing event not found.", 404);
        if (item.Status != "Draft") return Problem("sourcing-locked", "Only a draft sourcing event can be changed.", 409);
        if (item.Version != input.Version) return Problem("version-conflict", "Refresh this event before saving.", 409);
        var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            ? item.CurrencyCode : input.Draft.CurrencyCode };
        var issue = ProcurementSourcingDraftRules.Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var lines = await db.SourcingLines.Where(x => x.SourcingEventId == id)
            .OrderBy(x => x.LineNumber).ToListAsync(ct);
        var existing = lines.ToDictionary(x => x.Id);
        if (draft.Lines!.Any(x => x.Id.HasValue && (!existing.ContainsKey(x.Id.Value)
            || existing[x.Id.Value].Status != "Active")))
            return Problem("validation-failed", "A sourcing line is unavailable or belongs to another event.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(item, lines));

        item.Title = draft.Title!.Trim();
        item.Method = draft.Method!.Trim();
        item.CurrencyCode = draft.CurrencyCode!.Trim().ToUpperInvariant();
        item.OpensAt = draft.OpensAt;
        item.ClosesAt = draft.ClosesAt;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var included = draft.Lines.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        foreach (var line in lines.Where(x => x.Status == "Active" && !included.Contains(x.Id)))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Removed from draft";
        }
        var nextLine = lines.Count == 0 ? 1 : lines.Max(x => x.LineNumber) + 1;
        foreach (var incoming in draft.Lines)
        {
            if (incoming.Id is Guid lineId)
            {
                var line = existing[lineId];
                line.Description = incoming.Description!.Trim();
                line.UnitCode = incoming.UnitCode!.Trim();
                line.Quantity = incoming.Quantity;
                line.CategoryCode = Clean(incoming.CategoryCode);
                line.NeededBy = incoming.NeededBy;
            }
            else
            {
                var line = NewLine(id, nextLine++, incoming);
                db.AddScoped(line);
                lines.Add(line);
            }
        }
        ProcurementDraftAudit.Add(db, scope, "SourcingEvent", item.Id,
            "DraftUpdated", before, Detail(item, lines));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this event before saving.", 409); }
        return Results.Ok(Detail(item, lines));
    }

    private static async Task<IResult> Cancel(Guid id, SourcingCancel input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var item = await db.SourcingEvents
            .SingleOrDefaultAsync(x => x.Id == id && x.ExecutionVersion == 0 && x.OwnerSubjectId == scope.SubjectId, ct);
        if (item is null) return Problem("not-found", "Sourcing event not found.", 404);
        if (item.Status != "Draft") return Problem("sourcing-locked", "Only a draft sourcing event can be cancelled.", 409);
        if (item.Version != input.Version) return Problem("version-conflict", "Refresh this event before cancelling.", 409);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a cancellation reason of up to 1,000 characters.", 422);
        var lines = await db.SourcingLines.Where(x => x.SourcingEventId == id).ToListAsync(ct);
        var before = ProcurementDraftAudit.Snapshot(Detail(item, lines));
        item.Status = "Cancelled";
        item.CancellationReason = input.Reason.Trim();
        foreach (var line in lines.Where(x => x.Status == "Active"))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Event cancelled; see the event reason.";
        }
        ProcurementDraftAudit.Add(db, scope, "SourcingEvent", item.Id,
            "DraftCancelled", before, Detail(item, lines));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this event before cancelling.", 409); }
        return Results.Ok(Detail(item, lines));
    }

    private static SourcingLine NewLine(Guid eventId, int number, SourcingLineInput input) => new()
    {
        SourcingEventId = eventId, LineNumber = number, Status = "Active",
        Description = input.Description!.Trim(), UnitCode = input.UnitCode!.Trim(),
        Quantity = input.Quantity, CategoryCode = Clean(input.CategoryCode), NeededBy = input.NeededBy,
    };

    private static object Detail(SourcingEvent item, IEnumerable<SourcingLine> lines) => new
    {
        item.Id, item.Number, item.Title, item.Method, item.CurrencyCode,
        item.OpensAt, item.ClosesAt, item.Status, item.CancellationReason,
        item.Version, item.CreatedAt,
        lines = lines.OrderBy(x => x.LineNumber).Select(x => new { x.Id, x.LineNumber,
            x.Description, x.UnitCode, x.Quantity, x.CategoryCode, x.NeededBy,
            x.Status, x.StatusReason, x.Version }).ToArray(),
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);

    public sealed record VersionedSourcingDraft(long Version, SourcingDraftInput Draft);
    public sealed record SourcingCancel(long Version, string? Reason);
}
