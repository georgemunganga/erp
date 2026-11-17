// M54 branch scoping: verify the workers import schema honours an explicit
// `locationId` column on insert and applies cleanly through the shared
// import/export engine (the route layer is what defaults the missing value to
// the operator's current work scope). SQLite in-memory per factory, isolated
// tenant.
using Xunit;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public class M54ImportScopeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Apply_Insert_HonorsLocationIdColumn(bool useName)
    {
        var svc = BuildServiceWithLocation(out var locationId);
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert",
            [new() { ["firstName"] = "E", ["lastName"] = "F", ["email"] = "e@example.com",
                ["phone"] = "0971234567", ["workerType"] = "employee", ["locationId"] = useName ? "  tEsT bRaNcH  " : locationId.ToString() }],
            CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal(locationId.ToString(), preview.Rows[0].Resolved!["locationId"]);
        var applied = await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);
        Assert.True(applied.Created == 1,
            string.Join(" | ", applied.RowOutcomes.Select(r => $"[{r.Status}] {r.Message}"))
                + $" | created={applied.Created} updated={applied.Updated} skipped={applied.Skipped}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preview_RejectsBranchOutsideCompanyOrConfinement(bool wrongCompany)
    {
        var scope = new ShellContext();
        if (wrongCompany) scope.EntityId = Guid.NewGuid();
        else scope.AllowedLocationIds.Add(Guid.NewGuid());
        var svc = BuildServiceWithLocation(out var branch, scope);
        var preview = await svc.PreviewAsync("workers", "scope.csv", "insert",
            [new() { ["firstName"]="Scope", ["lastName"]="Test", ["email"]="scope@example.invalid",
                ["phone"]="0971234567", ["workerType"]="Permanent", ["locationId"]=branch.ToString() }], CancellationToken.None);
        Assert.Equal(1, preview.WillError);
        Assert.Contains("outside your company or assigned branch access", preview.Rows.Single().Message);
        Assert.Equal(0, (await svc.ApplyAsync(preview.Id, [0], CancellationToken.None)).Created);
    }

    private static ImportExportServiceImpl BuildServiceWithLocation(out Guid locationId, ShellContext? scope = null)
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        var entity = new Domain.Entities.LegalEntity
        {
            Id = Guid.NewGuid(), Code = "TST", RegisteredName = "Test Ltd",
            TradingName = "Test", CountryCode = "ZM",
        };
        db.LegalEntities.Add(entity);
        db.SaveChanges();
        locationId = Guid.NewGuid();
        db.WorkLocations.Add(new Domain.Entities.WorkLocation
        {
            Id = locationId, Code = "TST", Name = "Test Branch",
            LegalEntityId = entity.Id,
        });
        db.SaveChanges();
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz(), scope ?? new ShellContext());
        return new ImportExportServiceImpl(new[] { schema });
    }
}
