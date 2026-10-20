using Mightyfin.Erp.Procurement.Infrastructure.Documents;
namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementInvoiceDocumentStoreBindingTests
{
    [Theory]
    [InlineData("Host=/private/socket;Database=erp;Username=documents","Host=/private/socket;Database=erp;Username=finite",true)]
    [InlineData("Host=docs;Database=erp","Host=execution;Database=erp",false)]
    [InlineData("Host=db;Database=documents","Host=db;Database=erp",false)]
    [InlineData("Host=db;Port=5433;Database=erp","Host=db;Port=5432;Database=erp",false)]
    [InlineData("Host=db,other;Database=erp","Host=db,other;Database=erp",false)]
    [InlineData("Host=db","Host=db",false)]
    [InlineData("not a connection","Host=db;Database=erp",false)]
    public void Documents_need_an_unambiguous_same_physical_store_without_requiring_shared_credentials(string configured,string executing,bool expected)
    {
        Assert.Equal(expected,InvoiceDocumentStoreBinding.SamePhysicalDatabase(configured,executing));
    }
}
