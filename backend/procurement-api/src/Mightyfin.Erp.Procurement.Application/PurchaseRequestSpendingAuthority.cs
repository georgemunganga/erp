using Mightyfin.Erp.Procurement.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Immutable values to pin into a Procurement approval submission/assignment.</summary>
public sealed record PurchaseRequestAuthorityEvidence(
    Guid GrantId, long GrantRevision, string AuthorityReference, int AuthorityVersion,
    string TenantId, Guid LegalEntityId, string PrincipalId, string Permission, string Action,
    string CurrencyCode, string AmountBasis, decimal RequestAmount, decimal MaximumAmount,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil, DateTimeOffset EvaluatedAt,
    bool AllBranches, Guid? BranchRef, bool AllDepartments, Guid? DepartmentRef,
    bool AllCategories, IReadOnlyList<string> CategoryCodes);

/// <summary>Canonical worker identity captured at opening; hash the entire envelope for retained evidence.</summary>
public sealed record PurchaseRequestAssignmentIdentityEvidence(Guid AssigneeWorkerId, PurchaseRequestAuthorityEvidence Authority);

public static class PurchaseRequestAssignmentIdentity
{
    public static PurchaseRequestAssignmentIdentityEvidence? ReadVerified(PurchaseRequestAssignmentAuthorityEvidence value)
    {
        if (value.AssigneeWorkerId is not { } worker || worker == Guid.Empty || string.IsNullOrWhiteSpace(value.EvidenceJson)
            || value.EvidenceSha256 != Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value.EvidenceJson)))) return null;
        PurchaseRequestAssignmentIdentityEvidence? retained;
        try { retained = JsonSerializer.Deserialize<PurchaseRequestAssignmentIdentityEvidence>(value.EvidenceJson); }
        catch (JsonException) { return null; }
        var authority = retained?.Authority;
        if (retained is null || authority is null || retained.AssigneeWorkerId != worker
            || authority.TenantId != value.TenantId || authority.LegalEntityId != value.LegalEntityId
            || authority.PrincipalId != value.PrincipalId || authority.GrantId != value.GrantId
            || authority.GrantRevision != value.GrantRevision || authority.AuthorityVersion != value.AuthorityVersion) return null;
        return retained;
    }
}

public sealed record PurchaseRequestAuthorityResult(bool Eligible,
    PurchaseRequestAuthorityEvidence? Evidence, IReadOnlyList<string> Blockers);

/// <summary>
/// A pure Procurement spending-limit check, not authentication or authorization.
/// The caller must resolve an active canonical principal, enforce current server-side
/// permission and record scope, maker/checker and source/policy/Finance guards. Re-evaluate
/// current grants on every decision; historic evidence alone cannot authorize a decision.
/// Supply all versions of relevant logical authorities, including narrowed/revoked rows;
/// do not prefilter by amount, currency or record scope. Conflicting versions fail closed.
/// No FX conversion, tax-inclusive basis, delegation or combining grants is supported.
/// </summary>
public static class PurchaseRequestSpendingAuthority
{
    public const string SupportedAmountBasis = "EstimatedLineBase";

