using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderRevisionCompanionTests
{
    private static readonly Guid Company=Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Order=Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Key=Guid.Parse("30000000-0000-0000-0000-000000000003");
    private const string Actor="40000000-0000-0000-0000-000000000004";
    private const string Digest="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static PrincipalOrderDraftCommercialRevision Revision()=>new()
    {
        TenantId="tenant",LegalEntityId=Company,PurchaseOrderId=Order,
        ActorPrincipalId=Actor,CommandKey=Key,RevisionNumber=1,Reason="correct quantity",
        SnapshotSha256=Digest
    };

    private static AuditEvent Audit()=>new()
    {
        TenantId="tenant",LegalEntityId=Company,RecordType="PurchaseOrder",RecordId=Order,
        Action="PrincipalOrderEdit",ActorSubjectId=Actor,CorrelationId="correlation",
        ChangedFieldsJson=JsonSerializer.Serialize(new{schemaVersion=2,commandKey=Key,
            detail=new{reason="correct quantity",revisionNumber=1,effectiveSnapshotSha256=Digest}})
    };

    private static OutboxEvent Outbox()=>new()
    {
        TenantId="tenant",LegalEntityId=Company,AggregateType="PurchaseOrder",AggregateId=Order,
        EventType="purchase_order.internal_unfunded_draft_revised.v2",
        IdempotencyKey=$"po.principal.Edit.{Key:N}.v2",
        PayloadJson=JsonSerializer.Serialize(new{orderId=Order,commandKey=Key,
            internalVersionAuthorized=false,supplierSendPerformed=false,financeReservationChanged=false})
    };

    [Fact]
    public void Edit_companions_require_exact_order_actor_key_revision_and_non_delivery_claims()
    {
        var revision=Revision();var audit=Audit();var outgoing=Outbox();
        Assert.True(PrincipalOrderRevisionCompanions.MatchesAudit(revision,audit,"correlation"));
        Assert.True(PrincipalOrderRevisionCompanions.MatchesOutbox(revision,outgoing));
        audit.ActorSubjectId=Guid.NewGuid().ToString("D");
        Assert.False(PrincipalOrderRevisionCompanions.MatchesAudit(revision,audit,"correlation"));
        audit=Audit();audit.ChangedFieldsJson=JsonSerializer.Serialize(new{schemaVersion=2,commandKey=Key,
            detail=new{reason="correct quantity",revisionNumber=2,effectiveSnapshotSha256=Digest}});
        Assert.False(PrincipalOrderRevisionCompanions.MatchesAudit(revision,audit,"correlation"));
        outgoing.IdempotencyKey=$"po.principal.Edit.{Guid.NewGuid():N}.v2";
        Assert.False(PrincipalOrderRevisionCompanions.MatchesOutbox(revision,outgoing));
        outgoing=Outbox();outgoing.PayloadJson=JsonSerializer.Serialize(new{orderId=Order,commandKey=Key,
            internalVersionAuthorized=false,supplierSendPerformed=true,financeReservationChanged=false});
        Assert.False(PrincipalOrderRevisionCompanions.MatchesOutbox(revision,outgoing));
    }

    [Fact]
    public void Missing_or_malformed_companions_cannot_satisfy_same_save_pair()
    {
        var revision=Revision();
        Assert.Empty(Array.Empty<AuditEvent>().Where(x=>PrincipalOrderRevisionCompanions.MatchesAudit(
            revision,x,"correlation")));
        Assert.Empty(Array.Empty<OutboxEvent>().Where(x=>PrincipalOrderRevisionCompanions.MatchesOutbox(
            revision,x)));
        var audit=Audit();audit.ChangedFieldsJson="{broken";
        var outgoing=Outbox();outgoing.PayloadJson="{broken";
        Assert.False(PrincipalOrderRevisionCompanions.MatchesAudit(revision,audit,"correlation"));
        Assert.False(PrincipalOrderRevisionCompanions.MatchesOutbox(revision,outgoing));
    }
}
