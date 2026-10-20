using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Server-resolved canonical identity and grants; never bind this from a request body.</summary>
public sealed record SpendingAuthorityConfigurationActor(string TenantId, Guid LegalEntityId,
    string PrincipalId, bool IsActive, IReadOnlySet<string> Permissions);
public sealed record SpendingAuthoritySubject(string TenantId, Guid LegalEntityId,
    string PrincipalId, bool IsActive, bool IsCompanyMember);

/// <summary>Incomplete values are permitted in a draft; nothing here confers authority.</summary>
public sealed record SpendingAuthorityDraft(string? CurrencyCode = null, string? AmountBasis = null,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(AuthorityAmountJsonConverter))]
    decimal MaximumAmount = 0, DateTimeOffset EffectiveFrom = default,
    DateTimeOffset? EffectiveUntil = null, bool AllBranches = false, Guid? BranchRef = null,
    bool AllDepartments = false, Guid? DepartmentRef = null, bool AllCategories = false,
    IReadOnlyList<string>? CategoryCodes = null);

/// <summary>
/// Procurement configuration lifecycle only. Caller must load the COMPLETE logical authority
/// family, lock/serialize it, enforce canonical identity/record scope and save every returned
/// change plus audit atomically. Permissions here must come from server authorization.
/// A repository MUST call AssertPublishedBusinessUnchanged before updating published rows,
/// and forbid category-row updates/deletes for those rows. No role implies a financial limit.
/// </summary>
public static class SpendingAuthorityConfiguration
{
    public const string ConfigurePermission = "procurement-authority-configure";
    public const string ReviewPermission = "procurement-authority-review";
    public const string RevokePermission = "procurement-authority-revoke";
    public const string ApprovalPermission = "procurement-approval-decide";
    public const string ApprovalAction = "PurchaseRequest.Approve";

    public static ProcurementSpendingAuthorityGrant CreateDraft(string authorityReference,
        string subjectPrincipalId, SpendingAuthorityDraft input,
        SpendingAuthorityConfigurationActor actor, DateTimeOffset at,
        IReadOnlyCollection<ProcurementSpendingAuthorityGrant> family)
    {
        RequireActor(actor, actor.TenantId, actor.LegalEntityId, ConfigurePermission);
        if (!ValidIdentifier(authorityReference, 100) || !ValidIdentifier(subjectPrincipalId, 200))
            throw new InvalidOperationException("AuthorityReferenceOrPrincipalInvalid");
        var relevant = family.Where(x => x.TenantId == actor.TenantId
            && x.LegalEntityId == actor.LegalEntityId && x.PrincipalId == subjectPrincipalId
            && x.Permission == ApprovalPermission && x.Action == ApprovalAction
            && x.AuthorityReference == authorityReference).ToArray();
        AssertFamilyUnique(relevant);
        var next = checked(relevant.Select(x => x.AuthorityVersion).DefaultIfEmpty(0).Max() + 1);
        var grant = new ProcurementSpendingAuthorityGrant
        {
            TenantId = actor.TenantId, LegalEntityId = actor.LegalEntityId,
            AuthorityReference = authorityReference, AuthorityVersion = next,
            PrincipalId = subjectPrincipalId, Permission = ApprovalPermission, Action = ApprovalAction,
            ConfigurationOwnerPrincipalId = actor.PrincipalId, CreatedBy = actor.PrincipalId,
            CreatedAt = at, State = "Draft",
        };
        SetDraft(grant, input);
        return grant;
    }

    public static void EditDraft(ProcurementSpendingAuthorityGrant grant, long expectedVersion,
        SpendingAuthorityDraft input, SpendingAuthorityConfigurationActor actor, DateTimeOffset at)
    {
        RequireOwnedDraft(grant, expectedVersion, actor);
        SetDraft(grant, input);
        Touch(grant, actor, at);
    }

