using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> PrincipalPublicationList(int? page, int? pageSize, string? q,
        string? state, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var paging = ProcurementSourcingDiscoveryQueries.Paging(page, pageSize, q);
            if (state is not (null or "Draft" or "PublicationReview" or "Published" or "Cancelled"))
                throw new Problem("SourcingPublicationListStateInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            var query = PrincipalOwned(db, actor).AsNoTracking();
            if (actor.SelectedBranchId is { } selected) query = query.Where(x => x.BranchRef == selected);
            if (!actor.OrganisationWide)
            {
                var branches = actor.Branches.ToArray();
                query = query.Where(x => x.BranchRef != null && branches.Contains(x.BranchRef.Value));
            }
            if (state is not null) query = query.Where(x => x.Status == state);
            if (paging.Search.Length > 0)
                query = query.Where(x => EF.Functions.ILike(x.Number, paging.Pattern, "\\")
                    || EF.Functions.ILike(x.Title, paging.Pattern, "\\"));
            var total = await query.CountAsync(token);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items = new List<object>();
            foreach (var ev in rows)
            {
                try
                {
                    if (ev.Status is "PublicationReview" or "Published")
                        _ = await PrincipalPublicationEvidence(db, cases, ev, token);
                    else _ = await PrincipalReadOwned(db, ev, token);
                    items.Add(new { ev.Id, ev.Number, ev.Title, ev.CurrencyCode, ev.Status,
                        ev.Version, originalDraftFingerprint = ev.OriginalDraftFingerprint,
                        state = "VerifiedRetainedSourcing", supplierDeliveryState = ev.Status == "Published"
                            ? "SeeShortlist" : "NotStarted" });
                }
                catch (Exception error) when (IsDiscoveryEvidenceFailure(error)
                    || error is JsonException or InvalidOperationException)
                {
                    items.Add(new { ev.Id, ev.Number, state = "Unavailable",
                        blocker = "SourcingPublicationEvidenceNeedsReview" });
                }
            }
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new { items, total, page = paging.Number,
                pageSize = paging.Size, scope = "OwnCurrentCompany", canPublish = false }, Json));
        }, ct);
    }

    private static Task<IResult> PrincipalPublicationRead(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            if (id == Guid.Empty) throw new Problem("SourcingNotFound", 404);
            var ev = await db.SourcingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == id && x.ExecutionVersion == 2, token)
                ?? throw new Problem("SourcingNotFound", 404);
            var author = false;
            try
            {
                var actor = await PrincipalActor(core, scope, http, auth,
                    "procurement-sourcing-submit", ev.BranchRef, true, token);
                author = ev.OwnerSubjectId == actor.PrincipalId;
            }
            catch (Problem error) when (error.Status is 403 or 404) { }
            var reviewer = false;
            if (!author && ev.Status is ("PublicationReview" or "Published"))
            {
                var actor = await PrincipalActor(core, scope, http, auth,
                    PrincipalRfqPublicationCoreWorkflow.Permission, ev.BranchRef, true, token);
                var caseId = Guid.TryParseExact(ev.ApprovalInstanceRef, "D", out var parsed) ? parsed : Guid.Empty;
                reviewer = caseId != Guid.Empty && await cases.Cases.AsNoTracking().AnyAsync(x =>
                    x.Id == caseId && x.SourceModule == "procurement"
                    && x.SourceType == PrincipalRfqPublicationCoreWorkflow.SourceType && x.SourceId == id
                    && x.Stages.Any(stage => stage.Assignments.Any(assignment =>
                        assignment.AssigneeSubjectId == actor.PrincipalId
                        && assignment.PermissionKey == PrincipalRfqPublicationCoreWorkflow.Permission)), token);
            }
            if (!author && !reviewer) throw new Problem("SourcingNotFound", 404);
            if (ev.Status is "Draft" or "Cancelled")
            {
                if (!author) throw new Problem("SourcingNotFound", 404);
                return await PrincipalReadOwned(db, ev, token);
            }
            if (ev.Status is not ("PublicationReview" or "Published"))
                throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
            var verified = await PrincipalPublicationEvidence(db, cases, ev, token);
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new
            {
                ev.Id, ev.Number, ev.Title, ev.Method, ev.CurrencyCode, ev.Status, ev.Version,
                originalDraftFingerprint = ev.OriginalDraftFingerprint,
                canPublish = false, financeReservationChanged = false,
                publication = new { openingId = verified.Opening.Id,
                    coreCaseId = verified.Case.Id, caseState = verified.Case.State,
                    coreCaseVersion = verified.Case.Version,
                    policyVersionId = verified.Opening.PolicyVersionId,
                    policyVersionNumber = verified.Opening.PolicyVersionNumber,
                    supplierDeliveryState = "PerInvitation", reviewer,
                    assignments = verified.Case.Stages.SelectMany(x => x.Assignments)
                        .Where(x => reviewer && x.AssigneeSubjectId == scope.SubjectId).Select(x => new
                        { x.Id, x.Version, x.State, x.PermissionKey }).ToArray(),
                },
                lines = verified.Lines.Select(x => new
                {
                    id = x.Line.Id, x.Binding.PurchaseRequestId, x.Binding.PurchaseRequestLineId,
                    x.Line.Description, x.Line.UnitCode, x.Line.CategoryCode,
                    quantity = ProcurementEvidenceEncoding.Decimal(x.Binding.Quantity),
                    approvedUnitEstimate = ProcurementEvidenceEncoding.Decimal(x.Binding.UnitEstimate),
                    estimatedValue = ProcurementEvidenceEncoding.Decimal(x.Binding.EstimatedValue),
                    allocationState = x.Allocation.Status,
                }).ToArray(),
                invitations = verified.Invitations.Select(x => new
                { x.Id, x.SupplierId, x.Status, x.Version, contactRecorded = x.Status == "ManualContactRecorded" }).ToArray(),
            }, Json));
        }, ct);
    }

    private sealed record PublishedLine(SourcingDemandBinding Binding, SourcingLine Line,
        PurchaseRequestAllocation Allocation);
    private sealed record PublishedEvidence(SourcingPublicationOpening Opening,
        Mightyfin.Erp.Core.Approvals.Domain.ApprovalCase Case,
        IReadOnlyList<PublishedLine> Lines, IReadOnlyList<SourcingInvitation> Invitations);

    private static async Task<PublishedEvidence> PrincipalPublicationEvidence(ProcurementDbContext db,
        ApprovalDbContext cases, SourcingEvent ev, CancellationToken ct)
    {
        if (ev.Status is not ("PublicationReview" or "Published")
            || !Guid.TryParseExact(ev.ApprovalInstanceRef, "D", out var caseId)
            || caseId == Guid.Empty || ev.OriginalDraftFingerprint is null)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var opening = await db.SourcingPublicationOpenings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourcingEventId == ev.Id && x.CoreApprovalCaseId == caseId, ct)
            ?? throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        if (opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson)
            || opening.OriginalDraftFingerprint != ev.OriginalDraftFingerprint
            || opening.TenantId != ev.TenantId || opening.LegalEntityId != ev.LegalEntityId)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        using var snapshot = JsonDocument.Parse(opening.SnapshotJson);
        var root = snapshot.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 2
            || root.GetProperty("Id").GetGuid() != ev.Id
            || root.GetProperty("coreCaseId").GetGuid() != caseId
            || root.GetProperty("eventVersion").GetInt64() != opening.EventVersionAtOpening - 1
            || root.GetProperty("OriginalFingerprint").GetString() != ev.OriginalDraftFingerprint
            || root.GetProperty("policyVersionId").GetGuid() != opening.PolicyVersionId
            || root.GetProperty("publishedPolicyEvidenceId").GetGuid() != opening.PublishedPolicyEvidenceId)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var supplierIds = root.GetProperty("supplierIds").EnumerateArray().Select(x => x.GetGuid()).ToArray();
        if (supplierIds.Length is < 1 or > 100 || supplierIds.Distinct().Count() != supplierIds.Length)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == opening.PolicyVersionId, ct);
        var published = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.PublishedPolicyEvidenceId, ct);
        if (policy is null || published is null || policy.State != "Published"
            || policy.PolicyKey != SourcingPublicationPolicyRules.PolicyKey
            || policy.TenantId != ev.TenantId || policy.LegalEntityId != ev.LegalEntityId
            || policy.PolicyVersionNumber != opening.PolicyVersionNumber
            || opening.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || published.Stage != "Published" || published.SchemaVersion != 2
            || published.PolicyVersionId != policy.Id
            || published.TenantId != ev.TenantId || published.LegalEntityId != ev.LegalEntityId)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages)
            .ThenInclude(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        if (workflow.SourceModule != "procurement" || workflow.SourceType != PrincipalRfqPublicationCoreWorkflow.SourceType
            || workflow.TenantId != ev.TenantId || workflow.LegalEntityId != ev.LegalEntityId
            || workflow.SourceId != ev.Id || workflow.SourceVersion != opening.EventVersionAtOpening - 1
            || workflow.PolicyVersionId != opening.PolicyVersionId
            || workflow.PolicyVersionNumber != opening.PolicyVersionNumber
            || workflow.MakerSubjectId != opening.MakerPrincipalId
            || (ev.Status == "PublicationReview"
                ? workflow.State != "Pending" || ev.Version != opening.EventVersionAtOpening
                : workflow.State != "Approved" || ev.OpensAt is null))
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var original = await db.SourcingDraftEvidenceRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ev.SourceEvidenceId, ct)
            ?? throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var bindings = await db.SourcingDemandBindings.AsNoTracking()
            .Where(x => x.SourcingEventId == ev.Id).OrderBy(x => x.SourcingLineId).Take(201).ToArrayAsync(ct);
        _ = GovernedPrincipalRfqRestoration.RestoreVerified(original, ev, bindings);
        var lines = await db.SourcingLines.AsNoTracking().Where(x => x.SourcingEventId == ev.Id)
            .Take(201).ToArrayAsync(ct);
        var allocationIds = bindings.Select(x => x.AllocationId).ToArray();
        var allocations = await db.PurchaseRequestAllocations.AsNoTracking()
            .Where(x => allocationIds.Contains(x.Id)).ToArrayAsync(ct);
        if (bindings.Length is < 1 or > 200 || lines.Length != bindings.Length
            || allocations.Length != bindings.Length)
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        var verifiedLines = new List<PublishedLine>();
        foreach (var binding in bindings)
        {
            var line = lines.SingleOrDefault(x => x.Id == binding.SourcingLineId);
            var allocation = allocations.SingleOrDefault(x => x.Id == binding.AllocationId);
            if (line is null || allocation is null)
                throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
            PrincipalRfqPublicationReadInvariants.VerifyActiveHold(ev, binding, line, allocation);
            verifiedLines.Add(new(binding, line, allocation));
        }
        SourcingInvitation[] invitations = [];
        if (ev.Status == "Published")
        {
            var approved = await db.SourcingPublicationDecisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OpeningId == opening.Id && x.CaseState == "Approved", ct)
                ?? throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
            var decision = await cases.Decisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == approved.CoreApprovalDecisionId && x.CaseId == caseId, ct);
            if (decision is null || decision.Action != "Approve"
                || decision.ActorSubjectId != approved.ActorPrincipalId
                || ev.Version != approved.EventVersionAtDecision)
                throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
            invitations = await db.SourcingInvitations.AsNoTracking()
                .Where(x => x.SourcingEventId == ev.Id).Take(101).ToArrayAsync(ct);
            if (invitations.Length != supplierIds.Length
                || !invitations.Select(x => x.SupplierId).ToHashSet().SetEquals(supplierIds)
                || invitations.Any(x => x.Status is not ("ReadyForManualDelivery" or "ManualContactRecorded")
                    || x.SentAt is not null || x.ViewedAt is not null || x.DeliveryRef is not null))
                throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
            var contactedIds = invitations.Where(x => x.Status == "ManualContactRecorded")
                .Select(x => x.Id).ToArray();
            var contacts = await db.SourcingManualContacts.AsNoTracking()
                .Where(x => contactedIds.Contains(x.InvitationId)).ToArrayAsync(ct);
            if (contacts.Length != contactedIds.Length
                || contacts.Select(x => x.InvitationId).Distinct().Count() != contactedIds.Length
                || contacts.Any(x => x.SourcingEventId != ev.Id
                    || invitations.Single(i => i.Id == x.InvitationId).SupplierId != x.SupplierId
                    || x.ResponseSha256 != ProcurementEvidenceEncoding.Digest(x.ResponseJson)))
                throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        }
        else if (await db.SourcingInvitations.AsNoTracking().AnyAsync(x => x.SourcingEventId == ev.Id, ct))
            throw new Problem("SourcingPublicationEvidenceNeedsReview", 409);
        return new(opening, workflow, verifiedLines, invitations);
    }
}
