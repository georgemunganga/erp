using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<InvoiceAmountCalculation> InvoiceAmountCalculations => Set<InvoiceAmountCalculation>();

    // Root integrates this hook before global snake-case convention application.
    private void ConfigureInvoiceAmountCalculations(ModelBuilder model)
    {
        ConfigureBase<InvoiceAmountCalculation>(model, "invoice_amount_calculations");
        var row = model.Entity<InvoiceAmountCalculation>();
        row.HasOne<SupplierInvoice>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId })
            .IsUnique().HasDatabaseName("ux_invoice_amount_calculation_invoice");
        row.Property(x => x.CurrencyCode).HasMaxLength(3);
        row.Property(x => x.RoundingMode).HasMaxLength(30);
        row.Property(x => x.CoreSourcePayloadSha256).HasMaxLength(64);
        row.Property(x => x.InvoiceOriginalSha256).HasMaxLength(64);
        row.Property(x => x.CalculationPolicySha256).HasMaxLength(64);
        row.Property(x => x.CalculationSha256).HasMaxLength(64);
        row.Property(x => x.CalculationJson).HasColumnType("text");
        if (Database.IsNpgsql()) row.ToTable(t => t.HasCheckConstraint("ck_invoice_amount_calculation",
            "schema_version=2 AND version=1 AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND core_currency_reference_id <> '00000000-0000-0000-0000-000000000000'::uuid "
            + "AND core_currency_reference_revision>0 AND currency_code ~ '^[A-Z]{3}$' "
            + "AND minor_units BETWEEN 0 AND 4 AND rounding_mode IN ('ToEven','AwayFromZero') "
            + "AND core_source_payload_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' "
            + "AND calculation_policy_sha256 ~ '^[0-9a-f]{64}$' AND calculation_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(calculation_json) BETWEEN 1 AND 262144 "
            + "AND isfinite(core_published_at_utc) AND isfinite(observed_at_utc) "
            + "AND core_published_at_utc<=observed_at_utc AND created_at>=observed_at_utc "
            + "AND updated_at IS NULL AND updated_by IS NULL"));
    }

    // Root integrates after the shared audit/scope stamp, before SaveChanges.
    // No HTTP flag or caller completeness assertion substitutes for tracked rows.
    private void GuardInvoiceAmountCalculations()
    {
        GuardInvoiceAmountCalculationOriginals();
        foreach (var entry in ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Modified
            && (x.Entity.EventType == InvoiceAmountCalculationEvidence.EventType
                || x.OriginalValues.GetValue<string>(nameof(OutboxEvent.EventType)) == InvoiceAmountCalculationEvidence.EventType)))
        {
            foreach (var name in new[] { nameof(OutboxEvent.EventType), nameof(OutboxEvent.EventVersion),
                nameof(OutboxEvent.AggregateType), nameof(OutboxEvent.AggregateId), nameof(OutboxEvent.PayloadJson),
                nameof(OutboxEvent.IdempotencyKey), nameof(OutboxEvent.TenantId), nameof(OutboxEvent.LegalEntityId),
                nameof(OutboxEvent.CreatedBy), nameof(OutboxEvent.CreatedAt) })
                if (!Equals(entry.OriginalValues[name], entry.CurrentValues[name]))
                    throw new InvalidOperationException("InvoiceAmountCalculationMarkerAppendOnly");
        }
        var entries = ChangeTracker.Entries<InvoiceAmountCalculation>().ToArray();
        if (entries.Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("InvoiceAmountCalculationAppendOnly");
        var added = entries.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        if (added.Select(x => (x.TenantId, x.LegalEntityId, x.SupplierInvoiceId)).Distinct().Count() != added.Length)
            throw new InvalidOperationException("InvoiceAmountCalculationDuplicate");
        var audits = ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added
            && x.Entity.Action == InvoiceAmountCalculationEvidence.AuditAction).Select(x => x.Entity).ToArray();
        var events = ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Added
            && x.Entity.EventType == InvoiceAmountCalculationEvidence.EventType).Select(x => x.Entity).ToArray();
        if (audits.Length != added.Length || events.Length != added.Length)
            throw new InvalidOperationException("InvoiceAmountCalculationMarkerSetChanged");
        foreach (var row in added)
        {
            var parent = ChangeTracker.Entries<SupplierInvoice>().SingleOrDefault(x => x.State == EntityState.Added
                && x.Entity.Id == row.SupplierInvoiceId && x.Entity.TenantId == row.TenantId
                && x.Entity.LegalEntityId == row.LegalEntityId)?.Entity
                ?? throw new InvalidOperationException("InvoiceAmountCalculationNewInvoiceRequired");
            if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.CreatedBy != scope.SubjectId || row.CreatedBy != parent.CreatedBy)
                throw new InvalidOperationException("InvoiceAmountCalculationActorScopeChanged");
            var lines = ChangeTracker.Entries<SupplierInvoiceLine>().Where(x => x.State == EntityState.Added
                && x.Entity.SupplierInvoiceId == parent.Id).Select(x => x.Entity).ToArray();
            _ = InvoiceAmountCalculationEvidence.Verify(row, parent, lines);
            var marker = InvoiceAmountCalculationEvidence.MarkerJson(row);
            if (audits.Count(x => x.RecordType == nameof(InvoiceAmountCalculation) && x.RecordId == row.Id
                    && x.TenantId == row.TenantId && x.LegalEntityId == row.LegalEntityId
                    && x.ActorSubjectId == row.CreatedBy && x.CreatedBy == row.CreatedBy
                    && x.ChangedFieldsJson == marker) != 1
                || events.Count(x => x.AggregateType == nameof(SupplierInvoice) && x.AggregateId == parent.Id
                    && x.EventVersion == 2 && x.TenantId == row.TenantId && x.LegalEntityId == row.LegalEntityId
                    && x.CreatedBy == row.CreatedBy && x.PayloadJson == marker
                    && x.IdempotencyKey == "invoice-amount-calculation:" + row.Id.ToString("D")) != 1)
                throw new InvalidOperationException("InvoiceAmountCalculationMarkersRequired");
        }
    }
    private void GuardInvoiceAmountCalculationOriginals()
    {
        var parents = ChangeTracker.Entries<SupplierInvoice>().ToArray();
        var lines = ChangeTracker.Entries<SupplierInvoiceLine>().ToArray();
        var affected = parents.Where(x => x.State == EntityState.Modified).Select(x => x.Entity.Id)
            .Concat(lines.Where(x => x.State is EntityState.Added or EntityState.Modified)
                .Select(x => x.Entity.SupplierInvoiceId))
            .Concat(lines.Where(x => x.State == EntityState.Modified)
                .Select(x => x.OriginalValues.GetValue<Guid>(nameof(SupplierInvoiceLine.SupplierInvoiceId))))
            .Where(id => !parents.Any(x => x.State == EntityState.Added && x.Entity.Id == id))
            .Distinct().ToArray();
        if (affected.Length == 0 || !InvoiceAmountCalculationRelationAvailable()) return;
        foreach (var id in affected)
        {
            // The database child is authoritative. A detached/tracked substitute
            // must not turn an immutable v2 original into an editable v1 invoice.
            var calculation = InvoiceAmountCalculations.AsNoTracking()
                .SingleOrDefault(x => x.SupplierInvoiceId == id);
            if (calculation is null) continue;
            var parent = parents.SingleOrDefault(x => x.Entity.Id == id)?.Entity
                ?? SupplierInvoices.AsNoTracking().Single(x => x.Id == id);
            var original = SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == id)
                .ToDictionary(x => x.Id);
            foreach (var entry in lines.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                // Remove the persisted image first, including a line moved to a
                // different parent. Both parents are in the affected set above.
                original.Remove(entry.Entity.Id);
                if (entry.State != EntityState.Deleted && entry.Entity.SupplierInvoiceId == id)
                    original.Add(entry.Entity.Id, entry.Entity);
            }
            _ = InvoiceAmountCalculationEvidence.Verify(calculation, parent, original.Values.ToArray());
        }
    }

    private bool InvoiceAmountCalculationRelationAvailable()
    {
        if (!Database.IsNpgsql()) return true;
        var connection = Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (openedHere) connection.Open();
            using var command = connection.CreateCommand();
            command.Transaction = Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 15;
            command.CommandText = "SELECT CASE WHEN to_regclass('procurement.invoice_amount_calculations') IS NULL THEN 0 "
                + "WHEN EXISTS(SELECT 1 FROM pg_class WHERE oid=to_regclass('procurement.invoice_amount_calculations') "
                + "AND relkind='r' AND relpersistence='p' AND NOT relrowsecurity AND NOT relforcerowsecurity) THEN 1 ELSE -1 END";
            var available = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (available is not (0 or 1))
                throw new InvalidOperationException("InvoiceAmountCalculationPhysicalRelationRequired");
            // Once present, any malformed schema/permission/query error is a
            // denial, never a reason to silently classify the invoice as v1.
            return available == 1;
        }
        finally { if (openedHere) connection.Close(); }
    }

}
