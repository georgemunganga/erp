using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Item stewardship and unpublished catalog price drafts. Publication requires the policy workflow.</summary>
public static class ProcurementItemEndpoints
{
    public static void MapRoutes(WebApplication app)
    {
        var items = app.MapGroup("/api/procurement/v1/items");
        items.MapGet("", List).RequireAuthorization("procurement-item-manage");
        items.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-item-manage");
        items.MapPost("", Create).RequireAuthorization("procurement-item-manage");
        items.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/deactivate", Deactivate).RequireAuthorization("procurement-item-manage");
        items.MapGet("/{id:guid}/catalog-entries", ListEntries).RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/catalog-entries", AddEntry).RequireAuthorization("procurement-item-manage");
        items.MapPut("/{id:guid}/catalog-entries/{entryId:guid}", UpdateEntry).RequireAuthorization("procurement-item-manage");
        items.MapPost("/{id:guid}/catalog-entries/{entryId:guid}/withdraw", WithdrawEntry).RequireAuthorization("procurement-item-manage");
        app.MapGet("/api/procurement/v1/catalog", Catalog).RequireAuthorization("procurement-item-read");
    }

    private static async Task<IResult> List(ProcurementDbContext db, string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.CatalogItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%") || EF.Functions.ILike(x.Name, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Code)
            .Skip((number - 1) * size).Take(size).Select(x => new ItemView(
                x.Id, x.Code, x.Name, x.Kind, x.CategoryCode, x.UnitCode, x.Description, x.Status,
                x.IsPurchasable, x.IsStockItem, x.IsAsset, x.Brand, x.LeadTimeDays, x.Version))
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        var item = await db.CatalogItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Results.Ok(ToView(item));
    }

    private static async Task<IResult> Catalog(ProcurementDbContext db, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var now = DateTimeOffset.UtcNow;
        // Until audience/site/contract resolution is implemented, only unrestricted, supplier-backed entries are exposed.
        var query = from entry in db.CatalogEntries.AsNoTracking()
                    join item in db.CatalogItems.AsNoTracking() on entry.CatalogItemId equals item.Id
                    join supplier in db.Suppliers.AsNoTracking() on entry.SupplierId equals (Guid?)supplier.Id
                    where entry.Status == "Published" && item.Status == "Active" && item.IsPurchasable
                        && (supplier.Status == "Approved" || supplier.Status == "Active") && supplier.IsOrderable
                        && (supplier.EligibleFrom == null || supplier.EligibleFrom <= now)
                        && (supplier.EligibleUntil == null || supplier.EligibleUntil >= now)
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
                x.entry.UnitPrice, x.entry.CurrencyCode, x.supplier.DisplayName,
                x.entry.EffectiveTo, x.entry.PublishedVersion))
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> Create(ItemInput input, ProcurementDbContext db, CancellationToken ct)
    {
        var error = ValidateItem(input);
        if (error is not null) return BadRequest(error);
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.CatalogItems.AnyAsync(x => x.Code == code, ct)) return Conflict("An item with this code already exists.");
        var item = new CatalogItem { Code = code, Status = "Inactive" };
        Apply(input, item);
        db.AddScoped(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("The item could not be saved. Check its code and references."); }
        return Results.Created($"/api/procurement/v1/items/{item.Id}", ToView(item));
    }

    private static async Task<IResult> Update(Guid id, ItemUpdate input, ProcurementDbContext db, CancellationToken ct)
    {
        var error = ValidateItem(input.Item);
        if (error is not null) return BadRequest(error);
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

    private static async Task<IResult> Deactivate(Guid id, VersionInput input, ProcurementDbContext db, CancellationToken ct)
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
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This item changed since you opened it. Refresh and try again."); }
        return Results.Ok(ToView(item));
    }

    private static async Task<IResult> ListEntries(Guid id, ProcurementDbContext db, CancellationToken ct)
    {
        if (!await db.CatalogItems.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var entries = await db.CatalogEntries.AsNoTracking().Where(x => x.CatalogItemId == id)
            .OrderByDescending(x => x.EffectiveFrom).Select(x => new EntryView(x.Id, x.CatalogItemId, x.SupplierId,
                x.CurrencyCode, x.UnitCode, x.UnitPrice, x.EffectiveFrom, x.EffectiveTo, x.MinimumQuantity,
                x.MaximumQuantity, x.Status, x.Version)).ToListAsync(ct);
        return Results.Ok(new { items = entries });
    }

    private static async Task<IResult> AddEntry(Guid id, EntryInput input, ProcurementDbContext db, CancellationToken ct)
    {
        var error = ValidateEntry(input);
        if (error is not null) return BadRequest(error);
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

    private static async Task<IResult> UpdateEntry(Guid id, Guid entryId, EntryUpdate input, ProcurementDbContext db, CancellationToken ct)
    {
        var error = ValidateEntry(input.Entry);
        if (error is not null) return BadRequest(error);
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

    private static async Task<IResult> WithdrawEntry(Guid id, Guid entryId, VersionInput input, ProcurementDbContext db, CancellationToken ct)
    {
        if (input.Version < 1) return BadRequest("A valid record version is required.");
        var entry = await db.CatalogEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.CatalogItemId == id, ct);
        if (entry is null) return NotFound();
        if (entry.Version != input.Version) return Conflict("This catalog price changed. Refresh and try again.");
        if (entry.Status == "Withdrawn") return Results.Ok(ToView(entry));
        entry.Status = "Withdrawn";
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("This catalog price changed. Refresh and try again."); }
        return Results.Ok(ToView(entry));
    }

    private static string? ValidateItem(ItemInput x)
    {
        if (string.IsNullOrWhiteSpace(x.Code) || x.Code.Trim().Length > 50) return "Enter an item code of up to 50 characters.";
        if (string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 300) return "Enter an item name of up to 300 characters.";
        if (string.IsNullOrWhiteSpace(x.Kind) || x.Kind.Trim().Length > 40) return "Choose an item type.";
        if (string.IsNullOrWhiteSpace(x.CategoryCode)) return "Choose a category.";
        if (string.IsNullOrWhiteSpace(x.UnitCode) || x.UnitCode.Trim().Length > 30) return "Choose a unit.";
        if (x.PurchaseUnitConversion is <= 0) return "Unit conversion must be greater than zero.";
        if (x.MinimumOrderQuantity is < 0 || x.LeadTimeDays is < 0) return "Quantity and lead time cannot be negative.";
        return null;
    }

    private static string? ValidateEntry(EntryInput x)
    {
        if (string.IsNullOrWhiteSpace(x.CurrencyCode) || x.CurrencyCode.Trim().Length != 3) return "Choose a three-letter currency code.";
        if (string.IsNullOrWhiteSpace(x.UnitCode) || x.UnitCode.Trim().Length > 30) return "Choose a unit.";
        if (x.UnitPrice < 0 || x.MinimumQuantity is < 0 || x.MaximumQuantity is < 0) return "Price and quantities cannot be negative.";
        if (x.MinimumQuantity.HasValue && x.MaximumQuantity.HasValue && x.MinimumQuantity > x.MaximumQuantity) return "Maximum quantity must be at least the minimum.";
        if (x.EffectiveTo.HasValue && x.EffectiveTo.Value <= x.EffectiveFrom) return "The end date must be after the start date.";
        return null;
    }

    private static void Apply(ItemInput x, CatalogItem item)
    {
        item.Name = x.Name.Trim(); item.Kind = x.Kind.Trim(); item.CategoryCode = x.CategoryCode.Trim();
        item.UnitCode = x.UnitCode.Trim(); item.Description = x.Description?.Trim(); item.TaxCodeRef = x.TaxCodeRef?.Trim();
        item.PurchaseUnitCode = x.PurchaseUnitCode?.Trim(); item.PurchaseUnitConversion = x.PurchaseUnitConversion;
        item.Brand = x.Brand?.Trim(); item.Manufacturer = x.Manufacturer?.Trim();
        item.ManufacturerPartNumber = x.ManufacturerPartNumber?.Trim(); item.LeadTimeDays = x.LeadTimeDays;
        item.MinimumOrderQuantity = x.MinimumOrderQuantity; item.ExpenseAccountRef = x.ExpenseAccountRef?.Trim();
        item.IsStockItem = x.IsStockItem; item.IsAsset = x.IsAsset; item.InspectionRequired = x.InspectionRequired;
        // Steward CRUD never makes an item orderable. Activation needs review and policy checks.
        item.IsPurchasable = false;
    }

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
    private static EntryView ToView(CatalogEntry x) => new(x.Id, x.CatalogItemId, x.SupplierId, x.CurrencyCode,
        x.UnitCode, x.UnitPrice, x.EffectiveFrom, x.EffectiveTo, x.MinimumQuantity, x.MaximumQuantity, x.Status, x.Version);
    private static IResult BadRequest(string message) => Results.BadRequest(new { code = "invalid-item", message });
    private static IResult Conflict(string message) => Results.Conflict(new { code = "conflict", message });
    private static IResult NotFound() => Results.NotFound(new { code = "not-found", message = "Item not found." });
}

