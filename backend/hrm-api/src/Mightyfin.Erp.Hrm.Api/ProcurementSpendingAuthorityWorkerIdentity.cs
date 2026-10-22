using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owning-store bridge, not another identity authority. Load only persisted typed
/// Procurement evidence; resolve current people through Core. Callers must first establish
/// owning schema readiness, permissions, record scope and the coordinated command transaction.
/// Historical read/revoke paths do not require this bridge.</summary>
internal static class ProcurementSpendingAuthorityWorkerIdentity
{
    internal sealed record CurrentActors(ProcurementAuthorityIdentity.CanonicalAccount Actor,
        ProcurementAuthorityIdentity.CanonicalAccount Recipient);
    internal sealed record AuthorProof(SpendingAuthorityIdentityEvidence Created,
        ProcurementAuthorityIdentity.CanonicalAccount Actor, ProcurementAuthorityIdentity.CanonicalAccount Recipient);
    internal sealed record ReviewProof(SpendingAuthorityIdentityEvidence Created, SpendingAuthorityIdentityEvidence Submitted,
        ProcurementAuthorityIdentity.CanonicalAccount Actor, ProcurementAuthorityIdentity.CanonicalAccount Recipient);
    internal sealed record ActivationProof(SpendingAuthorityIdentityEvidence Created,
        SpendingAuthorityIdentityEvidence Submitted, SpendingAuthorityIdentityEvidence Activated);

    internal static async Task<CurrentActors> ResolveCreationActorsAsync(HrmDbContext core,
        ProcurementRequestScope scope, string recipientPrincipalId, CancellationToken ct)
        => new(await CurrentActor(core, scope, ct), await Canonical(core, scope, recipientPrincipalId, ct));

    internal static async Task<SpendingAuthorityIdentityEvidence> LoadCreatedAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
    {
        var created = await One(db, scope, grant, "Created", null, ct);
        SpendingAuthorityIdentity.VerifyOrigin(grant, created);
        return created;
    }

    internal static Task<SpendingAuthorityIdentityEvidence> LoadSubmittedAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
        => One(db,scope,grant,"Submitted",grant.Version,ct);

    internal static async Task<AuthorProof> RequireAuthorAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
    {
        var created = await LoadCreatedAsync(db, scope, grant, ct);
        var actor = await CurrentActor(core, scope, ct);
        var recipient = await Canonical(core, scope, grant.PrincipalId, ct);
        SpendingAuthorityIdentity.RequireAuthor(grant, created, actor.PrincipalId, actor.WorkerId);
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant, created, recipient.PrincipalId, recipient.WorkerId);
        return new(created, actor, recipient);
    }

    internal static async Task<ReviewProof> RequireReviewerAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
    {
        var created = await LoadCreatedAsync(db, scope, grant, ct);
        // Exact pending round, never an older matching submission chosen by ordering.
        var submitted = await One(db, scope, grant, "Submitted", grant.Version, ct);
        var actor = await CurrentActor(core, scope, ct);
        var recipient = await Canonical(core, scope, grant.PrincipalId, ct);
        SpendingAuthorityIdentity.RequireIndependentReviewer(grant, created, submitted, actor.PrincipalId, actor.WorkerId);
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant, created, recipient.PrincipalId, recipient.WorkerId);
        return new(created, submitted, actor, recipient);
    }

    /// <summary>Retained Active lineage only, for retiring a proved predecessor. It does not
    /// confer current spending eligibility or reactivate its historical people.</summary>
    internal static async Task<ActivationProof> RequireRetainedActivationAsync(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
    {
        var created = await LoadCreatedAsync(db, scope, grant, ct);
        var activated = await One(db, scope, grant, "Activated", null, ct);
        var submitted = await ById(db, scope, grant, activated.SubmittedEvidenceId
            ?? throw new InvalidOperationException("AuthorityHistoricalIdentityMissing"), ct);
        SpendingAuthorityIdentity.VerifyActivation(grant, created, submitted, activated);
        return new(created, submitted, activated);
    }

    /// <summary>Call AFTER the complete-family amount/scope evaluator selects a grant. Do not
    /// filter legacy grants before overlap/ambiguity checks or silently choose another version.
    /// Expected worker is a server-resolved candidate, rechecked here against current Core.</summary>
    internal static async Task RequireEligibleRecipientAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, string principalId,
        Guid expectedWorkerId, CancellationToken ct)
    {
        var proof = await RequireRetainedActivationAsync(db, scope, grant, ct);
        if (proof.Created.SchemaVersion != 1)
            throw new InvalidOperationException("AuthorityWorkerEvidenceRequired");
        var recipient = await Canonical(core, scope, principalId, ct);
        if (expectedWorkerId == Guid.Empty || recipient.WorkerId != expectedWorkerId)
            throw new InvalidOperationException("AuthorityRecipientIdentityChanged");
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant, proof.Created, recipient.PrincipalId, recipient.WorkerId);
    }

    private static async Task<ProcurementAuthorityIdentity.CanonicalAccount> CurrentActor(HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var actor = await Canonical(core, scope, scope.SubjectId, ct);
        if (scope.WorkerId is null || scope.WorkerId == Guid.Empty || actor.WorkerId != scope.WorkerId)
            throw new InvalidOperationException("AuthorityCurrentActorIdentityChanged");
        return actor;
    }

    private static async Task<ProcurementAuthorityIdentity.CanonicalAccount> Canonical(HrmDbContext core,
        ProcurementRequestScope scope, string principal, CancellationToken ct)
    {
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core,
            scope.TenantId, scope.LegalEntityId, principal, ct);
        if (account is null || account.WorkerId == Guid.Empty)
            throw new InvalidOperationException("AuthorityCanonicalAccountRequired");
        var unique = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core,
            scope.TenantId, scope.LegalEntityId, account.WorkerId, ct);
        if (unique is null || unique.PrincipalId != account.PrincipalId || unique.WorkerId != account.WorkerId)
            throw new InvalidOperationException("AuthorityCanonicalAccountRequired");
        return account;
    }

    private static IQueryable<SpendingAuthorityIdentityRecord> Owned(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant)
    {
        if (grant.TenantId != scope.TenantId || grant.LegalEntityId != scope.LegalEntityId || grant.Id == Guid.Empty)
            throw new InvalidOperationException("AuthorityIdentityEvidenceScopeMismatch");
        // Do not trust Local or an Unchanged attached envelope as retained provenance.
        return db.SpendingAuthorityIdentityRecords.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId && x.GrantId == grant.Id);
    }

    private static async Task<SpendingAuthorityIdentityEvidence> One(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, string stage, long? version, CancellationToken ct)
    {
        var query = Owned(db, scope, grant).Where(x => x.Stage == stage);
        if (version.HasValue) query = query.Where(x => x.GrantRecordVersionAtAction == version.Value);
        var rows = await query.Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("AuthorityHistoricalIdentityMissing");
        return SpendingAuthorityIdentityStorage.Read(rows[0]);
    }

    private static async Task<SpendingAuthorityIdentityEvidence> ById(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, Guid id, CancellationToken ct)
    {
        var rows = await Owned(db, scope, grant).Where(x => x.Id == id).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("AuthorityHistoricalIdentityMissing");
        return SpendingAuthorityIdentityStorage.Read(rows[0]);
    }
}
