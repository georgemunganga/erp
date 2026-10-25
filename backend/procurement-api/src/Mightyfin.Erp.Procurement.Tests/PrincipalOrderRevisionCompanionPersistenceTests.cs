using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderRevisionCompanionPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "p33-test";
    }

    private static async Task<(DbContextOptions<ProcurementDbContext>, Scope)> SeedAsync()
    {
        var scope = new Scope("p33-edit", Guid.NewGuid(), Guid.NewGuid().ToString("D"));
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var sourceLine = Guid.NewGuid();
        var order = new PurchaseOrder { SupplierId = Guid.NewGuid(), Number = "PO-P33",
            CurrencyCode = "ZMW", BuyerSubjectId = scope.SubjectId, BranchRef = Guid.NewGuid(),
            PrincipalDraftSchemaVersion = 2 };
        db.AddScoped(order);
        var line = new PurchaseOrderLine { PurchaseOrderId = order.Id,
            PurchaseRequestLineId = sourceLine, LineNumber = 1, Description = "Test",
            UnitCode = "EA", Quantity = 1m, UnitPrice = 1m };
        db.AddScoped(line);
        var allocation = new PurchaseRequestAllocation { PurchaseRequestId = Guid.NewGuid(),
            PurchaseRequestLineId = sourceLine, TargetType = "PurchaseOrder", TargetId = order.Id,
            Method = "DIRECT", Status = "Reserved", Quantity = 1m, EstimatedValue = 1m,
            CurrencyCode = "ZMW", DecidedBy = scope.SubjectId, DecidedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = "p33-original" };
        db.AddScoped(allocation);
        var binding = new PrincipalOrderSourceBinding { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, PurchaseOrderId = order.Id,
            PurchaseOrderLineId = line.Id, AllocationId = allocation.Id,
            PurchaseRequestId = allocation.PurchaseRequestId, PurchaseRequestLineId = sourceLine,
            BuyerPrincipalId = scope.SubjectId, BranchId = order.BranchRef!.Value,
            CurrencyCode = "ZMW", Quantity = 1m, ProposedUnitPrice = 1m, EstimatedValue = 1m,
            MethodId = Guid.NewGuid(), MethodRevision = 1, MethodCode = "DIRECT",
            RequestVersion = 1, SubmittedBusinessVersion = 1, SubmissionId = Guid.NewGuid(),
            ApprovalOutcomeId = Guid.NewGuid(), CoreApprovalCaseId = Guid.NewGuid(),
            CoreDecisionId = Guid.NewGuid(), FinanceSnapshotId = Guid.NewGuid(),
            RequesterPrincipalId = Guid.NewGuid(), UnitEstimate = 1m,
            RequestSnapshotSha256 = new string('a', 64), PolicyDecisionSha256 = new string('b', 64),
            PolicyRulesSha256 = new string('c', 64), FinanceSnapshotSha256 = new string('d', 64),
            PrincipalIdentitySha256 = new string('e', 64) };
        order.OriginalDirectSourceSha256 = PrincipalOrderDraftPins.Digest(order, [line], [binding]);
        binding.OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256;
        db.AddScoped(binding);
        var createKey = Guid.NewGuid();
        var response = JsonSerializer.Serialize(new { orderId = order.Id, order.Number, status = "Draft",
            version = 1, originalDirectSourceSha256 = order.OriginalDirectSourceSha256,
            commandKey = createKey, recordedHistoricalResult = true, manualFundingOnly = true,
            financeReservationChanged = false, supplierSendPerformed = false });
        db.AddScoped(new PrincipalOrderDraftReceipt { PurchaseOrderId = order.Id,
            Operation = "Create", CommandKey = createKey, ActorPrincipalId = scope.SubjectId,
            CommandSha256 = new string('f', 64), ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response), ResponseStatus = 201,
            ResultVersion = 1, CompletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return (options, scope);
    }

    private static async Task SaveEditAsync(DbContextOptions<ProcurementDbContext> options,
        Scope scope, bool audit, bool outbox)
    {
        await using var db = new ProcurementDbContext(options, scope);
        var order = await db.PurchaseOrders.SingleAsync();
        var line = await db.PurchaseOrderLines.SingleAsync();
        var binding = await db.PrincipalOrderSourceBindings.SingleAsync();
        var allocation = await db.PurchaseRequestAllocations.SingleAsync();
        var original = PrincipalOrderCommercialStates.Capture(order, [line]);
        var origin = new PrincipalOrderCommercialOrigin { PurchaseOrderId = order.Id,
            OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256!, SnapshotJson = original,
            SnapshotSha256 = PrincipalOrderCommercialStates.Digest(original),
            CapturedByPrincipalId = scope.SubjectId, CapturedAt = DateTimeOffset.UtcNow,
            CaptureMode = "VerifiedFirstEdit" };
        db.AddScoped(origin);
        order.ShipToRef = "Revised location";
        line.Quantity = 0.5m;
        allocation.Quantity = 0.5m;
        allocation.EstimatedValue = 0.5m;
        var snapshot = PrincipalOrderCommercialStates.Capture(order, [line]);
        var key = Guid.NewGuid();
        var revision = new PrincipalOrderDraftCommercialRevision { PurchaseOrderId = order.Id,
            OriginId = origin.Id, RevisionNumber = 1,
            OriginalDirectSourceSha256 = order.OriginalDirectSourceSha256!,
            SnapshotJson = snapshot, SnapshotSha256 = PrincipalOrderCommercialStates.Digest(snapshot),
            Reason = "Reduce quantity", ActorPrincipalId = scope.SubjectId,
            CommandKey = key, CommandSha256 = new string('1', 64),
            ResultOrderVersion = checked(order.Version + 1), RecordedAt = DateTimeOffset.UtcNow };
        db.AddScoped(revision);
        db.AddScoped(new PrincipalOrderEffectiveSourceSlice { PurchaseOrderId = order.Id,
            RevisionId = revision.Id, SourceBindingId = binding.Id, PurchaseOrderLineId = line.Id,
            AllocationId = allocation.Id, Quantity = 0.5m, UnitPrice = 1m, TaxAmount = 0m,
            EstimatedValue = 0.5m, CurrencyCode = "ZMW" });
        var response = JsonSerializer.Serialize(new { orderId = order.Id, order.Number,
            status = "Draft", version = revision.ResultOrderVersion,
            originalDirectSourceSha256 = order.OriginalDirectSourceSha256, commandKey = key,
            recordedHistoricalResult = true, manualFundingOnly = true,
            financeReservationChanged = false, supplierSendPerformed = false });
        db.AddScoped(new PrincipalOrderDraftReceipt { PurchaseOrderId = order.Id,
            Operation = "Edit", CommandKey = key, ActorPrincipalId = scope.SubjectId,
            CommandSha256 = revision.CommandSha256, ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response), ResponseStatus = 200,
            ResultVersion = revision.ResultOrderVersion, CompletedAt = DateTimeOffset.UtcNow });
        if (audit) db.AddScoped(new AuditEvent { RecordType = "PurchaseOrder", RecordId = order.Id,
            Action = "PrincipalOrderEdit", ActorSubjectId = scope.SubjectId,
            CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { schemaVersion = 2, commandKey = key,
                detail = new { reason = revision.Reason, revisionNumber = revision.RevisionNumber,
                    effectiveSnapshotSha256 = revision.SnapshotSha256 } }) });
        if (outbox) db.AddScoped(new OutboxEvent { AggregateType = "PurchaseOrder",
            AggregateId = order.Id, EventType = "purchase_order.internal_unfunded_draft_revised.v2",
            IdempotencyKey = $"po.principal.Edit.{key:N}.v2",
            PayloadJson = JsonSerializer.Serialize(new { orderId = order.Id, commandKey = key,
                internalVersionAuthorized = false, supplierSendPerformed = false,
                financeReservationChanged = false }) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Complete_revision_receipt_audit_and_outbox_save_together()
    {
        var (options, scope) = await SeedAsync();
        await SaveEditAsync(options, scope, audit: true, outbox: true);
        await using var stored = new ProcurementDbContext(options, scope);
        Assert.Equal(1, await stored.PrincipalOrderDraftCommercialRevisions.CountAsync());
        Assert.Equal(1, await stored.AuditEvents.CountAsync(x => x.Action == "PrincipalOrderEdit"));
        Assert.Equal(1, await stored.OutboxEvents.CountAsync());
        Assert.Equal(0.5m, (await stored.PurchaseRequestAllocations.SingleAsync()).Quantity);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Missing_same_save_companion_rejects_revision_without_persisting_it(bool audit, bool outbox)
    {
        var (options, scope) = await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SaveEditAsync(options, scope, audit, outbox));
        await using var stored = new ProcurementDbContext(options, scope);
        Assert.Empty(await stored.PrincipalOrderDraftCommercialRevisions.ToArrayAsync());
        Assert.Equal(1m, (await stored.PurchaseRequestAllocations.SingleAsync()).Quantity);
    }
}
