using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Npgsql;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Item stewardship, independent activation, and unpublished catalog price drafts.</summary>
public static class ProcurementItemEndpoints
{
    public static void MapRoutes(WebApplication app)
    {
        var items = app.MapGroup("/api/procurement/v1/items");
        items.AddEndpointFilter(ProcurementCatalogFencing.Write);
        items.MapGet("", List).RequireAuthorization("procurement-item-manage");
        items.MapGet("/export", Export).RequireAuthorization("procurement-item-manage")
            .RequireAuthorization("procurement-item-export");
        items.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-item-manage");
        items.MapPost("", Create).Accepts<ItemInput>("application/json").RequireAuthorization("procurement-item-manage");
        items.MapPut("/{id:guid}", Update).Accepts<ItemUpdate>("application/json").RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/activate", Activate).RequireAuthorization("procurement-item-approve");
        items.MapPost("/{id:guid}/deactivate", Deactivate).RequireAuthorization("procurement-item-manage");
        items.MapGet("/{id:guid}/catalog-entries", ListEntries).RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/catalog-entries", AddEntry).Accepts<EntryInput>("application/json").RequireAuthorization("procurement-item-manage");
        items.MapPut("/{id:guid}/catalog-entries/{entryId:guid}", UpdateEntry).Accepts<EntryUpdate>("application/json").RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/catalog-entries/{entryId:guid}/publish", PublishEntry).RequireAuthorization("procurement-catalog-approve");
        items.MapPost("/{id:guid}/catalog-entries/{entryId:guid}/withdraw", WithdrawEntry).RequireAuthorization("procurement-item-manage");
        app.MapGet("/api/procurement/v1/catalog", Catalog).RequireAuthorization("procurement-item-read");
    }

