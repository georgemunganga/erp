using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Own, draft-only purchase requests. Submission awaits policy, budget and approval integration.</summary>
public static class ProcurementRequestRoutes
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/requests");
        group.MapGet("", List).RequireAuthorization("procurement-request-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-request-read");
        group.MapPost("", Create).RequireAuthorization("procurement-request-create");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("procurement-request-edit");
        group.MapPost("/{id:guid}/withdraw", Withdraw).RequireAuthorization("procurement-request-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? limit, int? offset, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var skip = Math.Clamp(offset ?? 0, 0, 100_000);
        var own = db.PurchaseRequests.AsNoTracking().Where(x => x.CreatedBy == scope.SubjectId);
        var count = await own.CountAsync(ct);
        var records = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(skip).Take(take)
            .Select(x => new RequestSummary(x.Id, x.Number, x.Purpose, x.Status, x.CurrencyCode,
                x.NeededBy, x.CreatedAt, x.Version, x.Lines.Count))
            .ToListAsync(ct);
        return Results.Ok(new { records, count, limit = take, offset = skip });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var request = await db.PurchaseRequests.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == scope.SubjectId, ct);
        return request is null ? Error("not-found", "Request not found.", 404) : Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> Create(NewRequest input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var issue = Validate(input.Purpose, input.CurrencyCode, input.Lines);
        if (issue is not null) return Error("validation-failed", issue, 422);
        if (input.Lines!.Any(x => x.Id.HasValue))
            return Error("validation-failed", "New request lines cannot carry an existing line ID.", 422);
        var workerId = scope.WorkerId;
        if (workerId is null)
            return Error("employee-unavailable", "An active employee record is needed to create a request.", 403);
        var referenceIssue = await ValidateReferences(input.Lines, db, ct);
        if (referenceIssue is not null) return Error("validation-failed", referenceIssue, 422);

        var request = new PurchaseRequest
        {
            Number = "PR-" + Guid.CreateVersion7().ToString("N"),
            RequesterWorkerId = workerId.Value,
            Purpose = input.Purpose!.Trim(),
            CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(),
            NeededBy = input.NeededBy,
            BranchRef = scope.WorkLocationId,
            DepartmentRef = scope.OrgUnitId,
            CostCenterRef = Clean(input.CostCenterRef),
            ProjectRef = Clean(input.ProjectRef),
            DeliveryLocationRef = Clean(input.DeliveryLocationRef),
            DeliveryAddressText = Clean(input.DeliveryAddressText),
            FundingSourceRef = Clean(input.FundingSourceRef),
            NotesToApprover = Clean(input.NotesToApprover),
            SourceChannel = "ERP",
            Status = "Draft",
        };
        for (var i = 0; i < input.Lines!.Count; i++)
            request.Lines.Add(NewLine(request.Id, i + 1, input.Lines[i], request.CurrencyCode));
        db.AddScoped(request);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/requests/{request.Id}", ToDetail(request));
    }

    private static async Task<IResult> Update(Guid id, UpdateRequest input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == scope.SubjectId, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        if (request.Status != "Draft") return Error("request-locked", "Only a draft request can be changed.", 409);
        if (request.Version != input.Version) return Error("version-conflict", "Refresh this request before saving changes.", 409);
        if (scope.WorkerId != request.RequesterWorkerId)
            return Error("employee-unavailable", "An active employee record is needed to change this request.", 403);
        var issue = Validate(input.Purpose, input.CurrencyCode, input.Lines);
        if (issue is not null) return Error("validation-failed", issue, 422);
        if (input.Lines!.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count()
            != input.Lines.Count(x => x.Id.HasValue))
            return Error("validation-failed", "A request line was repeated.", 422);
        var referenceIssue = await ValidateReferences(input.Lines, db, ct);
        if (referenceIssue is not null) return Error("validation-failed", referenceIssue, 422);
        var existing = request.Lines.ToDictionary(x => x.Id);
        if (input.Lines.Any(x => x.Id.HasValue && !existing.ContainsKey(x.Id.Value)))
            return Error("validation-failed", "A request line does not belong to this request.", 422);
        if (input.Lines.Any(x => x.Id.HasValue && existing[x.Id.Value].ApprovalStatus == "Cancelled"))
            return Error("validation-failed", "A removed line cannot be restored; add a new line.", 422);

        request.Purpose = input.Purpose!.Trim();
        request.CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant();
        request.NeededBy = input.NeededBy;
        request.CostCenterRef = Clean(input.CostCenterRef);
        request.ProjectRef = Clean(input.ProjectRef);
        request.DeliveryLocationRef = Clean(input.DeliveryLocationRef);
        request.DeliveryAddressText = Clean(input.DeliveryAddressText);
        request.FundingSourceRef = Clean(input.FundingSourceRef);
        request.NotesToApprover = Clean(input.NotesToApprover);
        request.UpdatedAt = DateTimeOffset.UtcNow; // bump header version even for line-only changes
        var included = input.Lines.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        foreach (var line in request.Lines.Where(x => x.ApprovalStatus != "Cancelled" && !included.Contains(x.Id)))
        {
            line.ApprovalStatus = "Cancelled";
            line.CancelledQuantity = line.RequestedQuantity;
        }
        var nextLine = request.Lines.Count == 0 ? 1 : request.Lines.Max(x => x.LineNumber) + 1;
        foreach (var incoming in input.Lines)
        {
            if (incoming.Id is Guid lineId)
                ApplyLine(existing[lineId], incoming, request.CurrencyCode);
            else
            {
                var line = NewLine(request.Id, nextLine++, incoming, request.CurrencyCode);
                db.AddScoped(line);
                request.Lines.Add(line);
            }
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error("version-conflict", "Refresh this request before saving changes.", 409); }
        return Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> Withdraw(Guid id, VersionCommand input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.CreatedBy == scope.SubjectId, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        if (request.Status != "Draft") return Error("request-locked", "Only a draft request can be withdrawn here.", 409);
        if (request.Version != input.Version) return Error("version-conflict", "Refresh this request before withdrawing it.", 409);
        if (scope.WorkerId != request.RequesterWorkerId)
            return Error("employee-unavailable", "An active employee record is needed to withdraw this request.", 403);
        request.Status = "Withdrawn";
        foreach (var line in request.Lines.Where(x => x.ApprovalStatus != "Cancelled"))
        {
            line.ApprovalStatus = "Cancelled";
            line.CancelledQuantity = line.RequestedQuantity;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error("version-conflict", "Refresh this request before withdrawing it.", 409); }
        return Results.Ok(ToDetail(request));
    }

    private static async Task<string?> ValidateReferences(IEnumerable<RequestLineInput> lines,
        ProcurementDbContext db, CancellationToken ct)
    {
        var itemIds = lines.Where(x => x.CatalogItemId.HasValue).Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        var supplierIds = lines.Where(x => x.PreferredSupplierId.HasValue).Select(x => x.PreferredSupplierId!.Value).Distinct().ToArray();
        if (itemIds.Length > 0 && await db.CatalogItems.AsNoTracking()
                .CountAsync(x => itemIds.Contains(x.Id) && x.Status == "Active" && x.IsPurchasable, ct) != itemIds.Length)
            return "One or more items are unavailable for purchase.";
        if (supplierIds.Length > 0 && await db.Suppliers.AsNoTracking()
                .CountAsync(x => supplierIds.Contains(x.Id) && (x.Status == "Approved" || x.Status == "Active") && x.IsOrderable, ct) != supplierIds.Length)
            return "One or more preferred vendors are unavailable.";
        return null;
    }

    private static string? Validate(string? purpose, string? currency, IReadOnlyList<RequestLineInput>? lines)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > 1000)
            return "Enter a purpose of up to 1,000 characters.";
        if (currency is null || currency.Trim().Length != 3 || !currency.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return "Choose a three-letter currency code.";
        if (lines is null || lines.Count is < 1 or > 100)
            return "Add between 1 and 100 request lines.";
        decimal total = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 1000)
                return "Every line needs a description of up to 1,000 characters.";
            if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode.Trim().Length > 30)
                return "Every line needs a valid unit.";
            if (!ValidMoney(line.RequestedQuantity, positive: true) || !ValidMoney(line.EstimatedUnitPrice))
                return "Line quantity must be positive and unit price cannot be negative; use at most four decimal places.";
            if (line.DiscountPercent is decimal discount && (discount < 0 || discount > 100 || decimal.Round(discount, 4) != discount))
                return "Discount must be between 0 and 100 with at most four decimal places.";
            try { total += line.RequestedQuantity * line.EstimatedUnitPrice; }
            catch (OverflowException) { return "The estimated amount is too large."; }
            if (total > 99_999_999_999_999.9999m)
                return "The estimated amount is too large.";
        }
        return null;
    }

    private static bool ValidMoney(decimal value, bool positive = false) =>
        (positive ? value > 0 : value >= 0) && value <= 99_999_999_999_999.9999m
        && decimal.Round(value, 4) == value;

    private static PurchaseRequestLine NewLine(Guid requestId, int number, RequestLineInput input, string currency)
    {
        var line = new PurchaseRequestLine { PurchaseRequestId = requestId, LineNumber = number };
        ApplyLine(line, input, currency);
        return line;
    }

    private static void ApplyLine(PurchaseRequestLine line, RequestLineInput input, string currency)
    {
        line.Description = input.Description!.Trim();
        line.UnitCode = input.UnitCode!.Trim();
        line.RequestedQuantity = input.RequestedQuantity;
        line.EstimatedUnitPrice = input.EstimatedUnitPrice;
        line.CatalogItemId = input.CatalogItemId;
        line.PreferredSupplierId = input.PreferredSupplierId;
        line.CategoryCode = Clean(input.CategoryCode);
        line.PurchaseType = Clean(input.PurchaseType);
        line.NeededBy = input.NeededBy;
        line.EstimatedCurrencyCode = currency;
        line.DiscountPercent = input.DiscountPercent;
        line.TaxCodeRef = Clean(input.TaxCodeRef);
        line.CostCenterRef = Clean(input.CostCenterRef);
        line.ProjectRef = Clean(input.ProjectRef);
        line.SpecificationDocumentRefId = input.SpecificationDocumentRefId;
    }

    private static object ToDetail(PurchaseRequest x) => new
    {
        x.Id, x.Number, x.Status, x.Version, x.RequesterWorkerId, x.Purpose, x.CurrencyCode,
        x.NeededBy, x.BranchRef, x.DepartmentRef, x.CostCenterRef, x.ProjectRef,
        x.DeliveryLocationRef, x.DeliveryAddressText, x.FundingSourceRef, x.NotesToApprover,
        x.CreatedAt, x.UpdatedAt,
        Lines = x.Lines.OrderBy(line => line.LineNumber).Select(line => new
        {
            line.Id, line.LineNumber, line.Version, line.Description, line.UnitCode,
            line.RequestedQuantity, line.EstimatedUnitPrice, line.EstimatedCurrencyCode,
            line.CatalogItemId, line.PreferredSupplierId, line.CategoryCode, line.PurchaseType,
            line.NeededBy, line.DiscountPercent, line.TaxCodeRef, line.CostCenterRef,
            line.ProjectRef, line.SpecificationDocumentRefId, line.ApprovalStatus,
            line.CancelledQuantity,
        }),
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static IResult Error(string code, string message, int status) =>
        Results.Json(new ApiError(code, message, []), statusCode: status);

    public sealed record RequestSummary(Guid Id, string Number, string Purpose, string Status,
        string CurrencyCode, DateOnly? NeededBy, DateTimeOffset CreatedAt, long Version, int LineCount);
    public record NewRequest(string? Purpose, string? CurrencyCode, DateOnly? NeededBy,
        string? CostCenterRef, string? ProjectRef, string? DeliveryLocationRef,
        string? DeliveryAddressText, string? FundingSourceRef, string? NotesToApprover,
        List<RequestLineInput>? Lines);
    public sealed record UpdateRequest(long Version, string? Purpose, string? CurrencyCode,
        DateOnly? NeededBy, string? CostCenterRef, string? ProjectRef,
        string? DeliveryLocationRef, string? DeliveryAddressText, string? FundingSourceRef,
        string? NotesToApprover, List<RequestLineInput>? Lines)
        : NewRequest(Purpose, CurrencyCode, NeededBy, CostCenterRef, ProjectRef,
            DeliveryLocationRef, DeliveryAddressText, FundingSourceRef, NotesToApprover, Lines);
    public sealed record RequestLineInput(Guid? Id, string? Description, string? UnitCode,
        decimal RequestedQuantity, decimal EstimatedUnitPrice, Guid? CatalogItemId,
        Guid? PreferredSupplierId, string? CategoryCode, string? PurchaseType,
        DateOnly? NeededBy, decimal? DiscountPercent, string? TaxCodeRef,
        string? CostCenterRef, string? ProjectRef, Guid? SpecificationDocumentRefId);
    public sealed record VersionCommand(long Version);
}
