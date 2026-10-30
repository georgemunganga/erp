using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceAmountInventoryEvidenceTests
{
    private sealed class Graph
    {
        public SupplierInvoice Invoice { get; }
        public SupplierInvoiceLine[] Lines { get; }
        public InvoiceAmountCalculation Calculation { get; }
        public AuditEvent Audit { get; }
        public OutboxEvent Message { get; }
        public InvoiceIntakeCommandReceipt Receipt { get; }
        public InvoiceProcurementInventoryCapture Capture { get; }
        public Graph()
        {
            (Invoice, Lines, Calculation) = InvoiceAmountCalculationEvidenceTests.Fixture();
            Invoice.CreatedAt = Calculation.CreatedAt;
            Receipt = InvoiceIntakeCommandReceiptTests.ValidReceipt(Invoice, Lines, Calculation);
            var marker = InvoiceAmountCalculationEvidence.MarkerJson(Calculation);
            Audit = new() { TenantId = Invoice.TenantId, LegalEntityId = Invoice.LegalEntityId,
                RecordType = nameof(InvoiceAmountCalculation), RecordId = Calculation.Id,
                Action = InvoiceAmountCalculationEvidence.AuditAction, ActorSubjectId = Calculation.CreatedBy,
                CreatedAt = Calculation.CreatedAt, CreatedBy = Calculation.CreatedBy, ChangedFieldsJson = marker };
            Message = new() { TenantId = Invoice.TenantId, LegalEntityId = Invoice.LegalEntityId,
                EventType = InvoiceAmountCalculationEvidence.EventType, EventVersion = 2,
                AggregateType = nameof(SupplierInvoice), AggregateId = Invoice.Id,
                CreatedAt = Calculation.CreatedAt, CreatedBy = Calculation.CreatedBy, PayloadJson = marker,
                IdempotencyKey = "invoice-amount-calculation:" + Calculation.Id.ToString("D") };
            Capture = new(Invoice.TenantId, Invoice.LegalEntityId,
                InvoiceProcurementOwnerTables.Names.Select(name => new InvoiceProcurementCapturedTable(name,
                    name == "supplier_invoices" ? [new(Invoice.Id,1,new string('a',64),"{}")] :
                    name == "supplier_invoice_lines" ? [new(Lines[0].Id,1,new string('b',64),"{}")] : []))
                    .ToArray());
        }
        public InvoiceAmountInventorySupplement Verify() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(
            Capture, [Calculation], [Audit], [Message], [Invoice], Lines, [Receipt]);
    }

    [Fact]
    public async Task Legacy_empty_supplement_needs_no_Core_callback_and_does_not_change_frozen_capture()
    {
        var graph = new Graph(); var names = InvoiceProcurementOwnerTables.Names.ToArray();
        var result = InvoiceAmountInventoryEvidence.VerifyStoredGraph(graph.Capture, [], [], [], [], [], []);
        Assert.Equal(0, result.CalculationCount); Assert.True(result.CoreReferencesVerified);
        Assert.Equal(names, InvoiceProcurementOwnerTables.Names);
        Assert.Equal(21, graph.Capture.Tables.Count);
        Assert.Same(result, await InvoiceAmountInventoryEvidence.VerifyCoreHistoryAsync(result, null, default));
    }

    [Fact]
    public async Task Complete_graph_is_not_authority_until_exact_Core_history_callback_accepts()
    {
        var graph = new Graph(); var proof = graph.Verify();
        Assert.False(proof.CoreReferencesVerified);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvoiceAmountInventoryEvidence.VerifyCoreHistoryAsync(proof, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvoiceAmountInventoryEvidence.VerifyCoreHistoryAsync(proof, (_,_) => Task.FromResult(false), default));
        var calls = 0;
        var accepted = await InvoiceAmountInventoryEvidence.VerifyCoreHistoryAsync(proof, (requirement, _) =>
        {
            calls++;
            Assert.Equal(graph.Calculation.CoreCurrencyReferenceId, requirement.ReferenceId);
            Assert.Equal(graph.Calculation.CoreSourcePayloadSha256, requirement.SourcePayloadSha256);
            Assert.Equal(graph.Calculation.ObservedAtUtc, requirement.ObservedAtUtc);
            Assert.Equal(graph.Calculation.CorePublishedAtUtc, requirement.PublishedAtUtc);
            Assert.Equal(graph.Calculation.SupplierInvoiceId, requirement.InvoiceId);
            return Task.FromResult(true);
        }, default);
        Assert.Equal(1, calls); Assert.True(accepted.CoreReferencesVerified);
    }

    [Theory]
    [InlineData("child")][InlineData("audit")][InlineData("outbox")]
    public void Missing_child_or_either_marker_is_denied(string missing)
    {
        var g = new Graph();
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(g.Capture,
            missing == "child" ? [] : [g.Calculation], missing == "audit" ? [] : [g.Audit],
            missing == "outbox" ? [] : [g.Message], [g.Invoice], g.Lines, [g.Receipt]));
    }

    [Theory]
    [InlineData("audit-orphan")][InlineData("outbox-orphan")][InlineData("duplicate-child")]
    [InlineData("wrong-actor")][InlineData("wrong-scope")][InlineData("wrong-key")]
    [InlineData("weakened-json")][InlineData("line-set")][InlineData("inactive-line")]
    [InlineData("header")][InlineData("wrong-marker-type")]
    public void Orphans_duplicates_cross_scope_and_tampering_fail(string mutation)
    {
        var g = new Graph();
        switch (mutation)
        {
            case "audit-orphan": g.Audit.RecordId = Guid.NewGuid(); break;
            case "outbox-orphan": g.Message.AggregateId = Guid.NewGuid(); break;
            case "duplicate-child":
                Assert.Throws<InvalidOperationException>(() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(g.Capture,
                    [g.Calculation,g.Calculation], [g.Audit], [g.Message], [g.Invoice], g.Lines, [g.Receipt])); return;
            case "wrong-actor": g.Audit.ActorSubjectId = Guid.NewGuid().ToString(); break;
            case "wrong-scope": g.Message.LegalEntityId = Guid.NewGuid(); break;
            case "wrong-key": g.Message.IdempotencyKey = "another-key"; break;
            case "weakened-json": g.Message.PayloadJson = "{}"; break;
            case "line-set":
                Assert.Throws<InvalidOperationException>(() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(g.Capture,
                    [g.Calculation], [g.Audit], [g.Message], [g.Invoice], [], [g.Receipt])); return;
            case "inactive-line": g.Lines[0].IsActive = false; break;
            case "header": g.Invoice.GrossAmount += .01m; break;
            case "wrong-marker-type": g.Audit.RecordType = "SupplierInvoice"; break;
        }
        Assert.Throws<InvalidOperationException>(() => g.Verify());
    }

    [Fact]
    public void Delivered_outbox_retains_same_semantic_supplement_and_authority_requirement()
    {
        var g = new Graph(); var before = g.Verify();
        g.Message.State = "Published"; g.Message.Attempts = 2; g.Message.Version = 3;
        g.Message.UpdatedAt = InvoiceAmountCalculationEvidenceTests.At.AddMinutes(1);
        var after = g.Verify();
        Assert.Equal(before.SetSha256, after.SetSha256);
        Assert.False(after.CoreReferencesVerified);
    }
    [Fact]
    public void Calculation_without_fourth_table_receipt_cannot_be_accepted()
    {
        var g = new Graph();
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(g.Capture,
            [g.Calculation], [g.Audit], [g.Message], [g.Invoice], g.Lines, []));
    }

    [Fact]
    public void Orphan_receipt_without_calculation_cannot_disappear_into_legacy_empty_path()
    {
        var g = new Graph();
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountInventoryEvidence.VerifyStoredGraph(g.Capture,
            [], [], [], [], [], [g.Receipt]));
    }

    [Theory]
    [InlineData("scope")][InlineData("mutable")]
    public void Receipt_cross_scope_or_mutable_history_is_denied(string change)
    {
        var g = new Graph();
        if (change == "scope") g.Receipt.TenantId = "foreign-tenant";
        else g.Receipt.UpdatedAt = InvoiceAmountCalculationEvidenceTests.At;
        Assert.Throws<InvalidOperationException>(() => g.Verify());
    }

    [Fact]
    public void Jsonb_reordered_and_spaced_markers_preserve_the_complete_typed_inventory()
    {
        var g = new Graph();
        var original = g.Verify();
        var marker = InvoiceAmountCalculationEvidence.MarkerJson(g.Calculation);
        using var parsed = JsonDocument.Parse(marker);
        var fields = parsed.RootElement.EnumerateObject().ToArray();
        g.Audit.ChangedFieldsJson = "{ " + string.Join(" , ", fields.Reverse()
            .Select(x => JsonSerializer.Serialize(x.Name) + " : " + x.Value.GetRawText())) + " }";
        g.Message.PayloadJson = "{ " + string.Join(" , ", fields
            .Select(x => JsonSerializer.Serialize(x.Name) + " : " + x.Value.GetRawText())) + " }";
        var normalized = g.Verify();
        Assert.Equal(original.SetSha256, normalized.SetSha256);
        Assert.False(normalized.CoreReferencesVerified);
    }

    [Theory]
    [InlineData("extra")][InlineData("changed-type")][InlineData("blank")]
    [InlineData("invalid")][InlineData("duplicate-key")]
    public void Marker_object_changes_and_invalid_json_are_denied_for_both_stores(string mutation)
    {
        foreach (var target in new[] { "audit", "outbox" })
        {
            var g = new Graph();
            var marker = InvoiceAmountCalculationEvidence.MarkerJson(g.Calculation);
            var changed = mutation switch
            {
                "extra" => marker[..^1] + ",\"unexpected\":true}",
                "changed-type" => marker.Replace("\"schemaVersion\":2", "\"schemaVersion\":\"2\"", StringComparison.Ordinal),
                "blank" => "  ",
                "invalid" => "{",
                "duplicate-key" => marker[..^1] + ",\"schemaVersion\":2}",
                _ => throw new InvalidOperationException()
            };
            if (target == "audit") g.Audit.ChangedFieldsJson = changed;
            else g.Message.PayloadJson = changed;
            Assert.Throws<InvalidOperationException>(() => g.Verify());
        }
    }

}
