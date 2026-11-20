using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Legacy HRM services retain business validation; this additional product
/// boundary checks persisted sources and referenced owners, including logical
/// worker references that the older schema did not express as foreign keys.</summary>
internal sealed class ProductHrmWriteGuard(IHttpContextAccessor accessor, IConfiguration configuration) : SaveChangesInterceptor
{
    private static readonly MethodInfo ReadMethod = typeof(ProductHrmWriteGuard)
        .GetMethod(nameof(ReadOwned), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Dictionary<string, Type> LogicalReferences = new(StringComparer.Ordinal) {
        ["WorkerId"] = typeof(Worker), ["SubjectWorkerId"] = typeof(Worker),
        ["CandidateId"] = typeof(Candidate), ["LegalEntityId"] = typeof(LegalEntity),
        ["LocationId"] = typeof(WorkLocation), ["WorkLocationId"] = typeof(WorkLocation), ["OrgUnitId"] = typeof(OrgUnit),
    };
    internal async Task ValidateAsync(HrmDbContext db, HttpContext http, CancellationToken ct)
    {
        var scope = ProductRequestContext.Current(http);
        if (!ProductHrmWorkspace.RoleAllows(scope)) throw new ProductProcurementAuthorityException(403);
        var changed = db.ChangeTracker.Entries<Entity>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var added = changed.Where(e => e.State == EntityState.Added).ToDictionary(e => (e.Metadata.ClrType, e.Entity.Id), e => e.Entity);
        var checks = new Dictionary<Type, HashSet<Guid>>();
        void Require(Type type, Guid id)
        {
            if (added.TryGetValue((type, id), out var parent)) {
                if (parent.TenantId != scope!.TenantId) throw new ProductProcurementAuthorityException(403);
                return;
            }
            if (!checks.TryGetValue(type, out var ids)) checks[type] = ids = [];
            ids.Add(id);
        }
        foreach (var entry in changed)
        {
            if (entry.Entity.TenantId != scope!.TenantId || entry.Metadata.ClrType.Name.StartsWith("Core", StringComparison.Ordinal)
                || entry.Entity is LocalUser or ExternalIdentityLink or HrUserBranchAssignment or TenantRoleAssignment)
                throw new ProductProcurementAuthorityException(403);
            if (entry.State != EntityState.Added) Require(entry.Metadata.ClrType, entry.Entity.Id);
            foreach (var relation in entry.Metadata.GetForeignKeys())
            {
                var type = relation.PrincipalEntityType.ClrType;
                if (!typeof(Entity).IsAssignableFrom(type)) continue;
                var index = relation.PrincipalKey.Properties.ToList().FindIndex(p => p.Name == "Id" && p.ClrType == typeof(Guid));
                if (index >= 0 && entry.Property(relation.Properties[index].Name).CurrentValue is Guid id) Require(type, id);
            }
            foreach (var property in entry.Properties)
                if (property.CurrentValue is Guid id && LogicalReferences.TryGetValue(property.Metadata.Name, out var type)) Require(type, id);
        }
        // A batch import checks each target type once rather than issuing a
        // separate ownership query for every row/reference.
        foreach (var (type, ids) in checks)
        {
            var task = (Task<Guid[]>)ReadMethod.MakeGenericMethod(type).Invoke(null, [db, ids.ToArray(), scope!.TenantId, ct])!;
            var owned = await task;
            if (owned.Length != ids.Count) throw new ProductProcurementAuthorityException(403);
        }
    }
    private static Task<Guid[]> ReadOwned<T>(HrmDbContext db, Guid[] ids, string tenant, CancellationToken ct) where T : Entity
        => typeof(T) == typeof(Worker)
            ? db.Workers.AsNoTracking().Where(x => x.TenantId == tenant && ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct)
            : db.Set<T>().IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenant && ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (data.Context is HrmDbContext db && accessor.HttpContext is { } http
            && ProductHrmWorkspace.IsEndpoint(http, configuration) && ProductHrmWorkspace.HasAdmission(http))
            await ValidateAsync(db, http, cancellationToken);
        return result;
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result)
    {
        if (data.Context is HrmDbContext && accessor.HttpContext is { } http
            && ProductHrmWorkspace.IsEndpoint(http, configuration) && ProductHrmWorkspace.HasAdmission(http))
            throw new ProductProcurementAuthorityException(503);
        return result;
    }
}
