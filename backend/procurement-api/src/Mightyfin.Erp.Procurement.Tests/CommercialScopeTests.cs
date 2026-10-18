using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CommercialScopeTests
{
    private sealed class Scope(string tenant, Guid entity) : IProcurementScope
    {
        public string TenantId => tenant;
        public Guid LegalEntityId => entity;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string SubjectId => "commercial-test";
        public string? CorrelationId => "commercial-test-correlation";
    }

    private static ProcurementDbContext Context(string database, string tenant, Guid entity) =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options,
            new Scope(tenant, entity));

    [Fact]
    public async Task Commercial_records_are_scoped_and_audited()
    {
        var database = Guid.NewGuid().ToString();
        var entity = Guid.NewGuid();
        await using (var db = Context(database, "tenant-a", entity))
        {
            var eventRecord = new SourcingEvent
            {
                Number = "RFQ-1", Title = "Workstations", Method = "RFQ",
                CurrencyCode = "ZMW", OwnerSubjectId = "buyer-1",
            };
            db.AddScoped(eventRecord);
            await db.SaveChangesAsync();
            Assert.Equal("tenant-a", eventRecord.TenantId);
            Assert.Equal(entity, eventRecord.LegalEntityId);
            Assert.Contains(await db.AuditEvents.ToListAsync(), audit =>
                audit.RecordId == eventRecord.Id && audit.Action == "create");
        }
        await using var otherTenant = Context(database, "tenant-b", entity);
        Assert.Empty(await otherTenant.SourcingEvents.ToListAsync());
        await using var otherEntity = Context(database, "tenant-a", Guid.NewGuid());
        Assert.Empty(await otherEntity.SourcingEvents.ToListAsync());
    }

    [Fact]
    public async Task Finance_payment_snapshot_and_bid_evaluation_are_append_only()
    {
        await using var db = Context(Guid.NewGuid().ToString(), "tenant-a", Guid.NewGuid());
        var payment = new FinancePaymentStatusRef
        {
            SupplierInvoiceId = Guid.NewGuid(), FinancePaymentRef = "FIN-PAY-1", Status = "Posted",
            CurrencyCode = "ZMW", PaidAmount = 100, SyncedAt = DateTimeOffset.UtcNow,
        };
        db.AddScoped(payment);
        await db.SaveChangesAsync();
        payment.Status = "Pending";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        var evaluation = new BidEvaluation
        {
            SupplierBidId = Guid.NewGuid(), EvaluatorSubjectId = "evaluator-1", Stage = "Technical",
            CriterionCode = "CAPACITY", Score = 80, Passed = true, EvaluatedAt = DateTimeOffset.UtcNow,
        };
        db.AddScoped(evaluation);
        await db.SaveChangesAsync();
        evaluation.Score = 100;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
