using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class InvoiceIntakeDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "InvoiceAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
public sealed record InvoiceIntakeUpdateInput(long Version, InvoiceIntakeInput? Draft, string? Reason);
public sealed record InvoiceIntakeCancelInput(long Version, string? Reason);

public static partial class ProcurementInvoiceIntakeRoutes
{
    private static IQueryable<SupplierInvoice> Visible(ProcurementDbContext db, ProcurementRequestScope scope)
    {
        var query = db.SupplierInvoices.AsNoTracking();
        if (scope.IsExplicitAllBranches)
        {
            var permitted = scope.PermittedWorkLocationIds.ToArray();
            return query.Where(x => x.BranchRef != null && permitted.Contains(x.BranchRef.Value));
        }
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null) return query.Where(_ => false);
        if (scope.WorkLocationId is { } branch) query = query.Where(x => x.BranchRef == branch);
        return query;
    }
    private static bool IsMakerOrManager(SupplierInvoice invoice, ProcurementRequestScope scope, HttpContext http)
        => invoice.CreatedBy == scope.SubjectId || http.User.IsInRole("erp_admin") || http.User.IsInRole("procurement_manager");
    private static Task<bool> Unprocessed(ProcurementDbContext db, SupplierInvoice invoice, CancellationToken ct)
        => db.ManualInvoiceDrafts().AnyAsync(row => row.Id == invoice.Id, ct);
    private static async Task<string[]> Actions(ProcurementDbContext db, SupplierInvoice invoice,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
        => IsMakerOrManager(invoice, scope, http)
            && (await authorization.AuthorizeAsync(http.User, "procurement-invoice-capture")).Succeeded
            && await Unprocessed(db, invoice, ct) ? ["Edit", "Cancel"] : [];
    private static string Exact(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    private sealed record Prepared(InvoiceIntakeInput Input, decimal Net, decimal Tax, decimal Gross, Guid? Branch);
    private sealed class IntakeProblem(string code, string message, int status) : Exception(message)
    { internal string Code => code; internal int Status => status; }

    private static async Task<Prepared> Prepare(InvoiceIntakeInput input, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, SupplierInvoice? existing, CancellationToken ct)
    {
        input = input with { CurrencyCode = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? existing?.CurrencyCode ?? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : input.CurrencyCode };
        var issue = Validate(input); if (issue is not null) throw new IntakeProblem("validation-failed", issue, 422);
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null)
            throw new IntakeProblem("invoice-scope-unavailable", "Select a company or an authorized branch for invoice intake.", 422);
        decimal net, tax, gross;
        try
        {
            net = ExactDecimalArithmetic.Sum(input.Lines!.Select(x => ExactDecimalArithmetic.Multiply(x.Quantity, x.UnitPrice)));
            tax = ExactDecimalArithmetic.Sum(input.Lines!.Select(x => x.TaxAmount)); gross = ExactDecimalArithmetic.Add(net, tax);
        }
        catch (InvalidOperationException)
        { throw new IntakeProblem("validation-failed", "Invoice totals cannot be represented exactly. Check quantities, prices and tax; nothing was rounded.", 422); }
        if (!ExactDecimalJsonCodec.IsRepresentable(net, 18, 4) || !ExactDecimalJsonCodec.IsRepresentable(tax, 18, 4)
            || !ExactDecimalJsonCodec.IsRepresentable(gross, 18, 4))
            throw new IntakeProblem("validation-failed", "Invoice totals must fit 18 digits with up to four decimal places; nothing was rounded.", 422);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.SupplierId, ct);
        if (supplier is null || supplier.Status is not ("Active" or "Suspended" or "Inactive"))
            throw new IntakeProblem("validation-failed", "Choose an existing approved vendor. A vendor on hold can still have invoices recorded for review.", 422);
        PurchaseOrder? order = null;
        if (input.PurchaseOrderId is { } poId)
        {
            order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == poId, ct);
            if (order is null || order.SupplierId != supplier.Id
                || scope.WorkLocationId is { } selected && order.BranchRef != selected
                || existing is not null && order.BranchRef != existing.BranchRef)
                throw new IntakeProblem("validation-failed", "Choose an available purchase order for this vendor and the invoice's retained branch.", 422);
        }
        var branch = existing is null ? scope.WorkLocationId ?? order?.BranchRef : existing.BranchRef;
        if (branch is { } branchId && !await core.WorkLocations.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
            && x.Id == branchId && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived, ct))
            throw new IntakeProblem("validation-failed", "The invoice branch is unavailable in this company.", 422);
        var normalized = input.InvoiceNumber!.Trim().ToUpperInvariant(); var currency = input.CurrencyCode!.Trim().ToUpperInvariant();
        if (await db.SupplierInvoices.AnyAsync(x => (existing == null || x.Id != existing.Id)
            && x.SupplierId == supplier.Id && x.NormalizedInvoiceNumber == normalized && x.CurrencyCode == currency, ct))
            throw new IntakeProblem("duplicate-invoice", "This vendor invoice number is already recorded in this currency.", 409);
        return new(input, net, tax, gross, branch);
    }

    private static async Task<IResult> Update(Guid id, HttpRequest request, ProcurementDbContext db,
        ProcurementRequestScope scope, HrmDbContext core, HttpContext http, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(415);
        InvoiceIntakeUpdateInput? input;
        try { input = await request.ReadFromJsonAsync<InvoiceIntakeUpdateInput>(ct); }
        catch (Exception error) when (error is JsonException or BadHttpRequestException) { return Invalid("Enter valid exact invoice amounts with up to four decimal places."); }
        if (input?.Draft is null || input.Version <= 0 || !ValidReason(input.Reason)) return Invalid("Supply the current version, invoice details and a correction reason of up to 1,000 characters.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var visible = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (visible is null || !IsMakerOrManager(visible, scope, http)) return Missing();
        if (visible.Version != input.Version) return StaleInvoice();
        if (!await Unprocessed(db, visible, ct)) return LockedInvoice();
        var prepared = await Prepare(input.Draft, db, core, scope, visible, ct);
        var invoice = await db.SupplierInvoices.SingleAsync(x => x.Id == id, ct);
        var lines = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.LineNumber).ToListAsync(ct);
        var before = ProcurementDraftAudit.Snapshot(new { invoice, lines });
        Apply(invoice, prepared);
        var active = lines.Where(x => x.IsActive).ToArray(); var nextNumber = lines.Select(x => x.LineNumber).DefaultIfEmpty().Max() + 1;
        for (var i = 0; i < prepared.Input.Lines!.Count; i++)
        {
            var line = i < active.Length ? active[i] : new SupplierInvoiceLine { SupplierInvoiceId = id, LineNumber = nextNumber++ };
            ApplyLine(line, prepared.Input.Lines[i]);
            if (i >= active.Length) { db.AddScoped(line); lines.Add(line); }
        }
        foreach (var removed in active.Skip(prepared.Input.Lines.Count)) removed.IsActive = false;
        db.Entry(invoice).Property(x => x.UpdatedAt).IsModified = true; // Even an exact repeated correction advances CAS once.
        ProcurementDraftAudit.Add(db, scope, nameof(SupplierInvoice), id, "InvoiceIntakeCorrected", before,
            new { invoice, lines, reason = input.Reason!.Trim(), resultingVersion = checked(invoice.Version + 1) });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(Summary(invoice));
    }
    private static async Task<IResult> Cancel(Guid id, InvoiceIntakeCancelInput input, ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, CancellationToken ct)
    {
        if (input.Version <= 0 || !ValidReason(input.Reason)) return Invalid("Supply the current version and a cancellation reason of up to 1,000 characters.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var visible = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (visible is null || !IsMakerOrManager(visible, scope, http)) return Missing();
        if (visible.Version != input.Version) return StaleInvoice();
        if (!await Unprocessed(db, visible, ct)) return LockedInvoice();
        var invoice = await db.SupplierInvoices.SingleAsync(x => x.Id == id, ct);
        var before = ProcurementDraftAudit.Snapshot(invoice); invoice.Status = "Cancelled";
        ProcurementDraftAudit.Add(db, scope, nameof(SupplierInvoice), id, "InvoiceIntakeCancelled", before,
            new { invoice, reason = input.Reason!.Trim(), resultingVersion = checked(invoice.Version + 1) });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(Summary(invoice));
    }
    private static bool ValidReason(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 1000;
    private static IResult Missing() => Results.NotFound(new { code = "not-found", message = "Invoice not found." });
    private static IResult StaleInvoice() => Results.Conflict(new { code = "stale-invoice", message = "This invoice changed. Refresh it before trying again." });
    private static IResult LockedInvoice() => Results.Conflict(new { code = "invoice-locked", message = "Only an unprocessed manual Received invoice without source documents or downstream evidence can be corrected or cancelled." });
    private static object Summary(SupplierInvoice x) => new { x.Id, x.InvoiceNumber, x.Status, x.GrossAmount, x.CurrencyCode, x.BranchRef, x.Version };
    private static void Apply(SupplierInvoice invoice, Prepared prepared)
    {
        var input = prepared.Input;
        invoice.SupplierId = input.SupplierId; invoice.PurchaseOrderId = input.PurchaseOrderId;
        invoice.InvoiceNumber = input.InvoiceNumber!.Trim(); invoice.NormalizedInvoiceNumber = input.InvoiceNumber.Trim().ToUpperInvariant();
        invoice.CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(); invoice.InvoiceDate = input.InvoiceDate; invoice.DueDate = input.DueDate;
        invoice.NetAmount = prepared.Net; invoice.TaxAmount = prepared.Tax; invoice.GrossAmount = prepared.Gross;
    }
    private static void ApplyLine(SupplierInvoiceLine line, InvoiceIntakeLineInput input)
    {
        line.Description = input.Description!.Trim(); line.Quantity = input.Quantity; line.UnitPrice = input.UnitPrice; line.TaxAmount = input.TaxAmount;
        line.TaxCodeRef = Clean(input.TaxCodeRef); line.CostCenterRef = Clean(input.CostCenterRef);
    }
    private static IResult Duplicate() => Results.Conflict(new { code = "duplicate-invoice", message = "This vendor invoice number is already recorded in this currency." });
    private static bool IsInvoiceDuplicate(DbUpdateException error, ProcurementDbContext db)
        => error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "supplier_invoices" } pg
            && db.Model.FindEntityType(typeof(SupplierInvoice))!.GetIndexes().Any(index => index.IsUnique
                && index.Properties.Any(x => x.Name is nameof(SupplierInvoice.InvoiceNumber) or nameof(SupplierInvoice.NormalizedInvoiceNumber))
                && index.GetDatabaseName() == pg.ConstraintName);
}
