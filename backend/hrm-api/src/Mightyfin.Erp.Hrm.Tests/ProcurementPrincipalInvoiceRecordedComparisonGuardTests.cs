using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceRecordedComparisonGuardTests
{
    private static SupplierInvoice Invoice() => new()
    {
        Id = Guid.NewGuid(), TenantId = "tenant", LegalEntityId = Guid.NewGuid()
    };

    private static ProcurementCommercialCommandReceipt Receipt(SupplierInvoice invoice, Guid invoiceId, Guid? orderId = null)
    {
        var key = Guid.NewGuid();
        return new ProcurementCommercialCommandReceipt
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            PurchaseOrderId = orderId ?? Guid.NewGuid(), Operation = "InvoiceCompare",
            ActorPrincipalId = Guid.NewGuid().ToString("D"), CommandKey = key,
            CompletedAt = new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero),
            Fingerprint = new string('a', 64),
            ResponseJson = JsonSerializer.Serialize(new
            {
                invoiceId, commandKey = key, commandPermission = "procurement-invoice-match",
                commandSchemaVersion = 3, sourceSchemaVersion = 3,
                recordedHistoricalResult = true, isCurrentApproval = false, canPostToFinance = false,
                result = new { id = Guid.NewGuid(), outcome = "Blocked", version = 4,
                    matchingState = "ComparisonBlocked", hasCapacityClaim = false,
                    canPostToFinance = false, discrepancies = Array.Empty<object>(), sourceBlockers = Array.Empty<string>() }
            })
        };
    }

    [Fact]
    public void A_recorded_source3_result_closes_only_its_own_invoice()
    {
        var invoice = Invoice();
        var oldResult = new InvoiceMatchResult
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, ExecutionVersion = 2, SourceSchemaVersion = 3,
            Outcome = "Blocked"
        };
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, true, [oldResult], []));
        var sibling = Invoice();
        sibling.LegalEntityId = invoice.LegalEntityId;
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            sibling, true, [oldResult], []));
    }

    [Fact]
    public void Legacy_mapping_entry_cannot_reopen_a_source3_comparison_but_keeps_legacy_only_history()
    {
        var principal = Invoice();
        var principalResult = new InvoiceMatchResult {
            TenantId = principal.TenantId, LegalEntityId = principal.LegalEntityId,
            SupplierInvoiceId = principal.Id, SourceSchemaVersion = 3,
            ExecutionVersion = 2, Outcome = "Blocked" };
        principal.MatchRevision = 1;
        principal.LastMatchResultId = principalResult.Id;
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            principal, true, [principalResult], []));

        var legacy = Invoice();
        var legacyResult = new InvoiceMatchResult {
            TenantId = legacy.TenantId, LegalEntityId = legacy.LegalEntityId,
            SupplierInvoiceId = legacy.Id, SourceSchemaVersion = null,
            ExecutionVersion = 1, Outcome = "Blocked" };
        legacy.MatchRevision = 1;
        legacy.LastMatchResultId = legacyResult.Id;
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            legacy, false, [legacyResult], []));
    }

    [Fact]
    public void A_sibling_invoice_receipt_on_the_same_order_does_not_close_this_invoice()
    {
        var invoice = Invoice();
        var orderId = Guid.NewGuid();
        var sibling = Receipt(invoice, Guid.NewGuid(), orderId);
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, true, [], [sibling]));
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, true, [], [sibling, Receipt(invoice, invoice.Id, orderId)]));
        var truncatedSibling = Receipt(invoice, Guid.NewGuid(), orderId);
        truncatedSibling = CopyWithResponse(truncatedSibling, "{\"invoiceId\":\"" + Guid.NewGuid().ToString("D") + "\"}");
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, true, [], [truncatedSibling]));
    }

    [Theory]
    [InlineData("last")]
    [InlineData("revision")]
    [InlineData("claim")]
    public void Inconsistent_source3_pointers_are_never_an_empty_history(string mutation)
    {
        var invoice = Invoice();
        if (mutation == "last") invoice.LastMatchResultId = Guid.NewGuid();
        if (mutation == "revision") invoice.MatchRevision = 1;
        if (mutation == "claim") invoice.ActiveMatchClaimResultId = Guid.NewGuid();
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, true, [], []));
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, false, [], []));
    }

    [Fact]
    public void Malformed_target_receipt_fails_closed_but_legacy_history_remains_distinct()
    {
        var invoice = Invoice();
        var malformed = CopyWithResponse(Receipt(invoice, invoice.Id),
            "{\"invoiceId\":\"" + invoice.Id.ToString("D") + "\"");
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [], [malformed]));
        var legacy = new InvoiceMatchResult
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, ExecutionVersion = 1, Outcome = "Blocked"
        };
        invoice.LastMatchResultId = legacy.Id;
        invoice.MatchRevision = 1;
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, false, [legacy], []));
        var validTarget = Receipt(invoice, invoice.Id);
        var firstIdentity = "\"invoiceId\":\"" + invoice.Id.ToString("D") + "\"";
        var duplicateIdentity = validTarget.ResponseJson.Replace(firstIdentity,
            firstIdentity + ",\"invoiceId\":\"" + Guid.NewGuid().ToString("D") + "\"",
            StringComparison.Ordinal);
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [], [CopyWithResponse(validTarget, duplicateIdentity)]));
    }

    [Fact]
    public void Malformed_sibling_metadata_or_blocked_claim_cannot_be_skipped()
    {
        var invoice = Invoice();
        var sibling = Receipt(invoice, Guid.NewGuid());
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, false, [],
            [CopyWithResponse(sibling, sibling.ResponseJson, fingerprint: new string('z', 64))]));
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, false, [],
            [CopyWithResponse(sibling, sibling.ResponseJson, actor: Guid.Empty.ToString("D"))]));
        var falseClaim = sibling.ResponseJson.Replace("\"hasCapacityClaim\":false",
            "\"hasCapacityClaim\":true", StringComparison.Ordinal);
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(invoice, false, [],
            [CopyWithResponse(sibling, falseClaim)]));
    }

    [Fact]
    public void Legacy_map_admission_preserves_typed_legacy_receipts_but_closes_orphan_principal_work()
    {
        var invoice = Invoice();
        var orderId = Guid.NewGuid();
        var legacy = Receipt(invoice, invoice.Id, orderId);
        legacy = CopyWithResponse(legacy, legacy.ResponseJson.Replace(
            "\"commandSchemaVersion\":3,\"sourceSchemaVersion\":3",
            "\"commandSchemaVersion\":1", StringComparison.Ordinal));
        var legacyResult = new InvoiceMatchResult
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, ExecutionVersion = 1, Outcome = "Blocked"
        };
        invoice.MatchRevision = 1;
        invoice.LastMatchResultId = legacyResult.Id;
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [legacyResult], [legacy], principalOnly: true));

        var siblingPrincipal = Receipt(invoice, Guid.NewGuid(), orderId);
        Assert.False(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [legacyResult], [legacy, siblingPrincipal], principalOnly: true));
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [legacyResult], [legacy, Receipt(invoice, invoice.Id, orderId)], principalOnly: true));
        var truncatedSibling = CopyWithResponse(siblingPrincipal, "{\"invoiceId\":\"" +
            Guid.NewGuid().ToString("D") + "\"}");
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [legacyResult], [legacy, truncatedSibling], principalOnly: true));
        invoice.LastMatchResultId = Guid.NewGuid();
        Assert.True(ProcurementPrincipalInvoiceRecordedComparisonGuard.IsLocked(
            invoice, false, [legacyResult], [legacy], principalOnly: true));
    }

    [Fact]
    public void Legacy_map_route_checks_invoice_scoped_principal_history_after_replay_before_po_evidence()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
            "Mightyfin.Erp.Hrm.Api", "ProcurementInvoiceMatchingRoutes.cs")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory!.FullName,
            "Mightyfin.Erp.Hrm.Api", "ProcurementInvoiceMatchingRoutes.cs"));
        var map = source[source.IndexOf("private static Task<IResult> Map(", StringComparison.Ordinal)..];
        var replay = map.IndexOf("if(await Replay(", StringComparison.Ordinal);
        var fence = map.IndexOf("principalOnly: true", StringComparison.Ordinal);
        var invoiceLock = map.IndexOf("Invoice(db, scope, id, token, true)", StringComparison.Ordinal);
        var reload = map.IndexOf("Entry(invoice).ReloadAsync", StringComparison.Ordinal);
        var secondFence = map.IndexOf("principalOnly: true", fence + 1, StringComparison.Ordinal);
        var terminalDenial = map.IndexOf("throw new MatchingProblem(ProcurementPrincipalInvoiceRecordedComparisonGuard.Code, 409)", StringComparison.Ordinal);
        var poEvidence = map.IndexOf("ProcurementReceivingRoutes.InvoiceEvidence", StringComparison.Ordinal);
        Assert.True(replay >= 0 && replay < fence && fence < invoiceLock
            && invoiceLock < reload && reload < secondFence
            && secondFence < terminalDenial && terminalDenial < poEvidence);
        Assert.DoesNotContain("InvoiceMatchResults.AsNoTracking().AnyAsync", map);
    }

    private static ProcurementCommercialCommandReceipt CopyWithResponse(
        ProcurementCommercialCommandReceipt source, string response,
        string? fingerprint = null, string? actor = null) => new()
    {
        TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
        PurchaseOrderId = source.PurchaseOrderId, Operation = source.Operation,
        ActorPrincipalId = actor ?? source.ActorPrincipalId, CommandKey = source.CommandKey,
        Fingerprint = fingerprint ?? source.Fingerprint, ResponseJson = response, CompletedAt = source.CompletedAt
    };
}
