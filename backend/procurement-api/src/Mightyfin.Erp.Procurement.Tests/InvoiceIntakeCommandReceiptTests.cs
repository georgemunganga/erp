using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceIntakeCommandReceiptTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Exact(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    private static string Hash(string value) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private static (SupplierInvoice Invoice, SupplierInvoiceLine[] Lines,
        InvoiceAmountCalculation Calculation, InvoiceIntakeCommandReceipt Receipt) Fixture()
    {
        var (invoice, lines, calculation) = InvoiceAmountCalculationEvidenceTests.Fixture();
        return (invoice, lines, calculation, ValidReceipt(invoice, lines, calculation));
    }

    internal static InvoiceIntakeCommandReceipt ValidReceipt(SupplierInvoice invoice,
        SupplierInvoiceLine[] lines, InvoiceAmountCalculation calculation)
    {
        var result = InvoiceAmountCalculationEvidence.Verify(calculation, invoice, lines);
        var amounts = result.Lines.OrderBy(x => lines.Single(y => y.Id == x.OriginalLineId).LineNumber)
            .Select(x => new { lineNumber = lines.Single(y => y.Id == x.OriginalLineId).LineNumber,
                quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitRate),
                rawNet = Exact(x.RawNet), net = Exact(x.RoundedNet), tax = Exact(x.EnteredTax),
                gross = Exact(x.Gross), adjustment = Exact(x.RoundingAdjustment) }).ToArray();
        var response = JsonSerializer.Serialize(new {
            invoice.Id, invoice.InvoiceNumber, invoice.Status, invoice.GrossAmount,
            invoice.CurrencyCode, invoice.BranchRef, invoice.Version,
            intakeSchemaVersion = 2, calculationId = calculation.Id,
            amounts = new { rawNet = Exact(result.RawNet), net = Exact(result.RoundedNet),
                tax = Exact(result.Tax), gross = Exact(result.Gross),
                adjustment = Exact(result.RoundingAdjustment), lines = amounts }
        }, Json);
        return InvoiceIntakeCommandReceiptEvidence.Create(invoice, calculation, lines,
            Guid.NewGuid(), InvoiceAmountCalculationEvidenceTests.Actor, new string('a',64),
            response, InvoiceAmountCalculationEvidenceTests.At);
    }

    [Fact]
    public void Exact_original_response_and_actor_recovery_are_bound_to_saved_graph()
    {
        var (invoice, lines, calculation, receipt) = Fixture();
        InvoiceIntakeCommandReceiptEvidence.Verify(receipt, invoice, calculation, lines);
        InvoiceIntakeCommandReceiptEvidence.VerifySet([receipt], [calculation], [invoice], lines);
        Assert.Equal(201, receipt.ResponseStatus);
        Assert.Contains(calculation.Id.ToString("D"), receipt.ResponseJson);
    }

    [Fact]
    public void Complete_retained_set_rejects_missing_or_orphan_receipts()
    {
        var (invoice, lines, calculation, receipt) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceIntakeCommandReceiptEvidence.VerifySet([], [calculation], [invoice], lines));
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceIntakeCommandReceiptEvidence.VerifySet([receipt], [], [invoice], lines));
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceIntakeCommandReceiptEvidence.VerifySet([receipt], [calculation], [], lines));
    }

    [Fact]
    public void Later_matching_state_and_version_do_not_rewrite_original_201_receipt()
    {
        var (invoice, lines, calculation, receipt) = Fixture();
        invoice.MatchingState = "ComparisonBlocked";
        invoice.Status = "Exception";
        invoice.Version = 4;
        InvoiceIntakeCommandReceiptEvidence.VerifySet([receipt], [calculation], [invoice], lines);
    }

    [Theory]
    [InlineData("actor")][InlineData("key")][InlineData("scope")][InlineData("calculation")]
    [InlineData("request")][InlineData("response-id")][InlineData("response-amount")]
    [InlineData("response-extra")][InlineData("response-sha")][InlineData("status")]
    public void Foreign_actor_changed_key_amount_or_response_bytes_are_denied(string change)
    {
        var (invoice, lines, calculation, row) = Fixture();
        var changedResponse = change switch {
            "response-id" => row.ResponseJson.Replace(invoice.Id.ToString("D"),Guid.NewGuid().ToString("D")),
            "response-amount" => row.ResponseJson.Replace("\"gross\":\"5.22\"","\"gross\":\"5.23\""),
            "response-extra" => row.ResponseJson.Insert(1,"\"extra\":true,"),
            _ => row.ResponseJson };
        var copy = new InvoiceIntakeCommandReceipt {
            TenantId = change == "scope" ? "foreign" : row.TenantId,
            LegalEntityId = row.LegalEntityId, CreatedAt = row.CreatedAt,
            CreatedBy = row.CreatedBy, SupplierInvoiceId = row.SupplierInvoiceId,
            CalculationId = change == "calculation" ? Guid.NewGuid() : row.CalculationId,
            CommandKey = change == "key" ? Guid.Empty : row.CommandKey,
            ActorPrincipalId = change == "actor" ? Guid.NewGuid().ToString("D") : row.ActorPrincipalId,
            RequestSha256 = change == "request" ? "bad" : row.RequestSha256,
            ResponseJson = changedResponse,
            ResponseSha256 = change == "response-sha" ? new string('f',64) : Hash(changedResponse),
            ResponseStatus = change == "status" ? 200 : row.ResponseStatus,
            CompletedAtUtc = row.CompletedAtUtc
        };
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceIntakeCommandReceiptEvidence.Verify(copy, invoice, calculation, lines));
    }

    [Fact]
    public void Model_has_scoped_invoice_and_calculation_FKs_and_unique_actor_key()
    {
        var (invoice, _, _, _) = Fixture();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope(invoice.TenantId, invoice.LegalEntityId));
        var row = db.Model.FindEntityType(typeof(InvoiceIntakeCommandReceipt))!;
        Assert.NotNull(row);
        Assert.Contains(row.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "ActorPrincipalId", "CommandKey" }));
        Assert.Contains(row.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SupplierInvoice)
            && x.DeleteBehavior == DeleteBehavior.Restrict && x.Properties.Count == 3);
        Assert.Contains(row.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(InvoiceAmountCalculation)
            && x.DeleteBehavior == DeleteBehavior.Restrict && x.Properties.Count == 3);
    }

    [Fact]
    public async Task Atomic_parent_calculation_marker_and_receipt_set_is_required()
    {
        var (invoice, lines, calculation, receipt) = Fixture();
        await using var db = Store(invoice);
        Stage(db, invoice, lines, calculation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, await db.SupplierInvoices.AsNoTracking().CountAsync());
        db.AddScoped(receipt);
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.InvoiceIntakeCommandReceipts.CountAsync());
        db.InvoiceIntakeCommandReceipts.Remove(receipt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Orphan_receipt_and_duplicate_key_in_one_save_are_denied()
    {
        var (invoice, lines, calculation, receipt) = Fixture();
        await using var db = Store(invoice);
        Stage(db, invoice, lines, calculation);
        db.AddScoped(receipt);
        var duplicate = InvoiceIntakeCommandReceiptEvidence.Create(invoice, calculation, lines,
            receipt.CommandKey, receipt.ActorPrincipalId, receipt.RequestSha256,
            receipt.ResponseJson, receipt.CompletedAtUtc);
        db.AddScoped(duplicate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(duplicate).State = EntityState.Detached;
        db.Entry(calculation).State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<AuditEvent>().ToArray()) entry.State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<OutboxEvent>().ToArray()) entry.State = EntityState.Detached;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static ProcurementDbContext Store(SupplierInvoice invoice) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new Scope(invoice.TenantId, invoice.LegalEntityId));
    private static void Stage(ProcurementDbContext db, SupplierInvoice invoice,
        SupplierInvoiceLine[] lines, InvoiceAmountCalculation calculation)
    {
        db.AddScoped(invoice); foreach (var line in lines) db.AddScoped(line);
        db.AddScoped(calculation);
        var marker = InvoiceAmountCalculationEvidence.MarkerJson(calculation);
        db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceAmountCalculation),
            RecordId = calculation.Id, Action = InvoiceAmountCalculationEvidence.AuditAction,
            ActorSubjectId = calculation.CreatedBy, CreatedBy = calculation.CreatedBy,
            ChangedFieldsJson = marker });
        db.AddScoped(new OutboxEvent { AggregateType = nameof(SupplierInvoice),
            AggregateId = invoice.Id, EventType = InvoiceAmountCalculationEvidence.EventType,
            EventVersion = 2, CreatedBy = calculation.CreatedBy, PayloadJson = marker,
            IdempotencyKey = "invoice-amount-calculation:" + calculation.Id.ToString("D") });
    }

    private sealed record Scope(string TenantId, Guid LegalEntityId) : IProcurementScope
    {
        public string SubjectId => InvoiceAmountCalculationEvidenceTests.Actor;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "invoice-receipt-tests";
    }
}
