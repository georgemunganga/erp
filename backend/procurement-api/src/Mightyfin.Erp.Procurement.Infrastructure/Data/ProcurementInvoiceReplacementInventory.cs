using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>A separate complete-set supplement. It never changes the approved
/// 21-table baseline or accepts a broad old-table exclusion.</summary>
public sealed partial class ProcurementDbContext
{
    public async Task<(InvoiceReplacementLink Link, InvoiceReplacementCommandReceipt Receipt)?>
        ReadVerifiedInvoiceReplacementAsync(Guid invoiceId,
            Func<InvoiceAmountCoreReferenceRequirement, CancellationToken, Task<bool>> verifyCore,
            CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable || invoiceId == Guid.Empty)
            throw new InvalidOperationException("InvoiceReplacementReadTransactionRequired");
        var linkRelation = await RelationState("procurement.invoice_replacement_links", ct);
        var receiptRelation = await RelationState("procurement.invoice_replacement_command_receipts", ct);
        if (linkRelation != receiptRelation || linkRelation is not (0 or 1))
            throw new InvalidOperationException("InvoiceReplacementPhysicalRelationsIncomplete");
        if (linkRelation == 0) return null;
        var link = await InvoiceReplacementLinks.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceInvoiceId == invoiceId || x.SuccessorInvoiceId == invoiceId, ct);
        if (link is null) return null;
        _ = await CaptureInvoiceOwnerInventoryAsync(ct, verifyCore, openingNewBaseline: false);
        var receipt = await InvoiceReplacementCommandReceipts.AsNoTracking()
            .SingleAsync(x => x.LinkId == link.Id, ct);
        return (link, receipt);
    }

    public async Task VerifyInvoiceReplacementInventoryAsync(InvoiceProcurementInventoryCapture oldCapture,
        bool openingNewBaseline, CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable || oldCapture.TenantId != scope.TenantId
            || oldCapture.LegalEntityId != scope.LegalEntityId)
            throw new InvalidOperationException("InvoiceReplacementInventoryTransactionRequired");
        await Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        var linkRelation = await RelationState("procurement.invoice_replacement_links", ct);
        var receiptRelation = await RelationState("procurement.invoice_replacement_command_receipts", ct);
        if (linkRelation != receiptRelation || linkRelation is not (0 or 1))
            throw new InvalidOperationException("InvoiceReplacementPhysicalRelationsIncomplete");
        var markerAudits = await AuditEvents.AsNoTracking()
            .Where(x => x.Action == InvoiceReplacementEvidence.AuditAction).Take(100_001).ToArrayAsync(ct);
        var markerOutbox = await OutboxEvents.AsNoTracking()
            .Where(x => x.EventType == InvoiceReplacementEvidence.EventType).Take(100_001).ToArrayAsync(ct);
        if (linkRelation == 0)
        {
            if (markerAudits.Length != 0 || markerOutbox.Length != 0)
                throw new InvalidOperationException("InvoiceReplacementMarkersWithoutSchema");
            return;
        }
        var links = await InvoiceReplacementLinks.AsNoTracking().Take(100_001).ToArrayAsync(ct);
        var receipts = await InvoiceReplacementCommandReceipts.AsNoTracking().Take(100_001).ToArrayAsync(ct);
        if (links.Length > 100_000 || receipts.Length > 100_000
            || markerAudits.Length != links.Length || markerOutbox.Length != links.Length
            || receipts.Length != links.Length
            || openingNewBaseline && links.Length != 0
            || links.Select(x => x.SourceInvoiceId).Distinct().Count() != links.Length
            || links.Select(x => x.SuccessorInvoiceId).Distinct().Count() != links.Length
            || links.SelectMany(x => new[] { x.SourceInvoiceId, x.SuccessorInvoiceId })
                .Distinct().Count() != links.Length * 2
            || receipts.Select(x => x.LinkId).Distinct().Count() != receipts.Length
            || receipts.Select(x => (x.ActorPrincipalId, x.CommandKey)).Distinct().Count() != receipts.Length)
            throw new InvalidOperationException("InvoiceReplacementInventorySetChanged");
        var oldInvoices = oldCapture.Tables.Single(x => x.TableName == "supplier_invoices").Rows;
        var oldResults = oldCapture.Tables.Single(x => x.TableName == "invoice_match_results").Rows;
        foreach (var link in links)
        {
            if (oldInvoices.Count(x => x.Id == link.SourceInvoiceId) != 1
                || oldInvoices.Count(x => x.Id == link.SuccessorInvoiceId) != 1
                || oldResults.Count(x => x.Id == link.BlockedResultId) != 1)
                throw new InvalidOperationException("InvoiceReplacementOldInventoryMissing");
            var receipt = receipts.SingleOrDefault(x => x.LinkId == link.Id)
                ?? throw new InvalidOperationException("InvoiceReplacementReceiptMissing");
            var source = await SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == link.SourceInvoiceId, ct);
            var successor = await SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == link.SuccessorInvoiceId, ct);
            var mapping = await InvoiceMatchMappingVersions.AsNoTracking().SingleAsync(x => x.Id == link.SourceMappingVersionId, ct);
            var blocked = await InvoiceMatchResults.AsNoTracking().SingleAsync(x => x.Id == link.BlockedResultId, ct);
            var review = await InvoiceUnitReviewSources.AsNoTracking().SingleAsync(x => x.Id == link.ReviewSourceId, ct);
            var outcome = await InvoiceUnitReviewOutcomes.AsNoTracking().SingleAsync(x => x.Id == link.ReviewOutcomeId, ct);
            var calculation = await InvoiceAmountCalculations.AsNoTracking().SingleAsync(x => x.Id == link.CalculationId, ct);
            var capture = await InvoiceIntakeCommandReceipts.AsNoTracking().SingleAsync(x => x.Id == link.CaptureReceiptId, ct);
            InvoiceReplacementEvidence.Verify(link, receipt, source, successor, mapping, blocked,
                review, outcome, calculation, capture);
            if (await InvoiceMatchAllocations.AsNoTracking().AnyAsync(x =>
                    x.SupplierInvoiceId == source.Id, ct)
                || await SupplierCreditNoteRefs.AsNoTracking().AnyAsync(x =>
                    x.SupplierInvoiceId == source.Id, ct)
                || await FinanceInvoiceHandoffs.AsNoTracking().AnyAsync(x =>
                    x.SupplierInvoiceId == source.Id, ct)
                || await FinancePaymentStatusRefs.AsNoTracking().AnyAsync(x =>
                    x.SupplierInvoiceId == source.Id, ct)
                || await ManualPaymentRequests.AsNoTracking().AnyAsync(x =>
                    x.SupplierInvoiceId == source.Id, ct))
                throw new InvalidOperationException("InvoiceReplacementSourceEffectChanged");
            var allocations = await InvoiceMatchAllocations.AsNoTracking()
                .Where(x => x.PurchaseOrderId == link.PurchaseOrderId).ToArrayAsync(ct);
            var admitted = await ReadBlockedNoClaimResultsAsync(oldCapture, link.PurchaseOrderId,
                allocations.Select(x => x.MatchResultId).ToHashSet(), ct);
            var exceptionIds = await InvoiceExceptions.AsNoTracking()
                .Where(x => x.MatchResultId == blocked.Id).Select(x => x.Id).ToArrayAsync(ct);
            if (!admitted.Results.Contains(blocked.Id) || exceptionIds.Length != 1
                || !admitted.Exceptions.Contains(exceptionIds[0])
                || link.NoClaimProofSha256 != ReplacementNoClaimDigest(
                    source, mapping, blocked, review, outcome, exceptionIds))
                throw new InvalidOperationException("InvoiceReplacementNoClaimProofChanged");
            var marker = InvoiceReplacementEvidence.MarkerJson(link);
            if (markerAudits.Count(x => x.RecordType == nameof(InvoiceReplacementLink)
                    && x.RecordId == link.Id && x.ActorSubjectId == link.MakerPrincipalId
                    && x.CorrelationId == link.CommandKey.ToString("D")
                    && ProcurementEvidenceEncoding.PolicyJsonDigest(x.ChangedFieldsJson)
                        == ProcurementEvidenceEncoding.PolicyJsonDigest(marker)) != 1
                || markerOutbox.Count(x => x.AggregateType == nameof(SupplierInvoice)
                    && x.AggregateId == source.Id && x.EventVersion == 1
                    && x.IdempotencyKey == link.CommandKey.ToString("D")
                    && ProcurementEvidenceEncoding.PolicyJsonDigest(x.PayloadJson)
                        == ProcurementEvidenceEncoding.PolicyJsonDigest(marker)) != 1)
                throw new InvalidOperationException("InvoiceReplacementMarkersChanged");
        }
    }

    private async Task<int> RelationState(string name, CancellationToken ct)
    {
        await using var command = Database.GetDbConnection().CreateCommand();
        command.Transaction = Database.CurrentTransaction!.GetDbTransaction();
        command.CommandTimeout = 15;
        command.CommandText = "SELECT CASE WHEN to_regclass(@name) IS NULL THEN 0 "
            + "WHEN EXISTS(SELECT 1 FROM pg_class WHERE oid=to_regclass(@name) "
            + "AND relkind='r' AND relpersistence='p' AND NOT relrowsecurity AND NOT relforcerowsecurity) "
            + "THEN 1 ELSE -1 END";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "name"; parameter.DbType = DbType.String; parameter.Value = name;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
