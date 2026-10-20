using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;
public sealed class ReceivingPersistenceProjectionTests
{
    [Theory] [InlineData(0)] [InlineData(2)]
    public void Legacy_or_unknown_header_execution_version_never_acquires_issued_authority(int version)
    {
        var header = new GoodsReceipt { ExecutionVersion = version, IssuedBindingId = Guid.Empty };
        Assert.Null(header.IssuedVersionId);
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.VerifyHeader(header));
        Assert.Equal(Guid.Empty, header.IssuedBindingId); Assert.Null(header.SubmissionId);
    }
    [Fact]
    public void Version_one_cannot_use_a_missing_or_different_issued_binding()
    {
        var header = new GoodsReceipt { ExecutionVersion = 1, IssuedVersionId = Guid.NewGuid(), SubmissionId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.VerifyHeader(header));
        header.IssuedBindingId = header.IssuedVersionId.Value;
        ReceivingPersistenceProjection.VerifyHeader(header);
    }
    [Fact]
    public void Physical_projection_keeps_original_identity_and_truthful_overdelivery()
    {
        var (state, header, line) = Fixture();
        var observation = ReceivingPersistenceProjection.Observation(state, header, line);
        Assert.Equal(line.PhysicalObservationId, observation.Id); Assert.Equal(12m, observation.ReceivedQuantity);
        Assert.Equal(2m, observation.InitiallyRejectedQuantity); Assert.Equal(header.ReceiverWorkerId, observation.ReceiverWorkerId);
        Assert.Equal(line.PhysicalRecordedAt, observation.RecordedAt); Assert.Equal(state.Source, observation.Source);
        line.IssuedBindingId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.Observation(state, header, line));
    }
    [Fact]
    public void Unknown_empty_history_is_not_complete_or_zero_acceptance()
    {
        var (state, _, _) = Fixture();
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.Ledger(state, [], [], [], [], false, false));
        var structural = ReceivingPersistenceProjection.Ledger(state, [], [], [], [], true, false);
        Assert.False(structural.AcceptanceHistoryVerified);
        Assert.Throws<InvalidOperationException>(() => ControlledReceiving.Balance(structural, Guid.NewGuid()));
        state.CurrentAcceptedQuantity = 1m;
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.Ledger(state, [], [], [], [], true, true));
    }
    [Fact]
    public void Modified_source_or_scope_is_not_repaired_from_columns()
    {
        var (state, _, _) = Fixture(); state.LegalEntityId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.VerifyState(state));
    }
    private static (ReceivingSourceState, GoodsReceipt, GoodsReceiptLine) Fixture()
    {
        var entity = Guid.NewGuid(); var order = Guid.NewGuid(); var issued = Guid.NewGuid(); var poLine = Guid.NewGuid(); var submission = Guid.NewGuid();
        var classification = "{\"fixture\":\"explicit-test-only\"}";
        var source = new ReceivingSourcePin("test", entity, Guid.NewGuid(), order, issued, poLine, Guid.NewGuid(), 1,
            new string('a', 64), new string('b', 64), "CLASSIFICATION", ProcurementEvidenceEncoding.PolicyJsonDigest(classification),
            ReceivingKind.Goods, "IT", "Goods", "ZMW", "EA", 10, 100, ControlledReceiving.NetPriceBasis);
        var state = new ReceivingSourceState { TenantId = "test", LegalEntityId = entity, PurchaseOrderId = order, PurchaseOrderLineId = poLine,
            IssuedVersionId = issued, SubmissionId = submission, DeliveryEvidenceId = Guid.NewGuid(), Source = source,
            SourceSha256 = ReceivingPersistenceProjection.PinDigest(source), ClassificationJson = classification, ClassificationPolicyVersionId = Guid.NewGuid() };
        var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        var header = new GoodsReceipt { TenantId = "test", LegalEntityId = entity, PurchaseOrderId = order, ExecutionVersion = 1,
            IssuedVersionId = issued, IssuedBindingId = issued, SubmissionId = submission, ReceiverWorkerId = Guid.NewGuid(),
            ReceiverSubjectId = "receiver", RecordedAt = now, SiteRef = source.BranchId };
        var line = new GoodsReceiptLine { TenantId = "test", LegalEntityId = entity, GoodsReceiptId = header.Id, PurchaseOrderLineId = poLine,
            ExecutionVersion = 1, ReceivingSourceStateId = state.Id, SourceStateBindingId = state.Id, SourcePurchaseOrderId = order,
            IssuedBindingId = issued, ReceivedQuantity = 12, InitiallyRejectedQuantity = 2, PhysicalObservationId = Guid.NewGuid(),
            PhysicalPolicyVersionId = Guid.NewGuid(), PhysicalEvidenceRef = "EVIDENCE", PhysicalSequence = 1,
            PhysicalRecordedAt = now, PhysicalEffectKey = Guid.NewGuid() };
        line.PhysicalBindingId = line.PhysicalObservationId.Value;
        return (state, header, line);
    }
}
