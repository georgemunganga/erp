using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Schema-2 source inspection only. Deliberately unregistered and
/// disabled until genuine P30/P31 owner schema/finite-role checks and a separate
/// typed invoice mapping/claim path have passed. No POST and no Finance claims.</summary>
public static class ProcurementPrincipalInvoiceSourceRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed class Problem(string code, int status) : Exception(code) { internal int Status => status; }

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoices");
        group.MapGet("/{id:guid}/principal-matching/sources", Sources)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
        group.MapGet("/{id:guid}/principal-matching/sources/{orderId:guid}/lines/{lineId:guid}/acceptances", Acceptances)
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
    }

    internal static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<object>> read,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var option = http.RequestServices.GetService<ProcurementPrincipalInvoiceSourceOptions>();
            var orders = http.RequestServices.GetService<IProcurementPrincipalOrderDraftReadiness>();
            var receiving = http.RequestServices.GetService<IProcurementPrincipalReceivingReadiness>();
            if (option is not { Enabled: true } || orders is null || receiving is null
                || !await orders.IsReadyAsync(ct))
                throw new Problem("PrincipalInvoiceSourceUnavailable", 503);
            var value = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>()
                .ExecuteAsync(async (db, cases, token) =>
                {
                    if (!await orders.IsReadyAsync(db, false, token)
                        || !await receiving.IsReadyAsync(db, false, token))
                        throw new Problem("PrincipalInvoiceSourceUnavailable", 503);
                    await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                    return await read(db, cases, core,
                        http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
                }, ct);
            return Results.Content(JsonSerializer.Serialize(value, Json), "application/json");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is Problem or InvalidOperationException or UnauthorizedAccessException
            or KeyNotFoundException or ArgumentException or JsonException or DbUpdateException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem p ? p.Status : error is UnauthorizedAccessException ? 403
                : error is KeyNotFoundException ? 404
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503 : 409;
            return Results.Json(new { code = error is Problem ? error.Message : status == 503
                ? "PrincipalInvoiceSourceUnavailable" : "PrincipalInvoiceSourceNeedsReview",
                message = "Refresh this invoice and review its current access and source evidence. No comparison or payment was created." },
                statusCode: status);
        }
    }

    internal static async Task<(SupplierInvoice Invoice, HashSet<Guid> Branches)> Current(
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, Guid invoiceId, CancellationToken ct)
    {
        if (scope.PrincipalId?.ToString("D") != scope.SubjectId || scope.OrgUnitId is not null
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException();
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || person.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
            || !person.Permissions.Contains("procurement-invoice-read")
            || !person.Permissions.Contains("procurement-invoice-match")
            || !(await auth.AuthorizeAsync(http.User, "procurement-invoice-read")).Succeeded
            || !(await auth.AuthorizeAsync(http.User, "procurement-invoice-match")).Succeeded)
            throw new UnauthorizedAccessException();
        var allowed = person.AllowedBranches.ToArray();
        var branches = await core.WorkLocations.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && !x.IsArchived && (person.OrganisationWide || allowed.Contains(x.Id))
                && (scope.WorkLocationId == null || scope.WorkLocationId == x.Id))
            .Select(x => x.Id).ToArrayAsync(ct);
        if (branches.Length == 0) throw new UnauthorizedAccessException();
        var invoice = await ProcurementInvoiceIntakeRoutes.ReadableInvoices(db, scope)
            .SingleOrDefaultAsync(x => x.Id == invoiceId && x.BranchRef != null
                && branches.Contains(x.BranchRef.Value), ct)
            ?? throw new KeyNotFoundException();
        if (invoice.Status is not ("Received" or "MatchPending" or "Exception")
            || invoice.BranchRef is not { } branch || !branches.Contains(branch))
            throw new Problem("PrincipalInvoiceSourceUnavailable", 409);
        _ = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, ct);
        return (invoice, branches.ToHashSet());
    }

    private static (int Page, int Size, string? Pattern) Page(int? page, int? size, string? q)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 25 || q?.Length > 120
            || q?.Any(char.IsControl) == true) throw new Problem("PrincipalInvoiceSourceQueryInvalid", 422);
        var value = q?.Trim();
        return (page ?? 1, size ?? 25, string.IsNullOrEmpty(value) ? null : "%"
            + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
    }

    private static Task<IResult> Sources(Guid id, int? page, int? pageSize, string? q,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var paging = Page(page, pageSize, q);
            var (invoice, branches) = await Current(db, core, scope, http, auth, id, token);
            var bound = ProcurementPrincipalInvoiceSourceBridge.QueryBound(db, scope, branches, invoice);
            var query = from order in bound
                join issued in db.PrincipalOrderIssuedVersions.AsNoTracking()
                    on new { order.Id, Revision = order.CurrentRevision }
                    equals new { Id = issued.PurchaseOrderId, Revision = issued.CommercialRevision }
                join line in db.PurchaseOrderLines.AsNoTracking() on order.Id equals line.PurchaseOrderId
                where line.IsActive && (paging.Pattern == null
                    || EF.Functions.ILike(order.Number, paging.Pattern, "\\")
                    || EF.Functions.ILike(line.Description, paging.Pattern, "\\"))
                orderby order.Number, order.Id, line.LineNumber, line.Id
                select new { orderId = order.Id, orderNumber = order.Number,
                    issuedVersionId = issued.Id, lineId = line.Id, line.Description };
            var total = await query.CountAsync(token);
            var rows = await query.Skip((paging.Page - 1) * paging.Size).Take(paging.Size).ToArrayAsync(token);
            var verified = new Dictionary<(Guid, Guid), ProcurementPrincipalInvoiceSourceBridge.Verified>();
            var items = new List<object>();
            foreach (var row in rows)
            {
                var key = (row.orderId, row.issuedVersionId);
                if (!verified.TryGetValue(key, out var source))
                {
                    source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                        branches, invoice, row.orderId, row.issuedVersionId, token);
                    verified.Add(key, source);
                }
                var line = source.Order.Lines.Single(x => x.Id == row.lineId);
                items.Add(new { row.orderId, row.orderNumber, row.issuedVersionId,
                    purchaseOrderLineId = row.lineId, row.Description, line.UnitCode,
                    quantity = Exact(line.Quantity), unitPrice = Exact(line.UnitPrice),
                    taxAmount = Exact(line.TaxAmount), line.CategoryCode,
                    sourceSchemaVersion = 2, sourceState = "VerifiedIssuedAndAcceptanceHistory",
                    matchingAuthorityAvailable = false, canAllocate = false, canPostToFinance = false });
            }
            return new { invoiceId = id, invoiceVersion = invoice.Version, items, total,
                page = paging.Page, pageSize = paging.Size, sourceSchemaVersion = 2,
                isAuthority = false, matchingAuthorityAvailable = false, canPostToFinance = false };
        }, ct);

    private static Task<IResult> Acceptances(Guid id, Guid orderId, Guid lineId,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var (invoice, branches) = await Current(db, core, scope, http, auth, id, token);
            var issued = await (from order in ProcurementPrincipalInvoiceSourceBridge.QueryBound(db, scope, branches, invoice)
                join version in db.PrincipalOrderIssuedVersions.AsNoTracking()
                    on new { order.Id, Revision = order.CurrentRevision }
                    equals new { Id = version.PurchaseOrderId, Revision = version.CommercialRevision }
                where order.Id == orderId
                select version.Id).SingleOrDefaultAsync(token);
            if (issued == Guid.Empty) throw new KeyNotFoundException();
            var source = await ProcurementPrincipalInvoiceSourceBridge.LoadAsync(db, cases, scope,
                branches, invoice, orderId, issued, token);
            var line = source.Order.Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new KeyNotFoundException();
            var facts = source.Acceptances.Where(x => x.PurchaseOrderLineId == lineId)
                .OrderBy(x => x.AcceptedAt).ThenBy(x => x.Id).ToArray();
            return new { invoiceId = id, invoiceVersion = invoice.Version, orderId,
                issuedVersionId = issued, purchaseOrderLineId = lineId, sourceSchemaVersion = 2,
                line.CategoryCode, line.UnitCode, acceptedFacts = facts.Select(x => new
                {
                    principalAcceptanceFactId = x.Id,
                    principalReceivingSourceStateId = source.AcceptanceSourceStates[x.Id],
                    sourceSequence = x.Version, x.Kind, x.AcceptedAt,
                    acceptedQuantity = Exact(x.AcceptedQuantity), acceptedNetAmount = Exact(x.AcceptedNetAmount),
                    x.AcceptanceReference
                }).ToArray(), total = facts.Length, historyState = "VerifiedHistoricalPositiveOnly",
                matchingAuthorityAvailable = false, canAllocate = false, canPostToFinance = false };
        }, ct);

    private static string Exact(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}

public sealed class ProcurementPrincipalInvoiceSourceOptions
{
    public bool Enabled { get; set; }
}
