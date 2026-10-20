using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> SubmitOrder(Guid id, OrderVersionInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Submit", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                if (!await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, http.RequestServices, false, token))
                    throw new OrderProblem("FinanceCheckerSchemaUnavailable", 503);
                var load = await Load(db, cases, scope, id, true, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-submit", token, load.Order.BranchRef, true);
                var prior = await Receipt(db, scope, "Submit", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                await CurrentSourceCheckers(db, core, scope, load.Sources.Select(x => x.Source), token);
                await CatalogCommercial(db, load.Order.SupplierId, load.Order.CurrencyCode,
                    load.Sources.Select(x => (x.Source.Line, x.OrderLine)), token);
                var order = load.Order; var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
                if (order.Status != "Draft" || order.Version != input.ExpectedOrderVersion) Conflict("OrderSubmitSourceChanged");
                var policies = await db.PolicyVersions.Where(x => x.PolicyKey == "PURCHASE_ORDER" && x.State == "Published"
                    && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo)).ToArrayAsync(token);
                if (policies.Length != 1) Conflict("OrderPublishedPolicyRequired");
                var buyer = await Canonical(core, scope, order.BuyerSubjectId, token);
                var finance = await CurrentFinance(db, core, scope, load, options, order.CurrentRevision + 1, now, token);
                var route = PurchaseOrderCommercialWorkflow.PolicyRoute(order, load.Sources, policies[0], now);
                var plan = await ProcurementOrderAuthorityResolver.ResolveAsync(order, load.Sources, route.Stages,
                    buyer.WorkerId, core, db, scope, now, token);
                var workflow = new ApprovalCase { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    SourceModule = "procurement", SourceType = "purchase_order", SourceId = id,
                    SourceVersion = order.CurrentRevision + 1, PolicyVersionId = policies[0].Id,
                    PolicyVersionNumber = policies[0].PolicyVersionNumber, MakerSubjectId = order.BuyerSubjectId, OpenedAt = now };
                var pin = PurchaseOrderCommercialWorkflow.Submit(order, load.Sources, load.Supplier, policies[0], finance,
                    workflow.Id, now, buyer.WorkerId);
                var sequence = 0;
                foreach (var resolved in plan)
                {
                    var stage = new ApprovalStage { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                        CaseId = workflow.Id, Sequence = ++sequence, Code = resolved.Route.Code,
                        Mode = resolved.Route.Mode == "One" ? "Any" : "All",
                        RequiredApprovals = resolved.Route.Mode == "One" ? 1 : resolved.Assignees.Count,
                        State = sequence == 1 ? "Active" : "Waiting" };
                    foreach (var candidate in resolved.Assignees)
                    {
                        var assignment = new ApprovalAssignment { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                            CaseId = workflow.Id, StageId = stage.Id, AssigneeSubjectId = candidate.PrincipalId,
                            PermissionKey = "procurement-po-approve" };
                        stage.Assignments.Add(assignment);
                        var evidence = JsonSerializer.Serialize(new PurchaseOrderAssignmentIdentityEvidence(candidate.WorkerId, candidate.Authority));
                        db.AddScoped(new PurchaseOrderAssignmentAuthorityEvidence { SubmissionId = pin.Id,
                            CoreApprovalCaseId = workflow.Id, CoreAssignmentId = assignment.Id, CoreStageId = stage.Id,
                            GrantId = candidate.Authority.GrantId, GrantRevision = candidate.Authority.GrantRevision,
                            AuthorityVersion = candidate.Authority.AuthorityVersion, PrincipalId = candidate.PrincipalId,
                            AssigneeWorkerId = candidate.WorkerId, AssigneeKind = resolved.Route.Assignee,
                            AuthorityEvidenceJson = evidence, AuthorityEvidenceSha256 = ProcurementEvidenceEncoding.Digest(evidence) });
                    }
                    workflow.Stages.Add(stage);
                }
                cases.AddCase(workflow); db.AddScoped(pin);
                Audit(db, scope, order, "CommercialApprovalOpened", new { pin.Id, caseId = workflow.Id, pin.CommercialRevision });
                Event(db, order, "purchase_order.approval_opened.v1", $"procurement.po.submitted.{pin.Id:N}.v1",
                    new { orderId = id, caseId = workflow.Id, pin.CommercialRevision });
                await cases.SaveChangesAsync(token);
                return await SaveReply(db, scope, order, "Submit", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json", statusCode: 201);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> DecideOrder(Guid id, Guid assignmentId, OrderDecisionInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http); var reason = Clean(input.Reason);
            if (input.Action is not ("Approve" or "Reject" or "Return") || reason?.Length > 2000
                || input.Action != "Approve" && reason is null) Conflict("OrderDecisionInvalid");
            var fingerprint = Fingerprint(new { operation = "Decide", id, assignmentId, actor = scope.SubjectId,
                input.Action, reason, input.ExpectedOrderVersion, input.ExpectedCaseVersion, input.ExpectedAssignmentVersion });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                if (!await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, http.RequestServices, false, token))
                    throw new OrderProblem("FinanceCheckerSchemaUnavailable", 503);
                var load = await Load(db, cases, scope, id, false, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-approve", token, load.Order.BranchRef, true);
                var assignmentPin = await db.OrderAssignmentAuthority.SingleOrDefaultAsync(x => x.CoreAssignmentId == assignmentId
                    && x.PrincipalId == scope.SubjectId, token) ?? throw new OrderProblem("OrderAssignmentNotFound", 404);
                var pin = await db.OrderSubmissions.SingleOrDefaultAsync(x => x.Id == assignmentPin.SubmissionId
                    && x.PurchaseOrderId == id, token) ?? throw new OrderProblem("OrderSubmissionNotFound", 404);
                var now = DateTimeOffset.UtcNow;
                var actor = await ProcurementOrderAuthorityResolver.ValidateAsync(load.Order, load.Sources, pin,
                    assignmentPin, core, db, scope, now, token);
                var prior = await Receipt(db, scope, "Decide", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={pin.CoreApprovalCaseId} FOR UPDATE""", token);
                var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleOrDefaultAsync(x => x.Id == pin.CoreApprovalCaseId, token) ?? throw new OrderProblem("OrderCaseNotFound", 404);
                var assignment = workflow.Stages.SelectMany(x => x.Assignments).SingleOrDefault(x => x.Id == assignmentId
                    && x.AssigneeSubjectId == scope.SubjectId && x.DelegatedFromSubjectId == null && x.PermissionKey == "procurement-po-approve")
                    ?? throw new OrderProblem("OrderAssignmentNotFound", 404);
                if (assignment.StageId != assignmentPin.CoreStageId) Conflict("OrderAssignmentStageChanged");
                if (workflow.Version != input.ExpectedCaseVersion || assignment.Version != input.ExpectedAssignmentVersion
                    || load.Order.Version != input.ExpectedOrderVersion) Conflict("OrderDecisionVersionChanged");
                var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, token);
                var finance = input.Action == "Approve"
                    ? await CurrentFinance(db, core, scope, load, options, pin.CommercialRevision, now, token)
                    : await db.OrderFinanceEvidence.SingleAsync(x => x.Id == pin.FinanceEvidenceId, token);
                if (input.Action == "Approve")
                    await CurrentSourceCheckers(db, core, scope, load.Sources.Select(x => x.Source), token);
                if (input.Action == "Approve")
                    PurchaseOrderCommercialWorkflow.Verify(load.Order, load.Sources, load.Supplier, pin, policy, finance, now);
                else PurchaseOrderCommercialWorkflow.VerifyForReturn(load.Order, load.Sources, pin, policy, finance);
                if (input.Action == "Approve")
                {
                    var assignmentPins = await db.OrderAssignmentAuthority.Where(x => x.SubmissionId == pin.Id).ToArrayAsync(token);
                    var all = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
                    if (assignmentPins.Length != all.Length || all.Any(x => !assignmentPins.Any(p => p.CoreAssignmentId == x.Id
                        && p.PrincipalId == x.AssigneeSubjectId && p.CoreStageId == x.StageId))) Conflict("OrderAssignmentSetChanged");
                    foreach (var assigned in assignmentPins)
                        await ProcurementOrderAuthorityResolver.ValidateAsync(load.Order, load.Sources, pin,
                            assigned, core, db, scope, now, token);
                }
                var decisions = await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToListAsync(token);
                var before = decisions.Count;
                ApprovalEngine.Decide(workflow, assignment, new(scope.SubjectId, scope.TenantId, scope.LegalEntityId,
                    new HashSet<string> { "procurement-po-approve" }), input.Action, key.ToString("D"), reason, decisions, now);
                var decision = decisions.Single(x => x.IdempotencyKey == key.ToString("D"));
                if (decisions.Count > before) cases.Decisions.Add(decision);
                PurchaseOrderCommercialWorkflow.ApplyDecision(load.Order, pin, workflow, decision, reason, actor.WorkerId);
                Audit(db, scope, load.Order, "CommercialApprovalDecision", new { assignmentId, input.Action, reason, decision.Id });
                Event(db, load.Order, "purchase_order.approval_decided.v1", $"procurement.po.decision.{decision.Id:N}.v1",
                    new { orderId = id, caseId = workflow.Id, input.Action, state = workflow.State });
                await cases.SaveChangesAsync(token);
                return await SaveReply(db, scope, load.Order, "Decide", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }
}
