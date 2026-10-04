using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Microsoft.AspNetCore.Authorization;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Draft vendor records. Eligibility decisions belong to the later approval workflow.</summary>
public static class ProcurementVendorRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/vendors");
        group.MapGet("", ListAsync).RequireAuthorization("procurement-vendor-read");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("procurement-vendor-read");
        group.MapPost("", CreateAsync).RequireAuthorization("procurement-vendor-create");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("procurement-vendor-edit");
        group.MapPost("/{id:guid}/withdraw", WithdrawAsync).RequireAuthorization("procurement-vendor-edit");
        group.MapPost("/{id:guid}/contacts", AddContactAsync).RequireAuthorization("procurement-vendor-edit");
        group.MapPost("/{id:guid}/sites", AddSiteAsync).RequireAuthorization("procurement-vendor-edit");
        group.MapPost("/{id:guid}/qualifications", AddQualificationAsync).RequireAuthorization("procurement-vendor-edit");
    }

    private static async Task<IResult> ListAsync(ProcurementDbContext db, ProcurementRequestScope scope,
        IAuthorizationService authorization, HttpContext http, string? search, string? status,
        int? page, int? pageSize, CancellationToken ct)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (!(await authorization.AuthorizeAsync(http.User, "procurement-vendor-manage")).Succeeded)
            query = query.Where(x => ((x.Status == "Active" || x.Status == "Approved") && x.IsOrderable)
                || (x.Status == "Draft" && x.CreatedBy == scope.SubjectId));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.DisplayName, $"%{term}%")
                || EF.Functions.ILike(x.LegalName, $"%{term}%")
                || EF.Functions.ILike(x.Number, $"%{term}%"));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.DisplayName, x.LegalName, x.CountryCode,
                x.Status, x.IsOrderable, x.Version, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page = number, pageSize = size });
    }

    private static async Task<IResult> GetAsync(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, IAuthorizationService authorization, HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .Include(x => x.Contacts).Include(x => x.Sites)
            .Include(x => x.Qualifications).Include(x => x.Attributes).Include(x => x.Categories)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        var canManage = (await authorization.AuthorizeAsync(http.User, "procurement-vendor-manage")).Succeeded;
        if (!canManage && !((supplier.Status == "Active" || supplier.Status == "Approved") && supplier.IsOrderable)
            && !(supplier.Status == "Draft" && supplier.CreatedBy == scope.SubjectId))
            return Results.NotFound();
        return Results.Ok(canManage || supplier.CreatedBy == scope.SubjectId
            ? ToDetail(supplier) : ToBasicDetail(supplier));
    }

    private static async Task<IResult> CreateAsync(VendorDraftInput input, ProcurementDbContext db,
        ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var error = Validate(input);
        if (error is not null) return BadInput(error);
        var registration = NormalizeRegistration(input.RegistrationNumber);
        if (registration is not null && await db.Suppliers.AnyAsync(x =>
                x.CountryCode == input.CountryCode!.Trim().ToUpperInvariant()
                && x.NormalizedRegistration == registration, ct))
            return Conflict("A vendor with this company ID already exists. Open that vendor before adding another.");
        var supplier = new Supplier
        {
            Number = ("VEN-" + Guid.CreateVersion7().ToString("N")[..16]).ToUpperInvariant(),
            LegalName = input.LegalName!.Trim(),
            DisplayName = input.DisplayName!.Trim(),
            CountryCode = input.CountryCode!.Trim().ToUpperInvariant(),
            RegistrationNumber = Clean(input.RegistrationNumber),
            NormalizedRegistration = registration,
            TradingName = Clean(input.TradingName), Website = Clean(input.Website),
            CompanyIdType = Clean(input.CompanyIdType), TaxIdentifierRef = Clean(input.TaxIdentifierRef),
            SupplierType = Clean(input.SupplierType), CategoryCode = Clean(input.CategoryCode),
            DefaultCurrencyCode = Code(input.CurrencyCode), PaymentTermRef = Clean(input.PaymentTermRef),
            TaxCategoryRef = Clean(input.TaxCategoryRef), LanguageCode = Clean(input.LanguageCode),
            Remarks = Clean(input.Remarks), IsLocal = input.IsLocal,
            PortalAccessState = input.PortalAccessRequested ? "Requested" : null,
            ProposedByWorkerId = scope.WorkerId,
            Status = "Draft", IsOrderable = false,
        };
        if (input.PrimaryContact is not null)
        {
            var contactError = ValidateContact(input.PrimaryContact);
            if (contactError is not null) return BadInput(contactError);
            supplier.Contacts.Add(NewContact(supplier.Id, input.PrimaryContact, true));
        }
        foreach (var item in input.Contacts ?? [])
        {
            var contactError = ValidateContact(item);
            if (contactError is not null) return BadInput(contactError);
            supplier.Contacts.Add(NewContact(supplier.Id, item, false));
        }
        foreach (var item in input.Sites ?? [])
        {
            var siteError = ValidateSite(item);
            if (siteError is not null) return BadInput(siteError);
            supplier.Sites.Add(NewSite(supplier.Id, item));
        }
        if (supplier.Sites.GroupBy(x => x.Kind).Any(g => g.Count(x => x.IsPrimary) > 1))
            return BadInput("Use one primary address for each address type.");
        foreach (var item in input.Qualifications ?? [])
        {
            var qualificationError = ValidateQualification(item);
            if (qualificationError is not null) return BadInput(qualificationError);
            supplier.Qualifications.Add(NewQualification(supplier.Id, item));
        }
        foreach (var item in input.Attributes ?? [])
        {
            if (item.Kind is not ("CustomField" or "ReportingTag") || string.IsNullOrWhiteSpace(item.Key))
                return BadInput("Each custom field or reporting tag needs a type and name.");
            if (supplier.Attributes.Any(x => x.Kind == item.Kind && x.FieldKey == item.Key.Trim()))
                return BadInput("A custom field or reporting tag was entered more than once.");
            supplier.Attributes.Add(new SupplierAttribute { SupplierId = supplier.Id, Kind = item.Kind,
                FieldKey = item.Key.Trim(), FieldValue = item.Value?.Trim() ?? "" });
        }
        var similarNameFound = await db.Suppliers.AsNoTracking()
            .Where(x => x.DisplayName.ToLower() == supplier.DisplayName.ToLower()
                || x.LegalName.ToLower() == supplier.LegalName.ToLower())
            .AnyAsync(ct);
        db.AddScoped(supplier);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("This vendor already exists or its details conflict with another vendor."); }
        return Results.Created($"/api/procurement/v1/vendors/{supplier.Id}", new
        {
            supplier.Id, supplier.Number, supplier.Status, supplier.Version,
            possibleDuplicate = similarNameFound,
            message = similarNameFound
                ? "A vendor with a similar name exists. Review it before submitting this vendor for approval."
                : "Vendor draft saved.",
        });
    }

    private static async Task<IResult> UpdateAsync(Guid id, VendorDraftUpdate input, ProcurementDbContext db,
        ProcurementRequestScope scope, IAuthorizationService authorization, HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        if (!await CanEditRecord(supplier, scope, authorization, http)) return Results.NotFound();
        if (!CanEdit(supplier)) return Conflict("This vendor is already in review. Ask for it to be returned before editing.");
        if (input.Version != supplier.Version) return Stale();
        var error = Validate(input.Draft);
        if (error is not null) return BadInput(error);
        var registration = NormalizeRegistration(input.Draft.RegistrationNumber);
        var country = input.Draft.CountryCode!.Trim().ToUpperInvariant();
        if (registration is not null && await db.Suppliers.AnyAsync(x => x.Id != id &&
                x.CountryCode == country && x.NormalizedRegistration == registration, ct))
            return Conflict("A vendor with this company ID already exists.");
        supplier.LegalName = input.Draft.LegalName!.Trim();
        supplier.DisplayName = input.Draft.DisplayName!.Trim();
        supplier.CountryCode = country;
        supplier.RegistrationNumber = Clean(input.Draft.RegistrationNumber);
        supplier.NormalizedRegistration = registration;
        supplier.TradingName = Clean(input.Draft.TradingName);
        supplier.Website = Clean(input.Draft.Website);
        supplier.CompanyIdType = Clean(input.Draft.CompanyIdType);
        supplier.TaxIdentifierRef = Clean(input.Draft.TaxIdentifierRef);
        supplier.SupplierType = Clean(input.Draft.SupplierType);
        supplier.CategoryCode = Clean(input.Draft.CategoryCode);
        supplier.DefaultCurrencyCode = Code(input.Draft.CurrencyCode);
        supplier.PaymentTermRef = Clean(input.Draft.PaymentTermRef);
        supplier.TaxCategoryRef = Clean(input.Draft.TaxCategoryRef);
        supplier.LanguageCode = Clean(input.Draft.LanguageCode);
        supplier.Remarks = Clean(input.Draft.Remarks);
        supplier.IsLocal = input.Draft.IsLocal;
        supplier.PortalAccessState = input.Draft.PortalAccessRequested ? "Requested" : null;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        catch (DbUpdateException) { return Conflict("The company ID conflicts with another vendor."); }
        return Results.Ok(new { supplier.Id, supplier.Version, supplier.Status });
    }

    private static async Task<IResult> AddContactAsync(Guid id, VersionedContact input,
        ProcurementDbContext db, ProcurementRequestScope scope,
        IAuthorizationService authorization, HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.Include(x => x.Contacts).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        if (!await CanEditRecord(supplier, scope, authorization, http)) return Results.NotFound();
        if (!CanEdit(supplier)) return Conflict("This vendor is already in review.");
        if (input.Version != supplier.Version) return Stale();
        var error = ValidateContact(input.Contact);
        if (error is not null) return BadInput(error);
        if (input.Contact.IsPrimary && supplier.Contacts.Any(x => x.IsPrimary))
            return Conflict("This vendor already has a primary contact.");
        var contact = NewContact(id, input.Contact, input.Contact.IsPrimary);
        db.AddScoped(contact);
        db.Entry(supplier).Property(x => x.UpdatedAt).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        return Results.Created($"/api/procurement/v1/vendors/{id}", new { contact.Id, supplier.Version });
    }

    private static async Task<IResult> WithdrawAsync(Guid id, VendorWithdrawInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, IAuthorizationService authorization,
        HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null || !await CanEditRecord(supplier, scope, authorization, http))
            return Results.NotFound();
        if (supplier.Status != "Draft") return Conflict("Only a draft vendor can be withdrawn here.");
        if (supplier.Version != input.Version) return Stale();
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return BadInput("Give a short reason for withdrawing this vendor.");
        supplier.Status = "Withdrawn";
        supplier.IsOrderable = false;
        supplier.StatusReason = input.Reason.Trim();
        supplier.StatusDecidedAt = DateTimeOffset.UtcNow;
        supplier.StatusDecidedBy = scope.SubjectId;
        db.AddScoped(new SupplierDecision
        {
            SupplierId = supplier.Id, FromStatus = "Draft", ToStatus = "Withdrawn",
            ActorSubjectId = scope.SubjectId, Reason = input.Reason.Trim(),
            DecidedAt = supplier.StatusDecidedAt.Value,
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        return Results.Ok(new { supplier.Id, supplier.Status, supplier.Version });
    }

    private static async Task<IResult> AddSiteAsync(Guid id, VersionedSite input,
        ProcurementDbContext db, ProcurementRequestScope scope,
        IAuthorizationService authorization, HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.Include(x => x.Sites).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        if (!await CanEditRecord(supplier, scope, authorization, http)) return Results.NotFound();
        if (!CanEdit(supplier)) return Conflict("This vendor is already in review.");
        if (input.Version != supplier.Version) return Stale();
        var error = ValidateSite(input.Site);
        if (error is not null) return BadInput(error);
        if (input.Site.IsPrimary && supplier.Sites.Any(x => x.Kind == input.Site.Kind && x.IsPrimary))
            return Conflict("This vendor already has a primary address of that type.");
        var site = NewSite(id, input.Site);
        db.AddScoped(site);
        db.Entry(supplier).Property(x => x.UpdatedAt).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        return Results.Created($"/api/procurement/v1/vendors/{id}", new { site.Id, supplier.Version });
    }

    private static async Task<IResult> AddQualificationAsync(Guid id, VersionedQualification input,
        ProcurementDbContext db, ProcurementRequestScope scope,
        IAuthorizationService authorization, HttpContext http, CancellationToken ct)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        if (!await CanEditRecord(supplier, scope, authorization, http)) return Results.NotFound();
        if (!CanEdit(supplier)) return Conflict("This vendor is already in review.");
        if (input.Version != supplier.Version) return Stale();
        var error = ValidateQualification(input.Qualification);
        if (error is not null) return BadInput(error);
        var qualification = NewQualification(id, input.Qualification);
        db.AddScoped(qualification);
        db.Entry(supplier).Property(x => x.UpdatedAt).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Stale(); }
        return Results.Created($"/api/procurement/v1/vendors/{id}", new { qualification.Id, supplier.Version });
    }

    private static object ToDetail(Supplier x) => new
    {
        x.Id, x.Number, x.LegalName, x.DisplayName, x.TradingName, x.CountryCode,
        x.RegistrationNumber, x.CompanyIdType, x.TaxIdentifierRef, x.Website,
        x.SupplierType, x.CategoryCode, x.DefaultCurrencyCode, x.PaymentTermRef,
        x.TaxCategoryRef, x.LanguageCode, x.Remarks, x.IsLocal, x.Status,
        x.IsOrderable, x.PortalAccessState, x.Version, x.CreatedAt, x.UpdatedAt,
        contacts = x.Contacts.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Salutation, c.FirstName, c.LastName,
                c.Role, c.Email, c.Phone, c.WorkPhone, c.MobilePhone, c.LanguageCode,
                c.IsPrimary, c.Version }),
        sites = x.Sites.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Kind)
            .Select(s => new { s.Id, s.Kind, s.Label, s.AddressLine1, s.AddressLine2,
                s.City, s.Region, s.PostalCode, s.CountryCode, s.IsPrimary,
                s.IsServiceSite, s.Version }),
        qualifications = x.Qualifications.OrderBy(q => q.QualificationType)
            .Select(q => new { q.Id, q.QualificationType, q.Issuer, q.ReferenceNumber,
                q.IssuedOn, q.ExpiresOn, q.ReviewStatus, q.EvidenceDocumentRefId,
                q.Version }),
        attributes = x.Attributes.OrderBy(a => a.Kind).ThenBy(a => a.FieldKey)
            .Select(a => new { a.Id, a.Kind, key = a.FieldKey, value = a.FieldValue, a.Version }),
        categories = x.Categories.OrderBy(c => c.CategoryCode)
            .Select(c => new { c.Id, c.CategoryCode, c.IsPrimary, c.EffectiveFrom, c.EffectiveTo }),
    };

    private static object ToBasicDetail(Supplier x) => new
    {
        x.Id, x.Number, x.DisplayName, x.CountryCode, x.SupplierType,
        x.CategoryCode, x.Status, x.IsOrderable,
    };

    private static async Task<bool> CanEditRecord(Supplier supplier, ProcurementRequestScope scope,
        IAuthorizationService authorization, HttpContext http) =>
        supplier.CreatedBy == scope.SubjectId
        || (await authorization.AuthorizeAsync(http.User, "procurement-vendor-manage")).Succeeded;

    private static bool CanEdit(Supplier supplier) => supplier.Status is "Draft" or "More Information Required";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Code(string? value) => Clean(value)?.ToUpperInvariant();
    private static string? NormalizeRegistration(string? value)
    {
        if (Clean(value) is not { } text) return null;
        var normalized = new string(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }
    private static IResult BadInput(string message) => Results.UnprocessableEntity(new ApiError("validation-failed", message, []));
    private static IResult Conflict(string message) => Results.Conflict(new ApiError("vendor-conflict", message, []));
    private static IResult Stale() => Results.Conflict(new ApiError("stale-vendor", "This vendor changed. Refresh it and try again.", []));

    private static string? Validate(VendorDraftInput x)
    {
        if (string.IsNullOrWhiteSpace(x.LegalName) || x.LegalName.Length > 300) return "Enter a company name (up to 300 characters).";
        if (string.IsNullOrWhiteSpace(x.DisplayName) || x.DisplayName.Length > 300) return "Enter a display name (up to 300 characters).";
        if (string.IsNullOrWhiteSpace(x.CountryCode) || x.CountryCode.Trim().Length != 2) return "Choose a two-letter country code.";
        if (x.CurrencyCode is { Length: > 0 } && x.CurrencyCode.Trim().Length != 3) return "Choose a three-letter currency code.";
        if (x.RegistrationNumber is { Length: > 120 }) return "Company ID is too long.";
        if (x.Remarks is { Length: > 4000 }) return "Remarks are too long.";
        if (x.Contacts?.Count > 50 || x.Sites?.Count > 20 || x.Qualifications?.Count > 50
            || x.Attributes?.Count > 100) return "Too many vendor details were supplied at once.";
        return null;
    }

    private static string? ValidateContact(VendorContactInput x)
    {
        var name = Clean(x.Name) ?? Clean($"{x.FirstName} {x.LastName}");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return "Enter a contact name (up to 200 characters).";
        if (x.Email is { Length: > 0 } && (!x.Email.Contains('@') || x.Email.Length > 320)) return "Enter a valid contact email.";
        return null;
    }

    private static string? ValidateSite(VendorSiteInput x)
    {
        if (x.Kind is not ("Billing" or "Shipping" or "Physical")) return "Choose Billing, Shipping or Physical for the address type.";
        if (string.IsNullOrWhiteSpace(x.AddressLine1)) return "Enter the first address line.";
        if (string.IsNullOrWhiteSpace(x.CountryCode) || x.CountryCode.Trim().Length != 2) return "Choose a two-letter country code for the address.";
        return null;
    }

    private static string? ValidateQualification(VendorQualificationInput x)
    {
        if (string.IsNullOrWhiteSpace(x.QualificationType) || x.QualificationType.Length > 80) return "Enter a document type (up to 80 characters).";
        if (x.IssuedOn.HasValue && x.ExpiresOn.HasValue && x.ExpiresOn < x.IssuedOn) return "Expiry must be after the issue date.";
        if (x.EvidenceDocumentRefId.HasValue) return "Upload documents through the approved document service before linking them to a vendor.";
        return null;
    }

    private static SupplierContact NewContact(Guid supplierId, VendorContactInput x, bool primary) => new()
    {
        SupplierId = supplierId,
        Name = Clean(x.Name) ?? $"{x.FirstName} {x.LastName}".Trim(),
        Salutation = Clean(x.Salutation), FirstName = Clean(x.FirstName), LastName = Clean(x.LastName),
        Role = Clean(x.Role), Email = Clean(x.Email), Phone = Clean(x.Phone),
        WorkPhone = Clean(x.WorkPhone), MobilePhone = Clean(x.MobilePhone),
        LanguageCode = Clean(x.LanguageCode), IsPrimary = primary,
    };

    private static SupplierSite NewSite(Guid supplierId, VendorSiteInput x) => new()
    {
        SupplierId = supplierId, Kind = x.Kind, Label = Clean(x.Label),
        AddressLine1 = x.AddressLine1.Trim(), AddressLine2 = Clean(x.AddressLine2),
        City = Clean(x.City), Region = Clean(x.Region), PostalCode = Clean(x.PostalCode),
        CountryCode = x.CountryCode.Trim().ToUpperInvariant(), IsPrimary = x.IsPrimary,
        IsServiceSite = x.IsServiceSite,
    };

    private static SupplierQualification NewQualification(Guid supplierId, VendorQualificationInput x) => new()
    {
        SupplierId = supplierId, QualificationType = x.QualificationType.Trim(),
        Issuer = Clean(x.Issuer), ReferenceNumber = Clean(x.ReferenceNumber),
        IssuedOn = x.IssuedOn, ExpiresOn = x.ExpiresOn,
        EvidenceDocumentRefId = x.EvidenceDocumentRefId, ReviewStatus = "Pending",
    };
}

