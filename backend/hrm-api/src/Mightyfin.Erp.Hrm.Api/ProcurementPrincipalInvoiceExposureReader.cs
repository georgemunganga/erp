using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Procurement-owned typed capacity history. This is an internal
/// source-only adapter: no HTTP route/DI registration or capability is enabled.
/// The caller must already hold current Core action/branch permission and must
/// save any new comparison/claim in this exact serializable transaction.</summary>
internal static class ProcurementPrincipalInvoiceExposureReader
{
    internal static async Task<PrincipalInvoiceOwnedHistory> ReadLockedAsync(
        ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        IReadOnlySet<Guid> currentCoreBranches, SupplierInvoice currentInvoice,
        PrincipalInvoiceExposureState exposure, string policyRulesSha256,
        IServiceProvider services, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
            || db.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || cases.Database.CurrentTransaction is null
            || !ReferenceEquals(transaction.GetDbTransaction(), cases.Database.CurrentTransaction.GetDbTransaction())
            || scope.OrgUnitId is not null
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || currentInvoice.BranchRef is not { } branch || branch == Guid.Empty
            || !currentCoreBranches.Contains(branch)
            || scope.WorkLocationId is { } selected && selected != branch
            || currentInvoice.TenantId != scope.TenantId || currentInvoice.LegalEntityId != scope.LegalEntityId
            || exposure.TenantId != scope.TenantId || exposure.LegalEntityId != scope.LegalEntityId
            || exposure.BranchId != branch || exposure.SupplierId != currentInvoice.SupplierId
            || exposure.CurrencyCode != currentInvoice.CurrencyCode
            || policyRulesSha256 is not { Length: 64 })
            throw new InvalidOperationException("PrincipalInvoiceOwnerScopeOrTransactionInvalid");
        var orderId = exposure.PurchaseOrderId; var issuedId = exposure.PrincipalIssuedVersionId;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={orderId} AND branch_ref={branch} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.principal_invoice_exposure_states WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={exposure.Id} AND purchase_order_id={orderId} AND principal_issued_version_id={issuedId} FOR UPDATE", ct);
        var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
            currentCoreBranches, currentInvoice, orderId, issuedId, ct);
        var context = new InvoiceExposureReadContext(scope.TenantId, scope.LegalEntityId,
            branch, orderId, issuedId, exposure.SupplierId, exposure.CurrencyCode,
            policyRulesSha256, now);

        // Ignore the selected-branch query filter only after the exact owning
        // company/source lock. Hidden or corrupt sibling rows must block, not
        // disappear and turn into an apparent empty claim history.
        var comparisons = await db.PrincipalInvoiceComparisons.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId && x.PrincipalIssuedVersionId == issuedId)
            .OrderBy(x => x.Id).Take(1002).ToArrayAsync(ct);
        var claims = await db.PrincipalInvoiceClaims.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId && x.PrincipalIssuedVersionId == issuedId)
            .OrderBy(x => x.Id).Take(200002).ToArrayAsync(ct);
        if (comparisons.Length > 1000 || claims.Length > 200000)
            throw new InvalidOperationException("PrincipalInvoiceOwnerHistoryTooLargeForAtomicReview");
        var mappedInvoiceIds = await db.PrincipalInvoiceMappingVersions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId && x.PrincipalIssuedVersionId == issuedId)
            .Select(x => x.SupplierInvoiceId).Distinct().ToArrayAsync(ct);
        var legacyAllocations = await db.InvoiceMatchAllocations.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId, ct);
        var legacyMatches = await db.InvoiceMatchResults.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId, ct);
        var credits = await (from credit in db.SupplierCreditNoteRefs.IgnoreQueryFilters().AsNoTracking()
            join invoice in db.SupplierInvoices.IgnoreQueryFilters().AsNoTracking()
                on new { credit.TenantId, credit.LegalEntityId, Id = credit.SupplierInvoiceId }
                equals new { invoice.TenantId, invoice.LegalEntityId, Id = (Guid?)invoice.Id }
            where credit.TenantId == scope.TenantId && credit.LegalEntityId == scope.LegalEntityId
                && (invoice.PurchaseOrderId == orderId || mappedInvoiceIds.Contains(invoice.Id))
            select credit.Id).CountAsync(ct);
        var goods = await db.GoodsReceipts.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PurchaseOrderId == orderId, ct);
        var serviceReceiptCount = await (from accepted in db.ServiceAcceptances.IgnoreQueryFilters().AsNoTracking()
            join line in db.PurchaseOrderLines.IgnoreQueryFilters().AsNoTracking()
                on new { accepted.TenantId, accepted.LegalEntityId, Id = accepted.PurchaseOrderLineId }
                equals new { line.TenantId, line.LegalEntityId, line.Id }
            where accepted.TenantId == scope.TenantId && accepted.LegalEntityId == scope.LegalEntityId
                && line.PurchaseOrderId == orderId
            select accepted.Id).CountAsync(ct);
        var owned = new List<PrincipalInvoiceOwnedComparison>();
        foreach (var comparison in comparisons)
        {
            var invoice = await db.SupplierInvoices.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.Id == comparison.SupplierInvoiceId, ct);
            var state = await db.PrincipalInvoiceStates.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.SupplierInvoiceId == invoice.Id, ct);
            var mapping = await db.PrincipalInvoiceMappingVersions.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.Id == comparison.MappingVersionId, ct);
            var normalized = await db.PrincipalInvoiceMappingLines.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.MappingVersionId == mapping.Id).ToArrayAsync(ct);
            var original = await db.SupplierInvoiceLines.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.SupplierInvoiceId == invoice.Id && x.IsActive).ToArrayAsync(ct);
            _ = PrincipalInvoiceMappingEvidence.Verify(mapping, invoice, original, source.Order,
                source.Acceptances, source.AcceptanceSourceStates, normalized);
            var reviewOutcome = await db.PrincipalInvoiceUnitReviewOutcomes.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.Id == comparison.ReviewOutcomeId, ct);
            var review = await db.PrincipalInvoiceUnitReviewSources.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.Id == comparison.ReviewSourceId && x.Id == reviewOutcome.ReviewSourceId, ct);
            var pins = await db.PrincipalInvoiceUnitReviewAssignments.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.ReviewSourceId == review.Id).ToArrayAsync(ct);
            var attestations = await db.PrincipalInvoiceUnitReviewAttestations.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.ReviewSourceId == review.Id).ToArrayAsync(ct);
            var caseRow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleAsync(x => x.Id == review.CoreCaseId
                    && x.SourceType == PrincipalInvoiceUnitReviewTransitions.SourceType
                    && x.SourceId == review.Id, ct);
            var decisions = await cases.Decisions.Where(x => x.CaseId == caseRow.Id).ToArrayAsync(ct);
            var matching = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == review.MatchingPolicyId, ct);
            var reviewPolicy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == review.ReviewPolicyId, ct);
            if (comparison.MatchingPolicyId != matching.Id
                || comparison.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(matching.RulesJson))
                throw new InvalidOperationException("PrincipalInvoiceHistoricalComparisonPolicyChanged");
            _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope,
                matching, ct, OperationalPolicyPublicationMode.RetainedHistorical);
            _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope,
                reviewPolicy, ct, OperationalPolicyPublicationMode.RetainedHistorical);
            var opening = PrincipalInvoiceUnitReviewTransitions.Verify(review, mapping, caseRow, pins);
            await ProcurementPrincipalInvoiceDocumentUses.RequireAsync(db, scope, review, opening, ct);
            var hashes = await CurrentDocumentHashes(db, scope, services, invoice.Id,
                opening.Lines.Select(x => x.DocumentVersionId).Distinct().ToArray(), ct);
            _ = PrincipalInvoiceUnitReviewTerminal.Verify(reviewOutcome, review, mapping,
                caseRow, pins, attestations, decisions, matching, reviewPolicy, hashes, now);
            owned.Add(new(comparison, mapping, reviewOutcome, invoice, state, matching, original, normalized,
                claims.Where(x => x.ComparisonId == comparison.Id).ToArray()));
        }
        return PrincipalInvoiceExposureEvidence.Reconstruct(context, exposure, owned,
            claims.Length, legacyAllocations, legacyMatches, credits,
            checked(goods + serviceReceiptCount), now);
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> CurrentDocumentHashes(
        ProcurementDbContext db, ProcurementRequestScope scope, IServiceProvider services,
        Guid invoiceId, Guid[] ids, CancellationToken ct)
    {
        var config = services.GetRequiredService<IConfiguration>();
        if (ids.Length == 0 || ids.Any(x => x == Guid.Empty)
            || services.GetRequiredService<CoreDocumentFeature>() is not { Enabled: true }
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                config.GetConnectionString("CoreDocuments"), db.Database.GetDbConnection().ConnectionString))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentOwnerUnavailable");
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var rows = await (from version in documents.Versions
            join owner in documents.Documents on version.DocumentId equals owner.Id
            where ids.Contains(version.Id) && owner.OwnerModule == "procurement"
                && owner.OwnerType == "supplier_invoice" && owner.OwnerId == invoiceId
                && !owner.IsRetired && version.ScanState == DocumentScanState.Available
            select new { version.Id, version.Sha256 }).ToArrayAsync(ct);
        if (rows.Length != ids.Length) throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentsChanged");
        return rows.ToDictionary(x => x.Id, x => x.Sha256);
    }
}
