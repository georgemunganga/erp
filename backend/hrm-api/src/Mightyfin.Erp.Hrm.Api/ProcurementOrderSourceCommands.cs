using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> ConvertSources(ConvertApprovedOrderInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Convert", actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-convert", token);
                var prior = await Receipt(db, scope, "Convert", key, fingerprint, null, token);
                if (prior is not null)
                {
                    // The command key proves the recorded actor/payload, not today's target
                    // visibility. Resolve the actual saved PO rather than trusting source IDs
                    // or the newly selected branch in this retried request body/context.
                    var target = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == prior.PurchaseOrderId, token)
                        ?? throw new OrderProblem("OrderNotFound", 404);
                    Scope(target, scope, true);
                    await CurrentRecordScope(core, scope, target.BranchRef, true, token);
                    return prior.ResponseJson;
                }
                if (input.Draft is null || input.Sources is null || input.Draft.Lines is null
                    || input.Sources.Count != input.Draft.Lines.Count || input.Sources.Count is < 1 or > 100
                    || input.Sources.Select(x => x.DraftLineIndex).Distinct().Count() != input.Sources.Count
                    || input.Sources.Any(x => x.DraftLineIndex < 0 || x.DraftLineIndex >= input.Draft.Lines.Count)
                    || input.Sources.Select(x => x.PurchaseRequestLineId).Distinct().Count() != input.Sources.Count
                    || input.Draft.Lines.Any(x => x.Id is not null)) Conflict("OrderSourceSelectionInvalid");
                var draft = input.Draft with { CurrencyCode = string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
                    ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, token) : input.Draft.CurrencyCode };
                if (ProcurementOrderDraftRoutes.Validate(draft) is not null) Conflict("OrderDraftInvalid");
                foreach (var requestId in input.Sources.Select(x => x.PurchaseRequestId).Distinct().Order())
                    await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, token);
                var order = new PurchaseOrder { Number = "PO-" + Guid.CreateVersion7().ToString("N"),
                    SupplierId = draft.SupplierId, CurrencyCode = draft.CurrencyCode!.Trim().ToUpperInvariant(),
                    BuyerSubjectId = scope.SubjectId, BranchRef = scope.WorkLocationId, Status = "Draft",
                    ShipToRef = Clean(draft.ShipToRef), BillToRef = Clean(draft.BillToRef),
                    PaymentTermRef = Clean(draft.PaymentTermRef), ExpectedDeliveryOn = draft.ExpectedDeliveryOn };
                db.AddScoped(order); // Scope is authoritative before domain allocation checks.
                var supplier = await db.Suppliers.Include(x => x.Qualifications).SingleOrDefaultAsync(x => x.Id == order.SupplierId, token);
                if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
                    Conflict("OrderSupplierUnavailable");
                var lines = new List<PurchaseOrderLine>(); var now = DateTimeOffset.UtcNow;
                foreach (var selection in input.Sources.OrderBy(x => x.DraftLineIndex))
                {
                    var source = await Source(db, cases, scope, selection.PurchaseRequestId, selection.PurchaseRequestLineId, token);
                    await CatalogClassification(db, [source.Line], token);
                    if (source.Request.Version != selection.ExpectedRequestVersion) Conflict("OrderSourceVersionChanged");
                    await CurrentRecordScope(core, scope, source.Request.BranchRef, true, token);
                    if (lines.Count == 0) order.BranchRef = source.Request.BranchRef;
                    else if (order.BranchRef != source.Request.BranchRef) Conflict("OrderMixedBranchesUnsupported");
                    var line = ProcurementOrderDraftRoutes.NewLine(order.Id, selection.DraftLineIndex + 1,
                        draft.Lines![selection.DraftLineIndex]);
                    line.PurchaseRequestLineId = source.Line.Id;
                    db.AddScoped(line);
                    db.AddScoped(PurchaseOrderSourceAllocation.Reserve(source, order, line, line.Quantity, key, scope.SubjectId, now));
                    lines.Add(line);
                }
                Audit(db, scope, order, "ApprovedSourcesConverted", new { sourceLines = input.Sources, commandKey = key });
                Event(db, order, "purchase_order.draft_created.v1", $"procurement.po.convert.{key:N}.v1",
                    new { orderId = order.Id, sourceLines = input.Sources.Select(x => x.PurchaseRequestLineId) });
                return await SaveReply(db, scope, order, "Convert", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json", statusCode: 201);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> UpdateSourceDraft(Guid id, OrderSourceDraftInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness,
        ProcurementApprovalTransaction transaction, IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "SourceEdit", id, actor = scope.SubjectId, input });
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var load = await Load(db, cases, scope, id, true, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-edit", token, load.Order.BranchRef, true);
                var prior = await Receipt(db, scope, "SourceEdit", key, fingerprint, id, token);
                if (prior is not null) return prior.ResponseJson;
                await CatalogClassification(db, load.Sources.Select(x => x.Source.Line), token);
                var order = load.Order;
                if (order.Version != input.ExpectedOrderVersion || order.Status != "Draft"
                    || order.FinanceCommitmentRef is not null)
                    Conflict("OrderDraftNeedsFundingResolution");
                var lastSubmission = await db.OrderSubmissions.Where(x => x.PurchaseOrderId == id)
                    .OrderByDescending(x => x.CommercialRevision).FirstOrDefaultAsync(token);
                if (lastSubmission is not null && !await db.OrderFinanceResolutions.AnyAsync(x => x.SubmissionId == lastSubmission.Id
                    && x.Outcome == "ReopenedDraft", token)) Conflict("OrderDraftNeedsFundingResolution");
                if (input.Draft is null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000
                    || ProcurementOrderDraftRoutes.Validate(input.Draft) is not null) Conflict("OrderDraftInvalid");
                var draft = input.Draft;
                if (draft.Lines is null || draft.Lines.Count != load.Lines.Count
                    || draft.Lines.Any(x => x.Id is null) || draft.Lines.Select(x => x.Id).Distinct().Count() != draft.Lines.Count
                    || draft.Lines.Any(x => !load.Lines.Any(l => l.Id == x.Id))) Conflict("OrderSourceLineIdentityChanged");
                if (!string.Equals(draft.CurrencyCode?.Trim(), order.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    Conflict("OrderSourceCurrencyChanged");
                var supplier = await db.Suppliers.Include(x => x.Qualifications).SingleOrDefaultAsync(x => x.Id == draft.SupplierId, token);
                if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
                    Conflict("OrderSupplierUnavailable");
                var now = DateTimeOffset.UtcNow;
                foreach (var inputLine in draft.Lines!)
                {
                    var source = load.Sources.Single(x => x.OrderLine.Id == inputLine.Id);
                    if (!string.Equals(inputLine.UnitCode, source.Source.Line.UnitCode, StringComparison.OrdinalIgnoreCase))
                        Conflict("OrderSourceUnitChanged");
                    ProcurementOrderDraftRoutes.ApplyLine(source.OrderLine, inputLine);
                    PurchaseOrderSourceAllocation.ResizeReservation(source.Source, order, source.OrderLine,
                        source.Allocation, inputLine.Quantity, input.Reason.Trim(), now);
                }
                order.SupplierId = draft.SupplierId; order.ShipToRef = Clean(draft.ShipToRef);
                order.BillToRef = Clean(draft.BillToRef); order.PaymentTermRef = Clean(draft.PaymentTermRef);
                order.ExpectedDeliveryOn = draft.ExpectedDeliveryOn; order.UpdatedAt = now;
                Audit(db, scope, order, "SourceDraftUpdated", new { reason = input.Reason.Trim(), commandKey = key });
                return await SaveReply(db, scope, order, "SourceEdit", key, fingerprint, token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
