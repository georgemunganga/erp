namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>
/// Procurement-owned, explicitly configured spending authority for one canonical ERP
/// principal. This references identity and organization; it does not define either.
/// Publish a new grant/version when the business authority changes. A role is not a grant.
/// </summary>
public sealed class ProcurementSpendingAuthorityGrant : ProcurementRecord
{
    public string AuthorityReference { get; set; } = null!;
    public int AuthorityVersion { get; set; }
    public string PrincipalId { get; set; } = null!;
    public string Permission { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string State { get; set; } = "Draft";
    public string CurrencyCode { get; set; } = null!;
    public string AmountBasis { get; set; } = null!;
    public decimal MaximumAmount { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    // An omitted dimension is not an unrestricted grant. Unrestricted scope must be explicit.
    public bool AllBranches { get; set; }
    public Guid? BranchRef { get; set; }
    public bool AllDepartments { get; set; }
    public Guid? DepartmentRef { get; set; }
    public bool AllCategories { get; set; }
    public ICollection<ProcurementSpendingAuthorityCategory> Categories { get; set; }
        = new List<ProcurementSpendingAuthorityCategory>();
}

public sealed class ProcurementSpendingAuthorityCategory : ProcurementRecord
{
    public Guid GrantId { get; set; }
    public string CategoryCode { get; set; } = null!;
}