    private static async Task<IResult> List(ProcurementDbContext db, string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = ListQuery(db, search, status);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((number - 1) * size).Take(size).Select(x => new ItemView(
                x.Id, x.Code, x.Name, x.Kind, x.CategoryCode, x.UnitCode, x.Description, x.Status,
                x.IsPurchasable, x.IsStockItem, x.IsAsset, x.Brand, x.LeadTimeDays, x.Version))
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static IQueryable<CatalogItem> ListQuery(ProcurementDbContext db, string? search, string? status)
    {
        var query = db.CatalogItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%") || EF.Functions.ILike(x.Name, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        return query;
    }

    private static async Task<IResult> Export(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, ILoggerFactory loggerFactory, string? search, string? status, CancellationToken ct)
    {
        if (!ProcurementHumanCsvExport.ValidFilters(http.Request)
            || !ProcurementHumanCsvExport.TrySelectedIds(http.Request, out var selected))
            return Results.BadRequest(new { code = "invalid-export-filter" });
        var logger = loggerFactory.CreateLogger("Procurement.ItemCsvExport");
        try
        {
            if (!await ProcurementHumanCsvExport.NativeReaderReadyAsync(db, ct))
                return ProcurementHumanCsvExport.Unavailable(http, logger, "export-reader-unavailable");
            var query = ListQuery(db, search, status);
            if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
            var rows = await query
                .OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id)
                .Select(x => new { x.Code, x.Name, x.Kind, x.CategoryCode, x.UnitCode,
                    x.Status, x.IsPurchasable, x.IsStockItem, x.IsAsset })
                .Take(ProcurementHumanCsvExport.MaxRows + 1).ToListAsync(ct);
            if (ProcurementHumanCsvExport.ExceedsLimit(rows.Count))
                return Results.UnprocessableEntity(new { code = "export-limit-exceeded" });
            if (selected is not null && rows.Count != selected.Length)
                return Results.NotFound(new { code = "selected-records-unavailable" });
            var data = ProcurementHumanCsvExport.Encode(
                ["Item Code", "Name", "Kind", "Category Code", "Unit Code", "Status", "Purchasable", "Stock Item", "Asset"],
                rows.Select(x => new[] { x.Code, x.Name, x.Kind, x.CategoryCode, x.UnitCode,
                    x.Status, x.IsPurchasable ? "true" : "false", x.IsStockItem ? "true" : "false",
                    x.IsAsset ? "true" : "false" }));
            return await ProcurementHumanCsvExport.CompleteAsync(db, scope, http, "CatalogItemCsvExport",
                ProcurementHumanCsvExport.NormalizedFilter(search, status, selected),
                rows.Count, data, $"items-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is DbException or DbUpdateException or TimeoutException)
        {
            return ProcurementHumanCsvExport.Unavailable(http, logger, "export-storage-unavailable");
        }
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        var item = await db.CatalogItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Results.Ok(ToDetail(item));
    }

    private static async Task<IResult> Catalog(ProcurementDbContext db, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // Until audience/site/contract resolution is implemented, only unrestricted, supplier-backed entries are exposed.
        var query = from entry in db.CatalogEntries.AsNoTracking()
                    join item in db.CatalogItems.AsNoTracking() on entry.CatalogItemId equals item.Id
                    join supplier in db.Suppliers.AsNoTracking() on entry.SupplierId equals (Guid?)supplier.Id
                    where entry.Status == "Published" && item.Status == "Active" && item.IsPurchasable
                        && supplier.Status == "Active" && supplier.IsOrderable
                        && (supplier.EligibleFrom == null || supplier.EligibleFrom <= now)
                        && (supplier.EligibleUntil == null || supplier.EligibleUntil >= now)
                        && (supplier.PaymentVerificationToken == null || supplier.PaymentVerificationStatus == "Verified")
                        && !db.SupplierQualifications.AsNoTracking().Any(qualification =>
                            qualification.SupplierId == supplier.Id
                            && qualification.IsActive
                            && (qualification.ReviewStatus != "Verified" || qualification.ExpiresOn < today)
                            && !db.SupplierQualifications.AsNoTracking().Any(newer =>
                                newer.SupplierId == supplier.Id
                                && newer.IsActive
                                && newer.QualificationType.ToLower() == qualification.QualificationType.ToLower()
                                && newer.CreatedAt > qualification.CreatedAt))
                        && entry.EffectiveFrom <= now && (entry.EffectiveTo == null || entry.EffectiveTo > now)
                        && entry.AudienceCode == null && entry.SiteRef == null && entry.ContractRef == null
                    select new { entry, item, supplier };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.item.Name, $"%{term}%")
                || EF.Functions.ILike(x.item.Code, $"%{term}%"));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.item.Name).ThenBy(x => x.item.Code)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new CatalogView(x.entry.Id, x.item.Id, x.item.Code, x.item.Name,
                x.item.Kind, x.item.Description, x.item.CategoryCode, x.entry.UnitCode,
                x.entry.UnitPrice, x.entry.CurrencyCode, x.supplier.Id, x.supplier.DisplayName,
                x.entry.EffectiveTo, x.entry.PublishedVersion))
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Create(JsonElement body, ProcurementDbContext db, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<ItemInput>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var error = ValidateItem(input);
        if (error is not null) return Results.UnprocessableEntity(new { code="validation-failed",message=error });
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.CatalogItems.AnyAsync(x => x.Code == code, ct)) return Conflict("An item with this code already exists.");
        var item = ProcurementMasterDraftRules.NewItem(input);
        db.AddScoped(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("The item could not be saved. Check its code and references."); }
        return Results.Created($"/api/procurement/v1/items/{item.Id}", ToView(item));
    }

    private static async Task<IResult> Update(Guid id, JsonElement body, ProcurementDbContext db, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<ItemUpdate>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var error = ValidateItem(input.Item);
        if (error is not null) return Results.UnprocessableEntity(new { code="validation-failed",message=error });
        if (input.Version < 1) return BadRequest("A valid record version is required.");
        var item = await db.CatalogItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Version != input.Version) return Conflict("This item changed since you opened it. Refresh and try again.");
        if (item.Status != "Inactive") return Conflict("Only inactive items can be edited. Deactivate the item first.");
        var code = input.Item.Code.Trim().ToUpperInvariant();
        if (await db.CatalogItems.AnyAsync(x => x.Code == code && x.Id != id, ct)) return Conflict("An item with this code already exists.");
        item.Code = code;
        Apply(input.Item, item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This item changed since you opened it. Refresh and try again."); }
        catch (DbUpdateException) { return Conflict("The item could not be saved. Check its code and references."); }
        return Results.Ok(ToView(item));
    }

    private static async Task<IResult> Deactivate(Guid id, VersionInput input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (input.Version < 1) return BadRequest("A valid record version is required.");
        var item = await db.CatalogItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Version != input.Version) return Conflict("This item changed since you opened it. Refresh and try again.");
        if (item.Status == "Inactive") return Results.Ok(ToView(item));
        item.Status = "Inactive";
        item.IsPurchasable = false;
        // Draft prices remain for editing; published prices are withdrawn from future selection.
        var published = await db.CatalogEntries.Where(x => x.CatalogItemId == id && x.Status == "Published").ToListAsync(ct);
        foreach (var entry in published) entry.Status = "Withdrawn";
        db.AddScoped(new AuditEvent
        {
            RecordType = "CatalogItem", RecordId = item.Id, Action = "Deactivated",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { from = "Active", to = "Inactive",
                withdrawnPrices = published.Count, item.Code, item.Version }),
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This item changed since you opened it. Refresh and try again."); }
        return Results.Ok(ToView(item));
    }

    private static async Task<IResult> Activate(Guid id, ItemActivationInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var item = await db.CatalogItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Version != input.Version) return Conflict("This item changed. Refresh before activating it.");
        if (item.Status != "Inactive") return Conflict("Only an inactive item can be activated.");
        if (item.CreatedBy == scope.SubjectId)
            return Results.Json(new { code = "self-approval", message = "Another authorized person must activate this item." }, statusCode: 403);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return BadRequest("Enter a review reason of up to 1,000 characters.");
        if (!await db.ProcurementCategories.AsNoTracking()
                .AnyAsync(x => x.Code == item.CategoryCode && x.Status == "Active", ct))
            return Conflict("Activate the item's category before making it available for buying.");
        item.Status = "Active";
        item.IsPurchasable = true;
        db.AddScoped(new AuditEvent
        {
            RecordType = "CatalogItem", RecordId = item.Id, Action = "Activated",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { from = "Inactive", to = "Active",
                reason = input.Reason.Trim(), item.Code, item.Version }),
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This item changed. Refresh before activating it."); }
        return Results.Ok(ToView(item));
    }

    private static async Task<IResult> ListEntries(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        if (!await db.CatalogItems.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var entries = await db.CatalogEntries.AsNoTracking().Where(x => x.CatalogItemId == id)
            .OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct);
        return Results.Ok(new { items = entries.Select(ToView) });
    }

    private static async Task<IResult> AddEntry(Guid id, JsonElement body, ProcurementDbContext db, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<EntryInput>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var error = ValidateEntry(input);
        if (error is not null) return Results.UnprocessableEntity(new { code="validation-failed",message=error });
        var item = await db.CatalogItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (input.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == input.SupplierId.Value, ct))
            return BadRequest("Choose a supplier from this company.");
        var entry = new CatalogEntry { CatalogItemId = id, Status = "Draft", PublishedVersion = 0 };
        Apply(input, entry);
        db.AddScoped(entry);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("The catalog price could not be saved. Check its references."); }
        return Results.Created($"/api/procurement/v1/items/{id}/catalog-entries/{entry.Id}", ToView(entry));
    }

    private static async Task<IResult> UpdateEntry(Guid id, Guid entryId, JsonElement body, ProcurementDbContext db, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<EntryUpdate>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var error = ValidateEntry(input.Entry);
        if (error is not null) return Results.UnprocessableEntity(new { code="validation-failed",message=error });
        if (input.Version < 1) return BadRequest("A valid record version is required.");
        var entry = await db.CatalogEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.CatalogItemId == id, ct);
        if (entry is null) return NotFound();
        if (entry.Version != input.Version) return Conflict("This catalog price changed. Refresh and try again.");
        if (entry.Status != "Draft") return Conflict("Only draft catalog prices can be edited.");
        if (input.Entry.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == input.Entry.SupplierId.Value, ct))
            return BadRequest("Choose a supplier from this company.");
        Apply(input.Entry, entry);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This catalog price changed. Refresh and try again."); }
        catch (DbUpdateException) { return Conflict("The catalog price could not be saved. Check its references."); }
        return Results.Ok(ToView(entry));
    }

    private static async Task<IResult> WithdrawEntry(Guid id, Guid entryId, CatalogWithdrawInput input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (input.Version < 1) return BadRequest("A valid record version is required.");
        var entry = await db.CatalogEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.CatalogItemId == id, ct);
        if (entry is null) return NotFound();
        if (entry.Version != input.Version) return Conflict("This catalog price changed. Refresh and try again.");
        if (entry.Status == "Withdrawn") return Results.Ok(ToView(entry));
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return BadRequest("Enter a withdrawal reason of up to 1,000 characters.");
        var oldStatus = entry.Status;
        entry.Status = "Withdrawn";
        db.AddScoped(new AuditEvent
        {
            RecordType = "CatalogEntry", RecordId = entry.Id, Action = "Withdrawn",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { from = oldStatus, to = "Withdrawn", reason = input.Reason.Trim() }),
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This catalog price changed. Refresh and try again."); }
        return Results.Ok(ToView(entry));
    }

    private static async Task<IResult> PublishEntry(Guid id, Guid entryId, CatalogPublishInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        // Serializable predicate reads prevent two reviewers from publishing overlapping prices at once.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var entry = await db.CatalogEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.CatalogItemId == id, ct);
        if (entry is null) return NotFound();
        if (entry.Version != input.Version) return Conflict("This catalog price changed. Refresh before approving it.");
        if (entry.Status != "Draft") return Conflict("Only a draft catalog price can be published.");
        if (entry.CreatedBy == scope.SubjectId)
            return Results.Json(new { code = "self-approval", message = "Another authorized person must publish this price." }, statusCode: 403);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return BadRequest("Enter a review reason of up to 1,000 characters.");
        var item = await db.CatalogItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || item.Status != "Active" || !item.IsPurchasable)
            return Conflict("Activate the item before publishing a catalog price.");
        if (entry.UnitCode != item.UnitCode && entry.UnitCode != item.PurchaseUnitCode)
            return BadRequest("Use the item's unit or approved purchase unit for the catalog price.");
        if (entry.SupplierId is not Guid supplierId)
            return BadRequest("Choose an approved vendor for the catalog price.");
        var now = DateTimeOffset.UtcNow;
        if (entry.EffectiveTo is { } end && end <= now)
            return BadRequest("The catalog price has already expired.");
        var supplier = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
            .SingleOrDefaultAsync(x => x.Id == supplierId, ct);
        if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, now))
            return Conflict("The vendor must be active with current qualifications before a catalog price can be published.");
        if (await db.CatalogEntries.AsNoTracking().AnyAsync(x => x.Id != entryId
            && x.CatalogItemId == id && x.SupplierId == supplierId && x.Status == "Published"
            && x.CurrencyCode == entry.CurrencyCode && x.UnitCode == entry.UnitCode
            && x.AudienceCode == entry.AudienceCode && x.SiteRef == entry.SiteRef
            && x.ContractRef == entry.ContractRef
            && (x.EffectiveTo == null || x.EffectiveTo > entry.EffectiveFrom)
            && (entry.EffectiveTo == null || x.EffectiveFrom < entry.EffectiveTo), ct))
            return Conflict("This vendor already has an overlapping published price for the item.");
        entry.Status = "Published";
        entry.PublishedVersion++;
        db.AddScoped(new AuditEvent
        {
            RecordType = "CatalogEntry", RecordId = entry.Id, Action = "Published",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { from = "Draft", to = "Published",
                reason = input.Reason.Trim(), entry.CatalogItemId, entry.SupplierId,
                entry.UnitPrice, entry.CurrencyCode, entry.PublishedVersion }),
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict("This catalog price changed. Refresh before approving it."); }
        catch (DbUpdateException) { return Conflict("The catalog price changed during review. Refresh and try again."); }
        catch (PostgresException error) when (error.SqlState is "40001" or "40P01")
        { return Conflict("Another catalog price was published during review. Refresh and try again."); }
        return Results.Ok(ToView(entry));
    }

    private static string? ValidateItem(ItemInput? x) => ProcurementMasterDraftRules.ValidateItem(x);

    private static string? ValidateEntry(EntryInput x)
    {
        if (string.IsNullOrWhiteSpace(x.CurrencyCode) || x.CurrencyCode.Trim().Length != 3) return "Choose a three-letter currency code.";
        if (string.IsNullOrWhiteSpace(x.UnitCode) || x.UnitCode.Trim().Length > 30) return "Choose a unit.";
        if(!ExactDecimalJsonCodec.IsRepresentable(x.UnitPrice,18,4)
            || x.MinimumQuantity.HasValue && !ExactDecimalJsonCodec.IsRepresentable(x.MinimumQuantity.Value,18,4)
            || x.MaximumQuantity.HasValue && !ExactDecimalJsonCodec.IsRepresentable(x.MaximumQuantity.Value,18,4))
            return "Price and quantities must fit 18 digits with up to four decimal places.";
        if (x.UnitPrice < 0 || x.MinimumQuantity is < 0 || x.MaximumQuantity is < 0) return "Price and quantities cannot be negative.";
        if (!ExactDecimalJsonCodec.IsRepresentable(x.UnitPrice, 18, 4)
            || x.MinimumQuantity is { } min && !ExactDecimalJsonCodec.IsRepresentable(min, 18, 4)
            || x.MaximumQuantity is { } max && !ExactDecimalJsonCodec.IsRepresentable(max, 18, 4))
            return "Price and quantities must fit 18 digits with up to four decimal places; nothing was rounded.";
        if (x.MinimumQuantity.HasValue && x.MaximumQuantity.HasValue && x.MinimumQuantity > x.MaximumQuantity) return "Maximum quantity must be at least the minimum.";
        if (x.EffectiveTo.HasValue && x.EffectiveTo.Value <= x.EffectiveFrom) return "The end date must be after the start date.";
        return null;
    }

    private static void Apply(ItemInput x, CatalogItem item) => ProcurementMasterDraftRules.ApplyItem(x, item);

    private static void Apply(EntryInput x, CatalogEntry entry)
    {
        entry.SupplierId = x.SupplierId; entry.CurrencyCode = x.CurrencyCode.Trim().ToUpperInvariant();
        entry.UnitCode = x.UnitCode.Trim(); entry.UnitPrice = x.UnitPrice; entry.TaxBasis = x.TaxBasis?.Trim();
        entry.MinimumQuantity = x.MinimumQuantity; entry.MaximumQuantity = x.MaximumQuantity;
        entry.EffectiveFrom = x.EffectiveFrom; entry.EffectiveTo = x.EffectiveTo;
        entry.AudienceCode = x.AudienceCode?.Trim();
    }

    private static ItemView ToView(CatalogItem x) => new(x.Id, x.Code, x.Name, x.Kind, x.CategoryCode,
        x.UnitCode, x.Description, x.Status, x.IsPurchasable, x.IsStockItem, x.IsAsset, x.Brand, x.LeadTimeDays, x.Version);
    private static object ToDetail(CatalogItem x) => new
    {
        x.Id, x.Code, x.Name, x.Kind, x.CategoryCode, x.UnitCode, x.Description,
        x.TaxCodeRef, x.PurchaseUnitCode, x.PurchaseUnitConversion,
        x.Brand, x.Manufacturer, x.ManufacturerPartNumber, x.LeadTimeDays,
        x.MinimumOrderQuantity, x.ExpenseAccountRef, x.IsStockItem, x.IsAsset,
        x.InspectionRequired, x.Status, x.IsPurchasable, x.Version,
    };
    private static EntryView ToView(CatalogEntry x) => new(x.Id, x.CatalogItemId, x.SupplierId, x.CurrencyCode,
        x.UnitCode, x.UnitPrice, x.EffectiveFrom, x.EffectiveTo, x.MinimumQuantity, x.MaximumQuantity, x.Status, x.Version,
        x.TaxBasis, x.AudienceCode, new CatalogEntryExactAmounts(EntryExact(x.UnitPrice),
            x.MinimumQuantity is { } min ? EntryExact(min) : null, x.MaximumQuantity is { } max ? EntryExact(max) : null), 1);
    private static string EntryExact(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static IResult BadRequest(string message) => Results.BadRequest(new { code = "invalid-item", message });
    private static IResult Conflict(string message) => Results.Conflict(new { code = "conflict", message });
    private static IResult NotFound() => Results.NotFound(new { code = "not-found", message = "Item not found." });
}

