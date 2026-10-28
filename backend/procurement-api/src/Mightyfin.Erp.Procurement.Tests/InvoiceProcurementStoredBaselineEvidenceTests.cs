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

    [Fact]
    public void Safe_old_payment_draft_can_transition_only_with_unchanged_financial_identity()
    {
        var company = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var invoice = Captured(invoiceId, 1,
            InvoiceJson(invoiceId, company, supplier, 1, "Received", null, 10m));
        var payment = Captured(paymentId, 1,
            PaymentJson(paymentId, invoiceId, company, branch, supplier, 1, "Draft", 10m));
        var opening = new InvoiceProcurementInventoryCapture("tenant-a", company,
            InvoiceProcurementOwnerTables.Names.Select(name => new InvoiceProcurementCapturedTable(
                name, name == "supplier_invoices" ? [invoice]
                    : name == "manual_payment_requests" ? [payment] : [])).ToArray());
        var classes = InvoiceProcurementHistoricalClassification.Classify(opening)
            .ToDictionary(x => (x.TableName, x.RowId));
        Assert.Equal("MutableDraft", classes[("manual_payment_requests", paymentId)].Classification);
        Assert.Equal("BlockedAmbiguity", classes[("supplier_invoices", invoiceId)].Classification);
        var baseline = Baseline(opening);
        var rows = opening.Tables.SelectMany(table => table.Rows.Select(source =>
            new InvoiceProcurementOwnerBaselineRow
            {
                TenantId = baseline.TenantId, LegalEntityId = baseline.LegalEntityId,
                BaselineId = baseline.Id, TableName = table.TableName,
                SourceRowId = source.Id, SourceRowVersion = source.Version,
                OriginalRowSha256 = source.CanonicalRowSha256,
                ImmutableFieldsSha256 = InvoiceProcurementImmutableFields.Digest(
                    table.TableName, source.RowJson),
                InitialClassification = classes[(table.TableName, source.Id)].Classification,
                SupplierInvoiceId = classes[(table.TableName, source.Id)].SupplierInvoiceId,
                PurchaseOrderId = classes[(table.TableName, source.Id)].PurchaseOrderId,
                CreatedAt = baseline.CapturedAtUtc, CreatedBy = baseline.MakerPrincipalId
            })).ToArray();
        var transitioned = Captured(paymentId, 2,
            PaymentJson(paymentId, invoiceId, company, branch, supplier, 2, "PendingApproval", 10m));
        var current = opening with { Tables = opening.Tables.Select(table =>
            table.TableName == "manual_payment_requests"
                ? table with { Rows = [transitioned] } : table).ToArray() };
        Assert.Equal(paymentId, Assert.Single(InvoiceProcurementStoredBaselineEvidence
            .ReconcileCurrent(baseline, rows, current).ChangedMutableDraftRows).RowId);
        var tampered = Captured(paymentId, 2,
            PaymentJson(paymentId, invoiceId, company, branch, supplier, 2, "PendingApproval", 11m));
        var changedValue = opening with { Tables = opening.Tables.Select(table =>
            table.TableName == "manual_payment_requests"
                ? table with { Rows = [tampered] } : table).ToArray() };
        Assert.Throws<InvalidOperationException>(() => InvoiceProcurementStoredBaselineEvidence
            .ReconcileCurrent(baseline, rows, changedValue));
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
            invoice_date = "2026-10-28", status, intake_channel = "Manual",
            finance_posting_ref = (string?)null,
            evidence_document_ref_id = (Guid?)null,
            credit_for_invoice_ref = (Guid?)null,
            mapping_revision = mapping is null ? 0 : 1,
            match_revision = 0, matching_state = mapping is null ? "NotCompared" : "MappingUnverified",
            current_mapping_version_id = mapping,
            last_match_result_id = (Guid?)null,
            active_match_claim_result_id = (Guid?)null
        });
    }

    private static string CreditJson(Guid id, Guid company, Guid supplier,
        string currency, long version) => JsonSerializer.Serialize(new
    {
        id, version, tenant_id = "tenant-a", legal_entity_id = company,
        supplier_invoice_id = (Guid?)null, supplier_id = supplier,
        currency_code = currency
    });

    private static InvoiceProcurementCapturedRow Captured(Guid id, long version, string json) =>
        new(id, version, ProcurementEvidenceEncoding.PolicyJsonDigest(json), json);

    private static string PaymentJson(Guid id, Guid invoiceId, Guid company,
        Guid branch, Guid supplier, long version, string status, decimal amount) =>
        JsonSerializer.Serialize(new
        {
            id, version, tenant_id = "tenant-a", legal_entity_id = company,
            branch_id = branch, supplier_invoice_id = invoiceId,
            supplier_id = supplier, currency_code = "ZMW", amount, status,
            approved_amount = (decimal?)null, approved_by_principal_id = (string?)null,
            approved_by_core_linked_worker_id = (Guid?)null,
            approved_at = (DateTimeOffset?)null, paid_on = (DateOnly?)null,
            external_method = (string?)null, external_reference = (string?)null,
            recorded_amount = (decimal?)null, recorded_by_principal_id = (string?)null,
            recorded_by_core_linked_worker_id = (Guid?)null,
            recorded_at = (DateTimeOffset?)null, confirmed_by_principal_id = (string?)null,
            confirmed_by_core_linked_worker_id = (Guid?)null,
            confirmed_at = (DateTimeOffset?)null,
            evidence_document_ref_id = (Guid?)null, closure_reason = (string?)null
        });
}
