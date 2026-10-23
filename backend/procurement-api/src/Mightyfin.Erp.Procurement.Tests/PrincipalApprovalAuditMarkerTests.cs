using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalApprovalAuditMarkerTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "principal-audit";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "principal-audit-marker";
    }

    // This verifies durable SaveChanges output, not PostgreSQL or downgrade execution.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_identity_audit_survives_reload_without_values_and_legacy_field_array_stays_exact(bool successor)
    {
        var scope = new Scope();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Guid requestId;
        string legacyCreate;
        await using (var db = new ProcurementDbContext(options, scope))
        {
            var request = new PurchaseRequest
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                CreatedBy = scope.SubjectId, RequesterPrincipalId = Guid.Parse(scope.SubjectId),
                Number = "PR-AUDIT", SourceChannel = "ERP", Status = "Draft",
                Purpose = "Confidential purchase purpose", CurrencyCode = "ZMW",
            };
            if (successor) PurchaseRequestPrincipalIdentity.CaptureNew(request, scope.SubjectId, null);
            db.AddScoped(request);
            legacyCreate = JsonSerializer.Serialize(db.Entry(request).Properties
                .Select(x => x.Metadata.Name).OrderBy(x => x));
            await db.SaveChangesAsync();
            requestId = request.Id;
            request.Purpose = "Revised confidential purchase purpose";
            await db.SaveChangesAsync();
        }

        await using var fresh = new ProcurementDbContext(options, scope);
        var audits = await fresh.AuditEvents.AsNoTracking()
            .Where(x => x.RecordType == nameof(PurchaseRequest) && x.RecordId == requestId).ToListAsync();
        Assert.Equal(2, audits.Count);
        foreach (var audit in audits)
        {
            Assert.DoesNotContain("Confidential purchase purpose", audit.ChangedFieldsJson!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(scope.SubjectId, audit.ChangedFieldsJson!);
            using var payload = JsonDocument.Parse(audit.ChangedFieldsJson!);
            if (successor)
            {
                Assert.Equal(2, payload.RootElement.GetProperty("principalEvidenceVersion").GetInt32());
                Assert.Equal("PurchaseRequestOriginalIdentity", payload.RootElement.GetProperty("principalEvidenceKind").GetString());
                Assert.Equal(JsonValueKind.Array, payload.RootElement.GetProperty("fields").ValueKind);
            }
            else Assert.Equal(JsonValueKind.Array, payload.RootElement.ValueKind);
        }
        if (!successor) Assert.Equal(legacyCreate, audits.Single(x => x.Action == "create").ChangedFieldsJson);
    }
}
