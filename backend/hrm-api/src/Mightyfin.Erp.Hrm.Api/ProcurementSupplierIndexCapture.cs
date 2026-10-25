namespace Mightyfin.Erp.Hrm.Api;

// Supplier owns this existing unique-registration index. Exact PostgreSQL17
// metadata was read with the actual finite P35 execution login, READ ONLY, on
// the failed fresh private01 store. No index/model/migration is changed here.
internal static class ProcurementSupplierIndexCapture
{
    internal const string RegistrationIndex = "IX_suppliers_tenant_id_legal_entity_id_country_code_normalized~";
    internal static readonly IReadOnlyDictionary<string, string> ModelPredicates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [RegistrationIndex] = "normalized_registration IS NOT NULL"
    };
    internal static readonly IReadOnlyDictionary<string, string> PartialPredicates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [RegistrationIndex] = "(normalized_registration IS NOT NULL)"
    };
}
