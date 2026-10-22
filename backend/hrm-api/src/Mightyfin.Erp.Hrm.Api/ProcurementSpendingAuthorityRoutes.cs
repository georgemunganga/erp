using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Application;
using Npgsql;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Financial authority configuration, not purchasing approval execution.</summary>
public static partial class ProcurementSpendingAuthorityRoutes
{
    public const string ReadPermission = "procurement-authority-read";
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/configuration/spending-authorities");
        group.MapGet("", List).RequireAuthorization(ReadPermission);
        group.MapGet("/{id:guid}", Detail).RequireAuthorization(ReadPermission);
        group.MapGet("/lookups/{kind}", Lookups).RequireAuthorization(ReadPermission);
        group.MapPost("", Create).RequireAuthorization(SpendingAuthorityConfiguration.ConfigurePermission);
        group.MapPut("/{id:guid}", Edit).RequireAuthorization(SpendingAuthorityConfiguration.ConfigurePermission);
        group.MapPost("/{id:guid}/submit", (Guid id, VersionInput input, HttpContext http,
            ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            IAuthorizationService auth, CancellationToken ct) => Command("submit", id, input.Version,
                null, null, null, http, db, core, scope, auth, ct))
            .RequireAuthorization(SpendingAuthorityConfiguration.ConfigurePermission);
        group.MapPost("/{id:guid}/return", (Guid id, ReviewInput input, HttpContext http,
            ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            IAuthorizationService auth, CancellationToken ct) => Command("return", id, input.Version,
                input.Reason, null, null, http, db, core, scope, auth, ct))
            .RequireAuthorization(SpendingAuthorityConfiguration.ReviewPermission);
        group.MapPost("/{id:guid}/activate", (Guid id, ReviewInput input, HttpContext http,
            ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            IAuthorizationService auth, CancellationToken ct) => Command("activate", id, input.Version,
                input.Reason, null, null, http, db, core, scope, auth, ct))
            .RequireAuthorization(SpendingAuthorityConfiguration.ReviewPermission);
        group.MapPost("/{id:guid}/revoke", (Guid id, ReviewInput input, HttpContext http,
            ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            IAuthorizationService auth, CancellationToken ct) => Command("revoke", id, input.Version,
                input.Reason, null, null, http, db, core, scope, auth, ct))
            .RequireAuthorization(SpendingAuthorityConfiguration.RevokePermission);
        group.MapPost("/{id:guid}/supersede", (Guid id, SupersedeInput input, HttpContext http,
            ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            IAuthorizationService auth, CancellationToken ct) => Command("supersede", id, input.Version,
                input.Reason, input.PreviousGrantId, input.PreviousVersion, http, db, core, scope, auth, ct))
            .RequireAuthorization(SpendingAuthorityConfiguration.ReviewPermission);
    }

    private static async Task<IResult> List(string? state, string? action, int? page, int? pageSize,
        HttpContext http, ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            if (await Actor(core, scope, http, auth, ReadPermission, ct) is null) return AccessDenied();
            if (!TryReadContract(action, out var contract)) return Invalid("AuthorityActionUnsupported");
            var query = ProcurementAuthorityIdentity.InScope(db.SpendingAuthorityGrants.AsNoTracking(), scope)
                .Where(x => x.Action == contract.Action && x.Permission == contract.Permission);
            if (!string.IsNullOrWhiteSpace(state)) query = query.Where(x => x.State == state);
            var number = Math.Clamp(page ?? 1, 1, 100000);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = await query.CountAsync(ct);
            var records = await query.Include(x => x.Categories).OrderBy(x => x.AuthorityReference).ThenByDescending(x => x.AuthorityVersion)
                .Skip((number - 1) * size).Take(size).ToListAsync(ct);
            var permissions = await ReadActions(core, scope, http, auth, ct);
            // Inspect the actual store once per request, not once per listed grant.
            var identityReady = await IdentityStoreReady(http, db, true, ct);
            var items = new List<object>();
            foreach (var grant in records) items.Add(await ReadView(grant, db, core, scope, permissions, identityReady, ct));
            return Results.Ok(new { items, total, page = number, pageSize = size,
                currentActorPrincipalId = scope.SubjectId, canCreate = permissions.Contains(SpendingAuthorityConfiguration.ConfigurePermission)
                    && identityReady,
                createAction = contract.Action, createAmountBasis = contract.AmountBasis,
                scope = new { branchId = scope.WorkLocationId, departmentId = scope.OrgUnitId, confined = scope.IsConfined } });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Detail(Guid id, HttpContext http, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            if (await Actor(core, scope, http, auth, ReadPermission, ct) is null) return AccessDenied();
            var grant = await Scoped(db, scope).AsNoTracking().Include(x => x.Categories)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            return grant is null ? Missing() : Results.Ok(await ReadView(grant, db, core, scope,
                await ReadActions(core, scope, http, auth, ct), await IdentityStoreReady(http, db, true, ct), ct));
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Create(CreateInput input, HttpContext http, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            var actor = await Actor(core, scope, http, auth, SpendingAuthorityConfiguration.ConfigurePermission, ct);
            if (actor is null) return AccessDenied();
            if (string.IsNullOrWhiteSpace(input.AuthorityReference) || string.IsNullOrWhiteSpace(input.PrincipalId)
                || input.Draft is null) return Invalid("AuthorityDraftRequired");
            if (await ProcurementAuthorityIdentity.SubjectAsync(core, scope, input.PrincipalId, ct) is null)
                return Invalid("AuthoritySubjectUnavailable");
            var contract = ProcurementSpendingActions.Resolve(input.Action ?? SpendingAuthorityConfiguration.ApprovalAction);
            var draft = await Defaults(input.Draft, core, scope, ct, contract.Action);
            if (!ProcurementAuthorityIdentity.InputInScope(draft, scope)) return Missing();
            var issue = await ProcurementAuthorityIdentity.ReferenceIssueAsync(core, db, scope, draft, false, ct);
            if (issue is not null) return Invalid(issue);
            await using var tx = await SpendingAuthorityFamilyTransaction.BeginAsync(db, scope.TenantId,
                scope.LegalEntityId, input.PrincipalId, contract.Permission,
                contract.Action, input.AuthorityReference, ct);
            await using var transactionCore = await TransactionalCore(db, scope, tx, ct);
            core = transactionCore;
            actor = await Actor(core, scope, http, auth, SpendingAuthorityConfiguration.ConfigurePermission, ct);
            if (actor is null) return AccessDenied();
            if (await ProcurementAuthorityIdentity.SubjectAsync(core, scope, input.PrincipalId, ct) is null)
                return Invalid("AuthoritySubjectUnavailable");
            draft = await Defaults(input.Draft, core, scope, ct, contract.Action);
            issue = await ProcurementAuthorityIdentity.ReferenceIssueAsync(core, db, scope, draft, false, ct);
            if (issue is not null) return Invalid(issue);
            var family = await SpendingAuthorityFamilyTransaction.LoadAsync(db, input.PrincipalId,
                contract.Permission, contract.Action,
                input.AuthorityReference, ct);
            if (!await IdentityStoreReady(http, db, true, ct)) return IdentityUnavailable();
            var grant = SpendingAuthorityConfiguration.CreateDraft(input.AuthorityReference, input.PrincipalId,
                draft, actor, ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value, family, contract.Action);
            var identities = await ProcurementSpendingAuthorityWorkerIdentity.ResolveCreationActorsAsync(
                core, scope, grant.PrincipalId, ct);
            db.AddScoped(grant);
            db.AddScoped(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureCreation(
                grant, identities.Actor.WorkerId, identities.Recipient.WorkerId)));
            ProcurementDraftAudit.Add(db, scope, nameof(ProcurementSpendingAuthorityGrant), grant.Id,
                "authority.draft_created", null, View(grant));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/procurement/v1/configuration/spending-authorities/{grant.Id}", View(grant));
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Edit(Guid id, EditInput input, HttpContext http, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            var actor = await Actor(core, scope, http, auth, SpendingAuthorityConfiguration.ConfigurePermission, ct);
            if (actor is null) return AccessDenied();
            var key = await Scoped(db, scope).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (key is null) return Missing();
            if (input.Draft is null) return Invalid("AuthorityDraftRequired");
            await using var tx = await Begin(db, scope, key, ct);
            await using var transactionCore = await TransactionalCore(db, scope, tx, ct);
            core = transactionCore;
            var family = await Load(db, key, ct);
            var grant = family.SingleOrDefault(x => x.Id == id);
            if (grant is null || !InScope(grant, scope)) return Missing();
            actor = await Actor(core, scope, http, auth, SpendingAuthorityConfiguration.ConfigurePermission, ct);
            if (actor is null) return AccessDenied();
            var draft = await Defaults(input.Draft, core, scope, ct, grant.Action);
            if (!ProcurementAuthorityIdentity.InputInScope(draft, scope)) return Missing();
            var issue = await ProcurementAuthorityIdentity.ReferenceIssueAsync(core, db, scope, draft, false, ct);
            if (issue is not null) return Invalid(issue);
            if (!await IdentityStoreReady(http, db, true, ct)) return IdentityUnavailable();
            await ProcurementSpendingAuthorityWorkerIdentity.RequireAuthorAsync(core, db, scope, grant, ct);
            var before = View(grant);
            db.SpendingAuthorityCategories.RemoveRange(grant.Categories);
            SpendingAuthorityConfiguration.EditDraft(grant, input.Version, draft, actor, DateTimeOffset.UtcNow);
            // Category IDs are assigned in the domain. Explicitly mark replacement
            // rows Added; graph discovery otherwise treats nonempty IDs as updates.
            db.SpendingAuthorityCategories.AddRange(grant.Categories);
            ProcurementDraftAudit.Add(db, scope, nameof(ProcurementSpendingAuthorityGrant), id,
                "authority.draft_updated", before, View(grant));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(View(grant));
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Command(string operation, Guid id, long version, string? reason,
        Guid? previousId, long? previousVersion, HttpContext http, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            var permission = operation == "submit" ? SpendingAuthorityConfiguration.ConfigurePermission
                : operation == "revoke" ? SpendingAuthorityConfiguration.RevokePermission
                : SpendingAuthorityConfiguration.ReviewPermission;
            var actor = await Actor(core, scope, http, auth, permission, ct);
            if (actor is null) return AccessDenied();
            var key = await Scoped(db, scope).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (key is null) return Missing();
            await using var tx = await Begin(db, scope, key, ct);
            await using var transactionCore = await TransactionalCore(db, scope, tx, ct);
            core = transactionCore;
            var family = await Load(db, key, ct);
            var grant = family.SingleOrDefault(x => x.Id == id);
            if (grant is null || !InScope(grant, scope)) return Missing();
            actor = await Actor(core, scope, http, auth, permission, ct);
            if (actor is null) return AccessDenied();
            if (operation != "revoke" && !await IdentityStoreReady(http, db, true, ct)) return IdentityUnavailable();
            var before = View(grant);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            ProcurementSpendingAuthorityWorkerIdentity.AuthorProof? authorIdentity = null;
            ProcurementSpendingAuthorityWorkerIdentity.ReviewProof? reviewIdentity = null;
            SpendingAuthoritySubject? subject = null;
            if (operation is "submit" or "activate" or "supersede")
            {
                subject = await ProcurementAuthorityIdentity.SubjectAsync(core, scope, grant.PrincipalId, ct);
                if (subject is null) return Invalid("AuthoritySubjectUnavailable");
                var issue = await ProcurementAuthorityIdentity.ReferenceIssueAsync(core, db, scope,
                    ProcurementAuthorityIdentity.Draft(grant), true, ct);
                if (issue is not null) return Invalid(issue);
            }
            if (operation == "submit")
                authorIdentity = await ProcurementSpendingAuthorityWorkerIdentity.RequireAuthorAsync(core, db, scope, grant, ct);
            if (operation is "activate" or "supersede" or "return")
                reviewIdentity = await ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(core, db, scope, grant, ct);
            switch (operation)
            {
                case "submit": SpendingAuthorityConfiguration.Submit(grant, version, actor, subject!, now); break;
                case "return": SpendingAuthorityConfiguration.ReturnForChanges(grant, version, actor, reason ?? "", now); break;
                case "activate": SpendingAuthorityConfiguration.Activate(grant, version, actor, subject!, reason ?? "", now, family); break;
                case "revoke": SpendingAuthorityConfiguration.Revoke(grant, version, actor, reason ?? "", now); break;
                case "supersede":
                    var previous = family.SingleOrDefault(x => x.Id == previousId);
                    if (previous is null || !InScope(previous, scope) || !previousVersion.HasValue) return Missing();
                    await ProcurementSpendingAuthorityWorkerIdentity.RequireRetainedActivationAsync(db, scope, previous, ct);
                    var previousBefore = View(previous);
                    SpendingAuthorityConfiguration.Supersede(grant, version, previous, previousVersion.Value,
                        actor, subject!, reason ?? "", now, family);
                    ProcurementDraftAudit.Add(db, scope, nameof(ProcurementSpendingAuthorityGrant), previous.Id,
                        "authority.superseded", previousBefore, View(previous));
                    // The partial unique Active index requires retiring the predecessor first.
                    // Both saves and all audit remain inside the same PostgreSQL transaction.
                    var detect = db.ChangeTracker.AutoDetectChangesEnabled;
                    try
                    {
                        db.ChangeTracker.AutoDetectChangesEnabled = false;
                        db.Entry(previous).State = EntityState.Modified;
                        await db.SaveChangesAsync(ct);
                        db.Entry(grant).State = EntityState.Modified;
                    }
                    finally { db.ChangeTracker.AutoDetectChangesEnabled = detect; }
                    break;
                default: return Invalid("AuthorityCommandUnsupported");
            }
            if (operation == "submit")
                db.AddScoped(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureSubmission(
                    grant, authorIdentity!.Created, authorIdentity.Actor.PrincipalId,
                    authorIdentity.Actor.WorkerId, authorIdentity.Recipient.WorkerId)));
            if (operation is "activate" or "supersede")
                db.AddScoped(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureActivation(
                    grant, reviewIdentity!.Created, reviewIdentity.Submitted, reviewIdentity.Actor.PrincipalId,
                    reviewIdentity.Actor.WorkerId, reviewIdentity.Recipient.WorkerId)));
            ProcurementDraftAudit.Add(db, scope, nameof(ProcurementSpendingAuthorityGrant), id,
                "authority." + operation, before, View(grant));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(View(grant));
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<HrmDbContext> TransactionalCore(ProcurementDbContext db,
        ProcurementRequestScope scope, IDbContextTransaction transaction, CancellationToken ct)
    {
        var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options, new AuthorityTenantAccessor(scope.TenantId));
        try { await core.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct); return core; }
        catch { await core.DisposeAsync(); throw; }
    }

    private static Task<SpendingAuthorityConfigurationActor?> Actor(HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth, string permission, CancellationToken ct)
        => ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, permission, ct);
    private static IQueryable<ProcurementSpendingAuthorityGrant> Scoped(ProcurementDbContext db, ProcurementRequestScope scope)
        => ProcurementAuthorityIdentity.InScope(db.SpendingAuthorityGrants, scope);
    private static bool InScope(ProcurementSpendingAuthorityGrant grant, ProcurementRequestScope scope)
        => ProcurementAuthorityIdentity.InputInScope(ProcurementAuthorityIdentity.Draft(grant), scope);
    private static Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Begin(ProcurementDbContext db,
        ProcurementRequestScope scope, ProcurementSpendingAuthorityGrant grant, CancellationToken ct)
        => SpendingAuthorityFamilyTransaction.BeginAsync(db, scope.TenantId, scope.LegalEntityId,
            grant.PrincipalId, grant.Permission, grant.Action, grant.AuthorityReference, ct);
    private static Task<List<ProcurementSpendingAuthorityGrant>> Load(ProcurementDbContext db,
        ProcurementSpendingAuthorityGrant key, CancellationToken ct)
        => SpendingAuthorityFamilyTransaction.LoadAsync(db, key.PrincipalId, key.Permission, key.Action, key.AuthorityReference, ct);
    private static async Task<SpendingAuthorityDraft> Defaults(SpendingAuthorityDraft input,
        HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct, string action)
        => input with
        {
            CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
                ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode.Trim().ToUpperInvariant(),
            AmountBasis = string.IsNullOrWhiteSpace(input.AmountBasis)
                ? ProcurementSpendingActions.Resolve(action).AmountBasis : input.AmountBasis,
            BranchRef = input.AllBranches ? input.BranchRef : input.BranchRef ?? scope.WorkLocationId,
            DepartmentRef = input.AllDepartments ? input.DepartmentRef : input.DepartmentRef ?? scope.OrgUnitId,
        };
    private static object View(ProcurementSpendingAuthorityGrant grant, string? principalDisplayName = null,
        string[]? allowedActions = null, Guid? previousGrantId = null, long? previousVersion = null,
        object[]? blockingReasons = null) => new
    {
        grant.Id, grant.AuthorityReference, grant.AuthorityVersion, grant.PrincipalId,
        grant.Permission, grant.Action, grant.State, grant.ConfigurationOwnerPrincipalId,
        grant.CurrencyCode, grant.AmountBasis, maximumAmount = AuthorityAmountJsonConverter.Format(grant.MaximumAmount), grant.EffectiveFrom, grant.EffectiveUntil,
        grant.AllBranches, grant.BranchRef, grant.AllDepartments, grant.DepartmentRef, grant.AllCategories,
        categoryCodes = grant.Categories.Select(x => x.CategoryCode).Order(StringComparer.Ordinal).ToArray(),
        grant.SubmittedByPrincipalId, grant.SubmittedAt, grant.ReviewReturnReason,
        grant.ActivatedByPrincipalId, grant.ActivatedAt, grant.ActivationReason,
        grant.RevokedByPrincipalId, grant.RevokedAt, grant.RevocationReason,
        grant.SupersededByGrantId, grant.SupersededAt, grant.Version, grant.CreatedAt, grant.UpdatedAt,
        principalDisplayName, allowedActions = allowedActions ?? [], previousGrantId, previousVersion,
        blockingReasons = blockingReasons ?? [],
    };
    private static Task<bool> IdentityStoreReady(HttpContext? http, ProcurementDbContext db, bool write, CancellationToken ct)
    {
        var readiness = http?.RequestServices.GetService<IProcurementSpendingAuthorityIdentitySchemaReadiness>()
            ?? new ProcurementSpendingAuthorityIdentitySchemaReadiness();
        return write ? readiness.CanRecordAsync(db, ct) : readiness.IsReadyAsync(db, ct);
    }
    private static IResult IdentityUnavailable() => Results.Problem(statusCode: 503,
        title: "Spending authority identity checks are not available yet.",
        extensions: new Dictionary<string, object?> { ["code"] = "AuthorityIdentityStoreUnavailable" });
    private static IResult Missing() => Results.Problem(statusCode: 404, title: "Spending authority not found.", extensions: new Dictionary<string, object?> { ["code"] = "AuthorityNotFound" });
    private static IResult AccessDenied() => Results.Problem(statusCode: 403, title: "An active company account with this configuration permission is required.");
    private static IResult Invalid(string code) => Results.Problem(statusCode: 422, title: "Check the spending authority details.", extensions: new Dictionary<string, object?> { ["code"] = code });
    private static bool Handled(Exception ex) => ex is InvalidOperationException or DbUpdateException or NpgsqlException;
    private static IResult Failure(Exception ex)
    {
        var pg = ProcurementPostgresFailures.Postgres(ex);
        if (pg?.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
            return Results.Problem(statusCode: 503, title: "Spending authority setup is not available yet.");
        if (ex is NpgsqlException && pg is null)
            return Results.Problem(statusCode: 503, title: "Spending authority storage is unavailable. Try again shortly.");
        if (pg is not null || ex is DbUpdateConcurrencyException)
            return Results.Problem(statusCode: 409, title: "The authority changed. Refresh it before trying again.", extensions: new Dictionary<string, object?> { ["code"] = "AuthorityConcurrentChange" });
        var code = ex is InvalidOperationException && ex.Message.StartsWith("Authority", StringComparison.Ordinal)
            || ex is InvalidOperationException && ex.Message.StartsWith("IndependentAuthority", StringComparison.Ordinal)
            || ex is InvalidOperationException && ex.Message.StartsWith("PublishedAuthority", StringComparison.Ordinal)
            || ex is InvalidOperationException && ex.Message.StartsWith("SubmittedAuthority", StringComparison.Ordinal)
                ? ex.Message : "AuthorityOperationUnavailable";
        var status = code == "AuthorityPostgresTransactionRequired" ? 503
            : code is "AuthorityConfigurationAccessDenied" or "AuthorityConfigurationOwnerRequired" or "IndependentAuthorityReviewerRequired" or "AuthorityIndependentWorkerRequired"
                or "AuthorityOriginalAuthorRequired" or "AuthorityCurrentActorIdentityChanged" ? 403 : 409;
        return Results.Problem(statusCode: status, title: "This spending authority action could not be completed.", extensions: new Dictionary<string, object?> { ["code"] = code });
    }

    private sealed class AuthorityTenantAccessor(string tenant) : ITenantAccessor
    {
        public string GetTenantId() => tenant;
    }

    public sealed record CreateInput(string? AuthorityReference, string? PrincipalId, SpendingAuthorityDraft? Draft,
        string? Action = null);
    public sealed record EditInput(long Version, SpendingAuthorityDraft? Draft);
    public sealed record VersionInput(long Version);
    public sealed record ReviewInput(long Version, string? Reason);
    public sealed record SupersedeInput(long Version, Guid PreviousGrantId, long PreviousVersion, string? Reason);
}
