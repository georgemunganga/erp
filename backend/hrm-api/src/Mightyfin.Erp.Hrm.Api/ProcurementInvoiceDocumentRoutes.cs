using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record CoreDocumentFeature(bool Enabled);
public interface IDocumentSchemaReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class DocumentSchemaReadiness(DocumentDbContext db) : IDocumentSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try { return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return false; }
    }
}

public sealed class ProcurementInvoiceDocumentAccess(ProcurementDbContext db, ProcurementRequestScope scope,
    IHttpContextAccessor http, IAuthorizationService authorization) : IDocumentOwnerAccess
{
    public async Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct)
    {
        if (owner.Module != "procurement" || owner.Type != "supplier_invoice")
            throw new DocumentProblemException("document-not-found", "Document record not found.");
        var principal = http.HttpContext?.User ?? throw new UnauthorizedAccessException();
        var permission = action == DocumentAction.Upload ? "procurement-invoice-capture" : "procurement-invoice-read";
        if (!(await authorization.AuthorizeAsync(principal, null, permission)).Succeeded) throw new UnauthorizedAccessException();
        var invoice = await db.SupplierInvoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == owner.Id
            && (!scope.IsConfined || x.BranchRef == scope.WorkLocationId), ct);
        if (invoice is null) throw new DocumentProblemException("document-not-found", "Invoice not found.");
        if (action == DocumentAction.Upload && invoice.Status != "Received")
            throw new DocumentProblemException("invoice-evidence-locked", "This invoice is no longer open for new documents.");
    }
}

