using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static Task<IResult> ManualOpen(Guid id, OpenInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var invoice = await Invoice(db, scope, id, token, true);
            var actor = await Actor(core, scope, http, auth, invoice.BranchRef, [InvoiceUnitReviewTransitions.RequestPermission], token);
            if (!actor.Permissions.Contains("procurement-invoice-read") || !(await auth.AuthorizeAsync(http.User, null, "procurement-invoice-read")).Succeeded) throw new UnauthorizedAccessException();
            var key = Key(http); var request = new InvoiceUnitReviewRequest(2, "Open", key, id, null, null, input.InvoiceVersion, input.MappingRevision, null, null, null, Reason(input.Reason), null);
            if (request.Reason is null) throw new ReviewProblem("InvoiceManualUnitReviewReasonRequired", 422);
            if (await ReplayManual(db, scope, request, invoice, token) is { } replay) return replay;
            if (input.InvoiceVersion != invoice.Version || input.MappingRevision != invoice.MappingRevision) throw new ReviewProblem("InvoiceUnitReviewVersionChanged", 409);
            var mapping = await Mapping(db, invoice, token); var originals = await Originals(db, id, token);
            var matching = await Policy(db, scope, http, InvoiceMatchingPolicyRules.PolicyKey, token);
            var review = await Policy(db, scope, http, InvoiceRetainedUnitReview.ReviewPolicyKey, token);
            RequireManualPolicy(review);
            var people = new List<InvoiceUnitReviewPolicyRules.Person>();
            foreach (var principal in InvoiceUnitReviewPolicyRules.Parse(review.RulesJson).Stages.SelectMany(x => x.Principals).Distinct(StringComparer.Ordinal))
                people.Add(await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, principal, token) ?? throw new ReviewProblem("InvoiceUnitReviewerUnavailable", 409));
            foreach (var person in people)
                if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId, actor.PrincipalId, actor.WorkerId, person.PrincipalId, person.WorkerId, token))
                    throw new ReviewProblem("InvoiceUnitReviewIndependentReviewerRequired", 409);
            RequireManualMapping(mapping, invoice, originals);
            var hashes = EmptyManualDocumentHashes;
            var issued = await ReviewIssuedOrder(db, cases, core, scope, actor, mapping, mapping.PurchaseOrderId, mapping.IssuedVersionId, token);
            var now = Now();
            var plan = InvoiceUnitReviewTransitions.PrepareOpen(invoice, originals, mapping, issued, matching, review, actor, people, hashes,
                new(key, input.InvoiceVersion, input.MappingRevision, now, request.Reason));
            var source = InvoiceUnitReviewStorage.Source(plan, issued);
            RequireManualSource(source);
            cases.AddCase(plan.Case); await cases.SaveChangesAsync(token); // Actual insert provenance + final Core revision before receipt.
            db.AddScoped(source);
            foreach (var pin in plan.AssignmentWorkers) db.AddScoped(new InvoiceUnitReviewAssignmentEvidence { ReviewSourceId = source.Id,
                CoreCaseId = source.CoreCaseId, CoreAssignmentId = pin.AssignmentId, PrincipalId = pin.PrincipalId, WorkerId = pin.WorkerId });
            var response = new InvoiceUnitReviewResponse(1, id, source.Id, plan.Case.Id, key, "Open", invoice.Version,
                plan.Case.Version, plan.Case.State, "Pending", null, null, now, true, false, false);
            response = InvoiceUnitReviewCommands.BindSource(response, source);
            Receipt(db, scope, source, request, response, actor.WorkerId);
            db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceUnitReviewSource), RecordId = source.Id, ActorSubjectId = scope.SubjectId,
                Action = "ManualUnitReviewOpened", ChangedFieldsJson = InvoiceUnitReviewCommands.Serialize(new { invoiceId = id, mappingId = mapping.Id, coreCaseId = plan.Case.Id, commandKey = key }) });
            await db.SaveChangesAsync(token); return InvoiceUnitReviewCommands.Serialize(response);
        }, ct);

    private static Task<IResult> ManualDecide(Guid id, Guid assignmentId, DecisionInput input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunManual(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var (source, invoice) = await Source(db, scope, id, true, token);
            RequireManualSource(source);
            var actor = await Actor(core, scope, http, auth, source.BranchRef, [InvoiceRetainedUnitReview.ReviewPermission], token);
            if (input.Action is not ("Approve" or "Return" or "Reject") || input.Action == "Approve" && input.Attestation != InvoiceUnitReviewCommands.ManualUnitAttestation)
                throw new ReviewProblem("InvoiceManualUnitAttestationRequired", 422);
            var key = Key(http); var request = new InvoiceUnitReviewRequest(2, "Decision", key, invoice.Id, source.Id, assignmentId,
                input.InvoiceVersion, null, input.CaseVersion, input.AssignmentVersion, input.Action, Reason(input.Reason), input.Attestation);
            if (request.Reason is null) throw new ReviewProblem("InvoiceManualUnitReviewReasonRequired", 422);
            if (await ReplayManual(db, scope, request, invoice, token) is { } replay) return replay;
            if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId, source.MakerPrincipalId, source.MakerWorkerId, actor.PrincipalId, actor.WorkerId, token))
                throw new ReviewProblem("InvoiceUnitReviewIndependentReviewerRequired", 403);
            var workflow = await Workflow(cases, scope, source, true, token);
            var pins = await db.InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == id).ToArrayAsync(token);
            var owned = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
            var mapping = await Mapping(db, invoice, token); var originals = await Originals(db, invoice.Id, token);
            var matching = await db.PolicyVersions.SingleAsync(x => x.Id == source.MatchingPolicyId, token);
            var review = await db.PolicyVersions.SingleAsync(x => x.Id == source.ReviewPolicyId, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, matching, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, review, token);
            RequireManualPolicy(review);
            RequireManualMapping(mapping, invoice, originals);
            var hashes = EmptyManualDocumentHashes;
            var issued = await ReviewIssuedOrder(db, cases, core, scope, actor, mapping, source.PurchaseOrderId, source.IssuedVersionId, token);
            var decisions = (await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToArrayAsync(token)).ToList(); var before = decisions.Select(x => x.Id).ToHashSet(); var now = Now();
            _ = InvoiceUnitReviewTransitions.ApplyNewDecision(owned, invoice, originals, mapping, issued, matching, review, actor, hashes, assignmentId,
                new(key, input.InvoiceVersion, input.CaseVersion, input.AssignmentVersion, input.Action, request.Reason, now), decisions);
            var vote = decisions.Single(x => !before.Contains(x.Id)); cases.Decisions.Add(vote);
            InvoiceUnitReviewOutcome? outcome = null;
            if (workflow.State != "Pending")
            {
                var sealedOutcome = InvoiceUnitReviewTerminal.Seal(owned, invoice, originals, mapping, issued, matching, review, hashes, decisions, vote.Id, now);
                outcome = InvoiceUnitReviewStorage.Outcome(sealedOutcome, source); db.AddScoped(outcome);
            }
            await cases.SaveChangesAsync(token);
            var response = new InvoiceUnitReviewResponse(1, invoice.Id, source.Id, workflow.Id, key, "Decision", invoice.Version,
                workflow.Version, workflow.State, outcome?.State ?? "Pending", vote.Id, outcome?.Id, now, true, false, false);
            response = InvoiceUnitReviewCommands.BindSource(response, source, outcome);
            Receipt(db, scope, source, request, response, actor.WorkerId);
            db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceUnitReviewSource), RecordId = id, ActorSubjectId = scope.SubjectId,
                Action = "ManualUnitReviewDecision", ChangedFieldsJson = InvoiceUnitReviewCommands.Serialize(new { invoiceId = invoice.Id, assignmentId, decisionId = vote.Id, input.Action, state = response.ReviewState, commandKey = key }) });
            if (outcome is not null) db.AddScoped(new OutboxEvent { AggregateType = nameof(InvoiceUnitReviewSource), AggregateId = id,
                EventType = "invoice.manual_units_reviewed.v1", IdempotencyKey = key.ToString("D"), PayloadJson = InvoiceUnitReviewCommands.Serialize(new { invoiceId = invoice.Id, reviewId = id, state = outcome.State, canPostToFinance = false }) });
            await db.SaveChangesAsync(token); return InvoiceUnitReviewCommands.Serialize(response);
        }, ct);

}
