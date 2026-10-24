using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Canonical commercial state for the immutable original and each
/// effective unfunded Draft revision. It is separate from the issued snapshot.</summary>
public static class PrincipalOrderCommercialStates
{
    public sealed record Line(Guid Id, int LineNumber, Guid? PurchaseRequestLineId, Guid? CatalogItemId,
        string Description, string UnitCode, string Quantity, string UnitPrice, string TaxAmount,
        string? CostCenterRef, string? ProjectRef, DateOnly? ExpectedDeliveryOn);

    public sealed record State(Guid Id, string TenantId, Guid LegalEntityId, string Number,
        Guid SupplierId, string BuyerSubjectId, Guid BranchRef, string CurrencyCode,
        string? ShipToRef, string? BillToRef, string? PaymentTermRef, DateOnly? ExpectedDeliveryOn,
        string OriginalDirectSourceSha256, Line[] Lines);

    public static string Capture(PurchaseOrder order, IReadOnlyCollection<PurchaseOrderLine> lines)
    {
        if (order.PrincipalDraftSchemaVersion != 2 || order.OriginalDirectSourceSha256 is not { Length: 64 }
            || order.BranchRef is not { } branch || branch == Guid.Empty
            || lines.Count is < 1 or > 100 || lines.Any(x => !x.IsActive || x.PurchaseOrderId != order.Id)
            || lines.Select(x => x.Id).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("PrincipalOrderCommercialStateInvalid");
        return JsonSerializer.Serialize(new State(order.Id, order.TenantId, order.LegalEntityId,
            order.Number, order.SupplierId, order.BuyerSubjectId, branch, order.CurrencyCode,
            order.ShipToRef, order.BillToRef, order.PaymentTermRef, order.ExpectedDeliveryOn,
            order.OriginalDirectSourceSha256, lines.OrderBy(x => x.LineNumber).Select(x => new Line(
                x.Id, x.LineNumber, x.PurchaseRequestLineId, x.CatalogItemId, x.Description, x.UnitCode,
                ProcurementEvidenceEncoding.Decimal(x.Quantity), ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
                ProcurementEvidenceEncoding.Decimal(x.TaxAmount), x.CostCenterRef, x.ProjectRef,
                x.ExpectedDeliveryOn)).ToArray()));
    }

    public static string Digest(string snapshot) => ProcurementEvidenceEncoding.Digest(snapshot);

    public static (PurchaseOrder Order, PurchaseOrderLine[] Lines) Restore(string snapshot)
    {
        State state;
        try { state = JsonSerializer.Deserialize<State>(snapshot) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("PrincipalOrderCommercialSnapshotInvalid"); }
        if (state.Lines is null || state.Lines.Length is < 1 or > 100
            || state.Id == Guid.Empty || state.LegalEntityId == Guid.Empty || state.BranchRef == Guid.Empty
            || state.SupplierId == Guid.Empty || state.OriginalDirectSourceSha256 is not { Length: 64 })
            throw new InvalidOperationException("PrincipalOrderCommercialSnapshotInvalid");
        var order = new PurchaseOrder
        {
            Id = state.Id, TenantId = state.TenantId, LegalEntityId = state.LegalEntityId,
            PrincipalDraftSchemaVersion = 2, OriginalDirectSourceSha256 = state.OriginalDirectSourceSha256,
            Number = state.Number, SupplierId = state.SupplierId, BuyerSubjectId = state.BuyerSubjectId,
            BranchRef = state.BranchRef, CurrencyCode = state.CurrencyCode, ShipToRef = state.ShipToRef,
            BillToRef = state.BillToRef, PaymentTermRef = state.PaymentTermRef,
            ExpectedDeliveryOn = state.ExpectedDeliveryOn
        };
        var lines = state.Lines.Select(x => new PurchaseOrderLine
        {
            Id = x.Id, TenantId = state.TenantId, LegalEntityId = state.LegalEntityId,
            PurchaseOrderId = state.Id, LineNumber = x.LineNumber, PurchaseRequestLineId = x.PurchaseRequestLineId,
            CatalogItemId = x.CatalogItemId, Description = x.Description, UnitCode = x.UnitCode,
            Quantity = Exact(x.Quantity), UnitPrice = Exact(x.UnitPrice), TaxAmount = Exact(x.TaxAmount),
            CostCenterRef = x.CostCenterRef, ProjectRef = x.ProjectRef,
            ExpectedDeliveryOn = x.ExpectedDeliveryOn, IsActive = true
        }).ToArray();
        if (Capture(order, lines) != snapshot)
            throw new InvalidOperationException("PrincipalOrderCommercialSnapshotInvalid");
        return (order, lines);
    }

    public static void VerifyOrigin(PrincipalOrderCommercialOrigin origin, PurchaseOrder current,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings)
    {
        if (origin.SchemaVersion != 1 || origin.PurchaseOrderId != current.Id
            || origin.TenantId != current.TenantId || origin.LegalEntityId != current.LegalEntityId
            || origin.OriginalDirectSourceSha256 != current.OriginalDirectSourceSha256
            || origin.SnapshotSha256 != Digest(origin.SnapshotJson)
            || origin.CaptureMode is not ("Create" or "VerifiedFirstEdit")
            || origin.CapturedAt == default || !ProcurementPrincipalEvidence.Canonical(origin.CapturedByPrincipalId))
            throw new InvalidOperationException("PrincipalOrderOriginalCommercialInvalid");
        var (original, lines) = Restore(origin.SnapshotJson);
        PrincipalOrderDraftPins.Verify(original, lines, bindings);
        if (original.Id != current.Id || original.TenantId != current.TenantId
            || original.LegalEntityId != current.LegalEntityId || original.SupplierId != current.SupplierId
            || original.BuyerSubjectId != current.BuyerSubjectId
            || original.BranchRef != current.BranchRef || original.CurrencyCode != current.CurrencyCode
            || original.Number != current.Number)
            throw new InvalidOperationException("PrincipalOrderOriginalCommercialInvalid");
    }

    public static void VerifyRevision(PurchaseOrder current, IReadOnlyCollection<PurchaseOrderLine> lines,
        PrincipalOrderCommercialOrigin origin, IReadOnlyList<PrincipalOrderDraftCommercialRevision> revisions,
        IReadOnlyCollection<PrincipalOrderEffectiveSourceSlice> slices,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings,
        IReadOnlyCollection<PurchaseRequestAllocation> allocations)
    {
        VerifyOrigin(origin, current, bindings);
        if (revisions.Count == 0 || revisions.Count > 1000 || slices.Count != revisions.Count * bindings.Count)
            throw new InvalidOperationException("PrincipalOrderDraftRevisionMissing");
        var (originalOrder, originalLines) = Restore(origin.SnapshotJson);
        var originalUnits = originalLines.ToDictionary(x => x.Id, x => x.UnitCode);
        Guid? previous = null; string? previousSha = null;
        foreach (var pair in revisions.OrderBy(x => x.RevisionNumber).Select((revision, index) => (revision, index)))
        {
            var revision = pair.revision;
            if (revision.SchemaVersion != 1 || revision.PurchaseOrderId != current.Id || revision.OriginId != origin.Id
                || revision.TenantId != current.TenantId || revision.LegalEntityId != current.LegalEntityId
                || revision.RevisionNumber != pair.index + 1 || revision.PreviousRevisionId != previous
                || revision.PreviousSnapshotSha256 != previousSha
                || revision.OriginalDirectSourceSha256 != current.OriginalDirectSourceSha256
                || revision.SnapshotSha256 != Digest(revision.SnapshotJson)
                || revision.CommandKey == Guid.Empty || revision.CommandSha256 is not { Length: 64 }
                || revision.ResultOrderVersion != checked(revision.RevisionNumber + 1L)
                || revision.RecordedAt == default
                || string.IsNullOrWhiteSpace(revision.Reason)
                || !ProcurementPrincipalEvidence.Canonical(revision.ActorPrincipalId))
                throw new InvalidOperationException("PrincipalOrderDraftRevisionInvalid");
            var (versionOrder, versionLines) = Restore(revision.SnapshotJson);
            if (versionOrder.Id != current.Id || versionOrder.Number != current.Number
                || versionOrder.BuyerSubjectId != current.BuyerSubjectId || versionOrder.SupplierId != originalOrder.SupplierId
                || versionOrder.BranchRef != current.BranchRef || versionOrder.CurrencyCode != current.CurrencyCode
                || versionLines.Length != bindings.Count)
                throw new InvalidOperationException("PrincipalOrderDraftRevisionInvalid");
            var versionSlices = slices.Where(x => x.RevisionId == revision.Id).ToArray();
            if (versionSlices.Length != bindings.Count) throw new InvalidOperationException("PrincipalOrderDraftSliceInvalid");
            foreach (var binding in bindings)
            {
                var slice = versionSlices.SingleOrDefault(x => x.SourceBindingId == binding.Id);
                var line = versionLines.SingleOrDefault(x => x.Id == binding.PurchaseOrderLineId);
                if (slice is null || line is null || slice.SchemaVersion != 1
                    || slice.PurchaseOrderId != current.Id || slice.PurchaseOrderLineId != line.Id
                    || slice.AllocationId != binding.AllocationId || slice.TenantId != current.TenantId
                    || slice.LegalEntityId != current.LegalEntityId || slice.CurrencyCode != current.CurrencyCode
                    || slice.Quantity != line.Quantity || slice.UnitPrice != line.UnitPrice || slice.TaxAmount != line.TaxAmount
                    || slice.EstimatedValue != ExactDecimalArithmetic.Multiply(line.Quantity, line.UnitPrice)
                    || line.PurchaseRequestLineId != binding.PurchaseRequestLineId
                    || !originalUnits.TryGetValue(line.Id, out var originalUnit) || line.UnitCode != originalUnit)
                    throw new InvalidOperationException("PrincipalOrderDraftSliceInvalid");
            }
            previous = revision.Id; previousSha = revision.SnapshotSha256;
        }
        var latest = revisions.OrderByDescending(x => x.RevisionNumber).First();
        if (latest.SnapshotJson != Capture(current, lines)) throw new InvalidOperationException("PrincipalOrderDraftCurrentChanged");
        var currentSlices = slices.Where(x => x.RevisionId == latest.Id).ToArray();
        foreach (var slice in currentSlices)
        {
            var allocation = allocations.SingleOrDefault(x => x.Id == slice.AllocationId);
            if (allocation is null || allocation.Quantity != slice.Quantity || allocation.EstimatedValue != slice.EstimatedValue
                || allocation.TargetId != current.Id || allocation.TargetType != "PurchaseOrder"
                || allocation.CurrencyCode != slice.CurrencyCode)
                throw new InvalidOperationException("PrincipalOrderDraftAllocationChanged");
        }
    }

    /// <summary>Retained command proof for every effective edit. A same-save guard
    /// alone cannot prove old receipts still exist after reload or cancellation.</summary>
    public static void VerifyReceipts(PurchaseOrder current, PrincipalOrderCommercialOrigin? origin,
        IReadOnlyList<PrincipalOrderDraftCommercialRevision> revisions,
        IReadOnlyList<PrincipalOrderDraftReceipt> receipts)
    {
        if (origin is null)
        {
            if (revisions.Count != 0 || receipts.Count != 0)
                throw new InvalidOperationException("PrincipalOrderDraftRevisionWithoutOrigin");
            return;
        }
        if (origin.CaptureMode == "VerifiedFirstEdit" && revisions.Count == 0
            || origin.CaptureMode == "Create" && revisions.Count == 0 && receipts.Count != 0
            || revisions.Count != receipts.Count
            || receipts.Select(x => x.CommandKey).Distinct().Count() != receipts.Count)
            throw new InvalidOperationException("PrincipalOrderDraftReceiptSetChanged");
        long lastVersion = 1;
        foreach (var revision in revisions.OrderBy(x => x.RevisionNumber))
        {
            var receipt = receipts.SingleOrDefault(x => x.CommandKey == revision.CommandKey);
            if (receipt is null || receipt.SchemaVersion != 2 || receipt.Operation != "Edit"
                || receipt.TenantId != current.TenantId || receipt.LegalEntityId != current.LegalEntityId
                || receipt.PurchaseOrderId != current.Id
                || receipt.ActorPrincipalId != revision.ActorPrincipalId
                || receipt.CommandSha256 != revision.CommandSha256
                || receipt.ResponseStatus != 200
                || receipt.ResultVersion != revision.ResultOrderVersion
                || receipt.ResultVersion != checked(lastVersion + 1)
                || receipt.ResultVersion != checked(revision.RevisionNumber + 1L)
                || receipt.ResultVersion > current.Version
                || receipt.CompletedAt < revision.RecordedAt
                || receipt.ResponseSha256 != Digest(receipt.ResponseJson))
                throw new InvalidOperationException("PrincipalOrderDraftReceiptChanged");
            try
            {
                using var parsed = JsonDocument.Parse(receipt.ResponseJson);
                var response = parsed.RootElement;
                if (response.GetProperty("orderId").GetGuid() != current.Id
                    || response.GetProperty("number").GetString() != current.Number
                    || response.GetProperty("status").GetString() != "Draft"
                    || response.GetProperty("version").GetInt64() != receipt.ResultVersion
                    || response.GetProperty("originalDirectSourceSha256").GetString()
                        != current.OriginalDirectSourceSha256
                    || response.GetProperty("commandKey").GetGuid() != revision.CommandKey
                    || !response.GetProperty("recordedHistoricalResult").GetBoolean()
                    || !response.GetProperty("manualFundingOnly").GetBoolean()
                    || response.GetProperty("financeReservationChanged").GetBoolean()
                    || response.GetProperty("supplierSendPerformed").GetBoolean())
                    throw new InvalidOperationException("PrincipalOrderDraftReceiptChanged");
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException
                or InvalidOperationException or FormatException)
            {
                throw new InvalidOperationException("PrincipalOrderDraftReceiptChanged", error);
            }
            lastVersion = receipt.ResultVersion;
        }
        if (revisions.Count != 0 && current.Status == "Draft" && lastVersion != current.Version)
            throw new InvalidOperationException("PrincipalOrderDraftReceiptVersionChanged");
    }

    private static decimal Exact(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            || !ExactDecimalJsonCodec.IsRepresentable(number, 18, 4)
            || ProcurementEvidenceEncoding.Decimal(number) != value)
            throw new InvalidOperationException("PrincipalOrderCommercialDecimalInvalid");
        return number;
    }
}
