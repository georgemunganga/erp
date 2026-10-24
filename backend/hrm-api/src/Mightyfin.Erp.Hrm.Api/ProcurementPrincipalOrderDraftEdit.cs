using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    // P33 Stage A: edit an unfunded internal Draft only. The original P30 source
    // binding and Create receipt remain immutable; later Finance/approval states
    // require a separate governed amendment contract.
    private static Task<IResult> Edit(Guid id, EditInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = Key(http);
            if (http.RequestServices.GetService<PrincipalOrderRevisionOptions>() is not { Enabled: true }
                || !await ProcurementPrincipalOrderDraftHistory.InstalledAsync(db, token)
                || http.RequestServices.GetService<IPrincipalOrderRevisionReadiness>() is not { } revisionReady
                || !await revisionReady.IsReadyAsync(db, token))
                throw new Problem("OrderDraftRevisionUnavailable", 503);
            var reason = Clean(input?.Reason);
            if (input is null || input.ExpectedOrderVersion < 1
                || input.OriginalDirectSourceSha256 is not { Length: 64 }
                || reason is null or { Length: > 1000 } || input.Draft is null
                || ProcurementOrderDraftRoutes.Validate(input.Draft) is not null)
                throw new Problem("OrderDraftRevisionInvalid", 422);
            _ = await Actor(core, scope, http, auth, "procurement-po-edit", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 1, operation = "Edit", id,
                input.ExpectedOrderVersion, input.OriginalDirectSourceSha256, reason, input.Draft });
            if (await Replay(db, core, scope, http, auth, "procurement-po-edit", "Edit",
                    key, fingerprint, id, token) is { } previous)
                return previous;
            var initial = await Owned(db, core, scope, http, auth, "procurement-po-edit", id, token);
            var requestIds = await db.PrincipalOrderSourceBindings.AsNoTracking()
                .Where(x => x.PurchaseOrderId == id).Select(x => x.PurchaseRequestId)
                .Distinct().OrderBy(x => x).ToArrayAsync(token);
            if (requestIds.Length is < 1 or > 100)
                throw new Problem("OrderOriginalSourceUnavailable", 409);
            foreach (var requestId in requestIds)
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", token);
            await db.Entry(initial).ReloadAsync(token);
            var order = await Owned(db, core, scope, http, auth, "procurement-po-edit", id, token);
            if (order.Status != "Draft" || order.CurrentRevision != 0
                || order.Version != input.ExpectedOrderVersion
                || order.OriginalDirectSourceSha256 != input.OriginalDirectSourceSha256
                || order.FinanceCommitmentRef is not null
                || input.Draft.SupplierId != order.SupplierId
                || input.Draft.CurrencyCode?.Trim().ToUpperInvariant() != order.CurrencyCode
                || await db.PrincipalOrderFundingEvidence.AnyAsync(x => x.PurchaseOrderId == id, token)
                || await db.PrincipalOrderSubmissions.AnyAsync(x => x.PurchaseOrderId == id, token)
                || await db.PrincipalOrderIssuedVersions.AnyAsync(x => x.PurchaseOrderId == id, token)
                || await db.PrincipalReceivingSourceStates.AnyAsync(x => x.PurchaseOrderId == id, token))
                throw new Problem("OrderDraftRevisionRequiresUnfundedDraft", 409);
            var supplier = await db.Suppliers.Include(x => x.Qualifications)
                .SingleOrDefaultAsync(x => x.Id == order.SupplierId, token);
            if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
                throw new Problem("OrderSupplierUnavailable", 409);
            var lines = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == id && x.IsActive)
                .OrderBy(x => x.LineNumber).ToArrayAsync(token);
            var bindings = await db.PrincipalOrderSourceBindings.Where(x => x.PurchaseOrderId == id)
                .ToArrayAsync(token);
            var requestedLines = input.Draft.Lines!;
            if (lines.Length is < 1 or > 100 || lines.Length != bindings.Length
                || requestedLines.Count != lines.Length
                || !requestIds.ToHashSet().SetEquals(bindings.Select(x => x.PurchaseRequestId))
                || requestedLines.Any(x => x?.Id is null || x.Id == Guid.Empty)
                || !requestedLines.Select(x => x.Id!.Value).Order()
                    .SequenceEqual(lines.Select(x => x.Id).Order()))
                throw new Problem("OrderDraftRevisionLineSetChanged", 409);
            var effective = await ProcurementPrincipalOrderDraftHistory.VerifyAsync(db, order,
                lines, bindings, token);
            var origin = await db.PrincipalOrderCommercialOrigins.SingleOrDefaultAsync(x =>
                x.PurchaseOrderId == id, token);
            var earlier = await db.PrincipalOrderDraftCommercialRevisions.Where(x =>
                x.PurchaseOrderId == id).OrderBy(x => x.RevisionNumber).ToArrayAsync(token);
            if (origin is null)
            {
                if (earlier.Length != 0) throw new Problem("OrderOriginalCommercialMissing", 409);
                var original = PrincipalOrderCommercialStates.Capture(order, lines);
                origin = new PrincipalOrderCommercialOrigin
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    PurchaseOrderId = id, OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256,
                    SnapshotJson = original, SnapshotSha256 = PrincipalOrderCommercialStates.Digest(original),
                    CapturedByPrincipalId = scope.SubjectId, CapturedAt = DateTimeOffset.UtcNow,
                    CaptureMode = "VerifiedFirstEdit"
                };
                db.AddScoped(origin);
            }
            var now = DateTimeOffset.UtcNow;
            var revisionId = Guid.CreateVersion7();
            var revisionNumber = checked(earlier.Length + 1);
            var allocations = await db.PurchaseRequestAllocations.Where(x =>
                x.TargetType == "PurchaseOrder" && x.TargetId == id).ToArrayAsync(token);
            if (allocations.Length != bindings.Length)
                throw new Problem("OrderOriginalSourceUnavailable", 409);
            foreach (var binding in bindings.OrderBy(x => x.PurchaseRequestId).ThenBy(x => x.PurchaseRequestLineId))
            {
                _ = await Actor(core, scope, http, auth, "procurement-po-edit", binding.BranchId, true, token);
                var source = await Source(db, cases, core, scope, http, auth, "procurement-po-edit",
                    binding.PurchaseRequestId, binding.PurchaseRequestLineId, token);
                if (source.Submission.Id != binding.SubmissionId
                    || source.Outcome.Id != binding.ApprovalOutcomeId
                    || source.CoreCase.Id != binding.CoreApprovalCaseId
                    || source.Method.Id != binding.MethodId
                    || source.Finance.Id != binding.FinanceSnapshotId
                    || source.Request.BranchRef != binding.BranchId
                    || source.Request.CurrencyCode != binding.CurrencyCode)
                    throw new Problem("OrderOriginalSourceUnavailable", 409);
                var finance = await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(
                    source.Request, source.Finance,
                    http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),
                    core, db, scope, now, token);
                if (source.Submission.FinanceEvidenceMode != finance.Mode
                    || source.Submission.ManualEvidenceAuthorityRef != finance.Reference)
                    throw new Problem("OrderFinanceAuthorityChanged", 409);
                var line = lines.Single(x => x.Id == binding.PurchaseOrderLineId);
                var requested = requestedLines.Single(x => x!.Id == line.Id)!;
                var allocation = allocations.SingleOrDefault(x => x.Id == binding.AllocationId);
                if (allocation is null || !effective.TryGetValue(binding.Id, out var before)
                    || allocation.Status != "Reserved" || allocation.Quantity != before.Quantity
                    || allocation.EstimatedValue != before.EstimatedValue
                    || requested.UnitCode?.Trim().ToUpperInvariant() != line.UnitCode)
                    throw new Problem("OrderOriginalSourceUnavailable", 409);
                ProcurementOrderDraftRoutes.ApplyLine(line, requested);
                if (line.UnitPrice > before.UnitPrice
                    || ExactDecimalArithmetic.Multiply(line.Quantity, line.UnitPrice) > before.EstimatedValue
                    || line.TaxAmount > before.TaxAmount)
                    throw new Problem("OrderDraftUpwardCommitmentRequiresReapproval", 409);
                if (await ProcurementCatalogClassificationBinding.CommercialIssue(db, order.SupplierId,
                    order.CurrencyCode, [(source.Line, line)], token) is not null)
                    throw new Problem("OrderCatalogPriceChanged", 409);
                PurchaseOrderSourceAllocation.ResizeReservation(source, order, line, allocation,
                    line.Quantity, reason, now);
                db.AddScoped(new PrincipalOrderEffectiveSourceSlice
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    PurchaseOrderId = id, RevisionId = revisionId, SourceBindingId = binding.Id,
                    PurchaseOrderLineId = line.Id, AllocationId = allocation.Id,
                    Quantity = line.Quantity, UnitPrice = line.UnitPrice,
                    TaxAmount = line.TaxAmount,
                    EstimatedValue = allocation.EstimatedValue, CurrencyCode = order.CurrencyCode
                });
            }
            order.ShipToRef = Clean(input.Draft.ShipToRef);
            order.BillToRef = Clean(input.Draft.BillToRef);
            order.PaymentTermRef = Clean(input.Draft.PaymentTermRef);
            order.ExpectedDeliveryOn = input.Draft.ExpectedDeliveryOn;
            order.UpdatedAt = now; // Advance the PO CAS even when only line values changed.
            var snapshot = PrincipalOrderCommercialStates.Capture(order, lines);
            var next = new PrincipalOrderDraftCommercialRevision
            {
                Id = revisionId, TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                PurchaseOrderId = id, OriginId = origin.Id,
                RevisionNumber = revisionNumber,
                PreviousRevisionId = earlier.LastOrDefault()?.Id,
                PreviousSnapshotSha256 = earlier.LastOrDefault()?.SnapshotSha256,
                OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256,
                Reason = reason, ActorPrincipalId = scope.SubjectId,
                CommandKey = key, CommandSha256 = fingerprint,
                ResultOrderVersion = checked(order.Version + 1), RecordedAt = now,
                SnapshotJson = snapshot, SnapshotSha256 = PrincipalOrderCommercialStates.Digest(snapshot)
            };
            db.AddScoped(next);
            Record(db, scope, order, "Edit", key, new { reason, revisionNumber = next.RevisionNumber,
                effectiveSnapshotSha256 = next.SnapshotSha256 });
            return await Save(db, scope, order, "Edit", key, fingerprint, token);
        }, ct);
}

