using System.Collections.ObjectModel;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Builds invoice source evidence through the existing retained-issued/Core
/// owner. Call only after CURRENT Core actor/action and branch authorization and
/// physical readiness. Historical approval is never a grant to the current actor.
/// This helper neither admits an invoice owner nor enables claims or posting.</summary>
internal static class ProcurementPrincipalInvoiceIssuedWitness
{
    internal static async Task<PrincipalInvoiceIssuedWitness> LoadAsync(
        ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        IReadOnlySet<Guid> currentCoreAllowedBranches, Guid orderId,
        Guid issuedVersionId, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        if (!db.Database.IsNpgsql() || !cases.Database.IsNpgsql()
            || transaction is null || transaction.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(db.Database.GetDbConnection(), cases.Database.GetDbConnection())
            || !ReferenceEquals(transaction, cases.Database.CurrentTransaction?.GetDbTransaction()))
            throw Invalid();

        // The finite role must fail rather than silently omit a source if an RLS
        // policy is introduced. This setting lasts only in the owned transaction.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        var retained = await ProcurementPrincipalOrderRetainedIssued.LoadAsync(
            db, cases, scope, currentCoreAllowedBranches, orderId, issuedVersionId,
            selectedLineId: null, lockSources: true, ct: ct);
        var order = retained.Order;
        if (order.TenantId != scope.TenantId || order.LegalEntityId != scope.LegalEntityId
            || order.BranchRef is not { } branch || branch == Guid.Empty
            || !currentCoreAllowedBranches.Contains(branch) || order.SupplierId == Guid.Empty
            || string.IsNullOrWhiteSpace(order.CurrencyCode)
            || retained.Issued.Id != issuedVersionId
            || retained.Issued.PurchaseOrderId != order.Id
            || retained.CoreCase.Id != retained.Issued.CoreApprovalCaseId)
            throw Invalid();

        // Inventory every physical source on the PO, including unselected lines.
        // IDs and historical kind come from owning normalized rows; their expected
        // source is rebuilt from verified issued evidence, never stored source JSON
        // or a caller-provided witness. The Infra reader replays each complete ledger.
        var states = await db.PrincipalReceivingSourceStates.AsNoTracking()
            .Where(x => x.PurchaseOrderId == order.Id)
            .OrderBy(x => x.Id).Take(201).ToArrayAsync(ct);
        if (states.Length > 200 || states.Select(x => x.Id).Distinct().Count() != states.Length)
            throw Invalid();
        var sources = new Dictionary<Guid, PrincipalReceivingSource>();
        foreach (var state in states)
        {
            if (state.Id == Guid.Empty || state.TenantId != scope.TenantId
                || state.LegalEntityId != scope.LegalEntityId || state.BranchId != branch
                || state.IssuedVersionId != retained.Issued.Id)
                throw Invalid();
            var original = retained.Sources.SingleOrDefault(x => x.OrderLine.Id == state.PurchaseOrderLineId)
                ?? throw Invalid();
            var kind = state.Kind switch
            {
                "Goods" => ReceivingKind.Goods,
                "Service" => ReceivingKind.Service,
                _ => throw Invalid()
            };
            var expected = ProcurementPrincipalReceivingSource.ProjectRetained(
                retained with { SelectedLine = original }, kind);
            sources.Add(state.Id, expected);
        }
        return new(scope.TenantId, scope.LegalEntityId, branch, order.Id,
            retained.Issued.Id, retained.CoreCase.Id, order.SupplierId, order.CurrencyCode,
            new ReadOnlyDictionary<Guid, PrincipalReceivingSource>(sources));
    }

    /// <summary>Read-only retained commercial projection for original-unit review.
    /// The caller already owns invoice/case ordering. Do not acquire upstream PR/PO
    /// locks here or require a receiving source for a two-way mapping.</summary>
    internal static async Task<InvoiceIssuedOrder> LoadReviewIssuedOrderAsync(
        ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        IReadOnlySet<Guid> currentCoreAllowedBranches, Guid orderId,
        Guid issuedVersionId, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        if (!db.Database.IsNpgsql() || !cases.Database.IsNpgsql()
            || transaction is null || transaction.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(db.Database.GetDbConnection(), cases.Database.GetDbConnection())
            || !ReferenceEquals(transaction, cases.Database.CurrentTransaction?.GetDbTransaction()))
            throw Invalid();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        var retained = await ProcurementPrincipalOrderRetainedIssued.LoadAsync(
            db, cases, scope, currentCoreAllowedBranches, orderId, issuedVersionId,
            selectedLineId: null, lockSources: false, ct: ct);
        return ProjectIssuedOrder(retained);
    }

    // Pure projection only. LoadReviewIssuedOrderAsync supplies the authoritative
    // retained proof; constructing a Result in memory never asserts Core approval.
    internal static InvoiceIssuedOrder ProjectIssuedOrder(ProcurementPrincipalOrderRetainedIssued.Result retained)
    {
        var order = retained.Order;
        var issued = retained.Issued;
        if (order.PrincipalDraftSchemaVersion != 2 || order.Status != "Issued"
            || order.Id == Guid.Empty || order.SupplierId == Guid.Empty
            || order.BranchRef is null || order.BranchRef == Guid.Empty
            || string.IsNullOrWhiteSpace(order.CurrencyCode) || issued.Id == Guid.Empty
            || issued.PurchaseOrderId != order.Id || issued.CommercialRevision != 1
            || issued.AuthorizedAt == default || string.IsNullOrWhiteSpace(issued.CommercialSnapshotSha256)
            || retained.Lines.Count is < 1 or > 100
            || retained.Lines.Select(x => x.Id).Distinct().Count() != retained.Lines.Count
            || retained.Sources.Count != retained.Lines.Count
            || !retained.Sources.Select(x => x.OrderLine.Id).Order()
                .SequenceEqual(retained.Lines.Select(x => x.Id).Order()))
            throw Invalid();
        var lines = retained.Lines.OrderBy(x => x.LineNumber).Select(line =>
        {
            var source = retained.Sources.Single(x => x.OrderLine.Id == line.Id);
            if (line.Id == Guid.Empty || line.PurchaseOrderId != order.Id
                || line.TenantId != order.TenantId || line.LegalEntityId != order.LegalEntityId
                || string.IsNullOrWhiteSpace(source.OriginalCategoryCode))
                throw Invalid();
            return new InvoiceIssuedOrderLine(line.Id, line.Version, source.OriginalCategoryCode,
                line.UnitCode, line.Quantity, line.UnitPrice, line.TaxAmount);
        }).ToArray();
        // AuthorizedAt is retained commercial authorization; supplier manual
        // acknowledgment is never recast as actual delivery/acceptance evidence.
        return new(order.Id, issued.Id, issued.CommercialRevision, order.Version,
            order.TenantId, order.LegalEntityId, order.BranchRef, order.SupplierId,
            order.CurrencyCode, "Issued", issued.CommercialSnapshotSha256,
            issued.AuthorizedAt, lines);
    }

    private static InvalidOperationException Invalid()
        => new("PrincipalInvoiceRetainedIssuedWitnessUnavailable");
}
