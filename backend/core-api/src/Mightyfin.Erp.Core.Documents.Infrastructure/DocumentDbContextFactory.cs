using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed class DocumentDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    public DocumentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CORE_DOCUMENT_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=erp_design;Username=design;Password=design";
        return new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "core_document"))
            .Options, new DesignScope());
    }
    private sealed class DesignScope : IDocumentScope
    {
        public string TenantId => "design";
        public Guid LegalEntityId => Guid.Parse("00000000-0000-0000-0000-000000000001");
        public string SubjectId => "design";
    }
}