public sealed record VendorDraftInput(string? LegalName, string? DisplayName, string? CountryCode,
    string? RegistrationNumber, string? TradingName, string? Website, string? CompanyIdType,
    string? TaxIdentifierRef, string? SupplierType, string? CategoryCode,
    string? CurrencyCode, string? PaymentTermRef, string? TaxCategoryRef,
    string? LanguageCode, string? Remarks, bool IsLocal, bool PortalAccessRequested,
    VendorContactInput? PrimaryContact, List<VendorContactInput>? Contacts,
    List<VendorSiteInput>? Sites, List<VendorQualificationInput>? Qualifications,
    List<VendorAttributeInput>? Attributes);
public sealed record VendorDraftUpdate(long Version, VendorDraftInput Draft);
public sealed record VendorWithdrawInput(long Version, string? Reason);
public sealed record VendorContactInput(string? Name, string? Salutation, string? FirstName,
    string? LastName, string? Role, string? Email, string? Phone, string? WorkPhone,
    string? MobilePhone, string? LanguageCode, bool IsPrimary);
public sealed record VendorSiteInput(string Kind, string AddressLine1, string CountryCode,
    string? Label, string? AddressLine2, string? City, string? Region, string? PostalCode,
    bool IsPrimary, bool IsServiceSite);
public sealed record VendorQualificationInput(string QualificationType, string? Issuer,
    string? ReferenceNumber, DateOnly? IssuedOn, DateOnly? ExpiresOn, Guid? EvidenceDocumentRefId);
public sealed record VendorAttributeInput(string Kind, string Key, string? Value);
public sealed record VersionedContact(long Version, VendorContactInput Contact);
public sealed record VersionedSite(long Version, VendorSiteInput Site);
public sealed record VersionedQualification(long Version, VendorQualificationInput Qualification);
