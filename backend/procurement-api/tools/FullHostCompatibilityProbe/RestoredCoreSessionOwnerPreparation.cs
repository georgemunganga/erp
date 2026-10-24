using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

/// <summary>Owner-side synthetic Core fixture for a fresh private clone only.
/// The application host never receives this owner's connection.</summary>
internal static class RestoredCoreSessionOwnerPreparation
{
    private sealed record Actor(Guid Id, string Email, string Password);
    private sealed record Input(string Database, string Tenant, Guid CompanyId, Guid BranchId,
        Guid ForeignCompanyId, Guid ForeignBranchId, Actor Granted, Actor Ungranted,
        string OwnerBaselineSha256);

    private static void Need(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }

    internal static async Task RunAsync()
    {
        Need(Environment.GetEnvironmentVariable("P30_CORE_SESSION_OWNER_PREPARE") == "reviewed-private-only",
            "ExplicitPrivateOwnerPreparationRequired");
        const string path = "/evidence/core-owner-preparation-input.private.json";
        Need(File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
            && File.GetUnixFileMode(path) == (UnixFileMode.UserRead | UnixFileMode.UserWrite)
            && new FileInfo(path).Length <= 16_384, "ProtectedOwnerInputRequired");
        var input = JsonSerializer.Deserialize<Input>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidOperationException("OwnerInputMissing");
        Need(input.Database == "proc_p30_core_session_20261024_01"
            && input.Tenant == "p30-core-session-01"
            && Regex.IsMatch(input.OwnerBaselineSha256, "^[0-9a-f]{64}$")
            && new[] { input.CompanyId, input.BranchId, input.ForeignCompanyId,
                input.ForeignBranchId, input.Granted.Id, input.Ungranted.Id }.All(x => x != Guid.Empty)
            && input.CompanyId != input.ForeignCompanyId
            && input.BranchId != input.ForeignBranchId
            && input.Granted.Id != input.Ungranted.Id, "ExactFreshCoreFixtureIdentityRequired");
        foreach (var actor in new[] { input.Granted, input.Ungranted })
            Need(actor.Email.EndsWith("@core-session.example.test", StringComparison.Ordinal)
                && Regex.IsMatch(actor.Password, "^[0-9a-f]{64}$"),
                "PrivateSyntheticCoreActorRequired");
        Need(input.Granted.Email != input.Ungranted.Email, "DistinctSyntheticCoreAccountsRequired");

        // Invoke the actual Core LocalIdentity hasher. This private tool neither copies
        // its PBKDF2 implementation nor creates an alternative authentication format.
        var hasher = typeof(MonthlyLeaveAccrualWorker).Assembly.GetType("LocalPasswordHash", throwOnError: true)!;
        var hash = hasher.GetMethod("Hash", BindingFlags.Public | BindingFlags.Static,
            binder: null, types: [typeof(string)], modifiers: null);
        var verify = hasher.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static,
            binder: null, types: [typeof(string), typeof(string)], modifiers: null);
        Need(hash is not null && verify is not null, "ExistingCoreHasherRequired");
        string PasswordHash(string password)
        {
            var value = hash.Invoke(null, [password]) as string;
            Need(value is not null && verify.Invoke(null, [password, value]) is true,
                "ExistingCoreHasherVerificationRequired");
            return value!;
        }

        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "/private-pg-socket", Port = 5432, Database = input.Database,
            Username = "postgres", Pooling = false, Timeout = 10, CommandTimeout = 90,
            IncludeErrorDetail = false, LogParameters = false,
        }.ConnectionString;
        await using (var identity = new NpgsqlConnection(connection))
        {
            await identity.OpenAsync();
            await using var check = new NpgsqlCommand("""
                SELECT system_identifier::text='7698678713374847009'
                  AND current_setting('listen_addresses')=''
                  AND current_database()='proc_p30_core_session_20261024_01'
                  AND current_user='postgres'
                  AND inet_server_addr() IS NULL
                FROM pg_control_system()
                """, identity);
            Need(await check.ExecuteScalarAsync() is true, "ExactPrivateOwnerCloneRequired");
        }

        await using var core = RestoredCoreLocalSessionVerification.ModelContext(connection, input.Tenant);
        Need(!core.Database.HasPendingModelChanges(), "GenuineCoreModelParityRequired");
        await using var transaction = await core.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Need(!await core.LegalEntities.AnyAsync(x => x.TenantId == input.Tenant)
            && !await core.LocalUsers.AnyAsync(x => x.TenantId == input.Tenant)
            && !await core.TenantRoleAssignments.AnyAsync(x => x.TenantId == input.Tenant)
            && !await core.LocalSessions.AnyAsync(x => x.TenantId == input.Tenant),
            "FreshSyntheticTenantOnlyNoAdoption");
        core.LegalEntities.AddRange(
            new LegalEntity { Id = input.CompanyId, Code = "P30_CORE_01",
                RegisteredName = "Private Core session company", Currency = "ZMW", IsDefault = true },
            new LegalEntity { Id = input.ForeignCompanyId, Code = "P30_CORE_FOREIGN_01",
                RegisteredName = "Private Core session foreign company", Currency = "ZMW" });
        core.WorkLocations.AddRange(
            new WorkLocation { Id = input.BranchId, Code = "P30_CORE_BRANCH_01",
                Name = "Private Core session branch", LegalEntityId = input.CompanyId },
            new WorkLocation { Id = input.ForeignBranchId, Code = "P30_CORE_FOREIGN_BRANCH_01",
                Name = "Private Core session foreign branch", LegalEntityId = input.ForeignCompanyId });
        var grantedRole = "p30_core_granted_01";
        var ungrantedRole = "p30_core_ungranted_01";
        core.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = grantedRole, RoleName = "Private Core session access",
                Category = "system", PermissionsCsv = "procurement-access", Active = true },
            new TenantRoleAssignment { RoleKey = ungrantedRole, RoleName = "Private Core session denied",
                Category = "system", PermissionsCsv = "", Active = true });
        core.LocalUsers.AddRange(
            new LocalUser { Id = input.Granted.Id, WorkerId = null, Email = input.Granted.Email,
                NormalizedEmail = input.Granted.Email.ToUpperInvariant(), DisplayName = "Private granted Core actor",
                PasswordHash = PasswordHash(input.Granted.Password), RolesCsv = grantedRole,
                IsActive = true, MustChangePassword = false },
            new LocalUser { Id = input.Ungranted.Id, WorkerId = null, Email = input.Ungranted.Email,
                NormalizedEmail = input.Ungranted.Email.ToUpperInvariant(), DisplayName = "Private ungranted Core actor",
                PasswordHash = PasswordHash(input.Ungranted.Password), RolesCsv = ungrantedRole,
                IsActive = true, MustChangePassword = false });
        core.UserBranchAssignments.AddRange(
            new HrUserBranchAssignment { UserId = input.Granted.Id,
                UserEmail = input.Granted.Email, LocationId = input.BranchId },
            new HrUserBranchAssignment { UserId = input.Ungranted.Id,
                UserEmail = input.Ungranted.Email, LocationId = input.BranchId });
        await core.SaveChangesAsync();
        Need(!await core.Workers.AnyAsync(x => x.TenantId == input.Tenant)
            && await core.UserBranchAssignments.CountAsync(x => x.TenantId == input.Tenant) == 2
            && await core.LocalUsers.CountAsync(x => x.TenantId == input.Tenant) == 2,
            "ActualWorkerlessCoreFixtureRequired");
        await transaction.CommitAsync();
        await using var output = new FileStream("/result/core-owned-preparation.private.json",
            new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                Share = FileShare.None, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        await JsonSerializer.SerializeAsync(output, new
        {
            status = "CORE_OWNER_PREPARATION_WRITTEN_INDEPENDENT_READBACK_REQUIRED",
            database = input.Database, tenant = input.Tenant,
            actorIds = new[] { input.Granted.Id, input.Ungranted.Id },
            input.OwnerBaselineSha256, syntheticEntitiesRequested = 10,
            passwordHasher = "ExistingCoreLocalPasswordHash", workerRowsAdded = 0,
            productionTouched = false, ownerReadbackVerified = false,
        });
        Console.WriteLine("CORE_OWNER_PREPARATION_WRITTEN_INDEPENDENT_READBACK_REQUIRED");
    }
}
