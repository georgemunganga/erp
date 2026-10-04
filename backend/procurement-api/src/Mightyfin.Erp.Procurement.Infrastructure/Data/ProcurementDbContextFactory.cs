using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed class ProcurementDbContextFactory : IDesignTimeDbContextFactory<ProcurementDbContext>
{
    public ProcurementDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Procurement")
            ?? "Host=127.0.0.1;Database=procurement_design_only;Username=invalid";
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__procurement_migrations", "procurement"))
            .Options;
        return new ProcurementDbContext(options, new DesignTimeScope());
    }
}

internal sealed class DesignTimeScope : IProcurementScope
{
    public string TenantId => "design-time";
    public Guid LegalEntityId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string SubjectId => "design-time";
    public string? CorrelationId => null;
}
