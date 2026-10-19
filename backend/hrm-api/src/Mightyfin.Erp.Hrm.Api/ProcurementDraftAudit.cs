using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Records draft changes in the same SaveChanges call as their business record.</summary>
internal static class ProcurementDraftAudit
{
    public static JsonElement Snapshot(object value) => JsonSerializer.SerializeToElement(value);

    public static void Add(ProcurementDbContext db, ProcurementRequestScope scope,
        string recordType, Guid recordId, string action, object? before, object? after)
    {
        db.AddScoped(new AuditEvent
        {
            RecordType = recordType,
            RecordId = recordId,
            Action = action,
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { before, after }),
        });
    }
}
