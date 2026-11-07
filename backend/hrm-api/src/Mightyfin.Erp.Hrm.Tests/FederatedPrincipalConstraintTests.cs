using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class FederatedPrincipalConstraintTests
{
    [Fact]
    public async Task Existing_local_account_keeps_credentials_through_upgrade_and_defaults_to_local_mode()
    {
        await using var fixture = await Fixture.Start(seedLegacy: true);
        await using var db = fixture.Context();
        var user = await db.LocalUsers.SingleAsync();
        Assert.Equal("local", user.CredentialMode);
        Assert.Equal("legacy@example.test", user.Email);
        Assert.Equal("LEGACY@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.Equal(fixture.LegacyHash, user.PasswordHash);
        Assert.True(LocalPasswordHash.Verify("OriginalSyntheticPassword123!", user.PasswordHash));
        Assert.Equal("employee", user.RolesCsv);
    }
    [Fact]
    public async Task Multiple_federated_principals_have_null_credentials_no_default_roles_or_automatic_links()
    {
        await using var fixture = await Fixture.Start();
        await using var db = fixture.Context();
        db.LocalUsers.AddRange(LocalUser.Federated("First person"), LocalUser.Federated("Second person"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var users = await db.LocalUsers.ToArrayAsync();
        Assert.Equal(2, users.Length);
        Assert.All(users, user => { Assert.Null(user.Email); Assert.Null(user.NormalizedEmail); Assert.Null(user.PasswordHash);
            Assert.Equal("", user.RolesCsv); Assert.False(user.MustChangePassword); Assert.Null(user.PasswordChangedAt); });
        Assert.Empty(await db.ExternalIdentityLinks.ToArrayAsync());
        Assert.Empty(await db.LocalSessions.ToArrayAsync());
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("email")]
    [InlineData("normalized-email")]
    [InlineData("hash")]
    [InlineData("change-required")]
    [InlineData("changed-at")]
    [InlineData("empty-local-email")]
    [InlineData("empty-local-hash")]
    public async Task Context_rejects_invalid_credential_state_before_persistence(string fault)
    {
        await using var fixture = await Fixture.Start();
        await using var db = fixture.Context();
        var user = LocalUser.Federated("Explicit person");
        switch (fault)
        {
            case "mode": user.CredentialMode = "unknown"; break;
            case "email": user.Email = "invented@example.test"; break;
            case "normalized-email": user.NormalizedEmail = "INVENTED@EXAMPLE.TEST"; break;
            case "hash": user.PasswordHash = LocalPasswordHash.Hash("SyntheticPassword123!"); break;
            case "change-required": user.MustChangePassword = true; break;
            case "changed-at": user.PasswordChangedAt = DateTimeOffset.UtcNow; break;
            case "empty-local-email": user.CredentialMode = "local"; user.Email = " "; user.NormalizedEmail = "VALID"; user.PasswordHash = "hash"; break;
            case "empty-local-hash": user.CredentialMode = "local"; user.Email = "valid@example.test"; user.NormalizedEmail = "VALID@EXAMPLE.TEST"; user.PasswordHash = " "; break;
        }
        db.LocalUsers.Add(user);
        var error = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
        Assert.Equal("invalid-credential-state", error.Code);
    }

    [Fact]
    public async Task Mode_cannot_be_converted_through_context_or_raw_database_updates()
    {
        await using var fixture = await Fixture.Start();
        await using var db = fixture.Context();
        var user = LocalUser.Federated("Explicit person");
        db.LocalUsers.Add(user); await db.SaveChangesAsync();
        user.CredentialMode = "local"; user.Email = "new@example.test"; user.NormalizedEmail = "NEW@EXAMPLE.TEST"; user.PasswordHash = "hash";
        var error = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
        Assert.Equal("credential-mode-immutable", error.Code);
        db.ChangeTracker.Clear();
        if (db.Database.IsNpgsql())
        {
            var raw = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hrm.local_users SET credential_mode='local', email='new@example.test', normalized_email='NEW@EXAMPLE.TEST', password_hash='hash' WHERE id={user.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, raw.SqlState);
            Assert.Contains("Credential mode", raw.MessageText);
        }
        Assert.Equal("federated", (await db.LocalUsers.SingleAsync()).CredentialMode);
    }

    [Fact]
    public async Task Raw_database_write_cannot_add_password_or_login_contact_to_federated_principal()
    {
        await using var fixture = await Fixture.Start();
        await using var db = fixture.Context();
        var user = LocalUser.Federated("Explicit person");
        db.LocalUsers.Add(user); await db.SaveChangesAsync();
        var schema = db.Database.IsNpgsql() ? "hrm." : "";
        var query = "UPDATE "+schema+"local_users SET password_hash='credential' WHERE id={0}";
        if (db.Database.IsNpgsql())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(query, user.Id));
            Assert.Equal("CK_local_users_credential_state", error.ConstraintName);
        }
        else await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(query, user.Id));
        db.ChangeTracker.Clear(); Assert.Null((await db.LocalUsers.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Federated_link_keeps_same_tenant_FK_and_populated_rollback_retains_principal()
    {
        await using var fixture = await Fixture.Start();
        Guid id;
        await using (var db = fixture.Context())
        {
            var user = LocalUser.Federated("Explicit person"); db.LocalUsers.Add(user); await db.SaveChangesAsync(); id = user.Id;
            db.ExternalIdentityLinks.Add(new ExternalIdentityLink { Issuer = ProductCommercialLicense.Issuer, Subject = "17", LocalUserId = id });
            await db.SaveChangesAsync();
        }
        await using (var other = fixture.Context("other-tenant"))
        {
            other.ExternalIdentityLinks.Add(new ExternalIdentityLink { Issuer = ProductCommercialLicense.Issuer, Subject = "17", LocalUserId = id });
            await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        }
        await using var check = fixture.Context();
        if (check.Database.IsNpgsql())
        {
            var migrations = (await check.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.EndsWith("_CoreFederatedPrincipals", migrations[^1]);
            var error = await Assert.ThrowsAsync<PostgresException>(() => check.GetService<IMigrator>().MigrateAsync(migrations[^2]));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Contains(migrations[^1], await check.Database.GetAppliedMigrationsAsync());
        }
        Assert.Equal("federated", (await check.LocalUsers.SingleAsync()).CredentialMode);
        Assert.Single(await check.ExternalIdentityLinks.ToArrayAsync());
    }

    private sealed class Fixture(DbContextOptions<HrmDbContext> options, SqliteConnection? sqlite) : IAsyncDisposable
    {
        internal string LegacyHash { get; } = LocalPasswordHash.Hash("OriginalSyntheticPassword123!");
        internal HrmDbContext Context(string tenant = "federated-tests") => new(options, new FixedTenantAccessor(tenant));
        internal static async Task<Fixture> Start(bool seedLegacy = false)
        {
            var builder = new DbContextOptionsBuilder<HrmDbContext>();
            SqliteConnection? sqlite = null;
            var native = Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES");
            if (!string.IsNullOrWhiteSpace(native))
            {
                var connection = new NpgsqlConnectionStringBuilder(native);
                Assert.Equal("kuubu_session_test_control", connection.Database);
                var name = "federated_principal_"+Guid.NewGuid().ToString("N");
                await using var control = new NpgsqlConnection(connection.ConnectionString);
                await control.OpenAsync();
                await using var create = new NpgsqlCommand("CREATE DATABASE \""+name+"\"", control);
                await create.ExecuteNonQueryAsync();
                connection.Database = name;
                builder.UseNpgsql(connection.ConnectionString, pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm"));
            }
            else { sqlite = new SqliteConnection("Data Source=:memory:"); await sqlite.OpenAsync(); builder.UseSqlite(sqlite); }
            var fixture = new Fixture(builder.Options, sqlite);
            await using var db = fixture.Context();
            if (!string.IsNullOrWhiteSpace(native))
            {
                if (seedLegacy)
                {
                    await db.GetService<IMigrator>().MigrateAsync("20261107170241_CoreExternalIdentityLinks");
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO hrm.local_users (id,email,normalized_email,display_name,password_hash,roles_csv,
                            is_active,must_change_password,failed_login_count,tenant_id,created_at,created_by,is_archived)
                        VALUES ({Guid.NewGuid()},'legacy@example.test','LEGACY@EXAMPLE.TEST','Legacy person',{fixture.LegacyHash},
                            'employee',TRUE,FALSE,0,'federated-tests',{DateTimeOffset.UtcNow},'upgrade-fixture',FALSE)
                        """);
                }
                await CoreSchemaStartup.EnsureAsync(db, "Isolated federated principal fixture", mayApply: true);
                Assert.False(db.Database.HasPendingModelChanges());
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
                if (seedLegacy)
                {
                    db.LocalUsers.Add(new LocalUser { Email = "legacy@example.test", NormalizedEmail = "LEGACY@EXAMPLE.TEST",
                        DisplayName = "Legacy person", PasswordHash = fixture.LegacyHash, RolesCsv = "employee", MustChangePassword = false });
                    await db.SaveChangesAsync();
                }
            }
            return fixture;
        }
        public async ValueTask DisposeAsync() { if (sqlite is not null) await sqlite.DisposeAsync(); }
    }
}
