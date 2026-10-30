using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Classify only closed, recorded no-claim comparisons in the current
/// owner transaction. Every other historical result remains unclassified.</summary>
public sealed partial class ProcurementDbContext
{
    private async Task<(IReadOnlySet<Guid> Results, IReadOnlySet<Guid> Exceptions)>
        ReadBlockedNoClaimResultsAsync(InvoiceProcurementInventoryCapture capture,
            Guid purchaseOrderId, IReadOnlySet<Guid> activeResultIds, CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || purchaseOrderId == Guid.Empty)
            throw new InvalidOperationException("PrincipalInvoiceBlockedProofTransactionRequired");
        var captured = capture.Tables.Single(x => x.TableName == "invoice_match_results").Rows;
        var all = await InvoiceMatchResults.AsNoTracking().ToArrayAsync(ct);
        if (!captured.Select(x => x.Id).Order().SequenceEqual(all.Select(x => x.Id).Order())
            || all.Any(x => x.TenantId != scope.TenantId || x.LegalEntityId != scope.LegalEntityId))
            throw new InvalidOperationException("PrincipalInvoiceBlockedResultSetIncomplete");
        var blocked = all.Where(x => x.PurchaseOrderId == purchaseOrderId
            && !activeResultIds.Contains(x.Id)).ToArray();
        var targetResults = all.Where(x => x.PurchaseOrderId == purchaseOrderId).ToArray();
        var targetInvoiceIds = (await SupplierInvoices.AsNoTracking()
                .Where(x => x.PurchaseOrderId == purchaseOrderId).Select(x => x.Id).ToArrayAsync(ct))
            .Concat(await InvoiceMatchMappingVersions.AsNoTracking()
                .Where(x => x.PurchaseOrderId == purchaseOrderId).Select(x => x.SupplierInvoiceId).ToArrayAsync(ct))
            .Concat(targetResults.Select(x => x.SupplierInvoiceId)).Distinct().ToArray();
        // A saved Compare receipt with no corresponding target result is an
        // unclassified effect, even when no historical result needs admission.
        var targetCompareReceipts = await CommercialCommandReceipts.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId && x.Operation == "InvoiceCompare")
            .ToArrayAsync(ct);
        if (targetResults.Any(x => x.CommandKey is null || x.CommandKey == Guid.Empty)
            || targetResults.Select(x => x.CommandKey).Distinct().Count() != targetResults.Length
            || targetCompareReceipts.Length != targetResults.Length
            || !targetCompareReceipts.Select(x => x.CommandKey).Order()
                .SequenceEqual(targetResults.Select(x => x.CommandKey!.Value).Order()))
            throw new InvalidOperationException("PrincipalInvoiceTargetCompareReceiptSetIncomplete");
        var targetKeyTexts = targetResults.Select(x => x.CommandKey!.Value.ToString("D")).ToArray();
        var targetCompareAudits = await AuditEvents.AsNoTracking().Where(x =>
            x.RecordType == "SupplierInvoice" && x.Action == "PrincipalInvoiceCompared"
            && targetInvoiceIds.Contains(x.RecordId)).ToArrayAsync(ct);
        var targetCompareOutbox = await OutboxEvents.AsNoTracking().Where(x =>
            x.AggregateType == "SupplierInvoice" && x.EventType == "supplier_invoice.compared.v3"
            && targetInvoiceIds.Contains(x.AggregateId)).ToArrayAsync(ct);
        if (targetCompareAudits.Length != targetResults.Length
            || targetCompareOutbox.Length != targetResults.Length
            || !targetCompareAudits.Select(x => x.CorrelationId).Order()
                .SequenceEqual(targetKeyTexts.Order())
            || !targetCompareOutbox.Select(x => x.IdempotencyKey).Order()
                .SequenceEqual(targetKeyTexts.Order()))
            throw new InvalidOperationException("PrincipalInvoiceTargetCompareMarkerSetIncomplete");
        if (blocked.Length == 0) return (new HashSet<Guid>(), new HashSet<Guid>());
        if (blocked.Length > 200 || blocked.Any(x => x.CommandKey is null || x.PolicyVersionId is null))
            throw new InvalidOperationException("PrincipalInvoiceBlockedResultSetUnclassifiable");
        var ids = blocked.Select(x => x.Id).ToArray();
        var keys = blocked.Select(x => x.CommandKey!.Value).ToArray();
        var keyTexts = keys.Select(x => x.ToString("D")).ToArray();
        if (ids.Distinct().Count() != ids.Length || keys.Distinct().Count() != keys.Length)
            throw new InvalidOperationException("PrincipalInvoiceBlockedResultSetUnclassifiable");
        var invoiceIds = blocked.Select(x => x.SupplierInvoiceId).Distinct().ToArray();
        var mappingIds = blocked.Select(x => x.MappingVersionId!.Value).Distinct().ToArray();
        var policyIds = blocked.Select(x => x.PolicyVersionId!.Value).Distinct().ToArray();
        var invoices = await SupplierInvoices.AsNoTracking().Where(x => invoiceIds.Contains(x.Id)).ToArrayAsync(ct);
        var lines = await SupplierInvoiceLines.AsNoTracking().Where(x => invoiceIds.Contains(x.SupplierInvoiceId)).ToArrayAsync(ct);
        var mappings = await InvoiceMatchMappingVersions.AsNoTracking().Where(x => mappingIds.Contains(x.Id)).ToArrayAsync(ct);
        var reviewSources = await InvoiceUnitReviewSources.AsNoTracking()
            .Where(x => mappingIds.Contains(x.MappingVersionId)).ToArrayAsync(ct);
        var reviewSourceIds = reviewSources.Select(x => x.Id).ToArray();
        var reviewOutcomes = await InvoiceUnitReviewOutcomes.AsNoTracking()
            .Where(x => reviewSourceIds.Contains(x.ReviewSourceId)).ToArrayAsync(ct);
        var policies = await PolicyVersions.AsNoTracking().Where(x => policyIds.Contains(x.Id)).ToArrayAsync(ct);
        var exceptions = await InvoiceExceptions.AsNoTracking().Where(x => ids.Contains(x.MatchResultId!.Value)).ToArrayAsync(ct);
        var allocations = await InvoiceMatchAllocations.AsNoTracking().Where(x => ids.Contains(x.MatchResultId)).ToArrayAsync(ct);
        var receipts = await CommercialCommandReceipts.AsNoTracking().Where(x => keys.Contains(x.CommandKey)).ToArrayAsync(ct);
        var audits = await AuditEvents.AsNoTracking().Where(x =>
            (x.RecordType == "InvoiceMatchResult" && ids.Contains(x.RecordId))
            || (x.Action == "PrincipalInvoiceCompared" && keyTexts.Contains(x.CorrelationId!))).ToArrayAsync(ct);
        var outbox = await OutboxEvents.AsNoTracking().Where(x => keyTexts.Contains(x.IdempotencyKey)).ToArrayAsync(ct);
        var verified = PrincipalInvoiceBlockedNoClaimEvidence.VerifyClosedFootprint(blocked,
            invoices, lines, mappings, reviewSources, reviewOutcomes,
            policies, receipts, audits, outbox, exceptions, allocations);
        var capturedExceptions = capture.Tables.Single(x => x.TableName == "invoice_exceptions").Rows;
        if (capturedExceptions.Any(x => verified.Exceptions.Contains(x.Id)
            && exceptions.All(y => y.Id != x.Id))
            || exceptions.Any(x => capturedExceptions.All(y => y.Id != x.Id)))
            throw new InvalidOperationException("PrincipalInvoiceBlockedExceptionSetIncomplete");
        return verified;
    }
}
