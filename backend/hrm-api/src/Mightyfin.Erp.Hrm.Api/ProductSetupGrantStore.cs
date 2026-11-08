using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProductSetupGrant(string HandleHash, string Subject, string ClientId,
    string AccessToken, string CsrfToken, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

/// <summary>Short-lived encrypted setup state only; never an ERP operational session.</summary>
internal sealed class ProductSetupGrantStore(string directory, string clientId, IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("Kuubu.WorkspaceSetupGrant.v1", clientId);
    private static bool Handle(string value) => Regex.IsMatch(value, "^[A-Za-z0-9_-]{43}\\z", RegexOptions.CultureInvariant);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    private string PathFor(string handle) => Path.Combine(directory, Hash(handle)+".grant");

    internal async Task<string> IssueAsync(string subject, string accessToken, DateTimeOffset expires, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
            || accessToken.Length is < 1 or > 16384 || !Regex.IsMatch(accessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant)
            || expires <= now || expires > now.AddMinutes(5)) throw new InvalidOperationException("Invalid setup grant.");
        var handle = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var grant = new ProductSetupGrant(Hash(handle), subject, clientId, accessToken,
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)), now, expires.ToUniversalTime());
        var encrypted = Encoding.UTF8.GetBytes(protector.Protect(JsonSerializer.Serialize(grant)));
        if (encrypted.Length > 32768) throw new InvalidOperationException("Invalid setup grant size.");
        await using var file = new FileStream(PathFor(handle), new FileStreamOptions { Mode = FileMode.CreateNew,
            Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous,
            UnixCreateMode = OperatingSystem.IsWindows() ? null : UnixFileMode.UserRead | UnixFileMode.UserWrite });
        await file.WriteAsync(encrypted, ct);
        await file.FlushAsync(ct);
        return handle;
    }

    internal async Task<ProductSetupGrant?> ReadAsync(string handle, string subject, CancellationToken ct)
    {
        if (!Handle(handle)) return null;
        var path = PathFor(handle);
        var info = new FileInfo(path);
        if (!info.Exists || info.LinkTarget is not null || info.Length is < 1 or > 32768) return null;
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) return null;
        try
        {
            var encrypted = await File.ReadAllTextAsync(path, ct);
            var grant = JsonSerializer.Deserialize<ProductSetupGrant>(protector.Unprotect(encrypted));
            var now = clock.GetUtcNow();
            return grant is not null && grant.HandleHash == Hash(handle) && grant.Subject == subject && grant.ClientId == clientId
                && grant.AccessToken is { Length: > 0 and <= 16384 } && Regex.IsMatch(grant.AccessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant)
                && grant.CsrfToken is not null && Handle(grant.CsrfToken)
                && grant.IssuedAt <= now.AddSeconds(5) && grant.ExpiresAt > now
                && grant.ExpiresAt > grant.IssuedAt && grant.ExpiresAt <= grant.IssuedAt.AddMinutes(5)
                ? grant : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or IOException) { return null; }
    }

    internal void Revoke(string handle)
    {
        if (Handle(handle)) File.Delete(PathFor(handle));
    }

    internal static bool CsrfMatches(ProductSetupGrant grant, string candidate) => Handle(candidate)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(grant.CsrfToken), Encoding.ASCII.GetBytes(candidate));

    internal void Purge()
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-10).UtcDateTime;
        foreach (var path in Directory.EnumerateFiles(directory, "*.grant", SearchOption.TopDirectoryOnly))
        {
            var file = new FileInfo(path);
            if (file.LinkTarget is null && file.LastWriteTimeUtc < cutoff) File.Delete(path);
        }
    }

    internal static void ValidateDirectory(string? path, string? keyDirectory, string? applicationDirectory = null)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(keyDirectory)
            || !Path.IsPathFullyQualified(path) || !Directory.Exists(path)
            || new DirectoryInfo(path).LinkTarget is not null || string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(keyDirectory)), comparison))
            throw new InvalidOperationException("A separate private setup-grant directory is required.");
        if (applicationDirectory is not null)
        {
            var relative = Path.GetRelativePath(applicationDirectory, path);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".."+Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Setup grants must be stored outside application assets.");
        }
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("The setup-grant directory must be private to the runtime user.");
    }
}

internal sealed class ProductSetupGrantCleanup(ProductSetupGrantStore store, ILogger<ProductSetupGrantCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { store.Purge(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { logger.LogWarning("Setup grant cleanup is unavailable."); }
        }
    }
}
