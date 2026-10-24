using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    // Metadata discovery only. This does not authorize prices or assert that a
    // retained case/source/alias is still safe; detail rechecks all proof before
    // returning any offer count/body. No generic sourcing read is required.
    internal const string CommercialOpenedRoundScopeSql = """
        SELECT r.id AS "Value" FROM procurement.principal_rfq_commercial_round_openings r
        JOIN procurement.principal_rfq_commercial_offer_openings u
          ON u.tenant_id=r.tenant_id AND u.legal_entity_id=r.legal_entity_id
          AND u.opening_id=r.id AND u.sourcing_event_id=r.sourcing_event_id AND u.branch_id=r.branch_id
          AND u.approved_round_sha256=r.snapshot_sha256
        WHERE r.tenant_id=@tenant AND r.legal_entity_id=@company
          AND (@selected IS NULL OR r.branch_id=@selected)
          AND (@orgwide OR r.branch_id=ANY(@branches))
          AND r.snapshot_sha256=encode(sha256(convert_to(r.snapshot_json,'UTF8')),'hex')
          AND jsonb_typeof(r.snapshot_json::jsonb->'offerOpening'->'ReaderPrincipalIds')='array'
          AND (r.snapshot_json::jsonb->'offerOpening'->'ReaderPrincipalIds') @> jsonb_build_array(@principal::text)
        """;

    private static Task<IResult> CommercialOpenedRoundWorklist(HttpContext http,
        ProcurementRequestScope scope, int? offset, int? limit, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireOfferOpening(http, db, false, token);
            if (offset is < 0 or > 100000 || limit is < 1 or > 25)
                throw new Problem("RfqOpenedRoundPageInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqOfferOpeningControls.ReadAction, null, false, token);
            var query = db.Database.SqlQueryRaw<Guid>(CommercialOpenedRoundScopeSql,
                new NpgsqlParameter("tenant", scope.TenantId),
                new NpgsqlParameter("company", scope.LegalEntityId),
                new NpgsqlParameter("selected", NpgsqlDbType.Uuid) { Value = (object?)actor.SelectedBranchId ?? DBNull.Value },
                new NpgsqlParameter("orgwide", actor.OrganisationWide),
                new NpgsqlParameter("branches", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = actor.Branches.ToArray() },
                new NpgsqlParameter("principal", actor.PrincipalId));
            var total = await query.CountAsync(token);
            var ids = await query.OrderBy(x => x).Skip(offset ?? 0).Take(limit ?? 25).ToArrayAsync(token);
            var items = await (from round in db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                join opened in db.PrincipalRfqOfferOpenings.AsNoTracking() on round.Id equals opened.OpeningId
                where ids.Contains(round.Id)
                orderby round.Id
                select new { roundId = round.Id, round.SourcingEventId, round.BranchId,
                    opened.OpenedAt, unsealingId = opened.Id }).ToArrayAsync(token);
            return new Reply(JsonSerializer.Serialize(new { items, total, offset = offset ?? 0,
                limit = limit ?? 25, priceReadRequiresRetainedProof = true,
                evaluationAvailable = false, awardAvailable = false, purchaseOrderAvailable = false }, Json));
        }, ct);
}
