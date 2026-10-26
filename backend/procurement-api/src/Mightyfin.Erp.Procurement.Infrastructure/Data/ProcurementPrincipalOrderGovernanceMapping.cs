using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalOrderFundingEvidence> PrincipalOrderFundingEvidence => Set<PrincipalOrderFundingEvidence>();
    public DbSet<PrincipalOrderSubmission> PrincipalOrderSubmissions => Set<PrincipalOrderSubmission>();
    public DbSet<PrincipalOrderAssignmentEvidence> PrincipalOrderAssignments => Set<PrincipalOrderAssignmentEvidence>();
    public DbSet<PrincipalOrderDecisionEvidence> PrincipalOrderDecisions => Set<PrincipalOrderDecisionEvidence>();
    public DbSet<PrincipalOrderIssuedVersion> PrincipalOrderIssuedVersions => Set<PrincipalOrderIssuedVersion>();
    public DbSet<PrincipalOrderAcknowledgment> PrincipalOrderAcknowledgments => Set<PrincipalOrderAcknowledgment>();
    public DbSet<PrincipalOrderReleaseRequest> PrincipalOrderReleaseRequests => Set<PrincipalOrderReleaseRequest>();
    public DbSet<PrincipalOrderReleaseConfirmation> PrincipalOrderReleaseConfirmations => Set<PrincipalOrderReleaseConfirmation>();

    private void ConfigurePrincipalOrderGovernance(ModelBuilder model)
    {
        ConfigureBase<PrincipalOrderFundingEvidence>(model,"principal_order_funding_evidence");
        ConfigureBase<PrincipalOrderSubmission>(model,"principal_order_submissions");
        ConfigureBase<PrincipalOrderAssignmentEvidence>(model,"principal_order_assignments");
        ConfigureBase<PrincipalOrderDecisionEvidence>(model,"principal_order_decisions");
        ConfigureBase<PrincipalOrderIssuedVersion>(model,"principal_order_issued_versions");
        ConfigureBase<PrincipalOrderAcknowledgment>(model,"principal_order_acknowledgments");
        ConfigureBase<PrincipalOrderReleaseRequest>(model,"principal_order_release_requests");
        ConfigureBase<PrincipalOrderReleaseConfirmation>(model,"principal_order_release_confirmations");
        ConfigurePrincipalOrderRelease(model);
        // Each immutable operation is bound to its original command as well as
        // the shared receipt. This prevents a self-rehashed acknowledgement from
        // borrowing another operation's business artifact.
        foreach(var type in new[]{typeof(PrincipalOrderFundingEvidence),typeof(PrincipalOrderSubmission),
            typeof(PrincipalOrderDecisionEvidence),typeof(PrincipalOrderIssuedVersion),
            typeof(PrincipalOrderAcknowledgment),typeof(PrincipalOrderReleaseRequest),
            typeof(PrincipalOrderReleaseConfirmation)})
        {
            var entity=model.Entity(type);
            entity.HasIndex("TenantId","LegalEntityId","CommandKey").IsUnique();
            var table=entity.Metadata.GetTableName()!;
            entity.ToTable(t=>t.HasCheckConstraint("ck_"+table+"_command",
                "command_key <> '00000000-0000-0000-0000-000000000000'::uuid"));
        }
        foreach(var type in new[]{typeof(PrincipalOrderFundingEvidence),typeof(PrincipalOrderIssuedVersion),
            typeof(PrincipalOrderAcknowledgment),typeof(PrincipalOrderReleaseConfirmation)})
        {
            var entity=model.Entity(type);var table=entity.Metadata.GetTableName()!;
            entity.ToTable(t=>t.HasCheckConstraint("ck_"+table+"_result_version","resulting_order_version>0"));
        }
        var funding=model.Entity<PrincipalOrderFundingEvidence>();
        funding.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        funding.Property(x=>x.CommercialSnapshotSha256).HasMaxLength(64);
        funding.Property(x=>x.CommercialSnapshotJson).HasColumnType("text");
        funding.Property(x=>x.SourceReservationsJson).HasColumnType("text");
        funding.Property(x=>x.ManualCommitmentReference).HasMaxLength(200);
        funding.Property(x=>x.ExternalEvidenceReference).HasMaxLength(500);
        funding.Property(x=>x.ApprovedManualPolicyReference).HasMaxLength(500);
        funding.Property(x=>x.CurrencyCode).HasMaxLength(3);
        funding.Property(x=>x.RecordedByPrincipalId).HasMaxLength(36);
        funding.Property(x=>x.AttestedGrossAmount).HasPrecision(38,8);
        funding.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.CommercialRevision}).IsUnique();
        Link<PrincipalOrderFundingEvidence,PurchaseOrder>(model,nameof(Mightyfin.Erp.Procurement.Domain.PrincipalOrderFundingEvidence.PurchaseOrderId));
        funding.ToTable(t=>t.HasCheckConstraint("ck_principal_order_funding",
            "schema_version=2 AND commercial_revision=1 AND attested_gross_amount>=0 AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var submission=model.Entity<PrincipalOrderSubmission>();
        submission.Property(x=>x.BuyerPrincipalId).HasMaxLength(36);
        submission.Property(x=>x.CurrencyCode).HasMaxLength(3);
        submission.Property(x=>x.PolicyRulesSha256).HasMaxLength(64);
        submission.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        submission.Property(x=>x.CommercialSnapshotSha256).HasMaxLength(64);
        submission.Property(x=>x.CommercialSnapshotJson).HasColumnType("text");
        submission.Property(x=>x.PolicyDecisionJson).HasColumnType("text");
        submission.Property(x=>x.LineBaseAmount).HasPrecision(38,8);
        submission.Property(x=>x.GrossAmount).HasPrecision(38,8);
        submission.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.CommercialRevision}).IsUnique();
        submission.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CoreApprovalCaseId}).IsUnique();
        Link<PrincipalOrderSubmission,PurchaseOrder>(model,nameof(PrincipalOrderSubmission.PurchaseOrderId));
        Link<PrincipalOrderSubmission,PrincipalOrderFundingEvidence>(model,nameof(PrincipalOrderSubmission.FundingEvidenceId));
        Link<PrincipalOrderSubmission,PolicyVersion>(model,nameof(PrincipalOrderSubmission.PolicyVersionId));
        submission.ToTable(t=>t.HasCheckConstraint("ck_principal_order_submission",
            "schema_version=2 AND commercial_revision=1 AND submitted_row_version>1 AND policy_version_number>0 AND line_base_amount>0 AND gross_amount>=line_base_amount AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND buyer_principal_id IS NOT NULL AND buyer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var assignment=model.Entity<PrincipalOrderAssignmentEvidence>();
        assignment.Property(x=>x.PrincipalId).HasMaxLength(36);
        assignment.Property(x=>x.AuthorityEvidenceSha256).HasMaxLength(64);
        assignment.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CoreAssignmentId}).IsUnique();
        Link<PrincipalOrderAssignmentEvidence,PrincipalOrderSubmission>(model,nameof(PrincipalOrderAssignmentEvidence.SubmissionId));
        Link<PrincipalOrderAssignmentEvidence,ProcurementSpendingAuthorityGrant>(model,nameof(PrincipalOrderAssignmentEvidence.GrantId));
        assignment.ToTable(t=>t.HasCheckConstraint("ck_principal_order_assignment",
            "schema_version=2 AND grant_revision>0 AND authority_version>0 AND authority_evidence_sha256 ~ '^[0-9a-f]{64}$' AND principal_id IS NOT NULL AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_id <> '00000000-0000-0000-0000-000000000000' AND (worker_alias_id IS NULL OR worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var decision=model.Entity<PrincipalOrderDecisionEvidence>();
        decision.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        decision.Property(x=>x.Action).HasMaxLength(20);
        decision.Property(x=>x.Reason).HasMaxLength(2000);
        decision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CoreDecisionId}).IsUnique();
        Link<PrincipalOrderDecisionEvidence,PrincipalOrderSubmission>(model,nameof(PrincipalOrderDecisionEvidence.SubmissionId));
        decision.ToTable(t=>t.HasCheckConstraint("ck_principal_order_decision",
            "schema_version=2 AND action IN ('Approve','Return','Reject') AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (action='Approve' OR (reason IS NOT NULL AND length(btrim(reason))>0)) AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var issued=model.Entity<PrincipalOrderIssuedVersion>();
        issued.Property(x=>x.CommercialSnapshotJson).HasColumnType("text");
        issued.Property(x=>x.CommercialSnapshotSha256).HasMaxLength(64);
        issued.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        issued.Property(x=>x.ManualCommitmentReference).HasMaxLength(200);
        issued.Property(x=>x.AuthorizedByPrincipalId).HasMaxLength(36);
        issued.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.CommercialRevision}).IsUnique();
        Link<PrincipalOrderIssuedVersion,PurchaseOrder>(model,nameof(PrincipalOrderIssuedVersion.PurchaseOrderId));
        Link<PrincipalOrderIssuedVersion,PrincipalOrderSubmission>(model,nameof(PrincipalOrderIssuedVersion.SubmissionId));
        Link<PrincipalOrderIssuedVersion,PrincipalOrderFundingEvidence>(model,nameof(PrincipalOrderIssuedVersion.FundingEvidenceId));
        issued.ToTable(t=>t.HasCheckConstraint("ck_principal_order_issued",
            "schema_version=2 AND commercial_revision=1 AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND length(btrim(manual_commitment_reference))>0 AND authorized_by_principal_id IS NOT NULL AND authorized_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND authorized_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (authorized_by_worker_alias_id IS NULL OR authorized_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var acknowledgment=model.Entity<PrincipalOrderAcknowledgment>();
        acknowledgment.Property(x=>x.ApprovedManualDispatchPolicyReference).HasMaxLength(500);
        acknowledgment.Property(x=>x.Outcome).HasMaxLength(20);
        acknowledgment.Property(x=>x.Channel).HasMaxLength(80);
        acknowledgment.Property(x=>x.SupplierReference).HasMaxLength(200);
        acknowledgment.Property(x=>x.EvidenceReference).HasMaxLength(500);
        acknowledgment.Property(x=>x.RecordedByPrincipalId).HasMaxLength(36);
        acknowledgment.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.IssuedVersionId}).IsUnique();
        Link<PrincipalOrderAcknowledgment,PrincipalOrderIssuedVersion>(model,nameof(PrincipalOrderAcknowledgment.IssuedVersionId));
        Link<PrincipalOrderAcknowledgment,PurchaseOrder>(model,nameof(PrincipalOrderAcknowledgment.PurchaseOrderId));
        acknowledgment.ToTable(t=>t.HasCheckConstraint("ck_principal_order_acknowledgment",
            "schema_version=2 AND outcome IN ('Accepted','Rejected') AND length(btrim(approved_manual_dispatch_policy_reference))>0 AND observed_at<=recorded_at AND length(btrim(channel))>0 AND length(btrim(supplier_reference))>0 AND length(btrim(evidence_reference))>0 AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
    }

    private void ConfigurePrincipalOrderRelease(ModelBuilder model)
    {
        var request=model.Entity<PrincipalOrderReleaseRequest>();
        request.Property(x=>x.OriginalStatus).HasMaxLength(20);
        request.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        request.Property(x=>x.Reason).HasMaxLength(2000);
        request.Property(x=>x.RequestedByPrincipalId).HasMaxLength(36);
        request.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId}).IsUnique();
        Link<PrincipalOrderReleaseRequest,PurchaseOrder>(model,nameof(PrincipalOrderReleaseRequest.PurchaseOrderId));
        Link<PrincipalOrderReleaseRequest,PrincipalOrderFundingEvidence>(model,nameof(PrincipalOrderReleaseRequest.FundingEvidenceId));
        request.ToTable(t=>t.HasCheckConstraint("ck_principal_order_release_request",
            "schema_version=2 AND ((original_status='Draft' AND commercial_revision=0) OR (original_status IN ('PendingApproval','Returned','Rejected','Approved') AND commercial_revision=1)) AND ((original_status='PendingApproval' AND cancelled_core_case_id IS NOT NULL AND cancelled_core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid AND core_cancellation_decision_id IS NOT NULL AND core_cancellation_decision_id<>'00000000-0000-0000-0000-000000000000'::uuid) OR (original_status<>'PendingApproval' AND cancelled_core_case_id IS NULL AND core_cancellation_decision_id IS NULL)) AND requested_order_version>0 AND length(btrim(reason))>0 AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND requested_by_principal_id IS NOT NULL AND requested_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND requested_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (requested_by_worker_alias_id IS NULL OR requested_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
        var confirmation=model.Entity<PrincipalOrderReleaseConfirmation>();
        confirmation.Property(x=>x.ManualCommitmentReference).HasMaxLength(200);
        confirmation.Property(x=>x.ExternalReleaseEvidenceReference).HasMaxLength(500);
        confirmation.Property(x=>x.ApprovedManualPolicyReference).HasMaxLength(500);
        confirmation.Property(x=>x.RestoredSourceReservationsJson).HasColumnType("text");
        confirmation.Property(x=>x.ReleasedGrossAmount).HasPrecision(38,8);
        confirmation.Property(x=>x.CurrencyCode).HasMaxLength(3);
        confirmation.Property(x=>x.ConfirmedByPrincipalId).HasMaxLength(36);
        confirmation.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.ReleaseRequestId}).IsUnique();
        Link<PrincipalOrderReleaseConfirmation,PurchaseOrder>(model,nameof(PrincipalOrderReleaseConfirmation.PurchaseOrderId));
        Link<PrincipalOrderReleaseConfirmation,PrincipalOrderReleaseRequest>(model,nameof(PrincipalOrderReleaseConfirmation.ReleaseRequestId));
        Link<PrincipalOrderReleaseConfirmation,PrincipalOrderFundingEvidence>(model,nameof(PrincipalOrderReleaseConfirmation.FundingEvidenceId));
        confirmation.ToTable(t=>t.HasCheckConstraint("ck_principal_order_release_confirmation",
            "schema_version=2 AND released_gross_amount>=0 AND currency_code ~ '^[A-Z]{3}$' AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_release_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND confirmed_by_principal_id IS NOT NULL AND confirmed_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND confirmed_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (confirmed_by_worker_alias_id IS NULL OR confirmed_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
    }

    private void GuardPrincipalOrderGovernance()
    {
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is
            Mightyfin.Erp.Procurement.Domain.PrincipalOrderFundingEvidence or PrincipalOrderSubmission or PrincipalOrderAssignmentEvidence
            or PrincipalOrderDecisionEvidence or PrincipalOrderIssuedVersion or PrincipalOrderAcknowledgment
            or PrincipalOrderReleaseRequest or PrincipalOrderReleaseConfirmation))
            if(entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("PrincipalOrderGovernanceAppendOnly");
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.State==EntityState.Added
            &&x.Entity is PrincipalOrderFundingEvidence or PrincipalOrderSubmission or PrincipalOrderDecisionEvidence
                or PrincipalOrderIssuedVersion or PrincipalOrderAcknowledgment or PrincipalOrderReleaseRequest
                or PrincipalOrderReleaseConfirmation))
        {
            var (operation,orderId,key,capturedVersion)=entry.Entity switch
            {
                PrincipalOrderFundingEvidence x=>("Funding",x.PurchaseOrderId,x.CommandKey,(long?)x.ResultingOrderVersion),
                PrincipalOrderSubmission x=>("Submit",x.PurchaseOrderId,x.CommandKey,(long?)x.SubmittedRowVersion),
                PrincipalOrderDecisionEvidence x=>("Decide",PrincipalOrderSubmissions.Local.SingleOrDefault(s=>s.Id==x.SubmissionId)?.PurchaseOrderId??Guid.Empty,x.CommandKey,(long?)null),
                PrincipalOrderIssuedVersion x=>("Issue",x.PurchaseOrderId,x.CommandKey,(long?)x.ResultingOrderVersion),
                PrincipalOrderAcknowledgment x=>("Ack",x.PurchaseOrderId,x.CommandKey,(long?)x.ResultingOrderVersion),
                PrincipalOrderReleaseRequest x=>("ReleaseRequest",x.PurchaseOrderId,x.CommandKey,(long?)checked(x.RequestedOrderVersion+1)),
                PrincipalOrderReleaseConfirmation x=>("ReleaseConfirm",x.PurchaseOrderId,x.CommandKey,(long?)x.ResultingOrderVersion),
                _=>throw new InvalidOperationException("PrincipalOrderCommandEvidenceInvalid")
            };
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==orderId);
            var receipt=PrincipalOrderDraftReceipts.Local.SingleOrDefault(x=>x.PurchaseOrderId==orderId
                &&x.Operation==operation&&x.CommandKey==key&&Entry(x).State==EntityState.Added);
            if(order is null||receipt is null||receipt.ActorPrincipalId!=scope.SubjectId||key==Guid.Empty)
                throw new InvalidOperationException("PrincipalOrderCommandReceiptRequired");
            var result=PrincipalOrderGovernanceReceiptEncoding.Verify(receipt,order,operation,key);
            if(result.EvidenceId!=entry.Entity.Id || capturedVersion is {} version&&version!=receipt.ResultVersion)
                throw new InvalidOperationException("PrincipalOrderCommandReceiptBindingInvalid");
        }
        foreach(var funding in ChangeTracker.Entries<PrincipalOrderFundingEvidence>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==funding.PurchaseOrderId);
            if(order is null || order.PrincipalDraftSchemaVersion!=2 || order.Status!="Draft"
                || funding.RecordedByPrincipalId!=scope.SubjectId
                || funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || funding.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(funding.CommercialSnapshotJson))
                throw new InvalidOperationException("PrincipalOrderFundingPinInvalid");
        }
        foreach(var submission in ChangeTracker.Entries<PrincipalOrderSubmission>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==submission.PurchaseOrderId);
            var funding=PrincipalOrderFundingEvidence.Local.SingleOrDefault(x=>x.Id==submission.FundingEvidenceId);
            if(order is null || funding is null || order.Status!="PendingApproval"
                || order.PrincipalDraftSchemaVersion!=2 || order.BuyerSubjectId!=scope.SubjectId
                || submission.BuyerPrincipalId!=scope.SubjectId || submission.CoreApprovalCaseId==Guid.Empty
                || submission.FundingEvidenceId!=funding.Id || submission.GrossAmount!=funding.AttestedGrossAmount
                || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || submission.CommercialSnapshotSha256!=funding.CommercialSnapshotSha256
                || submission.CommercialSnapshotJson!=funding.CommercialSnapshotJson)
                throw new InvalidOperationException("PrincipalOrderSubmissionPinInvalid");
        }
        foreach(var pin in ChangeTracker.Entries<PrincipalOrderAssignmentEvidence>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
            if(!PrincipalOrderSubmissions.Local.Any(x=>x.Id==pin.SubmissionId&&x.CoreApprovalCaseId==pin.CoreApprovalCaseId))
                throw new InvalidOperationException("PrincipalOrderAssignmentPinInvalid");
        foreach(var pin in ChangeTracker.Entries<PrincipalOrderDecisionEvidence>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
            if(pin.ActorPrincipalId!=scope.SubjectId || !PrincipalOrderSubmissions.Local.Any(x=>x.Id==pin.SubmissionId
                &&x.CoreApprovalCaseId==pin.CoreApprovalCaseId)
                || !PrincipalOrderAssignments.Local.Any(x=>x.SubmissionId==pin.SubmissionId
                    &&x.CoreAssignmentId==pin.CoreAssignmentId&&x.PrincipalId==pin.ActorPrincipalId))
                throw new InvalidOperationException("PrincipalOrderDecisionPinInvalid");
        foreach(var issued in ChangeTracker.Entries<PrincipalOrderIssuedVersion>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==issued.PurchaseOrderId);
            if(order is null || order.Status!="IssuePending" || issued.AuthorizedByPrincipalId!=scope.SubjectId
                || issued.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || !PrincipalOrderSubmissions.Local.Any(x=>x.Id==issued.SubmissionId&&x.PurchaseOrderId==order.Id
                    &&x.CoreApprovalCaseId==issued.CoreApprovalCaseId&&x.CommercialSnapshotSha256==issued.CommercialSnapshotSha256)
                || !PrincipalOrderFundingEvidence.Local.Any(x=>x.Id==issued.FundingEvidenceId
                    &&x.ManualCommitmentReference==issued.ManualCommitmentReference))
                throw new InvalidOperationException("PrincipalOrderIssuePinInvalid");
        }
        foreach(var acknowledgment in ChangeTracker.Entries<PrincipalOrderAcknowledgment>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
            if(acknowledgment.RecordedByPrincipalId!=scope.SubjectId
                || !PrincipalOrderIssuedVersions.Local.Any(x=>x.Id==acknowledgment.IssuedVersionId
                    &&x.PurchaseOrderId==acknowledgment.PurchaseOrderId))
                throw new InvalidOperationException("PrincipalOrderAcknowledgmentPinInvalid");
        foreach(var request in ChangeTracker.Entries<PrincipalOrderReleaseRequest>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==request.PurchaseOrderId);
            if(order is null || order.PrincipalDraftSchemaVersion!=2 || order.Status!="ReleasePending"
                || order.BuyerSubjectId!=scope.SubjectId || request.RequestedByPrincipalId!=scope.SubjectId
                || request.OriginalStatus=="PendingApproval" && (request.CancelledCoreCaseId is null
                    || request.CancelledCoreCaseId==Guid.Empty || request.CoreCancellationDecisionId is null
                    || request.CoreCancellationDecisionId==Guid.Empty
                    || order.ApprovalInstanceRef!=request.CancelledCoreCaseId.Value.ToString("D"))
                || request.OriginalStatus!="PendingApproval" && (request.CancelledCoreCaseId is not null
                    || request.CoreCancellationDecisionId is not null)
                || request.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || !PrincipalOrderFundingEvidence.Local.Any(x=>x.Id==request.FundingEvidenceId&&x.PurchaseOrderId==order.Id))
                throw new InvalidOperationException("PrincipalOrderReleaseRequestPinInvalid");
        }
        foreach(var release in ChangeTracker.Entries<PrincipalOrderReleaseConfirmation>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==release.PurchaseOrderId);
            if(order is null || order.Status!="Cancelled" || release.ConfirmedByPrincipalId!=scope.SubjectId
                || !PrincipalOrderReleaseRequests.Local.Any(x=>x.Id==release.ReleaseRequestId&&x.PurchaseOrderId==order.Id
                    &&x.FundingEvidenceId==release.FundingEvidenceId)
                || !PrincipalOrderFundingEvidence.Local.Any(x=>x.Id==release.FundingEvidenceId
                    &&x.ManualCommitmentReference==release.ManualCommitmentReference))
                throw new InvalidOperationException("PrincipalOrderReleaseConfirmationPinInvalid");
        }
    }
}
