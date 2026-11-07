using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreWorkspaceProvisionerTests
{
    private const string Enrollment = "01arz3ndektsv4rrffq69g5fav";
    private const string Customer = "01arz3ndektsv4rrffq69g5faw";
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private static string Key(int n) => "synthetic_workspace_request_"+n.ToString("D8");

    [Fact]
    public async Task Explicit_create_is_atomic_role_free_and_idempotent_without_a_login_or_billing_record()
    {
        await using var f = await Fixture.Start();
        var created = await f.Create(Key(1));
        Assert.Equal(WorkspaceCreateStatus.Created, created.Status);
        var retry = await f.Create(Key(1));
        Assert.Equal(WorkspaceCreateStatus.Existing, retry.Status);
        Assert.Equal(created.CompanyId, retry.CompanyId);
        Assert.Equal(created.BindingId, retry.BindingId);
        Assert.Equal(WorkspaceCreateStatus.Conflict, (await f.Create(Key(1), "Another company")).Status);
        await using var db = f.Context("unrelated-default");
        Assert.Empty(await db.LegalEntities.ToArrayAsync());
        var binding = Assert.Single(await db.WorkspaceBindings.IgnoreQueryFilters().ToArrayAsync());
        Assert.Equal(1, binding.Slot);
        Assert.Equal(2, binding.PurchasedLimit);
        Assert.Equal(Customer, binding.CustomerId);
        Assert.Equal(Enrollment, binding.EnrollmentId);
        Assert.NotEqual("unrelated-default", binding.TenantId);
        Assert.Equal(64, binding.RequestHash.Length);
        Assert.Equal(64, binding.IdempotencyHash.Length);
        Assert.NotEqual(Key(1), binding.IdempotencyHash);
        await using var own = f.Context(binding.TenantId);
        Assert.Single(await own.LegalEntities.ToArrayAsync());
        Assert.Single(await own.WorkLocations.ToArrayAsync());
        Assert.Single(await own.ExternalIdentityLinks.ToArrayAsync());
        var owner = Assert.Single(await own.LocalUsers.ToArrayAsync());
        Assert.Equal(LocalCredentialModes.Federated, owner.CredentialMode);
        Assert.Equal("", owner.RolesCsv);
        Assert.Null(owner.PasswordHash); Assert.Null(owner.Email); Assert.False(owner.MustChangePassword);
        Assert.Empty(await own.LocalSessions.ToArrayAsync());
        Assert.Empty(await own.LocalCredentialLinks.ToArrayAsync());
        Assert.Empty(await own.TenantRoleAssignments.ToArrayAsync());
        Assert.Single(await own.AuditEntries.ToArrayAsync());
        Assert.Single(await own.OutboxMessages.ToArrayAsync());
    }

    [Fact]
    public async Task Multiple_companies_share_payer_capacity_but_have_separate_tenants_and_principals()
    {
        await using var f = await Fixture.Start();
        Assert.Equal(WorkspaceCreateStatus.Created, (await f.Create(Key(1), "First")).Status);
        Assert.Equal(WorkspaceCreateStatus.Created, (await f.Create(Key(2), "Second")).Status);
        Assert.Equal(WorkspaceCreateStatus.CapacityExhausted, (await f.Create(Key(3), "Third")).Status);
        await using var db = f.Context("inspect");
        var rows = await db.WorkspaceBindings.IgnoreQueryFilters().ToArrayAsync();
        Assert.Equal(new[] { 1, 2 }, rows.Select(x => x.Slot).Order().ToArray());
        Assert.Equal(2, rows.Select(x => x.TenantId).Distinct().Count());
        Assert.Equal(2, rows.Select(x => x.OwnerLocalUserId).Distinct().Count());
        foreach (var row in rows)
        {
            await using var scoped = f.Context(row.TenantId);
            Assert.Single(await scoped.LegalEntities.ToArrayAsync());
            Assert.Single(await scoped.LocalUsers.ToArrayAsync());
            Assert.Single(await scoped.ExternalIdentityLinks.ToArrayAsync());
        }
    }

    [Theory]
    [InlineData("zero")][InlineData("mismatch")][InlineData("foreign")][InlineData("expired")]
    public async Task Unpurchased_mismatched_or_expired_authority_cannot_create_partial_records(string fault)
    {
        await using var f = await Fixture.Start();
        var authority = f.Authority(fault == "zero" ? 0 : 2);
        var license = f.License(fault == "zero" ? 0 : fault == "mismatch" ? 3 : 2,
            fault == "foreign" ? Customer : Enrollment);
        if (fault == "expired") f.Clock.Advance();
        Assert.Equal(WorkspaceCreateStatus.Denied, (await f.Service.CreateAsync(authority, license, Key(1), "Company")).Status);
        await f.AssertEmpty();
    }

    [Theory]
    [InlineData("short", "Company")][InlineData("synthetic_workspace_request_00000001", " ")]
    [InlineData("synthetic_workspace_request_00000001", "Company\nInjected")]
    public async Task Invalid_request_has_no_side_effect(string key, string name)
    {
        await using var f = await Fixture.Start();
        Assert.Equal(WorkspaceCreateStatus.Invalid, (await f.Create(key, name)).Status);
        await f.AssertEmpty();
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Failure_or_expiry_after_database_writes_rolls_back_every_record(bool expiry)
    {
        await using var f = await Fixture.Start();
        f.Interceptor.Mode = expiry ? "expire" : "fail";
        Assert.Equal(expiry ? WorkspaceCreateStatus.Denied : WorkspaceCreateStatus.Unavailable,
            (await f.Create(Key(1))).Status);
        await f.AssertEmpty();
        f.Interceptor.Mode = "";
        Assert.Equal(WorkspaceCreateStatus.Created, (await f.Create(Key(1))).Status);
    }

    [Fact]
    public async Task Caller_cancellation_after_writes_propagates_and_rolls_back()
    {
        await using var f = await Fixture.Start();
        using var canceled = new CancellationTokenSource();
        f.Interceptor.Cancellation = canceled;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.CreateAsync(f.Authority(), f.License(), Key(1), "Company", canceled.Token));
        await f.AssertEmpty();
    }

    [Fact]
    public async Task Committed_binding_is_immutable_and_native_SQL_cannot_remove_or_reassign_it()
    {
        await using var f = await Fixture.Start();
        await f.Create(Key(1));
        await using var inspect = f.Context("inspect");
        var row = await inspect.WorkspaceBindings.IgnoreQueryFilters().SingleAsync();
        await using var db = f.Context(row.TenantId);
        var tracked = await db.WorkspaceBindings.SingleAsync();
        tracked.Slot = 2;
        await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
        if (db.Database.IsNpgsql())
        {
            var changed = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hrm.core_workspace_bindings SET slot = 2 WHERE id = {row.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, changed.SqlState);
            var removed = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM hrm.core_workspace_bindings WHERE id = {row.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, removed.SqlState);
        }
    }

    [NativeWorkspaceFact]
    public async Task Concurrent_creators_across_independent_contexts_never_exceed_purchased_capacity()
    {
        await using var f = await Fixture.Start();
        var results = await Task.WhenAll(Enumerable.Range(1, 12).Select(i => f.Create(Key(i), "Company "+i)));
        Assert.Equal(2, results.Count(x => x.Status == WorkspaceCreateStatus.Created));
        Assert.Equal(10, results.Count(x => x.Status == WorkspaceCreateStatus.CapacityExhausted));
        await using var db = f.Context("inspect");
        Assert.Equal(2, await db.WorkspaceBindings.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await db.LegalEntities.IgnoreQueryFilters().CountAsync());
    }

    [NativeWorkspaceFact]
    public async Task Concurrent_retries_return_one_company_and_one_audit_outbox_pair()
    {
        await using var f = await Fixture.Start();
        var results = await Task.WhenAll(Enumerable.Range(1, 12).Select(_ => f.Create(Key(1))));
        Assert.Single(results, x => x.Status == WorkspaceCreateStatus.Created);
        Assert.Equal(11, results.Count(x => x.Status == WorkspaceCreateStatus.Existing));
        Assert.Single(results.Select(x => x.CompanyId).Distinct());
        await using var db = f.Context("inspect");
        Assert.Equal(1, await db.AuditEntries.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    private sealed class NativeWorkspaceFactAttribute : FactAttribute
    {
        public NativeWorkspaceFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES")))
                Skip = "Requires the isolated PostgreSQL concurrency qualification.";
        }
    }

    internal sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        private long ticks;
        public override DateTimeOffset GetUtcNow() => now;
        public override long GetTimestamp() => ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Advance() { now += TimeSpan.FromMinutes(2); ticks += TimeSpan.FromMinutes(2).Ticks; }
    }

    internal sealed class SaveProbe(Clock clock) : SaveChangesInterceptor
    {
        internal string Mode = "";
        internal CancellationTokenSource? Cancellation;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (Mode == "expire") clock.Advance();
            if (Mode == "fail") throw new DbUpdateException("Synthetic post-save failure");
            if (Cancellation is not null) { Cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture(DbContextOptions<HrmDbContext> options, SqliteConnection? sqlite, Clock clock, SaveProbe interceptor) : IAsyncDisposable
    {
        internal Clock Clock => clock;
        internal SaveProbe Interceptor => interceptor;
        internal HrmDbContext Context(string tenant) => new(options, new Tenant(tenant));
        internal CoreWorkspaceProvisioner Service => new(Context, clock);
        internal WorkspaceSetupObservation Authority(int limit = 2) => new("42", Client, Customer, Enrollment, limit,
            clock.GetUtcNow().AddSeconds(60), clock, clock.GetTimestamp(), TimeSpan.FromSeconds(60));
        internal CommercialLicenseLease License(int limit = 2, string enrollment = Enrollment) => new(enrollment, new[] { "hrm" }, limit,
            clock.GetUtcNow().AddSeconds(60), clock, clock.GetTimestamp(), TimeSpan.FromSeconds(60));
        internal Task<WorkspaceCreateResult> Create(string key, string name = "Company") => Service.CreateAsync(Authority(), License(), key, name);
        internal async Task AssertEmpty()
        {
            await using var db = Context("inspect");
            Assert.Empty(await db.WorkspaceBindings.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.LegalEntities.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.ExternalIdentityLinks.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.AuditEntries.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.OutboxMessages.IgnoreQueryFilters().ToArrayAsync());
        }
        internal static async Task<Fixture> Start()
        {
            var clock = new Clock(); var interceptor = new SaveProbe(clock);
            var builder = new DbContextOptionsBuilder<HrmDbContext>().AddInterceptors(interceptor);
            SqliteConnection? sqlite = null;
            var native = Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES");
            if (!string.IsNullOrWhiteSpace(native))
            {
                var connection = new NpgsqlConnectionStringBuilder(native);
                Assert.Equal("kuubu_session_test_control", connection.Database);
                await using var control = new NpgsqlConnection(connection.ConnectionString);
                await control.OpenAsync();
                connection.Database = "workspace_allocate_"+Guid.NewGuid().ToString("N");
                await using var create = new NpgsqlCommand("CREATE DATABASE \""+connection.Database+"\"", control);
                await create.ExecuteNonQueryAsync();
                builder.UseNpgsql(connection.ConnectionString, pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm"));
            }
            else { sqlite = new SqliteConnection("Data Source=:memory:"); await sqlite.OpenAsync(); builder.UseSqlite(sqlite); }
            var fixture = new Fixture(builder.Options, sqlite, clock, interceptor);
            await using var db = fixture.Context("migration-only");
            if (!string.IsNullOrWhiteSpace(native))
            {
                await CoreSchemaStartup.EnsureAsync(db, "Isolated workspace allocation fixture", mayApply: true);
                Assert.False(db.Database.HasPendingModelChanges());
            }
            else await db.Database.EnsureCreatedAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync() { if (sqlite is not null) await sqlite.DisposeAsync(); }
        private sealed class Tenant(string value) : ITenantAccessor { public string GetTenantId() => value; }
    }
}
