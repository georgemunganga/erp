using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static Task<IResult> GovernedRead(HttpContext http,ProcurementRequestScope scope,
        Func<ProcurementDbContext,Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext,HrmDbContext,
            IAuthorizationService,CancellationToken,Task<Reply>> operation,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,operation,ct);
    }

    private static async Task<Guid[]> CurrentTaskBranches(HrmDbContext core,ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var assigned=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,ct);
        var candidates=assigned.Length>0?assigned:
            await core.WorkLocations.AsNoTracking().Where(x=>x.LegalEntityId==scope.LegalEntityId&&!x.IsArchived)
                .Select(x=>x.Id).ToArrayAsync(ct);
        if(scope.WorkLocationId is { } selected)
            candidates=candidates.Contains(selected)?[selected]:[];
        if(candidates.Length==0 || scope.IsConfined && scope.WorkLocationId is null
            && !scope.IsExplicitAllBranches)throw new Problem("OrderActionDenied",403);
        var active=await core.WorkLocations.AsNoTracking().Where(x=>x.LegalEntityId==scope.LegalEntityId
            &&!x.IsArchived&&candidates.Contains(x.Id)).Select(x=>x.Id).ToArrayAsync(ct);
        if(active.Length==0)throw new Problem("OrderActionDenied",403);
        return active;
    }

    // Purpose-only Finance read. In particular, the Finance actor need not
    // hold buyer-wide PO/PR read, and a list row is not sufficient evidence for
    // a manual external commitment or release.
    private static Task<IResult> FinanceDetail(Guid id,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var load=await LoadGoverned(db,cases,core,scope,http,auth,
                "procurement-po-finance-confirm",id,token,lockSources:false);
            var order=load.Order;
            if(order.Status!="Draft" && order.Status!="ReleasePending")
                throw new Problem("OrderFinanceTaskNotFound",404);
            var reservations=load.Sources.Select(x=>x.Source.Finance.ReservationRef)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if(reservations.Length==0 || reservations.Any(string.IsNullOrWhiteSpace))
                throw new Problem("OrderOriginalSourceUnavailable",409);
            var funding=await db.PrincipalOrderFundingEvidence.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id&&x.CommercialRevision==1,token);
            var release=await db.PrincipalOrderReleaseRequests.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
            if(order.Status=="Draft" && (funding is not null || release is not null)
                || order.Status=="ReleasePending" && (funding is null || release is null)
                || release is not null && (release.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                    || release.FundingEvidenceId!=funding?.Id || release.RequestedByPrincipalId!=order.BuyerSubjectId)
                || funding is not null && (funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                    || funding.CurrencyCode!=order.CurrencyCode
                    || funding.AttestedGrossAmount!=PurchaseOrderCommercialWorkflow.Totals(load.Lines).Gross
                    || funding.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(funding.CommercialSnapshotJson)
                    || !PrincipalOrderCommercialSnapshot.RetainedMatches(order,load.Lines,
                        funding.CommercialSnapshotJson,1)))
                throw new Problem("OrderFinanceEvidenceChanged",409);
            if(funding is not null)
            {
                var retained=JsonSerializer.Deserialize<string[]>(funding.SourceReservationsJson)??[];
                if(retained.Length!=reservations.Length || !retained.SequenceEqual(reservations))
                    throw new Problem("OrderFinanceEvidenceChanged",409);
            }
            var totals=PurchaseOrderCommercialWorkflow.Totals(load.Lines);
            return new Reply(JsonSerializer.Serialize(new{order.Id,order.Number,order.Status,order.Version,
                order.SupplierId,order.BranchRef,order.CurrencyCode,order.OriginalDirectSourceSha256,
                grossAmountExact=ProcurementEvidenceEncoding.Decimal(totals.Gross),
                sourceReservationReferences=reservations,
                funding=funding is null?null:new{funding.Id,funding.ManualCommitmentReference,
                    funding.ExternalEvidenceReference,funding.ApprovedManualPolicyReference,
                    funding.ObservedAt,attestedGrossAmountExact=ProcurementEvidenceEncoding.Decimal(funding.AttestedGrossAmount)},
                release=release is null?null:new{release.Id,release.Reason,release.RequestedAt,
                    release.RequestedByPrincipalId,release.OriginalDirectSourceSha256},
                canRecordFunding=order.Status=="Draft",canConfirmRelease=order.Status=="ReleasePending",
                previewOnly=true,financeLedgerChanged=false},Json));
        },ct);
    }

    // The Core assignment, its retained P30 pin, current company/branch and
    // pending order are joined BEFORE count/paging. This is not a broad PO list.
    private static Task<IResult> AssignedReviews(int? page,int? pageSize,string? q,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
            var actor=await Actor(core,scope,http,auth,"procurement-po-approve",null,false,token);
            var active=await CurrentTaskBranches(core,scope,token);
            var principal=actor.PrincipalId;
            var hasSearch=paging.Search.Length>0;
            var pattern=paging.Pattern;
            var query=cases.Assignments.FromSqlInterpolated($"""
                SELECT assignment.* FROM core_approval.assignments assignment
                JOIN core_approval.stages stage ON stage."Id"=assignment."StageId"
                    AND stage."TenantId"=assignment."TenantId"
                    AND stage."LegalEntityId"=assignment."LegalEntityId"
                JOIN core_approval.cases workflow ON workflow."Id"=assignment."CaseId"
                    AND workflow."TenantId"=assignment."TenantId"
                    AND workflow."LegalEntityId"=assignment."LegalEntityId"
                JOIN procurement.principal_order_assignments pin ON pin.core_assignment_id=assignment."Id"
                    AND pin.core_stage_id=stage."Id" AND pin.core_approval_case_id=workflow."Id"
                    AND pin.tenant_id=assignment."TenantId"
                    AND pin.legal_entity_id=assignment."LegalEntityId"
                JOIN procurement.principal_order_submissions submission ON submission.id=pin.submission_id
                    AND submission.core_approval_case_id=workflow."Id"
                    AND submission.tenant_id=pin.tenant_id
                    AND submission.legal_entity_id=pin.legal_entity_id
                JOIN procurement.principal_order_funding_evidence funding ON funding.id=submission.funding_evidence_id
                    AND funding.purchase_order_id=submission.purchase_order_id
                    AND funding.tenant_id=submission.tenant_id
                    AND funding.legal_entity_id=submission.legal_entity_id
                    AND funding.commercial_snapshot_sha256=submission.commercial_snapshot_sha256
                    AND funding.original_direct_source_sha256=submission.original_direct_source_sha256
                    AND funding.attested_gross_amount=submission.gross_amount
                JOIN procurement.purchase_orders po ON po.id=submission.purchase_order_id
                    AND po.tenant_id=submission.tenant_id
                    AND po.legal_entity_id=submission.legal_entity_id
                WHERE assignment."TenantId"={scope.TenantId}
                    AND assignment."LegalEntityId"={scope.LegalEntityId}
                    AND assignment."AssigneeSubjectId"={principal}
                    AND assignment."PermissionKey"='procurement-po-approve'
                    AND assignment."DelegatedFromSubjectId" IS NULL
                    AND assignment."State"='Pending' AND stage."State"='Active'
                    AND workflow."State"='Pending'
                    AND workflow."SourceModule"='procurement'
                    AND workflow."SourceType"='purchase_order'
                    AND workflow."SourceId"=po.id AND workflow."SourceVersion"=1
                    AND workflow."PolicyVersionId"=submission.policy_version_id
                    AND workflow."PolicyVersionNumber"=submission.policy_version_number
                    AND workflow."MakerSubjectId"=po.buyer_subject_id
                    AND pin.principal_id={principal}
                    AND po.principal_draft_schema_version=2
                    AND po.status='PendingApproval' AND po.current_revision=1
                    AND po.original_direct_source_sha256=submission.original_direct_source_sha256
                    AND po.branch_ref = ANY({active})
                    AND ({hasSearch}=false OR po.number ILIKE {pattern} ESCAPE '\')
                """).AsNoTracking();
            var total=await query.CountAsync(token);
            var assignments=await query.OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var ids=assignments.Select(x=>x.Id).ToArray();
            var pins=await db.PrincipalOrderAssignments.AsNoTracking().Where(x=>ids.Contains(x.CoreAssignmentId))
                .ToArrayAsync(token);
            var submissionIds=pins.Select(x=>x.SubmissionId).ToArray();
            var submissions=await db.PrincipalOrderSubmissions.AsNoTracking().Where(x=>submissionIds.Contains(x.Id))
                .ToArrayAsync(token);
            var orderIdsPage=submissions.Select(x=>x.PurchaseOrderId).ToArray();
            var orders=await db.PurchaseOrders.AsNoTracking().Where(x=>orderIdsPage.Contains(x.Id))
                .ToArrayAsync(token);
            var lines=await db.PurchaseOrderLines.AsNoTracking().Where(x=>orderIdsPage.Contains(x.PurchaseOrderId)
                &&x.IsActive).ToArrayAsync(token);
            var bindings=await db.PrincipalOrderSourceBindings.AsNoTracking()
                .Where(x=>orderIdsPage.Contains(x.PurchaseOrderId)).ToArrayAsync(token);
            var fundingIds=submissions.Select(x=>x.FundingEvidenceId).ToArray();
            var fundings=await db.PrincipalOrderFundingEvidence.AsNoTracking()
                .Where(x=>fundingIds.Contains(x.Id)).ToArrayAsync(token);
            var caseIdsPage=assignments.Select(x=>x.CaseId).ToArray();
            var workflows=await cases.Cases.AsNoTracking().Where(x=>caseIdsPage.Contains(x.Id)).ToArrayAsync(token);
            var items=new List<object>(assignments.Length);
            foreach(var task in assignments)
            {
                var pin=pins.SingleOrDefault(x=>x.CoreAssignmentId==task.Id&&x.CoreApprovalCaseId==task.CaseId
                    &&x.PrincipalId==principal);
                var submission=pin is null?null:submissions.SingleOrDefault(x=>x.Id==pin.SubmissionId);
                var order=submission is null?null:orders.SingleOrDefault(x=>x.Id==submission.PurchaseOrderId);
                var funding=submission is null?null:fundings.SingleOrDefault(x=>x.Id==submission.FundingEvidenceId);
                var workflow=workflows.SingleOrDefault(x=>x.Id==task.CaseId);
                if(pin is null || submission is null || order is null || funding is null || workflow is null
                    || order.BranchRef is null || !active.Contains(order.BranchRef.Value)
                    || order.Status!="PendingApproval" || workflow.State!="Pending"
                    || submission.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(submission.CommercialSnapshotJson)
                    || funding.CommercialSnapshotJson!=submission.CommercialSnapshotJson
                    || funding.CommercialSnapshotSha256!=submission.CommercialSnapshotSha256
                    || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                    || funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                    || workflow.SourceId!=order.Id || workflow.PolicyVersionId!=submission.PolicyVersionId)
                    throw new Problem("OrderApprovalEvidenceChanged",409);
                PrincipalOrderDraftPins.Verify(order,lines.Where(x=>x.PurchaseOrderId==order.Id).ToArray(),
                    bindings.Where(x=>x.PurchaseOrderId==order.Id).ToArray());
                items.Add(new{orderId=order.Id,order.Number,order.Status,order.Version,order.BranchRef,
                    order.SupplierId,order.CurrencyCode,assignmentId=task.Id,assignmentVersion=task.Version,
                    caseId=workflow.Id,caseVersion=workflow.Version,workflow.OpenedAt,
                    grossAmountExact=ProcurementEvidenceEncoding.Decimal(submission.GrossAmount)});
            }
            return new Reply(JsonSerializer.Serialize(new{items,total,page=paging.Number,pageSize=paging.Size,
                assignedToCurrentPrincipal=true,financePostingCreated=false,supplierSendPerformed=false},Json));
        },ct);
    }
}
