using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Loads every selected M5 source/fact through the current enlisted
    /// Procurement transaction. The caller must first reconstruct each expected
    /// source from the retained issued order and Core case; a stored source JSON
    /// cannot serve as its own independent expected value.</summary>
    public async Task<IReadOnlyList<PrincipalInvoiceAcceptedHistory>>
        LoadPrincipalInvoiceAcceptedSourcesAsync(
            IReadOnlyDictionary<Guid, PrincipalReceivingSource> expectedSources,
            CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || expectedSources is null || expectedSources.Count > 200
            || expectedSources.Any(x => x.Key == Guid.Empty || x.Value is null
                || x.Value.Commercial.TenantId != scope.TenantId
                || x.Value.Commercial.LegalEntityId != scope.LegalEntityId))
            throw new InvalidOperationException("PrincipalInvoiceAcceptedSourceTransactionRequired");
        // Fail if RLS could mask retained physical evidence in this transaction.
        await Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        var ids = expectedSources.Keys.ToArray();
        var states = await PrincipalReceivingSourceStates
            .Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        if (states.Length != ids.Length || states.Select(x => x.Id).Distinct().Count() != ids.Length)
            throw new InvalidOperationException("PrincipalInvoiceAcceptedSourceMissing");
        var store = new PrincipalReceivingStore(this);
        var histories = new List<PrincipalInvoiceAcceptedHistory>(states.Length);
        foreach (var state in states.OrderBy(x => x.Id))
        {
            var expected = expectedSources[state.Id];
            if (PrincipalReceivingStorageCodec.Source(state) != expected
                || (await store.SourceAsync(expected, ct)).Id != state.Id)
                throw new InvalidOperationException("PrincipalInvoiceAcceptedSourceChanged");
            var ledger = await store.LedgerAsync(state, ct);
            var facts = PrincipalReceivingFacts.Local
                .Where(x => x.SourceStateId == state.Id).OrderBy(x => x.Sequence).ToArray();
            var bindingIds = facts.Select(x => x.DraftLineBindingId).Distinct().ToArray();
            var keys = facts.Select(x => x.CommandKey).Distinct().ToArray();
            var bindings = await PrincipalReceivingDraftLineBindings.AsNoTracking()
                .Where(x => bindingIds.Contains(x.Id)).ToArrayAsync(ct);
            var receipts = await PrincipalReceivingCommandReceipts.AsNoTracking()
                .Where(x => keys.Contains(x.CommandKey)).ToArrayAsync(ct);
            var policies = ledger.RetainedPolicies.ToArray();
            histories.Add(PrincipalInvoiceAcceptedSourceProjection.Verify(
                expected, state, facts, bindings, receipts, policies));
        }
        return histories;
    }
}
