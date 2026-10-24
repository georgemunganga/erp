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
        ProcurementPrincipalPeople.Person Actor);
    internal sealed record ReviewProof(OperationalPolicyWorkerIdentityEvidence Created,
        OperationalPolicyWorkerIdentityEvidence Submitted, ProcurementPrincipalPeople.Person Actor);
    internal sealed record PublicationProof(OperationalPolicyWorkerIdentityEvidence Created,
        OperationalPolicyWorkerIdentityEvidence Submitted, OperationalPolicyWorkerIdentityEvidence Published);

    internal static async Task<ProcurementPrincipalPeople.Person> CurrentActorAsync(HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (permission is not (ConfigurePermission or ReviewPermission))
            throw new InvalidOperationException("OperationalPolicyConfigurationPermissionUnsupported");
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || scope.PrincipalId is not { } principal || actor.PrincipalId != principal.ToString("D")
            || !actor.Permissions.Contains(permission))
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
        OperationalPolicyWorkerIdentity.RequireAuthor(policy, created, actor.PrincipalId, actor.EvidenceWorker(created.SchemaVersion));
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
        OperationalPolicyWorkerIdentity.RequireIndependent(policy, created, submitted, actor.PrincipalId, actor.EvidenceWorker(created.SchemaVersion));
        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId, created.ActorPrincipalId, created.ActorWorkerId, actor.PrincipalId, actor.WorkerId, ct)
            || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId, submitted.ActorPrincipalId, submitted.ActorWorkerId, actor.PrincipalId, actor.WorkerId, ct))
            throw new InvalidOperationException("OperationalPolicyIndependentReviewerRequired");
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
            || policy.Id == Guid.Empty || policy.PolicyKey is not ("RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "MANUAL_PAYMENT_REVIEW" or PrincipalRfqCommercialRoundPolicyRules.PolicyKey)
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