public sealed record ItemInput(string Code, string Name, string Kind, string CategoryCode, string UnitCode,
    string? Description, string? TaxCodeRef, string? PurchaseUnitCode, decimal? PurchaseUnitConversion,
    string? Brand, string? Manufacturer, string? ManufacturerPartNumber, int? LeadTimeDays,
    decimal? MinimumOrderQuantity, string? ExpenseAccountRef, bool IsStockItem, bool IsAsset, bool InspectionRequired);
public sealed record ItemUpdate(long Version, ItemInput Item);
public sealed record VersionInput(long Version);
public sealed record ItemView(Guid Id, string Code, string Name, string Kind, string CategoryCode, string UnitCode,
    string? Description, string Status, bool IsPurchasable, bool IsStockItem, bool IsAsset, string? Brand, int? LeadTimeDays, long Version);
public sealed record EntryInput(Guid? SupplierId, string CurrencyCode, string UnitCode, decimal UnitPrice,
    string? TaxBasis, decimal? MinimumQuantity, decimal? MaximumQuantity, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo, string? AudienceCode);
public sealed record EntryUpdate(long Version, EntryInput Entry);
public sealed record EntryView(Guid Id, Guid CatalogItemId, Guid? SupplierId, string CurrencyCode, string UnitCode,
    decimal UnitPrice, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, decimal? MinimumQuantity,
    decimal? MaximumQuantity, string Status, long Version);
public sealed record CatalogView(Guid EntryId, Guid ItemId, string Code, string Name, string Kind,
    string? Description, string CategoryCode, string UnitCode, decimal EstimatedUnitPrice,
    string CurrencyCode, string SupplierName, DateTimeOffset? PriceValidUntil, int PublishedVersion);
