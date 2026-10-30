using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceIntakeRoutes
{
    private static readonly JsonSerializerOptions CalculationJson = new(JsonSerializerDefaults.Web);

    private static IConfiguration CalculationConfig(HttpContext http)
        => http.RequestServices.GetRequiredService<IConfiguration>();
    private static DateTimeOffset CalculationNow(HttpContext http)
        => ProcurementEvidenceEncoding.DatabaseTimestamp(http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow())!.Value;
    private static bool CalculationReadsEnabled(IConfiguration config)
        => config.GetValue<bool>("Procurement:InvoiceCurrencyCalculation:ReadEnabled");
    private static bool CalculationWritesEnabled(IConfiguration config)
        => CalculationReadsEnabled(config) && config.GetValue<bool>("Procurement:InvoiceCurrencyCalculation:CaptureEnabled");

    internal static async Task<bool> CurrencyCalculationReady(ProcurementDbContext db,
        IConfiguration config, CancellationToken ct, bool capture = false)
    {
        if (!CalculationReadsEnabled(config)) return false;
        return await ProcurementInvoiceCurrencySchemaReadiness.IsReadyAsync(db, config,
            capture ? InvoiceCurrencySchemaMode.RecordCapture : InvoiceCurrencySchemaMode.RecordRead, ct);
    }

    private static IResult CalculationUnavailable() => Results.Json(new {
        code = "invoice-calculation-unavailable",
        message = "Currency-based invoice calculation is not ready. Your existing invoices are unchanged."
    }, statusCode: 503);

    private static async Task<IResult> PreviewCurrencyCalculation(InvoiceIntakeInput input,
        HttpRequest request, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!IntakeWriteContextAllowed(scope, request)) return IntakeBranchRequired();
        if (!CalculationWritesEnabled(CalculationConfig(request.HttpContext))
            || !await CurrencyCalculationReady(db, CalculationConfig(request.HttpContext), ct)) return CalculationUnavailable();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var precision = await ResolveCalculationPrecision(db, scope, input.CurrencyCode, CalculationNow(request.HttpContext), ct);
            input = input with { CurrencyCode = precision.CurrencyCode };
            var issue = Validate(input); if (issue is not null) return Invalid(issue);
            var calculation = CalculateInput(input, precision);
            return Results.Ok(new { schemaVersion = 2, precision, amounts = CalculationView(calculation),
                previewSha256 = PreviewDigest(input, precision) });
        }
        catch (InvalidOperationException e) { return CalculationProblem(e); }
    }

    private static async Task<IResult> CaptureCurrencyRounded(InvoiceIntakeInput input,
        HttpRequest request, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var config = CalculationConfig(request.HttpContext);
        if (!CalculationWritesEnabled(config) || !await CurrencyCalculationReady(db, config, ct, capture: true)) return CalculationUnavailable();
        if (input.ExpectedCurrencyReferenceId is null || input.ExpectedCurrencyRevision is null
            || input.ExpectedCalculationSha256 is not { Length: 64 })
            return Invalid("Review the currency calculation before saving this invoice.");
        if (!Guid.TryParse(request.Headers["Idempotency-Key"], out var commandKey) || commandKey == Guid.Empty)
            return Invalid("A saved command reference is required before creating this invoice.");
        var shapeIssue = Validate(input, allowDefaultCurrency: true);
        if (shapeIssue is not null) return Invalid(shapeIssue);
        var requestSha = CaptureRequestDigest(input);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var prior = await db.InvoiceIntakeCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ActorPrincipalId == scope.SubjectId && x.CommandKey == commandKey, ct);
            if (prior is not null)
            {
                if (prior.RequestSha256 != requestSha)
                    return Results.Conflict(new { code = "invoice-capture-key-conflict", message = "This command reference belongs to different invoice details. Recover the original result before continuing." });
                return await SavedCurrencyCapture(prior, db, scope, ct);
            }
            var precision = await ResolveCalculationPrecision(db, scope, input.CurrencyCode, CalculationNow(request.HttpContext), ct);
            input = input with { CurrencyCode = precision.CurrencyCode };
            var issue = Validate(input); if (issue is not null) return Invalid(issue);
            if (input.ExpectedCurrencyReferenceId != precision.ReferenceId
                || input.ExpectedCurrencyRevision != precision.Revision
                || input.ExpectedCalculationSha256 != PreviewDigest(input, precision))
                return Results.Conflict(new { code = "invoice-calculation-changed", message = "Invoice details or currency settings changed. Review the updated calculation before saving." });
            var result = CalculateInput(input, precision);
            var selectedBranch = scope.WorkLocationId;
            if (selectedBranch is not { } branch || !await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(
                    db.Database.GetDbConnection(), transaction.GetDbTransaction(), scope.TenantId,
                    scope.LegalEntityId, branch, precision.ObservedAtUtc, ct))
                return Invalid("The invoice branch is unavailable in this company.");
            var prepared = await Prepare(input, db, core, scope, null, ct, result, branch);
            var invoice = new SupplierInvoice { BranchRef = prepared.Branch, IntakeChannel = "Manual",
                Status = "Received", CreatedBy = scope.SubjectId };
            Apply(invoice, prepared); db.AddScoped(invoice);
            var lines = new List<SupplierInvoiceLine>(input.Lines!.Count);
            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = i + 1 };
                ApplyLine(line, input.Lines[i]); db.AddScoped(line); lines.Add(line);
            }
            var snapshot = new InvoiceCurrencyPrecisionSnapshot(precision.CurrencyCode,
                precision.ReferenceId, precision.Revision, precision.MinorUnits, ParseRounding(precision.Rounding));
            var evidence = InvoiceAmountCalculationEvidence.Create(invoice, lines, snapshot,
                precision.PayloadSha256, precision.PublishedAtUtc, precision.ObservedAtUtc, scope.SubjectId);
            db.AddScoped(evidence);
            var marker = InvoiceAmountCalculationEvidence.MarkerJson(evidence);
            db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceAmountCalculation), RecordId = evidence.Id,
                Action = InvoiceAmountCalculationEvidence.AuditAction, ActorSubjectId = scope.SubjectId,
                ChangedFieldsJson = marker, CorrelationId = scope.CorrelationId });
            db.AddScoped(new OutboxEvent { AggregateType = nameof(SupplierInvoice), AggregateId = invoice.Id,
                EventType = InvoiceAmountCalculationEvidence.EventType, EventVersion = 2, PayloadJson = marker,
                IdempotencyKey = "invoice-amount-calculation:" + evidence.Id.ToString("D") });
            var responseJson = JsonSerializer.Serialize(new {
                invoice.Id, invoice.InvoiceNumber, invoice.Status, invoice.GrossAmount, invoice.CurrencyCode,
                invoice.BranchRef, invoice.Version, intakeSchemaVersion = 2,
                calculationId = evidence.Id, amounts = CalculationView(result) }, CalculationJson);
            db.AddScoped(InvoiceIntakeCommandReceiptEvidence.Create(invoice, evidence, lines, commandKey,
                scope.SubjectId, requestSha, responseJson, CalculationNow(request.HttpContext)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Content(responseJson, "application/json", statusCode: 201);
        }
        catch (InvalidOperationException e) { return CalculationProblem(e); }
    }

    private static async Task<IResult> RecoverCurrencyCapture(Guid key, HttpRequest request,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (key == Guid.Empty) return Missing();
        if (!await CurrencyCalculationReady(db, CalculationConfig(request.HttpContext), ct)) return CalculationUnavailable();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var receipt = await db.InvoiceIntakeCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, ct);
        return receipt is null ? Missing() : await SavedCurrencyCapture(receipt, db, scope, ct);
    }

    private static async Task<IResult> SavedCurrencyCapture(InvoiceIntakeCommandReceipt receipt,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var invoice = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == receipt.SupplierInvoiceId, ct);
        if (invoice is null || receipt.ActorPrincipalId != scope.SubjectId) return Missing();
        var calculation = await db.InvoiceAmountCalculations.AsNoTracking().SingleAsync(x => x.Id == receipt.CalculationId, ct);
        var lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == invoice.Id).ToArrayAsync(ct);
        var reference = await CoreCurrencyPrecisionReader.ReadHistoricalAsync(db.Database.GetDbConnection(),
            db.Database.CurrentTransaction!.GetDbTransaction(), scope.TenantId, calculation.CoreCurrencyReferenceId, calculation.ObservedAtUtc, ct);
        RequireRetainedReference(calculation, reference);
        InvoiceIntakeCommandReceiptEvidence.Verify(receipt, invoice, calculation, lines);
        return Results.Content(receipt.ResponseJson, "application/json", statusCode: receipt.ResponseStatus);
    }

    private static async Task<IResult> ReadCurrencyCalculation(Guid id, HttpRequest request,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!await CurrencyCalculationReady(db, CalculationConfig(request.HttpContext), ct)) return CalculationUnavailable();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoice = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null) return Missing();
        var evidence = await db.InvoiceAmountCalculations.AsNoTracking().SingleOrDefaultAsync(x => x.SupplierInvoiceId == id, ct);
        if (evidence is null) return Results.NotFound(new { code = "legacy-invoice-calculation", message = "This invoice retains its original calculation." });
        try
        {
            var reference = await CoreCurrencyPrecisionReader.ReadHistoricalAsync(db.Database.GetDbConnection(),
                transaction.GetDbTransaction(), scope.TenantId, evidence.CoreCurrencyReferenceId, evidence.ObservedAtUtc, ct);
            RequireRetainedReference(evidence, reference);
            var lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == id).ToArrayAsync(ct);
            var result = InvoiceAmountCalculationEvidence.Verify(evidence, invoice, lines);
            return Results.Ok(new { schemaVersion = 2, calculationId = evidence.Id,
                invoiceId = id, precision = reference, evidence.CalculationSha256, amounts = CalculationView(result, lines) });
        }
        catch (InvalidOperationException e) { return CalculationProblem(e); }
    }

    internal static void RequireRetainedReference(InvoiceAmountCalculation calculation, CoreCurrencyPrecisionSnapshot reference)
    {
        if (calculation.CoreCurrencyReferenceId != reference.ReferenceId
            || calculation.CoreCurrencyReferenceRevision != reference.Revision
            || calculation.CoreSourcePayloadSha256 != reference.PayloadSha256
            || calculation.CurrencyCode != reference.CurrencyCode || calculation.MinorUnits != reference.MinorUnits
            || calculation.RoundingMode != reference.Rounding || calculation.CorePublishedAtUtc != reference.PublishedAtUtc)
            throw new InvalidOperationException("InvoiceCalculationReferenceChanged");
    }

    internal static async Task<bool> VerifyRetainedCurrencyRequirement(ProcurementDbContext db,
        ProcurementRequestScope scope, InvoiceAmountCoreReferenceRequirement requirement,
        IConfiguration config, CancellationToken ct)
    {
        if (!await ProcurementInvoiceCurrencySchemaReadiness.IsReadyAsync(db, config,
                InvoiceCurrencySchemaMode.ExecutionRead, ct)) return false;
        if (requirement.TenantId != scope.TenantId || requirement.LegalEntityId != scope.LegalEntityId
            || requirement.InvoiceId == Guid.Empty || requirement.CalculationId == Guid.Empty)
            return false;
        var reference = await CoreCurrencyPrecisionReader.ReadHistoricalAsync(db.Database.GetDbConnection(),
            db.Database.CurrentTransaction!.GetDbTransaction(), scope.TenantId, requirement.ReferenceId, requirement.ObservedAtUtc, ct);
        return reference.ReferenceId == requirement.ReferenceId && reference.Revision == requirement.Revision
            && reference.CurrencyCode == requirement.CurrencyCode && reference.MinorUnits == requirement.MinorUnits
            && reference.Rounding == requirement.Rounding.ToString() && reference.PayloadSha256 == requirement.SourcePayloadSha256
            && reference.PublishedAtUtc == requirement.PublishedAtUtc;
    }

    private static Task<CoreCurrencyPrecisionSnapshot> ResolveCalculationPrecision(ProcurementDbContext db,
        ProcurementRequestScope scope, string? currency, DateTimeOffset observed, CancellationToken ct)
        => CoreCurrencyPrecisionReader.ResolveForCompanyAsync(db.Database.GetDbConnection(),
            db.Database.CurrentTransaction!.GetDbTransaction(), scope.TenantId, scope.LegalEntityId,
            string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant(), observed, ct);

    private static InvoiceMoneyRounding ParseRounding(string value) => value switch {
        "ToEven" => InvoiceMoneyRounding.ToEven, "AwayFromZero" => InvoiceMoneyRounding.AwayFromZero,
        _ => throw new InvalidOperationException("CoreCurrencyReferenceInvalid") };

    private static InvoiceCurrencyCalculationResult CalculateInput(InvoiceIntakeInput input, CoreCurrencyPrecisionSnapshot precision)
        => InvoiceCurrencyRoundedCalculation.Calculate(new(precision.CurrencyCode, precision.ReferenceId,
            precision.Revision, precision.MinorUnits, ParseRounding(precision.Rounding)),
            input.Lines!.Select((line, index) => new InvoiceCurrencyCalculationLineInput(
                new Guid(index + 1, 0, 0, new byte[8]), line.Quantity, line.UnitPrice, line.TaxAmount)).ToArray());

    private static object NormalizedCaptureInput(InvoiceIntakeInput input) => new {
        schemaVersion = 2, input.SupplierId, input.PurchaseOrderId,
        invoiceNumber = input.InvoiceNumber?.Trim(), input.InvoiceDate, input.DueDate,
        currencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode) ? null : input.CurrencyCode.Trim().ToUpperInvariant(),
        lines = input.Lines?.Select((x, i) => new { line = i + 1, description = x.Description?.Trim(),
            quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitPrice), tax = Exact(x.TaxAmount),
            taxCodeRef = Clean(x.TaxCodeRef), costCenterRef = Clean(x.CostCenterRef) }).ToArray()
    };

    private static string CaptureRequestDigest(InvoiceIntakeInput input)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new {
            operation = InvoiceIntakeCommandReceiptEvidence.Operation, input = NormalizedCaptureInput(input),
            input.ExpectedCurrencyReferenceId, input.ExpectedCurrencyRevision, input.ExpectedCalculationSha256
        }, CalculationJson));

    private static string PreviewDigest(InvoiceIntakeInput input, CoreCurrencyPrecisionSnapshot precision)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new {
            schemaVersion = 2, input = NormalizedCaptureInput(input),
            precision.ReferenceId, precision.Revision, precision.PayloadSha256,
            precision.CurrencyCode, precision.MinorUnits, precision.Rounding
        }, CalculationJson));

    internal static object CalculationView(InvoiceCurrencyCalculationResult result,
        IReadOnlyList<SupplierInvoiceLine>? originals = null) => new {
        rawNet = Exact(result.RawNet), net = Exact(result.RoundedNet), tax = Exact(result.Tax),
        gross = Exact(result.Gross), adjustment = Exact(result.RoundingAdjustment),
        lines = result.Lines.Select((x, i) => new { lineNumber = originals is null ? i + 1
                : originals.Single(line => line.Id == x.OriginalLineId).LineNumber,
            quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitRate), rawNet = Exact(x.RawNet),
            net = Exact(x.RoundedNet), tax = Exact(x.EnteredTax), gross = Exact(x.Gross), adjustment = Exact(x.RoundingAdjustment) })
            .OrderBy(x => x.lineNumber).ToArray()
    };

    private static IResult CalculationProblem(InvalidOperationException error) => Results.Json(new {
        code = error.Message, message = error.Message switch {
            "InvoiceTaxPrecisionExceeded" => "Enter tax using the decimal places allowed for this currency.",
            "CoreCurrencyReferenceUnavailable" or "CoreCurrencyReferenceRetired" => "Set up an active currency calculation in Core settings before continuing.",
            "CoreCurrencyCompanyUnavailable" or "CoreCurrencyCodeInvalid" => "Choose a valid company currency.",
            "InvoiceCalculationAmountOutOfRange" or "ExactDecimalNotRepresentable" => "Invoice totals are too large. Check the quantities, prices and tax.",
            _ => "The invoice calculation could not be verified. Refresh the details before continuing."
        }
    }, statusCode: error.Message is "InvoiceTaxPrecisionExceeded" or "InvoiceCalculationAmountOutOfRange" or "ExactDecimalNotRepresentable" ? 422 : 409);

    private static async Task<bool> UnprocessedWithCalculationHistory(ProcurementDbContext db, SupplierInvoice invoice, CancellationToken ct)
        => await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id, ct)
            && (!await CalculationTableExists(db, ct)
                || !await db.InvoiceAmountCalculations.AsNoTracking().AnyAsync(x => x.SupplierInvoiceId == invoice.Id, ct));

    private static async Task<bool> CalculationTableExists(ProcurementDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return true;
        if (!db.Database.IsNpgsql()) return false;
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            command.CommandText = "SELECT to_regclass('procurement.invoice_amount_calculations') IS NOT NULL";
            return await command.ExecuteScalarAsync(ct) is true;
        }
        finally { if (opened) await connection.CloseAsync(); }
    }
}
