using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentCommandRoutes
{
    private static Task<IResult> Create(JsonElement body, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct)
        => Run(http, scope, async (db, _, core, auth, token) =>
        {
            var key = CommandKey(http); var hash = Fingerprint("Create", null, body);
            var input = Parse<CreateInput>(body);
            if (input.BranchId == Guid.Empty || input.InvoiceId == Guid.Empty) throw new PaymentProblem("payment-input-invalid", 422);
            var person = await Actor(core, scope, http, auth, input.BranchId, "Create", token);
            if (await Replay(db, core, scope, auth, http, key, "Create", hash, null, token) is { } replay) return replay;
            var source = (await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, input.InvoiceId,
                input.BranchId, AllowedBranches(person, input.BranchId), ct: token)).Source;
            var assignees = await AssignedPeople(core, scope, input.BranchId, input.ApproverPrincipalId,
                input.RecorderPrincipalId, input.ConfirmerPrincipalId, token);
            var change = ManualPaymentWorkflow.Create(new(input.BranchId, input.InvoiceId, source.SupplierId,
                    source.CurrencyCode, input.Amount, input.ApproverPrincipalId, input.RecorderPrincipalId,
                    input.ConfirmerPrincipalId, input.Purpose, input.Title),
                DomainActor(person, scope, input.BranchId), assignees.Approver, assignees.Recorder,
                assignees.Confirmer, source, Now(http.RequestServices));
            if (!await ProcurementManualPaymentPeople.IndependentAssignmentsNow(core, change.Request, token))
                throw new PaymentProblem("payment-independent-assignment-required", 422);
            var response = Response(change, key);
            Persist(db, scope, change, key, hash, response, create: true);
            return response;
        }, ct, created: true);

    private static Task<IResult> Revise(Guid id, JsonElement body, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, _, core, auth, token) =>
        {
            var key = CommandKey(http); var hash = Fingerprint("Revise", id, body);
            var input = Parse<ReviseInput>(body);
            if (id == Guid.Empty || input.ExpectedVersion < 1) throw new PaymentProblem("payment-input-invalid", 422);
            var peek = await db.ManualPaymentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new KeyNotFoundException();
            var person = await Actor(core, scope, http, auth, peek.BranchId, "Revise", token);
            if (await Replay(db, core, scope, auth, http, key, "Revise", hash, id, token) is { } replay) return replay;
            var source = (await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, peek.SupplierInvoiceId,
                peek.BranchId, AllowedBranches(person, peek.BranchId), id, token)).Source;
            var current = await Current(db, scope, id, token);
            if (current.SupplierInvoiceId != peek.SupplierInvoiceId || current.BranchId != peek.BranchId
                || current.Version != input.ExpectedVersion) throw new PaymentProblem("payment-version-changed", 409);
            var assignees = await AssignedPeople(core, scope, current.BranchId, input.ApproverPrincipalId,
                input.RecorderPrincipalId, input.ConfirmerPrincipalId, token);
            var change = ManualPaymentWorkflow.Revise(current, input.ExpectedVersion,
                new(current.BranchId, current.SupplierInvoiceId, current.SupplierId, current.CurrencyCode,
                    input.Amount, input.ApproverPrincipalId, input.RecorderPrincipalId, input.ConfirmerPrincipalId,
                    input.Purpose, input.Title), DomainActor(person, scope, current.BranchId),
                assignees.Approver, assignees.Recorder, assignees.Confirmer, source,
                Now(http.RequestServices));
            if (!await ProcurementManualPaymentPeople.IndependentAssignmentsNow(core, change.Request, token))
                throw new PaymentProblem("payment-independent-assignment-required", 422);
            var response = Response(change, key);
            Persist(db, scope, change, key, hash, response, create: false);
            return response;
        }, ct);

    private static Task<IResult> Transition(Guid id, string action, JsonElement body, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = CommandKey(http); var hash = Fingerprint(action, id, body);
            var input = Parse<TransitionInput>(body);
            ValidateInput(action, id, input);
            var peek = await db.ManualPaymentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new KeyNotFoundException();
            var person = await Actor(core, scope, http, auth, peek.BranchId, action, token);
            if (await Replay(db, core, scope, auth, http, key, action, hash, id, token) is { } replay) return replay;
            var source = (await ManualPaymentCapacityReader.ReadLockedAsync(db, scope, peek.SupplierInvoiceId,
                peek.BranchId, AllowedBranches(person, peek.BranchId), id, token)).Source;
            var current = await Current(db, scope, id, token);
            if (current.SupplierInvoiceId != peek.SupplierInvoiceId || current.BranchId != peek.BranchId
                || current.Version != input.ExpectedVersion) throw new PaymentProblem("payment-version-changed", 409);
            await CurrentAssignment(core, scope, current, action, person, token);
            var actor = DomainActor(person, scope, current.BranchId);
            var clock = http.RequestServices.GetRequiredService<TimeProvider>();
            CoreCompanyBusinessDate? businessDate = null;
            if (action is "Record" or "Confirm")
                businessDate = await new CoreCompanyBusinessDateProvider(core, clock)
                    .GetAsync(scope.TenantId, scope.LegalEntityId, token);
            var now = businessDate is null ? Now(http.RequestServices)
                : ProcurementEvidenceEncoding.DatabaseTimestamp(businessDate.ObservedAtUtc)!.Value;
            var reason = input.Reason is null ? null : string.Join(" ", input.Reason.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
            var change = action switch
            {
                "Submit" => ManualPaymentWorkflow.Submit(current, current.Version, actor, source, now),
                "Approve" => ManualPaymentWorkflow.Approve(current, current.Version, actor, source, now),
                "Return" => ManualPaymentWorkflow.Return(current, current.Version, actor, reason!, now),
                "Reject" => ManualPaymentWorkflow.Reject(current, current.Version, actor, reason!, now),
                "Record" => ManualPaymentWorkflow.Record(current, current.Version, actor, source,
                    new ManualPaymentRecordedEvidence(input.PaidOn!.Value, input.ExternalMethod!,
                        input.ExternalReference!, input.Amount!.Value, null), businessDate!.Date, now),
                "Confirm" => ManualPaymentWorkflow.Confirm(current, current.Version, actor, businessDate!.Date, now),
                "Dispute" => ManualPaymentWorkflow.Dispute(current, current.Version, actor, reason!, now),
                "Cancel" => ManualPaymentWorkflow.Cancel(current, current.Version, actor, reason!, now),
                _ => throw new PaymentProblem("payment-action-invalid", 422),
            };
            var response = Response(change, key);
            // All companion records are added before the coordinator's Core-first / Procurement-second save.
            if (action == "Submit")
            {
                var policy = await CurrentPolicy(db, scope, http, now, token);
                var opening = ManualPaymentCoreWorkflow.Open(change, policy, source, now);
                cases.AddCase(opening.Case);
                db.AddScoped(opening.Link);
            }
            else if (action == "Record")
            {
                var original = await CaseLink(db, id, ManualPaymentCoreWorkflow.Approval, token);
                var approved = await CoreCase(cases, original, token);
                var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == original.PolicyVersionId, token)
                    ?? throw new PaymentProblem("payment-policy-unavailable", 409);
                await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, token,
                    policy.State == "Published" ? OperationalPolicyPublicationMode.CurrentPublished
                        : OperationalPolicyPublicationMode.RetainedHistorical);
                var opening = ManualPaymentCoreWorkflow.Open(change, policy, source, now, original, approved);
                cases.AddCase(opening.Case);
                db.AddScoped(opening.Link);
                db.AddScoped(ManualPaymentCoreWorkflow.ObserveDate(change, businessDate!.Date, businessDate.TimeZoneId, now));
            }
            else if (action is "Approve" or "Return" or "Reject" or "Confirm" or "Dispute"
                || action == "Cancel" && current.Status == "PendingApproval")
            {
                var purpose = action is "Confirm" or "Dispute"
                    ? ManualPaymentCoreWorkflow.Confirmation : ManualPaymentCoreWorkflow.Approval;
                var link = await CaseLink(db, id, purpose, token);
                var workflow = await CoreCase(cases, link, token);
                var decisions = new List<ApprovalDecision>();
                var assignment = workflow.Stages.Single().Assignments.Single();
                var voter = new ApprovalActor(scope.SubjectId, scope.TenantId, scope.LegalEntityId,
                    person.Permissions);
                if (action == "Cancel")
                    ApprovalEngine.Cancel(workflow, assignment, voter, Permission(action), key.ToString("D"),
                        reason!, decisions, now);
                else
                    ApprovalEngine.Decide(workflow, assignment, voter,
                        action is "Approve" or "Confirm" ? "Approve" : action == "Return" ? "Return" : "Reject",
                        key.ToString("D"), reason, decisions, now);
                var decision = decisions.Single();
                cases.Decisions.Add(decision);
                db.AddScoped(ManualPaymentCoreWorkflow.LinkDecision(change, link, workflow, decision));
            }
            if (action == "Confirm")
                db.AddScoped(ManualPaymentCoreWorkflow.ObserveDate(change, businessDate!.Date, businessDate.TimeZoneId, now));
            Persist(db, scope, change, key, hash, response, create: false);
            return response;
        }, ct);

    private static void ValidateInput(string action, Guid id, TransitionInput input)
    {
        if (id == Guid.Empty || input.ExpectedVersion < 1 || input.EvidenceDocumentRefId is not null)
            throw new PaymentProblem("payment-input-invalid", 422);
        if (action == "Record")
        {
            if (input.PaidOn is null || input.Amount is null || input.Amount <= 0
                || string.IsNullOrWhiteSpace(input.ExternalMethod) || string.IsNullOrWhiteSpace(input.ExternalReference)
                || input.Reason is not null) throw new PaymentProblem("payment-record-evidence-invalid", 422);
            return;
        }
        if (input.PaidOn is not null || input.ExternalMethod is not null || input.ExternalReference is not null
            || input.Amount is not null) throw new PaymentProblem("payment-input-invalid", 422);
        var needsReason = action is "Return" or "Reject" or "Dispute" or "Cancel";
        if (needsReason ? string.IsNullOrWhiteSpace(input.Reason) : input.Reason is not null)
            throw new PaymentProblem("payment-reason-invalid", 422);
    }

    private static IReadOnlySet<Guid> AllowedBranches(ProcurementPrincipalPeople.Person person, Guid branch)
        => person.OrganisationWide ? new HashSet<Guid> { branch } : person.AllowedBranches;

    private sealed record Assignees(ManualPaymentAssignee Approver, ManualPaymentAssignee Recorder,
        ManualPaymentAssignee Confirmer);
    private static async Task<Assignees> AssignedPeople(HrmDbContext core, ProcurementRequestScope scope, Guid branch,
        string approver, string recorder, string confirmer, CancellationToken ct)
        => new(await Assignee(core, scope, branch, approver, "Approve", ct),
            await Assignee(core, scope, branch, recorder, "Record", ct),
            await Assignee(core, scope, branch, confirmer, "Confirm", ct));

    private static async Task CurrentAssignment(HrmDbContext core, ProcurementRequestScope scope,
        ManualPaymentRequest request, string action, ProcurementPrincipalPeople.Person person, CancellationToken ct)
    {
        if (action == "Submit")
        {
            var current = await AssignedPeople(core, scope, request.BranchId, request.ApproverPrincipalId,
                request.RecorderPrincipalId, request.ConfirmerPrincipalId, ct);
            if (current.Approver.CoreLinkedWorkerId != request.ApproverCoreLinkedWorkerId
                || current.Recorder.CoreLinkedWorkerId != request.RecorderCoreLinkedWorkerId
                || current.Confirmer.CoreLinkedWorkerId != request.ConfirmerCoreLinkedWorkerId
                || !await ProcurementManualPaymentPeople.IndependentAssignmentsNow(core, request, ct))
                throw new PaymentProblem("payment-assignment-changed", 409);
        }
        else if (action is "Approve" or "Return" or "Reject")
        {
            if (request.ApproverPrincipalId != person.PrincipalId
                || request.ApproverCoreLinkedWorkerId != person.CoreLinkedWorkerId
                || !await ProcurementManualPaymentPeople.IndependentApprovalNow(core, request, person.PrincipalId, ct))
                throw new PaymentProblem("payment-independent-approver-required", 403);
        }
        else if (action == "Record")
        {
            var confirmer = await Assignee(core, scope, request.BranchId, request.ConfirmerPrincipalId, "Confirm", ct);
            if (request.RecorderPrincipalId != person.PrincipalId
                || request.RecorderCoreLinkedWorkerId != person.CoreLinkedWorkerId
                || confirmer.CoreLinkedWorkerId != request.ConfirmerCoreLinkedWorkerId
                || !await ProcurementManualPaymentPeople.IndependentRecordingNow(core, request, person.PrincipalId, ct))
                throw new PaymentProblem("payment-independent-recorder-required", 403);
        }
        else if (action is "Confirm" or "Dispute")
        {
            if (request.ConfirmerPrincipalId != person.PrincipalId
                || request.ConfirmerCoreLinkedWorkerId != person.CoreLinkedWorkerId
                || !await ProcurementManualPaymentPeople.IndependentConfirmationNow(core, request, person.PrincipalId, ct))
                throw new PaymentProblem("payment-independent-confirmer-required", 403);
        }
        else if (action == "Cancel" && request.ApproverPrincipalId == person.PrincipalId)
        {
            if (request.ApproverCoreLinkedWorkerId != person.CoreLinkedWorkerId
                || !await ProcurementManualPaymentPeople.IndependentApprovalNow(core, request, person.PrincipalId, ct))
                throw new PaymentProblem("payment-independent-approver-required", 403);
        }
    }

    private static async Task<PolicyVersion> CurrentPolicy(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == ManualPaymentReviewPolicyRules.PolicyKey
            && x.State == "Published" && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo))
            .Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new PaymentProblem("payment-policy-unavailable", 409);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, rows[0], ct);
        _ = ManualPaymentReviewPolicyRules.ResolveEffective(rows[0], scope.TenantId, scope.LegalEntityId, now);
        return rows[0];
    }
}
