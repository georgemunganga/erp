using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Retained company/branch/approved-source boundary BEFORE count/page.
/// This is candidate discovery, not full current eligibility or a new reservation.</summary>
internal static class ProcurementSourcingDiscoveryQueries
{
    internal sealed record Page(int Number, int Size, string Search)
    { internal int Offset => (Number - 1) * Size; internal string Pattern => "%" + Search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"; }
    internal static Page Paging(int? page, int? pageSize, string? q)
    {
        var number = page ?? 1; var size = pageSize ?? 25; var search = q?.Trim() ?? "";
        if (number is < 1 or > 100000 || size is < 1 or > 25 || search.Length > 100 || search.Any(char.IsControl))
            throw new ArgumentException("SourcingDiscoveryQueryInvalid");
        return new(number, size, search);
    }
    internal static IQueryable<PurchaseRequest> ScopedRequests(IQueryable<PurchaseRequest> query, RfqCurrentActor actor)
    {
        if (actor.SelectedBranchId is { } selected) query = query.Where(x => x.BranchRef == selected);
        if (!actor.OrganisationWide)
        {
            var branches = actor.Branches.ToArray();
            query = query.Where(x => x.BranchRef != null && branches.Contains(x.BranchRef.Value));
        }
        return query;
    }
    internal static IQueryable<SourcingEvent> ScopedDrafts(IQueryable<SourcingEvent> query, RfqCurrentActor actor)
    {
        if (actor.SelectedBranchId is { } selected) query = query.Where(x => x.BranchRef == selected);
        if (!actor.OrganisationWide)
        {
            var branches = actor.Branches.ToArray();
            query = query.Where(x => x.BranchRef != null && branches.Contains(x.BranchRef.Value));
        }
        return query;
    }
    internal static IQueryable<PurchaseRequestLine> Candidates(ProcurementDbContext db, ProcurementRequestScope scope)
        => db.PurchaseRequestLines.FromSqlInterpolated($"""
            SELECT line.* FROM procurement.purchase_request_lines line
            JOIN procurement.purchase_requests request ON request.id=line.purchase_request_id
                AND request.tenant_id=line.tenant_id AND request.legal_entity_id=line.legal_entity_id
            WHERE line.tenant_id={scope.TenantId} AND line.legal_entity_id={scope.LegalEntityId}
                AND request.status IN ('Approved','PartiallyOrdered','Ordered') AND request.approval_state='Approved'
                AND line.approval_status='Approved' AND line.approved_quantity>0 AND line.route IN ('Unassigned','PendingSourcing')
                AND EXISTS (
                    SELECT 1 FROM procurement.purchase_request_approval_submissions pin
                    JOIN procurement.purchase_request_approval_outcomes outcome ON outcome.submission_id=pin.id
                        AND outcome.tenant_id=pin.tenant_id AND outcome.legal_entity_id=pin.legal_entity_id
                        AND outcome.purchase_request_id=pin.purchase_request_id
                        AND outcome.submitted_business_version=pin.submitted_business_version
                        AND outcome.core_approval_case_id=pin.core_approval_case_id AND outcome.outcome='Approved'
                    JOIN core_approval.cases workflow ON workflow."Id"=pin.core_approval_case_id
                        AND workflow."TenantId"=pin.tenant_id AND workflow."LegalEntityId"=pin.legal_entity_id
                    JOIN core_approval.decisions decision ON decision."Id"=outcome.core_decision_id
                        AND decision."TenantId"=pin.tenant_id AND decision."LegalEntityId"=pin.legal_entity_id
                        AND decision."CaseId"=workflow."Id" AND decision."Action"='Approve'
                    JOIN procurement.policy_evaluations evaluation ON evaluation.id=pin.policy_evaluation_id
                        AND evaluation.tenant_id=pin.tenant_id AND evaluation.legal_entity_id=pin.legal_entity_id
                    WHERE pin.tenant_id=request.tenant_id AND pin.legal_entity_id=request.legal_entity_id
                        AND pin.purchase_request_id=request.id AND request.approval_instance_ref=pin.core_approval_case_id::text
                        AND request.submitted_version=pin.submitted_business_version
                        AND pin.request_snapshot_json::jsonb->>'TenantId'=request.tenant_id
                        AND pin.request_snapshot_json::jsonb->>'LegalEntityId'=request.legal_entity_id::text
                        AND pin.request_snapshot_json::jsonb->>'Id'=request.id::text
                        AND pin.request_snapshot_json::jsonb->>'Number'=request.number
                        AND pin.request_snapshot_json::jsonb->>'CurrencyCode'=request.currency_code
                        AND (pin.request_snapshot_json::jsonb ? 'BranchRef')
                        AND (pin.request_snapshot_json::jsonb->>'BranchRef') IS NOT DISTINCT FROM request.branch_ref::text
                        AND pin.maker_subject_id=request.created_by AND workflow."MakerSubjectId"=pin.maker_subject_id
                        AND workflow."SourceModule"='procurement' AND workflow."SourceType"='purchase_request'
                        AND workflow."SourceId"=request.id AND workflow."SourceVersion"=pin.submitted_business_version
                        AND workflow."PolicyVersionId"=pin.policy_version_id AND workflow."PolicyVersionNumber"=pin.policy_version_number
                        AND workflow."State"='Approved' AND workflow."CompletedAt" IS NOT NULL
                        AND workflow."CompletionKey"=decision."IdempotencyKey" AND outcome.decision_command_key=decision."IdempotencyKey"
                        AND EXISTS (SELECT 1 FROM jsonb_array_elements(
                            CASE WHEN jsonb_typeof(pin.request_snapshot_json::jsonb->'lines')='array'
                                THEN pin.request_snapshot_json::jsonb->'lines' ELSE '[]'::jsonb END) original
                            WHERE original->>'Id'=line.id::text AND original->>'PurchaseRequestId'=request.id::text
                                AND original->>'Description'=line.description AND original->>'UnitCode'=line.unit_code)
                        AND EXISTS (SELECT 1 FROM jsonb_array_elements(
                            CASE WHEN jsonb_typeof(evaluation.decision_json->'lines')='array'
                                THEN evaluation.decision_json->'lines' ELSE '[]'::jsonb END) result
                            JOIN procurement.procurement_methods method ON method.code=result->'decision'->>'sourcingMethodCode'
                                AND method.tenant_id=request.tenant_id AND method.legal_entity_id=request.legal_entity_id
                                AND method.status='Active' AND method.execution_route='CompetitiveSourcing'
                            WHERE result->>'lineId'=line.id::text AND result->'decision'->>'status'='Match')
                )
            """).AsNoTracking();
}
