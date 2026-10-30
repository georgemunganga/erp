using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed record InvoiceReplacementSourceWitness(SupplierInvoice Invoice,
    InvoiceMatchMappingVersion Mapping, InvoiceMatchResult BlockedResult,
    InvoiceUnitReviewSource ReviewSource, InvoiceUnitReviewOutcome ReviewOutcome,
    string NoClaimProofSha256);

public sealed partial class ProcurementDbContext
{
    /// <summary>Reuses the complete 21-table owner read and the exact historical
    /// Blocked/no-claim classifier. It cannot infer zero effect from outcome alone.</summary>
    public async Task<InvoiceReplacementSourceWitness> ReadReplacementSourceAsync(Guid invoiceId,
        Func<InvoiceAmountCoreReferenceRequirement, CancellationToken, Task<bool>> verifyCore,
        CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable || invoiceId == Guid.Empty)
            throw new InvalidOperationException("InvoiceReplacementSerializableRequired");
        var capture = await CaptureInvoiceOwnerInventoryAsync(ct, verifyCore, openingNewBaseline: false);
        var invoices = capture.Tables.Single(x => x.TableName == "supplier_invoices").Rows;
        if (invoices.Count(x => x.Id == invoiceId) != 1)
            throw new InvalidOperationException("InvoiceReplacementSourceInventoryMissing");
        var source = await SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == invoiceId, ct);
        if (await InvoiceReplacementLinks.AsNoTracking().AnyAsync(x =>
                x.SourceInvoiceId == invoiceId || x.SuccessorInvoiceId == invoiceId, ct))
            throw new InvalidOperationException("InvoiceReplacementSourceAlreadyLinked");
        var results = await InvoiceMatchResults.AsNoTracking().Where(x => x.SupplierInvoiceId == invoiceId).ToArrayAsync(ct);
        if (results.Length is < 1 or > 200 || source.MatchRevision != results.Length
            || source.LastMatchResultId is not { } lastId
            || source.ActiveMatchClaimResultId is not null || source.FinancePostingRef is not null
            || source.CreditForInvoiceRef is not null || source.Status != "Received")
            throw new InvalidOperationException("InvoiceReplacementSourceComparisonNotClosed");
        var blocked = results.SingleOrDefault(x => x.Id == lastId)
            ?? throw new InvalidOperationException("InvoiceReplacementSourceLastResultMissing");
        if (blocked.PurchaseOrderId is not { } orderId)
            throw new InvalidOperationException("InvoiceReplacementSourceOrderMissing");
        if (await InvoiceMatchAllocations.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoiceId, ct)
            || await SupplierCreditNoteRefs.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoiceId, ct)
            || await FinanceInvoiceHandoffs.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoiceId, ct)
            || await FinancePaymentStatusRefs.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoiceId, ct)
            || await ManualPaymentRequests.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoiceId, ct))
            throw new InvalidOperationException("InvoiceReplacementSourceFinancialEffectRequiresResolution");
        var allocationResultIds = (await InvoiceMatchAllocations.AsNoTracking()
            .Where(x => x.PurchaseOrderId == orderId).Select(x => x.MatchResultId).ToArrayAsync(ct)).ToHashSet();
        var admitted = await ReadBlockedNoClaimResultsAsync(capture, orderId, allocationResultIds, ct);
        if (!admitted.Results.Contains(blocked.Id))
            throw new InvalidOperationException("InvoiceReplacementSourceNoClaimProofUnavailable");
        var mapping = await InvoiceMatchMappingVersions.AsNoTracking().SingleAsync(x =>
            x.Id == blocked.MappingVersionId && x.SupplierInvoiceId == source.Id, ct);
        var reviews = await InvoiceUnitReviewSources.AsNoTracking().Where(x =>
            x.SupplierInvoiceId == invoiceId && x.MappingVersionId == mapping.Id).ToArrayAsync(ct);
        if (reviews.Length != 1) throw new InvalidOperationException("InvoiceReplacementReviewSetChanged");
        var outcomes = await InvoiceUnitReviewOutcomes.AsNoTracking().Where(x =>
            x.ReviewSourceId == reviews[0].Id).ToArrayAsync(ct);
        if (outcomes.Length != 1 || outcomes[0].State != "Verified")
            throw new InvalidOperationException("InvoiceReplacementReviewNotVerified");
        var sourceExceptions = await InvoiceExceptions.AsNoTracking()
            .Where(x => x.MatchResultId == blocked.Id).Select(x => x.Id).ToArrayAsync(ct);
        if (sourceExceptions.Length != 1 || !admitted.Exceptions.Contains(sourceExceptions[0]))
            throw new InvalidOperationException("InvoiceReplacementSourceExceptionChanged");
        var digest = ReplacementNoClaimDigest(source, mapping, blocked,
            reviews[0], outcomes[0], sourceExceptions);
        if (digest.Length != 64) throw new InvalidOperationException("InvoiceReplacementSourceDigestUnavailable");
        return new(source, mapping, blocked, reviews[0], outcomes[0], digest);
    }
}
