using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static class CoreCurrencyPrecisionRoutes
{
    private const string ReadAction = "core-currency-precision-read";
    private const string ConfigureAction = "core-currency-precision-configure";
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public sealed record PublishInput(Guid CompanyId, string CurrencyCode, int ExpectedRevision,
        int MinorUnits, string Rounding, string SourceReference, string Reason);
    public sealed record RetireInput(Guid CompanyId, int ExpectedRevision, string Reason);
    public sealed record VersionView(Guid Id, string CurrencyCode, int Revision, int MinorUnits,
        string Rounding, string SourceReference, DateTimeOffset PublishedAtUtc,
        DateTimeOffset? RetiredAtUtc, string Digest, bool Current,
        string? RetirementReason = null, Guid? CommandKey = null, string? Operation = null);
    private sealed record Actor(string TenantId, Guid PrincipalId, bool CanConfigure,
        string CompanyCurrency, Guid CompanyId);

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/core/v1/currency-precisions")
            .RequireAuthorization(CoreWorkspaceContextRoutes.Policy);
        group.AddEndpointFilter(async (invocation, next) =>
        {
            try
            {
                var services = invocation.HttpContext.RequestServices;
                var config = services.GetRequiredService<IConfiguration>();
                var write = HttpMethods.IsPost(invocation.HttpContext.Request.Method);
                if (ReadEnabled(config) && (!write || WriteEnabled(config))
                    && !await services.GetRequiredService<ICoreCurrencyPrecisionSchemaReadiness>()
                        .IsReadyAsync(write, invocation.HttpContext.RequestAborted))
                    return Error("CoreCurrencyPrecisionUnavailable", 503);
                return await next(invocation);
            }
            catch (Exception error) when (error.GetBaseException() is PostgresException
                { SqlState: "40001" or "40P01" or "23505" })
            { return Error("CoreCurrencyRevisionStale", 409); }
            catch (Exception error) when (error.GetBaseException() is PostgresException
                { SqlState: "42P01" or "42703" })
            { return Error("CoreCurrencyPrecisionUnavailable", 503); }
            catch (Exception error) when (error.GetBaseException() is NpgsqlException
                and not PostgresException)
            { return Error("CoreCurrencyPrecisionUnavailable", 503); }
        });
        group.MapGet("", List);
        group.MapGet("/{id:guid}", Detail);
        group.MapPost("", Publish);
        group.MapPost("/{id:guid}/retire", Retire);
        group.MapGet("/commands/{operation}/{key:guid}", Recover);
    }

    private static async Task<IResult> List(Guid companyId, HttpContext http, HrmDbContext db,
        IConfiguration config, TimeProvider clock, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!ReadEnabled(config)) return Error("CoreCurrencyPrecisionUnavailable", 503);
        var actor = await CurrentActor(http, db, companyId, false, clock.GetUtcNow(), ct);
        if (actor is null) return Results.Forbid();
        var rows = await db.CurrencyPrecisionVersions.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && !x.IsArchived)
            .OrderBy(x => x.CurrencyCode).ThenByDescending(x => x.Revision).Take(501).ToArrayAsync(ct);
        if (rows.Length > 500) return Error("CoreCurrencyListLimitExceeded", 409);
        var items = rows.Where(x => actor.CanConfigure || x.CurrencyCode == actor.CompanyCurrency)
            .Select(x => View(x, rows.Where(other => other.CurrencyCode == x.CurrencyCode)
                .Max(other => other.Revision) == x.Revision && x.RetiredAtUtc is null)).ToArray();
        return Results.Ok(new { schemaVersion = 1, companyId = actor.CompanyId,
            companyCurrency = actor.CompanyCurrency,
            canConfigure = actor.CanConfigure && WriteEnabled(config), items });
    }

    private static async Task<IResult> Detail(Guid id, Guid companyId, HttpContext http,
        HrmDbContext db, IConfiguration config, TimeProvider clock, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!ReadEnabled(config)) return Error("CoreCurrencyPrecisionUnavailable", 503);
        var actor = await CurrentActor(http, db, companyId, false, clock.GetUtcNow(), ct);
        if (actor is null) return Results.Forbid();
        var row = await db.CurrencyPrecisionVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == actor.TenantId && !x.IsArchived, ct);
        if (row is null || !actor.CanConfigure && row.CurrencyCode != actor.CompanyCurrency)
            return Results.NotFound();
        var latest = await db.CurrencyPrecisionVersions.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CurrencyCode == row.CurrencyCode && !x.IsArchived)
            .MaxAsync(x => x.Revision, ct);
        return Results.Ok(View(row, latest == row.Revision && row.RetiredAtUtc is null));
    }

    private static async Task<IResult> Publish(HttpContext http, PublishInput input,
        HrmDbContext db, IConfiguration config, TimeProvider clock, CancellationToken ct)
    {
        if (!ReadEnabled(config) || !WriteEnabled(config)) return Error("CoreCurrencyPrecisionUnavailable", 503);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var actor = await CurrentActor(http, db, input.CompanyId, true, clock.GetUtcNow(), ct);
        if (actor is null) return Results.Forbid();
        var key = CommandKey(http);
        if (key == Guid.Empty) return Error("CoreCurrencyCommandKeyRequired", 400);
        var fingerprint = Digest(JsonSerializer.Serialize(new { operation = "Publish", input }, Wire));
        var prior = await db.CurrencyPrecisionCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == actor.TenantId && x.ActorPrincipalId == actor.PrincipalId &&
            x.Operation == "Publish" && x.CommandKey == key, ct);
        if (prior is not null)
            return await SavedAsync(prior, fingerprint, actor.CompanyId, db, ct);
        if (!CoreCurrencyPrecisionRules.ValidPublish(input.CurrencyCode, input.ExpectedRevision,
                input.MinorUnits, input.Rounding, input.SourceReference, input.Reason))
            return Error("CoreCurrencyPrecisionInvalid", 422);
        var rows = await db.CurrencyPrecisionVersions.Where(x => x.TenantId == actor.TenantId &&
            x.CurrencyCode == input.CurrencyCode && !x.IsArchived)
            .OrderByDescending(x => x.Revision).Take(2).ToArrayAsync(ct);
        var latest = rows.FirstOrDefault();
        if (!CoreCurrencyPrecisionRules.CanPublish(latest?.Revision, input.ExpectedRevision))
            return Error("CoreCurrencyRevisionStale", 409);
        var now = UtcMicrosecond(clock.GetUtcNow());
        if (latest is not null && latest.RetiredAtUtc is null)
        {
            latest.RetiredAtUtc = now;
            latest.RetirementReason = "Superseded by published revision";
            latest.UpdatedAt = now;
            latest.UpdatedBy = actor.PrincipalId.ToString("D");
        }
        var version = new CoreCurrencyPrecisionVersion {
            TenantId = actor.TenantId, CurrencyCode = input.CurrencyCode,
            Revision = input.ExpectedRevision + 1, MinorUnits = input.MinorUnits,
            Rounding = input.Rounding, SourceReference = input.SourceReference.Trim(),
            PublicationReason = input.Reason.Trim(), PublishedAtUtc = now,
            CreatedAt = now, CreatedBy = actor.PrincipalId.ToString("D")
        };
        version.PayloadSha256 = Digest(JsonSerializer.Serialize(new {
            version.CurrencyCode, version.Revision, version.MinorUnits, version.Rounding,
            version.SourceReference, version.PublicationReason, version.PublishedAtUtc }, Wire));
        var response = JsonSerializer.Serialize(View(version, true, key, "Publish"), Wire);
        db.CurrencyPrecisionVersions.Add(version);
        db.CurrencyPrecisionCommandReceipts.Add(Receipt(actor, version.Id, "Publish", key,
            fingerprint, response, 201, now));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" or "40001" })
        { return Error("CoreCurrencyRevisionStale", 409); }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "40001")
        { return Error("CoreCurrencyRevisionStale", 409); }
        return Results.Content(response, "application/json", statusCode: 201);
    }

    private static async Task<IResult> Retire(Guid id, HttpContext http, RetireInput input,
        HrmDbContext db, IConfiguration config, TimeProvider clock, CancellationToken ct)
    {
        if (!ReadEnabled(config) || !WriteEnabled(config)) return Error("CoreCurrencyPrecisionUnavailable", 503);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var actor = await CurrentActor(http, db, input.CompanyId, true, clock.GetUtcNow(), ct);
        if (actor is null) return Results.Forbid();
        var key = CommandKey(http);
        if (key == Guid.Empty) return Error("CoreCurrencyCommandKeyRequired", 400);
        var fingerprint = Digest(JsonSerializer.Serialize(new { operation = "Retire", id, input }, Wire));
        var prior = await db.CurrencyPrecisionCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == actor.TenantId && x.ActorPrincipalId == actor.PrincipalId &&
            x.Operation == "Retire" && x.CommandKey == key, ct);
        if (prior is not null) return await SavedAsync(prior, fingerprint, actor.CompanyId, db, ct);
        if (input.ExpectedRevision < 1 || !CoreCurrencyPrecisionRules.NonBlank(input.Reason, 500))
            return Error("CoreCurrencyPrecisionInvalid", 422);
        var row = await db.CurrencyPrecisionVersions.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == actor.TenantId && !x.IsArchived, ct);
        if (row is null) return Results.NotFound();
        var latest = await db.CurrencyPrecisionVersions.Where(x => x.TenantId == actor.TenantId &&
            x.CurrencyCode == row.CurrencyCode && !x.IsArchived).MaxAsync(x => x.Revision, ct);
        if (!CoreCurrencyPrecisionRules.CanRetire(latest, row.Revision,
                input.ExpectedRevision, row.RetiredAtUtc))
            return Error("CoreCurrencyRevisionStale", 409);
        var now = UtcMicrosecond(clock.GetUtcNow());
        row.RetiredAtUtc = now; row.RetirementReason = input.Reason.Trim();
        row.UpdatedAt = now; row.UpdatedBy = actor.PrincipalId.ToString("D");
        var response = JsonSerializer.Serialize(View(row, false, key, "Retire"), Wire);
        db.CurrencyPrecisionCommandReceipts.Add(Receipt(actor, row.Id, "Retire", key,
            fingerprint, response, 200, now));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" or "40001" })
        { return Error("CoreCurrencyRevisionStale", 409); }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "40001")
        { return Error("CoreCurrencyRevisionStale", 409); }
        return Results.Content(response, "application/json", statusCode: 200);
    }

    private static async Task<IResult> Recover(string operation, Guid key, Guid companyId,
        HttpContext http, HrmDbContext db, IConfiguration config, TimeProvider clock, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!ReadEnabled(config)) return Error("CoreCurrencyPrecisionUnavailable", 503);
        var actor = await CurrentActor(http, db, companyId, true, clock.GetUtcNow(), ct);
        if (actor is null) return Results.Forbid();
        if (key == Guid.Empty || operation is not ("Publish" or "Retire")) return Results.NotFound();
        var receipt = await db.CurrencyPrecisionCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == actor.TenantId && x.ActorPrincipalId == actor.PrincipalId &&
            x.CompanyId == companyId && x.Operation == operation && x.CommandKey == key, ct);
        if (receipt is null) return Results.NotFound();
        return await SavedAsync(receipt, receipt.RequestSha256, actor.CompanyId, db, ct);
    }

    private static async Task<Actor?> CurrentActor(HttpContext http, HrmDbContext db,
        Guid companyId, bool configure, DateTimeOffset now, CancellationToken ct)
    {
        var tenant = http.User.FindFirstValue("tenant");
        if (string.IsNullOrWhiteSpace(tenant) || companyId == Guid.Empty ||
            !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var principal)) return null;
        var account = await db.LocalUsers.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == principal && x.TenantId == tenant && x.IsActive && !x.IsArchived, ct);
        if (account is null || account.LockedUntil is { } locked && locked > now) return null;
        var companyCurrency = await db.LegalEntities.AsNoTracking().Where(x =>
            x.Id == companyId && x.TenantId == tenant && !x.IsArchived)
            .Select(x => x.Currency).SingleOrDefaultAsync(ct);
        if (!CoreCurrencyPrecisionRules.CanonicalCode(companyCurrency)) return null;
        var assigned = LocalIdentityRoutes.ParseRoles(account.RolesCsv);
        if (assigned.Length == 0) return null;
        var roleRows = await db.TenantRoleAssignments.AsNoTracking().Where(x =>
            x.TenantId == tenant && assigned.Contains(x.RoleKey) && x.Active && !x.IsArchived)
            .Select(x => new { x.RoleKey, x.PermissionsCsv }).ToArrayAsync(ct);
        var grants = roleRows.SelectMany(x => x.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        var branches = await db.UserBranchAssignments.AsNoTracking().Where(x =>
            x.TenantId == tenant && x.UserId == principal && !x.IsArchived)
            .Select(x => x.LocationId).ToArrayAsync(ct);
        if (branches.Length != 0 && configure) return null;
        if (branches.Length != 0 && !await db.WorkLocations.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenant && branches.Contains(x.Id) && x.LegalEntityId == companyId && !x.IsArchived, ct))
            return null;
        var canConfigure = CoreCurrencyPrecisionPermissions.Allows(true, true, branches.Length != 0,
            grants.Contains(ReadAction), grants.Contains(ConfigureAction), configure: true);
        if (!CoreCurrencyPrecisionPermissions.Allows(true, true, branches.Length != 0,
            grants.Contains(ReadAction), grants.Contains(ConfigureAction), configure)) return null;
        return new(tenant, principal, canConfigure, companyCurrency!, companyId);
    }

    private static CoreCurrencyPrecisionCommandReceipt Receipt(Actor actor, Guid target,
        string operation, Guid key, string fingerprint, string response, int status, DateTimeOffset now)
        => new() { TenantId = actor.TenantId, ActorPrincipalId = actor.PrincipalId,
            CompanyId = actor.CompanyId, VersionId = target, Operation = operation, CommandKey = key,
            RequestSha256 = fingerprint, ResponseJson = response, ResponseSha256 = Digest(response),
            ResponseStatus = status, CompletedAtUtc = now, CreatedAt = now,
            CreatedBy = actor.PrincipalId.ToString("D") };
    private static async Task<IResult> SavedAsync(CoreCurrencyPrecisionCommandReceipt receipt,
        string fingerprint, Guid companyId, HrmDbContext db, CancellationToken ct)
    {
        if (receipt.RequestSha256 != fingerprint || receipt.CompanyId != companyId)
            return Error("CoreCurrencyCommandKeyConflict", 409);
        var version = await db.CurrencyPrecisionVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == receipt.VersionId && x.TenantId == receipt.TenantId && !x.IsArchived, ct);
        return RetainedResponseMatches(receipt, version, companyId)
            ? Results.Content(receipt.ResponseJson, "application/json", statusCode: receipt.ResponseStatus)
            : Error("CoreCurrencyReceiptInvalid", 503);
    }

    internal static bool RetainedResponseMatches(CoreCurrencyPrecisionCommandReceipt receipt,
        CoreCurrencyPrecisionVersion? version, Guid companyId)
    {
        if (version is null || receipt.VersionId != version.Id || receipt.TenantId != version.TenantId
            || receipt.CompanyId != companyId || receipt.CommandKey == Guid.Empty
            || receipt.Operation is not ("Publish" or "Retire")
            || receipt.ResponseStatus != (receipt.Operation == "Publish" ? 201 : 200)
            || receipt.ResponseJson is null || receipt.ResponseSha256 != Digest(receipt.ResponseJson)
            || receipt.RequestSha256 is not { Length: 64 }
            || !receipt.RequestSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        var payload = JsonSerializer.Serialize(new { version.CurrencyCode, version.Revision,
            version.MinorUnits, version.Rounding, version.SourceReference,
            version.PublicationReason, version.PublishedAtUtc }, Wire);
        if (version.PayloadSha256 != Digest(payload)) return false;
        var published = receipt.Operation == "Publish";
        if (!published && (version.RetiredAtUtc is null || string.IsNullOrWhiteSpace(version.RetirementReason)))
            return false;
        // Publication's original response remains current=true even if the
        // immutable version was retired later. Retirement has its own saved 200.
        var response = new VersionView(version.Id, version.CurrencyCode, version.Revision,
            version.MinorUnits, version.Rounding, version.SourceReference,
            version.PublishedAtUtc, published ? null : version.RetiredAtUtc,
            version.PayloadSha256, published, published ? null : version.RetirementReason,
            receipt.CommandKey, receipt.Operation);
        return receipt.ResponseJson == JsonSerializer.Serialize(response, Wire);
    }
    private static VersionView View(CoreCurrencyPrecisionVersion row, bool current,
        Guid? key = null, string? operation = null)
        => new(row.Id, row.CurrencyCode, row.Revision, row.MinorUnits, row.Rounding,
            row.SourceReference, row.PublishedAtUtc, row.RetiredAtUtc, row.PayloadSha256,
            current, row.RetirementReason, key, operation);
    private static Guid CommandKey(HttpContext http) =>
        Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) ? key : Guid.Empty;
    private static string Digest(string text) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static DateTimeOffset UtcMicrosecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }
    private static bool ReadEnabled(IConfiguration config) => config.GetValue<bool>("Core:CurrencyPrecision:ReadEnabled");
    private static bool WriteEnabled(IConfiguration config) => config.GetValue<bool>("Core:CurrencyPrecision:PublishEnabled");
    private static IResult Error(string code, int status) => Results.Json(new { code,
        message = code switch {
            "CoreCurrencyRevisionStale" => "The currency reference changed. Refresh and try again.",
            "CoreCurrencyCommandKeyConflict" => "This command key belongs to a different request.",
            "CoreCurrencyCommandKeyRequired" => "A command key is required.",
            "CoreCurrencyPrecisionInvalid" => "Check the currency code, precision, rounding and reason.",
            "CoreCurrencyListLimitExceeded" => "Too many currency references. Narrow the selection.",
            "CoreCurrencyReceiptInvalid" => "The saved command response is unavailable.",
            _ => "Currency precision settings are unavailable."
        } }, statusCode: status);
}