public static class ProcurementInvoiceDocumentRoutes
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("CoreDocuments:Enabled");
        services.AddSingleton(new CoreDocumentFeature(enabled));
        if (!enabled) return;
        var connection = configuration.GetConnectionString("CoreDocuments")
            ?? throw new InvalidOperationException("Core document metadata connection is required.");
        var directory = configuration["CoreDocuments:PrivateDirectory"]
            ?? throw new InvalidOperationException("Core document private storage directory is required.");
        var socket = configuration["CoreDocuments:ScannerSocket"]
            ?? throw new InvalidOperationException("Core document scanner socket is required.");
        services.AddHttpContextAccessor();
        services.AddScoped<IDocumentScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        services.AddDbContext<DocumentDbContext>(o => o.UseNpgsql(connection,
            p => p.MigrationsHistoryTable("__EFMigrationsHistory", "core_document")));
        var limits = new DocumentLimits(configuration.GetValue<long?>("CoreDocuments:MaxFileBytes") ?? 10 * 1024 * 1024,
            configuration.GetValue<int?>("CoreDocuments:MaxOwnerFiles") ?? 10);
        if (limits.MaxFileBytes is < 1 or > 100 * 1024 * 1024 || limits.MaxOwnerFiles is < 1 or > 100)
            throw new InvalidOperationException("Core document limits are outside the supported bounds.");
        services.AddSingleton<IDocumentBlobStore>(new PrivateDocumentStore(directory));
        services.AddSingleton<IDocumentScanner>(new ClamdDocumentScanner(new(socket, TimeZoneInfo.Utc,
            TimeSpan.FromSeconds(30), TimeSpan.FromHours(48))));
        services.AddSingleton(limits);
        services.AddScoped<IDocumentRecords, EfDocumentRecords>();
        services.AddScoped<IDocumentOwnerAccess, ProcurementInvoiceDocumentAccess>();
        services.AddScoped<IDocumentSchemaReadiness, DocumentSchemaReadiness>();
        services.AddScoped<RecordDocumentService>();
    }

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoices/{id:guid}/documents");
        group.AddEndpointFilter(async (context, next) =>
            await context.HttpContext.RequestServices.GetRequiredService<IDocumentSchemaReadiness>()
                .IsReadyAsync(context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.Problem(statusCode: 503, title: "Document storage is not ready. Try again later."));
        group.MapGet("", (Guid id, RecordDocumentService service, DocumentLimits limits, CancellationToken ct) =>
            Run(async () => Results.Ok(new { items = await service.ListAsync(Owner(id), ct),
                limits = new { limits.MaxFileBytes, limits.MaxOwnerFiles } }), ct))
            .RequireAuthorization("procurement-invoice-read");
        group.MapPost("", Upload).RequireAuthorization("procurement-invoice-capture");
        group.MapPost("/{versionId:guid}/retry-scan", (Guid id, Guid versionId, RecordDocumentService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.RetryScanAsync(Owner(id), versionId, ct)), ct))
            .RequireAuthorization("procurement-invoice-capture");
        group.MapGet("/{versionId:guid}/download", (Guid id, Guid versionId, HttpContext http, RecordDocumentService service, CancellationToken ct) =>
            Run(async () =>
            {
                var file = await service.DownloadAsync(Owner(id), versionId, ct);
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(file.Content, file.MediaType, file.FileName, enableRangeProcessing: false);
            }, ct)).RequireAuthorization("procurement-invoice-read");
    }

    private static Task<IResult> Upload(Guid id, HttpContext http, RecordDocumentService service,
        DocumentLimits limits, ProcurementDbContext db, CancellationToken ct) => Run(async () =>
    {
        if (http.Request.ContentType?.Split(';')[0].Trim() != "application/octet-stream")
            return Results.Problem(statusCode: 415, title: "Upload the document as a file.");
        if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
            return Results.BadRequest(new { code = "upload-key-required", message = "Choose the file again before uploading." });
        var encoded = http.Request.Headers["X-Document-Name"].ToString();
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 2048)
            return Results.BadRequest(new { code = "file-name-required", message = "Choose a file with a valid name." });
        if (http.Request.ContentLength > limits.MaxFileBytes) return Results.Problem(statusCode: 413, title: "The file is too large.");
        var bodyLimit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = limits.MaxFileBytes;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var saved = await service.UploadAsync(Owner(id), key, Uri.UnescapeDataString(encoded), http.Request.Body, deadline.Token);
        // Procurement keeps a business reference; Core remains authoritative for content and scan state.
        var documentKey = saved.DocumentId.ToString("D");
        if (!await db.AttachmentRefs.AnyAsync(x => x.OwnerType == "SupplierInvoice" && x.OwnerId == id
            && x.DocumentServiceId == documentKey && x.DocumentVersion == saved.VersionNumber, ct))
        {
            db.AddScoped(new ProcurementAttachmentRef
            {
                OwnerType = "SupplierInvoice", OwnerId = id, DocumentServiceId = documentKey,
                DocumentVersion = saved.VersionNumber, FileName = saved.FileName, Sha256 = saved.Sha256,
                Classification = "confidential", ScanStatus = "CoreOwned",
            });
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(saved);
    }, ct);

    private static DocumentOwner Owner(Guid id) => new("procurement", "supplier_invoice", id);
    private static async Task<IResult> Run(Func<Task<IResult>> action, CancellationToken ct)
    {
        try { return await action(); }
        catch (DocumentProblemException ex)
        {
            var status = ex.Code == "document-not-found" ? 404 : ex.Code is "document-limit" or "upload-key-required" ? 422
                : ex.Code == "document-content-changed" ? 503 : 409;
            return Results.Json(new { code = ex.Code, message = ex.Message }, statusCode: status);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (InvalidDataException ex) { return Results.UnprocessableEntity(new { code = "invalid-file", message = ex.Message }); }
        catch (ArgumentException ex) { return Results.UnprocessableEntity(new { code = "invalid-document", message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { code = "document-changed", message = "This file is being checked. Refresh its status." }); }
        catch (Exception ex) when (ex is IOException or DbUpdateException or DbException)
        { return Results.Problem(statusCode: 503, title: "The document action was not confirmed. Retry with the same file."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Results.Problem(statusCode: 408, title: "The upload took too long. Retry with the same file."); }
    }
}
