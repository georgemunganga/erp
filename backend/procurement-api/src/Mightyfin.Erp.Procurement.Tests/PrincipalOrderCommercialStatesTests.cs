using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderCommercialStatesTests
{
    [Fact]
    public void Original_source_pin_remains_immutable_while_one_effective_unfunded_revision_is_verified()
    {
        var actor = Guid.NewGuid().ToString("D");
        var sourceLine = Guid.NewGuid();
        var order = new PurchaseOrder { TenantId = "tenant", LegalEntityId = Guid.NewGuid(),
            PrincipalDraftSchemaVersion = 2, SupplierId = Guid.NewGuid(), BranchRef = Guid.NewGuid(),
            BuyerSubjectId = actor, Number = "PO-TEST", CurrencyCode = "ZMW", Status = "Draft" };
        var line = new PurchaseOrderLine { TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseOrderId = order.Id, PurchaseRequestLineId = sourceLine,
            LineNumber = 1, Description = "Original", UnitCode = "EA",
            Quantity = 2, UnitPrice = 10, TaxAmount = 1, IsActive = true };
        var binding = new PrincipalOrderSourceBinding { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id,
            PurchaseOrderLineId = line.Id, AllocationId = Guid.NewGuid(),
            PurchaseRequestId = Guid.NewGuid(), PurchaseRequestLineId = sourceLine,
            RequestVersion = 2, SubmittedBusinessVersion = 1,
            SubmissionId = Guid.NewGuid(), ApprovalOutcomeId = Guid.NewGuid(),
            CoreApprovalCaseId = Guid.NewGuid(), CoreDecisionId = Guid.NewGuid(),
            FinanceSnapshotId = Guid.NewGuid(), MethodId = Guid.NewGuid(), MethodRevision = 1,
            MethodCode = "DIRECT", RequestSnapshotSha256 = Hash, PolicyDecisionSha256 = Hash,
            PolicyRulesSha256 = Hash, FinanceSnapshotSha256 = Hash,
            PrincipalIdentitySha256 = Hash, BuyerPrincipalId = actor,
            RequesterPrincipalId = Guid.NewGuid(), BranchId = order.BranchRef!.Value,
            Quantity = 2, UnitEstimate = 10, ProposedUnitPrice = 10,
            EstimatedValue = 20, CurrencyCode = "ZMW" };
        order.OriginalDirectSourceSha256 = PrincipalOrderDraftPins.Digest(order, [line], [binding]);
        binding.OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256;
        var original = PrincipalOrderCommercialStates.Capture(order, [line]);
        var origin = new PrincipalOrderCommercialOrigin { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id,
            OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256,
            SnapshotJson = original, SnapshotSha256 = PrincipalOrderCommercialStates.Digest(original),
            CapturedByPrincipalId = actor, CapturedAt = DateTimeOffset.UtcNow,
            CaptureMode = "VerifiedFirstEdit" };
        line.Quantity = 1;
        line.Description = "Reduced";
        order.Version = 2;
        var allocation = new PurchaseRequestAllocation { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseRequestId = binding.PurchaseRequestId,
            PurchaseRequestLineId = sourceLine, TargetType = "PurchaseOrder", TargetId = order.Id,
            Quantity = 1, EstimatedValue = 10, CurrencyCode = "ZMW", Status = "Reserved" };
        allocation.Id = binding.AllocationId;
        var current = PrincipalOrderCommercialStates.Capture(order, [line]);
        var revision = new PrincipalOrderDraftCommercialRevision { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id, OriginId = origin.Id,
            RevisionNumber = 1, OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256,
            SnapshotJson = current, SnapshotSha256 = PrincipalOrderCommercialStates.Digest(current),
            Reason = "Need fewer units", ActorPrincipalId = actor, CommandKey = Guid.NewGuid(),
            CommandSha256 = Hash, ResultOrderVersion = 2,
            RecordedAt = DateTimeOffset.UtcNow };
        var slice = new PrincipalOrderEffectiveSourceSlice { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id, RevisionId = revision.Id,
            SourceBindingId = binding.Id, PurchaseOrderLineId = line.Id,
            AllocationId = allocation.Id, Quantity = 1, UnitPrice = 10, TaxAmount = 1,
            EstimatedValue = 10, CurrencyCode = "ZMW" };

        PrincipalOrderCommercialStates.VerifyRevision(order, [line], origin,
            [revision], [slice], [binding], [allocation]);
        var response = System.Text.Json.JsonSerializer.Serialize(new { orderId = order.Id,
            order.Number, status = "Draft", version = 2L,
            originalDirectSourceSha256 = order.OriginalDirectSourceSha256,
            commandKey = revision.CommandKey, recordedHistoricalResult = true,
            manualFundingOnly = true, financeReservationChanged = false,
            supplierSendPerformed = false },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var receipt = new PrincipalOrderDraftReceipt { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id, Operation = "Edit",
            CommandKey = revision.CommandKey, ActorPrincipalId = actor,
            CommandSha256 = revision.CommandSha256, ResponseJson = response,
            ResponseSha256 = PrincipalOrderCommercialStates.Digest(response),
            ResponseStatus = 200, ResultVersion = revision.ResultOrderVersion,
            CompletedAt = revision.RecordedAt.AddSeconds(1) };
        PrincipalOrderCommercialStates.VerifyReceipts(order, origin, [revision], [receipt]);
        Assert.Throws<InvalidOperationException>(() => PrincipalOrderCommercialStates.VerifyReceipts(
            order, origin, [revision], []));
        Assert.Throws<InvalidOperationException>(() => PrincipalOrderCommercialStates.VerifyReceipts(
            order, origin, [], []));
        var tampered = new PrincipalOrderDraftReceipt { TenantId = receipt.TenantId,
            LegalEntityId = receipt.LegalEntityId, PurchaseOrderId = receipt.PurchaseOrderId,
            Operation = receipt.Operation, CommandKey = receipt.CommandKey,
            ActorPrincipalId = receipt.ActorPrincipalId, CommandSha256 = new string('b', 64),
            ResponseJson = receipt.ResponseJson, ResponseSha256 = receipt.ResponseSha256,
            ResponseStatus = receipt.ResponseStatus, ResultVersion = receipt.ResultVersion,
            CompletedAt = receipt.CompletedAt };
        Assert.Throws<InvalidOperationException>(() => PrincipalOrderCommercialStates.VerifyReceipts(
            order, origin, [revision], [tampered]));
        var changedBody = receipt.ResponseJson.Replace("\"status\":\"Draft\"",
            "\"status\":\"Issued\"", StringComparison.Ordinal);
        Assert.NotEqual(receipt.ResponseJson, changedBody);
        var changedResponse = new PrincipalOrderDraftReceipt { TenantId = receipt.TenantId,
            LegalEntityId = receipt.LegalEntityId, PurchaseOrderId = receipt.PurchaseOrderId,
            Operation = receipt.Operation, CommandKey = receipt.CommandKey,
            ActorPrincipalId = receipt.ActorPrincipalId, CommandSha256 = receipt.CommandSha256,
            ResponseJson = changedBody,
            ResponseSha256 = PrincipalOrderCommercialStates.Digest(changedBody),
            ResponseStatus = receipt.ResponseStatus, ResultVersion = receipt.ResultVersion,
            CompletedAt = receipt.CompletedAt };
        Assert.Throws<InvalidOperationException>(() => PrincipalOrderCommercialStates.VerifyReceipts(
            order, origin, [revision], [changedResponse]));
        Assert.Equal(2m, binding.Quantity);
        Assert.Equal(1m, allocation.Quantity);
        line.UnitPrice = 11;
        Assert.Throws<InvalidOperationException>(() => PrincipalOrderCommercialStates.VerifyRevision(
            order, [line], origin, [revision], [slice], [binding], [allocation]));
    }

    private static readonly string Hash = new('a', 64);
}
