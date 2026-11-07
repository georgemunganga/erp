namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Core principal. Local credentials are absent for federated identities.</summary>
public sealed class LocalUser : Entity
{
    public string CredentialMode { get; set; } = LocalCredentialModes.Local;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string RolesCsv { get; set; } = "employee";
    public Guid? WorkerId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }

    /// <summary>Explicit creation only; no identity link, role, company or session grant.</summary>
    public static LocalUser Federated(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
        return new LocalUser { CredentialMode = LocalCredentialModes.Federated, DisplayName = displayName.Trim(),
            MustChangePassword = false, RolesCsv = "" };
    }
}

public static class LocalCredentialModes
{
    public const string Local = "local";
    public const string Federated = "federated";
    public static bool IsValid(LocalUser user) => user.CredentialMode switch
    {
        Local => !string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(user.NormalizedEmail)
            && !string.IsNullOrWhiteSpace(user.PasswordHash),
        Federated => user.Email is null && user.NormalizedEmail is null && user.PasswordHash is null
            && !user.MustChangePassword && user.PasswordChangedAt is null,
        _ => false
    };
}

/// <summary>Opaque, revocable server-side session token record.</summary>
public sealed class LocalSession : Entity
{
    public Guid LocalUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>One-time, expiring token for an account activation or password reset.</summary>
public sealed class LocalCredentialLink : Entity
{
    public Guid LocalUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
