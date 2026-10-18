using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanningDraftTests
{
    private sealed class Scope(string tenant, Guid entity) : IProcurementScope
    {
        public string TenantId => tenant;
        public Guid LegalEntityId => entity;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string SubjectId => "planning-owner";
        public string? CorrelationId => "plan-test";
    }

    [Fact]
    public void Rejects_invalid_amounts_and_duplicate_line_ids()
    {
        var line = new ProcurementPlanLineInput(null, null, "Laptops", "IT", "each", 2, 5000, null, null);
        Assert.Null(ProcurementPlanRules.Validate(new ProcurementPlanInput("FY-2027", "ZMW", null, [line])));
        Assert.NotNull(ProcurementPlanRules.Validate(new ProcurementPlanInput("FY-2027", "ZMW", null, [line with { EstimatedQuantity = 0 }])));
        Assert.NotNull(ProcurementPlanRules.Validate(new ProcurementPlanInput("FY-2027", "ZMW", null, [line with { EstimatedAmount = -1 }])));
        var repeated = Guid.NewGuid();
        Assert.NotNull(ProcurementPlanRules.Validate(new ProcurementPlanInput("FY-2027", "ZMW", null,
            [line with { Id = repeated }, line with { Id = repeated }])));
    }

    [Fact]
    public async Task Plan_lines_are_scoped_and_audited_with_parent()
    {
        var name = Guid.NewGuid().ToString();
        var entity = Guid.NewGuid();
        await using var db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name).Options,
            new Scope("tenant-a", entity));
        var plan = new ProcurementPlan
        {
            Number = "PLAN-TEST", FinancialPeriodRef = "FY-2027", CurrencyCode = "ZMW",
            OwnerSubjectId = "planning-owner",
        };
        plan.Lines.Add(new ProcurementPlanLine
        {
            ProcurementPlanId = plan.Id, LineNumber = 1, Description = "Laptops",
            UnitCode = "each", EstimatedQuantity = 2, EstimatedAmount = 5000,
        });
        db.AddScoped(plan);
        await db.SaveChangesAsync();
        var line = await db.ProcurementPlanLines.SingleAsync();
        Assert.Equal("tenant-a", line.TenantId);
        Assert.Equal(entity, line.LegalEntityId);
        Assert.Equal(plan.Id, line.ProcurementPlanId);
        Assert.Equal(2, await db.AuditEvents.CountAsync());

        await using var other = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name).Options,
            new Scope("tenant-b", entity));
        Assert.Empty(await other.ProcurementPlans.ToListAsync());
        Assert.Empty(await other.ProcurementPlanLines.ToListAsync());
    }
}
