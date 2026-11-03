using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SourcingPublicationOpening> SourcingPublicationOpenings => Set<SourcingPublicationOpening>();
    public DbSet<SourcingPublicationDecision> SourcingPublicationDecisions => Set<SourcingPublicationDecision>();
    public DbSet<SourcingPublicationReceipt> SourcingPublicationReceipts => Set<SourcingPublicationReceipt>();

    private void ConfigureSourcingPublication(ModelBuilder model)
    {
        ConfigureBase<SourcingPublicationOpening>(model, "sourcing_publication_openings");
        ConfigureBase<SourcingPublicationDecision>(model, "sourcing_publication_decisions");
        ConfigureBase<SourcingPublicationReceipt>(model, "sourcing_publication_receipts");
        var opening = model.Entity<SourcingPublicationOpening>();
        var decision = model.Entity<SourcingPublicationDecision>();
        var receipt = model.Entity<SourcingPublicationReceipt>();
        opening.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        opening.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        opening.Property(x => x.OriginalDraftFingerprint).HasMaxLength(64);
        opening.Property(x => x.SnapshotJson).HasColumnType("text");
        opening.Property(x => x.SnapshotSha256).HasMaxLength(64);
        decision.Property(x => x.Action).HasMaxLength(20);
        decision.Property(x => x.CaseState).HasMaxLength(20);
        decision.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.Operation).HasMaxLength(20);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.CommandSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        Link<SourcingPublicationOpening, SourcingEvent>(model, nameof(SourcingPublicationOpening.SourcingEventId));
        Link<SourcingPublicationOpening, PolicyVersion>(model, nameof(SourcingPublicationOpening.PolicyVersionId));
        Link<SourcingPublicationOpening, SourcingPolicyIdentityEvidence>(model, nameof(SourcingPublicationOpening.PublishedPolicyEvidenceId));
        Link<SourcingPublicationDecision, SourcingEvent>(model, nameof(SourcingPublicationDecision.SourcingEventId));
        Link<SourcingPublicationDecision, SourcingPublicationOpening>(model, nameof(SourcingPublicationDecision.OpeningId));
        Link<SourcingPublicationReceipt, SourcingEvent>(model, nameof(SourcingPublicationReceipt.SourcingEventId));
        Link<SourcingPublicationReceipt, SourcingPublicationOpening>(model, nameof(SourcingPublicationReceipt.OpeningId));
        Link<SourcingPublicationReceipt, SourcingPublicationDecision>(model, nameof(SourcingPublicationReceipt.DecisionId));
        opening.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
        opening.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.EventVersionAtOpening }).IsUnique();
        decision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalDecisionId }).IsUnique();
        decision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.CoreAssignmentId }).IsUnique();
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
        const string canonical = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        opening.ToTable(t => t.HasCheckConstraint("ck_sourcing_publication_opening_v2",
            "schema_version=2 AND event_version_at_opening>0 AND policy_version_number>0"
            + " AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND published_policy_evidence_id <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id" + canonical
            + " AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"
            + " AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$'"
            + " AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(opened_at)"));
        decision.ToTable(t => t.HasCheckConstraint("ck_sourcing_publication_decision_v2",
            "schema_version=2 AND action IN ('Approve','Return','Reject') AND case_state IN ('Pending','Approved','Returned','Rejected')"
            + " AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND core_approval_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id" + canonical
            + " AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"
            + " AND event_version_at_decision>0 AND isfinite(decided_at)"));
        receipt.ToTable(t => t.HasCheckConstraint("ck_sourcing_publication_receipt_v2",
            "schema_version=2 AND operation IN ('Open','Decide') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id" + canonical
            + " AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"
            + " AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'"
            + " AND result_version>0 AND isfinite(completed_at)"
            + " AND ((operation='Open' AND decision_id IS NULL AND response_status_code=201)"
            + " OR (operation='Decide' AND decision_id IS NOT NULL AND response_status_code=200))"));
    }

    private void GuardSourcingPublication()
    {
        foreach (var entry in ChangeTracker.Entries<SourcingPublicationOpening>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("SourcingPublicationOpeningAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity;
            var ev = SourcingEvents.Local.SingleOrDefault(x => x.Id == row.SourcingEventId);
            var policy = PolicyVersions.Local.SingleOrDefault(x => x.Id == row.PolicyVersionId)
                ?? PolicyVersions.SingleOrDefault(x => x.Id == row.PolicyVersionId);
            var published = SourcingPolicyIdentityEvidenceRecords.Local.SingleOrDefault(x => x.Id == row.PublishedPolicyEvidenceId)
                ?? SourcingPolicyIdentityEvidenceRecords.SingleOrDefault(x => x.Id == row.PublishedPolicyEvidenceId);
            var core = sharedApprovalCore?.Cases.SingleOrDefault(x => x.Id == row.CoreApprovalCaseId);
            if (ev is null || Entry(ev).State != EntityState.Modified || ev.ExecutionVersion != 2
                || ev.Status != "PublicationReview" || ev.ApprovalInstanceRef != row.CoreApprovalCaseId.ToString("D")
                || checked(Entry(ev).OriginalValues.GetValue<long>(nameof(SourcingEvent.Version)) + 1) != row.EventVersionAtOpening
                || ev.OriginalDraftFingerprint != row.OriginalDraftFingerprint
                || ev.OwnerSubjectId != row.MakerPrincipalId || row.MakerPrincipalId != scope.SubjectId
                || row.MakerCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.SchemaVersion != 2 || row.OpenedAt == default
                || row.OpenedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(row.OpenedAt)
                || row.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(row.SnapshotJson)
                || policy is null || policy.PolicyKey != SourcingPublicationPolicyRules.PolicyKey
                || policy.State != "Published" || policy.PolicyVersionNumber != row.PolicyVersionNumber
                || row.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                || published is null || published.Stage != "Published" || published.SchemaVersion != 2
                || published.PolicyVersionId != policy.Id || !sharedApprovalInsertedCases.Contains(row.CoreApprovalCaseId)
                || core is null || core.State != "Pending" || core.SourceModule != "procurement"
                || core.SourceType != PrincipalRfqPublicationCoreWorkflow.SourceType || core.SourceId != ev.Id
                || core.SourceVersion != row.EventVersionAtOpening - 1 || core.PolicyVersionId != policy.Id
                || core.PolicyVersionNumber != row.PolicyVersionNumber || core.MakerSubjectId != row.MakerPrincipalId)
                throw new InvalidOperationException("SourcingPublicationOpeningInvalid");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingPublicationDecision>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("SourcingPublicationDecisionAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity;
            var ev = SourcingEvents.Local.SingleOrDefault(x => x.Id == row.SourcingEventId)
                ?? SourcingEvents.SingleOrDefault(x => x.Id == row.SourcingEventId);
            var opening = SourcingPublicationOpenings.Local.SingleOrDefault(x => x.Id == row.OpeningId)
                ?? SourcingPublicationOpenings.SingleOrDefault(x => x.Id == row.OpeningId);
            var core = sharedApprovalCore?.Cases.SingleOrDefault(x => x.Id == row.CoreApprovalCaseId);
            var coreDecision = sharedApprovalCore?.Decisions.SingleOrDefault(x => x.Id == row.CoreApprovalDecisionId);
            if (ev is null || opening is null
                || opening.SourcingEventId != ev.Id || opening.CoreApprovalCaseId != row.CoreApprovalCaseId
                || ev.ApprovalInstanceRef != row.CoreApprovalCaseId.ToString("D")
                || (row.CaseState == "Pending" ? ev.Version
                    : Entry(ev).State == EntityState.Modified
                        ? checked(Entry(ev).OriginalValues.GetValue<long>(nameof(SourcingEvent.Version)) + 1)
                        : -1) != row.EventVersionAtDecision
                || row.SchemaVersion != 2
                || row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.ActorPrincipalId != scope.SubjectId || row.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || row.DecidedAt == default || row.DecidedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(row.DecidedAt)
                || !sharedApprovalInsertedDecisions.Contains(row.CoreApprovalDecisionId)
                || core is null || coreDecision is null || core.State != row.CaseState
                || core.SourceModule != "procurement" || core.SourceType != PrincipalRfqPublicationCoreWorkflow.SourceType
                || core.SourceId != ev.Id || core.Id != opening.CoreApprovalCaseId
                || coreDecision.CaseId != core.Id || coreDecision.AssignmentId != row.CoreAssignmentId
                || coreDecision.ActorSubjectId != row.ActorPrincipalId || coreDecision.Action != row.Action
                || ProcurementEvidenceEncoding.DatabaseTimestamp(coreDecision.DecidedAt) != row.DecidedAt
                || row.CaseState == "Pending" && (row.Action != "Approve" || ev.Status != "PublicationReview")
                || row.CaseState is "Approved" && ev.Status != "Published"
                || row.CaseState is "Returned" or "Rejected" && ev.Status != "Draft")
                throw new InvalidOperationException("SourcingPublicationDecisionInvalid");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingPublicationReceipt>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("SourcingPublicationReceiptAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity;
            var opening = SourcingPublicationOpenings.Local.SingleOrDefault(x => x.Id == row.OpeningId)
                ?? SourcingPublicationOpenings.SingleOrDefault(x => x.Id == row.OpeningId);
            var decision = row.DecisionId is { } decisionId
                ? SourcingPublicationDecisions.Local.SingleOrDefault(x => x.Id == decisionId) : null;
            if (row.SchemaVersion != 2 || opening is null || opening.SourcingEventId != row.SourcingEventId
                || row.CommandKey == Guid.Empty || row.ActorPrincipalId != scope.SubjectId
                || row.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || row.CommandSha256 is not { Length: 64 }
                || !PrincipalRfqPublicationReceiptResult.Matches(row, opening, decision)
                || row.ResultVersion < 1 || row.CompletedAt == default
                || (row.Operation == "Open" ? row.DecisionId is not null || row.ResponseStatusCode != 201
                    || Entry(opening).State != EntityState.Added || opening.MakerPrincipalId != row.ActorPrincipalId
                    || row.ResultVersion != opening.EventVersionAtOpening
                    : row.Operation != "Decide" || decision is null || decision.OpeningId != opening.Id
                    || decision.ActorPrincipalId != row.ActorPrincipalId || row.ResponseStatusCode != 200
                    || row.ResultVersion != decision.EventVersionAtDecision))
                throw new InvalidOperationException("SourcingPublicationReceiptInvalid");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingInvitation>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var ev = SourcingEvents.Local.SingleOrDefault(x => x.Id == entry.Entity.SourcingEventId)
                ?? SourcingEvents.SingleOrDefault(x => x.Id == entry.Entity.SourcingEventId);
            if (ev?.ExecutionVersion != 2) continue;
            var invite = entry.Entity;
            if (entry.State == EntityState.Modified
                && entry.OriginalValues.GetValue<string>(nameof(SourcingInvitation.Status)) == "ReadyForManualDelivery"
                && invite.Status == "ManualContactRecorded")
            {
                var pending = ChangeTracker.Entries<SourcingManualContact>().Where(x => x.State == EntityState.Added
                    && x.Entity.InvitationId == invite.Id).Select(x => x.Entity).ToArray();
                if (ev.Status != "Published" || pending.Length != 1
                    || invite.TenantId != scope.TenantId || invite.LegalEntityId != scope.LegalEntityId
                    || invite.SentAt is not null || invite.ViewedAt is not null || invite.DeliveryRef is not null
                    || pending[0].TenantId != invite.TenantId || pending[0].LegalEntityId != invite.LegalEntityId
                    || pending[0].SourcingEventId != ev.Id || pending[0].SupplierId != invite.SupplierId
                    || pending[0].InvitationVersionAtCapture != checked(entry.OriginalValues.GetValue<long>(nameof(SourcingInvitation.Version)) + 1)
                    || entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                        (nameof(SourcingInvitation.Status) or nameof(SourcingInvitation.Version)
                        or nameof(SourcingInvitation.UpdatedAt) or nameof(SourcingInvitation.UpdatedBy))))
                    throw new InvalidOperationException("ManualContactInvitationTransitionInvalid");
                PrincipalManualQuotationEvidence.VerifyContact(pending[0]);
                // The post-version owning guard verifies the complete retained
                // M4 receipt and resulting invitation version before persistence.
                continue;
            }
            var approval = SourcingPublicationDecisions.Local.SingleOrDefault(x => x.SourcingEventId == ev.Id
                && x.CoreApprovalCaseId.ToString("D") == ev.ApprovalInstanceRef && x.CaseState == "Approved");
            var supplier = Suppliers.Include(x => x.Qualifications).SingleOrDefault(x => x.Id == invite.SupplierId);
            if (entry.State != EntityState.Added || ev.Status != "Published"
                || invite.TenantId != scope.TenantId || invite.LegalEntityId != scope.LegalEntityId
                || invite.Status != "ReadyForManualDelivery" || invite.SentAt is not null
                || invite.ViewedAt is not null || invite.DeliveryRef is not null
                || approval is null || Entry(approval).State != EntityState.Added || supplier is null
                || !SupplierEligibility.CanReceiveNewOrders(supplier, approval.DecidedAt))
                throw new InvalidOperationException("SourcingInvitationRequiresInternalPublication");
        }
        // The coordinator saves Procurement once in the command and again before
        // commit. Only the newly inserted decision may require newly inserted
        // invitations; on the second save both are Unchanged.
        foreach (var approved in ChangeTracker.Entries<SourcingPublicationDecision>()
            .Where(x => x.State == EntityState.Added && x.Entity.CaseState == "Approved")
            .Select(x => x.Entity))
        {
            var opening = SourcingPublicationOpenings.Local.SingleOrDefault(x => x.Id == approved.OpeningId)
                ?? SourcingPublicationOpenings.SingleOrDefault(x => x.Id == approved.OpeningId)
                ?? throw new InvalidOperationException("SourcingShortlistMissing");
            using var snapshot = System.Text.Json.JsonDocument.Parse(opening.SnapshotJson);
            var expected = snapshot.RootElement.GetProperty("supplierIds").EnumerateArray()
                .Select(x => x.GetGuid()).ToHashSet();
            var actual = ChangeTracker.Entries<SourcingInvitation>().Where(x => x.State == EntityState.Added
                && x.Entity.SourcingEventId == approved.SourcingEventId).Select(x => x.Entity.SupplierId).ToArray();
            if (expected.Count is < 1 or > 100 || actual.Length != expected.Count
                || !actual.ToHashSet().SetEquals(expected))
                throw new InvalidOperationException("SourcingShortlistInvitationMismatch");
        }
    }
}
