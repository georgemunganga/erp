using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Complete company inventory in the already-owned serializable
    /// transaction. No table name or completeness assertion comes from HTTP.
    /// This read alone does not classify rows or admit an owner baseline.</summary>
    public async Task<InvoiceProcurementInventoryCapture> CaptureInvoiceOwnerInventoryAsync(CancellationToken ct,
        Func<InvoiceAmountCoreReferenceRequirement, CancellationToken, Task<bool>>? verifyCalculationReference = null,
        bool openingNewBaseline = true)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty)
            throw new InvalidOperationException("InvoiceOwnerSerializableInventoryRequired");
        // A later RLS policy must fail the owner capture instead of masking rows.
        await Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        var connection=Database.GetDbConnection();
        var transaction=Database.CurrentTransaction.GetDbTransaction();
        var tables=new List<InvoiceProcurementCapturedTable>(InvoiceProcurementOwnerTables.Names.Count);
        var totalRows=0;
        var totalBytes=0L;
        foreach(var table in InvoiceProcurementOwnerTables.Names)
        {
            await using var command=connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandTimeout=15;
            command.CommandText=$"SELECT id, version, row_to_json(t)::text FROM procurement.{table} AS t WHERE tenant_id=@tenant AND legal_entity_id=@company ORDER BY id";
            var tenant=command.CreateParameter();tenant.ParameterName="tenant";tenant.DbType=DbType.String;tenant.Value=scope.TenantId;command.Parameters.Add(tenant);
            var company=command.CreateParameter();company.ParameterName="company";company.DbType=DbType.Guid;company.Value=scope.LegalEntityId;command.Parameters.Add(company);
            var rows=new List<InvoiceProcurementCapturedRow>();
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
            {
                var id=reader.GetGuid(0);var version=reader.GetInt64(1);
                var json=InvoiceProcurementIssuedCanonicalRow.Project(table,
                    InvoiceProcurementAllocationCanonicalRow.Project(table,reader.GetString(2)));
                var bytes=Encoding.UTF8.GetByteCount(json);
                totalRows++;totalBytes+=bytes;
                if(id==Guid.Empty || version<1 || bytes is < 1 or > 1_048_576
                    || totalRows>100_000 || totalBytes>100_000_000)
                    throw new InvalidOperationException("InvoiceOwnerInventoryBoundExceeded");
                var sha=ProcurementEvidenceEncoding.PolicyJsonDigest(json);
                if(sha.Length!=64) throw new InvalidOperationException("InvoiceOwnerInventoryRowUnclassifiable");
                rows.Add(new(id,version,sha,json));
            }
            tables.Add(new(table,rows));
        }
        var capture = new InvoiceProcurementInventoryCapture(scope.TenantId,scope.LegalEntityId,tables);
        _ = await VerifyInvoiceAmountInventoryAsync(capture, openingNewBaseline, verifyCalculationReference, ct);
        return capture;
    }
}
