using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Explicitly opted-in secret handoff for later browser acceptance in the
/// same disposable clone. Never put this payload in the normal result or logs.</summary>
internal static class PrivateBrowserFixture
{
    internal static void Validate(string path, string resultPath)
    {
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("A Linux owner-private browser fixture path is required.");
        var fixture = Path.GetFullPath(path);
        var result = Path.GetFullPath(resultPath);
        var folder = Path.GetDirectoryName(fixture);
        if (fixture == result || !fixture.EndsWith(".json", StringComparison.Ordinal)
            || folder is null || folder != Path.GetDirectoryName(result)
            || !Directory.Exists(folder) || new DirectoryInfo(folder).LinkTarget is not null
            || File.GetUnixFileMode(folder) != (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)
            || File.Exists(fixture) || Directory.Exists(fixture)
            || new FileInfo(fixture).LinkTarget is not null)
            throw new InvalidOperationException("A new .json handoff in the owner-private result directory is required.");
    }

    internal static async Task Write(string path, PaymentFixture fixture)
    {
        var roles = new HashSet<string>(["maker", "approver", "recorder", "confirmer",
            "policy_author", "policy_publisher", "outsider"], StringComparer.Ordinal);
        if (!Regex.IsMatch(fixture.Tenant, "^payment-http-[0-9a-f]{16}$", RegexOptions.CultureInvariant)
            || fixture.Company == Guid.Empty || fixture.Branch == Guid.Empty
            || fixture.OtherBranch == Guid.Empty || fixture.OtherBranch == fixture.Branch
            || fixture.Invoice == Guid.Empty || fixture.People.Count != roles.Count
            || fixture.People.Keys.Any(role => !roles.Contains(role)) || fixture.People.Any(entry =>
                entry.Value.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Value.Password)
                || entry.Value.Email != entry.Key + "." + fixture.Tenant + "@example.invalid"))
            throw new InvalidOperationException("The browser handoff contains a non-synthetic fixture.");
        var people = fixture.People.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { role = x.Key, principalId = x.Value.Id,
                x.Value.Email, x.Value.Password }).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            fixtureTenant = fixture.Tenant, companyId = fixture.Company,
            branchId = fixture.Branch, otherBranchId = fixture.OtherBranch,
            invoiceId = fixture.Invoice, people
        });
        var created = false;
        try
        {
            await using var file = new FileStream(path, new FileStreamOptions {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
            created = true;
            await file.WriteAsync(bytes);
            await file.FlushAsync();
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // A partial secret must not be offered as a browser fixture.
            try { if (created) File.Delete(path); } catch { }
            throw;
        }
    }
}
