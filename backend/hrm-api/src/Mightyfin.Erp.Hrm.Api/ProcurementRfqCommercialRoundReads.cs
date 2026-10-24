using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static readonly string[] CommercialReadActions =
    [
        "procurement-sourcing-commercial-configure",
        "procurement-sourcing-round-open",
        PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
        PrincipalRfqSealedManualOffers.SubmitAction,
        PrincipalRfqSealedManualOffers.CloseAction,
    ];

    private static async Task<RfqPrincipalActor> CommercialReader(HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth,
        Guid? branch, CancellationToken ct)
    {
        foreach (var action in CommercialReadActions)
        {
            try { return await PrincipalActor(core, scope, http, auth, action, branch,
                branch is not null, ct); }
            catch (Problem error) when (error.Status == 403) { }
        }
        throw new Problem("RfqCommercialReadDenied", 403);
    }

    private static Task<IResult> CommercialRounds(HttpContext http,
        ProcurementRequestScope scope, int? offset, int? limit, CancellationToken ct)
        => Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            if (offset is < 0 or > 100000 || limit is < 1 or > 50)
                throw new Problem("RfqCommercialPageInvalid", 422);
            var actor = await CommercialReader(core, scope, http, auth, null, token);
            var branches = actor.Branches.ToArray();
            var rows = db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)));
            var total = await rows.CountAsync(token);
            var page = await rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip(offset ?? 0).Take(limit ?? 25)
                .Select(x => new { x.Id, x.SourcingEventId, x.PreparedPlanId, x.BranchId,
                    x.OpensAt, x.ClosesAt, x.CreatedAt, x.EventVersion })
                .ToArrayAsync(token);
            return new Reply(JsonSerializer.Serialize(new
            { items = page, total, offset = offset ?? 0, limit = limit ?? 25 }, Json));
        }, ct);

    private static Task<IResult> CommercialRound(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            if (id == Guid.Empty) throw new Problem("RfqCommercialRoundNotFound", 404);
            var actor = await CommercialReader(core, scope, http, auth, null, token);
            var branches = actor.Branches.ToArray();
            var opening = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)))
                .SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new Problem("RfqCommercialRoundNotFound", 404);
            _ = await CommercialReader(core, scope, http, auth, opening.BranchId, token);
            var decision = await db.PrincipalRfqCommercialRoundDecisions.AsNoTracking()
                .Where(x => x.OpeningId == id && x.BranchId == opening.BranchId)
                .Select(x => new { x.Id, x.State, x.DecidedAt, x.CoreCaseId })
                .SingleOrDefaultAsync(token);
            var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .Where(x => x.OpeningId == id && x.BranchId == opening.BranchId)
                .Select(x => new { x.Id, x.ClosedAt, x.SubmissionCount })
                .SingleOrDefaultAsync(token);
            // Submitted offer bodies remain sealed; this is only round status.
            return new Reply(JsonSerializer.Serialize(new
            { opening.Id, opening.SourcingEventId, opening.PreparedPlanId,
                opening.BranchId, opening.OpensAt, opening.ClosesAt,
                opening.EventVersion, decision, seal, awardMade = false,
                supplierDispatchConfirmed = false }, Json));
        }, ct);

    private static Task<IResult> CommercialRoundRecovery(string operation, Guid key,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            var action = operation switch
            {
                "Open" => "procurement-sourcing-round-open",
                "Decide" => PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
                "SubmitOffer" => PrincipalRfqSealedManualOffers.SubmitAction,
                "Close" => PrincipalRfqSealedManualOffers.CloseAction,
                _ => throw new Problem("RfqCommercialCommandNotFound", 404),
            };
            if (key == Guid.Empty) throw new Problem("RfqCommercialCommandNotFound", 404);
            var actor = await PrincipalActor(core, scope, http, auth, action, null, false, token);
            var branches = actor.Branches.ToArray();
            var receipt = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.CommandKey == key && x.Operation == operation
                    && x.ActorPrincipalId == actor.PrincipalId
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)))
                .SingleOrDefaultAsync(token)
                ?? throw new Problem("RfqCommercialCommandNotFound", 404);
            _ = await PrincipalActor(core, scope, http, auth, action, receipt.BranchId, true, token);
            var original = await new PrincipalRfqCommercialRoundStore(db, scope)
                .RecoverAsync(key, operation, receipt.SourcingEventId, null, token)
                ?? throw new Problem("RfqCommercialCommandNotFound", 404);
            return new Reply(original.ResponseJson, original.ResponseStatus);
        }, ct);
}
