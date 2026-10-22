using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owning-store/current Core bridge for operational-policy identity evidence.
/// Caller owns schema readiness, named HTTP authorization, transaction, rule validation,
/// access to the policy and current effective-time eligibility. No evidence grants permission.</summary>
internal static class ProcurementOperationalPolicyWorkerIdentity
{
    internal const string ConfigurePermission = "procurement-configuration-edit";
    internal const string ReviewPermission = "procurement-configuration-approve";
    internal sealed record AuthorProof(OperationalPolicyWorkerIdentityEvidence Created,
        ProcurementAuthorityIdentity.CanonicalAccount Actor);
    internal sealed record ReviewProof(OperationalPolicyWorkerIdentityEvidence Created,
        OperationalPolicyWorkerIdentityEvidence Submitted, ProcurementAuthorityIdentity.CanonicalAccount Actor);
    internal sealed record PublicationProof(OperationalPolicyWorkerIdentityEvidence Created,
        OperationalPolicyWorkerIdentityEvidence Submitted, OperationalPolicyWorkerIdentityEvidence Published);

    internal static async Task<ProcurementAuthorityIdentity.CanonicalAccount> CurrentActorAsync(HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (permission is not (ConfigurePermission or ReviewPermission))
            throw new InvalidOperationException("OperationalPolicyConfigurationPermissionUnsupported");
        var actor = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || actor.WorkerId == Guid.Empty || scope.WorkerId != actor.WorkerId)
            throw new InvalidOperationException("OperationalPolicyCurrentActorIdentityChanged");
        var unique = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core,
            scope.TenantId, scope.LegalEntityId, actor.WorkerId, ct);
        if (unique is null || unique.PrincipalId != actor.PrincipalId || unique.WorkerId != actor.WorkerId)
            throw new InvalidOperationException("OperationalPolicyCanonicalActorRequired");
        var roles = LocalIdentityRoutes.ParseRoles(actor.RolesCsv);
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, actor.RolesCsv, ct);
        if (!roles.Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
            && (!actions.Contains("procurement-access", StringComparer.Ordinal) || !actions.Contains(permission, StringComparer.Ordinal)))
            throw new InvalidOperationException("OperationalPolicyConfigurationAccessDenied");
        return actor;
    }

    internal static async Task<OperationalPolicyWorkerIdentityEvidence> LoadCreatedAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var created = await One(db, scope, policy, "Created", null, ct);
        OperationalPolicyWorkerIdentity.VerifyOrigin(policy, created);
        return created;
    }

    internal static async Task<AuthorProof> RequireAuthorAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, string configurePermission, CancellationToken ct)
    {
        if (configurePermission != ConfigurePermission)
            throw new InvalidOperationException("OperationalPolicyConfigurationPermissionUnsupported");
        var actor = await CurrentActorAsync(core, scope, configurePermission, ct);
        var created = await LoadCreatedAsync(db, scope, policy, ct);
        OperationalPolicyWorkerIdentity.RequireAuthor(policy, created, actor.PrincipalId, actor.WorkerId);
        return new(created, actor);
    }

    internal static async Task<ReviewProof> RequireReviewerAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, string reviewPermission, CancellationToken ct)
    {
        if (reviewPermission != ReviewPermission)
            throw new InvalidOperationException("OperationalPolicyConfigurationPermissionUnsupported");
        var actor = await CurrentActorAsync(core, scope, reviewPermission, ct);
        var created = await LoadCreatedAsync(db, scope, policy, ct);
        var submitted = await One(db, scope, policy, "Submitted", policy.Version, ct);
        OperationalPolicyWorkerIdentity.RequireIndependent(policy, created, submitted, actor.PrincipalId, actor.WorkerId);
        return new(created, submitted, actor);
    }

    /// <summary>Retained proof only: never resolves old people's current account mappings.
    /// Historical mode does not authorize a new operation or waive current policy selection.</summary>
    internal static async Task<PublicationProof> VerifyPublicationAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct,
        OperationalPolicyPublicationMode mode = OperationalPolicyPublicationMode.CurrentPublished)
    {
        var created = await LoadCreatedAsync(db, scope, policy, ct);
        var published = await One(db, scope, policy, "Published", null, ct);
        var submittedId = published.SubmittedEvidenceId
            ?? throw new InvalidOperationException("OperationalPolicyHistoricalIdentityMissing");
        var rows = await Owned(db, scope, policy).Where(x => x.Id == submittedId).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("OperationalPolicyHistoricalIdentityMissing");
        var submitted = OperationalPolicyIdentityStorage.Read(rows[0]);
        OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, published, mode);
        return new(created, submitted, published);
    }

    private static IQueryable<OperationalPolicyIdentityRecord> Owned(ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy)
    {
        if (policy.TenantId != scope.TenantId || policy.LegalEntityId != scope.LegalEntityId
            || policy.Id == Guid.Empty || policy.PolicyKey is not ("RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW")
            || policy.PolicyVersionNumber <= 0)
            throw new InvalidOperationException("OperationalPolicyIdentityScopeMismatch");
        // Never accept ChangeTracker.Local or caller-attached evidence as retained provenance.
        return db.OperationalPolicyIdentityRecords.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId && x.PolicyKey == policy.PolicyKey
            && x.PolicyVersionId == policy.Id && x.PolicyVersionNumber == policy.PolicyVersionNumber);
    }

    private static async Task<OperationalPolicyWorkerIdentityEvidence> One(ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, string stage, long? revision, CancellationToken ct)
    {
        var query = Owned(db, scope, policy).Where(x => x.Stage == stage);
        if (revision.HasValue) query = query.Where(x => x.PolicyRecordVersionAtAction == revision.Value);
        var rows = await query.Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("OperationalPolicyHistoricalIdentityMissing");
        return OperationalPolicyIdentityStorage.Read(rows[0]);
    }
}
