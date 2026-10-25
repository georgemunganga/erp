using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalGoodsReturnPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Theory]
    [InlineData(false, false, "Pending", 0, false, 1)]
    [InlineData(true, false, "Pending", 0, false, 1)]
    [InlineData(true, true, "Pending", 0, false, 1)]
    [InlineData(true, true, "Published", 0, false, 1)]
    [InlineData(true, true, "Pending", 1, false, 1)]
    [InlineData(true, true, "Pending", 0, true, 1)]
    [InlineData(true, true, "Pending", 0, false, 0)]
    public async Task Draft_evidence_cannot_save_without_exact_internal_event_and_verified_accepted_goods(
        bool addEvent, bool correctPayload, string eventState, int attempts, bool published, long initialRevision)
    {
        var actor = Guid.NewGuid().ToString("D"); var scope = new Scope("return-guard", Guid.NewGuid(), actor);
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var key = Guid.NewGuid();
        var head = new PrincipalGoodsReturnDraft { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            BranchId = Guid.NewGuid(), SourceStateId = Guid.NewGuid(), AcceptanceFactId = Guid.NewGuid(),
            ReceiptId = Guid.NewGuid(), PurchaseOrderId = Guid.NewGuid(), PurchaseOrderLineId = Guid.NewGuid(),
            IssuedVersionId = Guid.NewGuid(), SourceSha256 = new string('a', 64), AcceptanceSha256 = new string('b', 64),
            AcceptedFactQuantity = 1m, CreatorPrincipalId = actor };
        var revision = new PrincipalGoodsReturnRevision { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            DraftId = head.Id, Revision = initialRevision, CommandKey = key, ActorPrincipalId = actor, Quantity = 1m, Reason = "Damaged",
            ProposalSha256 = PrincipalGoodsReturnRules.Fingerprint(head.Id, 1, 1m, "Damaged", null, "Draft") };
        head.CurrentRevisionId = revision.Id;
        var response = PrincipalGoodsReturnRules.ResponseJson(head, revision);
        var request = JsonSerializer.Serialize(new { operation = "Create", input = new
        { acceptanceFactId = head.AcceptanceFactId, quantity = "1", reason = "Damaged", evidenceReference = (string?)null } });
        db.AddScoped(head); db.AddScoped(revision);
        db.AddScoped(new PrincipalGoodsReturnCommandReceipt { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            DraftId = head.Id, BranchId = head.BranchId, CommandKey = key, Operation = "Create",
            ActorPrincipalId = actor, RequestJson = request, RequestSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(request),
            ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response), ResponseStatus = 201, ResultRevision = 1 });
        if (addEvent) db.AddScoped(new OutboxEvent { State = eventState, Attempts = attempts,
            PublishedAt = published ? DateTimeOffset.UtcNow : null, AggregateType = "PrincipalGoodsReturnDraft", AggregateId = head.Id,
            EventType = "procurement.goods_return.draft_create.v1", IdempotencyKey = $"goods-return.draft.{key:N}.v1",
            PayloadJson = PrincipalGoodsReturnRules.OutboxPayloadJson(head.Id, "Create", key, 1,
                correctPayload ? actor : Guid.NewGuid().ToString("D")) });
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(initialRevision == 0 ? "ReturnDraftExactRevisionReceiptRequired" :
            addEvent && correctPayload && eventState == "Pending" && attempts == 0 && !published
                ? "ReturnDraftAcceptedGoodsSourceRequired" : "ReturnDraftInternalEventRequired", failure.Message);
        Assert.False(await db.PrincipalGoodsReturnDrafts.AsNoTracking().AnyAsync());
    }
}
