using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementInvoiceReplacementStagingTests
{
    private const string Tenant = "replacement-staging";
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-30T10:00:00Z");

    private static ProcurementRequestScope Scope()
    {
        var scope = new ProcurementRequestScope();
        scope.Set(Tenant, Company, Maker, Branch, null, false, "replacement-staging");
        return scope;
    }

    private static (InvoiceReplacementSourceWitness Witness, SupplierInvoice Successor,
        InvoiceAmountCalculation Calculation, InvoiceIntakeCommandReceipt Capture, Guid Key) Input(
        string tenant = Tenant, Guid? company = null)
    {
        var source = new SupplierInvoice {
            TenantId = tenant, LegalEntityId = company ?? Company, CreatedBy = Maker,
            BranchRef = Branch, SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", Version = 4
        };
        var mapping = new InvoiceMatchMappingVersion {
            SupplierInvoiceId = source.Id, PurchaseOrderId = Guid.NewGuid(), IssuedVersionId = Guid.NewGuid(),
            MappingRevision = 1, InvoiceOriginalSha256 = new string('a', 64)
        };
        var successor = new SupplierInvoice { InvoiceNumber = "SUPPLIER-CORRECTED-1" };
        var calculation = new InvoiceAmountCalculation { SupplierInvoiceId = successor.Id };
        var key = Guid.NewGuid();
        var capture = new InvoiceIntakeCommandReceipt {
            SupplierInvoiceId = successor.Id, CalculationId = calculation.Id,
            CommandKey = InvoiceReplacementEvidence.CaptureCommandKey(tenant, company ?? Company, source.Id, key)
        };
        var witness = new InvoiceReplacementSourceWitness(source, mapping, new InvoiceMatchResult(),
            new InvoiceUnitReviewSource(), new InvoiceUnitReviewOutcome(), new string('b', 64));
        return (witness, successor, calculation, capture, key);
    }

    [Fact]
    public void Actual_replacement_factory_stages_link_and_derived_receipt_in_the_owning_context()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Scope());
        var input = Input();
        var link = ProcurementInvoiceIntakeRoutes.CreateReplacementLink(input.Witness, input.Successor,
            input.Calculation, input.Capture, input.Key, "Supplier corrected the invoice", At);
        db.AddScoped(link); // Regression: the route's unscoped initializer threw here before SaveChanges.
        var receipt = InvoiceReplacementEvidence.CreateReceipt(link, input.Successor, new string('c', 64), At);
        db.AddScoped(receipt);
        Assert.Equal(EntityState.Added, db.Entry(link).State);
        Assert.Equal(EntityState.Added, db.Entry(receipt).State);
        Assert.Equal(Tenant, link.TenantId); Assert.Equal(Company, link.LegalEntityId);
        Assert.Equal(link.TenantId, receipt.TenantId); Assert.Equal(link.LegalEntityId, receipt.LegalEntityId);
        Assert.Equal(input.Witness.Invoice.Id, link.SourceInvoiceId);
        Assert.Equal(input.Witness.Invoice.Version, link.SourceInvoiceVersion);
        Assert.Equal(input.Witness.Mapping.PurchaseOrderId, link.PurchaseOrderId);
        Assert.Equal(input.Capture.CommandKey, link.CaptureCommandKey);
        Assert.Equal(Maker, link.MakerPrincipalId); Assert.Equal(Branch, link.BranchId);
        Assert.Equal(link.Id, receipt.LinkId); Assert.Equal(input.Key, receipt.CommandKey);
        Assert.Equal(4, input.Witness.Invoice.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Foreign_verified_source_scope_is_retained_and_rejected_instead_of_relabelled(bool foreignTenant)
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Scope());
        var input = Input(foreignTenant ? "another-tenant" : Tenant, foreignTenant ? Company : Guid.NewGuid());
        var link = ProcurementInvoiceIntakeRoutes.CreateReplacementLink(input.Witness, input.Successor,
            input.Calculation, input.Capture, input.Key, "Supplier corrected the invoice", At);
        Assert.Equal(input.Witness.Invoice.TenantId, link.TenantId);
        Assert.Equal(input.Witness.Invoice.LegalEntityId, link.LegalEntityId);
        Assert.Equal("Pinned approval evidence is outside the authenticated scope.",
            Assert.Throws<InvalidOperationException>(() => db.AddScoped(link)).Message);
        var receipt = InvoiceReplacementEvidence.CreateReceipt(link, input.Successor, new string('c', 64), At);
        Assert.Equal("Pinned approval evidence is outside the authenticated scope.",
            Assert.Throws<InvalidOperationException>(() => db.AddScoped(receipt)).Message);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
