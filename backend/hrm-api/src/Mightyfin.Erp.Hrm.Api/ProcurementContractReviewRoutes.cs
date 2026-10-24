using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Held contract review commands. MapRoutes is deliberately not called
/// until P35 schema, finite grants and actual Core transaction proof are captured.</summary>
public static partial class ProcurementContractReviewRoutes
{
    private sealed class Problem(string code, int status) : Exception(code) { internal int Status => status; }
    private sealed record Reply(string Body, int Status);
    public sealed record SubmitInput(long ExpectedContractVersion, Guid ReviewBranchId);

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/contracts");
        group.MapPost("/{id:guid}/internal-review/submit", Submit)
            .RequireAuthorization("procurement-contract-submit");
        group.MapGet("/internal-review/commands/{key:guid}", Recover)
            .RequireAuthorization("procurement-access");
    }

    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, CancellationToken, Task<Reply>> work,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var option = http.RequestServices.GetService<ProcurementContractReviewOptions>();
            var readiness = http.RequestServices.GetService<IProcurementContractReviewSchemaReadiness>();
            var approvals = http.RequestServices.GetService<ProcurementApprovalExecutionOptions>();
            if (option is not { Enabled: true, ExecutionDatabaseConfigured: true } || readiness is null
                || approvals is not { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true }
                || scope.OrgUnitId is not null)
                throw new Problem("ContractReviewUnavailable", 503);
            var result = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>()
                .ExecuteAsync(async (db, cases, token) =>
                {
                    if (!await readiness.CanRecordAsync(db, token)) throw new Problem("ContractReviewUnavailable", 503);
                    await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                    return await work(db, cases, core, token);
                }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is Problem or InvalidOperationException or UnauthorizedAccessException
            or DbUpdateException or JsonException || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem p ? p.Status : error is UnauthorizedAccessException ? 403
                : ProcurementPostgresFailures.IsConcurrency(error) ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503 : 409;
            return Results.Json(new { code = error is Problem ? error.Message
                    : status == 503 ? "ContractReviewSaveUnconfirmed" : "ContractReviewSourceChanged",
                message = status == 503
                    ? "The review command could not be confirmed. Keep its key and inspect the original result."
                    : "Refresh the contract and check its terms, company and current review access." }, statusCode: status);
        }
    }

    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key)
        && key != Guid.Empty ? key : throw new Problem("ContractReviewCommandKeyRequired", 400);

    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core,
        ProcurementRequestScope scope, string action, Guid branchId, CancellationToken ct)
    {
        if (branchId == Guid.Empty || scope.PrincipalId?.ToString("D") != scope.SubjectId
            || scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || scope.WorkLocationId is { } selected && selected != branchId)
            throw new Problem("ContractReviewScopeDenied", 403);
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || !person.Permissions.Contains(action)
            || !person.OrganisationWide && !person.AllowedBranches.Contains(branchId)
            || !await core.WorkLocations.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && x.Id == branchId && !x.IsArchived, ct))
            throw new Problem("ContractReviewScopeDenied", 403);
        return person;
    }

    private static async Task<Reply?> Replay(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, string operation, Guid key, string fingerprint,
        Guid? targetId, CancellationToken ct)
    {
        var rows = await db.ContractReviewCommandReceipts.AsNoTracking()
            .Where(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key)
            .Take(2).ToArrayAsync(ct);
        if (rows.Length == 0) return null;
        if (rows.Length != 1) throw new Problem("ContractReviewReceiptAmbiguous", 409);
        var receipt = rows[0];
        _ = await Actor(core, scope, receipt.Operation == "Submit"
            ? "procurement-contract-submit" : "procurement-contract-review", receipt.ReviewBranchId, ct);
        ContractReviewReceiptProof.Verify(receipt);
        if (receipt.Operation != operation || receipt.CommandSha256 != fingerprint
            || targetId is { } requested && receipt.ContractId != requested)
            throw new Problem("ContractReviewCommandKeyConflict", 409);
        return new(receipt.ResponseJson, receipt.ResponseStatus);
    }

    private static Task<IResult> Submit(Guid id, SubmitInput input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        return Run(http, scope, async (db, cases, core, token) =>
        {
            var key = Key(http);
            if (input is null || input.ExpectedContractVersion < 1 || input.ReviewBranchId == Guid.Empty)
                throw new Problem("ContractReviewSubmitInputInvalid", 422);
            var fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 1, operation = "Submit", id, actor = scope.SubjectId, input }));
            if (await Replay(db, core, scope, "Submit", key, fingerprint, id, token) is { } prior)
                return prior;
            var maker = await Actor(core, scope, "procurement-contract-submit", input.ReviewBranchId, token);
            var contract = await db.SupplierContracts.SingleOrDefaultAsync(x => x.Id == id
                && x.OwnerSubjectId == scope.SubjectId, token)
                ?? throw new Problem("ContractReviewContractNotFound", 404);
            if (contract.Status != "Draft" || contract.Version != input.ExpectedContractVersion)
                throw new Problem("ContractReviewDraftChanged", 409);
            var version = await db.SupplierContractVersions.SingleOrDefaultAsync(x => x.SupplierContractId == id
                && x.RevisionNumber == 1, token) ?? throw new Problem("ContractReviewVersionMissing", 409);
            var rates = await db.ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == version.Id)
                .ToArrayAsync(token);
            if (!await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == contract.SupplierId
                && (x.Status == "Active" || x.Status == "Approved") && x.IsOrderable, token))
                throw new Problem("ContractReviewSupplierUnavailable", 409);
            var now = DateTimeOffset.UtcNow;
            var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey
                && x.State == "Published" && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .Take(2).ToArrayAsync(token);
            if (policies.Length != 1) throw new Problem("ContractReviewPublishedPolicyRequired", 409);
            var policy = policies[0];
            await ProcurementPolicyDraftRoutes.ContractPolicyPublished(db, scope, policy, token);
            var rules = ProcurementContractReviewPolicyRules.Parse(policy.RulesJson);
            var named = new List<ContractReviewCurrentReviewer>();
            foreach (var principal in rules.Stages.SelectMany(x => x.PrincipalIds))
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principal, token);
                if (person is null) throw new Problem("ContractReviewReviewerUnavailable", 409);
                named.Add(new(person.PrincipalId, person.CoreLinkedWorkerId, person.OrganisationWide,
                    person.AllowedBranches, person.Permissions));
            }
            var count = await db.ContractReviewSubmissions.CountAsync(x => x.ContractId == id, token);
            var opened = ContractReviewCoreWorkflow.Open(contract, version, rates, policy,
                input.ReviewBranchId, maker.PrincipalId, maker.CoreLinkedWorkerId, named, checked(count + 1), now);
            contract.Status = "PendingReview"; version.Status = "PendingReview";
            cases.AddCase(opened.Case);
            db.AddScoped(opened.Submission);
            var at = opened.Submission.SubmittedAt;
            var receipt = new ContractReviewCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                ContractId = id, SubmissionId = opened.Submission.Id, ReviewBranchId = input.ReviewBranchId,
                Operation = "Submit", CommandKey = key, ActorPrincipalId = scope.SubjectId,
                CommandSha256 = fingerprint, ResponseStatus = 201,
                ResultContractVersion = checked(contract.Version + 1), CompletedAt = at,
                CreatedAt = at, CreatedBy = scope.SubjectId,
            };
            receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "PendingReview");
            receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
            db.AddScoped(receipt);
            await cases.SaveChangesAsync(token);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, 201);
        }, ct);
    }

    private static Task<IResult> Recover(Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct) => Run(http, scope, async (db, cases, core, token) =>
    {
        var row = await db.ContractReviewCommandReceipts.AsNoTracking()
            .Where(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key)
            .Select(x => new { x.Operation }).SingleOrDefaultAsync(token);
        if (row is null) throw new Problem("ContractReviewReceiptNotFound", 404);
        return await Replay(db, core, scope, row.Operation, key,
            await db.ContractReviewCommandReceipts.AsNoTracking().Where(x => x.ActorPrincipalId == scope.SubjectId
                && x.CommandKey == key).Select(x => x.CommandSha256).SingleAsync(token), null, token)
            ?? throw new Problem("ContractReviewReceiptNotFound", 404);
    }, ct);
}
