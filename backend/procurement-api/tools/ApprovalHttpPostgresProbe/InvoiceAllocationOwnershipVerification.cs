using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using NpgsqlTypes;

sealed partial class Probe
{
    private async Task VerifyInvoiceAllocationOwnership(Guid invoiceId,Guid resultId)
    {
        Guid invoiceLine,order,issued,line,supplier,state,exposure,goods,wrongGoods,wrongService;
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var result=await db.InvoiceMatchResults.SingleAsync(x=>x.Id==resultId && x.SupplierInvoiceId==invoiceId);
            Require(result.Outcome=="Blocked","Raw FK fixture must not pretend to be a qualified comparison.");
            order=result.PurchaseOrderId!.Value;issued=result.IssuedVersionId!.Value;
            var invoice=await db.SupplierInvoices.SingleAsync(x=>x.Id==invoiceId);supplier=invoice.SupplierId;
            invoiceLine=await db.SupplierInvoiceLines.Where(x=>x.SupplierInvoiceId==invoiceId).Select(x=>x.Id).SingleAsync();
            var source=await db.ReceivingSourceStates.FirstAsync(x=>x.PurchaseOrderId==order && x.IssuedVersionId==issued);
            state=source.Id;line=source.PurchaseOrderLineId;
            exposure=await db.InvoiceMatchExposureStates.Where(x=>x.PurchaseOrderId==order && x.IssuedVersionId==issued).Select(x=>x.Id).SingleAsync();
            goods=await db.GoodsReceiptLines.Where(x=>x.ReceivingSourceStateId==state && x.AcceptedQuantity>0).Select(x=>x.Id).FirstAsync();
            wrongGoods=await db.GoodsReceiptLines.Where(x=>x.ExecutionVersion==1 && x.PurchaseOrderLineId!=line && x.ReceivingSourceStateId!=null).Select(x=>x.Id).FirstAsync();
            wrongService=await db.ServiceAcceptances.Where(x=>x.ExecutionVersion==1 && x.PurchaseOrderLineId!=line && x.CertifiedAt!=null).Select(x=>x.Id).FirstAsync();
        }
        await using var owner=new NpgsqlConnection(databaseConnection);await owner.OpenAsync();
        async Task<bool> Manifest(NpgsqlTransaction? transaction=null)
        { await using var command=new NpgsqlCommand(InvoiceMatchingOwnershipConstraintSql.ManifestQuery,owner,transaction);return await command.ExecuteScalarAsync() is true; }
        Require(await Manifest(),"P26 exact allocation ownership overlay missing after migration.");
        foreach(var mutation in new[] {
            "ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods",
            "ALTER TABLE procurement.invoice_match_allocations ALTER CONSTRAINT fk_invoice_allocation_exact_goods NOT DEFERRABLE",
            "ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods; ALTER TABLE procurement.invoice_match_allocations ADD CONSTRAINT fk_invoice_allocation_exact_goods FOREIGN KEY (tenant_id,legal_entity_id,goods_receipt_line_id) REFERENCES procurement.goods_receipt_lines(tenant_id,legal_entity_id,id) DEFERRABLE INITIALLY DEFERRED",
            "ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods; ALTER TABLE procurement.invoice_match_allocations ADD CONSTRAINT fk_invoice_allocation_exact_goods FOREIGN KEY (tenant_id,legal_entity_id,goods_receipt_line_id,purchase_order_line_id,receiving_source_state_id) REFERENCES procurement.goods_receipt_lines(tenant_id,legal_entity_id,id,purchase_order_line_id,source_state_binding_id) DEFERRABLE INITIALLY DEFERRED NOT VALID" })
        {
            await using var transaction=await owner.BeginTransactionAsync();
            await using var alter=new NpgsqlCommand(mutation,owner,transaction);await alter.ExecuteNonQueryAsync();
            Require(!await Manifest(transaction),"P26 manifest accepted absent/lookalike/nondeferred/unvalidated ownership.");
            await transaction.RollbackAsync();Require(await Manifest(),"P26 ownership rollback failed.");
        }
        async Task Insert(Guid? goodsId,Guid? serviceId,string constraint,bool valid)
        {
            await using var transaction=await owner.BeginTransactionAsync();
            try
            {
                await using var command=new NpgsqlCommand("""
                    INSERT INTO procurement.invoice_match_allocations
                    (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_invoice_id,supplier_invoice_line_id,
                     match_result_id,exposure_state_id,purchase_order_id,issued_version_id,purchase_order_line_id,supplier_id,
                     receiving_source_state_id,goods_receipt_line_id,service_acceptance_id,kind,currency_code,unit_code,
                     quantity,net_amount,tax_amount,exposure_sequence,validation_reference,owner_coverage_reference)
                    VALUES (@id,'probe',@company,now(),'synthetic-rollback-only',1,@invoice,@invoiceLine,@result,@exposure,
                     @order,@issued,@line,@supplier,@state,@goods,@service,'Debit','ZMW','each',0.1,10,0,1,
                     'SYNTHETIC-ROLLED-BACK-FK-PROBE','NO-AUTHORITATIVE-OWNER-CLAIM')
                    """,owner,transaction);
                foreach(var (name,value) in new[] { ("id",Guid.NewGuid()),("company",OrderCompany),("invoice",invoiceId),("invoiceLine",invoiceLine),
                    ("result",resultId),("exposure",exposure),("order",order),("issued",issued),("line",line),("supplier",supplier),("state",state) }) command.Parameters.AddWithValue(name,value);
                command.Parameters.AddWithValue("goods",NpgsqlDbType.Uuid,goodsId.HasValue?(object)goodsId.Value:DBNull.Value);
                command.Parameters.AddWithValue("service",NpgsqlDbType.Uuid,serviceId.HasValue?(object)serviceId.Value:DBNull.Value);
                await command.ExecuteNonQueryAsync();
                await using var check=new NpgsqlCommand($"SET CONSTRAINTS procurement.{constraint} IMMEDIATE",owner,transaction);await check.ExecuteNonQueryAsync();
                Require(valid,"P26 accepted another receiving record's exact line/state binding.");
                await transaction.RollbackAsync();
            }
            catch(PostgresException error) when(!valid && error.SqlState=="23503" && error.ConstraintName==constraint)
            { await transaction.RollbackAsync(); }
        }
        // All synthetic allocations roll back. They test DB relations, not business authority.
        await Insert(goods,null,InvoiceMatchingOwnershipConstraintSql.Goods,true);
        await Insert(wrongGoods,null,InvoiceMatchingOwnershipConstraintSql.Goods,false);
        await Insert(null,wrongService,InvoiceMatchingOwnershipConstraintSql.Service,false);
        using(var services=Scope(OrderBuyer)) Require(!await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().InvoiceMatchAllocations.AnyAsync(),
            "Ownership-only rollback probes persisted a fabricated claim.");
        Console.WriteLine("PASS P26 ownership manifest tamper denials and rolled-back exact goods/service cross-binding FK probes; no qualified claims fabricated.");
    }
}
