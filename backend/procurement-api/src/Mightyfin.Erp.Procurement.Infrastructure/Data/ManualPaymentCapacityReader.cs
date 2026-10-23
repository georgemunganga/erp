using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed record LockedManualPaymentInvoice(ManualPaymentInvoiceSource Source, ManualPaymentCapacitySnapshot Capacity);

/// <summary>
/// Owning-store capacity read for a command already authenticated/authorized through Core.
/// Requires the existing serializable transaction, locks the stable invoice row before reading
/// every registered commitment/credit/settlement, and never starts or commits its own transaction.
/// The caller must persist its payment command in that SAME transaction. No route is registered.
/// </summary>
public static class ManualPaymentCapacityReader
{
    public static async Task<LockedManualPaymentInvoice> ReadLockedAsync(ProcurementDbContext db,
        IProcurementScope scope, Guid invoiceId, Guid transactionBranchId, IReadOnlySet<Guid> currentCoreAllowedBranches,
        Guid? excludingRequestId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty
            || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId) || transactionBranchId == Guid.Empty
            || !currentCoreAllowedBranches.Contains(transactionBranchId) || scope.OrgUnitId is not null
            || scope.IsConfined && scope.WorkLocationId is null
            || scope.WorkLocationId is { } selected && selected != transactionBranchId)
            throw new UnauthorizedAccessException("ManualPaymentBranchNotAuthorized");
        if (invoiceId == Guid.Empty || excludingRequestId == Guid.Empty)
            throw new ArgumentException("An exact invoice and optional current request are required.");
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
            || db.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("ManualPaymentSerializableTransactionRequired");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM procurement.supplier_invoices
            WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId}
                AND id={invoiceId} AND branch_ref={transactionBranchId} FOR UPDATE
            """, ct);
        // Explicit original intake fields; unrelated optional matching evidence is not a prerequisite.
        var invoice = await db.SupplierInvoices.AsNoTracking().Where(x => x.Id == invoiceId
            && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.BranchRef == transactionBranchId)
            .Select(x => new SupplierInvoice { Id = x.Id, TenantId = x.TenantId, LegalEntityId = x.LegalEntityId,
                SupplierId = x.SupplierId, BranchRef = x.BranchRef, CurrencyCode = x.CurrencyCode, InvoiceDate = x.InvoiceDate,
                GrossAmount = x.GrossAmount, Status = x.Status, FinancePostingRef = x.FinancePostingRef })
            .SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("ManualPaymentInvoiceNotFound");
        if (excludingRequestId is { } excluded && !await db.ManualPaymentRequests.AsNoTracking().AnyAsync(x => x.Id == excluded
            && x.SupplierInvoiceId == invoiceId && x.BranchId == transactionBranchId, ct))
            throw new InvalidOperationException("ManualPaymentSourceImmutable");
        // The authorized invoice is fixed above. Ignore only the selected-branch filter for
        // its child evidence so corrupt/mismatched history cannot disappear from the capacity
        // check. Tenant, company and exact invoice remain explicit before every materialization.
        var requests = await db.ManualPaymentRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.SupplierInvoiceId == invoiceId).ToArrayAsync(ct);
        var credits = await db.SupplierCreditNoteRefs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.SupplierInvoiceId == invoiceId).ToArrayAsync(ct);
        var settlements = await db.FinancePaymentStatusRefs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.SupplierInvoiceId == invoiceId).ToArrayAsync(ct);
        var capacity = ManualPaymentCapacity.Calculate(invoice, requests, credits, settlements, excludingRequestId);
        // Existing structured manual intake is the supported source. Future Finance-owned
        // posting states require their reconciliation adapter instead of assumed balances.
        var eligible = invoice.Status == "Received" && invoice.FinancePostingRef is null && invoice.InvoiceDate != default
            && await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == invoice.SupplierId, ct);
        return new(new(invoice.Id, invoice.TenantId, invoice.LegalEntityId, transactionBranchId,
            invoice.SupplierId, invoice.CurrencyCode, invoice.InvoiceDate, invoice.GrossAmount,
            capacity.RemainingExcludingThisRequest, eligible) { ExcludingManualPaymentRequestId = excludingRequestId }, capacity);
    }

    /// <summary>Existing credit capture/correction participates in the same registered capacity.
    /// The caller already holds the source invoice lock. Absence of the additive P27 tables
    /// preserves pre-payment CRUD; partial installation fails closed.</summary>
    public static async Task ValidateCreditClaimLockedAsync(ProcurementDbContext db, SupplierInvoice invoice,
        Guid? editingCreditId, decimal amount, CancellationToken ct = default)
    {
        if (!await ProcurementManualPaymentSchema.InstalledAsync(db, ct)) return;
        var requests = await db.ManualPaymentRequests.IgnoreQueryFilters().AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId && x.SupplierInvoiceId == invoice.Id).ToArrayAsync(ct);
        var settlements = await db.FinancePaymentStatusRefs.IgnoreQueryFilters().AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId && x.SupplierInvoiceId == invoice.Id).ToArrayAsync(ct);
        if (requests.Length == 0 && settlements.Length == 0) return; // Existing credit-only ceiling owns this path.
        if (db.Database.IsRelational() && (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
            || db.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable))
            throw new InvalidOperationException("ManualPaymentSerializableTransactionRequired");
        var credits = await db.SupplierCreditNoteRefs.IgnoreQueryFilters().AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId && x.SupplierInvoiceId == invoice.Id).ToListAsync(ct);
        if (editingCreditId is { } id && credits.RemoveAll(x => x.Id == id) != 1)
            throw new InvalidOperationException("ManualPaymentCreditHistoryNeedsReview");
        credits.Add(new SupplierCreditNoteRef { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode,
            Amount = amount, Status = "Draft" });
        _ = ManualPaymentCapacity.Calculate(invoice, requests, credits, settlements);
    }
}