    public static void Submit(ProcurementSpendingAuthorityGrant grant, long expectedVersion,
        SpendingAuthorityConfigurationActor actor, SpendingAuthoritySubject subject, DateTimeOffset at)
    {
        RequireOwnedDraft(grant, expectedVersion, actor);
        AssertComplete(grant, subject, at);
        grant.State = "PendingReview";
        grant.SubmittedByPrincipalId = actor.PrincipalId;
        grant.SubmittedAt = at;
        grant.SubmittedBusinessFingerprint = Fingerprint(grant);
        grant.ReviewReturnReason = null;
        Touch(grant, actor, at);
    }

    public static void ReturnForChanges(ProcurementSpendingAuthorityGrant grant, long expectedVersion,
        SpendingAuthorityConfigurationActor actor, string reason, DateTimeOffset at)
    {
        RequireReviewer(grant, expectedVersion, actor);
        RequireReason(reason);
        grant.State = "Draft";
        grant.SubmittedByPrincipalId = null;
        grant.SubmittedAt = null;
        grant.SubmittedBusinessFingerprint = null;
        grant.ReviewReturnReason = reason.Trim();
        Touch(grant, actor, at);
    }

    public static void Activate(ProcurementSpendingAuthorityGrant grant, long expectedVersion,
        SpendingAuthorityConfigurationActor actor, SpendingAuthoritySubject subject, string reason,
        DateTimeOffset at, IReadOnlyCollection<ProcurementSpendingAuthorityGrant> family)
    {
        RequireReviewer(grant, expectedVersion, actor);
        RequireReason(reason);
        AssertComplete(grant, subject, at);
        var relatives = RequireCurrentFamily(grant, family);
        if (relatives.Any(x => x.Id != grant.Id && x.State == "Active"))
            throw new InvalidOperationException("AuthoritySupersessionRequired");
        Publish(grant, actor, reason, at);
    }

    /// <summary>Both replacement and predecessor must be persisted in the SAME transaction.</summary>
    public static void Supersede(ProcurementSpendingAuthorityGrant replacement, long expectedVersion,
        ProcurementSpendingAuthorityGrant previous, long expectedPreviousVersion,
        SpendingAuthorityConfigurationActor actor, SpendingAuthoritySubject subject, string reason,
        DateTimeOffset at, IReadOnlyCollection<ProcurementSpendingAuthorityGrant> family)
    {
        RequireReviewer(replacement, expectedVersion, actor);
        RequireReason(reason);
        AssertComplete(replacement, subject, at);
        var relatives = RequireCurrentFamily(replacement, family);
        if (!SameAuthority(previous, replacement) || previous.Id == replacement.Id
            || previous.Version <= 0 || previous.Version == long.MaxValue
            || previous.Version != expectedPreviousVersion || previous.State != "Active"
            || previous.AuthorityVersion >= replacement.AuthorityVersion
            || relatives.Count(x => x.Id == previous.Id && ReferenceEquals(x, previous)) != 1
            || relatives.Any(x => x.Id != previous.Id && x.Id != replacement.Id && x.State == "Active"))
            throw new InvalidOperationException("AuthorityPredecessorInvalid");
        if (replacement.EffectiveFrom > at)
            throw new InvalidOperationException("FutureAuthoritySupersessionUnsupported");
        AssertFingerprint(previous);
        previous.State = "Superseded";
        previous.SupersededByGrantId = replacement.Id;
        previous.SupersededAt = at;
        Touch(previous, actor, at);
        Publish(replacement, actor, reason, at);
    }

    public static void Revoke(ProcurementSpendingAuthorityGrant grant, long expectedVersion,
        SpendingAuthorityConfigurationActor actor, string reason, DateTimeOffset at)
    {
        RequireActor(actor, grant.TenantId, grant.LegalEntityId, RevokePermission);
        if (grant.Version <= 0 || grant.Version == long.MaxValue || grant.Version != expectedVersion || grant.State != "Active")
            throw new InvalidOperationException("AuthorityStateOrVersionChanged");
        RequireReason(reason);
        AssertFingerprint(grant);
        grant.State = "Revoked";
        grant.RevokedByPrincipalId = actor.PrincipalId;
        grant.RevokedAt = at;
        grant.RevocationReason = reason.Trim();
        Touch(grant, actor, at);
    }

