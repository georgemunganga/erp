using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Manual, structured invoice intake. Matching and AP posting are separate governed workflows.</summary>
public static partial class ProcurementInvoiceIntakeRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoices");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (IntakeProblem problem) { return Results.Json(new { code = problem.Code, message = problem.Message }, statusCode: problem.Status); }
            catch (DbUpdateConcurrencyException) { return StaleInvoice(); }
            catch (DbUpdateException error) when (IsInvoiceDuplicate(error, context.HttpContext.RequestServices.GetRequiredService<ProcurementDbContext>())) { return Duplicate(); }
            catch (Exception error) when (error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) { return StaleInvoice(); }
            catch (Exception error) when (error is DbUpdateException or NpgsqlException)
            { return Results.Json(new { code = "invoice-save-unconfirmed", message = "Invoice storage is unavailable. Refresh this invoice before trying again." }, statusCode: 503); }
        });
        group.MapGet("", List).RequireAuthorization("procurement-invoice-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-invoice-read");
        group.MapPost("", Capture).Accepts<InvoiceIntakeInput>("application/json").RequireAuthorization("procurement-invoice-capture");
        group.MapPut("/{id:guid}", Update).Accepts<InvoiceIntakeUpdateInput>("application/json").RequireAuthorization("procurement-invoice-read", "procurement-invoice-capture");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-invoice-read", "procurement-invoice-capture");
        ProcurementInvoiceFinanceReadinessRoutes.MapRoutes(app);
    }

    // Shared canonical released invoice scope: selected branches and unknown legacy branches
    // must behave identically for intake, documents, credit intake and AP readiness.
    internal static IQueryable<SupplierInvoice> ReadableInvoices(ProcurementDbContext db, ProcurementRequestScope scope) => Visible(db, scope);

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope, int? page, int? pageSize,
        CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = Visible(db, scope);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.SupplierId, x.BranchRef,
                SupplierName = db.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.DisplayName).FirstOrDefault(),
                x.InvoiceNumber, x.InvoiceDate,
                x.CurrencyCode, x.GrossAmount, x.Status, x.IntakeChannel, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size, intakeCrudVersion = 1 });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
    {
        var invoice = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null) return Results.NotFound(new { code = "not-found", message = "Invoice not found." });
        var lines = await db.SupplierInvoiceLines.AsNoTracking()
            .Where(x => x.SupplierInvoiceId == id && x.IsActive).OrderBy(x => x.LineNumber)
            .Select(x => new { x.Id, x.LineNumber, x.Description, x.Quantity,
                x.UnitPrice, x.TaxAmount, x.TaxCodeRef, x.CostCenterRef,
                exactAmounts = new { quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitPrice), taxAmount = Exact(x.TaxAmount) } })
            .ToListAsync(ct);
        var supplierName = await db.Suppliers.AsNoTracking().Where(x => x.Id == invoice.SupplierId)
            .Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        return Results.Ok(new { invoice.Id, invoice.SupplierId, invoice.BranchRef, supplierName, invoice.PurchaseOrderId,
            invoice.InvoiceNumber, invoice.InvoiceDate, invoice.DueDate, invoice.CurrencyCode,
            invoice.NetAmount, invoice.TaxAmount, invoice.GrossAmount, invoice.IntakeChannel,
            invoice.Status, invoice.Version, invoice.CreatedAt, lines, intakeCrudVersion = 1,
            allowedActions = await Actions(db, invoice, scope, http, authorization, ct),
            exactAmounts = new { netAmount = Exact(invoice.NetAmount), taxAmount = Exact(invoice.TaxAmount), grossAmount = Exact(invoice.GrossAmount) },
            nextStep = "Confirm source evidence and resolve duplicate or PO exceptions before matching." });
    }

    internal static async Task<IResult> Capture(HttpRequest request, ProcurementDbContext db,
        ProcurementRequestScope scope, HrmDbContext core, CancellationToken ct)
    {
        if (!IntakeWriteContextAllowed(scope, request)) return IntakeBranchRequired();
        if (!request.HasJsonContentType()) return Results.StatusCode(415);
        InvoiceIntakeInput? input;
        try { input = await request.ReadFromJsonAsync<InvoiceIntakeInput>(ct); }
        catch (Exception error) when (error is JsonException or BadHttpRequestException)
        { return Invalid("Enter exact invoice amounts with up to four decimal places."); }
        if (input is null) return Invalid("Enter the invoice details.");
        var prepared = await Prepare(input, db, core, scope, null, ct);
        var invoice = new SupplierInvoice { BranchRef = prepared.Branch, IntakeChannel = "Manual", Status = "Received" };
        Apply(invoice, prepared); db.AddScoped(invoice);
        for (var i = 0; i < prepared.Input.Lines!.Count; i++)
        {
            var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = i + 1 };
            ApplyLine(line, prepared.Input.Lines[i]); db.AddScoped(line);
        }
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/invoices/{invoice.Id}", Summary(invoice));
    }

    private static string? Validate(InvoiceIntakeInput input)
    {
        if (input.SupplierId == Guid.Empty) return "Choose a vendor.";
        if (string.IsNullOrWhiteSpace(input.InvoiceNumber) || input.InvoiceNumber.Trim().Length > 80)
            return "Enter an invoice number of up to 80 characters.";
        if (input.CurrencyCode is null || input.CurrencyCode.Trim().Length != 3 ||
            !input.CurrencyCode.Trim().All(char.IsAsciiLetter))
            return "Choose a three-letter currency code.";
        if (input.InvoiceDate == default) return "Enter an invoice date.";
        if (input.DueDate.HasValue && input.DueDate < input.InvoiceDate)
            return "Due date cannot be before invoice date.";
        if (input.Lines is null || input.Lines.Count is < 1 or > 200)
            return "Enter between 1 and 200 invoice lines.";
        foreach (var line in input.Lines)
        {
            if (line is null || string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 500)
                return "Each line needs a description of up to 500 characters.";
            if (line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxAmount < 0)
                return "Each line needs a positive quantity and non-negative price and tax.";
            if (!ExactDecimalJsonCodec.IsRepresentable(line.Quantity, 18, 4) || !ExactDecimalJsonCodec.IsRepresentable(line.UnitPrice, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.TaxAmount, 18, 4)) return "Each quantity, price and tax must fit 18 digits with up to four decimal places.";
        }
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Invalid(string message) => Results.UnprocessableEntity(new { code = "validation-failed", message });
}

public sealed record InvoiceIntakeInput(Guid SupplierId, Guid? PurchaseOrderId,
    string? InvoiceNumber, DateOnly InvoiceDate, DateOnly? DueDate, string? CurrencyCode,
    List<InvoiceIntakeLineInput>? Lines);
public sealed record InvoiceIntakeLineInput(string? Description,
    [property: JsonConverter(typeof(InvoiceIntakeDecimalJsonConverter))] decimal Quantity,
    [property: JsonConverter(typeof(InvoiceIntakeDecimalJsonConverter))] decimal UnitPrice,
    [property: JsonConverter(typeof(InvoiceIntakeDecimalJsonConverter))] decimal TaxAmount, string? TaxCodeRef, string? CostCenterRef);
