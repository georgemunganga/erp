using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class FoundationScopeTests
{
    private sealed class Scope(string tenant, Guid entity) : IProcurementScope
    {
        public string TenantId { get; } = tenant;
        public Guid LegalEntityId { get; } = entity;
        public string SubjectId => "test-actor";
        public string? CorrelationId => "test-correlation";
    }

    private static ProcurementDbContext Context(string database, string tenant, Guid entity) =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options,
            new Scope(tenant, entity));

    [Fact]
    public async Task New_supplier_is_scoped_and_audited_without_sensitive_values()
    {
        var database = Guid.NewGuid().ToString();
        var entity = Guid.NewGuid();
        await using var db = Context(database, "tenant-a", entity);
        var supplier = new Supplier
        {
            Number = "SUP-1", LegalName = "Example Ltd", DisplayName = "Example",
            CountryCode = "ZM", RegistrationNumber = "SECRET-REG", Status = "Proposed",
        };
        db.AddScoped(supplier);
        await db.SaveChangesAsync();

        Assert.Equal("tenant-a", supplier.TenantId);
        Assert.Equal(entity, supplier.LegalEntityId);
        Assert.Equal("test-actor", supplier.CreatedBy);
        var audit = await db.AuditEvents.SingleAsync();
        Assert.Equal(supplier.Id, audit.RecordId);
        Assert.Equal("create", audit.Action);
        Assert.DoesNotContain("SECRET-REG", audit.ChangedFieldsJson ?? "");
        await using var otherTenant = Context(database, "tenant-b", entity);
        Assert.Empty(await otherTenant.Suppliers.ToListAsync());
        await using var otherEntity = Context(database, "tenant-a", Guid.NewGuid());
        Assert.Empty(await otherEntity.Suppliers.ToListAsync());
    }

    [Fact]
    public async Task Cross_scope_update_and_physical_delete_are_rejected()
    {
        var entity = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), "tenant-a", entity);
        var item = new CatalogItem
        {
            Code = "ITEM-1", Name = "Demo item", Kind = "Goods",
            CategoryCode = "OFFICE", UnitCode = "each",
        };
        db.AddScoped(item);
        await db.SaveChangesAsync();

        item.TenantId = "tenant-b";
        item.Name = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.CatalogItems.Remove(item);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_events_cannot_be_changed()
    {
        await using var db = Context(Guid.NewGuid().ToString(), "tenant-a", Guid.NewGuid());
        db.AddScoped(new Supplier { Number = "SUP-1", LegalName = "Example", DisplayName = "Example", CountryCode = "ZM" });
        await db.SaveChangesAsync();
        var audit = await db.AuditEvents.SingleAsync();
        audit.Action = "rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Supplier_contacts_share_parent_scope_before_tracking()
    {
        var entity = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), "tenant-a", entity);
        var supplier = new Supplier { Number = "SUP-2", LegalName = "Example", DisplayName = "Example", CountryCode = "ZM" };
        supplier.Contacts.Add(new SupplierContact { Name = "A Person", Email = "person@example.test" });
        db.AddScoped(supplier);
        await db.SaveChangesAsync();
        var contact = await db.SupplierContacts.SingleAsync();
        Assert.Equal(supplier.Id, contact.SupplierId);
        Assert.Equal("tenant-a", contact.TenantId);
        Assert.Equal(entity, contact.LegalEntityId);
    }

    [Fact]
    public async Task New_supplier_children_are_scoped_and_decisions_cannot_be_rewritten()
    {
        var entity = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), "tenant-a", entity);
        var supplier = new Supplier { Number = "SUP-3", LegalName = "Example", DisplayName = "Example", CountryCode = "ZM" };
        supplier.Sites.Add(new SupplierSite { AddressLine1 = "Test Road", CountryCode = "ZM", IsPrimary = true });
        supplier.Categories.Add(new SupplierCategory { CategoryCode = "OFFICE", IsPrimary = true });
        supplier.Decisions.Add(new SupplierDecision
        {
            FromStatus = "Draft", ToStatus = "Proposed", ActorSubjectId = "test-actor",
            Reason = "Submitted for review", DecidedAt = DateTimeOffset.UtcNow,
        });
        db.AddScoped(supplier);
        await db.SaveChangesAsync();

        var site = await db.SupplierSites.SingleAsync();
        Assert.Equal(supplier.Id, site.SupplierId);
        Assert.Equal("tenant-a", site.TenantId);
        Assert.Equal(entity, site.LegalEntityId);
        Assert.Equal("OFFICE", (await db.SupplierCategories.SingleAsync()).CategoryCode);

        var decision = await db.SupplierDecisions.SingleAsync();
        decision.Reason = "Changed after approval";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
