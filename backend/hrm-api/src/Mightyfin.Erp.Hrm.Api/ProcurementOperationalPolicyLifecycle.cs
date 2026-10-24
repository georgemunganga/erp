using System.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static Task<IResult> Create(PolicyDraft input, ProcurementDbContext db, HrmDbContext organizationDb,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
        => RunOperationalPolicyMutation(input.PolicyKey?.Trim().ToUpperInvariant(), null, db, organizationDb, scope, http, ct,
            core => CreatePolicy(input, db, core, scope, http, ct));
    private static Task<IResult> Update(Guid id, VersionedPolicyDraft input, ProcurementDbContext db, HrmDbContext organizationDb,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
        => RunOperationalPolicyMutation(null, id, db, organizationDb, scope, http, ct,
            core => UpdatePolicy(id, input, db, core, scope, http, ct));
    private static Task<IResult> SubmitReview(Guid id, ReviewSubmission input, ProcurementDbContext db, HrmDbContext organizationDb,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
        => RunOperationalPolicyMutation(null, id, db, organizationDb, scope, http, ct,
            core => SubmitReviewPolicy(id, input, db, core, scope, http, ct));
    private static Task<IResult> Publish(Guid id, PublicationInput input, ProcurementDbContext db, HrmDbContext organizationDb,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
        => RunOperationalPolicyMutation(null, id, db, organizationDb, scope, http, ct,
            core => PublishPolicy(id, input, db, core, scope, http, ct));
    private static Task<IResult> ReturnForChanges(Guid id, PublicationInput input, ProcurementDbContext db, HrmDbContext organizationDb,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
        => RunOperationalPolicyMutation(null, id, db, organizationDb, scope, http, ct,
            core => ReturnForChangesPolicy(id, input, db, core, scope, http, ct));

    private static bool IsOperationalIdentityPolicy(string? key) => key is "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "MANUAL_PAYMENT_REVIEW";

    private static async Task<IResult> RunOperationalPolicyMutation(string? createKey, Guid? id,
        ProcurementDbContext db, HrmDbContext originalCore, ProcurementRequestScope scope, HttpContext http,
        CancellationToken ct, Func<HrmDbContext, Task<IResult>> next)
    {
        // Discovery does not track a policy or confer authority. Re-read inside the owner snapshot.
        string? key;
        try
        {
            key = id.HasValue
                ? await db.PolicyVersions.AsNoTracking().Where(x => x.Id == id.Value).Select(x => x.PolicyKey).SingleOrDefaultAsync(ct)
                : createKey;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Problem("policy-storage-unavailable", "Policy storage is unavailable. No change was saved.", 503); }
        if (IsContractReviewPolicy(key))
            return await RunContractReviewPolicyMutation(id, db, scope, http, ct, next);
        if (!IsOperationalIdentityPolicy(key)) return await next(originalCore);
        if (key == ManualPaymentReviewPolicyRules.PolicyKey
            && (http.RequestServices.GetService<IProcurementManualPaymentGovernanceReadiness>() is not { } payments
                || !await payments.IsReadyAsync(ct)))
            return Problem("payment-review-unavailable", "Payment review settings are not ready in this workspace yet. No change was saved.", 503);
        try
        {
            if (!db.Database.IsNpgsql()) return OperationalIdentityUnavailable();
            if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries<PolicyVersion>().Any()
                || db.ChangeTracker.Entries<OperationalPolicyIdentityRecord>().Any())
                return Problem("operational-policy-context-conflict", "Refresh this policy before trying again.", 409);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await RequireOperationalIdentitySchema(http, db, true, ct);
            if (key == InvoiceRetainedUnitReview.ReviewPolicyKey) await RequireUnitReviewPolicySchema(http, db, ct);
            if (id.HasValue)
            {
                var currentKey = await db.PolicyVersions.AsNoTracking().Where(x => x.Id == id.Value).Select(x => x.PolicyKey).SingleOrDefaultAsync(ct);
                if (currentKey is null) return Problem("not-found", "Buying policy not found.", 404);
                if (currentKey != key) return Problem("operational-policy-context-conflict", "Refresh this policy before trying again.", 409);
            }
            await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
            var result = await next(core);
            if (result is not IStatusCodeHttpResult { StatusCode: { } status })
                return Problem("operational-policy-outcome-unavailable", "The policy outcome could not be confirmed. Refresh it before trying again.", 503);
            if (status is >= 200 and < 300) await transaction.CommitAsync(ct);
            return result; // Other results dispose/roll back the whole command.
        }
        catch (OperationalIdentityProblem problem) { return Problem(problem.Code, problem.Message, problem.Status); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("OperationalPolicy", StringComparison.Ordinal))
        { return OperationalIdentityFailure(error); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Problem("version-conflict", "Another policy changed. Refresh this policy before trying again.", 409); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Problem("operational-policy-storage-unavailable", "Policy controls are unavailable. No change was confirmed; refresh before trying again.", 503); }
    }

    private sealed class OperationalIdentityProblem(string code, string message, int status) : Exception(message)
    {
        internal string Code { get; } = code;
        internal int Status { get; } = status;
    }
    private static IResult OperationalIdentityUnavailable()
        => Problem("operational-policy-identity-unavailable", "Operational policy identity controls are not ready. No change was saved.", 503);
    private static async Task RequireOperationalIdentitySchema(HttpContext http, ProcurementDbContext db, bool write, CancellationToken ct)
    {
        var readiness = http.RequestServices.GetService<IProcurementOperationalPolicyIdentitySchemaReadiness>();
        if (readiness is null || !(write ? await readiness.CanRecordAsync(db, ct) : await readiness.IsReadyAsync(db, ct)))
            throw new OperationalIdentityProblem("operational-policy-identity-unavailable", "Operational policy identity controls are not ready. No change was saved.", 503);
    }
    private static IResult OperationalIdentityFailure(InvalidOperationException error)
    {
        var denied = error.Message is "OperationalPolicyOriginalAuthorRequired" or "OperationalPolicyIndependentReviewerRequired"
            or "OperationalPolicyCurrentActorIdentityChanged" or "OperationalPolicyCanonicalActorRequired"
            or "OperationalPolicyConfigurationAccessDenied";
        return denied
            ? Problem("operational-policy-person-denied", "Your current person cannot perform this policy action. Original-author and independent-review rules still apply.", 403)
            : Problem("operational-policy-identity-conflict", "This policy's retained identity could not be verified. Its history is preserved; refresh or use the controlled reconciliation process.", 409);
    }
}