    /// <summary>Repository guard: publication locks business values, not later lifecycle evidence.</summary>
    public static void AssertPublishedBusinessUnchanged(ProcurementSpendingAuthorityGrant original,
        ProcurementSpendingAuthorityGrant proposed)
    {
        if (original.PublishedBusinessFingerprint is null) return;
        if (original.Id != proposed.Id || original.PublishedBusinessFingerprint != proposed.PublishedBusinessFingerprint
            || Fingerprint(original) != original.PublishedBusinessFingerprint
            || Fingerprint(proposed) != original.PublishedBusinessFingerprint)
            throw new InvalidOperationException("PublishedAuthorityIsImmutable");
        if (original.State is "Revoked" or "Superseded" && proposed.State != original.State)
            throw new InvalidOperationException("TerminalAuthorityCannotBeRevived");
        if (original.State == "Active" && proposed.State is not ("Active" or "Revoked" or "Superseded"))
            throw new InvalidOperationException("PublishedAuthorityStateInvalid");
    }

    public static string Fingerprint(ProcurementSpendingAuthorityGrant grant)
    {
        var value = JsonSerializer.Serialize(new
        {
            grant.Id, grant.TenantId, grant.LegalEntityId, grant.AuthorityReference,
            grant.AuthorityVersion, grant.PrincipalId, grant.Permission, grant.Action,
            grant.ConfigurationOwnerPrincipalId, grant.CreatedBy,
            CreatedAt = CanonicalTime(grant.CreatedAt),
            grant.CurrencyCode, grant.AmountBasis,
            MaximumAmount = grant.MaximumAmount.ToString("G29", CultureInfo.InvariantCulture),
            EffectiveFrom = CanonicalTime(grant.EffectiveFrom),
            EffectiveUntil = grant.EffectiveUntil is { } until ? CanonicalTime(until) : (DateTimeOffset?)null, grant.AllBranches, grant.BranchRef,
            grant.AllDepartments, grant.DepartmentRef, grant.AllCategories,
            Categories = grant.Categories.OrderBy(x => x.CategoryCode, StringComparer.Ordinal)
                .ThenBy(x => x.Id).Select(x => new { x.Id, x.TenantId, x.LegalEntityId, x.GrantId, x.CategoryCode }),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static void SetDraft(ProcurementSpendingAuthorityGrant grant, SpendingAuthorityDraft input)
    {
        var categories = input.CategoryCodes?.ToArray() ?? [];
        if (categories.Length > 100 || categories.Any(x => !ValidIdentifier(x, 100))
            || categories.Distinct(StringComparer.Ordinal).Count() != categories.Length
            || input.CurrencyCode is { Length: > 3 } || input.AmountBasis is { Length: > 100 }
            || input.MaximumAmount < 0 || input.MaximumAmount > 99_999_999_999_999.9999m
            || decimal.Round(input.MaximumAmount, 4) != input.MaximumAmount)
            throw new InvalidOperationException("AuthorityDraftFieldsInvalid");
        grant.CurrencyCode = input.CurrencyCode?.Trim().ToUpperInvariant() ?? "";
        grant.AmountBasis = input.AmountBasis ?? "";
        grant.MaximumAmount = input.MaximumAmount;
        grant.EffectiveFrom = input.EffectiveFrom;
        grant.EffectiveUntil = input.EffectiveUntil;
        grant.AllBranches = input.AllBranches;
        grant.BranchRef = input.BranchRef;
        grant.AllDepartments = input.AllDepartments;
        grant.DepartmentRef = input.DepartmentRef;
        grant.AllCategories = input.AllCategories;
        grant.Categories = categories.Select(code => new ProcurementSpendingAuthorityCategory
        {
            TenantId = grant.TenantId, LegalEntityId = grant.LegalEntityId, GrantId = grant.Id,
            CategoryCode = code, CreatedAt = grant.CreatedAt, CreatedBy = grant.ConfigurationOwnerPrincipalId,
        }).ToList();
    }

    private static void AssertComplete(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthoritySubject subject, DateTimeOffset at)
    {
        if (subject.TenantId != grant.TenantId || subject.LegalEntityId != grant.LegalEntityId
            || subject.PrincipalId != grant.PrincipalId || !subject.IsActive || !subject.IsCompanyMember)
            throw new InvalidOperationException("AuthoritySubjectUnavailable");
        if (!ValidIdentifier(grant.AuthorityReference, 100) || !ValidIdentifier(grant.PrincipalId, 200)
            || !ValidIdentifier(grant.ConfigurationOwnerPrincipalId, 200) || !ValidIdentifier(grant.CreatedBy, 200)
            || grant.AuthorityVersion <= 0 || grant.MaximumAmount <= 0 || grant.MaximumAmount > 99_999_999_999_999.9999m
            || decimal.Round(grant.MaximumAmount, 4) != grant.MaximumAmount
            || grant.CurrencyCode.Length != 3 || !grant.CurrencyCode.All(x => x is >= 'A' and <= 'Z')
            || grant.AmountBasis != PurchaseRequestSpendingAuthority.SupportedAmountBasis
            || grant.Permission != ApprovalPermission || grant.Action != ApprovalAction
            || grant.EffectiveFrom == default || grant.EffectiveUntil is { } until
                && (until <= grant.EffectiveFrom || until <= at))
            throw new InvalidOperationException("AuthorityFinancialTermsInvalid");
        if (grant.AllBranches ? grant.BranchRef is not null : grant.BranchRef is null || grant.BranchRef == Guid.Empty)
            throw new InvalidOperationException("AuthorityBranchScopeRequired");
        if (grant.AllDepartments ? grant.DepartmentRef is not null : grant.DepartmentRef is null || grant.DepartmentRef == Guid.Empty)
            throw new InvalidOperationException("AuthorityDepartmentScopeRequired");
        if (grant.AllCategories ? grant.Categories.Count != 0 : grant.Categories.Count == 0)
            throw new InvalidOperationException("AuthorityCategoryScopeRequired");
        if (grant.Categories.Any(x => x.GrantId != grant.Id || x.TenantId != grant.TenantId
            || x.LegalEntityId != grant.LegalEntityId || !ValidIdentifier(x.CategoryCode, 100))
            || grant.Categories.Select(x => x.CategoryCode).Distinct(StringComparer.Ordinal).Count() != grant.Categories.Count)
            throw new InvalidOperationException("AuthorityCategoryScopeInvalid");
    }

    private static void RequireOwnedDraft(ProcurementSpendingAuthorityGrant grant, long version,
        SpendingAuthorityConfigurationActor actor)
    {
        RequireActor(actor, grant.TenantId, grant.LegalEntityId, ConfigurePermission);
        if (grant.State != "Draft" || grant.Version <= 0 || grant.Version == long.MaxValue
            || grant.Version != version || grant.PublishedBusinessFingerprint is not null)
            throw new InvalidOperationException("AuthorityStateOrVersionChanged");
        if (grant.ConfigurationOwnerPrincipalId != actor.PrincipalId)
            throw new InvalidOperationException("AuthorityConfigurationOwnerRequired");
    }

    private static void RequireReviewer(ProcurementSpendingAuthorityGrant grant, long version,
        SpendingAuthorityConfigurationActor actor)
    {
        RequireActor(actor, grant.TenantId, grant.LegalEntityId, ReviewPermission);
        if (grant.State != "PendingReview" || grant.Version <= 0 || grant.Version == long.MaxValue
            || grant.Version != version || grant.PublishedBusinessFingerprint is not null)
            throw new InvalidOperationException("AuthorityStateOrVersionChanged");
        if (string.IsNullOrWhiteSpace(grant.SubmittedByPrincipalId) || grant.SubmittedAt is null
            || grant.SubmittedBusinessFingerprint is null
            || Fingerprint(grant) != grant.SubmittedBusinessFingerprint)
            throw new InvalidOperationException("SubmittedAuthorityChanged");
        if (actor.PrincipalId == grant.PrincipalId || actor.PrincipalId == grant.CreatedBy
            || actor.PrincipalId == grant.ConfigurationOwnerPrincipalId || actor.PrincipalId == grant.SubmittedByPrincipalId)
            throw new InvalidOperationException("IndependentAuthorityReviewerRequired");
    }

    private static void RequireActor(SpendingAuthorityConfigurationActor actor, string tenant,
        Guid company, string permission)
    {
        if (!actor.IsActive || !ValidIdentifier(actor.PrincipalId, 200)
            || string.IsNullOrWhiteSpace(tenant) || company == Guid.Empty
            || actor.TenantId != tenant || actor.LegalEntityId != company || !actor.Permissions.Contains(permission))
            throw new InvalidOperationException("AuthorityConfigurationAccessDenied");
    }

    private static ProcurementSpendingAuthorityGrant[] RequireCurrentFamily(
        ProcurementSpendingAuthorityGrant grant, IReadOnlyCollection<ProcurementSpendingAuthorityGrant> family)
    {
        var relatives = family.Where(x => SameAuthority(x, grant)).ToArray();
        AssertFamilyUnique(relatives);
        if (relatives.Count(x => x.Id == grant.Id && ReferenceEquals(x, grant)) != 1
            || relatives.Any(x => x.AuthorityVersion > grant.AuthorityVersion))
            throw new InvalidOperationException("AuthorityLatestVersionRequired");
        return relatives;
    }

    private static void AssertFamilyUnique(ProcurementSpendingAuthorityGrant[] family)
    {
        if (family.Any(x => x.AuthorityVersion <= 0)
            || family.Select(x => x.AuthorityVersion).Distinct().Count() != family.Length
            || family.Select(x => x.Id).Distinct().Count() != family.Length)
            throw new InvalidOperationException("AuthorityFamilyVersionsAmbiguous");
    }

    private static bool SameAuthority(ProcurementSpendingAuthorityGrant left, ProcurementSpendingAuthorityGrant right)
        => left.TenantId == right.TenantId && left.LegalEntityId == right.LegalEntityId
            && left.PrincipalId == right.PrincipalId && left.Permission == right.Permission
            && left.Action == right.Action && left.AuthorityReference == right.AuthorityReference;
    private static void Publish(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityConfigurationActor actor, string reason, DateTimeOffset at)
    {
        grant.State = "Active";
        grant.ActivatedByPrincipalId = actor.PrincipalId;
        grant.ActivatedAt = at;
        grant.ActivationReason = reason.Trim();
        grant.PublishedBusinessFingerprint = Fingerprint(grant);
        Touch(grant, actor, at);
    }
    private static void AssertFingerprint(ProcurementSpendingAuthorityGrant grant)
    {
        if (grant.PublishedBusinessFingerprint is null || Fingerprint(grant) != grant.PublishedBusinessFingerprint)
            throw new InvalidOperationException("PublishedAuthorityIsImmutable");
    }
    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new InvalidOperationException("AuthorityDecisionReasonRequired");
    }
    // PostgreSQL timestamps retain microseconds, and numeric columns can add trailing zeros.
    // Fingerprints use their stable persisted representation rather than JSON display scale/offset.
    private static DateTimeOffset CanonicalTime(DateTimeOffset value)
        => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    private static bool ValidIdentifier(string? text, int length)
        => !string.IsNullOrWhiteSpace(text) && text.Length <= length && text.Trim() == text
            && !text.Any(char.IsControl);
    private static void Touch(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityConfigurationActor actor, DateTimeOffset at)
    {
        grant.Version = checked(grant.Version + 1);
        grant.UpdatedBy = actor.PrincipalId;
        grant.UpdatedAt = at;
    }
}
