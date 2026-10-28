using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceCurrentOwnerScopeTests
{
    private const string Tenant = "m6-owner-scope";
    private const string Currency = "ZMW";
    [Fact]
    public void Clean_target_draft_and_unrelated_supplier_credit_do_not_invent_exposure()
    {
        var f = Fixture();
        var unrelatedCredit = Row(f.Company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = f.Company,
            supplier_invoice_id = (Guid?)null, supplier_id = Guid.NewGuid(), currency_code = Currency,
            credit_number = "UNRELATED-1"
        });
        PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
            Capture(f.Company, f.Invoice, ("supplier_credit_note_refs", unrelatedCredit)),
            f.Order, f.Supplier, f.Branch, Currency, new HashSet<Guid>());
    }

    [Fact]
    public void Related_credit_payment_and_wrong_scope_block_only_the_target()
    {
        var f = Fixture();
        var relatedCredit = Row(f.Company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = f.Company,
            supplier_invoice_id = (Guid?)null, supplier_id = f.Supplier, currency_code = Currency,
            credit_number = "TARGET-1"
        });
        var payment = Row(f.Company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = f.Company,
            supplier_invoice_id = f.Invoice.Id
        });
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
            Capture(f.Company, f.Invoice, ("supplier_credit_note_refs", relatedCredit)),
            f.Order, f.Supplier, f.Branch, Currency, new HashSet<Guid>()));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
            Capture(f.Company, f.Invoice, ("manual_payment_requests", payment)),
            f.Order, f.Supplier, f.Branch, Currency, new HashSet<Guid>()));
        var malformedCredit = Row(f.Company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = f.Company,
            supplier_invoice_id = (string?)null, supplier_id = f.Supplier, currency_code = "zmw",
            credit_number = "TARGET-2"
        });
        Assert.Equal("PrincipalInvoiceCreditScopeUnclassifiable", Assert.Throws<InvalidOperationException>(() =>
            PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
                Capture(f.Company, f.Invoice, ("supplier_credit_note_refs", malformedCredit)),
                f.Order, f.Supplier, f.Branch, Currency, new HashSet<Guid>())).Message);
        var invalidLink = Row(f.Company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = f.Company,
            supplier_invoice_id = "not-a-guid", supplier_id = Guid.NewGuid(), currency_code = Currency,
            credit_number = "OTHER-1"
        });
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
            Capture(f.Company, f.Invoice, ("supplier_credit_note_refs", invalidLink)),
            f.Order, f.Supplier, f.Branch, Currency, new HashSet<Guid>()));
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(
            Capture(f.Company, f.Invoice), f.Order, f.Supplier, Guid.NewGuid(), Currency, new HashSet<Guid>()));
    }

    private static (Guid Company, Guid Order, Guid Supplier, Guid Branch,
        InvoiceProcurementCapturedRow Invoice) Fixture()
    {
        var company = Guid.NewGuid(); var order = Guid.NewGuid();
        var supplier = Guid.NewGuid(); var branch = Guid.NewGuid();
        var invoice = Row(company, new
        {
            id = Guid.NewGuid(), version = 1, tenant_id = Tenant, legal_entity_id = company,
            purchase_order_id = order, supplier_id = supplier, branch_ref = branch,
            currency_code = Currency, status = "Received", finance_posting_ref = (string?)null,
            credit_for_invoice_ref = (string?)null
        });
        return (company, order, supplier, branch, invoice);
    }
    private static InvoiceProcurementCapturedRow Row(Guid company, object value)
    {
        var json = JsonSerializer.Serialize(value);
        using var parsed = JsonDocument.Parse(json);
        var id = parsed.RootElement.GetProperty("id").GetGuid();
        return new(id, 1, ProcurementEvidenceEncoding.PolicyJsonDigest(json), json);
    }
    private static InvoiceProcurementInventoryCapture Capture(Guid company,
        InvoiceProcurementCapturedRow invoice, params (string Table, InvoiceProcurementCapturedRow Row)[] more) =>
        new(Tenant, company, InvoiceProcurementOwnerTables.Names.Select(name =>
            new InvoiceProcurementCapturedTable(name,
                (name == "supplier_invoices" ? new[] { invoice } : Array.Empty<InvoiceProcurementCapturedRow>())
                .Concat(more.Where(x => x.Table == name).Select(x => x.Row)).ToArray())).ToArray());
}
