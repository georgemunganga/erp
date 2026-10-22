using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    /// <summary>Raw database invariant negatives only, against two genuine HTTP-created
    /// governed RFQs. Bootstrap mutations exist only inside rolled-back transactions.
    /// No fake successful authority, disabled constraints, row deletion or hash rewriting.</summary>
    private async Task VerifyRfqRawInvariants(Guid eventId, Guid otherEventId)
    {
        Require(eventId != otherEventId, "Raw RFQ ownership proof requires two distinct actual events.");
        var actorScope = new FinanceSchemaScope(OrderCompany, OrderBuyer);
        ProcurementDbContext Runtime() => new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(runtimeConnection, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, actorScope);
        Guid evidenceId, otherEvidenceId, bindingId, lineId, allocationId, receiptId;
        await using (var db = Runtime())
        {
            var events = await db.SourcingEvents.AsNoTracking().Where(x => x.Id == eventId || x.Id == otherEventId).ToArrayAsync();
            Require(events.Length == 2 && events.All(x => x.ExecutionVersion == 1 && x.Status == "Draft"
                && x.TenantId == "probe" && x.LegalEntityId == OrderCompany && x.SourceEvidenceId is not null),
                "Raw negatives need actual governed draft owners within SAME tenant/company.");
            evidenceId = events.Single(x => x.Id == eventId).SourceEvidenceId!.Value;
            otherEvidenceId = events.Single(x => x.Id == otherEventId).SourceEvidenceId!.Value;
            var binding = await db.Set<SourcingDemandBinding>().AsNoTracking().Where(x => x.SourcingEventId == eventId)
                .OrderBy(x => x.Id).FirstAsync();
            bindingId = binding.Id; lineId = binding.SourcingLineId; allocationId = binding.AllocationId;
            receiptId = await db.Set<SourcingCommandReceipt>().AsNoTracking().Where(x => x.SourcingEventId == eventId && x.Operation == "Create")
                .Select(x => x.Id).SingleAsync();
        }
        async Task<string> RetainedPins()
        {
            // A fresh actual RESTRICTED connection after every rollback, not the mutation
            // connection/transaction or an EF tracker that could retain original values.
            await using var db = Runtime();
            var events = await db.SourcingEvents.AsNoTracking().Where(x => x.Id == eventId || x.Id == otherEventId).OrderBy(x => x.Id).ToArrayAsync();
            var evidence = await db.Set<SourcingDraftEvidence>().AsNoTracking().Where(x => x.SourcingEventId == eventId || x.SourcingEventId == otherEventId).OrderBy(x => x.Id).ToArrayAsync();
            var lines = await db.SourcingLines.AsNoTracking().Where(x => x.SourcingEventId == eventId || x.SourcingEventId == otherEventId).OrderBy(x => x.Id).ToArrayAsync();
            var bindings = await db.Set<SourcingDemandBinding>().AsNoTracking().Where(x => x.SourcingEventId == eventId || x.SourcingEventId == otherEventId).OrderBy(x => x.Id).ToArrayAsync();
            var allocationIds = bindings.Select(x => x.AllocationId).ToArray();
            var allocations = await db.PurchaseRequestAllocations.AsNoTracking().Where(x => allocationIds.Contains(x.Id)).OrderBy(x => x.Id).ToArrayAsync();
            var receipts = await db.Set<SourcingCommandReceipt>().AsNoTracking().Where(x => x.SourcingEventId == eventId || x.SourcingEventId == otherEventId).OrderBy(x => x.Id).ToArrayAsync();
            Require(evidence.Length == 2 && evidence.All(x => x.PayloadSha256 == ProcurementEvidenceEncoding.Digest(x.PayloadJson)
                    && events.Single(e => e.Id == x.SourcingEventId).OriginalDraftFingerprint == x.OriginalDraftFingerprint)
                && receipts.All(x => x.ResponseSha256 == ProcurementEvidenceEncoding.Digest(x.ResponseJson)),
                "Independent retained original/response hashes no longer verify.");
            return JsonSerializer.Serialize(new { events, evidence, lines, bindings, allocations, receipts });
        }
        var original = await RetainedPins();
        var cases = new (string Label, string Sql, string Code, string Constraint)[]
        {
            ("event-required-method-revision", "UPDATE procurement.sourcing_events SET method_revision=NULL WHERE id=@event", "23514", "ck_sourcing_owned_draft"),
            ("original-envelope-schema", "UPDATE procurement.sourcing_draft_evidence SET schema_version=2 WHERE id=@evidence", "23514", "ck_rfq_original_envelope"),
            ("binding-negative-quantity", "UPDATE procurement.sourcing_demand_bindings SET quantity=-1 WHERE id=@binding", "23514", "ck_rfq_binding_value"),
            ("receipt-schema", "UPDATE procurement.sourcing_command_receipts SET response_schema_version=2 WHERE id=@receipt", "23514", "ck_rfq_receipt_identity"),
            // Same scope and valid referenced UUIDs; only exact owning event/source tuple differs.
            ("event-other-original", "UPDATE procurement.sourcing_events SET source_evidence_id=@otherEvidence WHERE id=@event", "23503", "fk_rfq_event_original"),
            ("binding-other-original", "UPDATE procurement.sourcing_demand_bindings SET source_evidence_id=@otherEvidence WHERE id=@binding", "23503", "fk_rfq_binding_original"),
            // Move the existing parent line, choosing an unused number on the other real event.
            // This avoids the unique binding-line index masking the exact parent ownership FK.
            ("line-other-event", "UPDATE procurement.sourcing_lines SET sourcing_event_id=@otherEvent,line_number=(SELECT coalesce(max(line_number),0)+1 FROM procurement.sourcing_lines WHERE sourcing_event_id=@otherEvent) WHERE id=@line", "23503", "fk_rfq_binding_exact_line"),
            // Change original parent target rather than adopt another already-bound allocation:
            // no unique allocation index collision, and all original PR/source/value fields stay.
            ("allocation-other-event", "UPDATE procurement.purchase_request_allocations SET target_id=@otherEvent WHERE id=@allocation", "23503", "fk_rfq_binding_exact_hold"),
        };
        foreach (var item in cases)
        {
            var before = await RfqFootprint();
            await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
            await using var transaction = await owner.BeginTransactionAsync();
            string? sqlState = null, constraint = null;
            try
            {
                await using var update = new NpgsqlCommand(item.Sql, owner, transaction);
                update.Parameters.AddWithValue("event", eventId); update.Parameters.AddWithValue("otherEvent", otherEventId);
                update.Parameters.AddWithValue("evidence", evidenceId); update.Parameters.AddWithValue("otherEvidence", otherEvidenceId);
                update.Parameters.AddWithValue("binding", bindingId); update.Parameters.AddWithValue("line", lineId);
                update.Parameters.AddWithValue("allocation", allocationId); update.Parameters.AddWithValue("receipt", receiptId);
                Require(await update.ExecuteNonQueryAsync() == 1, "Raw negative did not target exactly one actual producer row: " + item.Label);
                // Deferred FK execution success is not a denial. Force the pending exact FK
                // checks now; a rollback without this step would give vacuous coverage.
                await using var immediate = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", owner, transaction);
                await immediate.ExecuteNonQueryAsync();
            }
            catch (PostgresException error) { sqlState = error.SqlState; constraint = error.ConstraintName; }
            finally { await transaction.RollbackAsync(); }
            Require(before == await RfqFootprint() && original == await RetainedPins(),
                "Raw RFQ negative/rollback changed original hashes/fields/counters/audit/outbox: " + item.Label);
            Require(sqlState == item.Code && constraint == item.Constraint,
                "Raw RFQ negative expected " + item.Code + "/" + item.Constraint + ", got " + (sqlState ?? "no-denial") + "/" + constraint + ": " + item.Label);
        }
        Console.WriteLine("PASS P28 raw4CHECK23514 +4exactdeferredFK23503 named denials on actual same-tenant/company producer rows; rollback and fresh restricted hash/column reload unchanged. Raw invariants are not business authorization proof.");
    }
}
