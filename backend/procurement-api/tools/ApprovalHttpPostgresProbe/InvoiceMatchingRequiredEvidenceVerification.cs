using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    /// <summary>Raw CHECK proof only, in owner transactions that always roll back.
    /// Synthetic Verified SQL values are never an authenticated owner/provider or persisted claim.</summary>
    private async Task VerifyInvoiceMatchingRequiredEvidence(Guid invoiceId,Guid resultId)
    {
        Guid exposureId;
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var result=await db.InvoiceMatchResults.AsNoTracking().SingleAsync(x=>x.Id==resultId && x.SupplierInvoiceId==invoiceId);
            Require(result.ExecutionVersion==1 && result.Outcome=="Blocked" && result.InvoiceOriginalSha256 is not null && result.InputFingerprint is not null
                && result.EvidenceSha256 is not null && result.PolicyRulesSha256 is not null && result.EvidenceJson is not null && result.SourceWatermarksJson is not null,
                "CHECK probe requires an otherwise complete actual persisted current comparison.");
            exposureId=await db.InvoiceMatchExposureStates.Where(x=>x.PurchaseOrderId==result.PurchaseOrderId && x.IssuedVersionId==result.IssuedVersionId).Select(x=>x.Id).SingleAsync();
        }
        async Task<string> Snapshot()
        {
            using var services=Scope(OrderBuyer); var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new { result=await db.InvoiceMatchResults.AsNoTracking().SingleAsync(x=>x.Id==resultId),
                exposure=await db.InvoiceMatchExposureStates.AsNoTracking().SingleAsync(x=>x.Id==exposureId),
                allocations=await db.InvoiceMatchAllocations.CountAsync(),handoffs=await db.FinanceInvoiceHandoffs.CountAsync(),payments=await db.FinancePaymentStatusRefs.CountAsync() },Json);
        }
        var before=await Snapshot(); await using var owner=new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        async Task Execute(string table,Guid target,string set,bool denied,string constraint,bool verifiedEnvelope=false)
        {
            await using var transaction=await owner.BeginTransactionAsync();
            try
            {
                if(verifiedEnvelope)
                {
                    await using var prepare=new NpgsqlCommand("""
                        UPDATE procurement.invoice_match_exposure_states
                        SET coverage_state='Verified',owner_reference='SYNTHETIC-ROLLBACK-NOT-AN-OWNER',
                            owner_watermark='SYNTHETIC-ROLLBACK-ONLY',owner_evidence_sha256=repeat('a',64),owner_valid_until=now()+interval '1 hour'
                        WHERE tenant_id='probe' AND legal_entity_id=@company AND id=@id
                        """,owner,transaction);
                    prepare.Parameters.AddWithValue("company",OrderCompany);prepare.Parameters.AddWithValue("id",target);
                    Require(await prepare.ExecuteNonQueryAsync()==1,"Otherwise-valid rollback exposure envelope was not prepared.");
                }
                // table/set are internal fixed allowlists below, never HTTP/user input.
                await using var mutation=new NpgsqlCommand($"UPDATE procurement.{table} SET {set} WHERE tenant_id='probe' AND legal_entity_id=@company AND id=@id",owner,transaction);
                mutation.Parameters.AddWithValue("company",OrderCompany);mutation.Parameters.AddWithValue("id",target);
                Require(await mutation.ExecuteNonQueryAsync()==1,"CHECK probe did not reach its actual target row.");
                Require(!denied,"Nullable current evidence passed CHECK via SQL unknown: "+set);
                await transaction.RollbackAsync();
            }
            catch(PostgresException error) when(denied && error.SqlState==PostgresErrorCodes.CheckViolation && error.ConstraintName==constraint)
            { await transaction.RollbackAsync(); }
        }
        // Positive controls first. Failures cannot be attributed to an invalid source/FK/body fixture.
        await Execute("invoice_match_results",resultId,"evidence_sha256=evidence_sha256",false,"ck_invoice_match_execution");
        await Execute("invoice_match_exposure_states",exposureId,"coverage_state=coverage_state",false,"ck_invoice_exposure_state",true);
        foreach(var column in new[] { "invoice_original_sha256","input_fingerprint","evidence_sha256","policy_rules_sha256","evidence_json","source_watermarks_json" })
            await Execute("invoice_match_results",resultId,column+"=NULL",true,"ck_invoice_match_execution");
        await Execute("invoice_match_results",resultId,"policy_rules_sha256='not-a-valid-sha'",true,"ck_invoice_match_execution");
        foreach(var column in new[] { "owner_reference","owner_watermark","owner_evidence_sha256","owner_valid_until" })
            await Execute("invoice_match_exposure_states",exposureId,column+"=NULL",true,"ck_invoice_exposure_state",true);
        // Legacy current-model columns remain nullable behind execution0; no backfill is fabricated.
        await Execute("invoice_match_results",resultId,"execution_version=0,invoice_original_sha256=NULL,input_fingerprint=NULL,evidence_sha256=NULL,policy_rules_sha256=NULL,evidence_json=NULL,source_watermarks_json=NULL",false,"ck_invoice_match_execution");
        await Execute("invoice_match_exposure_states",exposureId,"coverage_state='Unknown',owner_reference=NULL,owner_watermark=NULL,owner_evidence_sha256=NULL,owner_valid_until=NULL",false,"ck_invoice_exposure_state");
        Require(before==await Snapshot(),"Required-evidence raw probes persisted synthetic exposure or changed business evidence.");
        Console.WriteLine("PASS P26 required-evidence CHECK controls: six current-result NULLs, malformed policy SHA, four Verified exposure NULLs rejected23514; legacy/Unknown compatible; all raw mutations rolled back.");
    }
}
