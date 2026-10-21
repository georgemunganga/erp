using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    // TOOL ONLY: no P28 entity, readiness override, source allocation or production route changes.
    public async Task VerifyLegacySourcing()
    {
        Require(OrderDistinctRuntime, "Legacy sourcing proof requires separate owner/runtime identities.");
        Dictionary<string, string> footprint;
        Dictionary<Guid, string> previousAudits;
        string[] sourcingTables;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Require(applied.LastOrDefault() == "20261020190554_P27FinanceCheckerIdentity"
                && applied.All(x => !x.Contains("P28", StringComparison.Ordinal)),
                "Legacy fixture must run against the actual P27 migration tip, never P28.");
            Require(await db.OrderIssuedVersions.AnyAsync() && await db.OrderFinanceEvidence.AnyAsync(),
                "Legacy rehearsal requires genuinely used PO/Finance evidence from the real HTTP workflow.");
            sourcingTables = new[] { typeof(SourcingEvent), typeof(SourcingLine), typeof(AuditEvent) }
                .Select(t => db.Model.FindEntityType(t)!.GetTableName()!).ToArray();
            await RequireExactLegacyColumns(db);
            footprint = await LegacyUnrelatedFootprint(sourcingTables);
            previousAudits = await db.AuditEvents.AsNoTracking().ToDictionaryAsync(x => x.Id,
                x => JsonSerializer.Serialize(x, Json));
        }
        var input = new
        {
            title = "Pre-P28 legacy RFQ cancellation preservation", method = "RFQ", currencyCode = "ZMW",
            lines = new[] {
                new { description = "Retained legacy RFQ line one", unitCode = "EACH", quantity = 1.0001m, categoryCode = "IT" },
                new { description = "Retained legacy RFQ line two", unitCode = "EACH", quantity = 2m, categoryCode = "IT" }
            }
        };
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, Root + "/sourcing", OrderRequester, input);
        var created = await Send(HttpMethod.Post, Root + "/sourcing", OrderBuyer, input);
        await Check(created, HttpStatusCode.Created);
        var draft = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.Clone();
        var id = draft.GetProperty("id").GetGuid();
        var ids = draft.GetProperty("lines").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        const string reason = "Synthetic pre-P28 cancellation retained for upgrade/down/upgrade rehearsal";
        var cancelled = await Send(HttpMethod.Post, $"{Root}/sourcing/{id}/cancel", OrderBuyer,
            new { version = draft.GetProperty("version").GetInt64(), reason });
        await Check(cancelled, HttpStatusCode.OK);
        var history = await Get($"{Root}/sourcing/{id}", OrderBuyer);
        Require(history.GetProperty("status").GetString() == "Cancelled"
            && history.GetProperty("lines").GetArrayLength() == 2,
            "Legacy HTTP GET lost the cancelled event/retained lines.");
        // A fresh owning context/physical connection independently checks persisted effects.
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var item = await db.SourcingEvents.AsNoTracking().SingleAsync(x => x.Id == id);
            var lines = await db.SourcingLines.AsNoTracking().Where(x => x.SourcingEventId == id).ToArrayAsync();
            Require(item.Status == "Cancelled" && item.CancellationReason == reason
                && item.OwnerSubjectId == OrderBuyer.ToString("D") && item.TenantId == "probe"
                && item.LegalEntityId == OrderCompany && lines.Length == 2
                && ids.SetEquals(lines.Select(x => x.Id))
                && lines.All(x => x.Status == "Cancelled" && x.PurchaseRequestLineId == null
                    && x.TenantId == "probe" && x.LegalEntityId == OrderCompany)
                && lines.Single(x => x.LineNumber == 1).Quantity == 1.0001m,
                "Legacy cancellation must preserve original line IDs/quantity and not bind demand.");
            var audit = await db.AuditEvents.AsNoTracking().SingleAsync(x => x.RecordType == "SourcingEvent"
                && x.RecordId == id && x.Action == "DraftCancelled");
            using var payload = JsonDocument.Parse(audit.ChangedFieldsJson!);
            var before = payload.RootElement.GetProperty("before");
            var after = payload.RootElement.GetProperty("after");
            Require(audit.ActorSubjectId == OrderBuyer.ToString("D")
                && before.GetProperty("Status").GetString() == "Draft"
                && after.GetProperty("Status").GetString() == "Cancelled"
                && after.GetProperty("CancellationReason").GetString() == reason
                && before.GetProperty("lines").GetArrayLength() == 2
                && after.GetProperty("lines").GetArrayLength() == 2,
                "Real pre-P28 DraftCancelled before/after audit was lost or replaced.");
            var allAudits = await db.AuditEvents.AsNoTracking().ToArrayAsync();
            Require(previousAudits.All(old => allAudits.Any(x => x.Id == old.Key
                && JsonSerializer.Serialize(x, Json) == old.Value)), "Legacy operation rewrote existing audit history.");
            var addedAudits = allAudits.Where(x => !previousAudits.ContainsKey(x.Id)).ToArray();
            Require(addedAudits.Length == 8 && addedAudits.All(x =>
                    (x.Action is "create" or "update" or "DraftCreated" or "DraftCancelled")
                    && x.ActorSubjectId == OrderBuyer.ToString("D")
                    && ((x.RecordType == "SourcingEvent" && x.RecordId == id)
                        || (x.RecordType == "SourcingLine" && ids.Contains(x.RecordId)))),
                "Legacy operation emitted unrelated or governed-source audit evidence.");
            await RequireExactLegacyColumns(db);
            var afterFootprint = await LegacyUnrelatedFootprint(sourcingTables);
            Require(footprint.Count == afterFootprint.Count && footprint.All(x => afterFootprint.GetValueOrDefault(x.Key) == x.Value),
                "Legacy create/cancel changed allocation, Finance, approval, receiving, outbox or another unrelated table.");
            Console.WriteLine($"PASS legacy sourcing owner proof: event={id:D}; cancellationAudit={audit.Id:D}; cancellationPayloadSha256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(audit.ChangedFieldsJson!))).ToLowerInvariant()}; retainedLines=2; P27-only; unrelated table contents unchanged.");
        }
    }

    private async Task RequireExactLegacyColumns(ProcurementDbContext db)
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        foreach (var type in new[] { typeof(SourcingEvent), typeof(SourcingLine) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var expected = entity.GetProperties().Select(x => x.GetColumnName(table)!).ToHashSet(StringComparer.Ordinal);
            await using var command = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table", owner);
            command.Parameters.AddWithValue("schema", entity.GetSchema()!);
            command.Parameters.AddWithValue("table", entity.GetTableName()!);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            Require(expected.SetEquals(actual), "Legacy sourcing schema differs from the pre-P28 mapped model.");
        }
    }

    private async Task<Dictionary<string, string>> LegacyUnrelatedFootprint(string[] excludedTables)
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        var tables = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='procurement' ORDER BY tablename", owner))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) if (!excludedTables.Contains(reader.GetString(0), StringComparer.Ordinal)) tables.Add(reader.GetString(0));
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var quoted = "\"" + table.Replace("\"", "\"\"") + "\"";
            await using var command = new NpgsqlCommand($"SELECT md5(COALESCE(string_agg(row_to_json(t)::text, E'\\n' ORDER BY row_to_json(t)::text),'')) FROM procurement.{quoted} t", owner);
            hashes.Add(table, (string)(await command.ExecuteScalarAsync())!);
        }
        return hashes;
    }
}
