using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

internal static class ProcurementRfqCommercialSchema
{
    private const string CountSql = """
        SELECT count(*)::integer AS "Value" FROM pg_tables WHERE schemaname='procurement'
        AND tablename IN ('principal_rfq_commercial_round_openings',
          'principal_rfq_commercial_round_votes',
          'principal_rfq_commercial_round_decisions',
          'principal_rfq_commercial_offer_submissions',
          'principal_rfq_commercial_round_seals',
          'principal_rfq_commercial_command_receipts',
          'principal_rfq_commercial_offer_openings',
          'principal_rfq_commercial_offer_opening_receipts')
        """;

    internal static bool Installed(ProcurementDbContext db)
    {
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return true;
        return Complete(db.Database.SqlQueryRaw<int>(CountSql).Single());
    }

    internal static bool Complete(int count) => count switch
    {
        0 => false, // Existing P30 quotation Drafts remain usable before P32.
        8 => true,
        _ => throw new InvalidOperationException("RfqCommercialSchemaIncomplete"),
    };
}

public sealed partial class ProcurementDbContext
{
    /// <summary>Same-store bid fence. A retained sealed offer makes its source
    /// Draft immutable; an original-key command recovery remains read-only.</summary>
    public bool HasSealedCommercialOffer(Guid bidId)
    {
        if (bidId == Guid.Empty) return false;
        if (PrincipalRfqCommercialOfferSubmissions.Local.Any(x => x.SupplierBidId == bidId
            && Entry(x).State != EntityState.Deleted)) return true;
        if (!ProcurementRfqCommercialSchema.Installed(this)) return false;
        return PrincipalRfqCommercialOfferSubmissions.IgnoreQueryFilters().Any(x =>
            x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
            && x.SupplierBidId == bidId);
    }

    private void GuardSealedCommercialQuoteDrafts()
    {
        var changed = ChangeTracker.Entries<ProcurementRecord>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => x.Entity switch
            {
                SupplierBid bid when x.State != EntityState.Added => bid.Id,
                SupplierBidLine line => line.SupplierBidId,
                SourcingManualQuoteVersion version => version.SupplierBidId,
                _ => Guid.Empty,
            }).Where(x => x != Guid.Empty).Distinct().ToArray();
        foreach (var bidId in changed)
            if (HasSealedCommercialOffer(bidId))
                throw new InvalidOperationException("RfqCommercialSealedQuoteImmutable");
    }
}
