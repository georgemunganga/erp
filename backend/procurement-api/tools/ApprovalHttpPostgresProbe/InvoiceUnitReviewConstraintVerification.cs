using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Api;
using Npgsql;
sealed partial class Probe
{
    private async Task VerifyInvoiceUnitReviewConstraints()
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        await using var owner = new NpgsqlConnection(db.Database.GetDbConnection().ConnectionString); await owner.OpenAsync();
        var before = await UnitReviewFootprint();
        var negatives = new (string Sql, string State, string Name)[] {
            ("UPDATE procurement.invoice_unit_review_sources SET source_sha256='invalid'", "23514", "ck_inv_unit_source"),
            ("UPDATE procurement.invoice_unit_review_assignments SET worker_id='00000000-0000-0000-0000-000000000000'", "23514", "ck_inv_unit_assignment"),
            ("UPDATE procurement.invoice_unit_review_outcomes SET evidence_schema_version=1", "23514", "ck_inv_unit_outcome"),
            ("UPDATE procurement.invoice_unit_review_receipts SET operation='Unknown'", "23514", "ck_inv_unit_receipt"),
            ($"UPDATE procurement.invoice_unit_review_sources SET mapping_version_id='{Guid.NewGuid()}'", "23503", "fk_inv_unit_source_mapping"),
            ($"UPDATE procurement.invoice_unit_review_sources SET review_policy_id='{Guid.NewGuid()}'", "23503", "fk_inv_unit_source_review_policy"),
            ($"UPDATE procurement.invoice_unit_review_assignments SET core_case_id='{Guid.NewGuid()}'", "23503", "fk_inv_unit_assignment_source"),
            ($"UPDATE procurement.invoice_unit_review_outcomes SET review_source_id='{Guid.NewGuid()}'", "23503", "fk_inv_unit_outcome_source"),
            ($"UPDATE procurement.invoice_unit_review_receipts SET review_source_id='{Guid.NewGuid()}'", "23503", "fk_inv_unit_receipt_source"),
        };
        foreach (var value in negatives)
        {
            await using var tx = await owner.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(value.Sql, owner, tx); await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Expected unit-review SQL denial: " + value.Name);
            }
            catch (PostgresException error) when (error.SqlState == value.State && error.ConstraintName == value.Name) { }
            finally { await tx.RollbackAsync(); }
        }
        Require(before == await UnitReviewFootprint(), "Raw owner constraint negatives changed retained evidence.");
        Require(OrderDistinctRuntime && InvoiceDocumentStoreBinding.SamePhysicalDatabase(databaseConnection, runtimeConnection)
            && new NpgsqlConnectionStringBuilder(databaseConnection).Username != new NpgsqlConnectionStringBuilder(runtimeConnection).Username,
            "Unit-review append-evidence denials require the distinct finite runtime on the same actual store.");
        await using var runtime = new NpgsqlConnection(runtimeConnection); await runtime.OpenAsync();
        foreach (var table in new[] { "invoice_unit_review_sources", "invoice_unit_review_assignments", "invoice_unit_review_outcomes", "invoice_unit_review_receipts" })
            foreach (var sql in new[] { $"UPDATE procurement.{table} SET version=version WHERE false", $"DELETE FROM procurement.{table} WHERE false", $"TRUNCATE procurement.{table}" })
            {
                await using var tx = await runtime.BeginTransactionAsync();
                try
                {
                    await using var command = new NpgsqlCommand(sql, runtime, tx); await command.ExecuteNonQueryAsync();
                    throw new InvalidOperationException("Finite runtime could rewrite unit-review append evidence.");
                }
                catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InsufficientPrivilege) { }
                finally { await tx.RollbackAsync(); }
            }
        Require(before == await UnitReviewFootprint(), "Finite-role denial tests changed persisted effects.");
        Console.WriteLine("PASS: 9 named unit-review CHECK/FK rollback negatives and12 finite UPDATE/DELETE/TRUNCATE denials.");
    }
}
