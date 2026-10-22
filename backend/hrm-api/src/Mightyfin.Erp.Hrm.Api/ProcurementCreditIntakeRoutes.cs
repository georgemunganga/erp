using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record CreditCaptureInput(Guid SupplierInvoiceId, string? CreditNumber,
    [property: JsonConverter(typeof(InvoiceIntakeDecimalJsonConverter))] decimal Amount, string? Reason);
public sealed record CreditDraftInput(string? CreditNumber,
    [property: JsonConverter(typeof(InvoiceIntakeDecimalJsonConverter))] decimal Amount, string? Reason);
public sealed record CreditUpdateInput(long Version, CreditDraftInput? Draft, string? Reason);
public sealed record CreditCancelInput(long Version, string? Reason);

/// <summary>Documentary invoice-linked credit drafts only; no AP posting, application or refund.</summary>
public static partial class ProcurementInvoiceIntakeRoutes
{
    public static void MapCreditRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/vendor-credits");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (IntakeProblem problem) { return Results.Json(new { code = problem.Code, message = problem.Message }, statusCode: problem.Status); }
            catch (DbUpdateConcurrencyException) { return CreditStale(); }
            catch (Exception error) when (error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) { return CreditStale(); }
            catch (DbUpdateException error) when (IsCreditDuplicate(error, context.HttpContext.RequestServices.GetRequiredService<ProcurementDbContext>()))
            { return CreditDuplicate(); }
            catch (Exception error) when (error is DbUpdateException or NpgsqlException)
            { return Results.Json(new { code = "credit-save-unconfirmed", message = "Credit storage is unavailable. Refresh this reference before trying again." }, statusCode: 503); }
        });
        group.MapGet("", CreditList).RequireAuthorization("procurement-invoice-read");
        group.MapGet("/{id:guid}", CreditDetail).RequireAuthorization("procurement-invoice-read");
        group.MapPost("", CreditCapture).Accepts<CreditCaptureInput>("application/json")
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-capture");
        group.MapPut("/{id:guid}", CreditUpdate).Accepts<CreditUpdateInput>("application/json")
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-capture");
        group.MapPost("/{id:guid}/cancel", CreditCancel).RequireAuthorization("procurement-invoice-read", "procurement-invoice-capture");
    }
    private static IQueryable<SupplierCreditNoteRef> CreditVisible(ProcurementDbContext db, ProcurementRequestScope scope)
    {
        var invoices = Visible(db, scope);
        return db.SupplierCreditNoteRefs.AsNoTracking().Where(credit => credit.SupplierInvoiceId != null
            && invoices.Any(invoice => invoice.Id == credit.SupplierInvoiceId
                && invoice.SupplierId == credit.SupplierId && invoice.CurrencyCode == credit.CurrencyCode));
    }
    private static async Task<IResult> CreditList(ProcurementDbContext db, ProcurementRequestScope scope,
        int? page, int? pageSize, Guid? invoiceId, string? search, string? status, CancellationToken ct)
    {
        search = Clean(search); status = Clean(status);
        if (search?.Length > 160 || status is not (null or "Draft" or "Cancelled"))
            return Invalid("Search up to 160 characters and choose Draft or Cancelled, or all statuses.");
        var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = CreditVisible(db, scope);
        if (invoiceId.HasValue) query = query.Where(x => x.SupplierInvoiceId == invoiceId.Value);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (search is not null)
        {
            var term = search.ToUpperInvariant();
            query = query.Where(x => x.CreditNumber.ToUpper().Contains(term)
                || db.SupplierInvoices.Any(invoice => invoice.Id == x.SupplierInvoiceId && invoice.InvoiceNumber.ToUpper().Contains(term))
                || db.Suppliers.Any(supplier => supplier.Id == x.SupplierId && supplier.DisplayName.ToUpper().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size).Select(x => new
            {
                x.Id, x.SupplierInvoiceId, x.SupplierId,
                invoiceNumber = db.SupplierInvoices.Where(i => i.Id == x.SupplierInvoiceId).Select(i => i.InvoiceNumber).FirstOrDefault(),
                supplierName = db.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.DisplayName).FirstOrDefault(),
                x.CreditNumber, x.Status, x.CurrencyCode, x.Amount, exactAmount = Exact(x.Amount), x.Version, x.CreatedAt,
            }).ToListAsync(ct);
        return Results.Ok(new { items, total, page = number, pageSize = size, creditCrudVersion = 1 });
    }
    private static async Task<IResult> CreditDetail(Guid id, ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService authorization, CancellationToken ct)
    {
        var credit = await CreditVisible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (credit is null) return CreditMissing();
        return Results.Ok(await CreditView(db, scope, http, authorization, credit, ct));
    }
    private static bool CreditMaker(SupplierCreditNoteRef credit, ProcurementRequestScope scope, HttpContext http)
        => credit.CreatedBy == scope.SubjectId || http.User.IsInRole("erp_admin") || http.User.IsInRole("procurement_manager");
    private static bool CreditEditable(SupplierCreditNoteRef credit)
        => credit.Status == "Draft" && credit.FinanceCreditRef is null && credit.SupplierReturnId is null;
    private static async Task<object> CreditView(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService authorization, SupplierCreditNoteRef credit, CancellationToken ct)
    {
        var invoice = await Visible(db, scope).SingleAsync(x => x.Id == credit.SupplierInvoiceId, ct);
        var supplierName = await db.Suppliers.Where(x => x.Id == credit.SupplierId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct);
        var canEdit = CreditMaker(credit, scope, http) && CreditEditable(credit)
            && (await authorization.AuthorizeAsync(http.User, "procurement-invoice-capture")).Succeeded;
        return new { credit.Id, credit.SupplierInvoiceId, invoiceNumber = invoice.InvoiceNumber, credit.SupplierId,
            supplierName, credit.CreditNumber, credit.Status, credit.CurrencyCode, credit.Amount, exactAmount = Exact(credit.Amount),
            credit.Reason, credit.Version, credit.CreatedAt, creditCrudVersion = 1,
            allowedActions = canEdit ? new[] { "Edit", "Cancel" } : Array.Empty<string>() };
    }
    private static async Task<IResult> CreditCapture(HttpRequest request, ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
    {
        var input = await CreditBody<CreditCaptureInput>(request, ct);
        if (input.SupplierInvoiceId == Guid.Empty) return Invalid("Choose an existing bill.");
        CreditValidate(input.CreditNumber, input.Amount, input.Reason);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var invoice = await CreditInvoice(db, scope, input.SupplierInvoiceId, ct);
        await CreditLockInvoice(db, scope, invoice.Id, ct);
        await CreditNumberAvailable(db, invoice.SupplierId, input.CreditNumber!, null, ct);
        await CreditValidateRegistered(db, invoice, null, input.Amount, ct);
        var row = new SupplierCreditNoteRef { SupplierId = invoice.SupplierId, SupplierInvoiceId = invoice.Id,
            CurrencyCode = invoice.CurrencyCode, CreditNumber = input.CreditNumber!.Trim().ToUpperInvariant(),
            Amount = input.Amount, Reason = input.Reason!.Trim(), Status = "Draft" };
        db.AddScoped(row);
        ProcurementDraftAudit.Add(db, scope, nameof(SupplierCreditNoteRef), row.Id, "CreditIntakeCaptured", null,
            new { credit = row, rawCreditNumber = input.CreditNumber, resultingVersion = 1 });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Created($"/api/procurement/v1/vendor-credits/{row.Id}", await CreditView(db, scope, http, authorization, row, ct));
    }
    private static async Task<IResult> CreditUpdate(Guid id, HttpRequest request, ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
    {
        var input = await CreditBody<CreditUpdateInput>(request, ct);
        if (input.Draft is null || input.Version <= 0 || !ValidReason(input.Reason)) return Invalid("Supply the current version, credit draft and a correction reason of up to 1,000 characters.");
        CreditValidate(input.Draft.CreditNumber, input.Draft.Amount, input.Draft.Reason);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var visible = await CreditVisible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (visible is null || !CreditMaker(visible, scope, http)) return CreditMissing();
        await CreditLockInvoice(db, scope, visible.SupplierInvoiceId!.Value, ct);
        var row = await db.SupplierCreditNoteRefs.SingleAsync(x => x.Id == id, ct);
        CreditRequireEditable(row, input.Version);
        var invoice = await CreditInvoice(db, scope, row.SupplierInvoiceId!.Value, ct);
        await CreditNumberAvailable(db, row.SupplierId, input.Draft.CreditNumber!, id, ct);
        await CreditValidateRegistered(db, invoice, id, input.Draft.Amount, ct);
        var before = ProcurementDraftAudit.Snapshot(row);
        row.CreditNumber = input.Draft.CreditNumber!.Trim().ToUpperInvariant(); row.Amount = input.Draft.Amount; row.Reason = input.Draft.Reason!.Trim();
        db.Entry(row).Property(x => x.UpdatedAt).IsModified = true;
        ProcurementDraftAudit.Add(db, scope, nameof(SupplierCreditNoteRef), id, "CreditIntakeCorrected", before,
            new { credit = row, rawCreditNumber = input.Draft.CreditNumber, reason = input.Reason!.Trim(), resultingVersion = checked(row.Version + 1) });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(await CreditView(db, scope, http, authorization, row, ct));
    }
    private static async Task<IResult> CreditCancel(Guid id, CreditCancelInput input, ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization, CancellationToken ct)
    {
        if (input.Version <= 0 || !ValidReason(input.Reason)) return Invalid("Supply the current version and a cancellation reason of up to 1,000 characters.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var visible = await CreditVisible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (visible is null || !CreditMaker(visible, scope, http)) return CreditMissing();
        await CreditLockInvoice(db, scope, visible.SupplierInvoiceId!.Value, ct);
        var row = await db.SupplierCreditNoteRefs.SingleAsync(x => x.Id == id, ct); CreditRequireEditable(row, input.Version);
        var before = ProcurementDraftAudit.Snapshot(row); row.Status = "Cancelled";
        ProcurementDraftAudit.Add(db, scope, nameof(SupplierCreditNoteRef), id, "CreditIntakeCancelled", before,
            new { credit = row, reason = input.Reason!.Trim(), resultingVersion = checked(row.Version + 1) });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(await CreditView(db, scope, http, authorization, row, ct));
    }
    private static async Task<T> CreditBody<T>(HttpRequest request, CancellationToken ct) where T : class
    {
        if (!request.HasJsonContentType()) throw new IntakeProblem("unsupported-media-type", "Use application/json.", 415);
        try { return await request.ReadFromJsonAsync<T>(ct) ?? throw new JsonException(); }
        catch (Exception error) when (error is JsonException or BadHttpRequestException)
        { throw new IntakeProblem("validation-failed", "Enter valid credit details and exact amounts with up to four decimal places.", 422); }
    }
    private static void CreditValidate(string? number, decimal amount, string? reason)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 80 || number.Trim().ToUpperInvariant().Length > 80
            || !ValidReason(reason) || amount <= 0 || !ExactDecimalJsonCodec.IsRepresentable(amount, 18, 4))
            throw new IntakeProblem("validation-failed", "Enter a credit number up to 80 characters, a positive exact amount up to four decimal places, and a reason up to 1,000 characters.", 422);
    }
    private static void CreditRequireEditable(SupplierCreditNoteRef row, long version)
    {
        if (row.Version != version) throw new IntakeProblem("stale-credit", "This reference changed. Refresh it before trying again.", 409);
        if (!CreditEditable(row)) throw new IntakeProblem("credit-locked", "Only a manual Draft without Finance or return linkage can be corrected or cancelled.", 409);
    }
    private static async Task<SupplierInvoice> CreditInvoice(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
        => await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new IntakeProblem("not-found", "Bill not found.", 404);
    private static async Task CreditLockInvoice(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
    {
        if (db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
    }
    private static async Task CreditNumberAvailable(ProcurementDbContext db, Guid supplierId, string number, Guid? currentId, CancellationToken ct)
    {
        var canonical = number.Trim().ToUpperInvariant();
        if (await db.SupplierCreditNoteRefs.AnyAsync(x => x.SupplierId == supplierId && (currentId == null || x.Id != currentId)
            && x.CreditNumber.Trim().ToUpper() == canonical, ct))
            throw new IntakeProblem("duplicate-credit", "This supplier credit number is already recorded.", 409);
    }
    private static async Task CreditValidateRegistered(ProcurementDbContext db, SupplierInvoice invoice, Guid? currentId, decimal amount, CancellationToken ct)
    {
        if (invoice.GrossAmount <= 0 || invoice.Status == "Cancelled" || !ExactDecimalJsonCodec.IsRepresentable(invoice.GrossAmount, 18, 4))
            throw new IntakeProblem("credit-source-unavailable", "The bill's captured total needs review before another credit reference.", 409);
        var all = db.SupplierCreditNoteRefs.Where(x => x.SupplierInvoiceId == invoice.Id);
        if (await all.AnyAsync(x => x.SupplierId != invoice.SupplierId || x.CurrencyCode != invoice.CurrencyCode
            || x.SupplierReturnId != null || x.Amount <= 0
            || x.Status != "Draft" && x.Status != "Received" && x.Status != "Cancelled"
            || x.Status == "Cancelled" && x.FinanceCreditRef != null, ct))
            throw new IntakeProblem("credit-history-needs-review", "Existing registered credit history needs review; no new amount was assumed.", 409);
        decimal total;
        try { total = await all.Where(x => x.Status != "Cancelled" && (currentId == null || x.Id != currentId)).SumAsync(x => x.Amount, ct); }
        catch (OverflowException)
        { throw new IntakeProblem("credit-history-needs-review", "The registered credit total cannot be represented exactly.", 409); }
        try { total = ExactDecimalArithmetic.Add(total, amount); }
        catch (InvalidOperationException)
        { throw new IntakeProblem("credit-history-needs-review", "The registered credit total cannot be represented exactly.", 409); }
        if (total > invoice.GrossAmount) throw new IntakeProblem("credit-captured-total-exceeded", "Registered credit references would exceed this bill's captured gross amount. This is not a Finance balance.", 422);
    }
    private static IResult CreditMissing() => Results.NotFound(new { code = "not-found", message = "Credit reference not found." });
    private static IResult CreditStale() => Results.Conflict(new { code = "stale-credit", message = "This reference changed. Refresh it before trying again." });
    private static IResult CreditDuplicate() => Results.Conflict(new { code = "duplicate-credit", message = "This supplier credit number is already recorded." });
    private static bool IsCreditDuplicate(DbUpdateException error, ProcurementDbContext db)
        => error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "supplier_credit_note_refs" } pg
            && db.Model.FindEntityType(typeof(SupplierCreditNoteRef))!.GetIndexes().Any(index => index.IsUnique
                && index.Properties.Any(x => x.Name == nameof(SupplierCreditNoteRef.CreditNumber)) && index.GetDatabaseName() == pg.ConstraintName);
}