public sealed class PrincipalOrderRevisionOptions
{
    public bool Enabled { get; set; }
}

public interface IPrincipalOrderRevisionReadiness
{
    Task<bool> IsReadyAsync(Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext owner,
        CancellationToken ct);
}

public static class PrincipalOrderRevisionCapability
{
    public static bool RetainedDraftEligible(Mightyfin.Erp.Procurement.Domain.PurchaseOrder order,
        bool hasFunding, bool hasSubmission, bool hasIssuedVersion, bool hasReceiving)
        => order.PrincipalDraftSchemaVersion==2 && order.Status=="Draft"
            && order.CurrentRevision==0 && order.FinanceCommitmentRef is null
            && order.OriginalDirectSourceSha256 is { Length: 64 }
            && !hasFunding && !hasSubmission && !hasIssuedVersion && !hasReceiving;

    public static async Task<bool> ReadyAsync(IServiceProvider services,
        Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext owner,
        bool pendingModelOrMigration, bool principalDraftReady, CancellationToken ct)
        => !pendingModelOrMigration && principalDraftReady
            && services.GetService<PrincipalOrderRevisionOptions>() is { Enabled: true }
            && services.GetService<IPrincipalOrderRevisionReadiness>() is { } readiness
            && await readiness.IsReadyAsync(owner, ct);
}
