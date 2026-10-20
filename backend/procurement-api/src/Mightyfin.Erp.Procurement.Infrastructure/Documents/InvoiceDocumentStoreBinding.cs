using Npgsql;

namespace Mightyfin.Erp.Procurement.Infrastructure.Documents;

/// <summary>Conservative existing Core-store read binding; an ambiguous/separate owner requires its own authenticated adapter.</summary>
public static class InvoiceDocumentStoreBinding
{
    public static bool SamePhysicalDatabase(string? configured,string? executing)
    {
        if(string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(executing)) return false;
        try
        {
            var owner=new NpgsqlConnectionStringBuilder(configured); var current=new NpgsqlConnectionStringBuilder(executing);
            // Dedicated finite identities may differ; server/port/database must be explicit and identical.
            // Multi-host, host aliases and omitted database names are ambiguous: use an owning reader instead.
            return !string.IsNullOrWhiteSpace(owner.Host) && !owner.Host.Contains(',') && !owner.Host.Contains(' ')
                && !string.IsNullOrWhiteSpace(owner.Database) && !string.IsNullOrWhiteSpace(current.Database)
                && string.Equals(owner.Host,current.Host,StringComparison.Ordinal) && owner.Port==current.Port
                && string.Equals(owner.Database,current.Database,StringComparison.Ordinal);
        }
        catch(ArgumentException) { return false; }
    }
}
