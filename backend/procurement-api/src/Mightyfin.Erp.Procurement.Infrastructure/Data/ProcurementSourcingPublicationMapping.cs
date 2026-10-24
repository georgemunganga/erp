using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SourcingPublicationOpening> SourcingPublicationOpenings => Set<SourcingPublicationOpening>();
    public DbSet<SourcingPublicationDecision> SourcingPublicationDecisions => Set<SourcingPublicationDecision>();
    public DbSet<SourcingPublicationReceipt> SourcingPublicationReceipts => Set<SourcingPublicationReceipt>();
    public DbSet<SourcingManualContact> SourcingManualContacts => Set<SourcingManualContact>();
    public DbSet<SourcingManualQuoteVersion> SourcingManualQuoteVersions => Set<SourcingManualQuoteVersion>();

    private void ConfigureSourcingPublication(ModelBuilder model)
    {
        ConfigureBase<SourcingPublicationOpening>(model, "sourcing_publication_openings");
        ConfigureBase<SourcingPublicationDecision>(model, "sourcing_publication_decisions");
        ConfigureBase<SourcingPublicationReceipt>(model, "sourcing_publication_receipts");
        ConfigureBase<SourcingManualContact>(model, "sourcing_manual_contacts");
        ConfigureBase<SourcingManualQuoteVersion>(model, "sourcing_manual_quote_versions");
        var opening = model.Entity<SourcingPublicationOpening>();
        var decision = model.Entity<SourcingPublicationDecision>();
        var receipt = model.Entity<SourcingPublicationReceipt>();
        var contact = model.Entity<SourcingManualContact>();
        var quote = model.Entity<SourcingManualQuoteVersion>();
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
        contact.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        contact.Property(x => x.Channel).HasMaxLength(30);
        contact.Property(x => x.Reference).HasMaxLength(200);
        contact.Property(x => x.CommandSha256).HasMaxLength(64);
        contact.Property(x => x.ResponseJson).HasColumnType("text");
        contact.Property(x => x.ResponseSha256).HasMaxLength(64);
        quote.Property(x => x.Operation).HasMaxLength(20);
        quote.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        quote.Property(x => x.SnapshotJson).HasColumnType("text");
        quote.Property(x => x.SnapshotSha256).HasMaxLength(64);
        quote.Property(x => x.CommandSha256).HasMaxLength(64);
        quote.Property(x => x.ResponseJson).HasColumnType("text");
        quote.Property(x => x.ResponseSha256).HasMaxLength(64);
        model.Entity<SupplierBid>().Property(x => x.DraftRevision).HasDefaultValue(0);
        Link<SourcingPublicationOpening, SourcingEvent>(model, nameof(SourcingPublicationOpening.SourcingEventId));
        Link<SourcingPublicationOpening, PolicyVersion>(model, nameof(SourcingPublicationOpening.PolicyVersionId));
        Link<SourcingPublicationOpening, SourcingPolicyIdentityEvidence>(model, nameof(SourcingPublicationOpening.PublishedPolicyEvidenceId));
        Link<SourcingPublicationDecision, SourcingEvent>(model, nameof(SourcingPublicationDecision.SourcingEventId));
        Link<SourcingPublicationDecision, SourcingPublicationOpening>(model, nameof(SourcingPublicationDecision.OpeningId));
        Link<SourcingPublicationReceipt, SourcingEvent>(model, nameof(SourcingPublicationReceipt.SourcingEventId));
        Link<SourcingPublicationReceipt, SourcingPublicationOpening>(model, nameof(SourcingPublicationReceipt.OpeningId));
        Link<SourcingPublicationReceipt, SourcingPublicationDecision>(model, nameof(SourcingPublicationReceipt.DecisionId));
        Link<SourcingManualContact, SourcingEvent>(model, nameof(SourcingManualContact.SourcingEventId));
        Link<SourcingManualContact, SourcingInvitation>(model, nameof(SourcingManualContact.InvitationId));
        Link<SourcingManualContact, Supplier>(model, nameof(SourcingManualContact.SupplierId));
        Link<SourcingManualQuoteVersion, SourcingEvent>(model, nameof(SourcingManualQuoteVersion.SourcingEventId));
        Link<SourcingManualQuoteVersion, SupplierBid>(model, nameof(SourcingManualQuoteVersion.SupplierBidId));
        opening.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
        opening.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.EventVersionAtOpening }).IsUnique();
        decision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalDecisionId }).IsUnique();
        decision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.CoreAssignmentId }).IsUnique();
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
        contact.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.InvitationId }).IsUnique();
        contact.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
        quote.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId, x.Revision }).IsUnique();
        quote.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
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
        contact.ToTable(t => t.HasCheckConstraint("ck_sourcing_manual_contact_v2",
            "schema_version=2 AND channel IN ('EmailOutsideSystem','Physical','Telephone','Other')"
            + " AND reference IS NOT NULL AND length(btrim(reference)) BETWEEN 1 AND 200"
            + " AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id" + canonical
            + " AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"
            + " AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'"
            + " AND isfinite(reported_contact_at) AND isfinite(recorded_at) AND reported_contact_at <= recorded_at"));
        quote.ToTable(t => t.HasCheckConstraint("ck_sourcing_manual_quote_version_v2",
            "schema_version=2 AND operation IN ('Create','Revise','Cancel') AND revision>0 AND bid_version>0"
            + " AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id" + canonical
            + " AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"
            + " AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid"
            + " AND command_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$'"
            + " AND response_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(recorded_at)"));
    }

    private void GuardSourcingPublicationEvidence()
    {
        foreach (var entry in ChangeTracker.Entries<SourcingManualQuoteVersion>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("SourcingManualQuoteEvidenceAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity;
            var bid = SupplierBids.Local.SingleOrDefault(x => x.Id == row.SupplierBidId)
                ?? SupplierBids.SingleOrDefault(x => x.Id == row.SupplierBidId);
            var invitation = bid?.InvitationId is { } invitationId
                ? SourcingInvitations.SingleOrDefault(x => x.Id == invitationId) : null;
            var ev = bid is null ? null : SourcingEvents.SingleOrDefault(x => x.Id == bid.SourcingEventId);
            if (bid is null || ev is null || invitation is null || ev.ExecutionVersion != 2
                || ev.Status != "Published" || invitation.Status != "ManualContactRecorded"
                || row.SchemaVersion != 2 || row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.SourcingEventId != ev.Id || row.ActorPrincipalId != scope.SubjectId
                || row.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || bid.SurrogateEnteredBy != row.ActorPrincipalId || bid.SourceChannel != "Manual"
                || bid.SubmissionVersion != 1 || bid.SubmittedAt is not null
                || row.CommandKey == Guid.Empty || row.CommandSha256 is not { Length: 64 }
                || row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(row.ResponseJson)
                || row.RecordedAt == default)
                throw new InvalidOperationException("SourcingManualQuoteEvidenceInvalid");
            var bidEntry = Entry(bid);
            var expectedVersion = bidEntry.State == EntityState.Added ? 1
                : bidEntry.State == EntityState.Modified
                    ? checked(bidEntry.OriginalValues.GetValue<long>(nameof(SupplierBid.Version)) + 1) : -1;
            var prior = SourcingManualQuoteVersions.AsNoTracking().Where(x => x.SupplierBidId == bid.Id)
                .OrderByDescending(x => x.Revision).FirstOrDefault();
            if (row.BidVersion != expectedVersion || row.Revision != (prior?.Revision ?? 0) + 1
                || bid.DraftRevision != row.Revision
                || row.Operation == "Create" && (prior is not null || bidEntry.State != EntityState.Added || bid.Status != "Draft")
                || row.Operation == "Revise" && (prior is null || bidEntry.State != EntityState.Modified || bid.Status != "Draft")
                || row.Operation == "Cancel" && (prior is null || bidEntry.State != EntityState.Modified || bid.Status != "Cancelled")
                || row.Operation is not ("Create" or "Revise" or "Cancel"))
                throw new InvalidOperationException("SourcingManualQuoteRevisionInvalid");
            var lines = SupplierBidLines.Where(x => x.SupplierBidId == bid.Id).ToList();
            foreach (var local in SupplierBidLines.Local.Where(x => x.SupplierBidId == bid.Id))
            {
                lines.RemoveAll(x => x.Id == local.Id);
                lines.Add(local);
            }
            if (lines.Count is < 1 or > 200 || lines.Any(x => Entry(x).State == EntityState.Deleted)
                || row.Operation == "Create" && lines.Any(x => Entry(x).State != EntityState.Added)
                || row.Operation == "Cancel" && lines.Any(x => Entry(x).State != EntityState.Unchanged)
                || row.SnapshotJson != PrincipalManualQuotationSnapshot.Build(bid, lines)
                || row.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(row.SnapshotJson))
                throw new InvalidOperationException("SourcingManualQuoteSnapshotInvalid");
            if (row.Operation != "Cancel")
            {
                var sources = SourcingLines.Where(x => x.SourcingEventId == ev.Id).ToArray();
                PrincipalManualQuotationDraft.Validate(ev, invitation, bid, sources, lines, row.ActorPrincipalId);
            }
        }
        foreach (var bidEntry in ChangeTracker.Entries<SupplierBid>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var bid = bidEntry.Entity;
            // Stored state remains authoritative. A tracked v2 parent can additionally
            // require the negative mutation guard; it never grants publication authority.
            var ev = SourcingEvents.SingleOrDefault(x => x.Id == bid.SourcingEventId)
                ?? SourcingEvents.Local.SingleOrDefault(x => x.Id == bid.SourcingEventId);
            if (ev?.ExecutionVersion != 2) continue;
            if (bidEntry.State == EntityState.Deleted || !SourcingManualQuoteVersions.Local.Any(x => x.SupplierBidId == bid.Id))
                throw new InvalidOperationException("SourcingManualQuoteVersionRequired");
            if (bidEntry.State == EntityState.Modified)
            {
                var changed = bidEntry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name).ToHashSet();
                if (!changed.IsSubsetOf([nameof(SupplierBid.Status), nameof(SupplierBid.DraftRevision), nameof(ProcurementRecord.Version),
                    nameof(ProcurementRecord.UpdatedAt), nameof(ProcurementRecord.UpdatedBy)])
                    || bidEntry.OriginalValues.GetValue<string>(nameof(SupplierBid.Status)) != "Draft")
                    throw new InvalidOperationException("SourcingManualQuoteMaterialLocked");
            }
        }
        foreach (var lineEntry in ChangeTracker.Entries<SupplierBidLine>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var line = lineEntry.Entity;
            var bid = SupplierBids.Local.SingleOrDefault(x => x.Id == line.SupplierBidId)
                ?? SupplierBids.SingleOrDefault(x => x.Id == line.SupplierBidId);
            var ev = bid is null ? null : SourcingEvents.SingleOrDefault(x => x.Id == bid.SourcingEventId)
                ?? SourcingEvents.Local.SingleOrDefault(x => x.Id == bid.SourcingEventId);
            if (ev?.ExecutionVersion != 2) continue;
            var revision = SourcingManualQuoteVersions.Local.SingleOrDefault(x => x.SupplierBidId == bid!.Id);
            if (revision is null || lineEntry.State == EntityState.Deleted
                || revision.Operation == "Cancel" || revision.Operation == "Create" && lineEntry.State != EntityState.Added)
                throw new InvalidOperationException("SourcingManualQuoteLineVersionRequired");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingManualContact>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("SourcingManualContactAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity;
            var ev = SourcingEvents.Local.SingleOrDefault(x => x.Id == row.SourcingEventId)
                ?? SourcingEvents.SingleOrDefault(x => x.Id == row.SourcingEventId);
            var invitation = SourcingInvitations.Local.SingleOrDefault(x => x.Id == row.InvitationId)
                ?? SourcingInvitations.SingleOrDefault(x => x.Id == row.InvitationId);
            var approved = SourcingPublicationDecisions.AsNoTracking().Where(x => x.SourcingEventId == row.SourcingEventId
                && x.CaseState == "Approved").OrderByDescending(x => x.DecidedAt).FirstOrDefault();
            if (ev is null || ev.ExecutionVersion != 2 || ev.Status != "Published"
                || invitation is null || invitation.SourcingEventId != ev.Id || invitation.SupplierId != row.SupplierId
                || Entry(invitation).State != EntityState.Modified
                || invitation.Status != "ManualContactRecorded" || invitation.SentAt is not null
                || invitation.ViewedAt is not null || invitation.DeliveryRef is not null
                || row.SchemaVersion != 2 || row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.ActorPrincipalId != scope.SubjectId || row.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || row.CommandKey == Guid.Empty || row.CommandSha256 is not { Length: 64 }
                || row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(row.ResponseJson)
                || row.ReportedContactAt > row.RecordedAt || row.Reference is null
                || row.Reference.Trim().Length is < 1 or > 200
                || row.Channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other")
                || approved is null || row.ReportedContactAt < approved.DecidedAt)
                throw new InvalidOperationException("SourcingManualContactInvalid");
        }
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
                || row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(row.ResponseJson)
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
            var approval = SourcingPublicationDecisions.Local.SingleOrDefault(x => x.SourcingEventId == ev.Id
                && x.CoreApprovalCaseId.ToString("D") == ev.ApprovalInstanceRef && x.CaseState == "Approved");
            var supplier = Suppliers.Include(x => x.Qualifications).SingleOrDefault(x => x.Id == invite.SupplierId);
            var contact = SourcingManualContacts.Local.SingleOrDefault(x => x.InvitationId == invite.Id);
            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name).ToHashSet();
                if (ev.Status != "Published" || invite.Status != "ManualContactRecorded"
                    || entry.OriginalValues.GetValue<string>(nameof(SourcingInvitation.Status)) != "ReadyForManualDelivery"
                    || !changed.IsSubsetOf([nameof(SourcingInvitation.Status), nameof(ProcurementRecord.Version),
                        nameof(ProcurementRecord.UpdatedAt), nameof(ProcurementRecord.UpdatedBy)])
                    || contact is null || contact.SourcingEventId != ev.Id || contact.SupplierId != invite.SupplierId)
                    throw new InvalidOperationException("SourcingInvitationContactInvalid");
                continue;
            }
            if (entry.State != EntityState.Added || ev.Status != "Published"
                || invite.TenantId != scope.TenantId || invite.LegalEntityId != scope.LegalEntityId
                || invite.Status != "ReadyForManualDelivery" || invite.SentAt is not null
                || invite.ViewedAt is not null || invite.DeliveryRef is not null
                || approval is null || Entry(approval).State != EntityState.Added || supplier is null
                || !SupplierEligibility.CanReceiveNewOrders(supplier, approval.DecidedAt))
                throw new InvalidOperationException("SourcingInvitationRequiresInternalPublication");
        }
        // A coordinator may save the same context again after the command saves.
        // Validate the invitation batch only for a newly recorded terminal vote.
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
