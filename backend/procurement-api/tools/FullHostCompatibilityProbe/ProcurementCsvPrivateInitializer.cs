using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

/// <summary>Fresh private BE-01 owner/host setup, never a product API or production seeder.</summary>
internal static class ProcurementCsvPrivateInitializer
{
    private const string Database = "proc_csv_export_20261025_01";
    private const string Socket = "/authority-identity-private-socket";
    private const string SystemId = "7698678713374847009";
    private static readonly UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly UnixFileMode PrivateDirectory = PrivateFile | UnixFileMode.UserExecute;

    internal static async Task RunAsync()
    {
        var maintenance = Environment.GetEnvironmentVariable("BE01_CSV_MAINTENANCE_CONNECTION")
            ?? throw new InvalidOperationException("Private maintenance connection required.");
        var evidence = Environment.GetEnvironmentVariable("BE01_CSV_EVIDENCE_DIR")
            ?? throw new InvalidOperationException("Private evidence directory required.");
        var parsed = new NpgsqlConnectionStringBuilder(maintenance);
        Need(OperatingSystem.IsLinux() && parsed.Host == Socket && parsed.Database == "postgres"
            && parsed.Username == "postgres" && !parsed.Multiplexing && !parsed.Pooling,
            "Exact unpooled private PG maintenance socket required.");
        Need(Path.IsPathFullyQualified(evidence) && Directory.Exists(evidence)
            && (File.GetAttributes(evidence) & FileAttributes.ReparsePoint) == 0
            && File.GetUnixFileMode(evidence) == PrivateDirectory,
            "Existing private0700 evidence directory required.");
        var credentialsPath = Path.Combine(evidence, "be01-csv-credentials.private.json");
        var bootstrapPath = Path.Combine(evidence, "be01-csv-bootstrap.private.json");
        var completionPath = Path.Combine(evidence, "be01-csv-completion.private.json");
        var revokeRequestPath = Path.Combine(evidence, "be01-csv-revoke-request.private.json");
        var revokeReceiptPath = Path.Combine(evidence, "be01-csv-revoke-receipt.private.json");
        Need(!File.Exists(credentialsPath) && !File.Exists(bootstrapPath) && !File.Exists(completionPath)
            && !File.Exists(revokeRequestPath) && !File.Exists(revokeReceiptPath),
            "Fresh BE-01 evidence paths required.");
        await using (var connection = new NpgsqlConnection(maintenance))
        {
            await connection.OpenAsync();
            await using (var identity = new NpgsqlCommand(
                "SELECT system_identifier::text=@id AND current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')='' FROM pg_control_system()", connection))
            {
                identity.Parameters.AddWithValue("id", SystemId);
                Need(await identity.ExecuteScalarAsync() is true,
                    "Private PostgreSQL17 identity and no TCP required before SQL setup.");
            }
            await using (var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=@name)", connection))
            {
                exists.Parameters.AddWithValue("name", Database);
                Need(await exists.ExecuteScalarAsync() is false,
                    "Exact CSV database already exists; never drop or reuse.");
            }
            await using var create = new NpgsqlCommand("CREATE DATABASE " + Quote(Database), connection);
            await create.ExecuteNonQueryAsync();
        }
        var owner = new NpgsqlConnectionStringBuilder(parsed.ConnectionString) { Database = Database }.ConnectionString;
        var fixture = new PrincipalApprovalFixture(owner, browserBuyerProfile: false);
        var companyB = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var deniedBranch = Guid.NewGuid();
        var companyBBranch = Guid.NewGuid();
        var actors = await SeedCoreAsync(fixture, companyB, branchB, deniedBranch, companyBBranch);
        await using (var approval = fixture.Approvals())
        {
            Need(!approval.Database.HasPendingModelChanges(), "Core approval migration drift.");
            await approval.Database.MigrateAsync();
        }
        await using (var procurement = fixture.Procurement())
        {
            Need(!procurement.Database.HasPendingModelChanges(), "Procurement migration drift.");
            await procurement.Database.MigrateAsync();
        }
        var dataset = await SeedMastersAsync(owner, fixture, actors, companyB, companyBBranch);
        var roles = await CreateFiniteRolesAsync(owner);
        await WritePrivateAsync(credentialsPath, actors.ToDictionary(x => x.Key, x => new
            { email = x.Value.Email, password = x.Value.Password }));
        var credentialSha = Hash(await File.ReadAllBytesAsync(credentialsPath));
        await using var positive = new PrincipalApprovalHost(fixture, owner, roles.Record,
            roles.Record, actualSchema: true, normalLocalAuthentication: true, csvExportOnly: true,
            csvCoreConnection: roles.Core);
        await using var denied = new PrincipalApprovalHost(fixture, owner, roles.NoAudit,
            roles.NoAudit, actualSchema: true, normalLocalAuthentication: true, csvExportOnly: true,
            csvCoreConnection: roles.Core);
        foreach (var (host, record) in new[] { (positive, roles.Record), (denied, roles.NoAudit) })
        {
            var settings = host.Settings();
            Need(settings["ConnectionStrings:Hrm"] == roles.Core
                && settings["ConnectionStrings:Procurement"] == record
                && settings["ConnectionStrings:ProcurementApprovalExecution"] == record
                && settings["ConnectionStrings:Hrm"] != owner,
                "Private CSV host must use finite Core and Record connections only.");
        }
        positive.PrepareEntryPointEnvironment();
        positive.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        positive.StartServer();
        denied.PrepareEntryPointEnvironment();
        denied.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        denied.StartServer();
        var positiveUrl = OnlyLoopbackUrl(positive);
        var deniedUrl = OnlyLoopbackUrl(denied);
        Need(positiveUrl != deniedUrl, "Finite audit roles must use distinct loopback hosts.");
        var challenge = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        // The reviewed normal-cookie run includes large bounded CSV controls,
        // independent owner captures and real UI clicks after native setup.
        var deadline = DateTimeOffset.UtcNow.AddMinutes(30);
        await WritePrivateAsync(bootstrapPath, new
        {
            schemaVersion = 1, status = "PRIVATE_HOSTS_LISTENING_NOT_ACCEPTANCE",
            productSourceCommit = "86ed727774853140fee93a35aa6ce05362e71edf",
            databaseName = Database, privatePgSystemIdentifier = SystemId,
            positiveApiUrl = positiveUrl, auditDeniedApiUrl = deniedUrl,
            finiteRecordRole = roles.RecordRole, finiteAuditDeniedRole = roles.NoAuditRole,
            finiteCoreRole = roles.CoreRole,
            credentialsFile = new { path = credentialsPath, sha256 = credentialSha },
            companyAId = fixture.Company, companyBId = companyB,
            branchAId = fixture.Branch, branchBId = branchB, deniedBranchId = deniedBranch,
            actors = actors.ToDictionary(x => x.Key, x => new
                { principalId = x.Value.PrincipalId, credentialKey = x.Key,
                  roleKey = x.Value.RoleKey,
                  currentActions = x.Value.Actions.Split(',', StringSplitOptions.RemoveEmptyEntries) }),
            datasets = dataset, challenge, completionPath, expiresAt = deadline,
            revocationRequestPath = revokeRequestPath, revocationReceiptPath = revokeReceiptPath,
            revocationRoleKey = "be01_reader",
            revocationExpectedPermissionsSha256 = Hash(Encoding.UTF8.GetBytes(actors["reader"].Actions)),
            revocationNewPermissionsSha256 = Hash(Encoding.UTF8.GetBytes(RemoveExportAction(actors["reader"].Actions))),
            employeeFixtureCount = 0, productionTouched = false,
            browserUiVerified = false, ownerVerified = false
        });
        var revocationProcessed = false;
        while (!File.Exists(completionPath) && DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(revokeRequestPath))
            {
                Need(!revocationProcessed, "Only one synthetic Core revocation is supported.");
                await ApplyPrivateRevocationAsync(fixture, actors["reader"],
                    revokeRequestPath, revokeReceiptPath);
                revocationProcessed = true;
            }
            await Task.Delay(250);
        }
        Need(File.Exists(completionPath) && DateTimeOffset.UtcNow < deadline
            && (File.GetAttributes(completionPath) & FileAttributes.ReparsePoint) == 0
            && File.GetUnixFileMode(completionPath) == PrivateFile
            && new FileInfo(completionPath).Length is > 0 and < 4096,
            "Protected timely BE-01 control-plane completion required.");
        var completion = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(completionPath));
        Need(completion.GetProperty("challenge").GetString() == challenge
            && completion.GetProperty("databaseName").GetString() == Database
            && completion.GetProperty("status").GetString() == "PRIVATE_PROBES_FINISHED_NOT_OWNER_ACCEPTANCE",
            "BE-01 control-plane completion changed.");
        Console.WriteLine("PASS_PRIVATE_BE01_FIXTURE_CONTROL_PLANE_ONLY");
    }

    private static async Task<Dictionary<string, Actor>> SeedCoreAsync(PrincipalApprovalFixture fixture,
        Guid companyB, Guid branchB, Guid deniedBranch, Guid companyBBranch)
    {
        await using var core = fixture.Core();
        Need(!core.Database.HasPendingModelChanges(), "Core migration drift.");
        await core.Database.MigrateAsync();
        core.LegalEntities.AddRange(
            new LegalEntity { Id = fixture.Company, Code = "BE01_A", RegisteredName = "Private BE01 A", IsDefault = true },
            new LegalEntity { Id = companyB, Code = "BE01_B", RegisteredName = "Private BE01 B" });
        core.WorkLocations.AddRange(
            new WorkLocation { Id = fixture.Branch, Code = "BE01_A_HQ", Name = "Private HQ", Type = "headquarters", LegalEntityId = fixture.Company },
            new WorkLocation { Id = branchB, Code = "BE01_A_B", Name = "Private B", LegalEntityId = fixture.Company },
            new WorkLocation { Id = deniedBranch, Code = "BE01_A_DENY", Name = "Private denied", LegalEntityId = fixture.Company },
            new WorkLocation { Id = companyBBranch, Code = "BE01_B_HQ", Name = "Private foreign HQ", Type = "headquarters", LegalEntityId = companyB });
        // Public login branding is owned and sealed before finite Core runtime
        // starts; a GET must not need an implicit INSERT on this table.
        core.CompanyBrandings.Add(new CompanyBranding
            { DisplayName = "Private BE01", CompanyName = "Private BE01 company" });
        var definitions = new (string Name, Guid Id, string Role, string Actions)[]
        {
            ("reader", fixture.Maker, "be01_reader", "procurement-access,procurement-vendor-read,procurement-vendor-export"),
            ("manager", fixture.Checker, "be01_manager", "procurement-access,procurement-item-manage,procurement-item-export"),
            ("approver", fixture.Approver, "be01_approver", "procurement-access,procurement-vendor-read,procurement-vendor-approve,procurement-vendor-export"),
            ("invoiceCapture", fixture.ConfigMaker, "be01_invoice_capture", "procurement-access,procurement-vendor-read,procurement-invoice-capture,procurement-vendor-export"),
            ("noExport", fixture.ConfigReviewer, "be01_no_export", "procurement-access,procurement-vendor-read,procurement-item-manage"),
            ("legacyAdmin", fixture.Ungranted, "erp_admin", "erp_admin")
        };
        var actors = new Dictionary<string, Actor>();
        foreach (var (name, id, role, actions) in definitions)
        {
            var email = "be01-" + name.ToLowerInvariant() + "@example.test";
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!";
            var salt = RandomNumberGenerator.GetBytes(16);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
            var encoded = "pbkdf2-sha256$210000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
            core.TenantRoleAssignments.Add(new TenantRoleAssignment
                { RoleKey = role, RoleName = "Private " + role, Category = "system", PermissionsCsv = actions, Active = true });
            core.LocalUsers.Add(new LocalUser { Id = id, WorkerId = null, Email = email,
                NormalizedEmail = email.ToUpperInvariant(), DisplayName = "Private " + name,
                PasswordHash = encoded, RolesCsv = role, IsActive = true,
                MustChangePassword = false, PasswordChangedAt = DateTimeOffset.UtcNow });
            foreach (var branch in new[] { fixture.Branch, branchB })
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = id, LocationId = branch });
            if (name == "noExport")
                core.UserBranchAssignments.Add(new HrUserBranchAssignment
                    { UserId = id, LocationId = companyBBranch });
            actors.Add(name, new Actor(id, email, password, role, actions));
        }
        await core.SaveChangesAsync();
        Need(!await core.Workers.AnyAsync(), "BE-01 Core fixture must have zero employees.");
        return actors;
    }

    private static async Task<object> SeedMastersAsync(string owner, PrincipalApprovalFixture fixture,
        Dictionary<string, Actor> actors, Guid companyB, Guid companyBBranch)
    {
        var vendorIds = new Dictionary<string, Guid>();
        async Task Vendor(string key, string number, string name, string status, bool orderable,
            Guid company, Guid creator)
        {
            var scope = new PrincipalApprovalFixture.PrincipalScope(company,
                company == companyB ? companyBBranch : fixture.Branch, creator);
            await using var db = Store(owner, scope);
            var row = new Supplier { Number = number, DisplayName = name,
                LegalName = "Synthetic " + number, CountryCode = "ZM", Status = status,
                IsOrderable = orderable };
            db.AddScoped(row);
            await db.SaveChangesAsync();
            vendorIds.Add(key, row.Id);
        }
        await Vendor("active", "BE01FIELD-ACTIVE", "=BE01FIELD Active", "Active", true, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("approved", "BE01FIELD-APPROVED", "+BE01FIELD Approved", "Approved", true, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("ownDraft", "BE01FIELD-OWN", "Private own draft", "Draft", false, fixture.Company, actors["reader"].PrincipalId);
        await Vendor("hiddenDraft", "BE01FIELD-HIDDEN", "Private other draft", "Draft", false, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("submitted", "BE01FIELD-SUBMITTED", "Private submitted", "Submitted", false, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("suspended", "BE01FIELD-SUSPENDED", "@BE01FIELD Suspended", "Suspended", false, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("inactive", "BE01FIELD-INACTIVE", "Private inactive", "Inactive", false, fixture.Company, actors["noExport"].PrincipalId);
        await Vendor("companyB", "BE01FIELD-FOREIGN", "Private foreign", "Active", true, companyB, actors["noExport"].PrincipalId);
        var fieldIds = new List<Guid>();
        var excludedId = Guid.Empty;
        await using (var db = Store(owner,
            new PrincipalApprovalFixture.PrincipalScope(fixture.Company, fixture.Branch, actors["manager"].PrincipalId)))
        {
            for (var n = 1; n <= 10_001; n++)
            {
                var included = n <= 10_000;
                var field = n <= 3;
                var code = included ? $"BE01CAPINCLUDED-{(field ? "BE01FIELD-" : "")}{n:D5}"
                                    : "BE01CAPEXCLUDED-10001";
                var name = n switch
                {
                    1 => "=BE01FIELD item, one",
                    2 => "+BE01FIELD item \"two\"",
                    3 => "@BE01FIELD item\nthree",
                    _ => "Private item " + n.ToString("D5")
                };
                var row = new CatalogItem { Code = code, Name = name, Kind = "Goods",
                    CategoryCode = "BE01", UnitCode = "each", Status = "Active",
                    IsPurchasable = true, IsStockItem = true, IsAsset = false };
                db.AddScoped(row);
                if (field) fieldIds.Add(row.Id);
                if (!included) excludedId = row.Id;
                if (n % 500 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
            }
            await db.SaveChangesAsync();
        }
        Guid companyBItem;
        await using (var db = Store(owner,
            new PrincipalApprovalFixture.PrincipalScope(companyB, companyBBranch, actors["noExport"].PrincipalId)))
        {
            var row = new CatalogItem { Code = "BE01CAPINCLUDED-FOREIGN", Name = "Foreign private item",
                Kind = "Goods", CategoryCode = "BE01", UnitCode = "each", Status = "Active",
                IsPurchasable = true };
            db.AddScoped(row);
            await db.SaveChangesAsync();
            companyBItem = row.Id;
        }
        Need(fieldIds.Count == 3 && excludedId != Guid.Empty,
            "Exact synthetic item corpus unavailable.");
        return new
        {
            vendorVisibilityCaseIds = new Dictionary<string, Guid[]>
            {
                ["reader"] = [vendorIds["active"], vendorIds["approved"], vendorIds["ownDraft"]],
                ["approver"] = [vendorIds["active"], vendorIds["approved"], vendorIds["submitted"], vendorIds["suspended"]],
                ["invoiceCapture"] = [vendorIds["active"], vendorIds["approved"], vendorIds["suspended"], vendorIds["inactive"]]
            },
            itemFieldCaseIds = fieldIds, itemForbiddenCaseIds = new[] { companyBItem },
            itemVisibleCount = 10_001, filterQuery = new { vendors = "?search=BE01FIELD", items = "?search=BE01FIELD" },
            emptySearch = "BE01ABSENT", capSearch = "BE01CAP", exact10000Search = "BE01CAPINCLUDED",
            capRowCount = 10_001, excludedItemId = excludedId,
            companyBVendorId = vendorIds["companyB"], hiddenVendorId = vendorIds["hiddenDraft"]
        };
    }

    private static ProcurementDbContext Store(string owner, PrincipalApprovalFixture.PrincipalScope scope) =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(owner, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
            scope);

    private static async Task<(string Record, string NoAudit, string Core,
        string RecordRole, string NoAuditRole, string CoreRole)> CreateFiniteRolesAsync(string owner)
    {
        var random = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        var record = "be01_csv_record_" + random;
        var noAudit = "be01_csv_noaudit_" + random;
        var core = "be01_csv_core_" + random;
        var recordPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var noAuditPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var corePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        async Task Sql(string value)
        {
            await using var command = new NpgsqlCommand(value, connection);
            await command.ExecuteNonQueryAsync();
        }
        await Sql("SET log_statement=none; SET log_min_error_statement=panic; SET log_duration=off; SET log_min_duration_statement=-1");
        foreach (var (name, password) in new[] { (record, recordPassword), (noAudit, noAuditPassword),
            (core, corePassword) })
        {
            await using (var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@name)", connection))
            {
                exists.Parameters.AddWithValue("name", name);
                Need(await exists.ExecuteScalarAsync() is false, "Private finite role already exists.");
            }
            await Sql("CREATE ROLE " + Quote(name) + " LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '" + password + "'");
            await Sql("GRANT CONNECT ON DATABASE " + Quote(Database) + " TO " + Quote(name));
            if (name == core) continue;
            await Sql("GRANT USAGE ON SCHEMA procurement TO " + Quote(name));
            await Sql("GRANT SELECT ON procurement.suppliers,procurement.catalog_items,procurement.audit_events,procurement.__procurement_migrations TO " + Quote(name));
        }
        await Sql("GRANT INSERT ON procurement.audit_events TO " + Quote(record));
        await Sql("GRANT USAGE ON SCHEMA hrm TO " + Quote(core));
        await Sql("GRANT SELECT ON hrm.__hrm_migrations,hrm.local_users,hrm.local_sessions,"
            + "hrm.tenant_role_assignments,hrm.user_branch_assignments,hrm.legal_entities,"
            + "hrm.work_locations,hrm.org_units,hrm.workers,hrm.company_brandings,"
            + "hrm.audit_entries TO " + Quote(core));
        await Sql("GRANT UPDATE (failed_login_count,locked_until,last_login_at)"
            + " ON hrm.local_users TO " + Quote(core));
        await Sql("GRANT INSERT ON hrm.local_sessions,hrm.audit_entries TO " + Quote(core));
        await Sql("GRANT UPDATE (last_seen_at,revoked_at) ON hrm.local_sessions TO " + Quote(core));
        await using (var privileges = new NpgsqlCommand(
            "SELECT has_table_privilege(@record,'procurement.audit_events','INSERT')"
            + " AND NOT has_table_privilege(@denied,'procurement.audit_events','INSERT')"
            + " AND NOT has_table_privilege(@record,'procurement.suppliers','INSERT')"
            + " AND NOT has_table_privilege(@record,'procurement.catalog_items','INSERT')"
            + " AND NOT has_table_privilege(@denied,'procurement.suppliers','INSERT')"
            + " AND NOT has_table_privilege(@denied,'procurement.catalog_items','INSERT')", connection))
        {
            privileges.Parameters.AddWithValue("record", record);
            privileges.Parameters.AddWithValue("denied", noAudit);
            Need(await privileges.ExecuteScalarAsync() is true,
                "BE-01 finite Record and audit-denied privilege split failed.");
        }
        await using (var corePrivileges = new NpgsqlCommand(
            "SELECT has_table_privilege(@core,'hrm.local_users','SELECT')"
            + " AND has_column_privilege(@core,'hrm.local_users','last_login_at','UPDATE')"
            + " AND has_table_privilege(@core,'hrm.local_sessions','INSERT')"
            + " AND has_table_privilege(@core,'hrm.audit_entries','INSERT')"
            + " AND NOT has_table_privilege(@core,'hrm.local_users','INSERT')"
            + " AND NOT has_table_privilege(@core,'hrm.tenant_role_assignments','UPDATE')"
            + " AND NOT has_table_privilege(@core,'procurement.audit_events','INSERT')", connection))
        {
            corePrivileges.Parameters.AddWithValue("core", core);
            Need(await corePrivileges.ExecuteScalarAsync() is true,
                "BE-01 finite Core login/session rights changed.");
        }
        return (PrincipalApprovalRuntimePrivileges.RuntimeConnection(owner, record, recordPassword, false),
            PrincipalApprovalRuntimePrivileges.RuntimeConnection(owner, noAudit, noAuditPassword, false),
            PrincipalApprovalRuntimePrivileges.RuntimeConnection(owner, core, corePassword, false),
            record, noAudit, core);
    }

    private static string OnlyLoopbackUrl(PrincipalApprovalHost host)
    {
        var addresses = host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses.ToArray();
        Need(addresses is { Length: 1 } && Uri.TryCreate(addresses[0], UriKind.Absolute, out var url)
            && url.Scheme == "http" && url.Host == "127.0.0.1" && url.Port > 0,
            "One private ephemeral loopback host required.");
        return addresses![0];
    }

    private static async Task WritePrivateAsync(string path, object payload)
    {
        await using var output = new FileStream(path, new FileStreamOptions
        { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read, UnixCreateMode = PrivateFile });
        await JsonSerializer.SerializeAsync(output, payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Need(File.GetUnixFileMode(path) == PrivateFile, "BE-01 private output mode changed.");
    }

    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static string RemoveExportAction(string csv) => string.Join(',',
        csv.Split(',', StringSplitOptions.None).Where(x => x != "procurement-vendor-export"));

    private static async Task ApplyPrivateRevocationAsync(PrincipalApprovalFixture fixture, Actor reader,
        string requestPath, string receiptPath)
    {
        Need((File.GetAttributes(requestPath) & FileAttributes.ReparsePoint) == 0
            && File.GetUnixFileMode(requestPath) == PrivateFile
            && new FileInfo(requestPath).Length is > 0 and < 4096
            && !File.Exists(receiptPath), "Protected one-time Core revocation request required.");
        var request = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(requestPath));
        Need(request.ValueKind == JsonValueKind.Object && request.EnumerateObject().Count() == 5
            && request.GetProperty("schemaVersion").GetInt32() == 1
            && request.GetProperty("principalId").GetGuid() == reader.PrincipalId
            && request.GetProperty("action").GetString() == "procurement-vendor-export",
            "Unexpected synthetic Core revocation target.");
        var challenge = request.GetProperty("challenge").GetString() ?? "";
        var oldHash = Hash(Encoding.UTF8.GetBytes(reader.Actions));
        var next = RemoveExportAction(reader.Actions);
        var nextHash = Hash(Encoding.UTF8.GetBytes(next));
        Need(Regex.IsMatch(challenge, "^[a-f0-9]{64}$")
            && request.GetProperty("expectedPermissionsSha256").GetString() == oldHash
            && next != reader.Actions, "Exact original synthetic grant required.");
        await using var core = fixture.Core();
        var role = await core.TenantRoleAssignments.AsNoTracking().SingleAsync(x =>
            x.TenantId == PrincipalApprovalFixture.Tenant && x.RoleKey == reader.RoleKey
            && x.Active && !x.IsArchived);
        Need(role.PermissionsCsv == reader.Actions, "Synthetic Core role changed before CAS.");
        var updated = await core.TenantRoleAssignments.Where(x => x.Id == role.Id
            && x.TenantId == PrincipalApprovalFixture.Tenant
            && x.RoleKey == reader.RoleKey && x.PermissionsCsv == reader.Actions
            && x.Active && !x.IsArchived).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.PermissionsCsv, next)
                .SetProperty(x => x.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow)
                .SetProperty(x => x.UpdatedBy, "BE01_PRIVATE_REVOKE"));
        Need(updated == 1, "Single synthetic Core grant CAS failed.");
        await WritePrivateAsync(receiptPath, new
        {
            schemaVersion = 1, challenge, principalId = reader.PrincipalId,
            action = "procurement-vendor-export", oldPermissionsSha256 = oldHash,
            newPermissionsSha256 = nextHash, status = "CURRENT_CORE_ROLE_CAS_REVOKED"
        });
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static void Need(bool condition, string message) => PrincipalApprovalHttpVerification.Need(condition, message);
    private sealed record Actor(Guid PrincipalId, string Email, string Password,
        string RoleKey, string Actions);
}
