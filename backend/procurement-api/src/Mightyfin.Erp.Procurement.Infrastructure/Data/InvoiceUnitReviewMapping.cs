using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<InvoiceUnitReviewSource> InvoiceUnitReviewSources => Set<InvoiceUnitReviewSource>();
    public DbSet<InvoiceUnitReviewAssignmentEvidence> InvoiceUnitReviewAssignments => Set<InvoiceUnitReviewAssignmentEvidence>();
    public DbSet<InvoiceUnitReviewOutcome> InvoiceUnitReviewOutcomes => Set<InvoiceUnitReviewOutcome>();
    public DbSet<InvoiceUnitReviewCommandReceipt> InvoiceUnitReviewReceipts => Set<InvoiceUnitReviewCommandReceipt>();
    private void ConfigureInvoiceUnitReview(ModelBuilder model)
    {
        ConfigureBase<InvoiceUnitReviewSource>(model, "invoice_unit_review_sources");
        ConfigureBase<InvoiceUnitReviewAssignmentEvidence>(model, "invoice_unit_review_assignments");
        ConfigureBase<InvoiceUnitReviewOutcome>(model, "invoice_unit_review_outcomes");
        ConfigureBase<InvoiceUnitReviewCommandReceipt>(model, "invoice_unit_review_receipts");
        var source = model.Entity<InvoiceUnitReviewSource>();
        source.Property(x => x.SourceJson).HasColumnType("text"); source.Property(x => x.IssuedJson).HasColumnType("text");
        source.Property(x => x.MakerPrincipalId).HasMaxLength(200);
        foreach (var name in new[] { nameof(InvoiceUnitReviewSource.OriginalSha256), nameof(InvoiceUnitReviewSource.MappingSha256), nameof(InvoiceUnitReviewSource.SourceSha256) }) source.Property(name).HasMaxLength(64);
        source.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId }).HasName("ak_inv_unit_source_case");
        source.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId }).IsUnique().HasDatabaseName("ux_inv_unit_source_case");
        source.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId, x.MappingVersionId }).IsUnique().HasDatabaseName("ux_inv_unit_source_mapping");
        source.HasOne<InvoiceMatchMappingVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.IssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.IssuedVersionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_source_mapping");
        source.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MatchingPolicyId }).HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_source_matching_policy");
        source.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewPolicyId }).HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_source_review_policy");
        source.ToTable(t => t.HasCheckConstraint("ck_inv_unit_source", "invoice_version_at_open > 0 AND mapping_revision > 0 AND matching_policy_id <> review_policy_id AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(maker_principal_id))>0 AND source_sha256 IS NOT NULL AND source_sha256 ~ '^[0-9a-f]{64}$' AND original_sha256 IS NOT NULL AND original_sha256 ~ '^[0-9a-f]{64}$' AND mapping_sha256 IS NOT NULL AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(source_json) BETWEEN 1 AND 262144 AND octet_length(issued_json) BETWEEN 1 AND 262144 AND isfinite(opened_at)"));
        var pin = model.Entity<InvoiceUnitReviewAssignmentEvidence>();
        pin.Property(x => x.PrincipalId).HasMaxLength(200);
        pin.HasOne<InvoiceUnitReviewSource>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.CoreCaseId }).HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_assignment_source");
        pin.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId }).IsUnique().HasDatabaseName("ux_inv_unit_assignment");
        pin.ToTable(t => t.HasCheckConstraint("ck_inv_unit_assignment", "core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(principal_id))>0"));
        var outcome = model.Entity<InvoiceUnitReviewOutcome>();
        outcome.Property(x => x.State).HasMaxLength(20); outcome.Property(x => x.EvidenceJson).HasColumnType("text"); outcome.Property(x => x.EvidenceSha256).HasMaxLength(64); outcome.Property(x => x.ReviewerPrincipalId).HasMaxLength(200);
        outcome.HasOne<InvoiceUnitReviewSource>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.CoreCaseId }).HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_outcome_source");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId }).IsUnique().HasDatabaseName("ux_inv_unit_outcome_source");
        outcome.ToTable(t => t.HasCheckConstraint("ck_inv_unit_outcome", "((state='Verified' AND evidence_schema_version=2) OR (state IN ('Returned','Rejected') AND evidence_schema_version=1)) AND final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(reviewer_principal_id))>0 AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)"));
        var receipt = model.Entity<InvoiceUnitReviewCommandReceipt>();
        receipt.Property(x => x.RequestJson).HasColumnType("text"); receipt.Property(x => x.Operation).HasMaxLength(20); receipt.Property(x => x.ActorPrincipalId).HasMaxLength(200); receipt.Property(x => x.Fingerprint).HasMaxLength(64); receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasOne<InvoiceUnitReviewSource>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId }).HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_inv_unit_receipt_source");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.CommandKey }).IsUnique().HasDatabaseName("ux_inv_unit_command_key");
        receipt.ToTable(t => t.HasCheckConstraint("ck_inv_unit_receipt", "operation IN ('Open','Decision') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND fingerprint IS NOT NULL AND fingerprint ~ '^[0-9a-f]{64}$' AND octet_length(response_json) BETWEEN 1 AND 262144 AND octet_length(request_json) BETWEEN 1 AND 65536 AND isfinite(completed_at)"));
    }
    private void GuardInvoiceUnitReview()
    {
        var rows = ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is InvoiceUnitReviewSource or InvoiceUnitReviewAssignmentEvidence or InvoiceUnitReviewOutcome or InvoiceUnitReviewCommandReceipt).ToArray();
        foreach (var entry in rows)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("InvoiceUnitReviewAppendOnly");
            if (entry.State == EntityState.Added && (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId || entry.Entity.Version != 1)) throw new InvalidOperationException("InvoiceUnitReviewScopeMismatch");
        }
        var added = rows.Where(x => x.State == EntityState.Added).ToArray(); if (added.Length == 0) return;
        if (scope.WorkerId is not { } worker || worker == Guid.Empty || !Database.IsNpgsql() || Database.CurrentTransaction is null)
            throw new InvalidOperationException("InvoiceUnitReviewOwningTransactionRequired");
        using var core = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>().UseNpgsql(Database.GetDbConnection()).Options, new UnitReviewApprovalScope(scope.TenantId, scope.LegalEntityId, scope.SubjectId));
        core.Database.UseTransaction(Database.CurrentTransaction.GetDbTransaction());
        foreach (var source in added.Select(x => x.Entity).OfType<InvoiceUnitReviewSource>())
        {
            var typed = InvoiceUnitReviewStorage.Read(source);
            var invoice = SupplierInvoices.Single(x => x.Id == source.SupplierInvoiceId);
            var mapping = InvoiceMatchMappingVersions.Single(x => x.Id == source.MappingVersionId);
            var originals = SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive).OrderBy(x => x.LineNumber).ToArray();
            _ = InvoiceMappingEvidence.Verify(mapping, invoice, originals);
            if (source.MakerPrincipalId != scope.SubjectId || source.MakerWorkerId != worker || invoice.Version != source.InvoiceVersionAtOpen
                || invoice.CurrentMappingVersionId != mapping.Id || invoice.BranchRef != source.BranchRef || mapping.ActorPrincipalId != scope.SubjectId || mapping.ActorWorkerId != worker
                || source.MappingRevision != mapping.MappingRevision || source.MappingSha256 != mapping.MappingSha256 || source.OriginalSha256 != mapping.InvoiceOriginalSha256)
                throw new InvalidOperationException("InvoiceUnitReviewSourceBindingRequired");
            var order = PurchaseOrders.Single(x => x.Id == source.PurchaseOrderId);
            var issuedRow = OrderIssuedVersions.Single(x => x.Id == source.IssuedVersionId);
            var orderSubmission = OrderSubmissions.Single(x => x.Id == issuedRow.SubmissionId);
            var orderCase = core.Cases.Single(x => x.Id == orderSubmission.CoreApprovalCaseId);
            var orderLines = PurchaseOrderLines.Where(x => x.PurchaseOrderId == order.Id && x.IsActive).ToArray();
            ReceivingIssuedAuthority.Verify(order, orderLines, issuedRow, orderSubmission, orderCase,
                PolicyVersions.Single(x => x.Id == orderSubmission.PolicyVersionId));
            var delivery = OrderDeliveryEvidence.Where(x => x.IssuedVersionId == issuedRow.Id).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First();
            var projected = JsonSerializer.Deserialize<InvoiceIssuedOrder>(source.IssuedJson) ?? throw new InvalidOperationException("InvoiceUnitReviewIssuedProofRequired");
            if (delivery.Outcome != "Delivered" || projected.IssuedAt != delivery.ObservedAt || projected.IssuedReference != issuedRow.CommercialSnapshotSha256
                || projected.RevisionNumber != issuedRow.RevisionNumber || projected.RevisionState != "Issued"
                || projected.Id != order.Id || projected.RevisionId != issuedRow.Id || projected.BranchRef != source.BranchRef
                || projected.TenantId != source.TenantId || projected.LegalEntityId != source.LegalEntityId
                || projected.CurrencyCode != order.CurrencyCode || projected.SupplierId != order.SupplierId || projected.Lines.Count != orderLines.Length
                || orderLines.Any(line => !projected.Lines.Any(p => p.Id == line.Id && p.Quantity == line.Quantity && p.UnitPrice == line.UnitPrice && p.TaxAmount == line.TaxAmount && p.UnitCode == line.UnitCode
                    && p.CategoryCode == PurchaseRequestLines.Where(x => x.Id == line.PurchaseRequestLineId).Select(x => x.CategoryCode).Single())))
                throw new InvalidOperationException("InvoiceUnitReviewIssuedProjectionMismatch");
            InvoiceUnitReviewTransitions.VerifyIssuedEvidence(projected, mapping, invoice, originals, source.OpenedAt, typed.IssuedEvidenceSha256);
            var pins = added.Select(x => x.Entity).OfType<InvoiceUnitReviewAssignmentEvidence>().Where(x => x.ReviewSourceId == source.Id).ToArray();
            var workflow = core.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).Single(x => x.Id == source.CoreCaseId);
            var owned = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
            var matchingPolicy = PolicyVersions.Single(x => x.Id == source.MatchingPolicyId);
            var reviewPolicy = PolicyVersions.Single(x => x.Id == source.ReviewPolicyId);
            InvoiceUnitReviewTransitions.ValidateOwnedEvidence(owned, invoice, originals, mapping, projected,
                matchingPolicy, reviewPolicy, typed.Lines.GroupBy(x => x.DocumentVersionId).ToDictionary(x => x.Key, x => x.First().DocumentSha256), source.OpenedAt);
            VerifyUnitReviewPolicyIdentity(matchingPolicy);
            VerifyUnitReviewPolicyIdentity(reviewPolicy);
            if (!UnitReviewInsertedInCurrentTransaction("cases", workflow.Id) || workflow.State != "Pending" || core.Decisions.Any(x => x.CaseId == workflow.Id) || typed.ReviewPolicy.Stages.Sum(x => x.Assignees.Count) != pins.Length)
                throw new InvalidOperationException("InvoiceUnitReviewOpeningCaseRequired");
        }
        foreach (var source in added.Select(x => x.Entity).OfType<InvoiceUnitReviewSource>())
            if (added.Count(x => x.Entity is InvoiceUnitReviewCommandReceipt row && row.ReviewSourceId == source.Id && row.Operation == "Open") != 1)
                throw new InvalidOperationException("InvoiceUnitReviewOpeningReceiptRequired");
        foreach (var pin in added.Select(x => x.Entity).OfType<InvoiceUnitReviewAssignmentEvidence>())
            if (!added.Any(x => x.Entity is InvoiceUnitReviewSource source && source.Id == pin.ReviewSourceId)) throw new InvalidOperationException("InvoiceUnitReviewSameSaveSourceRequired");
        foreach (var outcome in added.Select(x => x.Entity).OfType<InvoiceUnitReviewOutcome>())
        {
            var source = InvoiceUnitReviewSources.Single(x => x.Id == outcome.ReviewSourceId);
            var typed = InvoiceUnitReviewStorage.Read(source);
            var pins = InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == source.Id).ToArray();
            var workflow = core.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).Single(x => x.Id == source.CoreCaseId);
            var decisions = core.Decisions.Where(x => x.CaseId == workflow.Id).ToArray();
            var plan = InvoiceUnitReviewStorage.Restore(source, pins, workflow);
            var invoice = SupplierInvoices.Single(x => x.Id == source.SupplierInvoiceId);
            var originals = SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive).OrderBy(x => x.LineNumber).ToArray();
            var mapping = InvoiceMatchMappingVersions.Single(x => x.Id == source.MappingVersionId);
            if (outcome.ReviewerPrincipalId != scope.SubjectId || outcome.ReviewerWorkerId != worker || outcome.CompletedAt < source.OpenedAt)
                throw new InvalidOperationException("InvoiceUnitReviewOutcomeActorRequired");
            // Recreate only the immutable issued projection recorded by the validated opening adapter.
            // It is revalidated against its exact fingerprint by Seal; mutable current authority remains an API guard.
            var issued = JsonSerializer.Deserialize<InvoiceIssuedOrder>(source.IssuedJson) ?? throw new InvalidOperationException("InvoiceUnitReviewIssuedProofRequired");
            var expected = InvoiceUnitReviewTerminal.Seal(plan, invoice, originals, mapping, issued,
                PolicyVersions.Single(x => x.Id == source.MatchingPolicyId), PolicyVersions.Single(x => x.Id == source.ReviewPolicyId),
                typed.Lines.GroupBy(x => x.DocumentVersionId).ToDictionary(x => x.Key, x => x.First().DocumentSha256), decisions, outcome.FinalDecisionId, outcome.CompletedAt);
            InvoiceUnitReviewStorage.VerifyOutcome(outcome, expected);
        }
        foreach (var receipt in added.Select(x => x.Entity).OfType<InvoiceUnitReviewCommandReceipt>())
        {
            var source = InvoiceUnitReviewSources.Local.SingleOrDefault(x => x.Id == receipt.ReviewSourceId) ?? InvoiceUnitReviewSources.Single(x => x.Id == receipt.ReviewSourceId);
            if (receipt.ActorPrincipalId != scope.SubjectId || receipt.ActorWorkerId != worker || receipt.CommandKey == Guid.Empty || receipt.CompletedAt < source.OpenedAt
                || receipt.Fingerprint != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.RequestJson))
                throw new InvalidOperationException("InvoiceUnitReviewReceiptBindingRequired");
            var workflow = core.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).Single(x => x.Id == source.CoreCaseId);
            var invoice = SupplierInvoices.Single(x => x.Id == source.SupplierInvoiceId);
            var pins = added.Select(x => x.Entity).OfType<InvoiceUnitReviewAssignmentEvidence>().Where(x => x.ReviewSourceId == source.Id).ToArray();
            if (pins.Length == 0) pins = InvoiceUnitReviewAssignments.Where(x => x.ReviewSourceId == source.Id).ToArray();
            var decision = receipt.Operation == "Decision" ? core.Decisions.Single(x => x.CaseId == source.CoreCaseId && x.IdempotencyKey == receipt.CommandKey.ToString("D")) : null;
            var outcome = added.Select(x => x.Entity).OfType<InvoiceUnitReviewOutcome>().SingleOrDefault(x => x.ReviewSourceId == source.Id);
            InvoiceUnitReviewCommands.Verify(receipt, source, invoice, workflow, pins, decision, outcome);
            if (receipt.Operation == "Open" && !added.Any(x => x.Entity is InvoiceUnitReviewSource row && row.Id == source.Id)
                || decision is not null && !UnitReviewInsertedInCurrentTransaction("decisions", decision.Id))
                throw new InvalidOperationException("InvoiceUnitReviewReceiptEffectRequired");
        }
        foreach (var outcome in added.Select(x => x.Entity).OfType<InvoiceUnitReviewOutcome>())
        {
            var final = core.Decisions.Single(x => x.Id == outcome.FinalDecisionId);
            if (added.Count(x => x.Entity is InvoiceUnitReviewCommandReceipt row && row.Operation == "Decision"
                && row.ReviewSourceId == outcome.ReviewSourceId && row.CommandKey.ToString("D") == final.IdempotencyKey
                && row.ActorPrincipalId == outcome.ReviewerPrincipalId && row.ActorWorkerId == outcome.ReviewerWorkerId) != 1)
                throw new InvalidOperationException("InvoiceUnitReviewTerminalReceiptRequired");
        }
    }
    private bool UnitReviewInsertedInCurrentTransaction(string table, Guid id)
    {
        if (table is not ("cases" or "decisions")) throw new InvalidOperationException("InvoiceUnitReviewCoreTableInvalid");
        using var command = Database.GetDbConnection().CreateCommand();
        command.Transaction = Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM core_approval." + table + " WHERE id=@id AND xmin::text::bigint=(txid_current()%4294967296))";
        var parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.DbType = System.Data.DbType.Guid; parameter.Value = id; command.Parameters.Add(parameter);
        return command.ExecuteScalar() is true;
    }
    private void VerifyUnitReviewPolicyIdentity(PolicyVersion policy)
    {
        var created = OperationalPolicyIdentityStorage.Read(OperationalPolicyIdentityRecords.Single(x => x.PolicyVersionId == policy.Id && x.Stage == "Created"));
        var published = OperationalPolicyIdentityStorage.Read(OperationalPolicyIdentityRecords.Single(x => x.PolicyVersionId == policy.Id && x.Stage == "Published"));
        var submitted = OperationalPolicyIdentityStorage.Read(OperationalPolicyIdentityRecords.Single(x => x.Id == published.SubmittedEvidenceId));
        OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, published);
    }
    private sealed record UnitReviewApprovalScope(string TenantId, Guid LegalEntityId, string SubjectId) : IApprovalScope;
}
