using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private sealed record AssignedRules(PolicyVersion Policy, ReceivingActorProjection Actor, string CurrencyCode, string RuleScopeJson);
    private static async Task<AssignedRules> AssignedPolicy(ProcurementDbContext db,
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        Microsoft.AspNetCore.Authorization.IAuthorizationService auth, bool service, CancellationToken ct)
    {
        var actor = await Actor(core, scope, http, auth, service ? ControlledReceiving.CertifyPermission : ControlledReceiving.InspectPermission,
            scope.WorkLocationId, null, ct, target: false);
        var now = Now(); var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == "RECEIVING" && x.State == "Published"
            && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (policies.Length != 1) throw new ReceivingProblem("ReceivingPublishedPolicyUnavailable", 409);
        var document = ReceivingPolicyRules.Parse(policies[0].RulesJson);
        var rules = document.Rules.Where(rule => service ? rule.Kind == ReceivingKind.Service && rule.ServiceOwnerWorkerIds.Contains(actor.WorkerId)
            : rule.Kind == ReceivingKind.Goods && rule.InspectionRequired && rule.InspectorWorkerIds.Contains(actor.WorkerId)).ToArray();
        foreach (var rule in rules) _ = ReceivingPolicyRules.Project(policies[0], rule);
        var configuredSites = rules.SelectMany(rule => rule.SiteIds).Distinct().ToArray();
        var activeSites = await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
            && !x.IsArchived && configuredSites.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        var assigned = rules.SelectMany(rule => rule.SiteIds.Where(activeSites.Contains).Select(site => new { category = rule.CategoryCode,
            purchase_type = rule.PurchaseType, site })).ToArray();
        return new(policies[0], actor, document.CurrencyCode, JsonSerializer.Serialize(assigned));
    }
    // Immutable submitted source lineage drives assignment filtering before count/page.
    // A state pin is not present on an un-certified service draft, so use the existing
    // submission evidence rather than mutable PR classifications or another assignment table.
    private static IQueryable<PurchaseOrderLine> AssignedLines(ProcurementDbContext db, ProcurementRequestScope scope, AssignedRules assigned)
        => from candidate in db.PurchaseOrderLines.FromSqlInterpolated($"""
            SELECT line.* FROM procurement.purchase_order_lines line
            JOIN procurement.purchase_orders po ON po.id=line.purchase_order_id AND po.tenant_id=line.tenant_id AND po.legal_entity_id=line.legal_entity_id
            JOIN procurement.purchase_order_issued_versions issued ON issued.purchase_order_id=po.id AND issued.tenant_id=po.tenant_id
                AND issued.legal_entity_id=po.legal_entity_id AND issued.revision_number=po.current_revision
            JOIN procurement.purchase_order_submissions submitted ON submitted.id=issued.submission_id AND submitted.tenant_id=issued.tenant_id
                AND submitted.legal_entity_id=issued.legal_entity_id AND submitted.purchase_order_id=po.id
            WHERE line.tenant_id={scope.TenantId} AND line.legal_entity_id={scope.LegalEntityId} AND line.is_active AND po.status='Issued' AND po.currency_code={assigned.CurrencyCode}
                AND EXISTS (SELECT 1 FROM jsonb_array_elements(submitted.source_evidence_json::jsonb) src,
                    jsonb_to_recordset({assigned.RuleScopeJson}::jsonb) AS rule(category text,purchase_type text,site uuid)
                    WHERE src->>'orderLineId'=line.id::text AND src->>'CategoryCode'=rule.category
                        AND src->>'PurchaseType'=rule.purchase_type AND po.branch_ref=rule.site)
            """)
            join boundedOrder in BoundIssuedOrders(db, scope) on candidate.PurchaseOrderId equals boundedOrder.Id
            select candidate;
    private static Task<IResult> InspectionTasks(HttpContext http, ProcurementRequestScope scope, int? page, int? pageSize, CancellationToken ct)
        => AssignedTasks(false, http, scope, page, pageSize, ct);
    private static Task<IResult> ServiceTasks(HttpContext http, ProcurementRequestScope scope, int? page, int? pageSize, CancellationToken ct)
        => AssignedTasks(true, http, scope, page, pageSize, ct);
    private static Task<IResult> AssignedTasks(bool service, HttpContext http, ProcurementRequestScope scope, int? page, int? pageSize, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var assigned = await AssignedPolicy(db, core, scope, http, auth, service, token);
            var branches = assigned.Actor.BranchIds.ToArray(); var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var assignedLines = AssignedLines(db, scope, assigned);
            if (service)
            {
                var query = from row in db.ServiceAcceptances.AsNoTracking()
                    join line in assignedLines on row.PurchaseOrderLineId equals line.Id
                    join order in db.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
                    where row.ExecutionVersion == 1 && row.Status == "PendingCertification" && row.CertifiedAt == null
                        && row.SourcePurchaseOrderId == order.Id
                        && db.OrderIssuedVersions.Any(x => x.Id == row.IssuedBindingId && x.PurchaseOrderId == order.Id && x.RevisionNumber == order.CurrentRevision)
                        && (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                        && (branches.Length == 0 || order.BranchRef != null && branches.Contains(order.BranchRef.Value))
                    orderby row.CreatedAt, row.Id
                    select new { row.Id, row.Status, row.Version, row.DeliverableRef, row.PeriodFrom, row.PeriodTo,
                        orderId = order.Id, orderNumber = order.Number, purchaseOrderLineId = line.Id };
                return JsonSerializer.Serialize(new { items = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token),
                    total = await query.CountAsync(token), page = number, pageSize = size, purpose = "AssignedServiceCertification" }, Json);
            }
            else
            {
                var query = from row in db.GoodsReceiptLines.AsNoTracking()
                    join header in db.GoodsReceipts.AsNoTracking() on row.GoodsReceiptId equals header.Id
                    join line in assignedLines on row.PurchaseOrderLineId equals line.Id
                    join order in db.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
                    where row.ExecutionVersion == 1 && header.ExecutionVersion == 1 && header.RecordedAt != null
                        && row.PhysicalObservationId != null && row.PhysicalPolicyVersionId == assigned.Policy.Id
                        && header.SiteRef == order.BranchRef && header.BranchRef == order.BranchRef
                        && db.OrderIssuedVersions.Any(x => x.Id == header.IssuedBindingId && x.PurchaseOrderId == order.Id && x.RevisionNumber == order.CurrentRevision)
                        && !db.ReceiptInspections.Any(x => x.GoodsReceiptLineId == row.Id && x.ExecutionVersion == 1)
                        && (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                        && (branches.Length == 0 || order.BranchRef != null && branches.Contains(order.BranchRef.Value))
                    orderby header.CreatedAt, header.Id, row.Id
                    select new { receiptId = header.Id, header.Number, lineId = row.Id, row.Version, row.Status,
                        header.SiteRef, header.ReceivedAt, orderId = order.Id, orderNumber = order.Number };
                return JsonSerializer.Serialize(new { items = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token),
                    total = await query.CountAsync(token), page = number, pageSize = size, purpose = "AssignedInspection" }, Json);
            }
        }, ct);
    private static Task<IResult> InspectionTask(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var assigned = await AssignedPolicy(db, core, scope, http, auth, false, token);
            var (header, lines) = await LoadReceipt(db, id, token); ReceivingPersistenceProjection.VerifyHeader(header);
            var sourceLineIds = lines.Select(line => line.PurchaseOrderLineId).ToArray();
            var eligibleIds = await AssignedLines(db, scope, assigned).Where(x => sourceLineIds.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(token);
            var permitted = new List<GoodsReceiptLine>();
            foreach (var line in lines.Where(x => eligibleIds.Contains(x.PurchaseOrderLineId) && x.PhysicalObservationId != null && x.PhysicalPolicyVersionId == assigned.Policy.Id))
            {
                var source = await Source(db, cases, scope, header.PurchaseOrderId, header.IssuedVersionId!.Value, line.PurchaseOrderLineId, token, lockSource: false);
                var final = await Resolve(db, source, line.PhysicalPolicyVersionId, token, persistState: false); var ledger = await Ledger(db, final, token);
                var actor = await Actor(core, scope, http, auth, ControlledReceiving.InspectPermission, source.Order.BranchRef, header.SiteRef, token);
                ControlledReceiving.VerifyCurrentActor(final.Source, final.Rules, actor, ledger, ControlledReceiving.InspectPermission, Now(), header.SiteRef, line.PhysicalObservationId);
                permitted.Add(line);
            }
            if (permitted.Count == 0) throw new ReceivingProblem("ReceivingAssignedTaskNotFound", 404);
            return await ReceiptRead(db, cases, core, auth, scope, http, header, permitted.ToArray(), token, purposeOnly: true);
        }, ct);
    private static Task<IResult> ServiceTask(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var assigned = await AssignedPolicy(db, core, scope, http, auth, true, token);
            var row = await db.ServiceAcceptances.SingleOrDefaultAsync(x => x.Id == id && x.ExecutionVersion == 1, token)
                ?? throw new ReceivingProblem("ReceivingAssignedTaskNotFound", 404);
            if (row.SourcePurchaseOrderId is null || !await AssignedLines(db, scope, assigned).AnyAsync(x => x.Id == row.PurchaseOrderLineId, token))
                throw new ReceivingProblem("ReceivingAssignedTaskNotFound", 404);
            var source = await Source(db, cases, scope, row.SourcePurchaseOrderId.Value, row.IssuedBindingId, row.PurchaseOrderLineId, token, lockSource: false);
            var final = await Resolve(db, source, row.PolicyVersionId, token, persistState: false);
            var actor = await Actor(core, scope, http, auth, ControlledReceiving.CertifyPermission, source.Order.BranchRef, source.Order.BranchRef, token);
            // Membership/site/current identity defines task visibility; maker/checker/capacity
            // may block Certify and is represented by the separate action hint, not hidden.
            if (!final.Rules.ServiceOwnerWorkerIds.Contains(actor.WorkerId) || !actor.SiteIds.Contains(source.Order.BranchRef!.Value)
                || !final.Rules.ReceivingSiteIds.Contains(source.Order.BranchRef.Value)) throw new ReceivingProblem("ReceivingAssignedTaskNotFound", 404);
            return await ServiceRead(db, cases, core, auth, scope, http, row, token, purposeOnly: true);
        }, ct);
}
