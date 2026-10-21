using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> Create(CreateInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        var eventId = Guid.CreateVersion7(); // Stable across confirmed-abort retries of this original HTTP command.
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = Key(http); var title = Normalize(input.Title);
            if (title.Length is < 1 or > 200 || input.Sources is not { Length: > 0 and <= 200 }
                || input.Sources.Any(x => x is null || x.PurchaseRequestId == Guid.Empty || x.PurchaseRequestLineId == Guid.Empty
                    || x.ExpectedRequestVersion < 1 || x.Quantity <= 0 || !ExactDecimalJsonCodec.IsRepresentable(x.Quantity, 18, 4))
                || input.Sources.Select(x => x.PurchaseRequestLineId).Distinct().Count() != input.Sources.Length)
                throw new Problem("SourcingSourceSelectionInvalid", 422);
            var actor = await Actor(core, scope, http, auth, "procurement-sourcing-convert", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 1, operation = "Create", title,
                sources = input.Sources.OrderBy(x => x.PurchaseRequestLineId).Select(x => new { x.PurchaseRequestId,
                    x.PurchaseRequestLineId, x.ExpectedRequestVersion, quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity) }).ToArray() });
            if (await Replay(db, core, scope, http, auth, actor, "procurement-sourcing-convert", "Create", key, fingerprint, null, token) is { } prior) return prior;
            foreach (var requestId in input.Sources.Select(x => x.PurchaseRequestId).Distinct().Order())
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, token);
            var loaded = new List<RfqDemandSelection>(); var now = DateTimeOffset.UtcNow;
            foreach (var selected in input.Sources.OrderBy(x => x.PurchaseRequestLineId))
            {
                var (source, _) = await EligibleSource(db, cases, core, scope, http, auth,
                    selected.PurchaseRequestId, selected.PurchaseRequestLineId, now, token);
                loaded.Add(new(source, selected.ExpectedRequestVersion, selected.Quantity));
            }
            var plan = GovernedRfqDraft.Create(actor, eventId, key, title, loaded, now);
            var captured = GovernedRfqRestoration.Capture(plan.Result, key); var first = plan.Result.Lines[0];
            var evidence = new SourcingDraftEvidence { SourcingEventId = eventId, BranchRef = plan.Result.BranchId,
                MakerSubjectId = actor.SubjectId, MakerWorkerId = actor.WorkerId, CreateCommandKey = key,
                OriginalDraftFingerprint = captured.OriginalFingerprint, PayloadJson = captured.PayloadJson, PayloadSha256 = captured.PayloadSha256 };
            var ev = new SourcingEvent { Id = eventId, ExecutionVersion = 1, Number = "RF-" + eventId.ToString("N"),
                Title = plan.Result.Title, Method = first.MethodCode, MethodId = first.MethodId, MethodRevision = first.MethodRevision,
                BranchRef = plan.Result.BranchId, CurrencyCode = plan.Result.CurrencyCode, OwnerSubjectId = actor.SubjectId,
                MakerWorkerId = actor.WorkerId, SourceEvidenceId = evidence.Id, OriginalDraftFingerprint = captured.OriginalFingerprint };
            db.AddScoped(ev); db.AddScoped(evidence);
            var number = 0;
            foreach (var pin in plan.Result.Lines)
            {
                var source = loaded.Single(x => x.Source.Line.Id == pin.LineId).Source;
                var instruction = plan.Instructions.Single(x => x.AllocationId == pin.AllocationId);
                db.AddScoped(new SourcingLine { Id = pin.EventLineId, SourcingEventId = ev.Id, LineNumber = ++number,
                    PurchaseRequestLineId = pin.LineId, Description = source.Line.Description, UnitCode = pin.UnitCode,
                    CategoryCode = pin.CategoryCode, NeededBy = source.Line.NeededBy, Quantity = pin.Quantity });
                db.AddScoped(new PurchaseRequestAllocation { Id = pin.AllocationId, PurchaseRequestId = pin.RequestId,
                    PurchaseRequestLineId = pin.LineId, Method = pin.MethodCode, TargetType = "SourcingEvent", TargetId = ev.Id,
                    Status = "Reserved", Quantity = pin.Quantity, EstimatedValue = pin.EstimatedValue, CurrencyCode = ev.CurrencyCode,
                    DecidedBy = actor.SubjectId, DecidedAt = now, IdempotencyKey = instruction.EffectKey });
                db.AddScoped(Binding(ev.Id, evidence.Id, pin));
                source.Line.AllocatedQuantity = instruction.ResultingAllocatedQuantity; source.Line.Route = "PendingSourcing";
                source.Request.UpdatedAt = now;
            }
            Record(db, scope, ev, "DraftReserved", key, new { lineIds = plan.Result.Lines.Select(x => x.LineId).ToArray() });
            return await Save(db, scope, actor, ev, "Create", key, fingerprint, token);
        }, ct);
    }

    private static Task<IResult> Cancel(Guid id, CancelInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = Key(http); var reason = Normalize(input.Reason);
            if (input.ExpectedEventVersion < 1 || reason.Length is < 1 or > 2000 || input.OriginalDraftFingerprint is not { Length: 64 })
                throw new Problem("SourcingCancellationInvalid", 422);
            var actor = await Actor(core, scope, http, auth, "procurement-sourcing-cancel", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 1, operation = "Cancel", id, input.ExpectedEventVersion,
                input.OriginalDraftFingerprint, reason });
            if (await Replay(db, core, scope, http, auth, actor, "procurement-sourcing-cancel", "Cancel", key, fingerprint, id, token) is { } prior) return prior;
            var initial = await OwnedEvent(db, core, scope, http, auth, actor, "procurement-sourcing-cancel", id, token);
            var selected = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
                .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x).ToArrayAsync(token);
            if (selected.Length is < 1 or > 200) throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
            foreach (var request in selected) await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, request, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", token);
            await db.Entry(initial).ReloadAsync(token);
            var ev = await OwnedEvent(db, core, scope, http, auth, actor, "procurement-sourcing-cancel", id, token);
            if (ev.Status != "Draft" || ev.Version != input.ExpectedEventVersion || ev.OriginalDraftFingerprint != input.OriginalDraftFingerprint)
                throw new Problem("SourcingDraftChanged", 409);
            var evidence = await db.SourcingDraftEvidenceRecords.SingleAsync(x => x.Id == ev.SourceEvidenceId, token);
            var bindings = await db.SourcingDemandBindings.Where(x => x.SourcingEventId == id).OrderBy(x => x.PurchaseRequestLineId).ToArrayAsync(token);
            if (bindings.Length is < 1 or > 200 || !selected.SequenceEqual(bindings.Select(x => x.PurchaseRequestId).Distinct().Order()))
                throw new Problem("SourcingSourceSetChanged", 409);
            var lines = await db.SourcingLines.Where(x => x.SourcingEventId == id).ToArrayAsync(token);
            if (lines.Length != bindings.Length) throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
            var sources = new List<ApprovedPurchaseSource>(); var owned = new List<RfqOwnedDraftLine>();
            foreach (var binding in bindings)
            {
                var source = await ApprovedDemandSourceLoader.LoadRetainedAsync(db, cases, scope, binding.PurchaseRequestId,
                    binding.PurchaseRequestLineId, new(binding.SubmissionId, binding.ApprovalOutcomeId, binding.MethodId), token);
                _ = await Actor(core, scope, http, auth, "procurement-sourcing-cancel", source.Request.BranchRef, true, token);
                var line = lines.SingleOrDefault(x => x.Id == binding.SourcingLineId) ?? throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
                var allocation = source.Allocations.SingleOrDefault(x => x.Id == binding.AllocationId)
                    ?? throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
                sources.Add(source); owned.Add(new(SourcingDraftPersistenceProjection.Pin(binding), line, allocation));
            }
            var historical = sources.DistinctBy(x => x.Submission.Id).Select(x => new RfqHistoricalApproval(x.Submission, x.Outcome,
                x.CoreCase, x.FinalDecision, x.Evaluation, x.Policy, x.Finance)).ToArray();
            var restored = GovernedRfqRestoration.RestoreVerified(SourcingDraftPersistenceProjection.Evidence(evidence), ev, owned, historical);
            var plan = GovernedRfqDraft.Cancel(actor, restored, key, input.OriginalDraftFingerprint, reason, sources);
            foreach (var instruction in plan.Instructions)
            {
                var source = sources.Single(x => x.Line.Id == instruction.RequestLineId);
                var allocation = source.Allocations.Single(x => x.Id == instruction.AllocationId);
                if (allocation.Version != instruction.ExpectedVersion || source.Request.Version != instruction.ExpectedRequestVersion)
                    throw new Problem("SourcingReservationChanged", 409);
                allocation.Status = "Released"; allocation.Reason = reason;
                source.Line.AllocatedQuantity = instruction.ResultingAllocatedQuantity; // Preserve competitive route and all other holds.
                source.Request.UpdatedAt = DateTimeOffset.UtcNow;
            }
            ev.Status = "Cancelled"; ev.CancellationReason = reason;
            foreach (var line in lines) { line.Status = "Cancelled"; line.StatusReason = "Quote request cancelled; see the event reason."; }
            Record(db, scope, ev, "DraftCancelled", key, new { reason });
            return await Save(db, scope, actor, ev, "Cancel", key, fingerprint, token);
        }, ct);

    private static SourcingDemandBinding Binding(Guid ev, Guid evidence, RfqDemandPin p) => new()
    {
        SourcingEventId = ev, SourceEvidenceId = evidence, SourcingLineId = p.EventLineId, PurchaseRequestId = p.RequestId,
        PurchaseRequestLineId = p.LineId, RequestVersion = p.RequestVersion, SubmissionId = p.SubmissionId,
        SubmittedVersion = p.SubmittedVersion, CoreApprovalCaseId = p.CaseId, ApprovalOutcomeId = p.OutcomeId,
        CoreDecisionId = p.DecisionId, RequestSha = p.RequestSha, PolicyDecisionSha = p.PolicyDecisionSha,
        PolicyRulesSha = p.PolicyRulesSha, FinanceSnapshotId = p.FinanceId, FinanceSha = p.FinanceSha,
        MethodId = p.MethodId, MethodRevision = p.MethodRevision, MethodCode = p.MethodCode,
        MinimumQuoteCount = p.MinimumQuoteCount, MakerWorkerId = p.MakerWorkerId, RequesterWorkerId = p.RequesterWorkerId,
        AllocationId = p.AllocationId, Quantity = p.Quantity, UnitEstimate = p.UnitEstimate, EstimatedValue = p.EstimatedValue,
        UnitCode = p.UnitCode, CategoryCode = p.CategoryCode, DepartmentRef = p.DepartmentId,
    };
}
