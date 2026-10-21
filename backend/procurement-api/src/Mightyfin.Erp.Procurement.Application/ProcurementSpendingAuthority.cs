using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Trusted module-supplied amount/category inputs, not a fabricated request or an HTTP authorization claim.</summary>
public sealed record ProcurementSpendContext(Guid TargetId, string TenantId, Guid LegalEntityId,
    string CurrencyCode, string AmountBasis, decimal Amount, Guid? BranchRef,
    IReadOnlyList<Guid?> DepartmentRefs, IReadOnlyList<string> CategoryCodes);
public sealed record ProcurementSpendingAuthorityEvidence(
    Guid GrantId, long GrantRevision, string AuthorityReference, int AuthorityVersion,
    string TenantId, Guid LegalEntityId, string PrincipalId, string Permission, string Action,
    string CurrencyCode, string AmountBasis, decimal TargetAmount, decimal MaximumAmount,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil, DateTimeOffset EvaluatedAt,
    bool AllBranches, Guid? BranchRef, bool AllDepartments, Guid? DepartmentRef,
    bool AllCategories, IReadOnlyList<string> CategoryCodes);
public sealed record ProcurementSpendingAuthorityResult(bool Eligible,
    ProcurementSpendingAuthorityEvidence? Evidence, IReadOnlyList<string> Blockers);

/// <summary>Shared spending-limit mechanics. Caller still enforces identity, assignment, scope and maker/checker.</summary>
public static class ProcurementSpendingAuthority
{
    public static ProcurementSpendingAuthorityResult Evaluate(ProcurementSpendContext context,
        string principalId, string permission, string action, DateTimeOffset at,
        IEnumerable<ProcurementSpendingAuthorityGrant> grants) =>
        EvaluateCore(context, principalId, permission, action, at, grants,
            ProcurementSpendingActions.Matches(action, permission, context.AmountBasis));

    // Narrow proposed pure use, not registered public authority/configuration support.
    internal static ProcurementSpendingAuthorityResult EvaluateSourcingPublication(ProcurementSpendContext context,
        string principalId, DateTimeOffset at, IEnumerable<ProcurementSpendingAuthorityGrant> grants)
    {
        var contract = ProcurementSpendingActions.SourcingPublication;
        return EvaluateCore(context, principalId, contract.Permission, contract.Action, at, grants,
            context.AmountBasis == contract.AmountBasis);
    }

    private static ProcurementSpendingAuthorityResult EvaluateCore(ProcurementSpendContext context,
        string principalId, string permission, string action, DateTimeOffset at,
        IEnumerable<ProcurementSpendingAuthorityGrant> grants, bool supportedContract)
    {
        if (context.TargetId == Guid.Empty || string.IsNullOrWhiteSpace(context.TenantId)
            || context.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(principalId)
            || !ValidCurrency(context.CurrencyCode) || context.Amount <= 0
            || !supportedContract
            || context.DepartmentRefs.Count == 0 || context.CategoryCodes.Count == 0
            || context.CategoryCodes.Any(string.IsNullOrWhiteSpace))
            return new(false, null, ["AuthorityContextInvalid"]);
        var candidates = grants.ToArray();
        // Load all versions of each relevant logical authority, not only rows already
        // filtered by currency, limit or record scope. A narrowed replacement must never
        // make an overlapping older, broader version appear to be the only valid grant.
        var relevant = candidates.Where(x => x.TenantId == context.TenantId
            && x.LegalEntityId == context.LegalEntityId && x.PrincipalId == principalId
            && x.Permission == permission && x.Action == action).ToArray();
        foreach (var authority in relevant.GroupBy(x => x.AuthorityReference, StringComparer.Ordinal))
        {
            var current = authority.Where(x => x.State == "Active"
                && x.EffectiveFrom <= at && (x.EffectiveUntil is null || at < x.EffectiveUntil)).ToArray();
            if (current.Length > 1 || (current.Length != 0
                && authority.GroupBy(x => x.AuthorityVersion).Any(version => version.Count() > 1)))
                return new(false, null, ["SpendingAuthorityEvidenceAmbiguous"]);
        }
        var eligible = candidates.Where(x => Covers(x, context, principalId, permission, action, at))
            .OrderBy(x => x.MaximumAmount).ThenBy(x => x.AuthorityReference, StringComparer.Ordinal)
            .ThenByDescending(x => x.AuthorityVersion).ThenBy(x => x.Id).ToArray();
        if (eligible.Length == 0) return new(false, null, ["SpendingAuthorityUnavailable"]);
        // Do not choose between contradictory loaded versions of the same grant.
        var chosen = eligible[0];
        if (candidates.Count(x => x.Id == chosen.Id) != 1)
            return new(false, null, ["SpendingAuthorityEvidenceAmbiguous"]);
        var categories = chosen.Categories.Select(x => x.CategoryCode)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(true, new(chosen.Id, chosen.Version, chosen.AuthorityReference,
            chosen.AuthorityVersion, chosen.TenantId, chosen.LegalEntityId, chosen.PrincipalId,
            chosen.Permission, chosen.Action, chosen.CurrencyCode.ToUpperInvariant(), context.AmountBasis,
            context.Amount, chosen.MaximumAmount, chosen.EffectiveFrom, chosen.EffectiveUntil, at,
            chosen.AllBranches, chosen.BranchRef, chosen.AllDepartments, chosen.DepartmentRef,
            chosen.AllCategories, Array.AsReadOnly(categories)), []);
    }

    private static bool Covers(ProcurementSpendingAuthorityGrant grant, ProcurementSpendContext context,
        string principalId, string permission, string action, DateTimeOffset at)
    {
        if (grant.Id == Guid.Empty || grant.Version <= 0 || grant.AuthorityVersion <= 0
            || string.IsNullOrWhiteSpace(grant.AuthorityReference) || grant.State != "Active"
            || grant.TenantId != context.TenantId || grant.LegalEntityId != context.LegalEntityId
            || grant.PrincipalId != principalId || grant.Permission != permission || grant.Action != action
            || !ValidCurrency(grant.CurrencyCode) || !CurrencyEquals(grant.CurrencyCode, context.CurrencyCode)
            || grant.AmountBasis != context.AmountBasis || grant.MaximumAmount <= 0 || grant.MaximumAmount < context.Amount
            || grant.EffectiveFrom == default || at < grant.EffectiveFrom
            || grant.EffectiveUntil is { } until && (until <= grant.EffectiveFrom || at >= until)) return false;
        if (grant.AllBranches ? grant.BranchRef is not null
            : grant.BranchRef is null || grant.BranchRef == Guid.Empty || grant.BranchRef != context.BranchRef) return false;
        if (grant.AllDepartments ? grant.DepartmentRef is not null
            : grant.DepartmentRef is null || grant.DepartmentRef == Guid.Empty
                || context.DepartmentRefs.Any(x => x != grant.DepartmentRef)) return false;
        if (grant.AllCategories) return grant.Categories.Count == 0;
        if (grant.Categories.Count == 0 || grant.Categories.Any(x => x.Id == Guid.Empty
            || x.GrantId != grant.Id || x.TenantId != grant.TenantId || x.LegalEntityId != grant.LegalEntityId
            || string.IsNullOrWhiteSpace(x.CategoryCode))
            || grant.Categories.Select(x => x.CategoryCode).Distinct(StringComparer.Ordinal).Count() != grant.Categories.Count)
            return false;
        return context.CategoryCodes.All(code => grant.Categories.Any(category => category.CategoryCode == code));
    }
    private static bool ValidCurrency(string? code) => code is { Length: 3 }
        && code.All(x => x is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    private static bool CurrencyEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
