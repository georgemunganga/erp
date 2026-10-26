using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Native single-record and bounded import paths share draft validation and construction.</summary>
internal static class ProcurementMasterDraftRules
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Code(string? value) => Clean(value)?.ToUpperInvariant();

    internal static string? NormalizeRegistration(string? value)
    {
        if (Clean(value) is not { } text) return null;
        var normalized = new string(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    internal static string? ValidateVendor(VendorDraftInput x)
    {
        if (string.IsNullOrWhiteSpace(x.LegalName) || x.LegalName.Length > 300) return "Enter a company name (up to 300 characters).";
        if (string.IsNullOrWhiteSpace(x.DisplayName) || x.DisplayName.Length > 300) return "Enter a display name (up to 300 characters).";
        if (string.IsNullOrWhiteSpace(x.CountryCode) || x.CountryCode.Trim().Length != 2) return "Choose a two-letter country code.";
        if (x.CurrencyCode is { Length: > 0 } && x.CurrencyCode.Trim().Length != 3) return "Choose a three-letter currency code.";
        if (x.RegistrationNumber is { Length: > 120 }) return "Company ID is too long.";
        if (x.Remarks is { Length: > 4000 }) return "Remarks are too long.";
        if (x.Contacts?.Count > 50 || x.Sites?.Count > 20 || x.Qualifications?.Count > 50
            || x.Attributes?.Count > 100) return "Too many vendor details were supplied at once.";
        var attributeKeys = new HashSet<(string Kind, string Key)>();
        foreach (var attribute in x.Attributes ?? [])
        {
            if (attribute is null || attribute.Kind is not ("CustomField" or "ReportingTag")
                || string.IsNullOrWhiteSpace(attribute.Key) || attribute.Key.Trim().Length > 120)
                return "Each custom field or reporting tag needs a supported type and a name of up to 120 characters.";
            if (!attributeKeys.Add((attribute.Kind, attribute.Key.Trim())))
                return "A custom field or reporting tag was entered more than once.";
        }
        return null;
    }

    internal static Supplier NewVendor(VendorDraftInput input, ProcurementRequestScope scope)
    {
        return new Supplier
        {
            // The first 16 digits of a Version 7 UUID carry mostly timestamp;
            // use a random UUID suffix so one import batch cannot exhaust the
            // few random bits available within a millisecond. Record IDs stay V7.
            Number = ("VEN-" + Guid.NewGuid().ToString("N")[..16]).ToUpperInvariant(),
            LegalName = input.LegalName!.Trim(),
            DisplayName = input.DisplayName!.Trim(),
            CountryCode = input.CountryCode!.Trim().ToUpperInvariant(),
            RegistrationNumber = Clean(input.RegistrationNumber),
            NormalizedRegistration = NormalizeRegistration(input.RegistrationNumber),
            TradingName = Clean(input.TradingName), Website = Clean(input.Website),
            CompanyIdType = Clean(input.CompanyIdType), TaxIdentifierRef = Clean(input.TaxIdentifierRef),
            SupplierType = Clean(input.SupplierType), CategoryCode = Clean(input.CategoryCode),
            DefaultCurrencyCode = Code(input.CurrencyCode), PaymentTermRef = Clean(input.PaymentTermRef),
            TaxCategoryRef = Clean(input.TaxCategoryRef), LanguageCode = Clean(input.LanguageCode),
            Remarks = Clean(input.Remarks), IsLocal = input.IsLocal,
            PortalAccessState = input.PortalAccessRequested ? "Requested" : null,
            // An optional linked alias is metadata, never an employee prerequisite.
            ProposedByWorkerId = scope.CoreLinkedWorkerId ?? scope.WorkerId,
            Status = "Draft", IsOrderable = false,
        };
    }

    internal static VendorDraftInput ImportVendor(IReadOnlyDictionary<string, string> row, string? currency) =>
        new(LegalName: row.GetValueOrDefault("legalName"), DisplayName: row.GetValueOrDefault("displayName"),
            CountryCode: row.GetValueOrDefault("countryCode"), RegistrationNumber: row.GetValueOrDefault("registrationNumber"),
            TradingName: null, Website: null, CompanyIdType: null, TaxIdentifierRef: null,
            SupplierType: null, CategoryCode: null, CurrencyCode: currency,
            PaymentTermRef: null, TaxCategoryRef: null, LanguageCode: null, Remarks: null,
            IsLocal: false, PortalAccessRequested: false, PrimaryContact: null, Contacts: null,
            Sites: null, Qualifications: null, Attributes: null);

    internal static string? ValidateItem(ItemInput? x)
    {
        if (x is null) return "Enter the item details before saving.";
        if (string.IsNullOrWhiteSpace(x.Code) || x.Code.Trim().Length > 50) return "Enter an item code of up to 50 characters.";
        if (string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 300) return "Enter an item name of up to 300 characters.";
        if (string.IsNullOrWhiteSpace(x.Kind) || x.Kind.Trim().Length > 40) return "Choose an item type.";
        if (string.IsNullOrWhiteSpace(x.CategoryCode)) return "Choose a category.";
        if (string.IsNullOrWhiteSpace(x.UnitCode) || x.UnitCode.Trim().Length > 30) return "Choose a unit.";
        if (x.PurchaseUnitConversion.HasValue && !ExactDecimalJsonCodec.IsRepresentable(x.PurchaseUnitConversion.Value, 18, 6)) return "Unit conversion must fit 18 digits with up to six decimal places.";
        if (x.MinimumOrderQuantity.HasValue && !ExactDecimalJsonCodec.IsRepresentable(x.MinimumOrderQuantity.Value, 18, 4)) return "Minimum order quantity must fit 18 digits with up to four decimal places.";
        if (x.PurchaseUnitConversion is <= 0) return "Unit conversion must be greater than zero.";
        if (x.MinimumOrderQuantity is < 0 || x.LeadTimeDays is < 0) return "Quantity and lead time cannot be negative.";
        return null;
    }

    internal static CatalogItem NewItem(ItemInput input)
    {
        var item = new CatalogItem { Code = input.Code.Trim().ToUpperInvariant(), Status = "Inactive" };
        ApplyItem(input, item);
        return item;
    }

    internal static ItemInput ImportItem(IReadOnlyDictionary<string, string> row) =>
        new(Code: row.GetValueOrDefault("code") ?? "", Name: row.GetValueOrDefault("name") ?? "",
            Kind: row.GetValueOrDefault("kind") ?? "", CategoryCode: row.GetValueOrDefault("categoryCode") ?? "",
            UnitCode: row.GetValueOrDefault("unitCode") ?? "", Description: row.GetValueOrDefault("description"),
            TaxCodeRef: null, PurchaseUnitCode: null, PurchaseUnitConversion: null,
            Brand: null, Manufacturer: null, ManufacturerPartNumber: null, LeadTimeDays: null,
            MinimumOrderQuantity: null, ExpenseAccountRef: null, IsStockItem: false,
            IsAsset: false, InspectionRequired: false);

    internal static void ApplyItem(ItemInput x, CatalogItem item)
    {
        item.Name = x.Name.Trim(); item.Kind = x.Kind.Trim(); item.CategoryCode = x.CategoryCode.Trim();
        item.UnitCode = x.UnitCode.Trim(); item.Description = x.Description?.Trim(); item.TaxCodeRef = x.TaxCodeRef?.Trim();
        item.PurchaseUnitCode = x.PurchaseUnitCode?.Trim(); item.PurchaseUnitConversion = x.PurchaseUnitConversion;
        item.Brand = x.Brand?.Trim(); item.Manufacturer = x.Manufacturer?.Trim();
        item.ManufacturerPartNumber = x.ManufacturerPartNumber?.Trim(); item.LeadTimeDays = x.LeadTimeDays;
        item.MinimumOrderQuantity = x.MinimumOrderQuantity; item.ExpenseAccountRef = x.ExpenseAccountRef?.Trim();
        item.IsStockItem = x.IsStockItem; item.IsAsset = x.IsAsset; item.InspectionRequired = x.InspectionRequired;
        item.IsPurchasable = false;
    }
}
