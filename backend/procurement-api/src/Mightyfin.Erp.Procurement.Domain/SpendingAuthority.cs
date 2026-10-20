namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>
/// Procurement-owned, explicitly configured spending authority for one canonical ERP
/// principal. This references identity and organization; it does not define either.
/// Logical authority key: tenant/entity/principal/permission/action/AuthorityReference
/// (ordinal identifiers). AuthorityVersion is unique within that key; effective Active
/// versions must never overlap. Currency, amount limit and record scope are versioned
/// attributes, NOT part of the key. Close/revoke the old version when replacing it.
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
    public string ConfigurationOwnerPrincipalId { get; set; } = null!;
    public string? SubmittedByPrincipalId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? SubmittedBusinessFingerprint { get; set; }
    public string? ReviewReturnReason { get; set; }
    public string? ActivatedByPrincipalId { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivationReason { get; set; }
    public string? PublishedBusinessFingerprint { get; set; }
    public string? RevokedByPrincipalId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? SupersededByGrantId { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
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
