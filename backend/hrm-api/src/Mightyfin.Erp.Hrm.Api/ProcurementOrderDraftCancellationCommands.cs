using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> CancelSourceDraft(Guid id, OrderSourceCancelInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "CancelDraft", id, actor = scope.SubjectId, input });
            var reply = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, true, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-edit", token, load.Order.BranchRef, true);
                var prior = await Receipt(db, scope, "CancelDraft", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                if (input.ExpectedOrderVersion != load.Order.Version || string.IsNullOrWhiteSpace(input.Reason)
                    || input.Reason.Length > 1000) Conflict("OrderDraftCancelInputInvalid");
                var finance = await db.OrderFinanceEvidence.Where(x => x.PurchaseOrderId == id)
                    .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(token);
                var resolution = finance is null ? null : await db.OrderFinanceResolutions
                    .SingleOrDefaultAsync(x => x.FinanceEvidenceId == finance.Id, token);
                PurchaseOrderFinanceResolutionWorkflow.CancelUnfundedDraft(load.Order, load.Sources, finance,
                    resolution, input.Reason.Trim(), DateTimeOffset.UtcNow);
                Audit(db, scope, load.Order, "UnfundedDraftCancelled", new { reason = input.Reason.Trim(), commandKey = key });
                Event(db, load.Order, "purchase_order.draft_cancelled.v1", $"procurement.po.cancel_draft.{key:N}.v1", new { orderId = id });
                return await SaveReply(db, scope, load.Order, "CancelDraft", key, fingerprint, token);
            }, ct);
            return Results.Content(reply, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> ResolveUnsubmittedFunding(Guid id, OrderFinanceResolutionInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "DraftFinanceRelease", id, actor = scope.SubjectId, input });
            var reply = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, false, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-finance-confirm", token, load.Order.BranchRef, true);
                var finance = await db.OrderFinanceEvidence.Where(x => x.PurchaseOrderId == id)
                    .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(token)
                    ?? throw new OrderProblem("OrderFinanceConfirmationRequired", 409);
                var verifier = await Canonical(core, scope, scope.SubjectId, token);
                if (finance.WorkerIdentitySha256 != PurchaseOrderWorkerIdentity.Digest(load.Order, finance.CommercialRevision,
                        Guid.Empty, finance.BuyerWorkerId, finance.ParticipantWorkersJson)
                    || finance.ParticipantWorkersJson != PurchaseOrderWorkerIdentity.Participants(load.Sources, finance.BuyerWorkerId)
                    || !PurchaseOrderWorkerIdentity.IsIndependent(finance.ParticipantWorkersJson, verifier.WorkerId)) Deny();
                var prior = await Receipt(db, scope, "DraftFinanceRelease", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                if (input.ExpectedOrderVersion != load.Order.Version || input.RestoredReservationReferences is null
                    || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000
                    || string.IsNullOrWhiteSpace(input.EvidenceReference) || input.EvidenceReference.Length > 500)
                    Conflict("OrderDraftFinanceReleaseInputInvalid");
                var now = DateTimeOffset.UtcNow; var policy = ManualPolicy(options, scope, now);
                if (policy.EvidenceMaximumAgeMinutes is { } maximum && input.ConfirmedAt < now.AddMinutes(-maximum))
                    Conflict("OrderDraftFinanceReleaseExpired");
                var resolution = PurchaseOrderFinanceResolutionWorkflow.ResolveUnsubmittedDraft(load.Order, load.Sources,
                    finance, input.EvidenceReference.Trim(), policy.ApprovedPolicyReference, input.ReleasedGrossAmount,
                    input.CurrencyCode, input.RestoredReservationReferences, scope.SubjectId, verifier.WorkerId,
                    input.Reason.Trim(), input.ConfirmedAt, now);
                db.AddScoped(resolution);
                Audit(db, scope, load.Order, "UnsubmittedFinanceReleaseConfirmed", new { financeEvidenceId = finance.Id, resolution.Outcome });
                Event(db, load.Order, "purchase_order.unsubmitted_finance_released.v1", $"procurement.po.draft_release.{finance.Id:N}.v1", new { orderId = id });
                return await SaveReply(db, scope, load.Order, "DraftFinanceRelease", key, fingerprint, token);
            }, ct);
            return Results.Content(reply, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }
}
