using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Integration;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

internal sealed class LocalAuthOptions : AuthenticationSchemeOptions;

internal static class LocalPasswordHash
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Passwords must contain at least 12 characters.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}

internal static class LocalSessionTokens
{
    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

internal sealed class LocalAuthenticationHandler(
    IOptionsMonitor<LocalAuthOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    HrmDbContext db,
    IConfiguration config)
    : AuthenticationHandler<LocalAuthOptions>(options, logger, encoder)
{
    public const string Scheme = "local";
    public const string CookieName = "hrm_session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(token)) return AuthenticateResult.NoResult();

        var now = DateTimeOffset.UtcNow;
        var session = await db.LocalSessions.FirstOrDefaultAsync(x =>
            x.TokenHash == LocalSessionTokens.Hash(token) && x.RevokedAt == null && x.ExpiresAt > now);
        if (session is null) return AuthenticateResult.Fail("Session expired or revoked.");

        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == session.LocalUserId && x.IsActive && !x.IsArchived);
        if (user is null) return AuthenticateResult.Fail("Account inactive.");

        if (session.LastSeenAt < now.AddMinutes(-5))
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")));
        identity.AddClaim(new Claim("sub", user.Id.ToString("D")));
        identity.AddClaim(new Claim("preferred_username", user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
        identity.AddClaim(new Claim("tenant", user.TenantId));
        identity.AddClaim(new Claim("environment", "standalone"));
        foreach (var role in await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, user.RolesCsv, Context.RequestAborted))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        if (user.WorkerId is Guid workerId)
            identity.AddClaim(new Claim("worker_id", workerId.ToString("D")));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}

internal static class LocalIdentityRoutes
{
    public static void Map(WebApplication app)
    {
        foreach (var prefix in new[] { "/api/hrm/auth", "/api/v1/hrm/auth" })
        {
            var g = app.MapGroup(prefix);
            g.MapGet("/capabilities", (IConfiguration config) => Results.Ok(AuthCapabilities(config))).AllowAnonymous();
            g.MapPost("/login", LoginAsync).AllowAnonymous();
            g.MapGet("/me", MeAsync).AllowAnonymous();
            g.MapPost("/logout", LogoutAsync).RequireAuthorization();
            g.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
            g.MapPost("/set-password", CompletePasswordSetupAsync).AllowAnonymous();
            g.MapGet("/users", ListUsersAsync).RequireAuthorization("hrm-admin");
            g.MapGet("/users/{id:guid}", GetUserAsync).RequireAuthorization("hrm-admin");
            g.MapPost("/users", CreateUserAsync).RequireAuthorization("hrm-admin");
            g.MapPatch("/users/{id:guid}", UpdateUserAsync).RequireAuthorization("hrm-admin");
            g.MapPost("/users/{id:guid}/reset-password", ResetPasswordAsync).RequireAuthorization("hrm-admin");
            g.MapPost("/users/{id:guid}/send-password-link", SendPasswordLinkAsync).RequireAuthorization("hrm-admin");
        }
    }

    internal static object AuthCapabilities(IConfiguration config)
    {
        var mode = (config["ERP:AuthMode"] ?? config["HRM:AuthMode"] ?? "local").Trim().ToLowerInvariant();
        var identityConfigured = !string.IsNullOrWhiteSpace(config["HRM:IdentityBaseUrl"]);
        return new
        {
            mode,
            localUsersEnabled = mode is "local" or "hybrid",
            identityConfigured = identityConfigured && (mode is "oidc" or "hybrid"),
        };
    }

