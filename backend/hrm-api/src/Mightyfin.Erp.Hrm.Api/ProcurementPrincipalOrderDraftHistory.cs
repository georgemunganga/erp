using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Shared retained reader for original P30 orders and P33 edited Drafts.
/// It never infers a missing origin or a missing revision from current values.</summary>
internal static class ProcurementPrincipalOrderDraftHistory
{
    internal sealed record Effective(decimal Quantity, decimal UnitPrice, decimal TaxAmount, decimal EstimatedValue);
    internal sealed record Verified(IReadOnlyDictionary<Guid, Effective> Effective, bool RevisionSchemaInstalled);

    // The shared genuine PG17 capture binds the complete three-table catalog.
    // A partial, substituted or changed graph cannot be mistaken for an empty
    // revision history, even when new revision commands are disabled.
    internal static string? CapturedMigrationId => ProcurementPrincipalOrderRevisionSchemaCapture.Populated
        ? ProcurementPrincipalOrderRevisionSchemaCapture.MigrationId : null;
    internal static string? CapturedCatalogSha256 => ProcurementPrincipalOrderRevisionSchemaCapture.HistoryCatalogSha256;

    internal static bool Classify(int count, int regularCount, bool migrationApplied, bool catalogMatches)
        => (count, regularCount) switch
        {
            (0, 0) when !migrationApplied => false,
            (3, 3) when migrationApplied && catalogMatches => true,
            _ => throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable")
        };

