using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> IssueOrder(Guid id, OrderVersionInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Issue", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, true, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-issue", token);
                var prior = await Receipt(db, scope, "Issue", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                if (load.Order.Version != input.ExpectedOrderVersion) Conflict("OrderIssueVersionChanged");
                var pin = await db.OrderSubmissions.SingleOrDefaultAsync(x => x.PurchaseOrderId == id
                    && x.CommercialRevision == load.Order.CurrentRevision, token) ?? throw new OrderProblem("OrderSubmissionMissing", 409);
                await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={pin.CoreApprovalCaseId} FOR UPDATE""", token);
                var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleAsync(x => x.Id == pin.CoreApprovalCaseId, token);
                var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, token);
                var now = DateTimeOffset.UtcNow;
                var finance = await CurrentFinance(db, core, scope, load, options, pin.CommercialRevision, now, token);
                // Recheck the complete current approval authority, never turn issue into a new approval.
                var assignments = await db.OrderAssignmentAuthority.Where(x => x.SubmissionId == pin.Id).ToArrayAsync(token);
                var tasks = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
                if (assignments.Length != tasks.Length || tasks.Any(x => !assignments.Any(p => p.CoreAssignmentId == x.Id
                    && p.PrincipalId == x.AssigneeSubjectId && p.CoreStageId == x.StageId))) Conflict("OrderAssignmentSetChanged");
                foreach (var assignment in assignments)
                    await ProcurementOrderAuthorityResolver.ValidateAsync(load.Order, load.Sources, pin, assignment,
                        core, db, scope, now, token);
                var issued = PurchaseOrderCommercialWorkflow.AuthorizeIssue(load.Order, load.Sources, load.Supplier,
                    pin, policy, finance, workflow, scope.SubjectId, now);
                db.AddScoped(issued);
                Event(db, load.Order, "purchase_order.dispatch_requested.v1", issued.IssueEffectKey,
                    new { orderId = id, issuedVersionId = issued.Id, issued.RevisionNumber, issued.CommercialSnapshotSha256 });
                Audit(db, scope, load.Order, "IssueAuthorized", new { issued.Id, issued.RevisionNumber, status = "IssuePending" });
                return await SaveReply(db, scope, load.Order, "Issue", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> ConfirmOrderDelivery(Guid id, OrderDeliveryInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Delivery", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                // Delivery confirms an already sealed payload, so master-data suspension or
                // historical source offboarding must not hide the external delivery outcome.
                await LockOrder(db, scope, id, token);
                var order = await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new OrderProblem("OrderNotFound", 404);
                Scope(order, scope, false);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-dispatch-confirm", token);
                var prior = await Receipt(db, scope, "Delivery", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                if (order.Version != input.ExpectedOrderVersion) Conflict("OrderDeliveryVersionChanged");
                if (string.IsNullOrWhiteSpace(options.ApprovedManualDispatchPolicyReference)
                    || !options.AllowedDeliveryChannels.Contains(input.Channel, StringComparer.Ordinal)
                    || input.DeliveryReference?.Length > 200 || input.EvidenceReference?.Length > 500)
                    Conflict("OrderDispatchPolicyRequired");
                var issued = await db.OrderIssuedVersions.SingleOrDefaultAsync(x => x.Id == input.IssuedVersionId
                    && x.PurchaseOrderId == id, token) ?? throw new OrderProblem("OrderIssuedVersionMissing", 409);
                var verifier = await Canonical(core, scope, scope.SubjectId, token);
                var delivery = PurchaseOrderCommercialWorkflow.ConfirmDelivery(order, issued, input.DeliveryReference,
                    input.Outcome, input.Channel, input.EvidenceReference, scope.SubjectId, input.ObservedAt,
                    DateTimeOffset.UtcNow, verifier.WorkerId);
                db.AddScoped(delivery);
                Audit(db, scope, order, "SupplierDeliveryObserved", new { issuedVersionId = issued.Id,
                    delivery.Outcome, policyReference = options.ApprovedManualDispatchPolicyReference });
                Event(db, order, "purchase_order.delivery_observed.v1", $"procurement.po.delivery.{delivery.Id:N}.v1",
                    new { orderId = id, issuedVersionId = issued.Id, delivery.Outcome });
                return await SaveReply(db, scope, order, "Delivery", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }
}
