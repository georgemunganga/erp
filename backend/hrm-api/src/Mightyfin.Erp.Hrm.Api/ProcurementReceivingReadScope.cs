using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    // Database-side retained scope/lineage fence, BEFORE caller policy/branch filters,
    // counts and paging. The detailed Source verifier still checks the full intent/hash.
    // Core columns use their existing Core-owned quoted schema contract; no Core writes.
    private static IQueryable<PurchaseOrder> BoundIssuedOrders(ProcurementDbContext db, ProcurementRequestScope scope)
        => db.PurchaseOrders.FromSqlInterpolated($"""
            SELECT po.* FROM procurement.purchase_orders po
            JOIN procurement.purchase_order_issued_versions issued ON issued.purchase_order_id=po.id AND issued.tenant_id=po.tenant_id
                AND issued.legal_entity_id=po.legal_entity_id AND issued.revision_number=po.current_revision
            JOIN procurement.purchase_order_submissions submitted ON submitted.id=issued.submission_id AND submitted.tenant_id=issued.tenant_id
                AND submitted.legal_entity_id=issued.legal_entity_id AND submitted.purchase_order_id=po.id
            JOIN core_approval.cases workflow ON workflow."Id"=submitted.core_approval_case_id AND workflow."TenantId"=po.tenant_id
                AND workflow."LegalEntityId"=po.legal_entity_id
            WHERE po.tenant_id={scope.TenantId} AND po.legal_entity_id={scope.LegalEntityId} AND po.status='Issued'
                AND issued.commercial_snapshot_json::jsonb=submitted.commercial_snapshot_json::jsonb
                AND issued.commercial_snapshot_sha256=submitted.commercial_snapshot_sha256
                AND issued.commercial_snapshot_json::jsonb->>'TenantId'=po.tenant_id
                AND issued.commercial_snapshot_json::jsonb->>'LegalEntityId'=po.legal_entity_id::text
                AND issued.commercial_snapshot_json::jsonb->>'Id'=po.id::text
                AND issued.commercial_snapshot_json::jsonb->>'revision'=po.current_revision::text
                AND (issued.commercial_snapshot_json::jsonb ? 'BranchRef')
                AND (issued.commercial_snapshot_json::jsonb->>'BranchRef') IS NOT DISTINCT FROM po.branch_ref::text
                AND issued.commercial_snapshot_json::jsonb->>'CurrencyCode'=po.currency_code
                AND issued.commercial_snapshot_json::jsonb->>'Number'=po.number
                AND issued.commercial_snapshot_json::jsonb->>'SupplierId'=po.supplier_id::text
                AND issued.commercial_snapshot_json::jsonb->>'BuyerSubjectId'=po.buyer_subject_id
                AND po.submitted_business_version=submitted.commercial_revision AND po.current_revision=submitted.commercial_revision
                AND po.submitted_at=submitted.submitted_at AND po.approval_instance_ref=submitted.core_approval_case_id::text
                AND submitted.buyer_principal_id=po.buyer_subject_id AND submitted.currency_code=po.currency_code
                AND workflow."SourceModule"='procurement' AND workflow."SourceType"='purchase_order'
                AND workflow."SourceId"=po.id AND workflow."SourceVersion"=submitted.commercial_revision
                AND workflow."State"='Approved' AND workflow."MakerSubjectId"=submitted.buyer_principal_id
                AND workflow."PolicyVersionId"=submitted.policy_version_id AND workflow."PolicyVersionNumber"=submitted.policy_version_number
                AND workflow."CompletedAt" IS NOT NULL AND workflow."CompletedAt"<=issued.authorized_at
            """);
}
