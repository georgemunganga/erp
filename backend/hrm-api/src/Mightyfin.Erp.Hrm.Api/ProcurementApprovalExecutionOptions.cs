using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementApprovalExecutionOptions
{
    public bool Enabled { get; set; }
    // Set from the explicit connection-string registration, never from an HTTP body.
    public bool ExecutionDatabaseConfigured { get; set; }
    public ProcurementManualFinancePolicy[] ManualFinancePolicies { get; set; } = [];

    public bool HasCurrentManualEvidencePolicy(Guid company, DateTimeOffset now)
    {
        var policies = ManualFinancePolicies.Where(x => x.Enabled && x.LegalEntityId == company
            && x.EffectiveFrom != default && x.EffectiveFrom <= now
            && (x.EffectiveUntil is null || now < x.EffectiveUntil)).ToArray();
        return policies.Length == 1 && !string.IsNullOrWhiteSpace(policies[0].ApprovedPolicyReference);
    }
}
public sealed class ProcurementManualFinancePolicy
{
    public Guid LegalEntityId { get; set; }
    public bool Enabled { get; set; }
    public string ApprovedPolicyReference { get; set; } = "";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
    public int? EvidenceMaximumAgeMinutes { get; set; }
}

internal static class ProcurementApprovalExecutionLocks
{
    // All approval, Finance evidence and source writers take the request first.
    internal static Task LockRequestAsync(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
        => db.Database.IsNpgsql() ? db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM procurement.purchase_requests WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct)
            : Task.CompletedTask;
    internal static async Task<HrmDbContext> CoreAsync(ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("ApprovalTransactionRequired");
        var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(db.Database.GetDbConnection()).Options,
            new Tenant(scope.TenantId));
        try { await core.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct); return core; }
        catch { await core.DisposeAsync(); throw; }
    }
    private sealed class Tenant(string id) : ITenantAccessor { public string GetTenantId() => id; }
}

internal static class ProcurementApprovalFinanceEligibility
{
    internal static async Task<(string Mode, string Reference)> CheckAsync(PurchaseRequest request,
        BudgetCheckSnapshot finance, ProcurementApprovalExecutionOptions options, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset now, CancellationToken ct)
    {
        var policies = options.ManualFinancePolicies.Where(x => x.Enabled && x.LegalEntityId == scope.LegalEntityId
            && x.EffectiveFrom != default && x.EffectiveFrom <= now
            && (x.EffectiveUntil is null || now < x.EffectiveUntil)).ToArray();
        if (policies.Length != 1 || string.IsNullOrWhiteSpace(policies[0].ApprovedPolicyReference))
            throw new InvalidOperationException("FinanceEvidenceAuthorityNotConfigured");
        var policy = policies[0];
        if (policy.EvidenceMaximumAgeMinutes is { } minutes && (minutes <= 0
            || finance.CheckedAt.AddMinutes(minutes) < now)) throw new InvalidOperationException("FinanceEvidenceExpired");
        if (finance.CheckedAt > now || finance.CheckedBySubjectId == request.CreatedBy)
            throw new InvalidOperationException("FinanceEvidenceVerifierInvalid");
        _ = await RequireCurrentCheckerAsync(request, finance, core, db, scope, ct);
        return (PurchaseRequestApprovalPinning.ApprovedManualEvidence, policy.ApprovedPolicyReference);
    }

    /// <summary>New positive use only. Historic source restore/cancel/release uses original bytes,
    /// not a fabricated active historic account. Does not attest external Finance reservation truth.</summary>
    internal static async Task<ProcurementAuthorityIdentity.CanonicalAccount> RequireCurrentCheckerAsync(
        PurchaseRequest request, BudgetCheckSnapshot finance, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var verifier = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, finance.CheckedBySubjectId, ct);
        if (verifier is null) throw new InvalidOperationException("FinanceEvidenceVerifierUnavailable");
        await ProcurementFinanceWorkerIndependence.RequireAsync(request, verifier.WorkerId, core, db, scope, ct);
        if (!FinanceCheckerIdentityEvidence.HasValidBinding(finance))
            throw new InvalidOperationException("FinanceEvidenceCheckerIdentityMissingOrInvalid");
        if (!FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(finance, scope.TenantId, scope.LegalEntityId,
                request.Id, request.SubmittedVersion ?? request.Version, verifier.PrincipalId, verifier.WorkerId))
            throw new InvalidOperationException("FinanceEvidenceCheckerIdentityChanged");
        await RequireCurrentBudgetPermissionAsync(core, verifier, ct);
        return verifier;
    }


    internal static async Task RequireCurrentBudgetPermissionAsync(HrmDbContext core,
        ProcurementAuthorityIdentity.CanonicalAccount verifier, CancellationToken ct)
    {
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, verifier.RolesCsv, ct);
        var admin = LocalIdentityRoutes.ParseRoles(verifier.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase);
        if (!admin && (!actions.Contains("procurement-access") || !actions.Contains("procurement-budget-check")))
            throw new InvalidOperationException("FinanceEvidenceVerifierPermissionRevoked");
    }

}
