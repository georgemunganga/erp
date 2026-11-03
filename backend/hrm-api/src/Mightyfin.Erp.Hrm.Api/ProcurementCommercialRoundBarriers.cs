using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    // Called only after existing M4 source/action/record-scope authorization.
    // Independent of feature flags: retained enrollment never becomes legacy M4.
    private static async Task<bool> RoundBarrierInstalled(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        var presence = await CommercialRoundPlanSchema.PresenceAsync(db, ct);
        if (presence == CommercialRoundSchemaPresence.Absent) return false;
        if (presence != CommercialRoundSchemaPresence.Complete
            || !await http.RequestServices.GetRequiredService<IProcurementCommercialRoundReadiness>().BarrierReadyAsync(db, ct))
            throw new Problem("RoundPlanPublicationBarrierUnavailable", 503);
        return true;
    }
    private static async Task RequireRoundPublicationAllowed(HttpContext http, ProcurementDbContext db, Guid eventId, CancellationToken ct)
    {
        if (await RoundBarrierInstalled(http, db, ct)
            && await db.CommercialRoundEnrollments.AsNoTracking().AnyAsync(x => x.SourcingEventId == eventId, ct))
            throw new Problem("CommercialRoundPublicationUnavailable", 409);
    }
    private static async Task RequireRoundSourceCancellationAllowed(HttpContext http, ProcurementDbContext db,
        SourcingEvent source, CancellationToken ct)
    {
        if (!await RoundBarrierInstalled(http, db, ct)
            || !await db.CommercialRoundEnrollments.AsNoTracking().AnyAsync(x => x.SourcingEventId == source.Id, ct)) return;
        var cancelled = await (from plan in db.CommercialRoundPlans.AsNoTracking()
            join revision in db.CommercialRoundPlanRevisions.AsNoTracking()
                on new { PlanId=plan.Id, ResultVersion=plan.Version } equals new { revision.PlanId, revision.ResultVersion }
            join receipt in db.CommercialRoundPlanReceipts.AsNoTracking()
                on new { PlanId=plan.Id, RevisionId=revision.Id, ResultVersion=plan.Version }
                equals new { receipt.PlanId, receipt.RevisionId, receipt.ResultVersion }
            where plan.SourcingEventId==source.Id && plan.Status=="Cancelled"
                && revision.ResultStatus=="Cancelled" && revision.Operation=="Cancel" && receipt.Operation=="Cancel"
            select plan.Id).AnyAsync(ct);
        if (!cancelled)
            throw new Problem("CancelCommercialRoundPlanFirst", 409);
    }
}
