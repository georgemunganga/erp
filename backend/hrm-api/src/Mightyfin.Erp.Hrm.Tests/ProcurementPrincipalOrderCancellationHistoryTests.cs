using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderCancellationHistoryTests
{
    [Fact]
    public void Draft_cancellation_filters_every_retained_execution_family_before_releasing_allocations()
    {
        var person=Guid.NewGuid();
        var scope=new ProcurementRequestScope();
        scope.Set("po-cancel-history",Guid.NewGuid(),person.ToString("D"),Guid.NewGuid(),null,true,"test");
        scope.AssignPrincipal(person);
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=not_opened;Username=unused;Password=unused").Options,scope);
        var sql=ProcurementPrincipalOrderDraftRoutes.UnfundedCancellationCandidates(db,Guid.NewGuid())
            .ToQueryString();
        Assert.Contains("principal_order_funding_evidence",sql);
        Assert.Contains("principal_order_submissions",sql);
        Assert.Contains("principal_order_issued_versions",sql);
        Assert.Contains("principal_receiving_sources",sql);
        Assert.True(sql.Split("NOT EXISTS",StringSplitOptions.None).Length>=5);
        Assert.Contains("purchase_order_id",sql);
        Assert.False(db.ChangeTracker.HasChanges());
    }
}
