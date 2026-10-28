using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementStoredBaselineEvidenceTests
{
    [Fact]
    public void Stored_zero_is_a_recomputed_captured_fact_not_an_empty_table_assumption()
    {
        var capture = Capture();
        var baseline = Baseline(capture);
        var verified = InvoiceProcurementStoredBaselineEvidence.VerifyStored(baseline, []);
        Assert.Equal(21, verified.Tables.Count);
        Assert.Equal(0, verified.RowCount);
        Assert.Equal(baseline.InventorySetSha256, verified.CompleteSetSha256);
        var partition = InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(baseline, [], capture);
        Assert.Empty(partition.ChangedMutableDraftRows);
        Assert.Empty(partition.AddedRows);
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.VerifyStored(
                Baseline(capture, new string('0',64)), []));
    }

    [Fact]
    public void Later_draft_pointer_transition_keeps_financial_identity_but_value_tamper_is_denied()
    {
        var id = Guid.NewGuid();
        var company = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var source = InvoiceJson(id, company, supplier, 1, "Received", null, 10m);
        var capture = Capture(new InvoiceProcurementCapturedRow(id, 1,
            ProcurementEvidenceEncoding.PolicyJsonDigest(source), source), company);
        var baseline = Baseline(capture);
        var stored = new InvoiceProcurementOwnerBaselineRow
        {
            TenantId = baseline.TenantId, LegalEntityId = baseline.LegalEntityId,
            BaselineId = baseline.Id, TableName = "supplier_invoices", SourceRowId = id,
            SourceRowVersion = 1, OriginalRowSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(source),
            ImmutableFieldsSha256 = InvoiceProcurementImmutableFields.Digest("supplier_invoices", source),
            InitialClassification = "MutableDraft", SupplierInvoiceId = id,
            CreatedAt = baseline.CapturedAtUtc, CreatedBy = baseline.MakerPrincipalId
        };
        Assert.Equal(baseline.InventorySetSha256,
            InvoiceProcurementStoredBaselineEvidence.VerifyStored(baseline, [stored]).CompleteSetSha256);
        var changed = InvoiceJson(id, company, supplier, 2, "MappingUnverified", Guid.NewGuid(), 10m);
        var current = Capture(new InvoiceProcurementCapturedRow(id, 2,
            ProcurementEvidenceEncoding.PolicyJsonDigest(changed), changed), baseline.LegalEntityId);
        var partition = InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(baseline, [stored], current);
        Assert.Equal(id, Assert.Single(partition.ChangedMutableDraftRows).RowId);
        Assert.Empty(partition.AddedRows);
        var addedId = Guid.NewGuid();
        var addedJson = InvoiceJson(addedId, company, supplier, 1, "Received", null, 1m);
        var addedRow = new InvoiceProcurementCapturedRow(addedId, 1,
            ProcurementEvidenceEncoding.PolicyJsonDigest(addedJson), addedJson);
        var withNewInvoice = current with
        {
            Tables = current.Tables.Select(table => table.TableName == "supplier_invoices"
                ? table with { Rows = [table.Rows.Single(), addedRow] } : table).ToArray()
        };
        var currentPartition = InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(
            baseline, [stored], withNewInvoice);
        Assert.Equal(addedId, Assert.Single(currentPartition.AddedRows).RowId);
        var tampered = InvoiceJson(id, company, supplier, 2, "MappingUnverified", Guid.NewGuid(), 11m);
        var changedValue = Capture(new InvoiceProcurementCapturedRow(id, 2,
            ProcurementEvidenceEncoding.PolicyJsonDigest(tampered), tampered), baseline.LegalEntityId);
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(baseline, [stored], changedValue));
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(baseline, [stored], Capture(null, baseline.LegalEntityId)));
        var blocked = new InvoiceProcurementOwnerBaselineRow
        {
            TenantId = stored.TenantId, LegalEntityId = stored.LegalEntityId,
            BaselineId = stored.BaselineId, TableName = stored.TableName,
            SourceRowId = stored.SourceRowId, SourceRowVersion = stored.SourceRowVersion,
            OriginalRowSha256 = stored.OriginalRowSha256,
            ImmutableFieldsSha256 = stored.ImmutableFieldsSha256,
            InitialClassification = "BlockedAmbiguity", SupplierInvoiceId = id,
            CreatedAt = baseline.CapturedAtUtc, CreatedBy = baseline.MakerPrincipalId
        };
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.ReconcileCurrent(baseline, [blocked], current));
    }

    [Fact]
    public void Unlinked_retained_credit_blocks_only_its_supplier_and_currency()
    {
        var company = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var creditId = Guid.NewGuid();
        var credit = CreditJson(creditId, company, supplier, "ZMW", 1);
        var captured = new InvoiceProcurementCapturedRow(creditId, 1,
            ProcurementEvidenceEncoding.PolicyJsonDigest(credit), credit);
        var opening = new InvoiceProcurementInventoryCapture("tenant-a", company,
            InvoiceProcurementOwnerTables.Names.Select(name => new InvoiceProcurementCapturedTable(
                name, name == "supplier_credit_note_refs" ? [captured] : [])).ToArray());
        var baseline = Baseline(opening);
        var row = new InvoiceProcurementOwnerBaselineRow
        {
            TenantId = baseline.TenantId, LegalEntityId = baseline.LegalEntityId,
            BaselineId = baseline.Id, TableName = "supplier_credit_note_refs",
            SourceRowId = creditId, SourceRowVersion = 1,
            OriginalRowSha256 = captured.CanonicalRowSha256,
            ImmutableFieldsSha256 = InvoiceProcurementImmutableFields.Digest(
                "supplier_credit_note_refs", credit),
            InitialClassification = "BlockedAmbiguity",
            CreatedAt = baseline.CapturedAtUtc, CreatedBy = baseline.MakerPrincipalId
        };
        var order = Guid.NewGuid();
        Assert.False(InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
            baseline, [row], opening, order, supplier, "ZMW"));
        Assert.True(InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
            baseline, [row], opening, order, Guid.NewGuid(), "ZMW"));
        Assert.True(InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
            baseline, [row], opening, order, supplier, "USD"));
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
                baseline, [row], opening, order, supplier, "zmw"));

        var changed = CreditJson(creditId, company, supplier, "ZMW", 2);
        var changedRow = new InvoiceProcurementCapturedRow(creditId, 2,
            ProcurementEvidenceEncoding.PolicyJsonDigest(changed), changed);
        var current = opening with
        {
            Tables = opening.Tables.Select(table => table.TableName == "supplier_credit_note_refs"
                ? table with { Rows = [changedRow] } : table).ToArray()
        };
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
                baseline, [row], current, order, Guid.NewGuid(), "ZMW"));
    }

    private static InvoiceProcurementInventoryCapture Capture(InvoiceProcurementCapturedRow? invoice = null,
        Guid? company = null)
    {
        var selectedCompany = company ?? Guid.NewGuid();
        return new("tenant-a", selectedCompany,
            InvoiceProcurementOwnerTables.Names.Select(name => new InvoiceProcurementCapturedTable(name,
                name == "supplier_invoices" && invoice is not null ? [invoice] : [])).ToArray());
    }

    private static InvoiceProcurementOwnerBaseline Baseline(InvoiceProcurementInventoryCapture capture,
        string? inventorySetOverride = null)
    {
        var summary = InvoiceProcurementInventoryEvidence.Summarize(capture);
        var maker = Guid.NewGuid().ToString("D");
        var at = DateTimeOffset.UtcNow;
        return new InvoiceProcurementOwnerBaseline
        {
            Id = Guid.NewGuid(), TenantId = capture.TenantId, LegalEntityId = capture.LegalEntityId,
            CoreCaseId = Guid.NewGuid(), MakerPrincipalId = maker,
            CapturedAtUtc = at, CapturedThroughMigration = "M6", CreatedAt = at, CreatedBy = maker,
            CatalogManifestJson = InvoiceProcurementBaselineEvidence.CatalogManifestJson(),
            CatalogManifestSha256 = summary.ManifestSha256,
            InventorySetSha256 = inventorySetOverride ?? summary.CompleteSetSha256,
            InventoryRowCount = summary.RowCount, InitialActiveClaimsSha256 = new string('a',64),
            InitialExposureSetSha256 = new string('b',64)
        };
    }

    private static string InvoiceJson(Guid id, Guid company, Guid supplier, long version,
        string status, Guid? mapping, decimal net)
    {
        return JsonSerializer.Serialize(new
        {
            id, version, tenant_id = "tenant-a", legal_entity_id = company,
            supplier_id = supplier, invoice_number = "INV-1",
            normalized_invoice_number = "INV1", currency_code = "ZMW",
            net_amount = net, tax_amount = 0m, gross_amount = net,
            invoice_date = "2026-10-28", status, current_mapping_version_id = mapping
        });
    }

    private static string CreditJson(Guid id, Guid company, Guid supplier,
        string currency, long version) => JsonSerializer.Serialize(new
    {
        id, version, tenant_id = "tenant-a", legal_entity_id = company,
        supplier_invoice_id = (Guid?)null, supplier_id = supplier,
        currency_code = currency
    });
}
