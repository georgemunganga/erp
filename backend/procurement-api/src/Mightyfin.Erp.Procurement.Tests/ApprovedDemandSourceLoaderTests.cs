using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ApprovedDemandSourceLoaderTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope, IApprovalScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "shared-source"; }
    private static readonly Scope Current = new("source-loader", Guid.NewGuid(), "buyer");
    private static DbContextOptions<ProcurementDbContext> Options() => new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public async Task Empty_or_unknown_target_types_do_not_query_any_target_store_and_are_not_authority()
    {
        var db = new ProcurementDbContext(Options(), Current); db.Dispose();
        Assert.Empty(await ApprovedDemandSourceLoader.LoadTargetOwnersAsync(db, Guid.NewGuid(), []));
        var unknown = new PurchaseRequestAllocation { TargetType = "UnknownFutureTarget", TargetId = Guid.NewGuid(), Status = "Reserved" };
        Assert.Empty(await ApprovedDemandSourceLoader.LoadTargetOwnersAsync(db, Guid.NewGuid(), new[] { unknown }));
        // The owning reducer still rejects the retained unknown row; no target query is not verified zero history.
        var f = new PurchaseOrderSourceAllocationTests.Fixture();
        f.Ledger.Add(unknown);
        Assert.Throws<InvalidOperationException>(() => PurchaseDemandAllocationBalance.Evaluate(f.Request, f.Line,
            f.Ledger, f.Owners, f.Request.TenantId, f.Company));
    }

    [Fact]
    public async Task Missing_request_exits_before_any_Core_approval_store_query()
    {
        await using var db = new ProcurementDbContext(Options(), Current);
        var core = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Current);
        core.Dispose();
        var error = await Assert.ThrowsAsync<ApprovedDemandLoadException>(() => ApprovedDemandSourceLoader.LoadAsync(db,
            core, Current, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(ApprovedDemandLoadFailure.SourceNotFound, error.Failure); Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Runtime_owner_loading_retains_inactive_PO_and_cancelled_RFQ_lines_and_scopes_actual_parents()
    {
        var options = Options(); Guid requestLine = Guid.NewGuid();
        var po = new PurchaseOrder { Number = "PO-INACTIVE", SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", BuyerSubjectId = Current.SubjectId };
        var poLine = new PurchaseOrderLine { PurchaseOrderId = po.Id, PurchaseRequestLineId = requestLine,
            LineNumber = 1, Description = "Retained", UnitCode = "each", Quantity = 1, UnitPrice = 1, IsActive = false };
        var rfq = new SourcingEvent { Number = "RF-RETAINED", Title = "Retained", Method = "RFQ", CurrencyCode = "ZMW", OwnerSubjectId = Current.SubjectId, Status = "Cancelled" };
        var rfqLine = new SourcingLine { SourcingEventId = rfq.Id, PurchaseRequestLineId = requestLine, LineNumber = 1,
            Description = "Retained", UnitCode = "each", Quantity = 1, Status = "Cancelled" };
        await using (var seed = new ProcurementDbContext(options, Current))
        { seed.AddScoped(po); seed.AddScoped(poLine); seed.AddScoped(rfq); seed.AddScoped(rfqLine); await seed.SaveChangesAsync(); }
        var foreignScope = Current with { LegalEntityId = Guid.NewGuid() };
        var foreign = new SourcingEvent { Number = "RF-FOREIGN", Title = "Foreign", Method = "RFQ", CurrencyCode = "ZMW", OwnerSubjectId = Current.SubjectId };
        await using (var seed = new ProcurementDbContext(options, foreignScope))
        {
            seed.AddScoped(foreign); seed.AddScoped(new SourcingLine { SourcingEventId = foreign.Id,
                PurchaseRequestLineId = requestLine, LineNumber = 1, Description = "Foreign", UnitCode = "each", Quantity = 1 });
            await seed.SaveChangesAsync();
        }
        var ledger = new[] {
            new PurchaseRequestAllocation { TargetType = "PurchaseOrder", TargetId = po.Id, Status = "Released" },
            new PurchaseRequestAllocation { TargetType = "SourcingEvent", TargetId = rfq.Id, Status = "Reserved" },
            new PurchaseRequestAllocation { TargetType = "SourcingEvent", TargetId = foreign.Id, Status = "Reserved" },
        };
        await using var db = new ProcurementDbContext(options, Current);
        var owners = await ApprovedDemandSourceLoader.LoadTargetOwnersAsync(db, requestLine, ledger);
        Assert.Equal(2, owners.Count);
        Assert.Contains(owners, x => x.TargetType == "PurchaseOrder" && x.TargetId == po.Id && x.TargetLineId == poLine.Id);
        Assert.Contains(owners, x => x.TargetType == "SourcingEvent" && x.TargetId == rfq.Id && x.TargetLineId == rfqLine.Id);
        Assert.DoesNotContain(owners, x => x.TargetId == foreign.Id);
        Assert.Empty(db.ChangeTracker.Entries()); // Runtime owner projection remains read-only and never manufactures rows.
    }
}
