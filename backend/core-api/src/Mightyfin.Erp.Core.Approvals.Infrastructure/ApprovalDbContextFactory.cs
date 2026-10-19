using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Core.Approvals.Infrastructure;

public sealed class ApprovalDbContextFactory : IDesignTimeDbContextFactory<ApprovalDbContext>
{
    public ApprovalDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CORE_APPROVAL_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=erp_design;Username=design;Password=design";
        var options = new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval"))
            .Options;
        return new ApprovalDbContext(options, new DesignScope());
    }

    private sealed class DesignScope : IApprovalScope
    {
        public string TenantId => "design";
        public Guid LegalEntityId => Guid.Parse("00000000-0000-0000-0000-000000000001");
        public string SubjectId => "design";
    }
}
