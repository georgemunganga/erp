using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M6dCurrencyProviderModelTests
{
    private sealed record Scope : IProcurementScope
    {
        public string TenantId => "sqlite-model-test";
        public Guid LegalEntityId => Guid.Parse("11111111-1111-4111-8111-111111111111");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string SubjectId => "sqlite-model-actor";
        public string? CorrelationId => null;
    }

    [Fact]
    public void Sqlite_hrm_schema_creates_and_procurement_model_omits_only_pg_currency_checks()
    {
        using var hrm = TestDbContextFactory.Create();
        Assert.NotNull(hrm.Model.FindEntityType(typeof(Mightyfin.Erp.Hrm.Domain.Entities.CoreCurrencyPrecisionVersion)));
        Assert.NotNull(hrm.Model.FindEntityType(typeof(Mightyfin.Erp.Hrm.Domain.Entities.CoreCurrencyPrecisionCommandReceipt)));
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var procurement = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>().UseSqlite(connection).Options, new Scope());
        foreach (var (type, check) in new[] {
            (typeof(InvoiceAmountCalculation), "ck_invoice_amount_calculation"),
            (typeof(InvoiceIntakeCommandReceipt), "ck_invoice_intake_receipt_v2") })
        {
            var entity = procurement.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
            Assert.DoesNotContain(entity.GetCheckConstraints(), x => x.Name == check);
        }
    }
}
