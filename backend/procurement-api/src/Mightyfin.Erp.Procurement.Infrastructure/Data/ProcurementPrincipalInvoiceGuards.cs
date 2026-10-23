using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardPrincipalInvoiceMatching()
    {
        var records = ChangeTracker.Entries<ProcurementRecord>()
            .Where(x => x.Entity is PrincipalInvoiceState or PrincipalInvoiceMappingVersion
                or PrincipalInvoiceMappingLine or PrincipalInvoiceUnitReviewSource
                or PrincipalInvoiceUnitReviewAssignment or PrincipalInvoiceUnitReviewAttestation
                or PrincipalInvoiceUnitReviewOutcome
                or PrincipalInvoiceComparison or PrincipalInvoiceExposureState
                or PrincipalInvoiceClaim or PrincipalInvoiceCommandReceipt)
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (records.Length == 0) return;
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || sharedApprovalCore?.Database.CurrentTransaction is null || sharedApprovalTransaction is null
            || !ReferenceEquals(sharedApprovalTransaction, Database.CurrentTransaction.GetDbTransaction())
            || !ReferenceEquals(sharedApprovalTransaction, sharedApprovalCore.Database.CurrentTransaction.GetDbTransaction())
            || sharedApprovalTransaction.IsolationLevel != System.Data.IsolationLevel.Serializable
            || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            || !ProcurementPrincipalEvidence.OptionalWorker(scope.CoreLinkedWorkerId))
            throw new InvalidOperationException("PrincipalInvoiceOwningTransactionRequired");
        foreach (var entry in records)
        {
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId
                || entry.State == EntityState.Deleted)
                throw new InvalidOperationException("PrincipalInvoiceScopeOrDeletionInvalid");
            if (entry.State == EntityState.Added && entry.Entity.Version != 1)
                throw new InvalidOperationException("PrincipalInvoiceInitialVersionInvalid");
            if (entry.State == EntityState.Modified && entry.Entity is not (PrincipalInvoiceState or PrincipalInvoiceExposureState))
                throw new InvalidOperationException("PrincipalInvoiceEvidenceAppendOnly");
        }
        foreach (var entry in ChangeTracker.Entries<PrincipalInvoiceState>().Where(x => x.State == EntityState.Modified))
            if (entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                ("InvoiceVersion" or "MappingRevision" or "CurrentMappingId" or "LastComparisonId"
                    or "ActiveClaimComparisonId" or "State" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("PrincipalInvoiceStateSourceImmutable");
        foreach (var entry in ChangeTracker.Entries<PrincipalInvoiceExposureState>().Where(x => x.State == EntityState.Modified))
            if (entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                ("Sequence" or "CoverageState" or "OwnerReference" or "OwnerWatermark"
                    or "OwnerEvidenceSha256" or "OwnerValidUntil" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("PrincipalInvoiceExposureSourceImmutable");

        var added = records.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        foreach (var item in ChangeTracker.Entries<PrincipalInvoiceExposureState>()
            .Where(x => x.State == EntityState.Added))
        {
            var exposure = item.Entity;
            var order = PurchaseOrders.Single(x => x.Id == exposure.PurchaseOrderId);
            var issued = PrincipalOrderIssuedVersions.Single(x => x.Id == exposure.PrincipalIssuedVersionId);
            if (exposure.Sequence != 0 || exposure.CoverageState != "Unknown"
                || exposure.OwnerReference is not null || exposure.OwnerWatermark is not null
                || exposure.OwnerEvidenceSha256 is not null || exposure.OwnerValidUntil is not null
                || issued.PurchaseOrderId != order.Id || order.BranchRef != exposure.BranchId
                || order.SupplierId != exposure.SupplierId || order.CurrencyCode != exposure.CurrencyCode
                || !added.OfType<PrincipalInvoiceMappingVersion>().Any(x =>
                    x.PurchaseOrderId == order.Id && x.PrincipalIssuedVersionId == issued.Id
                    && x.SupplierId == exposure.SupplierId && x.BranchId == exposure.BranchId
                    && x.CurrencyCode == exposure.CurrencyCode))
                throw new InvalidOperationException("PrincipalInvoiceExposureMustStartUnknownWithMapping");
        }
        foreach (var item in ChangeTracker.Entries<PrincipalInvoiceExposureState>()
            .Where(x => x.State == EntityState.Modified))
        {
            var exposure = item.Entity;
            var prior = item.OriginalValues.GetValue<long>(nameof(PrincipalInvoiceExposureState.Sequence));
            var comparison = added.OfType<PrincipalInvoiceComparison>()
                .SingleOrDefault(x => x.ExposureStateId == exposure.Id);
            if (comparison is null || exposure.Sequence != checked(prior + 1)
                || exposure.CoverageState != "Verified"
                || exposure.OwnerReference != comparison.OwnerReference
                || exposure.OwnerWatermark != comparison.OwnerWatermark
                || exposure.OwnerEvidenceSha256 is not { Length: 64 }
                || exposure.OwnerValidUntil <= comparison.ComparedAt
                || Receipt(comparison.SupplierInvoiceId, "Compare").CommandKey != comparison.CommandKey)
                throw new InvalidOperationException("PrincipalInvoiceExposureAdvanceRequiresComparedClaim");
        }
        PrincipalInvoiceState State(Guid invoiceId) => PrincipalInvoiceStates.Local.SingleOrDefault(x => x.SupplierInvoiceId == invoiceId)
            ?? PrincipalInvoiceStates.Single(x => x.SupplierInvoiceId == invoiceId);
        SupplierInvoice Invoice(Guid id) => SupplierInvoices.Local.SingleOrDefault(x => x.Id == id)
            ?? SupplierInvoices.Single(x => x.Id == id);
        PrincipalInvoiceMappingVersion Mapping(Guid id) => PrincipalInvoiceMappingVersions.Local.SingleOrDefault(x => x.Id == id)
            ?? PrincipalInvoiceMappingVersions.Single(x => x.Id == id);
        PrincipalInvoiceCommandReceipt Receipt(Guid invoiceId, string operation)
            => added.OfType<PrincipalInvoiceCommandReceipt>().Single(x => x.SupplierInvoiceId == invoiceId && x.Operation == operation);

        foreach (var item in ChangeTracker.Entries<PrincipalInvoiceState>()
            .Where(x => x.State == EntityState.Modified))
        {
            var state = item.Entity;
            var previous = item.OriginalValues;
            var originalState = previous.GetValue<string>(nameof(PrincipalInvoiceState.State));
            if (state.State == "Reviewed")
            {
                var outcome = added.OfType<PrincipalInvoiceUnitReviewOutcome>()
                    .SingleOrDefault(x => x.State == "Verified" &&
                        PrincipalInvoiceUnitReviewSources.Any(r => r.Id == x.ReviewSourceId
                            && r.SupplierInvoiceId == state.SupplierInvoiceId));
                if (originalState != "Mapped" || outcome is null
                    || Receipt(state.SupplierInvoiceId, "DecideReview").ActorPrincipalId != scope.SubjectId
                    || state.LastComparisonId is not null || state.ActiveClaimComparisonId is not null)
                    throw new InvalidOperationException("PrincipalInvoiceReviewedStateRequiresCoreOutcome");
            }
            else if (state.State == "Compared")
            {
                var compare = added.OfType<PrincipalInvoiceComparison>()
                    .SingleOrDefault(x => x.SupplierInvoiceId == state.SupplierInvoiceId);
                if (originalState != "Reviewed" || compare is null
                    || state.LastComparisonId != compare.Id || state.ActiveClaimComparisonId != compare.Id
                    || Receipt(state.SupplierInvoiceId, "Compare").CommandKey != compare.CommandKey)
                    throw new InvalidOperationException("PrincipalInvoiceComparedStateRequiresClaim");
            }
            else throw new InvalidOperationException("PrincipalInvoiceStateTransitionUnsupported");
            if (state.InvoiceVersion != previous.GetValue<long>(nameof(PrincipalInvoiceState.InvoiceVersion))
                || state.MappingRevision != previous.GetValue<int>(nameof(PrincipalInvoiceState.MappingRevision))
                || state.CurrentMappingId != previous.GetValue<Guid?>(nameof(PrincipalInvoiceState.CurrentMappingId)))
                throw new InvalidOperationException("PrincipalInvoiceStateOwnerChanged");
        }

        foreach (var state in added.OfType<PrincipalInvoiceState>())
        {
            var invoice = Invoice(state.SupplierInvoiceId);
            var firstMapping = added.OfType<PrincipalInvoiceMappingVersion>()
                .SingleOrDefault(x => x.SupplierInvoiceId == invoice.Id);
            if (state.BranchId != invoice.BranchRef || state.SupplierId != invoice.SupplierId
                || state.CurrencyCode != invoice.CurrencyCode || state.InvoiceVersion != invoice.Version
                || (firstMapping is null
                    ? state.MappingRevision != 0 || state.CurrentMappingId is not null || state.State != "Unmapped"
                    : state.MappingRevision != 1 || state.CurrentMappingId != firstMapping.Id || state.State != "Mapped")
                || state.LastComparisonId is not null || state.ActiveClaimComparisonId is not null
                )
                throw new InvalidOperationException("PrincipalInvoiceStateOriginalRequired");
        }
        foreach (var mapping in added.OfType<PrincipalInvoiceMappingVersion>())
        {
            var invoice = Invoice(mapping.SupplierInvoiceId);
            var state = State(invoice.Id);
            var rows = added.OfType<PrincipalInvoiceMappingLine>().Where(x => x.MappingVersionId == mapping.Id).ToArray();
            var originals = SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArray();
            var issued = PrincipalOrderIssuedVersions.Single(x => x.Id == mapping.PrincipalIssuedVersionId);
            var lines = PurchaseOrderLines.Where(x => x.PurchaseOrderId == mapping.PurchaseOrderId && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArray();
            PrincipalInvoiceMappingEvidence.VerifyRows(mapping, rows);
            if (mapping.MakerPrincipalId != scope.SubjectId || mapping.MakerWorkerAliasId != scope.CoreLinkedWorkerId
                || mapping.InvoiceVersion != invoice.Version || mapping.MappingRevision != state.MappingRevision
                || state.CurrentMappingId != mapping.Id || mapping.OriginalInvoiceSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, originals)
                || mapping.MappingSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(mapping.MappingJson)
                || mapping.PurchaseOrderId != issued.PurchaseOrderId || mapping.IssuedSnapshotSha256 != issued.CommercialSnapshotSha256
                || rows.Length != originals.Length || rows.Length is < 1 or > 200
                || rows.Select(x => x.SupplierInvoiceLineId).Distinct().Count() != rows.Length
                || !rows.Select(x => x.SupplierInvoiceLineId).Order().SequenceEqual(originals.Select(x => x.Id).Order())
                || rows.Any(x => x.SupplierInvoiceId != invoice.Id || x.PurchaseOrderId != mapping.PurchaseOrderId
                    || x.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
                    || !lines.Any(line => line.Id == x.PurchaseOrderLineId))
                || Receipt(invoice.Id, "Map").ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("PrincipalInvoiceMappingSameSaveRequired");
        }
        foreach (var line in added.OfType<PrincipalInvoiceMappingLine>())
            if (!added.OfType<PrincipalInvoiceMappingVersion>().Any(x => x.Id == line.MappingVersionId)
                || line.PrincipalAcceptanceFactId.HasValue != line.PrincipalReceivingSourceStateId.HasValue
                || line.PrincipalAcceptanceFactId.HasValue != line.AcceptanceSequence.HasValue
                || line.PrincipalAcceptanceFactId is { } factId &&
                    !PrincipalReceivingFacts.Any(x => x.Id == factId
                        && x.SourceStateId == line.PrincipalReceivingSourceStateId
                        && x.Sequence == line.AcceptanceSequence && x.Kind == "Acceptance"
                        && (x.Operation == "Accept" || x.Operation == "Certify")))
                throw new InvalidOperationException("PrincipalInvoiceMappingLineRequiresHeader");

        foreach (var review in added.OfType<PrincipalInvoiceUnitReviewSource>())
        {
            var mapping = Mapping(review.MappingVersionId);
            var pins = added.OfType<PrincipalInvoiceUnitReviewAssignment>()
                .Where(x => x.ReviewSourceId == review.Id).ToArray();
            var coreCase = sharedApprovalCore.Cases.Local.Single(x => x.Id == review.CoreCaseId);
            _ = PrincipalInvoiceUnitReviewTransitions.Verify(review, mapping, coreCase, pins);
            if (review.MakerPrincipalId != scope.SubjectId || review.MakerWorkerAliasId != scope.CoreLinkedWorkerId
                || review.SupplierInvoiceId != mapping.SupplierInvoiceId
                || review.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
                || review.MappingSha256 != mapping.MappingSha256
                || review.OriginalInvoiceSha256 != mapping.OriginalInvoiceSha256
                || review.OriginalUnitsSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(review.OriginalUnitsJson)
                || State(review.SupplierInvoiceId).CurrentMappingId != mapping.Id
                || pins.Length == 0
                || Receipt(review.SupplierInvoiceId, "OpenReview").ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("PrincipalInvoiceReviewOpeningRequired");
        }
        foreach (var assignment in added.OfType<PrincipalInvoiceUnitReviewAssignment>())
            if (!added.OfType<PrincipalInvoiceUnitReviewSource>().Any(x => x.Id == assignment.ReviewSourceId && x.CoreCaseId == assignment.CoreCaseId))
                throw new InvalidOperationException("PrincipalInvoiceReviewAssignmentOwnerRequired");
        foreach (var attestation in added.OfType<PrincipalInvoiceUnitReviewAttestation>())
        {
            var source = PrincipalInvoiceUnitReviewSources.Single(x => x.Id == attestation.ReviewSourceId);
            var pin = PrincipalInvoiceUnitReviewAssignments.Single(x => x.ReviewSourceId == source.Id
                && x.CoreAssignmentId == attestation.CoreAssignmentId);
            var decision = sharedApprovalCore.Decisions.Local.SingleOrDefault(x => x.Id == attestation.CoreDecisionId)
                ?? sharedApprovalCore.Decisions.Single(x => x.Id == attestation.CoreDecisionId);
            if (attestation.CoreCaseId != source.CoreCaseId
                || attestation.ReviewerPrincipalId != scope.SubjectId
                || attestation.ReviewerWorkerAliasId != scope.CoreLinkedWorkerId
                || pin.ReviewerPrincipalId != scope.SubjectId
                || pin.ReviewerWorkerAliasId != scope.CoreLinkedWorkerId
                || attestation.OriginalUnitsSha256 != source.OriginalUnitsSha256
                || attestation.Statement != InvoiceUnitReviewCommands.OriginalUnitAttestation
                || decision.CaseId != source.CoreCaseId || decision.AssignmentId != pin.CoreAssignmentId
                || decision.ActorSubjectId != scope.SubjectId || decision.Action != "Approve"
                || decision.DecidedAt != attestation.AttestedAt
                || Receipt(source.SupplierInvoiceId, "DecideReview").ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("PrincipalInvoiceOriginalUnitAttestationRequired");
        }
        foreach (var outcome in added.OfType<PrincipalInvoiceUnitReviewOutcome>())
        {
            var review = PrincipalInvoiceUnitReviewSources.Single(x => x.Id == outcome.ReviewSourceId);
            if (outcome.CoreCaseId != review.CoreCaseId || outcome.ReviewerPrincipalId != scope.SubjectId
                || outcome.ReviewerWorkerAliasId != scope.CoreLinkedWorkerId
                || outcome.EvidenceSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(outcome.EvidenceJson)
                || outcome.CompletedAt < review.OpenedAt
                || Receipt(review.SupplierInvoiceId, "DecideReview").ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("PrincipalInvoiceReviewDecisionRequired");
        }
        foreach (var comparison in added.OfType<PrincipalInvoiceComparison>())
        {
            var mapping = Mapping(comparison.MappingVersionId);
            var review = PrincipalInvoiceUnitReviewOutcomes.Single(x => x.Id == comparison.ReviewOutcomeId);
            var reviewSource = PrincipalInvoiceUnitReviewSources.Single(x => x.Id == comparison.ReviewSourceId);
            var exposure = PrincipalInvoiceExposureStates.Local.SingleOrDefault(x => x.Id == comparison.ExposureStateId)
                ?? PrincipalInvoiceExposureStates.Single(x => x.Id == comparison.ExposureStateId);
            var state = State(comparison.SupplierInvoiceId);
            if (comparison.ActorPrincipalId != scope.SubjectId || comparison.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || comparison.SupplierInvoiceId != mapping.SupplierInvoiceId
                || comparison.PurchaseOrderId != mapping.PurchaseOrderId
                || comparison.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
                || review.ReviewSourceId != reviewSource.Id
                || reviewSource.MappingVersionId != mapping.Id
                || reviewSource.SupplierInvoiceId != comparison.SupplierInvoiceId
                || comparison.InvoiceVersion != mapping.InvoiceVersion
                || comparison.OriginalInvoiceSha256 != mapping.OriginalInvoiceSha256
                || comparison.EvidenceSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(comparison.EvidenceJson)
                || review.State != "Verified" || state.LastComparisonId != comparison.Id
                || exposure.PurchaseOrderId != comparison.PurchaseOrderId
                || exposure.PrincipalIssuedVersionId != comparison.PrincipalIssuedVersionId
                || comparison.OwnerReference != exposure.OwnerReference
                || comparison.OwnerWatermark != exposure.OwnerWatermark
                || comparison.Outcome != "Compared" || (state.ActiveClaimComparisonId != comparison.Id
                    || exposure.CoverageState != "Verified" || exposure.OwnerValidUntil <= comparison.ComparedAt
                    || !added.OfType<PrincipalInvoiceClaim>().Any(x => x.ComparisonId == comparison.Id))
                || Receipt(comparison.SupplierInvoiceId, "Compare").ActorPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("PrincipalInvoiceComparisonOwnerRequired");
        }
        foreach (var claim in added.OfType<PrincipalInvoiceClaim>())
        {
            var comparison = added.OfType<PrincipalInvoiceComparison>().SingleOrDefault(x => x.Id == claim.ComparisonId);
            var mappingLine = PrincipalInvoiceMappingLines.SingleOrDefault(x => x.MappingVersionId == claim.MappingVersionId
                && x.SupplierInvoiceLineId == claim.SupplierInvoiceLineId);
            if (comparison is null || comparison.Outcome != "Compared"
                || claim.SupplierInvoiceId != comparison.SupplierInvoiceId
                || claim.MappingVersionId != comparison.MappingVersionId
                || claim.PurchaseOrderId != comparison.PurchaseOrderId
                || claim.PrincipalIssuedVersionId != comparison.PrincipalIssuedVersionId
                || claim.OwnerCoverageReference != comparison.OwnerReference
                || mappingLine is null || mappingLine.PurchaseOrderLineId != claim.PurchaseOrderLineId
                || mappingLine.PrincipalReceivingSourceStateId != claim.PrincipalReceivingSourceStateId
                || mappingLine.PrincipalAcceptanceFactId != claim.PrincipalAcceptanceFactId
                || mappingLine.AcceptanceSequence != claim.AcceptanceSequence
                || claim.PrincipalAcceptanceFactId.HasValue != claim.PrincipalReceivingSourceStateId.HasValue
                || claim.PrincipalAcceptanceFactId.HasValue != claim.AcceptanceSequence.HasValue
                || claim.Quantity <= 0 || claim.NetAmount < 0 || claim.TaxAmount < 0)
                throw new InvalidOperationException("PrincipalInvoiceClaimAuthorityRequired");
        }
        foreach (var receipt in added.OfType<PrincipalInvoiceCommandReceipt>())
        {
            var invoice = Invoice(receipt.SupplierInvoiceId);
            if (receipt.ActorPrincipalId != scope.SubjectId || receipt.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || receipt.BranchId != invoice.BranchRef || receipt.CommandKey == Guid.Empty
                || receipt.RequestSha256 is not { Length: 64 } || receipt.ResponseStatus != 200
                || receipt.CompletedAt == default
                || receipt.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson)
                || !added.Any(x => receipt.Operation switch
                {
                    "Map" => x is PrincipalInvoiceMappingVersion map && map.SupplierInvoiceId == invoice.Id,
                    "OpenReview" => x is PrincipalInvoiceUnitReviewSource review && review.SupplierInvoiceId == invoice.Id,
                    "DecideReview" => x is PrincipalInvoiceUnitReviewAttestation attestation
                        && PrincipalInvoiceUnitReviewSources.Any(review => review.Id == attestation.ReviewSourceId && review.SupplierInvoiceId == invoice.Id)
                        || x is PrincipalInvoiceUnitReviewOutcome outcome
                        && PrincipalInvoiceUnitReviewSources.Any(review => review.Id == outcome.ReviewSourceId && review.SupplierInvoiceId == invoice.Id),
                    "Compare" => x is PrincipalInvoiceComparison result && result.SupplierInvoiceId == invoice.Id,
                    _ => false,
                }))
                throw new InvalidOperationException("PrincipalInvoiceCommandReceiptEffectRequired");
            using var parsed = JsonDocument.Parse(receipt.ResponseJson);
            var response = parsed.RootElement;
            if (response.GetProperty("invoiceId").GetGuid() != invoice.Id
                || response.GetProperty("commandKey").GetGuid() != receipt.CommandKey
                || response.GetProperty("sourceSchemaVersion").GetInt32() != 2
                || response.GetProperty("canPostToFinance").GetBoolean())
                throw new InvalidOperationException("PrincipalInvoiceCommandReceiptResponseChanged");
            if (receipt.Operation == "Map")
            {
                var map = added.OfType<PrincipalInvoiceMappingVersion>()
                    .Single(x => x.SupplierInvoiceId == invoice.Id);
                if (response.GetProperty("mappingVersionId").GetGuid() != map.Id
                    || receipt.CompletedAt != map.MappedAt)
                    throw new InvalidOperationException("PrincipalInvoiceMappingReceiptChanged");
            }
            else if (receipt.Operation == "OpenReview")
            {
                var review = added.OfType<PrincipalInvoiceUnitReviewSource>()
                    .Single(x => x.SupplierInvoiceId == invoice.Id);
                if (response.GetProperty("reviewId").GetGuid() != review.Id
                    || response.GetProperty("coreCaseId").GetGuid() != review.CoreCaseId
                    || receipt.CompletedAt != review.OpenedAt)
                    throw new InvalidOperationException("PrincipalInvoiceOpeningReceiptChanged");
            }
            else if (receipt.Operation == "DecideReview")
            {
                var reviewId = response.GetProperty("reviewId").GetGuid();
                var voteId = response.GetProperty("decisionId").GetGuid();
                var vote = sharedApprovalCore.Decisions.Local.SingleOrDefault(x => x.Id == voteId)
                    ?? sharedApprovalCore.Decisions.Single(x => x.Id == voteId);
                if (!PrincipalInvoiceUnitReviewSources.Any(x => x.Id == reviewId
                        && x.SupplierInvoiceId == invoice.Id && x.CoreCaseId == vote.CaseId)
                    || vote.ActorSubjectId != scope.SubjectId || receipt.CompletedAt != vote.DecidedAt)
                    throw new InvalidOperationException("PrincipalInvoiceDecisionReceiptChanged");
            }
            else if (receipt.Operation == "Compare")
            {
                var comparison = added.OfType<PrincipalInvoiceComparison>()
                    .Single(x => x.SupplierInvoiceId == invoice.Id);
                if (response.GetProperty("comparisonId").GetGuid() != comparison.Id
                    || response.GetProperty("mappingVersionId").GetGuid() != comparison.MappingVersionId
                    || receipt.CommandKey != comparison.CommandKey
                    || receipt.CompletedAt != comparison.ComparedAt)
                    throw new InvalidOperationException("PrincipalInvoiceComparisonReceiptChanged");
            }
        }
    }
}
