using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> ResolveOrderFunding(Guid id, OrderFinanceResolutionInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "FinanceRelease", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, false, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-finance-confirm", token);
                var prior = await Receipt(db, scope, "FinanceRelease", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                if (load.Order.Version != input.ExpectedOrderVersion || input.RestoredReservationReferences is null
                    || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000
                    || string.IsNullOrWhiteSpace(input.EvidenceReference) || input.EvidenceReference.Length > 500)
                    Conflict("OrderFinanceReleaseInputInvalid");
                var pin = await db.OrderSubmissions.SingleOrDefaultAsync(x => x.PurchaseOrderId == id
                    && x.CommercialRevision == load.Order.CurrentRevision, token)
                    ?? throw new OrderProblem("OrderSubmissionMissing", 409);
                await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={pin.CoreApprovalCaseId} FOR UPDATE""", token);
                var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleAsync(x => x.Id == pin.CoreApprovalCaseId, token);
                var finance = await db.OrderFinanceEvidence.SingleAsync(x => x.Id == pin.FinanceEvidenceId, token);
                var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, token);
                var assignments = await db.OrderAssignmentAuthority.Where(x => x.SubmissionId == pin.Id).ToArrayAsync(token);
                var now = DateTimeOffset.UtcNow; var authority = ManualPolicy(options, scope, now);
                if (authority.EvidenceMaximumAgeMinutes is { } maximum && input.ConfirmedAt < now.AddMinutes(-maximum))
                    Conflict("OrderFinanceReleaseEvidenceExpired");
                var verifier = await Canonical(core, scope, scope.SubjectId, token);
                var resolution = PurchaseOrderFinanceResolutionWorkflow.Resolve(load.Order, load.Sources, pin, policy, finance,
                    workflow, assignments, input.EvidenceReference.Trim(), authority.ApprovedPolicyReference,
                    input.ReleasedGrossAmount, input.CurrencyCode, input.RestoredReservationReferences,
                    scope.SubjectId, verifier.WorkerId, input.Reason.Trim(), input.ConfirmedAt, now);
                db.AddScoped(resolution);
                Audit(db, scope, load.Order, "FinanceCommitmentReleaseConfirmed", new { pin.Id, resolution.Outcome, commandKey = key });
                Event(db, load.Order, "purchase_order.finance_release_confirmed.v1", $"procurement.po.finance_release.{pin.Id:N}.v1",
                    new { orderId = id, submissionId = pin.Id, resolution.Outcome });
                return await SaveReply(db, scope, load.Order, "FinanceRelease", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }
}
