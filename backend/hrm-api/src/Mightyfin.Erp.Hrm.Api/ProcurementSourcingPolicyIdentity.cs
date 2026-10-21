using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementSourcingPolicyIdentitySchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
}
// Source gate only. Replaced by actual owning-store P29 schema/privilege/transaction proof after review.
public sealed class UnavailableSourcingPolicyIdentitySchema : IProcurementSourcingPolicyIdentitySchemaReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(false);
}

public static partial class ProcurementPolicyDraftRoutes
{
    private sealed class SourcingIdentityProblem(string code, string message, int status) : Exception(message)
    { public string Code { get; } = code; public int Status { get; } = status; }
    private static async Task RequireSourcingIdentitySchema(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        var readiness = http.RequestServices.GetService<IProcurementSourcingPolicyIdentitySchemaReadiness>();
        if (readiness is null || !await readiness.IsReadyAsync(db, ct))
            throw new SourcingIdentityProblem("sourcing-policy-identity-unavailable", "Sourcing policy identity controls are not ready. No change was saved.", 503);
    }
    private static async Task<SourcingPolicyIdentityEvidence> SourcingOrigin(ProcurementDbContext db, PolicyVersion policy, CancellationToken ct)
    {
        var row = await db.SourcingPolicyIdentityEvidenceRecords.SingleOrDefaultAsync(x => x.PolicyVersionId == policy.Id && x.Stage == "Created", ct);
        if (row is null) throw new SourcingIdentityProblem("sourcing-policy-identity-unknown", "This older policy has no retained maker identity. Create a new policy version; its history is preserved.", 409);
        try { SourcingPolicyIdentity.VerifyOrigin(policy, row); }
        catch (InvalidOperationException) { throw IdentityConflict(); }
        return row;
    }
    private static async Task<(SourcingPolicyIdentityEvidence Origin, ProcurementAuthorityIdentity.CanonicalAccount Actor)> SourcingOriginalAuthor(
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var origin = await SourcingOrigin(db, policy, ct);
        var actor = await SourcingCurrentActor(core, scope, "procurement-configuration-edit", ct);
        try { SourcingPolicyIdentity.RequireAuthor(policy, origin, actor.PrincipalId, actor.WorkerId); }
        catch (InvalidOperationException) { throw new SourcingIdentityProblem("sourcing-policy-original-author-required", "Only the original author, with the original worker identity, can edit or submit this sourcing policy.", 403); }
        return (origin, actor);
    }
    private static async Task<ProcurementAuthorityIdentity.CanonicalAccount> SourcingCurrentActor(HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (!await ReceivingConfigurationActor(core, scope, permission, ct))
            throw new SourcingIdentityProblem("sourcing-configuration-unavailable", "Your current account cannot perform this policy action.", 403);
        return await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct)
            ?? throw new SourcingIdentityProblem("sourcing-configuration-unavailable", "Your current account is unavailable.", 403);
    }
    private static async Task<(SourcingPolicyIdentityEvidence Origin, SourcingPolicyIdentityEvidence Submitted,
        ProcurementAuthorityIdentity.CanonicalAccount Actor)> SourcingIndependentPolicyReviewer(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var origin = await SourcingOrigin(db, policy, ct);
        var submitted = await db.SourcingPolicyIdentityEvidenceRecords.SingleOrDefaultAsync(x => x.PolicyVersionId == policy.Id
            && x.Stage == "Submitted" && x.PolicyRecordVersionAtAction == policy.Version, ct);
        if (submitted is null) throw IdentityConflict();
        try { SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted); }
        catch (InvalidOperationException) { throw IdentityConflict(); }
        var actor = await SourcingCurrentActor(core, scope, "procurement-configuration-approve", ct);
        try { SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, actor.PrincipalId, actor.WorkerId); }
        catch (InvalidOperationException) { throw new SourcingIdentityProblem("self-approval", "Another current person, independent of the original maker and submitter, must review this policy.", 403); }
        return (origin, submitted, actor);
    }
    private static SourcingIdentityProblem IdentityConflict() => new("sourcing-policy-identity-conflict",
        "The retained policy review evidence does not match this revision. Refresh or start a new controlled policy version.", 409);
}
