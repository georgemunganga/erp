using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Durable owning-transaction effects; no email or supplier submission.
/// Generic create/change audits remain independently produced by the shared ERP.</summary>
public static class PrincipalQuotationEffects
{
    public sealed record Effects(AuditEvent Audit, OutboxEvent Outbox);
    public static Effects Contact(SourcingManualContact row) => Build(row, row.SourcingEventId, row.Id,
        "OutsideContactRecorded", row.ActorPrincipalId, row.CommandKey, row.ResponseSha256);
    public static Effects Quotation(SourcingManualQuoteVersion row) => Build(row, row.SourcingEventId, row.SupplierBidId,
        "QuotationDraft" + row.Operation, row.ActorPrincipalId, row.CommandKey, row.ResponseSha256);
    private static Effects Build(ProcurementRecord owner, Guid source, Guid target, string action, string actor, Guid key, string responseSha)
    {
        var payload = JsonSerializer.Serialize(new { schemaVersion = 3, sourceId = source, recordId = target,
            commandKey = key, responseSha256 = responseSha, supplierSubmitted = false, emailSentBySystem = false },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(new AuditEvent { TenantId = owner.TenantId, LegalEntityId = owner.LegalEntityId,
            RecordType = "SupplierQuotation", RecordId = target, Action = action, ActorSubjectId = actor,
            ChangedFieldsJson = payload }, new OutboxEvent { TenantId = owner.TenantId, LegalEntityId = owner.LegalEntityId,
            AggregateType = "SupplierQuotation", AggregateId = target, EventType = "sourcing." + action + ".v3",
            IdempotencyKey = $"quotation.{key:N}.v3", PayloadJson = payload });
    }
    public static void Verify(Effects expected, IReadOnlyList<AuditEvent> audits, IReadOnlyList<OutboxEvent> outbox,
        string? correlation)
    {
        var a = expected.Audit; var e = expected.Outbox;
        if (audits.Count != 1 || outbox.Count != 1) throw new InvalidOperationException("QuotationOwningEffectsMissing");
        var actual = audits[0]; var published = outbox[0];
        if (actual.TenantId != a.TenantId || actual.LegalEntityId != a.LegalEntityId || actual.RecordType != a.RecordType
            || actual.RecordId != a.RecordId || actual.Action != a.Action || actual.ActorSubjectId != a.ActorSubjectId
            || actual.CorrelationId != correlation || actual.ChangedFieldsJson != a.ChangedFieldsJson
            || published.TenantId != e.TenantId || published.LegalEntityId != e.LegalEntityId
            || published.AggregateType != e.AggregateType || published.AggregateId != e.AggregateId
            || published.EventType != e.EventType || published.EventVersion != 1 || published.State != "Pending"
            || published.Attempts != 0 || published.PublishedAt is not null || published.NextAttemptAt is not null
            || published.IdempotencyKey != e.IdempotencyKey || published.PayloadJson != e.PayloadJson)
            throw new InvalidOperationException("QuotationOwningEffectsChanged");
    }
}