    public static string[] ParseRoles(string csv)
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(IsValidRoleKey)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static async Task<string[]> ResolveGrantedRolesAsync(HrmDbContext db, string csv, CancellationToken ct)
    {
        var assigned = ParseRoles(csv);
        if (assigned.Length == 0) return [];
        var configured = await db.TenantRoleAssignments.Where(r => r.Active).ToListAsync(ct);
        if (configured.Count == 0)
            return assigned.Where(r => HrmStaffAccess.Roles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        var grants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in assigned)
        {
            var row = configured.FirstOrDefault(r => r.RoleKey.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;
            grants.Add(row.RoleKey);
            var permissions = ParseRoles(row.PermissionsCsv);
            foreach (var permission in permissions.Length > 0 ? permissions : [row.RoleKey])
                if (HrmStaffAccess.Roles.Contains(permission, StringComparer.OrdinalIgnoreCase))
                    grants.Add(permission);
        }
        return grants.ToArray();
    }

    private static bool IsValidRoleKey(string key) =>
        key.Length > 0 && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static async Task<string[]> RequireAssignableRolesAsync(HrmDbContext db, IEnumerable<string>? requestedRoles, CancellationToken ct)
    {
        var roles = ParseRoles(string.Join(',', requestedRoles ?? ["employee"]));
        var active = await db.TenantRoleAssignments.Where(r => r.Active).Select(r => r.RoleKey).ToListAsync(ct);
        var valid = active.Count == 0
            ? roles.Where(r => HrmStaffAccess.Roles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray()
            : roles.Where(r => active.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        return valid.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static object UserDto(LocalUser user, bool includeSecretState = true, string[]? roles = null) => new
    {
        id = user.Id,
        email = user.Email,
        displayName = user.DisplayName,
        roles = roles ?? ParseRoles(user.RolesCsv),
        workerId = user.WorkerId,
        isActive = user.IsActive,
        mustChangePassword = includeSecretState && user.MustChangePassword,
        lastLoginAt = user.LastLoginAt,
        createdAt = user.CreatedAt,
    };

    private static object SessionDto(LocalUser user, string[]? grantedRoles = null) => new
    {
        user = UserDto(user, roles: grantedRoles),
        authenticated = true,
    };

    private static async Task<IResult> LoginAsync(LoginRequest request, HrmDbContext db, HttpContext http, IConfiguration config, CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? "";
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.NormalizedEmail == NormalizeEmail(email), ct);
        var now = DateTimeOffset.UtcNow;
        if (user is null || !user.IsActive || user.IsArchived || (user.LockedUntil is not null && user.LockedUntil > now)
            || !LocalPasswordHash.Verify(request.Password ?? "", user.PasswordHash))
        {
            if (user is not null && user.IsActive)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5)
                {
                    user.LockedUntil = now.AddMinutes(15);
                    user.FailedLoginCount = 0;
                }
                await db.SaveChangesAsync(ct);
            }
            return Results.Json(new { code = "invalid-credentials", message = "Email or password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        var token = LocalSessionTokens.Create();
        db.LocalSessions.Add(new LocalSession
        {
            TenantId = user.TenantId,
            LocalUserId = user.Id,
            TokenHash = LocalSessionTokens.Hash(token),
            ExpiresAt = now.AddDays(7),
            LastSeenAt = now,
            UserAgent = http.Request.Headers.UserAgent.ToString()[..Math.Min(http.Request.Headers.UserAgent.ToString().Length, 500)],
            CreatedBy = user.Id.ToString("D"),
        });
        await db.SaveChangesAsync(ct);
        http.Response.Cookies.Append(LocalAuthenticationHandler.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7),
        });
        return Results.Ok(SessionDto(user, await ResolveGrantedRolesAsync(db, user.RolesCsv, ct)));
    }

    private static async Task<IResult> MeAsync(HttpContext http, HrmDbContext db, CancellationToken ct)
    {
        if (http.User.Identity?.IsAuthenticated != true) return Results.Ok(new { authenticated = false, user = (object?)null });
        var raw = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) return Results.Ok(new { authenticated = false, user = (object?)null });
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsArchived, ct);
        return user is null
            ? Results.Ok(new { authenticated = false, user = (object?)null })
            : Results.Ok(SessionDto(user, await ResolveGrantedRolesAsync(db, user.RolesCsv, ct)));
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, HrmDbContext db, CancellationToken ct)
    {
        var token = http.Request.Cookies[LocalAuthenticationHandler.CookieName];
        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = await db.LocalSessions.FirstOrDefaultAsync(x => x.TokenHash == LocalSessionTokens.Hash(token), ct);
            if (session is not null) { session.RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
        }
        http.Response.Cookies.Delete(LocalAuthenticationHandler.CookieName, new CookieOptions { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
        return Results.Ok(new { authenticated = false });
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, HttpContext http, HrmDbContext db, CancellationToken ct)
    {
        var user = await CurrentUserAsync(http, db, ct);
        if (user is null || !LocalPasswordHash.Verify(request.CurrentPassword ?? "", user.PasswordHash))
            return Results.Json(new { code = "invalid-password", message = "Current password is incorrect." }, statusCode: StatusCodes.Status400BadRequest);
        user.PasswordHash = LocalPasswordHash.Hash(request.NewPassword ?? "");
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { changed = true });
    }

    private static async Task<IResult> ListUsersAsync(HrmDbContext db, CancellationToken ct)
        => Results.Ok(new { items = await db.LocalUsers.OrderBy(x => x.Email).Select(x => UserDto(x)).ToListAsync(ct) });

    private static async Task<IResult> GetUserAsync(Guid id, HrmDbContext db, CancellationToken ct)
    {
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return Results.NotFound(new { code = "user-not-found", message = "User account not found." });
        var activity = await db.AuditEntries.Where(x => x.EntityType == nameof(LocalUser) && x.EntityId == id.ToString())
            .OrderByDescending(x => x.CreatedAt).Take(50)
            .Select(x => new { action = x.Action, actor = x.ActorSubjectId, at = x.CreatedAt })
            .ToListAsync(ct);
        var sessions = await db.LocalSessions.Where(x => x.LocalUserId == id)
            .OrderByDescending(x => x.LastSeenAt).Take(20)
            .Select(x => new { createdAt = x.CreatedAt, lastSeenAt = x.LastSeenAt, expiresAt = x.ExpiresAt, revokedAt = x.RevokedAt, userAgent = x.UserAgent })
            .ToListAsync(ct);
        return Results.Ok(new { user = UserDto(user), activity, sessions });
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        HrmDbContext db,
        IOutboxWriter outbox,
        IIdentityProvisioningService identity,
        IConfiguration config,
        CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? "";
        var roles = await RequireAssignableRolesAsync(db, request.Roles, ct);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(request.DisplayName) || roles.Length == 0)
            return Results.BadRequest(new { code = "invalid-user", message = "Email, display name, and at least one valid HRM role are required." });
        if (await db.LocalUsers.AnyAsync(x => x.NormalizedEmail == NormalizeEmail(email), ct))
            return Results.Conflict(new { code = "email-taken", message = "An account with that email already exists." });

        var authMode = config["ERP:AuthMode"]?.Trim().ToLowerInvariant();
        if (authMode is "oidc" or "hybrid")
        {
            var directoryMatches = await identity.SearchDirectoryAsync(email, ct);
            if (directoryMatches.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return Results.Conflict(new
                {
                    code = "identity-exists",
                    message = "This person already has an organisation identity. Select that identity instead of creating a local HRMS account."
                });
        }
        var user = new LocalUser
        {
            Email = email,
            NormalizedEmail = NormalizeEmail(email),
            DisplayName = request.DisplayName.Trim(),
            // The recipient chooses their password from the one-time email link.
            PasswordHash = LocalPasswordHash.Hash(CreateUnusablePassword()),
            RolesCsv = string.Join(',', roles),
            WorkerId = request.WorkerId,
            MustChangePassword = true,
        };
        db.LocalUsers.Add(user);
        await db.SaveChangesAsync(ct);
        await QueueCredentialLinkAsync(user, db, outbox, config, ct);
        return Results.Created($"/api/hrm/auth/users/{user.Id}", UserDto(user));
    }

    private static async Task<IResult> UpdateUserAsync(Guid id, UpdateUserRequest request, HrmDbContext db, CancellationToken ct)
    {
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return Results.NotFound(new { code = "user-not-found", message = "User account not found." });
        if (request.Email is not null)
        {
            var normalized = NormalizeEmail(request.Email);
            if (await db.LocalUsers.AnyAsync(x => x.Id != id && x.NormalizedEmail == normalized, ct))
                return Results.Conflict(new { code = "email-taken", message = "An account with that email already exists." });
            user.Email = request.Email.Trim();
            user.NormalizedEmail = normalized;
        }
        if (request.DisplayName is not null) user.DisplayName = request.DisplayName.Trim();
        if (request.Roles is not null)
        {
            var roles = await RequireAssignableRolesAsync(db, request.Roles, ct);
            if (roles.Length == 0) return Results.BadRequest(new { code = "invalid-roles", message = "At least one valid HRM role is required." });
            user.RolesCsv = string.Join(',', roles);
        }
        if (request.IsActive is not null) user.IsActive = request.IsActive.Value;
        if (request.WorkerId is not null) user.WorkerId = request.WorkerId;
        if (!await ActiveAdminUserRemainsAsync(db, ct))
            return Results.Json(new { code = "last-admin-user", message = "At least one active user must keep HRMS administration access." }, statusCode: StatusCodes.Status400BadRequest);
        await db.SaveChangesAsync(ct);
        return Results.Ok(UserDto(user));
    }

    private static async Task<IResult> ResetPasswordAsync(Guid id, ResetPasswordRequest request, HrmDbContext db, CancellationToken ct)
    {
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return Results.NotFound(new { code = "user-not-found", message = "User account not found." });
        user.PasswordHash = LocalPasswordHash.Hash(request.NewPassword ?? "");
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await db.LocalSessions.Where(x => x.LocalUserId == id && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { reset = true });
    }

    private static async Task<IResult> SendPasswordLinkAsync(Guid id, HrmDbContext db, IOutboxWriter outbox, IConfiguration config, CancellationToken ct)
    {
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsArchived, ct);
        if (user is null) return Results.NotFound(new { code = "user-not-found", message = "Active user account not found." });
        await QueueCredentialLinkAsync(user, db, outbox, config, ct);
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> CompletePasswordSetupAsync(CompletePasswordSetupRequest request, HrmDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest(new { code = "invalid-link", message = "This account link is invalid or has expired." });
        LocalCredentialLink? link;
        try { link = await db.LocalCredentialLinks.FirstOrDefaultAsync(x => x.TokenHash == LocalSessionTokens.Hash(request.Token) && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, ct); }
        catch { link = null; }
        if (link is null) return Results.BadRequest(new { code = "invalid-link", message = "This account link is invalid or has expired." });
        var user = await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == link.LocalUserId && x.IsActive && !x.IsArchived, ct);
        if (user is null) return Results.BadRequest(new { code = "invalid-link", message = "This account link is invalid or has expired." });
        user.PasswordHash = LocalPasswordHash.Hash(request.NewPassword ?? "");
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        link.UsedAt = DateTimeOffset.UtcNow;
        await db.LocalCredentialLinks.Where(x => x.LocalUserId == user.Id && x.Id != link.Id && x.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, DateTimeOffset.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { changed = true });
    }

