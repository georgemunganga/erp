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
        if (value.IdentitySchemaVersion != 1 || value.AssigneeWorkerId is not { } worker || worker == Guid.Empty || string.IsNullOrWhiteSpace(value.EvidenceJson)
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

/// <summary>Successor evidence binds the assignment to its Core principal and optional known alias.</summary>
public sealed record PurchaseRequestPrincipalAssignmentEvidence(int SchemaVersion, string TenantId,
    Guid LegalEntityId, Guid SubmissionId, Guid CoreApprovalCaseId, Guid CoreStageId, Guid CoreAssignmentId,
    string PrincipalId, Guid? KnownCoreWorkerId, string AssigneeKind, PurchaseRequestAuthorityEvidence Authority);

public static class PurchaseRequestPrincipalAssignmentIdentity
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static PurchaseRequestPrincipalAssignmentEvidence? ReadVerified(PurchaseRequestAssignmentAuthorityEvidence value)
    {
        if (value.IdentitySchemaVersion != 2 || value.EvidenceJson is not { Length: > 0 and <= 32768 }
            || value.AssigneeWorkerId == Guid.Empty
            || value.EvidenceSha256 != ProcurementEvidenceEncoding.Digest(value.EvidenceJson)) return null;
        PurchaseRequestPrincipalAssignmentEvidence? retained;
        try
        {
            // Existing canonical JSON reader rejects duplicate properties recursively.
            if (ProcurementEvidenceEncoding.PolicyJsonDigest(value.EvidenceJson).Length != 64) return null;
            retained = JsonSerializer.Deserialize<PurchaseRequestPrincipalAssignmentEvidence>(value.EvidenceJson, Options);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException) { return null; }
        var authority=retained?.Authority;
        if (retained is null || authority is null || retained.SchemaVersion != 2
            || !Guid.TryParseExact(value.PrincipalId,"D",out var principal) || principal==Guid.Empty
            || value.PrincipalId != principal.ToString("D")
            || retained.TenantId != value.TenantId || retained.LegalEntityId != value.LegalEntityId
            || retained.SubmissionId != value.SubmissionId || retained.CoreApprovalCaseId != value.CoreApprovalCaseId
            || retained.CoreStageId != value.CoreStageId || retained.CoreAssignmentId != value.CoreAssignmentId
            || retained.PrincipalId != value.PrincipalId || retained.KnownCoreWorkerId != value.AssigneeWorkerId
            || retained.AssigneeKind != value.AssigneeKind
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

        var context = new ProcurementSpendContext(request.Id, request.TenantId, request.LegalEntityId,
            request.CurrencyCode, amountBasis, amount, request.BranchRef,
            [request.DepartmentRef], lines.Select(x => x.CategoryCode!).Distinct(StringComparer.Ordinal).ToArray());
        var result = ProcurementSpendingAuthority.Evaluate(context, principalId, permission, action, at, grants);
        if (!result.Eligible || result.Evidence is not { } chosen) return new(false, null, result.Blockers);
        return new(true, new(chosen.GrantId, chosen.GrantRevision, chosen.AuthorityReference, chosen.AuthorityVersion,
            chosen.TenantId, chosen.LegalEntityId, chosen.PrincipalId, chosen.Permission, chosen.Action,
            chosen.CurrencyCode, chosen.AmountBasis, chosen.TargetAmount, chosen.MaximumAmount,
            chosen.EffectiveFrom, chosen.EffectiveUntil, chosen.EvaluatedAt, chosen.AllBranches,
            chosen.BranchRef, chosen.AllDepartments, chosen.DepartmentRef, chosen.AllCategories, chosen.CategoryCodes), []);
    }

    private static bool ValidCurrency(string? code) => code is { Length: 3 }
        && code.All(x => x is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    private static bool CurrencyEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
