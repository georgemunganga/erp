using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductSetupGrantStoreTests
{
    private const string Client = "4798a03c-5ed7-4438-9ec9-d46f63a2f4f1";
    private const string Token = "synthetic.private.setup.token";

    [Fact]
    public async Task Opaque_handle_points_only_to_a_private_encrypted_short_lived_person_grant()
    {
        var (path, protection, clock, store) = Fixture();
        var handle = await store.IssueAsync("17", Token, clock.Now.AddMinutes(5), default);
        var file = Assert.Single(Directory.GetFiles(path));
        Assert.DoesNotContain(handle, Path.GetFileName(file));
        var ciphertext = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain(Token, ciphertext);
        var grant = await store.ReadAsync(handle, "17", default);
        Assert.NotNull(grant); Assert.Equal(Token, grant.AccessToken);
        Assert.Equal(Client, grant.ClientId);
        Assert.DoesNotContain(grant.CsrfToken, ciphertext);
        Assert.True(ProductSetupGrantStore.CsrfMatches(grant, grant.CsrfToken));
        Assert.False(ProductSetupGrantStore.CsrfMatches(grant, new string('x', 43)));
        Assert.Null(await store.ReadAsync(handle, "18", default));
        var other = new ProductSetupGrantStore(path, Guid.NewGuid().ToString(), protection, clock);
        Assert.Null(await other.ReadAsync(handle, "17", default));
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        clock.Now += TimeSpan.FromMinutes(5);
        Assert.Null(await store.ReadAsync(handle, "17", default));
    }

    [Fact]
    public async Task Grant_reference_payload_and_ciphertext_cannot_be_swapped_or_tampered()
    {
        var (path, _, clock, store) = Fixture();
        var handle = await store.IssueAsync("17", Token, clock.Now.AddMinutes(5), default);
        var file = Assert.Single(Directory.GetFiles(path));
        var other = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var moved = Path.Combine(path, Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(other)))+".grant");
        File.Move(file, moved);
        Assert.Null(await store.ReadAsync(other, "17", default));
        Assert.Null(await store.ReadAsync(handle, "17", default));
        File.Move(moved, file);
        var text = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, "tampered"+text);
        Assert.Null(await store.ReadAsync(handle, "17", default));
        Assert.Null(await store.ReadAsync("../foreign", "17", default));
    }

    [Fact]
    public async Task Revocation_and_bounded_cleanup_remove_only_owned_grant_records()
    {
        var (path, _, clock, store) = Fixture();
        var handle = await store.IssueAsync("17", Token, clock.Now.AddMinutes(5), default);
        store.Revoke(handle);
        Assert.Null(await store.ReadAsync(handle, "17", default));
        store.Revoke(handle);
        await store.IssueAsync("17", Token, clock.Now.AddMinutes(5), default);
        var file = Assert.Single(Directory.GetFiles(path));
        var unrelated = Path.Combine(path, "operator-note.txt");
        await File.WriteAllTextAsync(unrelated, "retain");
        File.SetLastWriteTimeUtc(file, clock.Now.AddMinutes(-11).UtcDateTime);
        store.Purge();
        Assert.True(File.Exists(unrelated)); Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Unbounded_expired_or_invalid_credential_state_is_never_written()
    {
        var (path, _, clock, store) = Fixture();
        foreach (var expiry in new[] { clock.Now, clock.Now.AddMinutes(6) })
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.IssueAsync("17", Token, expiry, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.IssueAsync("017", Token, clock.Now.AddMinutes(5), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.IssueAsync("17", "token\r\nheader", clock.Now.AddMinutes(5), default));
        Assert.Empty(Directory.GetFiles(path));
    }

    [Fact]
    public void Grant_directory_must_be_private_separate_and_outside_application_assets()
    {
        var (path, _, _, _) = Fixture();
        Assert.Throws<InvalidOperationException>(() => ProductSetupGrantStore.ValidateDirectory(path, path+Path.DirectorySeparatorChar));
        var assets = Path.Combine(path, "application"); Directory.CreateDirectory(assets);
        var child = Path.Combine(assets, "private"); Directory.CreateDirectory(child);
        Assert.Throws<InvalidOperationException>(() => ProductSetupGrantStore.ValidateDirectory(child, path, assets));
        Assert.Throws<InvalidOperationException>(() => ProductSetupGrantStore.ValidateDirectory("relative", path));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead);
            Assert.Throws<InvalidOperationException>(() => ProductSetupGrantStore.ValidateDirectory(path, assets));
        }
    }

    private static (string, IDataProtectionProvider, Clock, ProductSetupGrantStore) Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "kuubu-setup-store-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var protection = new EphemeralDataProtectionProvider(); var clock = new Clock();
        return (root, protection, clock, new ProductSetupGrantStore(root, Client, protection, clock));
    }
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
