using System.Text.Json;
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
    public sealed record QuotationContactInput(long ExpectedEventVersion, long ExpectedInvitationVersion,
        string Channel, string Reference, DateTimeOffset ReportedAt);

    private static void MapPrincipalQuotationRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/quotation-sources", QuotationSources).RequireAuthorization("procurement-quotation-read");
        group.MapGet("/{id:guid}/quotation-drafts", QuotationList).RequireAuthorization("procurement-quotation-read");
        group.MapGet("/{id:guid}/quotation-drafts/{bidId:guid}", QuotationRead).RequireAuthorization("procurement-quotation-read");
        group.MapGet("/{id:guid}/quotation-drafts/{bidId:guid}/versions/{version:long}", QuotationVersionRead)
            .RequireAuthorization("procurement-quotation-read");
        group.MapGet("/quotation-commands/{key:guid}", QuotationRecover).RequireAuthorization("procurement-quotation-read");
        group.MapPost("/{id:guid}/invitations/{invitationId:guid}/manual-contact", QuotationContact)
            .RequireAuthorization("procurement-quotation-read");
        group.MapPost("/{id:guid}/invitations/{invitationId:guid}/quotation-drafts", QuotationCreate)
            .RequireAuthorization("procurement-quotation-read");
        group.MapPost("/{id:guid}/quotation-drafts/{bidId:guid}/revise", QuotationRevise)
            .RequireAuthorization("procurement-quotation-read");
        group.MapPost("/{id:guid}/quotation-drafts/{bidId:guid}/cancel", QuotationCancel)
            .RequireAuthorization("procurement-quotation-read");
    }

    // GET and same-key replay deliberately avoid commercial-write flags and
    // RECORD privileges. Both profiles use this exact owning connection.
    private static async Task<IResult> RunQuotation(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<Reply>> action,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var readiness = http.RequestServices.GetService<IProcurementManualQuotationReadiness>();
            if (readiness is null || !readiness.HasCapturedContract
                || !http.RequestServices.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().ExecutionDatabaseConfigured
                || scope.OrgUnitId is not null)
                throw new Problem("QuotationStoreUnavailable", 503);
            var result = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, cases, token) =>
            {
                if (!await readiness.CanReadAsync(db, token))
                    throw new Problem("QuotationHistoryUnavailable", 503);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                return await action(db, cases, core, http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (Exception error) when (error is Problem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or OverflowException or FormatException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem problem ? problem.Status
                : ProcurementPostgresFailures.IsConcurrency(error)
                    || ProcurementPostgresFailures.Postgres(error)?.SqlState == "23505" ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503
                : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404
                : error is ArgumentException or JsonException or FormatException ? 422 : 409;
            return Results.Json(new { code = error is Problem ? error.Message
                    : status == 503 ? "QuotationSaveUnconfirmed" : "QuotationEvidenceNeedsReview",
                message = error is Problem { Message: "QuotationHistoryReadBudgetExceeded" }
                    ? "This quotation has a long history. Open a saved version, or try again."
                    : status == 503 ? "This save could not be confirmed. Keep its save reference and check again."
                    : "Refresh the quotation and check your access and entered details.",
                details = Array.Empty<string>() }, statusCode: status);
        }
    }

    private static async Task RequireQuotationRecord(HttpContext http, ProcurementDbContext db, bool newCommercial, CancellationToken ct)
    {
        if (newCommercial && !http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            throw new Problem("QuotationCaptureUnavailable", 503);
        if (!await http.RequestServices.GetRequiredService<IProcurementManualQuotationReadiness>().CanRecordAsync(db, ct))
            throw new Problem("QuotationRecordingUnavailable", 503);
    }

    private static IQueryable<SourcingEvent> QuotationOwnedSources(ProcurementDbContext db) =>
        from ev in db.SourcingEvents
        join original in db.SourcingDraftEvidenceRecords on ev.SourceEvidenceId equals (Guid?)original.Id
        where ev.ExecutionVersion == 2 && original.SchemaVersion == 2
            && original.SourcingEventId == ev.Id && original.MakerPrincipalId == ev.OwnerSubjectId
            && original.MakerSubjectId == ev.OwnerSubjectId && original.MakerWorkerId == ev.MakerWorkerId
            && original.BranchRef == ev.BranchRef && original.OriginalDraftFingerprint == ev.OriginalDraftFingerprint
        select ev;

    private static async Task<SourcingEvent> QuotationSource(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth, string permission,
        Guid id, CancellationToken ct)
    {
        var ev = await QuotationOwnedSources(db).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new Problem("QuotationNotFound", 404);
        _ = await PrincipalActor(core, scope, http, auth, permission, ev.BranchRef, true, ct);
        return ev;
    }

    private static async Task<PublishedEvidence> QuotationRetainedSource(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, SourcingEvent source, CancellationToken ct)
    {
        var retained = await PrincipalRetainedPublicationEvidence(db, cases, source, ct);
        var original = await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync(x => x.Id == source.SourceEvidenceId, ct);
        var bindings = retained.Lines.Select(x => x.Binding).ToArray();
        var draft = GovernedPrincipalRfqRestoration.RestoreVerified(original, source, bindings);
        var sources = new List<ApprovedPurchaseSource>();
        foreach (var binding in bindings)
            sources.Add(await ApprovedDemandSourceLoader.LoadRetainedAsync(db, cases, scope,
                binding.PurchaseRequestId, binding.PurchaseRequestLineId,
                new(binding.SubmissionId, binding.ApprovalOutcomeId, binding.MethodId), ct));
        GovernedPrincipalRfqRestoration.VerifyHistorical(draft, sources);
        return retained;
    }

    private static async Task LockQuotationSource(ProcurementDbContext db, ProcurementRequestScope scope,
        Guid source, Guid invitation, Guid? bid, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={source} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_invitations WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={invitation} AND sourcing_event_id={source} FOR UPDATE", ct);
        if (bid is { } bidId)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_bids WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={bidId} AND sourcing_event_id={source} FOR UPDATE", ct);
    }

    private static async Task RequireQuotationKeyUnused(ProcurementDbContext db, Guid key, CancellationToken ct)
    {
        await RequirePrincipalSourcingKeyUnused(db, key, ct);
        if (await db.SourcingManualContacts.AsNoTracking().AnyAsync(x => x.CommandKey == key, ct)
            || await db.SourcingManualQuoteVersions.AsNoTracking().AnyAsync(x => x.CommandKey == key, ct))
            throw new Problem("QuotationCommandKeyConflict", 409);
    }
}
