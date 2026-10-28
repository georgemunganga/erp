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

    private static InvalidOperationException Invalid()
        => new("PrincipalInvoiceRetainedIssuedWitnessUnavailable");
}
