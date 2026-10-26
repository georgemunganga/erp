using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreOutboxPublisherTenantTests
{
    private static OutboxMessage Message(string name) => new()
    {
        PublicId = $"evt_{name}", EventType = "hrm.private-test", SubjectId = $"subject-{name}",
        // A tenant-like payload field is deliberately untrusted. Scope comes
        // from the persisted outbox row, never event content.
        CorrelationId = $"correlation-{name}", PayloadJson = "{\"tenantId\":\"forged-payload-tenant\"}",
        Status = "publishing",
        AvailableAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task TwoDatabaseRowTenants_AcknowledgeWithMatchingAuditTenant_WithoutOpeningGenericWrites()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var seedOptions = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        Guid firstId;
        Guid secondId;
        Guid foreignWorkerId;
        await using (var first = new HrmDbContext(seedOptions, new FixedTenantAccessor("tenant-a")))
        {
            await first.Database.EnsureCreatedAsync();
            var row = Message("tenant-a");
            first.OutboxMessages.Add(row);
            await first.SaveChangesAsync();
            firstId = row.Id;
        }
        await using (var second = new HrmDbContext(seedOptions, new FixedTenantAccessor("tenant-b")))
        {
            var row = Message("tenant-b");
            var worker = new Worker
            {
                EmployeeNo = "PUB-B", FirstName = "Foreign", LastName = "Worker",
                WorkerType = "employee", Status = "active",
            };
            second.OutboxMessages.Add(row);
            second.Workers.Add(worker);
            await second.SaveChangesAsync();
            secondId = row.Id;
            foreignWorkerId = worker.Id;
        }

        var publisherTenant = new OutboxPublisherTenantAccessor();
        var publisherOptions = new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditInterceptor(new HttpContextAccessor(), publisherTenant))
            .Options;
        await using (var publisherDb = new HrmDbContext(publisherOptions, publisherTenant))
        {
            var store = new EfOutboxPublisherStore(publisherDb, publisherTenant);
            await store.CompleteAsync(firstId, true, "nats", null, default);

            var afterFirst = await publisherDb.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                .ToDictionaryAsync(x => x.Id);
            Assert.Equal("published", afterFirst[firstId].Status);
            Assert.Equal("publishing", afterFirst[secondId].Status);
            Assert.Equal("tenant-a", afterFirst[firstId].TenantId);
            Assert.Equal("tenant-b", afterFirst[secondId].TenantId);

            await store.CompleteAsync(secondId, false, "nats", "synthetic failure", default);
            var audits = await publisherDb.AuditEntries.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.EntityType == nameof(OutboxMessage)).ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.Contains(audits, x => x.EntityId == firstId.ToString() && x.TenantId == "tenant-a");
            Assert.Contains(audits, x => x.EntityId == secondId.ToString() && x.TenantId == "tenant-b");
        }

        // A publisher row binding is local to that row's save. It cannot turn
        // unrelated tracked foreign records into permissible cross-tenant writes.
        await using (var deniedDb = new HrmDbContext(publisherOptions, publisherTenant))
        {
            var foreign = await deniedDb.Workers.IgnoreQueryFilters()
                .SingleAsync(x => x.Id == foreignWorkerId);
            foreign.FirstName = "Tampered";
            var deniedStore = new EfOutboxPublisherStore(deniedDb, publisherTenant);
            var denied = await Assert.ThrowsAsync<DomainException>(() =>
                deniedStore.CompleteAsync(firstId, true, "nats", null, default));
            Assert.Equal("cross-tenant-write", denied.Code);
        }
        await using (var check = new HrmDbContext(seedOptions, new FixedTenantAccessor("tenant-a")))
        {
            Assert.Equal(1, (await check.OutboxMessages.SingleAsync(x => x.Id == firstId)).PublishAttempts);
        }
    }

    [Fact]
    public async Task UnboundPublisherTenantCannotSave_AndOrdinaryAccessorCannotUseGlobalPublisherStore()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        var publisherTenant = new OutboxPublisherTenantAccessor();
        await using (var publisherDb = new HrmDbContext(options, publisherTenant))
        {
            await publisherDb.Database.EnsureCreatedAsync();
            publisherDb.OutboxMessages.Add(Message("unbound"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => publisherDb.SaveChangesAsync());
        }
        await using (var ordinaryDb = new HrmDbContext(options, new FixedTenantAccessor("tenant-a")))
        {
            var store = new EfOutboxPublisherStore(ordinaryDb, new FixedTenantAccessor("tenant-a"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CompleteAsync(Guid.NewGuid(), true, "nats", null, default));
            Assert.Empty(await ordinaryDb.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        }
    }
}
