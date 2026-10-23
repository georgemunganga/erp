using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentRoutes
{
    private sealed record PaymentInvoiceLabel(Guid InvoiceId, Guid SupplierId, Guid? BranchId,
        string InvoiceNumber, string SupplierDisplayName, string CurrencyCode,
        decimal GrossAmount, DateOnly InvoiceDate);

    // A small, purpose-specific label. Payment readers do not need the wider supplier
    // profile or invoice detail permissions to understand their own payment records.
    private static async Task<Dictionary<Guid, PaymentInvoiceLabel>> PaymentInvoiceLabels(
        ProcurementDbContext db, ProcurementRequestScope scope,
        ManualPaymentRequest[] requests, CancellationToken ct)
    {
        if (requests.Length == 0) return new Dictionary<Guid, PaymentInvoiceLabel>();
        var ids = requests.Select(x => x.SupplierInvoiceId).Distinct().ToArray();
        var labels = await (from invoice in db.SupplierInvoices.AsNoTracking()
            join supplier in db.Suppliers.AsNoTracking() on new
            { invoice.TenantId, invoice.LegalEntityId, Id = invoice.SupplierId }
                equals new { supplier.TenantId, supplier.LegalEntityId, supplier.Id }
            where invoice.TenantId == scope.TenantId && invoice.LegalEntityId == scope.LegalEntityId
                && supplier.TenantId == scope.TenantId && supplier.LegalEntityId == scope.LegalEntityId
                && ids.Contains(invoice.Id)
            select new PaymentInvoiceLabel(invoice.Id, supplier.Id, invoice.BranchRef,
                invoice.InvoiceNumber, supplier.DisplayName, invoice.CurrencyCode,
                invoice.GrossAmount, invoice.InvoiceDate)).ToArrayAsync(ct);
        var byId = labels.ToDictionary(x => x.InvoiceId);
        if (byId.Count != ids.Length || requests.Any(x => !byId.TryGetValue(x.SupplierInvoiceId, out var label)
            || label.BranchId != x.BranchId || label.SupplierId != x.SupplierId
            || label.CurrencyCode != x.CurrencyCode))
            throw new ReadProblem("ManualPaymentInvoiceLabelUnavailable", 503);
        return byId;
    }

    // Picker values are previews only. Create locks this invoice and rechecks current
    // capacity, credits, Finance-owned settlement evidence and branch authorization.
    private static async Task<IResult> InvoiceLookup(Guid? branchId, Guid? invoiceId, string? q, string? cursor, int? limit,
        HttpContext http, ProcurementRequestScope scope, ProcurementApprovalTransaction transaction,
        IAuthorizationService authorization, CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (db, _, core, actor, _, token) =>
        {
            var search = q?.Trim() ?? "";
            var size = limit ?? 25;
            var offset = 0;
            if (branchId is not { } branch || branch == Guid.Empty || invoiceId == Guid.Empty || size is < 1 or > 50
                || search.Length > 100 || search.Any(char.IsControl)
                || cursor is { Length: > 12 } || cursor is not null &&
                    (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out offset)
                        || offset is < 0 or > 100000))
                throw new ArgumentException("ManualPaymentInvoiceLookupInvalid");
            if (offset > 100000 - size) throw new ArgumentException("ManualPaymentInvoiceLookupInvalid");

            // The read grant alone never reveals candidate invoices. A current draft
            // action is required, then the explicit target branch is checked through Core.
            var canDraft = false;
            foreach (var action in new[] { "procurement-payment-create", "procurement-payment-edit", "procurement-payment-submit" })
                if (actor.Permissions.Contains(action)
                    && (await authorization.AuthorizeAsync(http.User, null, action)).Succeeded)
                { canDraft = true; break; }
            if (!canDraft || scope.CoreLinkedWorkerId != actor.CoreLinkedWorkerId)
                throw new UnauthorizedAccessException("ManualPaymentDraftActionRequired");
            if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null)
                throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");
            if (!await core.WorkLocations.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && x.Id == branch && !x.IsArchived, token))
                throw new KeyNotFoundException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, token);
            if (!actor.OrganisationWide && !actor.AllowedBranches.Contains(branch))
                throw new KeyNotFoundException();

            var query = from invoice in db.SupplierInvoices.AsNoTracking()
                join supplier in db.Suppliers.AsNoTracking() on new
                { invoice.TenantId, invoice.LegalEntityId, Id = invoice.SupplierId }
                    equals new { supplier.TenantId, supplier.LegalEntityId, supplier.Id }
                where invoice.TenantId == scope.TenantId && invoice.LegalEntityId == scope.LegalEntityId
                    && supplier.TenantId == scope.TenantId && supplier.LegalEntityId == scope.LegalEntityId
                    && invoice.BranchRef == branch && invoice.Status == "Received"
                    && invoice.FinancePostingRef == null && invoice.InvoiceDate != default
                    && invoice.GrossAmount > 0
                select new { invoice.Id, invoice.InvoiceNumber, invoice.SupplierId,
                    SupplierDisplayName = supplier.DisplayName, invoice.CurrencyCode,
                    invoice.GrossAmount, invoice.InvoiceDate, invoice.BranchRef };
            if (invoiceId is { } selectedInvoice) query = query.Where(x => x.Id == selectedInvoice);
            if (search.Length > 0)
            {
                var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%")
                    .Replace("_", "\\_") + "%";
                query = query.Where(x => EF.Functions.ILike(x.InvoiceNumber, pattern, "\\")
                    || EF.Functions.ILike(x.SupplierDisplayName, pattern, "\\"));
            }
            var rows = await query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Id)
                .Skip(offset).Take(size + 1).ToArrayAsync(token);
            var page = rows.Take(size).ToArray();
            return new
            {
                items = page.Select(x => new { invoiceId = x.Id, x.InvoiceNumber, x.SupplierId,
                    x.SupplierDisplayName, x.CurrencyCode,
                    grossExact = ProcurementEvidenceEncoding.Decimal(x.GrossAmount),
                    x.InvoiceDate, branchId = x.BranchRef }).ToArray(),
                nextCursor = rows.Length > size ? (offset + size).ToString(CultureInfo.InvariantCulture) : null,
                hasMore = rows.Length > size,
                previewOnly = true
            };
        }, ct);
}