    private static async Task QueueCredentialLinkAsync(LocalUser user, HrmDbContext db, IOutboxWriter outbox, IConfiguration config, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await db.LocalCredentialLinks.Where(x => x.LocalUserId == user.Id && x.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, now), ct);
        var token = LocalSessionTokens.Create();
        db.LocalCredentialLinks.Add(new LocalCredentialLink { TenantId = user.TenantId, LocalUserId = user.Id, TokenHash = LocalSessionTokens.Hash(token), ExpiresAt = now.AddHours(24) });
        user.MustChangePassword = true;
        await db.SaveChangesAsync(ct);
        var publicUrl = config["HRM:PublicUrl"]?.TrimEnd('/') ?? "https://erp.mightyfinance.co.zm";
        await outbox.EnqueueAsync(HrmEventTypes.AccountAccessLink, user.Id.ToString("D"), new
        {
            email = user.Email,
            first_name = user.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.DisplayName,
            account_link = $"{publicUrl}/sign-in?token={Uri.EscapeDataString(token)}",
            expires_at = now.AddHours(24).ToString("O")
        }, ct);
    }

    private static string CreateUnusablePassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!";

    private static async Task<LocalUser?> CurrentUserAsync(HttpContext http, HrmDbContext db, CancellationToken ct)
    {
        var raw = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? await db.LocalUsers.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct) : null;
    }

    private static async Task<bool> ActiveAdminUserRemainsAsync(HrmDbContext db, CancellationToken ct)
    {
        var roleRows = await db.TenantRoleAssignments.Where(r => r.Active).ToListAsync(ct);
        var activeUsers = await db.LocalUsers.Where(u => u.IsActive && !u.IsArchived).ToListAsync(ct);
        return activeUsers.Any(u => UserGrantsAdmin(u, roleRows));
    }

    private static bool UserGrantsAdmin(LocalUser user, IReadOnlyCollection<TenantRoleAssignment> roleRows)
    {
        var assigned = ParseRoles(user.RolesCsv);
        if (assigned.Length == 0) return false;
        if (roleRows.Count == 0)
            return assigned.Contains("hr_admin", StringComparer.OrdinalIgnoreCase);
        return assigned.Any(role =>
        {
            var row = roleRows.FirstOrDefault(r => r.RoleKey.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (row is null) return false;
            return ParseRoles(row.PermissionsCsv).DefaultIfEmpty(row.RoleKey).Contains("hr_admin", StringComparer.OrdinalIgnoreCase);
        });
    }

    public sealed record LoginRequest(string? Email, string? Password);
    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public sealed record CreateUserRequest(string? Email, string? DisplayName, string? Password, string[]? Roles, Guid? WorkerId);
    public sealed record UpdateUserRequest(string? Email, string? DisplayName, string[]? Roles, bool? IsActive, Guid? WorkerId);
    public sealed record ResetPasswordRequest(string? NewPassword);
    public sealed record CompletePasswordSetupRequest(string? Token, string? NewPassword);
}

internal static class LocalIdentityBootstrap
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var email = config["HRM:LocalAdminEmail"]?.Trim();
        var password = config["HRM:LocalAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        if (await db.LocalUsers.AnyAsync(ct)) return;
        var user = new LocalUser
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = config["HRM:LocalAdminDisplayName"]?.Trim() ?? "NewWorldCargo Administrator",
            PasswordHash = LocalPasswordHash.Hash(password),
            RolesCsv = "hr_admin,employee,hr_ops,finance_approver,investigator,payroll,manager",
            MustChangePassword = true,
        };
        db.LocalUsers.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