public sealed record ItemInput(string Code, string Name, string Kind, string CategoryCode, string UnitCode,
    string? Description, string? TaxCodeRef, string? PurchaseUnitCode, [property: JsonConverter(typeof(PurchaseUnitConversionJsonConverter))] decimal? PurchaseUnitConversion,
    string? Brand, string? Manufacturer, string? ManufacturerPartNumber, int? LeadTimeDays,
    [property: JsonConverter(typeof(NullableCommercialLineDecimalJsonConverter))] decimal? MinimumOrderQuantity, string? ExpenseAccountRef, bool IsStockItem, bool IsAsset, bool InspectionRequired);
public sealed record ItemUpdate(long Version, ItemInput Item);
public sealed record VersionInput(long Version);
public sealed record ItemActivationInput(long Version, string? Reason);
public sealed record CatalogPublishInput(long Version, string? Reason);
public sealed record CatalogWithdrawInput(long Version, string? Reason);
public sealed record ItemView(Guid Id, string Code, string Name, string Kind, string CategoryCode, string UnitCode,
    string? Description, string Status, bool IsPurchasable, bool IsStockItem, bool IsAsset, string? Brand, int? LeadTimeDays, long Version);
public sealed class CatalogEntryDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "CatalogEntryAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
public sealed class CatalogEntryNullableDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : ExactDecimalJsonCodec.Read(ref reader, 18, 4, "CatalogEntryQuantity");
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    { if (value is { } amount) writer.WriteNumberValue(amount); else writer.WriteNullValue(); }
}
public sealed record EntryInput(Guid? SupplierId, string CurrencyCode, string UnitCode,
    [property: JsonConverter(typeof(CatalogEntryDecimalJsonConverter))] decimal UnitPrice,
    string? TaxBasis,
    [property: JsonConverter(typeof(CatalogEntryNullableDecimalJsonConverter))] decimal? MinimumQuantity,
    [property: JsonConverter(typeof(CatalogEntryNullableDecimalJsonConverter))] decimal? MaximumQuantity, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo, string? AudienceCode);
public sealed record EntryUpdate(long Version, EntryInput Entry);
public sealed record EntryView(Guid Id, Guid CatalogItemId, Guid? SupplierId, string CurrencyCode, string UnitCode,
    decimal UnitPrice, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, decimal? MinimumQuantity,
    decimal? MaximumQuantity, string Status, long Version, string? TaxBasis, string? AudienceCode,
    CatalogEntryExactAmounts ExactAmounts, int CatalogEditVersion);
public sealed record CatalogEntryExactAmounts(string UnitPrice, string? MinimumQuantity, string? MaximumQuantity);
public sealed record CatalogView(Guid EntryId, Guid ItemId, string Code, string Name, string Kind,
    string? Description, string CategoryCode, string UnitCode, decimal EstimatedUnitPrice,
    string CurrencyCode, Guid SupplierId, string SupplierName, DateTimeOffset? PriceValidUntil, int PublishedVersion)
{
    // Keep the numeric member for existing clients; display/decimal-aware clients
    // use this additive companion without a JavaScript binary-float conversion.
    public string ExactEstimatedUnitPrice => EstimatedUnitPrice.ToString("0.####", CultureInfo.InvariantCulture);
}
