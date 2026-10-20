using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private static object CapacityView(ReceivingCapacityView value) => new { value.Verified,
        orderRemainingQuantity = value.OrderRemainingQuantity is { } order ? Exact(order) : null,
        physicalRemainingQuantity = value.PhysicalRemainingQuantity is { } physical ? Exact(physical) : null,
        acceptableQuantity = value.AcceptableQuantity is { } acceptable ? Exact(acceptable) : null, value.Blocker };
    private static object UnknownCapacity(string code) => CapacityView(new(false, null, null, null, code));
    private static object SourceLabels(SourceLoad source)
    {
        using var snapshot = JsonDocument.Parse(source.Issued.CommercialSnapshotJson); var root = snapshot.RootElement;
        return new { orderNumber = root.GetProperty("Number").GetString(), supplierId = root.GetProperty("SupplierId").GetGuid(),
            supplierDisplayName = root.GetProperty("DisplayName").GetString(), supplierLegalName = root.GetProperty("LegalName").GetString(),
            source.Line.Description, source.Line.UnitCode, source.Order.CurrencyCode, source.Order.BranchRef,
            source.Issued.RevisionNumber, issuedVersionId = source.Issued.Id };
    }
    private static async Task<object> DocumentReferences(ProcurementDbContext db, string ownerType, Guid id, CancellationToken ct)
    {
        var references = await db.AttachmentRefs.AsNoTracking().Where(x => x.OwnerType == ownerType && x.OwnerId == id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new { x.Id, x.DocumentServiceId, x.DocumentVersion,
                x.FileName, x.Sha256, x.Classification, x.ExpiresOn }).ToArrayAsync(ct);
        // These are references only. Core owns bytes, scan result and download authorization;
        // its current owner adapter supports supplier_invoice, not receiving owners yet.
        return new { ownerModule = "procurement", ownerType, ownerId = id, references,
            contentAccessAvailable = false, uploadAvailable = false, status = "CoreReceivingOwnerAdapterUnavailable" };
    }
    private static async Task<ReceivingActionCandidate> CurrentHint(HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, FinalSource final, ReceivingLedger ledger, string permission, Guid? site,
        Func<ReceivingActorProjection, ReceivingActionCandidate> hint, CancellationToken ct)
    {
        try
        {
            var actor = await Actor(core, scope, http, auth, permission, final.Load.Order.BranchRef, site, ct);
            return hint(actor);
        }
        catch (ReceivingProblem ex) when (ex.Status == 403) { return new(permission switch { ControlledReceiving.InspectPermission => "Inspect", ControlledReceiving.AcceptPermission => "Accept",
                ControlledReceiving.CertifyPermission => "Certify", _ => "Record" }, false, "ReceivingCurrentPermissionRequired"); }
    }
    private static async Task<string> ReceiptRead(ProcurementDbContext db, ApprovalDbContext cases, HrmDbContext core,
        IAuthorizationService auth, ProcurementRequestScope scope, HttpContext http, GoodsReceipt header,
        GoodsReceiptLine[] lines, CancellationToken ct, bool purposeOnly = false)
    {
        var (states, inspections) = await ReceiptProjection(db, lines, ct);
        var node = JsonSerializer.SerializeToNode(View(header, lines, states, inspections), Json)!.AsObject();
        var hints = new List<object>(); var now = Now(); var verifiedSourceCount = 0;
        foreach (var line in lines.Where(x => x.Status != "Cancelled"))
        {
            object? retainedLabels = null;
            try
            {
                if (header.ExecutionVersion != 1 || header.IssuedVersionId is null) throw new ReceivingProblem("ReceivingLegacyOrUnsupportedEvidence", 409);
                var source = await Source(db, cases, scope, header.PurchaseOrderId, header.IssuedVersionId.Value, line.PurchaseOrderLineId, ct, lockSource: false);
                retainedLabels = SourceLabels(source); verifiedSourceCount++;
                var final = await Resolve(db, source, line.PhysicalPolicyVersionId, ct, persistState: false, requireCurrentPolicy: false);
                var ledger = await Ledger(db, final, ct);
                var actions = new List<ReceivingActionCandidate>();
                if (line.PhysicalObservationId is { } physical)
                {
                    actions.Add(await CurrentHint(core, scope, http, auth, final, ledger, ControlledReceiving.InspectPermission, header.SiteRef,
                        actor => ReceivingPositiveReadModel.Inspect(final.Source, final.Rules, actor, ledger, physical, now), ct));
                    if (!purposeOnly) actions.Add(await CurrentHint(core, scope, http, auth, final, ledger, ControlledReceiving.AcceptPermission, header.SiteRef,
                        actor => ReceivingPositiveReadModel.Accept(final.Source, final.Rules, actor, ledger, physical, now), ct));
                }
                else if (!purposeOnly) actions.Add(await CurrentHint(core, scope, http, auth, final, ledger, ControlledReceiving.RecordPermission, header.SiteRef,
                    actor => ReceivingPositiveReadModel.Record(final.Source, final.Rules, actor, ledger, header, line, now), ct));
                var capacity = ReceivingPositiveReadModel.Capacity(ledger, final.Rules, line.PhysicalObservationId);
                hints.Add(new { lineId = line.Id, sourceVersion = states.TryGetValue(final.State.Id, out var persisted) ? persisted.Version : 0,
                    labels = retainedLabels, capacity = CapacityView(capacity), actionCandidates = actions,
                    effects = ledger.Effects.Where(x => x.AcceptanceRecordId == line.Id).OrderBy(x => x.LedgerSequence)
                        .Select(x => new { x.Id, kind = x.Kind.ToString(), quantity = Exact(x.QuantityDelta), netAmount = Exact(x.NetAmountDelta),
                            x.LedgerSequence, x.ReceivingPolicyVersionId, x.EvidenceReference, x.OccurredAt }) });
            }
            catch (Exception ex) when (ex is ReceivingProblem or InvalidOperationException or JsonException or KeyNotFoundException)
            { hints.Add(new { lineId = line.Id, labels = retainedLabels, capacity = UnknownCapacity("ReceivingEvidenceNeedsReview"), actionCandidates = Array.Empty<ReceivingActionCandidate>(), blocker = "ReceivingEvidenceNeedsReview" }); }
        }
        var draftActions = new List<ReceivingActionCandidate>();
        if (!purposeOnly && verifiedSourceCount == lines.Count(x => x.Status != "Cancelled") && verifiedSourceCount > 0
            && header.Status == "Draft" && header.RecordedAt is null && header.CreatedBy == scope.SubjectId)
        {
            try
            {
                await Actor(core, scope, http, auth, "procurement-receipt-edit", header.BranchRef, header.SiteRef, ct);
                draftActions.Add(new("EditDraft", true, null)); draftActions.Add(new("CancelDraft", true, null));
            }
            catch (ReceivingProblem ex) when (ex.Status == 403) { draftActions.Add(new("EditDraft", false, "ReceivingCurrentPermissionRequired")); }
        }
        node["draftActionCandidates"] = JsonSerializer.SerializeToNode(draftActions, Json);
        node["readPurpose"] = purposeOnly ? "AssignedInspection" : "Receipt";
        node["lineReadiness"] = JsonSerializer.SerializeToNode(hints, Json);
        if (purposeOnly) { node.Remove("notes"); node.Remove("deliveryNoteRef"); }
        else node["documents"] = JsonSerializer.SerializeToNode(await DocumentReferences(db, "GoodsReceipt", header.Id, ct), Json);
        return node.ToJsonString(Json);
    }
    private static async Task<string> ServiceRead(ProcurementDbContext db, ApprovalDbContext cases, HrmDbContext core,
        IAuthorizationService auth, ProcurementRequestScope scope, HttpContext http, ServiceAcceptance row,
        CancellationToken ct, bool purposeOnly = false)
    {
        var state = row.ReceivingSourceStateId is { } stateId ? await db.ReceivingSourceStates.SingleOrDefaultAsync(x => x.Id == stateId, ct) : null;
        var node = JsonSerializer.SerializeToNode(View(row, state), Json)!.AsObject();
        node["readPurpose"] = purposeOnly ? "AssignedServiceCertification" : "Receipt";
        if (!purposeOnly)
        {
            node["notes"] = row.Notes;
            node["cancellationReason"] = row.Status == "Cancelled" ? row.Reason : null;
        } // Purpose-only tasks never expose narrative notes or cancellation reasons.
        try
        {
            if (row.ExecutionVersion != 1 || row.SourcePurchaseOrderId is null) throw new ReceivingProblem("ReceivingLegacyOrUnsupportedEvidence", 409);
            var source = await Source(db, cases, scope, row.SourcePurchaseOrderId.Value, row.IssuedBindingId, row.PurchaseOrderLineId, ct, lockSource: false);
            node["labels"] = JsonSerializer.SerializeToNode(SourceLabels(source), Json);
            if (!purposeOnly && row.CreatedBy == scope.SubjectId && row.Status == "PendingCertification" && row.CertifiedAt == null)
            {
                try
                {
                    await Actor(core, scope, http, auth, "procurement-receipt-edit", source.Order.BranchRef, source.Order.BranchRef, ct);
                    node["draftActionCandidates"] = JsonSerializer.SerializeToNode(new[] { new ReceivingActionCandidate("EditDraft", true, null),
                        new ReceivingActionCandidate("CancelDraft", true, null) }, Json);
                }
                catch (ReceivingProblem ex) when (ex.Status == 403) { node["draftActionCandidates"] = new JsonArray(); }
            }
            var final = await Resolve(db, source, row.PolicyVersionId, ct, persistState: false, requireCurrentPolicy: false); var ledger = await Ledger(db, final, ct);
            var candidate = await CurrentHint(core, scope, http, auth, final, ledger, ControlledReceiving.CertifyPermission, source.Order.BranchRef,
                actor => ReceivingPositiveReadModel.Certify(final.Source, final.Rules, actor, ledger, row, Now()), ct);
            node["capacity"] = JsonSerializer.SerializeToNode(CapacityView(ReceivingPositiveReadModel.Capacity(ledger, final.Rules, null)), Json);
            node["actionCandidates"] = JsonSerializer.SerializeToNode(new[] { candidate }, Json);
            node["effects"] = JsonSerializer.SerializeToNode(ledger.Effects.Where(x => x.AcceptanceRecordId == row.Id).OrderBy(x => x.LedgerSequence)
                .Select(x => new { x.Id, kind = x.Kind.ToString(), quantity = Exact(x.QuantityDelta), netAmount = Exact(x.NetAmountDelta),
                    x.LedgerSequence, x.ReceivingPolicyVersionId, x.EvidenceReference, x.OccurredAt }), Json);
        }
        catch (Exception ex) when (ex is ReceivingProblem or InvalidOperationException or JsonException or KeyNotFoundException)
        {
            node["capacity"] = JsonSerializer.SerializeToNode(UnknownCapacity("ReceivingEvidenceNeedsReview"), Json);
            node["actionCandidates"] = new JsonArray(); node["blocker"] = "ReceivingEvidenceNeedsReview";
        }
        if (!purposeOnly) node["documents"] = JsonSerializer.SerializeToNode(await DocumentReferences(db, "ServiceAcceptance", row.Id, ct), Json);
        return node.ToJsonString(Json);
    }
}
