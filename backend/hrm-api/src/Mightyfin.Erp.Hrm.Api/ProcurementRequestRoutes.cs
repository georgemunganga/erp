using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Npgsql;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Own purchase requests. Submission queues a policy and budget decision; it grants no buying authority.</summary>
public static class ProcurementRequestRoutes
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/requests");
        group.MapGet("", List).RequireAuthorization("procurement-request-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-request-read");
        group.MapGet("/{id:guid}/review", GetForReview).RequireAuthorization("procurement-request-return");
        group.MapPost("", Create).Accepts<NewRequest>("application/json").AddEndpointFilter(ProcurementCatalogFencing.Read).RequireAuthorization("procurement-request-create");
        group.MapPut("/{id:guid}", Update).Accepts<UpdateRequest>("application/json").AddEndpointFilter(ProcurementCatalogFencing.Read).RequireAuthorization("procurement-request-edit");
        group.MapPost("/{id:guid}/submit", Submit).AddEndpointFilter(ProcurementCatalogFencing.Read).RequireAuthorization("procurement-request-submit");
        group.MapPost("/{id:guid}/return-for-changes", ReturnForChanges)
            .RequireAuthorization("procurement-request-return");
        group.MapPost("/{id:guid}/withdraw", Withdraw).RequireAuthorization("procurement-request-edit");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        int? limit, int? offset, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var skip = Math.Clamp(offset ?? 0, 0, 100_000);
        var own = OwnRequests(db, scope).AsNoTracking();
        var count = await own.CountAsync(ct);
        var records = await own.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(skip).Take(take)
            .Select(x => new RequestSummary(x.Id, x.Number, x.Purpose, x.Status, x.CurrencyCode,
                x.NeededBy, x.CreatedAt, x.Version,
                x.Lines.Count(line => line.ApprovalStatus != "Cancelled"), x.BranchRef))
            .ToListAsync(ct);
        return Results.Ok(new { records, count, limit = take, offset = skip });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var request = await OwnRequests(db, scope).AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return request is null ? Error("not-found", "Request not found.", 404) : Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> GetForReview(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await ScopedRequests(db, scope).AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.Status == "Submitted", ct);
        if (request is null)
            return Error("not-found", "Request not found.", 404);
        return Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> Create(JsonElement body, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<NewRequest>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var currency = string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct)
            : input.CurrencyCode;
        var issue = Validate(input.Purpose, currency, input.Lines);
        if (issue is not null) return Error("validation-failed", issue, 422);
        if (input.Lines!.Any(x => x.Id.HasValue))
            return Error("validation-failed", "New request lines cannot carry an existing line ID.", 422);
        if (scope.PrincipalId is not { } principalId || principalId == Guid.Empty)
            return Error("account-unavailable", "An active system account is needed to create a request.", 403);
        var catalogItems = new Dictionary<Guid, CatalogItem>();
        var referenceIssue = await ValidateReferences(input.Lines!, currency!, db, catalogItems, ct);
        if (referenceIssue is not null) return Error("validation-failed", referenceIssue, 422);
        var catalogVersions = await CatalogVersions(input.Lines!, db, ct);

        var request = new PurchaseRequest
        {
            Number = "PR-" + Guid.CreateVersion7().ToString("N"),
            RequesterPrincipalId = principalId,
            RequesterWorkerId = scope.WorkerId,
            Purpose = input.Purpose!.Trim(),
            CurrencyCode = currency!.Trim().ToUpperInvariant(),
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
            request.Lines.Add(NewLine(request.Id, i + 1, input.Lines[i], request.CurrencyCode, catalogVersions, catalogItems));
        db.AddScoped(request);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/requests/{request.Id}", ToDetail(request));
    }

    private static async Task<IResult> Update(Guid id, JsonElement body, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var parsed=ProcurementOriginalValueInput.Read<UpdateRequest>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var request = await OwnRequests(db, scope).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        if (request.Status is not ("Draft" or "ReturnedForChanges"))
            return Error("request-locked", "Only a draft or returned request can be changed.", 409);
        if (request.ApprovalInstanceRef is not null && !await ReleasedPriorCase(db, request, ct))
            return Error("finance-release-pending", "Finance must confirm the previous reservation release before changes.", 409);
        if (request.Version != input.Version) return Error("version-conflict", "Refresh this request before saving changes.", 409);

        var currency = string.IsNullOrWhiteSpace(input.CurrencyCode) ? request.CurrencyCode : input.CurrencyCode;
        var issue = Validate(input.Purpose, currency, input.Lines);
        if (issue is not null) return Error("validation-failed", issue, 422);
        if (input.Lines!.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count()
            != input.Lines!.Count(x => x.Id.HasValue))
            return Error("validation-failed", "A request line was repeated.", 422);
        var catalogItems = new Dictionary<Guid, CatalogItem>();
        var referenceIssue = await ValidateReferences(input.Lines!, currency!, db, catalogItems, ct);
        if (referenceIssue is not null) return Error("validation-failed", referenceIssue, 422);
        var catalogVersions = await CatalogVersions(input.Lines!, db, ct);
        var existing = request.Lines.ToDictionary(x => x.Id);
        if (input.Lines.Any(x => x.Id.HasValue && !existing.ContainsKey(x.Id.Value)))
            return Error("validation-failed", "A request line does not belong to this request.", 422);
        if (input.Lines.Any(x => x.Id.HasValue && existing[x.Id.Value].ApprovalStatus == "Cancelled"))
            return Error("validation-failed", "A removed line cannot be restored; add a new line.", 422);

        request.Purpose = input.Purpose!.Trim();
        request.CurrencyCode = currency!.Trim().ToUpperInvariant();
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
                ApplyLine(existing[lineId], incoming, request.CurrencyCode, catalogVersions, catalogItems);
            else
            {
                var line = NewLine(request.Id, nextLine++, incoming, request.CurrencyCode, catalogVersions, catalogItems);
                db.AddScoped(line);
            }
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Error("version-conflict", "Refresh this request before saving changes.", 409); }
        return Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> Withdraw(Guid id, WithdrawCommand input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
        await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, ct);
        var request = await OwnRequests(db, scope).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        if (request.Status is not ("Draft" or "Submitted" or "ReturnedForChanges"))
            return Error("request-locked", "Only a draft, returned or submitted request can be withdrawn here.", 409);
        if (request.Status == "ReturnedForChanges" && request.ApprovalInstanceRef is not null
            && !await ReleasedPriorCase(db, request, ct))
            return Error("finance-release-pending", "Finance must confirm the previous reservation release before withdrawal.", 409);
        if (request.Version != input.Version) return Error("version-conflict", "Refresh this request before withdrawing it.", 409);

        if (request.Status == "Submitted" && (string.IsNullOrWhiteSpace(input.Reason)
            || input.Reason.Trim().Length > 1000))
            return Error("validation-failed", "Give a reason of up to 1,000 characters for withdrawing a submitted request.", 422);
        if (request.Status == "Submitted" && request.ApprovalInstanceRef is not null)
            return Error("request-locked", "Cancel the active approval before withdrawing this request.", 409);
        if (request.Status == "Submitted" && await db.BudgetCheckSnapshots.AsNoTracking()
                .AnyAsync(x => x.PurchaseRequestId == id && x.RequestVersion == request.Version
                    && x.ReservationStatus == "Confirmed", ct))
            return Error("budget-reservation-active",
                "Finance must release the confirmed reservation before this request can be withdrawn.", 409);
        var priorStatus = request.Status;
        request.Status = "Withdrawn";
        request.ApprovalState = "Withdrawn";
        foreach (var line in request.Lines.Where(x => x.ApprovalStatus != "Cancelled"))
        {
            line.ApprovalStatus = "Cancelled";
            line.CancelledQuantity = line.RequestedQuantity;
        }
        db.AddScoped(new AuditEvent
        {
            RecordType = "PurchaseRequest", RecordId = request.Id, Action = "Withdrawn",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                from = priorStatus, to = "Withdrawn", reason = input.Reason?.Trim(),
            }),
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(ToDetail(request));
        }
        catch (DbUpdateConcurrencyException) { return Error("version-conflict", "Refresh this request before withdrawing it.", 409); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Error("withdrawal-conflict", "The funding or request state changed. Refresh before withdrawing it.", 409); }
    }

    private static async Task<IResult> Submit(Guid id, VersionCommand input, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, ct);
        var request = await OwnRequests(db, scope).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Error("not-found", "Request not found.", 404);
        if (request.Status is not ("Draft" or "ReturnedForChanges"))
            return Error("request-locked", "Only a draft or returned request can be submitted.", 409);
        if (request.Version != input.Version)
            return Error("version-conflict", "Refresh this request before submitting it.", 409);

        var currentLines = request.Lines.Where(x => x.ApprovalStatus != "Cancelled").ToArray();
        if (currentLines.Length == 0)
            return Error("validation-failed", "Add at least one request line before submitting.", 422);
        if (request.NeededBy is null && currentLines.Any(x => x.NeededBy is null))
            return Error("validation-failed", "Choose a needed-by date for each line or the whole request.", 422);
        if (currentLines.Any(x => x.NeededBy is { } date && date < DateOnly.FromDateTime(DateTime.UtcNow)))
            return Error("validation-failed", "A request line has a needed-by date in the past.", 422);
        if (request.NeededBy is { } neededBy && neededBy < DateOnly.FromDateTime(DateTime.UtcNow))
            return Error("validation-failed", "The needed-by date is in the past.", 422);
        var referenceIssue = await ValidateStoredReferences(currentLines, request.CurrencyCode, db, ct);
        if (referenceIssue is not null) return Error("validation-failed", referenceIssue, 422);
        decimal estimatedTotal;
        try { estimatedTotal = currentLines.Sum(x => x.RequestedQuantity * x.EstimatedUnitPrice); }
        catch (OverflowException) { return Error("validation-failed", "The estimated amount is too large.", 422); }
        if (request.ApprovalInstanceRef is not null && !await ReleasedPriorCase(db, request, ct))
            return Error("finance-release-pending", "Finance must confirm the previous reservation release before resubmitting.", 409);
        var priorStatus = request.Status;
        request.ApprovalInstanceRef = null;
        foreach (var line in currentLines)
        {
            if (line.AllocatedQuantity != 0 || line.OrderedQuantity != 0 || line.AcceptedQuantity != 0)
                return Error("request-committed", "An ordered or received request cannot be resubmitted here.", 409);
            line.ApprovalStatus = "Pending";
            line.ApprovedQuantity = 0;
            line.Route = "Unassigned";
        }
        request.Status = "Submitted";
        request.ApprovalState = "PendingPolicyAndBudget";
        request.SubmittedAt = DateTimeOffset.UtcNow;
        request.SubmittedVersion = request.Version + 1;
        request.ReturnedAt = null;
        request.ReturnedBySubjectId = null;
        request.ReturnReason = null;
        var policyAssessment = await ProcurementPolicyEvaluation.Assess(request, db, scope,
            "Submission", ct);
        db.AddScoped(new AuditEvent
        {
            RecordType = "PurchaseRequest", RecordId = request.Id, Action = "Submitted",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                from = priorStatus, to = "Submitted", version = request.SubmittedVersion,
                lineCount = currentLines.Length, estimatedTotal, request.CurrencyCode,
                policyStatus = policyAssessment.Status,
                snapshot = SubmissionSnapshot(request, currentLines),
            }),
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error("version-conflict", "Refresh this request before submitting it.", 409);
        }
        catch (DbUpdateException)
        { return Error("submission-conflict", "Another submission or buying-rule check was recorded. Refresh the request.", 409); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Error("version-conflict", "A selected item or catalog price changed. Refresh before submitting.", 409); }
        return Results.Ok(ToDetail(request));
    }

    private static async Task<IResult> ReturnForChanges(Guid id, ReturnInput input,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, ct);
            var request = await ScopedRequests(db, scope).Include(x => x.Lines)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            if (request is null)
                return Error("not-found", "Request not found.", 404);
            if (PurchaseRequestOwnership.IsOwner(request, scope.PrincipalId))
                return Error("self-review", "The requester cannot return their own request for changes.", 403);
            if (request.Status != "Submitted" || request.ApprovalInstanceRef is not null)
                return Error("request-locked", "Only a submitted request without an active approval can be returned here.", 409);
            if (request.Version != input.Version)
                return Error("version-conflict", "Refresh this request before returning it.", 409);
            var reason = input.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                return Error("validation-failed", "Explain what needs to change in up to 1,000 characters.", 422);
            if (await db.BudgetCheckSnapshots.AsNoTracking().AnyAsync(x =>
                    x.PurchaseRequestId == id && x.RequestVersion == request.Version
                    && x.ReservationStatus == "Confirmed", ct))
                return Error("budget-reservation-active",
                    "Finance must release the confirmed reservation before this request can be returned.", 409);

            request.Status = "ReturnedForChanges";
            request.ApprovalState = "ReturnedForChanges";
            request.ReturnedAt = DateTimeOffset.UtcNow;
            request.ReturnedBySubjectId = scope.SubjectId;
            request.ReturnReason = reason;
            db.AddScoped(new AuditEvent
            {
                RecordType = "PurchaseRequest", RecordId = id, Action = "ReturnedForChanges",
                ActorSubjectId = scope.SubjectId,
                ChangedFieldsJson = JsonSerializer.Serialize(new
                {
                    from = "Submitted", to = "ReturnedForChanges", requestVersion = input.Version,
                    reason, returnedAt = request.ReturnedAt,
                    submittedSnapshot = SubmissionSnapshot(request,
                        request.Lines.Where(x => x.ApprovalStatus != "Cancelled")
                            .OrderBy(x => x.LineNumber).ToArray()),
                }),
            });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(ToDetail(request));
        }
        catch (DbUpdateConcurrencyException)
        { return Error("version-conflict", "Refresh this request before returning it.", 409); }
        catch (Exception error) when (ProcurementPostgresFailures.IsConcurrency(error))
        { return Error("return-conflict", "The funding or request state changed. Refresh before returning it.", 409); }
    }

    private static async Task<string?> ValidateStoredReferences(PurchaseRequestLine[] lines,
        string currency, ProcurementDbContext db, CancellationToken ct)
    {
        var suppliers = lines.Where(x => x.PreferredSupplierId.HasValue).Select(x => x.PreferredSupplierId!.Value).Distinct().ToArray();
        if (await ProcurementCatalogClassificationBinding.StoredIssue(db, lines, ct, currency) is not null)
            return "A selected catalog item or price is unavailable or changed. Edit the draft to select the current catalog classification and price.";
        if (suppliers.Length > 0)
        {
            var selected = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                .Where(x => suppliers.Contains(x.Id)).ToListAsync(ct);
            if (selected.Count != suppliers.Length || selected.Any(x => !SupplierEligibility.CanReceiveNewOrders(x, DateTimeOffset.UtcNow)))
                return "A preferred vendor is no longer available or its qualification has expired.";
        }
        return null;
    }

    private static async Task<string?> ValidateReferences(IEnumerable<RequestLineInput> lines,
        string currency, ProcurementDbContext db, IDictionary<Guid, CatalogItem> catalogItems, CancellationToken ct)
    {
        var submitted = lines.ToArray();
        var itemIds = submitted.Where(x => x.CatalogItemId.HasValue).Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        var supplierIds = submitted.Where(x => x.PreferredSupplierId.HasValue).Select(x => x.PreferredSupplierId!.Value).Distinct().ToArray();
        var ownedItems = await db.CatalogItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var item in ownedItems) catalogItems.Add(item.Id, item);
        foreach (var line in submitted.Where(x => x.CatalogItemId.HasValue))
            if (!catalogItems.TryGetValue(line.CatalogItemId!.Value, out var item)
                || CatalogRequestClassification.Bind(item, line.CategoryCode, line.PurchaseType, allowMissing: true) is null)
                return "Choose an available item with its catalog category and purchase type; catalog classification cannot be overridden.";
        if (supplierIds.Length > 0)
        {
            var selected = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                .Where(x => supplierIds.Contains(x.Id)).ToListAsync(ct);
            if (selected.Count != supplierIds.Length || selected.Any(x => !SupplierEligibility.CanReceiveNewOrders(x, DateTimeOffset.UtcNow)))
                return "One or more preferred vendors are unavailable or have expired qualifications.";
        }
        var catalogIds = submitted.Where(x => x.CatalogEntryId.HasValue)
            .Select(x => x.CatalogEntryId!.Value).Distinct().ToArray();
        if (catalogIds.Length > 0)
        {
            var entries = await db.CatalogEntries.AsNoTracking().Where(x => catalogIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            foreach (var line in submitted.Where(x => x.CatalogEntryId.HasValue))
            {
                if (!entries.TryGetValue(line.CatalogEntryId!.Value, out var entry)
                    || !CatalogRequestClassification.OfferMatches(entry, line.CatalogItemId, line.PreferredSupplierId,
                        line.UnitCode, currency.Trim().ToUpperInvariant(), line.RequestedQuantity,
                        line.EstimatedUnitPrice, publishedVersion: null, DateTimeOffset.UtcNow))
                    return "Choose an available catalog price with the same item, vendor, unit, currency and price.";
            }
        }
        return null;
    }

    private static async Task<Dictionary<Guid, int>> CatalogVersions(IEnumerable<RequestLineInput> lines,
        ProcurementDbContext db, CancellationToken ct)
    {
        var ids = lines.Where(x => x.CatalogEntryId.HasValue).Select(x => x.CatalogEntryId!.Value)
            .Distinct().ToArray();
        return await db.CatalogEntries.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PublishedVersion, ct);
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
            if (!ProcurementPolicyCode.TryNormalizeOptional(line.CategoryCode, out _))
                return "Category code must contain up to 100 letters, digits or underscores.";
            if (!ProcurementPolicyCode.TryNormalizeOptional(line.PurchaseType, out var purchaseType)
                || purchaseType is { Length: > 40 })
                return "Purchase type must contain up to 40 letters, digits or underscores.";
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

    private static PurchaseRequestLine NewLine(Guid requestId, int number, RequestLineInput input, string currency,
        IReadOnlyDictionary<Guid, int> catalogVersions, IReadOnlyDictionary<Guid, CatalogItem> catalogItems)
    {
        var line = new PurchaseRequestLine { PurchaseRequestId = requestId, LineNumber = number };
        ApplyLine(line, input, currency, catalogVersions, catalogItems);
        return line;
    }

    private static void ApplyLine(PurchaseRequestLine line, RequestLineInput input, string currency,
        IReadOnlyDictionary<Guid, int> catalogVersions, IReadOnlyDictionary<Guid, CatalogItem> catalogItems)
    {
        line.Description = input.Description!.Trim();
        line.UnitCode = input.UnitCode!.Trim();
        line.RequestedQuantity = input.RequestedQuantity;
        line.EstimatedUnitPrice = input.EstimatedUnitPrice;
        line.CatalogItemId = input.CatalogItemId;
        line.CatalogEntryId = input.CatalogEntryId;
        line.CatalogPublishedVersion = input.CatalogEntryId is Guid catalogEntryId
            ? catalogVersions[catalogEntryId] : null;
        line.PreferredSupplierId = input.PreferredSupplierId;
        var classification = input.CatalogItemId is Guid itemId
            ? CatalogRequestClassification.Bind(catalogItems[itemId], input.CategoryCode, input.PurchaseType, allowMissing: true)
                ?? throw new InvalidOperationException("CatalogClassificationChanged")
            : null;
        line.CategoryCode = classification?.CategoryCode ?? ProcurementPolicyCode.NormalizeOptional(input.CategoryCode);
        line.PurchaseType = classification?.PurchaseType ?? ProcurementPolicyCode.NormalizeOptional(input.PurchaseType);
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
        x.Id, x.Number, x.Status, x.ApprovalState, x.ApprovalInstanceRef, x.SubmittedAt, x.SubmittedVersion,
        x.ReturnedAt, x.ReturnedBySubjectId, x.ReturnReason,
        x.Version, x.RequesterPrincipalId, x.RequesterWorkerId, x.Purpose, x.CurrencyCode,
        x.NeededBy, x.BranchRef, x.DepartmentRef, x.CostCenterRef, x.ProjectRef,
        x.DeliveryLocationRef, x.DeliveryAddressText, x.FundingSourceRef, x.NotesToApprover,
        x.CreatedAt, x.UpdatedAt, RequestEditVersion = 1,
        Lines = x.Lines.OrderBy(line => line.LineNumber).Select(line => new
        {
            line.Id, line.LineNumber, line.Version, line.Description, line.UnitCode,
            line.RequestedQuantity, line.ApprovedQuantity, line.EstimatedUnitPrice, line.EstimatedCurrencyCode,
            line.CatalogItemId, line.CatalogEntryId, line.CatalogPublishedVersion,
            line.PreferredSupplierId, line.CategoryCode, line.PurchaseType,
            line.NeededBy, line.DiscountPercent, line.TaxCodeRef, line.CostCenterRef,
            line.ProjectRef, line.SpecificationDocumentRefId, line.ApprovalStatus,
            line.CancelledQuantity,
            ExactAmounts = new {
                RequestedQuantity = line.RequestedQuantity.ToString("0.####", CultureInfo.InvariantCulture),
                EstimatedUnitPrice = line.EstimatedUnitPrice.ToString("0.####", CultureInfo.InvariantCulture),
                DiscountPercent = line.DiscountPercent?.ToString("0.####", CultureInfo.InvariantCulture),
            },
        }),
    };

    private static object SubmissionSnapshot(PurchaseRequest request, PurchaseRequestLine[] lines) => new
    {
        request.Id, request.RequesterPrincipalId, request.RequesterWorkerId, request.SubmittedVersion, request.Purpose,
        request.CurrencyCode, request.NeededBy, request.BranchRef, request.DepartmentRef,
        request.CostCenterRef, request.ProjectRef, request.DeliveryLocationRef,
        request.DeliveryAddressText, request.FundingSourceRef, request.NotesToApprover,
        lines = lines.Select(line => new
        {
            line.Id, line.LineNumber, line.Description, line.UnitCode,
            line.RequestedQuantity, line.EstimatedUnitPrice, line.DiscountPercent,
            line.CategoryCode, line.PurchaseType, line.NeededBy,
            line.CatalogItemId, line.CatalogEntryId, line.CatalogPublishedVersion,
            line.PreferredSupplierId, line.CostCenterRef, line.ProjectRef,
            line.SpecificationDocumentRefId,
        }).ToArray(),
    };

    private static IQueryable<PurchaseRequest> OwnRequests(ProcurementDbContext db, ProcurementRequestScope scope) =>
        ScopedRequests(db, scope).Where(PurchaseRequestOwnership.OwnedBy(scope.PrincipalId));

    private static IQueryable<PurchaseRequest> ScopedRequests(ProcurementDbContext db, ProcurementRequestScope scope)
    {
        var requests = db.PurchaseRequests;
        if (scope.PrincipalId is null || scope.PrincipalId == Guid.Empty) return requests.Where(x => false);
        IQueryable<PurchaseRequest> scoped = requests;
        if (scope.IsExplicitAllBranches)
        {
            var permitted = scope.PermittedWorkLocationIds.ToArray();
            // Retain existing company-wide authority for legacy own requests
            // without a branch. Confined users never inherit those records.
            var companyWide = !scope.IsConfined;
            scoped = scoped.Where(x => x.BranchRef.HasValue
                ? permitted.Contains(x.BranchRef.Value) : companyWide);
        }
        else if (scope.WorkLocationId.HasValue || scope.IsConfined)
            scoped = scoped.Where(x => scope.WorkLocationId != null && x.BranchRef == scope.WorkLocationId);
        if (scope.OrgUnitId.HasValue) scoped = scoped.Where(x => x.DepartmentRef == scope.OrgUnitId);
        return scoped;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static IResult Error(string code, string message, int status) =>
        Results.Json(new ApiError(code, message, []), statusCode: status);

    public sealed record RequestSummary(Guid Id, string Number, string Purpose, string Status,
        string CurrencyCode, DateOnly? NeededBy, DateTimeOffset CreatedAt, long Version, int LineCount, Guid? BranchRef = null);
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
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal RequestedQuantity,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal EstimatedUnitPrice, Guid? CatalogItemId,
        Guid? PreferredSupplierId, string? CategoryCode, string? PurchaseType,
        DateOnly? NeededBy, [property: JsonConverter(typeof(RequestDiscountJsonConverter))] decimal? DiscountPercent, string? TaxCodeRef,
        string? CostCenterRef, string? ProjectRef, Guid? SpecificationDocumentRefId,
        Guid? CatalogEntryId = null);
    public sealed record VersionCommand(long Version);
    private static async Task<bool> ReleasedPriorCase(ProcurementDbContext db, PurchaseRequest request, CancellationToken ct)
    {
        if (request.Status != "ReturnedForChanges" || !Guid.TryParse(request.ApprovalInstanceRef, out var caseId)) return false;
        var submissionId = await ReleasedPriorSubmissionIds(db, request.Id, caseId, request.SubmittedVersion)
            .SingleOrDefaultAsync(ct);
        if (submissionId is null) return false;
        return await db.FinanceReleaseRequests.AsNoTracking().Where(x => x.SubmissionId == submissionId.Value
            && x.CoreApprovalCaseId == caseId && x.Outcome == "Returned")
            .Join(db.FinanceReleaseConfirmations.AsNoTracking(), r => r.Id, c => c.ReleaseRequestId,
                (r, c) => new { r, c }).AnyAsync(x => x.c.ReservationRef == x.r.ReservationRef
                    && x.c.ReleasedAmount == x.r.ReservedAmount && x.c.CurrencyCode == x.r.CurrencyCode, ct);
    }

    // Draft CRUD needs only the old scoped source ID. Do not materialize new Finance identity
    // columns here or make historical return/release proof depend on positive-use schema readiness.
    internal static IQueryable<Guid?> ReleasedPriorSubmissionIds(ProcurementDbContext db, Guid requestId,
        Guid caseId, long? submittedVersion) => db.ApprovalSubmissions.AsNoTracking()
        .Where(x => x.PurchaseRequestId == requestId && x.CoreApprovalCaseId == caseId
            && x.SubmittedBusinessVersion == submittedVersion).Select(x => (Guid?)x.Id);

    public sealed record ReturnInput(long Version, string? Reason);
    public sealed record WithdrawCommand(long Version, string? Reason = null);
}
