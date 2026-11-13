using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>SQLite normally; the dedicated native job sets the PostgreSQL control
/// connection and exercises these same invariants through all genuine migrations.</summary>
public sealed class ProductIdentityLinkConstraintTests
{
    [Fact]
    public async Task Relational_FK_rejects_a_link_to_a_user_from_another_tenant()
    {
        await using var fixture = await Fixture.Start();
        var user = await fixture.User("tenant-a");
        await using var wrongTenant = fixture.Context("tenant-b");
        wrongTenant.ExternalIdentityLinks.Add(Link(user));
        await Assert.ThrowsAsync<DbUpdateException>(() => wrongTenant.SaveChangesAsync());
        await using var check = fixture.Context("tenant-a");
        Assert.Empty(await check.ExternalIdentityLinks.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public async Task An_explicit_person_can_have_separate_principals_in_separate_tenants()
    {
        await using var fixture = await Fixture.Start();
        foreach (var tenant in new[] { "tenant-a", "tenant-b" })
        {
            var user = await fixture.User(tenant);
            await using var db = fixture.Context(tenant);
            db.ExternalIdentityLinks.Add(Link(user));
            await db.SaveChangesAsync();
        }
        await using var check = fixture.Context("tenant-a");
        Assert.Single(await check.ExternalIdentityLinks.ToArrayAsync());
        Assert.Equal(2, await check.ExternalIdentityLinks.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await check.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_same_tenant_subject_cannot_be_rebound_even_after_revocation(bool revoke)
    {
        await using var fixture = await Fixture.Start();
        var first = await fixture.User("tenant-a", "first");
        var second = await fixture.User("tenant-a", "second");
        await using var db = fixture.Context("tenant-a");
        var row = Link(first);
        row.IsActive = !revoke;
        db.ExternalIdentityLinks.Add(row);
        await db.SaveChangesAsync();
        if (db.Database.IsNpgsql())
        {
            var changed = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hrm.external_identity_links SET subject = 'changed' WHERE id = {row.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, changed.SqlState);
            var removed = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM hrm.external_identity_links WHERE id = {row.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, removed.SqlState);
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            // Later additive migrations must not make this owning migration's
            // retained-evidence guard disappear from acceptance.
            var owning = Assert.Single(applied, id => id.EndsWith("_CoreExternalIdentityLinks", StringComparison.Ordinal));
            var beforeOwning = applied[Array.IndexOf(applied, owning)-1];
            var rollback = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(beforeOwning));
            Assert.Equal(PostgresErrorCodes.CheckViolation, rollback.SqlState);
            Assert.Contains(owning, await db.Database.GetAppliedMigrationsAsync());
            Assert.Single(await db.ExternalIdentityLinks.AsNoTracking().ToArrayAsync());
        }
        db.ExternalIdentityLinks.Add(Link(second));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Hard_deleting_a_linked_user_cannot_silently_destroy_link_evidence()
    {
        await using var fixture = await Fixture.Start();
        var user = await fixture.User("tenant-a");
        await using var db = fixture.Context("tenant-a");
        db.ExternalIdentityLinks.Add(Link(user));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // exercise the database FK, not tracked fixup
        db.LocalUsers.Remove(await db.LocalUsers.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static ExternalIdentityLink Link(Guid user) => new()
    { Issuer = "https://commercial-api.greenwebb.tech", Subject = "17", LocalUserId = user };

    private sealed class Fixture(DbContextOptions<HrmDbContext> options, SqliteConnection? sqlite) : IAsyncDisposable
    {
        internal HrmDbContext Context(string tenant) => new(options, new FixedTenantAccessor(tenant));
        internal async Task<Guid> User(string tenant, string name = "person")
        {
            await using var db = Context(tenant);
            var user = new LocalUser { Email = name+"@example.test", NormalizedEmail = name.ToUpperInvariant()+"@EXAMPLE.TEST",
                DisplayName = name, PasswordHash = "not-a-password" };
            db.LocalUsers.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }
        internal static async Task<Fixture> Start()
        {
            var builder = new DbContextOptionsBuilder<HrmDbContext>();
            SqliteConnection? sqlite = null;
            var native = Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES");
            if (!string.IsNullOrWhiteSpace(native))
            {
                var connection = new NpgsqlConnectionStringBuilder(native);
                Assert.Equal("kuubu_session_test_control", connection.Database);
                var name = "product_identity_"+Guid.NewGuid().ToString("N");
                await using var control = new NpgsqlConnection(connection.ConnectionString);
                await control.OpenAsync();
                await using var create = new NpgsqlCommand("CREATE DATABASE \""+name+"\"", control);
                await create.ExecuteNonQueryAsync();
                connection.Database = name;
                builder.UseNpgsql(connection.ConnectionString, pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm"));
            }
            else
            {
                sqlite = new SqliteConnection("Data Source=:memory:");
                await sqlite.OpenAsync();
                builder.UseSqlite(sqlite);
            }
            var fixture = new Fixture(builder.Options, sqlite);
            await using var db = fixture.Context("migration-only");
            if (!string.IsNullOrWhiteSpace(native))
            {
                await CoreSchemaStartup.EnsureAsync(db, "Isolated product identity link fixture", mayApply: true);
                Assert.False(db.Database.HasPendingModelChanges());
            }
            else await db.Database.EnsureCreatedAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            if (sqlite is not null) await sqlite.DisposeAsync();
            // Native databases remain retained evidence; never drop a supplied DB.
        }
    }
}