    internal static async Task<bool> InstalledAsync(ProcurementDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        command.CommandTimeout = 5;
        command.CommandText = """
            SELECT count(*),count(*) FILTER (WHERE c.relkind='r') FROM pg_catalog.pg_class c
            JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='procurement'
              AND c.relname IN ('principal_order_commercial_origins',
                'principal_order_draft_commercial_revisions',
                'principal_order_effective_source_slices')
            """;
        int count, regular;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable");
            count = Convert.ToInt32(reader.GetValue(0));
            regular = Convert.ToInt32(reader.GetValue(1));
        }
        command.CommandText = """
            SELECT EXISTS(SELECT 1 FROM procurement.__procurement_migrations
              WHERE "MigrationId" LIKE '%P33%')
            """;
        var anyP33History = await command.ExecuteScalarAsync(ct) is true;
        if (count == 0) return Classify(0, regular, anyP33History, false);
        var capturedMigrationId = CapturedMigrationId;
        var capturedCatalogSha256 = CapturedCatalogSha256;
        if (count != 3 || regular != 3 || capturedMigrationId is null
            || capturedCatalogSha256 is null || !anyP33History)
            throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable");
        if (db.Database.HasPendingModelChanges()
            || (await db.Database.GetPendingMigrationsAsync(ct)).Any()
            || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(capturedMigrationId,
                StringComparer.Ordinal))
            throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable");
        // The exact digest is captured from genuine P33 Up. Names alone cannot
        // admit an installed-but-altered graph; catalog reads remain enlisted
        // in the same transaction as the owner read or command.
        command.CommandText = """
            WITH owned AS (
              SELECT c.oid,c.relname FROM pg_catalog.pg_class c
              JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname='procurement' AND c.relkind='r'
                AND c.relname IN ('principal_order_commercial_origins',
                  'principal_order_draft_commercial_revisions',
                  'principal_order_effective_source_slices')
            ), shape AS (
              SELECT jsonb_build_object(
                'columns', (SELECT coalesce(jsonb_agg(jsonb_build_array(o.relname,a.attname,
                    pg_catalog.format_type(a.atttypid,a.atttypmod),a.attnotnull,
                    a.attidentity,a.attgenerated,
                    pg_catalog.pg_get_expr(d.adbin,d.adrelid)) ORDER BY o.relname,a.attnum),'[]'::jsonb)
                  FROM owned o JOIN pg_catalog.pg_attribute a ON a.attrelid=o.oid
                  LEFT JOIN pg_catalog.pg_attrdef d ON d.adrelid=o.oid AND d.adnum=a.attnum
                  WHERE a.attnum>0 AND NOT a.attisdropped),
                'constraints', (SELECT coalesce(jsonb_agg(jsonb_build_array(o.relname,k.conname,
                    k.contype,k.convalidated,k.condeferrable,k.condeferred,
                    pg_catalog.pg_get_constraintdef(k.oid)) ORDER BY o.relname,k.conname),'[]'::jsonb)
                  FROM owned o JOIN pg_catalog.pg_constraint k ON k.conrelid=o.oid),
                'indexes', (SELECT coalesce(jsonb_agg(jsonb_build_array(o.relname,i.relname,
                    x.indisvalid,x.indisready,x.indisunique,
                    pg_catalog.pg_get_indexdef(i.oid)) ORDER BY o.relname,i.relname),'[]'::jsonb)
                  FROM owned o JOIN pg_catalog.pg_index x ON x.indrelid=o.oid
                  JOIN pg_catalog.pg_class i ON i.oid=x.indexrelid)) AS value
            ) SELECT value::text FROM shape
            """;
        var shape = await command.ExecuteScalarAsync(ct) as string
            ?? throw new InvalidOperationException("PrincipalOrderRevisionSchemaUnavailable");
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shape))).ToLowerInvariant();
        return Classify(count, regular, true, string.Equals(actual, capturedCatalogSha256,
            StringComparison.Ordinal));
    }

    internal static async Task<IReadOnlyDictionary<Guid,Effective>> VerifyAsync(ProcurementDbContext db,
        PurchaseOrder order, IReadOnlyCollection<PurchaseOrderLine> lines,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings, CancellationToken ct)
        => (await VerifyStatusAsync(db, order, lines, bindings, ct)).Effective;

    internal static async Task<Verified> VerifyStatusAsync(ProcurementDbContext db,
        PurchaseOrder order, IReadOnlyCollection<PurchaseOrderLine> lines,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings, CancellationToken ct)
    {
        var installed = await InstalledAsync(db, ct);
        if (!installed)
        {
            var legacyCreates = await db.PrincipalOrderDraftReceipts.AsNoTracking()
                .Where(x => x.PurchaseOrderId == order.Id && x.Operation == "Create")
                .Take(2).ToArrayAsync(ct);
            PrincipalOrderCommercialStates.VerifyCreateReceipt(order,
                legacyCreates.Length == 1 ? legacyCreates[0] : null);
            PrincipalOrderDraftPins.Verify(order, lines, bindings);
            return new Verified(bindings.ToDictionary(x => x.Id, x => new Effective(x.Quantity,
                x.ProposedUnitPrice, lines.Single(line => line.Id == x.PurchaseOrderLineId).TaxAmount,
                x.EstimatedValue)), false);
        }
        var origin=await db.PrincipalOrderCommercialOrigins.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==order.Id,ct);
        var revisions=await db.PrincipalOrderDraftCommercialRevisions.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id).OrderBy(x=>x.RevisionNumber).ToArrayAsync(ct);
        var allReceipts=await db.PrincipalOrderDraftReceipts.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id).ToArrayAsync(ct);
        var creates=allReceipts.Where(x=>x.Operation=="Create").ToArray();
        PrincipalOrderCommercialStates.VerifyCreateReceipt(order,
            creates.Length==1?creates[0]:null);
        PrincipalOrderCommercialStates.VerifyReceipts(order,origin,revisions,
            allReceipts.Where(x=>x.Operation=="Edit").ToArray());
        if(origin is null)
        {
            if(revisions.Length!=0)throw new InvalidOperationException("PrincipalOrderDraftOriginMissing");
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            return new Verified(bindings.ToDictionary(x=>x.Id,x=>new Effective(x.Quantity,x.ProposedUnitPrice,
                lines.Single(line=>line.Id==x.PurchaseOrderLineId).TaxAmount,x.EstimatedValue)), true);
        }
        PrincipalOrderCommercialStates.VerifyOrigin(origin,order,bindings);
        if(revisions.Length==0)
        {
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            return new Verified(bindings.ToDictionary(x=>x.Id,x=>new Effective(x.Quantity,x.ProposedUnitPrice,
                lines.Single(line=>line.Id==x.PurchaseOrderLineId).TaxAmount,x.EstimatedValue)), true);
        }
        var slices=await db.PrincipalOrderEffectiveSourceSlices.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id).ToArrayAsync(ct);
        var allocationIds=bindings.Select(x=>x.AllocationId).ToArray();
        var allocations=await db.PurchaseRequestAllocations.AsNoTracking()
            .Where(x=>allocationIds.Contains(x.Id)).ToArrayAsync(ct);
        PrincipalOrderCommercialStates.VerifyRevision(order,lines,origin,revisions,slices,bindings,allocations);
        var latest=revisions[^1];
        return new Verified(slices.Where(x=>x.RevisionId==latest.Id).ToDictionary(x=>x.SourceBindingId,
            x=>new Effective(x.Quantity,x.UnitPrice,x.TaxAmount,x.EstimatedValue)), true);
    }
}
