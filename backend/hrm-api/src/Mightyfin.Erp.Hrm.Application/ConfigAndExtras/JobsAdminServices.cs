using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>M28: jobs catalogue, tenant role assignments and retention rules —
/// the remaining admin CRUD surfaces, on top of the shared IConfigRepository
/// extensions.</summary>
public interface IJobsAdminService
{
    Task<List<JobDto>> ListJobsAsync(bool includeInactive, CancellationToken ct);
    Task<JobDto> CreateJobAsync(JobCreateRequest request, CancellationToken ct);
    Task<JobDto> UpdateJobAsync(Guid id, JobUpdateRequest request, CancellationToken ct);
    Task<JobDto> CloseJobAsync(Guid id, CancellationToken ct);

    Task<List<TenantRoleDto>> ListRolesAsync(CancellationToken ct);
    Task<TenantRoleDto> CreateRoleAsync(RoleCreateRequest request, CancellationToken ct);
    Task<TenantRoleDto> UpdateRoleAsync(string roleKey, RoleUpdateRequest request, CancellationToken ct);

    Task<List<DataRetentionDto>> ListRetentionRulesAsync(CancellationToken ct);
    Task<DataRetentionDto> CreateRetentionRuleAsync(DataRetentionCreateRequest request, CancellationToken ct);
    Task<DataRetentionDto> UpdateRetentionRuleAsync(Guid id, DataRetentionUpdateRequest request, CancellationToken ct);
    Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct);
}

public sealed class JobsAdminServiceImpl(IConfigRepository repo, IAuthzService authz) : IJobsAdminService
{
    // ================= Jobs =================

    public async Task<List<JobDto>> ListJobsAsync(bool includeInactive, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var jobs = await repo.ListJobsAsync(ct);
        var units = await repo.ListOrgUnitsAsync(ct);
        return jobs
            .Where(j => includeInactive || j.Status == "active")
            .OrderBy(j => j.Code)
            .Select(j => ToJobDto(j, units))
            .ToList();
    }

