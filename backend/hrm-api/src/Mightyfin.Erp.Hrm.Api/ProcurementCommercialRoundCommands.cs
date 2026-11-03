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
    private static Task<IResult> RoundCreate(Guid id, JsonElement body, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            var input = RoundInput<RoundCreateInput>(body);
            if (id == Guid.Empty || input.ExpectedEventVersion < 1 || input.Draft is null) throw new Problem("RoundPlanInputInvalid", 422);
            CommercialRoundPlanDraft.Validate(input.Draft);
            var key = Key(http); var hash = RoundHash("Create", id, input);
            await LockPrincipalSourcingCommandKey(db, key, token);
            _ = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-read", id, token);
            if (await RoundReplay(db, scope, id, key, "Create", hash, token) is { } replay) return replay;
            var source = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-manage", id, token);
            RoundWriteContext(http, scope, source); await RequireRoundRecord(http, db, token);
            await RoundLockSource(db, scope, id, token); await db.Entry(source).ReloadAsync(token);
            if (source.Version != input.ExpectedEventVersion || source.Status != "Draft") throw new Problem("RoundPlanSourceChanged", 409);
            if (await db.CommercialRoundEnrollments.AnyAsync(x => x.SourcingEventId == id, token)) throw new Problem("RoundPlanAlreadyEnrolled", 409);
            await RequireRoundUntouched(db, id, token);
            var pin = await RoundVerifyHeldSource(db, cases, scope, source, token);
            await RoundParticipants(core, scope, source.BranchRef!.Value, input.Draft, token);
            var zone = await RoundCompanyZone(core, scope, token);
            var change = CommercialRoundPlanDraft.Create(pin, new(scope.SubjectId, scope.CoreLinkedWorkerId), input.Draft,
                zone, key, hash, ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value);
            db.StageCommercialRoundPlan(change); RoundRecord(db, scope, change);
            await db.SaveChangesAsync(token);
            return new(change.Receipt.ResponseJson, change.Receipt.ResponseStatusCode);
        }, ct);

    private static Task<IResult> RoundChange(Guid id, JsonElement body, string operation,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            var input = RoundInput<RoundChangeInput>(body);
            if (id == Guid.Empty || input.ExpectedEventVersion < 1 || input.ExpectedPlanVersion < 1
                || input.ExpectedPlanVersion == long.MaxValue || operation == "Revise" && (input.Draft is null || input.Reason is not null)
                || operation == "Cancel" && (input.Draft is not null || string.IsNullOrWhiteSpace(input.Reason)))
                throw new Problem("RoundPlanInputInvalid", 422);
            var key = Key(http); var hash = RoundHash(operation, id, input);
            await LockPrincipalSourcingCommandKey(db, key, token);
            _ = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-read", id, token);
            if (await RoundReplay(db, scope, id, key, operation, hash, token) is { } replay) return replay;
            var source = await RoundSource(db, core, scope, http, auth,
                operation == "Cancel" ? "procurement-round-plan-cancel" : "procurement-round-plan-manage", id, token);
            RoundWriteContext(http, scope, source); await RequireRoundRecord(http, db, token);
            await RoundLockSource(db, scope, id, token); await db.Entry(source).ReloadAsync(token);
            if (source.Status != "Draft" || source.Version != input.ExpectedEventVersion) throw new Problem("RoundPlanSourceChanged", 409);
            await RequireRoundUntouched(db, id, token);
            var graph = await RoundGraph(db, source, null, tracked: true, token);
            if (graph.Plan.Version != input.ExpectedPlanVersion || graph.Plan.Status != "Draft") throw new Problem("RoundPlanVersionChanged", 409);
            if (operation == "Revise") _ = await RoundVerifyHeldSource(db, cases, scope, source, token);
            if (input.Draft is not null) await RoundParticipants(core, scope, source.BranchRef!.Value, input.Draft, token);
            var draft = input.Draft ?? CommercialRoundPlanDraft.Restore(graph.Revision, graph.Criteria, graph.Participants);
            // Cancellation preserves its original content/zone, not current settings.
            var zone = operation == "Cancel" ? graph.Revision.CompanyTimeZoneId : await RoundCompanyZone(core, scope, token);
            var change = CommercialRoundPlanDraft.Revise(graph.Plan, graph.Revision, input.ExpectedPlanVersion,
                new(scope.SubjectId, scope.CoreLinkedWorkerId), draft, zone, operation, input.Reason?.Trim(), key, hash,
                ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value);

            db.StageCommercialRoundPlan(change); RoundRecord(db, scope, change);
            await db.SaveChangesAsync(token);
            return new(change.Receipt.ResponseJson, change.Receipt.ResponseStatusCode);
        }, ct);

    private static async Task RoundLockSource(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
    {
        var selected = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
            .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x).Take(201).ToArrayAsync(ct);
        if (selected.Length is < 1 or > 200) throw new Problem("RoundPlanSourceInvalid", 409);
        foreach (var requestId in selected) await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
        var current = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
            .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x).Take(201).ToArrayAsync(ct);
        if (!selected.SequenceEqual(current)) throw new Problem("RoundPlanSourceChanged", 409);
        // Every round writer first holds the same event lock. The immutable
        // enrollment needs no UPDATE grant; lock its mutable plan only afterward.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.commercial_round_plans WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND sourcing_event_id={id} FOR UPDATE", ct);
    }
    private static async Task RequireRoundUntouched(ProcurementDbContext db, Guid id, CancellationToken ct)
    {
        if (await db.SourcingPublicationOpenings.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingPublicationDecisions.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingPublicationReceipts.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingInvitations.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingManualContacts.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingManualQuoteVersions.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SupplierBids.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct)
            || await db.SourcingAwards.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, ct))
            throw new Problem("RoundPlanSourceAlreadyExposed", 409);
    }
    private static async Task<RoundPlanSource> RoundVerifyHeldSource(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, SourcingEvent source, CancellationToken ct)
    {
        if (source.ExecutionVersion != 2 || source.Status != "Draft" || source.BranchRef is null
            || source.ApprovalInstanceRef is not null || source.OpensAt is not null || source.ClosesAt is not null)
            throw new Problem("RoundPlanSourceInvalid", 409);
        var original = await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync(x => x.Id == source.SourceEvidenceId, ct);
        var bindings = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == source.Id).OrderBy(x => x.SourcingLineId).ToArrayAsync(ct);
        var snapshot = GovernedPrincipalRfqRestoration.RestoreVerified(original, source, bindings);
        var sources = new List<ApprovedPurchaseSource>();
        var branchScope = QuotationRetainedDemandScope(scope, source.BranchRef);
        foreach (var binding in bindings)
        {
            var retained = await ApprovedDemandSourceLoader.LoadRetainedAsync(db, cases, branchScope,
                binding.PurchaseRequestId, binding.PurchaseRequestLineId,
                new(binding.SubmissionId, binding.ApprovalOutcomeId, binding.MethodId), ct);
            var hold = retained.Allocations.SingleOrDefault(x => x.Id == binding.AllocationId);
            if (hold is null || hold.Status != "Reserved" || hold.TargetType != "SourcingEvent" || hold.TargetId != source.Id
                || hold.Quantity != binding.Quantity || hold.EstimatedValue != binding.EstimatedValue)
                throw new Problem("RoundPlanDemandHoldChanged", 409);
            sources.Add(retained);
        }
        GovernedPrincipalRfqRestoration.VerifyHistorical(snapshot, sources);
        return new(source.TenantId, source.LegalEntityId, source.Id, source.BranchRef.Value, original.Id,
            source.Version, original.PayloadSha256, source.OwnerSubjectId);
    }
    private static async Task RoundParticipants(HrmDbContext core, ProcurementRequestScope scope, Guid branch,
        RoundPlanDraftInput input, CancellationToken ct)
    {
        CommercialRoundPlanDraft.Validate(input);
        foreach (var principal in input.Participants.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal))
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, principal, ct);
            if (person is null || !person.OrganisationWide && !person.AllowedBranches.Contains(branch))
                throw new Problem("RoundPlanParticipantUnavailable", 409);
        }
    }
    private static async Task<string> RoundCompanyZone(HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
        => await core.LegalEntities.AsNoTracking().Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived)
            .Select(x => x.TimeZoneId).SingleAsync(ct);
    private static void RoundRecord(ProcurementDbContext db, ProcurementRequestScope scope, RoundPlanChange change)
    {
        var revision = change.Revision;
        db.AddScoped(new AuditEvent { RecordType = "CommercialRoundPlan", RecordId = change.Plan.Id, Action = revision.Operation,
            ActorSubjectId = scope.SubjectId, CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { eventId = change.Plan.SourcingEventId, revisionId = revision.Id,
                version = revision.ResultVersion, commandKey = change.Receipt.CommandKey, status = revision.ResultStatus }) });
        db.AddScoped(new OutboxEvent { AggregateType = "CommercialRoundPlan", AggregateId = change.Plan.Id,
            EventType = "commercial_round.plan_" + revision.Operation.ToLowerInvariant() + ".v1",
            IdempotencyKey = "round.plan." + change.Receipt.CommandKey.ToString("N"),
            PayloadJson = JsonSerializer.Serialize(new { eventId = change.Plan.SourcingEventId, planId = change.Plan.Id,
                revisionId = revision.Id, version = revision.ResultVersion, status = revision.ResultStatus }) });
    }
}
