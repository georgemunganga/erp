using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> ConfirmFunding(Guid id, OrderFinanceInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Finance", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, false, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-finance-confirm", token, load.Order.BranchRef, true);
                var prior = await Receipt(db, scope, "Finance", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                var order = load.Order; var now = DateTimeOffset.UtcNow;
                if (order.Status != "Draft" || order.Version != input.ExpectedOrderVersion
                    || input.CommercialRevision != order.CurrentRevision + 1 || input.ConfirmedAt == default
                    || input.ConfirmedAt > now || input.SourceReservationReferences is null)
                    Conflict("OrderFinanceSourceChanged");
                var buyer = await Canonical(core, scope, order.BuyerSubjectId, token);
                var verifier = await Canonical(core, scope, scope.SubjectId, token);
                if (!PurchaseOrderWorkerIdentity.IsIndependent(PurchaseOrderWorkerIdentity.Participants(load.Sources, buyer.WorkerId), verifier.WorkerId))
                    Deny();
                var policy = ManualPolicy(options, scope, now);
                if (policy.EvidenceMaximumAgeMinutes is { } maximum && input.ConfirmedAt < now.AddMinutes(-maximum))
                    Conflict("OrderFinanceEvidenceExpired");
                if (string.IsNullOrWhiteSpace(input.CommitmentReference) || input.CommitmentReference.Length > 200
                    || string.IsNullOrWhiteSpace(input.EvidenceReference) || input.EvidenceReference.Length > 500
                    || input.ReplacesCommitmentReference?.Length > 200) Conflict("OrderFinanceReferenceRequired");
                if (input.CurrencyCode != order.CurrencyCode
                    || input.ConfirmedGrossAmount != PurchaseOrderCommercialWorkflow.Totals(load.Lines).Gross)
                    Conflict("OrderFinanceAmountMismatch");
                var expected = load.Sources.Select(x => x.Source.Finance.ReservationRef).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (expected.Any(string.IsNullOrWhiteSpace) || input.SourceReservationReferences.Any(string.IsNullOrWhiteSpace)
                    || input.SourceReservationReferences.Distinct(StringComparer.Ordinal).Count() != input.SourceReservationReferences.Count
                    || !expected.SequenceEqual(input.SourceReservationReferences.Order(StringComparer.Ordinal)))
                    Conflict("OrderFinanceReservationTransferMismatch");
                var previous = await db.OrderFinanceEvidence.Where(x => x.PurchaseOrderId == id)
                    .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(token);
                if (previous is not null && input.ReplacesCommitmentReference != previous.CommitmentReference)
                    Conflict("OrderFinanceReplacementNotConfirmed");
                db.AddScoped(new PurchaseOrderFinanceEvidence { PurchaseOrderId = id, SupplierId = order.SupplierId,
                    CommercialRevision = input.CommercialRevision, CommitmentReference = input.CommitmentReference.Trim(),
                    EvidenceReference = input.EvidenceReference.Trim(), EvidenceMode = "ApprovedManualEvidence",
                    ApprovedManualPolicyReference = policy.ApprovedPolicyReference, Status = "Confirmed",
                    CurrencyCode = order.CurrencyCode, ConfirmedGrossAmount = input.ConfirmedGrossAmount,
                    SourceReservationsJson = JsonSerializer.Serialize(expected),
                    CommercialSnapshotSha256 = ProcurementEvidenceEncoding.Digest(PurchaseOrderCommercialWorkflow.Snapshot(
                        order, load.Lines, load.Supplier, input.CommercialRevision)),
                    VerifiedByPrincipalId = scope.SubjectId, VerifiedByWorkerId = verifier.WorkerId,
                    ConfirmedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(input.ConfirmedAt)!.Value,
                    ReplacesCommitmentReference = input.ReplacesCommitmentReference });
                order.FinanceCommitmentRef = input.CommitmentReference.Trim(); order.UpdatedAt = now;
                Audit(db, scope, order, "FinanceCommitmentConfirmed", new { input.CommercialRevision, commandKey = key });
                return await SaveReply(db, scope, order, "Finance", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<ProcurementAuthorityIdentity.CanonicalAccount> Canonical(HrmDbContext core,
        ProcurementRequestScope scope, string principal, CancellationToken ct) =>
        await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, principal, ct)
        ?? throw new OrderProblem("OrderCurrentIdentityUnavailable", 409);

    private static ProcurementManualFinancePolicy ManualPolicy(ProcurementOrderExecutionOptions options,
        ProcurementRequestScope scope, DateTimeOffset now)
    {
        var policies = options.ManualCommitmentPolicies.Where(x => x.LegalEntityId == scope.LegalEntityId && x.Enabled
            && x.EffectiveFrom != default && x.EffectiveFrom <= now && (x.EffectiveUntil == null || now < x.EffectiveUntil)
            && !string.IsNullOrWhiteSpace(x.ApprovedPolicyReference)
            && (x.EvidenceMaximumAgeMinutes is null or > 0)).ToArray();
        return policies.Length == 1 ? policies[0] : throw new OrderProblem("OrderApprovedFinancePolicyRequired", 409);
    }

    private static async Task<PurchaseOrderFinanceEvidence> CurrentFinance(ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, OrderLoad load, ProcurementOrderExecutionOptions options,
        int revision, DateTimeOffset now, CancellationToken ct)
    {
        var evidence = await db.OrderFinanceEvidence.Where(x => x.PurchaseOrderId == load.Order.Id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw new OrderProblem("OrderFinanceConfirmationRequired", 409);
        var policy = ManualPolicy(options, scope, now);
        var verifier = await Canonical(core, scope, evidence.VerifiedByPrincipalId, ct);
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, verifier.RolesCsv, ct);
        var admin = LocalIdentityRoutes.ParseRoles(verifier.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase);
        if (evidence.Status != "Confirmed" || evidence.CommercialRevision != revision
            || evidence.ApprovedManualPolicyReference != policy.ApprovedPolicyReference
            || evidence.VerifiedByWorkerId != verifier.WorkerId || evidence.ConfirmedAt > now
            || policy.EvidenceMaximumAgeMinutes is { } maximum && evidence.ConfirmedAt < now.AddMinutes(-maximum)
            || !admin && (!actions.Contains("procurement-access") || !actions.Contains("procurement-po-finance-confirm")))
            Conflict("OrderFinanceConfirmationUnavailable");
        return evidence;
    }
}