    public async Task<JobDto> CreateJobAsync(JobCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Title, "title");
        var existing = (await repo.ListJobsAsync(ct)).FirstOrDefault(j => j.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("job-code-taken", $"Job code '{request.Code}' is already in use.");
        var allUnits = await repo.ListOrgUnitsAsync(ct);
        if (request.OrgUnitId.HasValue)
        {
            if (allUnits.All(u => u.Id != request.OrgUnitId))
                throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
        }
        var job = new Job
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            OrgUnitId = request.OrgUnitId,
            Grade = request.Grade?.Trim(),
        };
        return ToJobDto(await repo.CreateJobAsync(job, ct), allUnits);
    }

    public async Task<JobDto> UpdateJobAsync(Guid id, JobUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var job = await repo.GetJobAsync(id, ct)
            ?? throw new DomainException("job-not-found", $"Job {id} does not exist.");
        if (request.Title is not null) job.Title = request.Title.Trim();
        if (request.Grade is not null) job.Grade = request.Grade.Trim();
        var allUnits = await repo.ListOrgUnitsAsync(ct);
        if (request.OrgUnitId.HasValue)
        {
            if (allUnits.All(u => u.Id != request.OrgUnitId))
                throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
            job.OrgUnitId = request.OrgUnitId;
        }
        return ToJobDto(await repo.UpdateJobAsync(job, ct), allUnits);
    }

    public async Task<JobDto> CloseJobAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var job = await repo.GetJobAsync(id, ct)
            ?? throw new DomainException("job-not-found", $"Job {id} does not exist.");
        job.Status = "inactive";
        var units = await repo.ListOrgUnitsAsync(ct);
        return ToJobDto(await repo.UpdateJobAsync(job, ct), units);
    }

    private static JobDto ToJobDto(Job j, List<OrgUnit> units) =>
        new(j.Id, j.Code, j.Title, j.OrgUnitId, units.FirstOrDefault(u => u.Id == j.OrgUnitId)?.Name, j.Grade, j.Status);

    // ================= Roles =================

    public async Task<List<TenantRoleDto>> ListRolesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "erp_admin");
        var rows = await repo.ListRoleAssignmentsAsync(ct);
        return rows.OrderBy(r => r.Category).ThenBy(r => r.RoleName).Select(ToRoleDto).ToList();
    }

    public async Task<TenantRoleDto> CreateRoleAsync(RoleCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "erp_admin");
        var key = NormalizeRoleKey(request.RoleKey);
        RequireNonEmpty(request.RoleName, "roleName");
        if ((await repo.ListRoleAssignmentsAsync(ct)).Any(r => r.RoleKey.Equals(key, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("role-key-taken", $"Role '{key}' already exists.");
        var category = NormalizeCategory(request.Category);
        var row = new TenantRoleAssignment
        {
            RoleKey = key,
            RoleName = request.RoleName.Trim(),
            Category = category,
            PermissionsCsv = string.Join(',', NormalizePermissions(category, request.Permissions ?? [])),
            Active = request.Active,
        };
        return ToRoleDto(await repo.CreateRoleAssignmentAsync(row, ct));
    }

    public async Task<TenantRoleDto> UpdateRoleAsync(string roleKey, RoleUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "erp_admin");
        var rows = await repo.ListRoleAssignmentsAsync(ct);
        var row = rows.FirstOrDefault(r => r.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
            throw new DomainException("role-not-found", $"Role '{roleKey}' is not managed for this tenant.");
        if (request.Active is not null) row.Active = request.Active.Value;
        if (request.RoleName is not null)
        {
            RequireNonEmpty(request.RoleName, "roleName");
            row.RoleName = request.RoleName.Trim();
        }
        if (request.Category is not null)
        {
            if (request.Permissions is null && NormalizeCategory(request.Category) != row.Category)
                throw new DomainException("permissions-required", "Choose permissions when changing a role category.");
            row.Category = NormalizeCategory(request.Category);
        }
        if (request.Permissions is not null) row.PermissionsCsv = string.Join(',', NormalizePermissions(row.Category, request.Permissions));
        EnsureAdminAccessRemains(rows);
        row = await repo.UpdateRoleAssignmentAsync(row, ct);
        return ToRoleDto(row);
    }

    public static string[] PermissionKeys = HrmStaffAccess.Roles
        .Concat(ProcurementActionCatalog.BootstrapGrants.Keys).ToArray();

    private static TenantRoleDto ToRoleDto(TenantRoleAssignment row)
    {
        var stored = ParseCsv(row.PermissionsCsv);
        var permissions = row.Category == "procurement"
            ? ProcurementActionCatalog.ResolveConfiguredActionsForRole(row.RoleKey, row.PermissionsCsv)
            : stored.DefaultIfEmpty(row.RoleKey).ToArray(); // Keep HRM/payroll/system edit projection.
        var effectiveProcurement = row.Active && !row.IsArchived
            ? ProcurementActionCatalog.ResolveConfiguredActionsForRole(row.RoleKey, row.PermissionsCsv) : [];
        return new(row.Id, row.RoleKey, row.RoleName, row.Category, row.Active, permissions, effectiveProcurement);
    }

    private static string NormalizeRoleKey(string? roleKey)
    {
        var key = (roleKey ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (key.Length < 2 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new DomainException("invalid-role-key", "Role key must use letters, numbers and underscores only.");
        return key;
    }

    private static string NormalizeCategory(string? category)
    {
        var value = (category ?? "hrm").Trim().ToLowerInvariant();
        return value is "hrm" or "payroll" or "system" or "procurement" ? value : "hrm";
    }

    private static string[] NormalizePermissions(string category, IEnumerable<string> permissions)
    {
        var allowed = new HashSet<string>(category == "procurement"
            ? ProcurementActionCatalog.BootstrapGrants.Keys
            : HrmStaffAccess.Roles, StringComparer.OrdinalIgnoreCase);
        var values = permissions.SelectMany(p => ParseCsv(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0 || values.Any(p => !allowed.Contains(p)))
            throw new DomainException("invalid-permissions", $"Select only supported {category} permissions for this role.");
        return values;
    }

    private static void EnsureAdminAccessRemains(IEnumerable<TenantRoleAssignment> rows)
    {
        if (!rows.Any(r => r.Active && !r.IsArchived && ParseCsv(r.PermissionsCsv).DefaultIfEmpty(r.RoleKey).Any(p => p.Equals("hr_admin", StringComparison.OrdinalIgnoreCase))))
            throw new DomainException("last-admin-role", "At least one active role must grant HRMS administration.");
    }

    private static string[] ParseCsv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ================= Retention rules =================

    public async Task<List<DataRetentionDto>> ListRetentionRulesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rows = await repo.ListRetentionRulesAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DataRetentionDto> CreateRetentionRuleAsync(DataRetentionCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.RecordType, "recordType");
        if (request.RetentionMonths <= 0 || request.RetentionMonths > 600)
            throw new DomainException("retention-months", "RetentionMonths must be between 1 and 600.");
        var normalized = request.RecordType.Trim().ToLowerInvariant();
        var existing = (await repo.ListRetentionRulesAsync(ct)).FirstOrDefault(r => r.RecordType.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("retention-rule-taken", $"A retention rule for '{request.RecordType}' already exists.");
        var rule = new RetentionRule
        {
            RecordType = request.RecordType.Trim().ToLowerInvariant(),
            RetentionMonths = request.RetentionMonths,
            Description = request.Description?.Trim(),
        };
        return ToDto(await repo.CreateRetentionRuleAsync(rule, ct));
    }

    public async Task<DataRetentionDto> UpdateRetentionRuleAsync(Guid id, DataRetentionUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rule = await repo.GetRetentionRuleAsync(id, ct)
            ?? throw new DomainException("retention-rule-not-found", $"Retention rule {id} does not exist.");
        if (request.RetentionMonths.HasValue)
        {
            if (request.RetentionMonths.Value <= 0 || request.RetentionMonths.Value > 600)
                throw new DomainException("retention-months", "RetentionMonths must be between 1 and 600.");
            rule.RetentionMonths = request.RetentionMonths.Value;
        }
        if (request.Description is not null) rule.Description = request.Description.Trim();
        if (request.Active.HasValue) rule.Active = request.Active.Value;
        return ToDto(await repo.UpdateRetentionRuleAsync(rule, ct));
    }

    public async Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rule = await repo.GetRetentionRuleAsync(id, ct)
            ?? throw new DomainException("retention-rule-not-found", $"Retention rule {id} does not exist.");
        await repo.DeleteRetentionRuleAsync(id, ct);
    }

    private static DataRetentionDto ToDto(RetentionRule r) => new(r.Id, r.RecordType, r.Description, r.RetentionMonths, r.Active);

    private static void RequireNonEmpty(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("field-required", $"{field} is required.");
    }
}
