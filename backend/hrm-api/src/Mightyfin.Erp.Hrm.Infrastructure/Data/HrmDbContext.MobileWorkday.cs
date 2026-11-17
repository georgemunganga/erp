using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

public sealed partial class HrmDbContext
{
    public DbSet<MobileWorkdayEvent> MobileWorkdayEvents => Set<MobileWorkdayEvent>();
    private static void ConfigureMobileWorkday(ModelBuilder modelBuilder)
    {
        ConfigureEntity<MobileWorkdayEvent>(modelBuilder,"mobile_workday_events",e=>{
            e.HasIndex(x=>new{x.TenantId,x.WorkerId,x.RequestId}).IsUnique();
            e.HasIndex(x=>new{x.TenantId,x.AttendanceRecordId,x.Revision}).IsUnique();
            e.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.WorkerId,x.RecordedAt});
            e.Property(x=>x.Kind).HasMaxLength(32);e.Property(x=>x.StateAfter).HasMaxLength(32);
            e.Property(x=>x.Reason).HasMaxLength(300);e.Property(x=>x.Summary).HasMaxLength(2000);
            e.Property(x=>x.Blockers).HasMaxLength(1000);e.Property(x=>x.RequestHash).HasMaxLength(64);
            e.HasOne<AttendanceRecord>().WithMany().HasForeignKey(x=>x.AttendanceRecordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Worker>().WithMany().HasForeignKey(x=>x.WorkerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LegalEntity>().WithMany().HasForeignKey(x=>x.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WorkLocation>().WithMany().HasForeignKey(x=>x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
