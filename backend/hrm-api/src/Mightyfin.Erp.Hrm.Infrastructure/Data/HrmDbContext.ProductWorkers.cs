using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

public sealed partial class HrmDbContext
{
    public bool HasProductWorkerScope=>productWorkerScope?.Enabled==true;
    private bool WorkerScopeReady=>productWorkerScope?.IsCurrent==true && productWorkerScope.TenantId==tenant.GetTenantId();
    private string WorkerScopeTenant=>productWorkerScope?.TenantId??"untrusted-worker-scope";
    private Guid WorkerScopeCompany=>productWorkerScope?.CompanyId??Guid.Empty;
    private bool WorkerScopeAll=>productWorkerScope?.AllLocations==true;
    private Guid[] WorkerScopeLocations=>productWorkerScope?.LocationIds??[];
    private void ConfigureProductWorkerScope(ModelBuilder model)
    {
        // Always configure identical expressions: model caching must never bake
        // the first context's mode, tenant or branch list into another context.
        model.Entity<Worker>().HasQueryFilter(w=>w.TenantId==CurrentTenantId() && (!HasProductWorkerScope || WorkerScopeReady
            && w.TenantId==WorkerScopeTenant
            && WorkspaceBindings.Any(b=>b.TenantId==WorkerScopeTenant && b.LegalEntityId==WorkerScopeCompany && !b.IsArchived)
            && !LegalEntities.Any(e=>e.TenantId==WorkerScopeTenant && e.Id!=WorkerScopeCompany)
            && (!w.LocationId.HasValue || WorkLocations.Any(l=>l.TenantId==WorkerScopeTenant && l.Id==w.LocationId && l.LegalEntityId==WorkerScopeCompany && (WorkerScopeAll || !l.IsArchived)))
            && (!w.OrgUnitId.HasValue || OrgUnits.Any(o=>o.TenantId==WorkerScopeTenant && o.Id==w.OrgUnitId && o.LegalEntityId==WorkerScopeCompany))
            && (WorkerScopeAll || w.LocationId.HasValue && WorkerScopeLocations.Contains(w.LocationId.Value))));
        ChildFilter<EmergencyContact>(model);ChildFilter<WorkerBankDetail>(model);ChildFilter<WorkerEducation>(model);
        ChildFilter<ExternalWorkHistory>(model);ChildFilter<InternalWorkHistory>(model);
    }
    private void ChildFilter<T>(ModelBuilder model) where T:Entity
        =>model.Entity<T>().HasQueryFilter(e=>e.TenantId==CurrentTenantId() && (!HasProductWorkerScope || WorkerScopeReady
            && Workers.Any(w=>w.Id==EF.Property<Guid>(e,"WorkerId"))));
    private static bool WorkerChild(Entity e)=>e is EmergencyContact or WorkerBankDetail or WorkerEducation or Mightyfin.Erp.Hrm.Domain.Entities.ExternalWorkHistory or Mightyfin.Erp.Hrm.Domain.Entities.InternalWorkHistory;
    private static DomainException WorkerDenied()=>new("forbidden","The worker record or relationship is outside the selected company and branches.");
    private async Task GuardProductWorkerWritesAsync(CancellationToken ct)
    {
        if(!HasProductWorkerScope)return;
        var changes=ChangeTracker.Entries<Entity>().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if(!changes.Any(e=>e.Entity is Worker || WorkerChild(e.Entity)))return;
        if(!WorkerScopeReady || !await WorkspaceBindings.AnyAsync(b=>b.TenantId==WorkerScopeTenant && b.LegalEntityId==WorkerScopeCompany && !b.IsArchived,ct).ConfigureAwait(false)
            || await LegalEntities.AnyAsync(e=>e.TenantId==WorkerScopeTenant && e.Id!=WorkerScopeCompany,ct).ConfigureAwait(false))throw WorkerDenied();
        foreach(var entry in changes.Where(e=>e.Entity is Worker)) {
            var worker=(Worker)entry.Entity;
            if(worker.TenantId!=WorkerScopeTenant || entry.State!=EntityState.Added && !await Workers.AsNoTracking().AnyAsync(w=>w.Id==worker.Id,ct).ConfigureAwait(false))throw WorkerDenied();
            if(entry.State==EntityState.Deleted)throw WorkerDenied(); // Archive is the supported lifecycle.
            var original=entry.State==EntityState.Added?null:await Workers.IgnoreQueryFilters().AsNoTracking().Where(w=>w.Id==worker.Id).Select(w=>new OriginalWorker(w.SubjectId,w.LocationId,w.OrgUnitId)).SingleOrDefaultAsync(ct).ConfigureAwait(false);
            await GuardWorkerTarget(worker,original,ct).ConfigureAwait(false);
            if(entry.State==EntityState.Added?worker.SubjectId is not null:original is null || worker.SubjectId!=original.SubjectId)throw WorkerDenied();
        }
        foreach(var entry in changes.Where(e=>WorkerChild(e.Entity))) {
            if(entry.Entity.TenantId!=WorkerScopeTenant)throw WorkerDenied();
            var parent=(Guid)entry.Property("WorkerId").CurrentValue!;
            if(entry.State!=EntityState.Added)await GuardOriginalChild(entry,ct).ConfigureAwait(false);
            var added=changes.FirstOrDefault(e=>e.State==EntityState.Added && e.Entity is Worker && e.Entity.Id==parent);
            if(added is null && !await Workers.AsNoTracking().AnyAsync(w=>w.Id==parent,ct).ConfigureAwait(false))throw WorkerDenied();
        }
        if(!WorkerScopeReady)throw WorkerDenied();
    }
    private sealed record OriginalWorker(string? SubjectId,Guid? LocationId,Guid? OrgUnitId);
    private async Task GuardWorkerTarget(Worker worker,OriginalWorker? original,CancellationToken ct)
    {
        if(worker.LocationId is {} location) {
            var unchanged=original?.LocationId==location;
            if(!WorkerScopeAll && !WorkerScopeLocations.Contains(location)
                || !await WorkLocations.AnyAsync(l=>l.TenantId==WorkerScopeTenant && l.Id==location && l.LegalEntityId==WorkerScopeCompany && (!l.IsArchived || WorkerScopeAll && unchanged),ct).ConfigureAwait(false))throw WorkerDenied();
        }else if(!WorkerScopeAll)throw WorkerDenied();
        if(worker.OrgUnitId is {} unit){var unchanged=original?.OrgUnitId==unit;
            if(!await OrgUnits.AnyAsync(o=>o.TenantId==WorkerScopeTenant && o.Id==unit && o.LegalEntityId==WorkerScopeCompany && (!o.IsArchived || unchanged),ct).ConfigureAwait(false))throw WorkerDenied();}
        if(worker.ManagerId is {} manager && (manager==worker.Id || !await Workers.AsNoTracking().AnyAsync(w=>w.Id==manager,ct).ConfigureAwait(false)))throw WorkerDenied();
    }
    private async Task GuardOriginalChild(EntityEntry<Entity> entry,CancellationToken ct)
    {
        if(entry.Entity is EmergencyContact)await OriginalChild<EmergencyContact>(entry.Entity.Id,ct).ConfigureAwait(false);
        else if(entry.Entity is WorkerBankDetail)await OriginalChild<WorkerBankDetail>(entry.Entity.Id,ct).ConfigureAwait(false);
        else if(entry.Entity is WorkerEducation)await OriginalChild<WorkerEducation>(entry.Entity.Id,ct).ConfigureAwait(false);
        else if(entry.Entity is Mightyfin.Erp.Hrm.Domain.Entities.ExternalWorkHistory)await OriginalChild<ExternalWorkHistory>(entry.Entity.Id,ct).ConfigureAwait(false);
        else await OriginalChild<InternalWorkHistory>(entry.Entity.Id,ct).ConfigureAwait(false);
    }
    private async Task OriginalChild<T>(Guid id,CancellationToken ct) where T:Entity
    {
        var original=await Set<T>().IgnoreQueryFilters().AsNoTracking().Where(e=>e.Id==id).Select(e=>new{e.TenantId,WorkerId=EF.Property<Guid>(e,"WorkerId")}).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if(original is null || original.TenantId!=WorkerScopeTenant || !await Workers.AsNoTracking().AnyAsync(w=>w.Id==original.WorkerId,ct).ConfigureAwait(false))throw WorkerDenied();
    }
    // A company-owned numbering check returns only availability, never worker data.
    public async Task<bool> ProductEmployeeNumberExistsAsync(string number,CancellationToken ct)
    {
        if(!HasProductWorkerScope || !WorkerScopeReady)throw WorkerDenied();
        return await Workers.IgnoreQueryFilters().AnyAsync(w=>w.TenantId==WorkerScopeTenant && w.EmployeeNo==number,ct);
    }
}
