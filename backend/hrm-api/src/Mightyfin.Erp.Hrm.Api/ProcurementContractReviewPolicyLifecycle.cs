using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementContractReviewOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementContractReviewSchemaReadiness
{
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}

public sealed class UnavailableProcurementContractReviewSchemaReadiness : IProcurementContractReviewSchemaReadiness
{
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>Uses the existing policy lifecycle and Core transaction; no contract policy
/// write is possible until genuine P35 schema/finite-role admission replaces the unavailable provider.</summary>
public static partial class ProcurementPolicyDraftRoutes
{
    private static bool IsContractReviewPolicy(string? key) => key == ProcurementContractReviewPolicyRules.PolicyKey;

    private static async Task<IResult> RunContractReviewPolicyMutation(Guid? id, ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct,
        Func<HrmDbContext, Task<IResult>> next)
    {
        var options = http.RequestServices.GetService<ProcurementContractReviewOptions>();
        var readiness = http.RequestServices.GetService<IProcurementContractReviewSchemaReadiness>();
        if (options is not { Enabled: true, ExecutionDatabaseConfigured: true } || readiness is null)
            return Problem("contract-review-unavailable", "Contract review settings are not ready. No change was saved.", 503);
        try
        {
            if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction is not null)
                return Problem("contract-review-unavailable", "Contract review settings are not ready. No change was saved.", 503);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await readiness.CanRecordAsync(db, ct))
                return Problem("contract-review-unavailable", "Contract review settings are not ready. No change was saved.", 503);
            if (id.HasValue)
            {
                var currentKey = await db.PolicyVersions.AsNoTracking().Where(x => x.Id == id.Value)
                    .Select(x => x.PolicyKey).SingleOrDefaultAsync(ct);
                if (currentKey is null) return Problem("not-found", "Buying policy not found.", 404);
                if (!IsContractReviewPolicy(currentKey))
                    return Problem("contract-review-context-conflict", "Refresh this policy before trying again.", 409);
            }
            await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
            var outcome = await next(core);
            if (outcome is not IStatusCodeHttpResult { StatusCode: { } status })
                return Problem("contract-review-outcome-unknown", "Refresh this policy before trying again.", 503);
            if (status is >= 200 and < 300) await transaction.CommitAsync(ct);
            return outcome;
        }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Problem("version-conflict", "Another setting changed. Refresh before saving.", 409); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Problem("contract-review-unavailable", "Contract review settings are unavailable. Refresh before trying again.", 503); }
    }

    private static async Task<OperationalPolicyWorkerIdentityEvidence> ContractPolicyOrigin(
        ProcurementDbContext db, ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var rows = await db.ContractReviewPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policy.Id
            && x.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey && x.Stage == "Created")
            .Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("ContractReviewPolicyOriginUnavailable");
        var origin = ContractReviewPolicyIdentityStorage.Read(rows[0]);
        if (origin.TenantId != scope.TenantId || origin.LegalEntityId != scope.LegalEntityId)
            throw new InvalidOperationException("ContractReviewPolicyScopeChanged");
        OperationalPolicyWorkerIdentity.VerifyOrigin(policy, origin);
        return origin;
    }

    private static async Task<(OperationalPolicyWorkerIdentityEvidence Origin,
        OperationalPolicyWorkerIdentityEvidence Submitted)> ContractPolicySubmitted(
        ProcurementDbContext db, ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var origin = await ContractPolicyOrigin(db, scope, policy, ct);
        var rows = await db.ContractReviewPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policy.Id
            && x.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey && x.Stage == "Submitted"
            && x.PolicyRecordVersionAtAction == policy.Version).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new InvalidOperationException("ContractReviewPolicySubmittedUnavailable");
        var submitted = ContractReviewPolicyIdentityStorage.Read(rows[0]);
        OperationalPolicyWorkerIdentity.VerifySubmission(policy, origin, submitted);
        return (origin, submitted);
    }

    internal static async Task ContractPolicyPublished(ProcurementDbContext db,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct,
        OperationalPolicyPublicationMode mode = OperationalPolicyPublicationMode.CurrentPublished)
    {
        var origin = await ContractPolicyOrigin(db, scope, policy, ct);
        var publishedRows = await db.ContractReviewPolicyIdentityRecords.AsNoTracking()
            .Where(x => x.PolicyVersionId == policy.Id && x.Stage == "Published").Take(2).ToArrayAsync(ct);
        if (publishedRows.Length != 1) throw new InvalidOperationException("ContractReviewPolicyPublicationUnavailable");
        var published = ContractReviewPolicyIdentityStorage.Read(publishedRows[0]);
        var submittedRows = await db.ContractReviewPolicyIdentityRecords.AsNoTracking()
            .Where(x => x.PolicyVersionId == policy.Id && x.Id == published.SubmittedEvidenceId)
            .Take(2).ToArrayAsync(ct);
        if (submittedRows.Length != 1) throw new InvalidOperationException("ContractReviewPolicySubmissionUnavailable");
        var submitted = ContractReviewPolicyIdentityStorage.Read(submittedRows[0]);
        OperationalPolicyWorkerIdentity.VerifyPublication(policy, origin, submitted, published, mode);
    }

    private static async Task<ProcurementPrincipalPeople.Person> ContractPolicyActor(HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
        => await ProcurementOperationalPolicyWorkerIdentity.CurrentActorAsync(core, scope, permission, ct);

    private sealed record ContractAuthor(OperationalPolicyWorkerIdentityEvidence Created,
        ProcurementPrincipalPeople.Person Actor);
    private sealed record ContractReviewer(OperationalPolicyWorkerIdentityEvidence Created,
        OperationalPolicyWorkerIdentityEvidence Submitted, ProcurementPrincipalPeople.Person Actor);

    private static async Task<ContractAuthor> ContractPolicyAuthor(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var actor = await ContractPolicyActor(core, scope, "procurement-configuration-edit", ct);
        var origin = await ContractPolicyOrigin(db, scope, policy, ct);
        OperationalPolicyWorkerIdentity.RequireAuthor(policy, origin, actor.PrincipalId, actor.EvidenceWorker(2));
        return new(origin, actor);
    }

    private static async Task<ContractReviewer> ContractPolicyReviewer(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        var actor = await ContractPolicyActor(core, scope, "procurement-configuration-approve", ct);
        var (origin, submitted) = await ContractPolicySubmitted(db, scope, policy, ct);
        OperationalPolicyWorkerIdentity.RequireIndependent(policy, origin, submitted,
            actor.PrincipalId, actor.EvidenceWorker(2));
        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                origin.ActorPrincipalId, origin.ActorWorkerId, actor.PrincipalId, actor.CoreLinkedWorkerId, ct)
            || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                submitted.ActorPrincipalId, submitted.ActorWorkerId, actor.PrincipalId, actor.CoreLinkedWorkerId, ct))
            throw new InvalidOperationException("ContractReviewPolicyIndependentPublisherRequired");
        return new(origin, submitted, actor);
    }

    private static async Task<IResult?> CheckContractReviewCompanyRules(PolicyVersion policy,
        HrmDbContext core, ProcurementRequestScope scope, bool draft, CancellationToken ct)
    {
        var issues = ProcurementContractReviewPolicyRules.Validate(policy.RulesJson, draft);
        if (issues.Count != 0) return Results.Json(new { code = "invalid-contract-review-rules",
            message = "Choose named reviewers and a valid stage mode.", issues }, statusCode: 422);
        if (!await core.LegalEntities.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
            && x.Id == scope.LegalEntityId && !x.IsArchived, ct))
            return Problem("company-unavailable", "The selected company is unavailable.", 409);
        if (draft) return null;
        var rules = ProcurementContractReviewPolicyRules.Parse(policy.RulesJson);
        foreach (var principal in rules.Stages.SelectMany(x => x.PrincipalIds))
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId,
                principal, ct);
            if (person is null || !person.Permissions.Contains("procurement-contract-review")
                || !person.Permissions.Contains("procurement-access")
                || !person.OrganisationWide && person.AllowedBranches.Count == 0)
                return Problem("reviewer-unavailable", "Choose a current reviewer for this company.", 422);
        }
        return null;
    }
}