    public static PurchaseRequestAuthorityResult Evaluate(PurchaseRequest request,
        string principalId, string permission, string action, string amountBasis,
        DateTimeOffset at, IEnumerable<ProcurementSpendingAuthorityGrant> grants)
    {
        var blockers = new List<string>();
        if (string.IsNullOrWhiteSpace(principalId) || string.IsNullOrWhiteSpace(permission)
            || string.IsNullOrWhiteSpace(action)) blockers.Add("AuthorityPrincipalOrActionMissing");
        if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.TenantId)
            || request.LegalEntityId == Guid.Empty) blockers.Add("AuthorityRequestScopeInvalid");
        if (amountBasis != SupportedAmountBasis) blockers.Add("AuthorityAmountBasisUnsupported");
        if (!ValidCurrency(request.CurrencyCode)) blockers.Add("AuthorityCurrencyInvalid");
        var lines = request.Lines.Where(x => x.ApprovalStatus != "Cancelled").ToArray();
        if (lines.Length == 0 || lines.Select(x => x.Id).Distinct().Count() != lines.Length
            || lines.Any(x => x.Id == Guid.Empty || x.PurchaseRequestId != request.Id
                || x.TenantId != request.TenantId || x.LegalEntityId != request.LegalEntityId
                || x.RequestedQuantity <= 0 || x.EstimatedUnitPrice < 0
                || string.IsNullOrWhiteSpace(x.CategoryCode)
                || (!string.IsNullOrWhiteSpace(x.EstimatedCurrencyCode)
                    && !CurrencyEquals(x.EstimatedCurrencyCode, request.CurrencyCode))))
            blockers.Add("AuthorityRequestLinesInvalid");
        decimal amount = 0;
        try { amount = lines.Sum(x => checked(x.RequestedQuantity * x.EstimatedUnitPrice)); }
        catch (OverflowException) { blockers.Add("AuthorityAmountOverflow"); }
        // Zero is not implicit financial authority; zero-value buying needs its own policy.
        if (amount <= 0) blockers.Add("AuthorityAmountNotPositive");
        if (blockers.Count != 0) return new(false, null, blockers);

        var candidates = grants.ToArray();
        // Load all versions of each relevant logical authority, not only rows already
        // filtered by currency, limit or record scope. A narrowed replacement must never
        // make an overlapping older, broader version appear to be the only valid grant.
        var relevant = candidates.Where(x => x.TenantId == request.TenantId
            && x.LegalEntityId == request.LegalEntityId && x.PrincipalId == principalId
            && x.Permission == permission && x.Action == action).ToArray();
        foreach (var authority in relevant.GroupBy(x => x.AuthorityReference, StringComparer.Ordinal))
        {
            var current = authority.Where(x => x.State == "Active"
                && x.EffectiveFrom <= at && (x.EffectiveUntil is null || at < x.EffectiveUntil)).ToArray();
            if (current.Length > 1 || (current.Length != 0
                && authority.GroupBy(x => x.AuthorityVersion).Any(version => version.Count() > 1)))
                return new(false, null, ["SpendingAuthorityEvidenceAmbiguous"]);
        }
        var eligible = candidates.Where(x => Covers(x, request, lines, principalId, permission,
                action, amountBasis, amount, at))
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
            chosen.Permission, chosen.Action, chosen.CurrencyCode.ToUpperInvariant(), amountBasis,
            amount, chosen.MaximumAmount, chosen.EffectiveFrom, chosen.EffectiveUntil, at,
            chosen.AllBranches, chosen.BranchRef, chosen.AllDepartments, chosen.DepartmentRef,
            chosen.AllCategories, Array.AsReadOnly(categories)), []);
    }

    private static bool Covers(ProcurementSpendingAuthorityGrant grant, PurchaseRequest request,
        PurchaseRequestLine[] lines, string principalId, string permission, string action,
        string basis, decimal amount, DateTimeOffset at)
    {
        if (grant.Id == Guid.Empty || grant.Version <= 0 || grant.AuthorityVersion <= 0
            || string.IsNullOrWhiteSpace(grant.AuthorityReference) || grant.State != "Active"
            || grant.TenantId != request.TenantId || grant.LegalEntityId != request.LegalEntityId
            || grant.PrincipalId != principalId || grant.Permission != permission || grant.Action != action
            || !ValidCurrency(grant.CurrencyCode) || !CurrencyEquals(grant.CurrencyCode, request.CurrencyCode)
            || grant.AmountBasis != basis || grant.MaximumAmount <= 0 || grant.MaximumAmount < amount
            || grant.EffectiveFrom == default || at < grant.EffectiveFrom
            || (grant.EffectiveUntil is { } until && (until <= grant.EffectiveFrom || at >= until)))
            return false;
        if (grant.AllBranches ? grant.BranchRef is not null
            : grant.BranchRef is null || grant.BranchRef == Guid.Empty || grant.BranchRef != request.BranchRef)
            return false;
        if (grant.AllDepartments ? grant.DepartmentRef is not null
            : grant.DepartmentRef is null || grant.DepartmentRef == Guid.Empty
                || grant.DepartmentRef != request.DepartmentRef)
            return false;
        if (grant.AllCategories) return grant.Categories.Count == 0;
        if (grant.Categories.Count == 0 || grant.Categories.Any(x => x.Id == Guid.Empty
            || x.GrantId != grant.Id || x.TenantId != grant.TenantId || x.LegalEntityId != grant.LegalEntityId
            || string.IsNullOrWhiteSpace(x.CategoryCode))
            || grant.Categories.Select(x => x.CategoryCode).Distinct(StringComparer.Ordinal).Count()
                != grant.Categories.Count) return false;
        return lines.All(line => !string.IsNullOrWhiteSpace(line.CategoryCode)
            && grant.Categories.Any(category => category.CategoryCode == line.CategoryCode));
    }

    private static bool ValidCurrency(string? code) => code is { Length: 3 }
        && code.All(x => x is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    private static bool CurrencyEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
