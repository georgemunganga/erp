using Npgsql;

sealed partial class Probe
{
    /// <summary>Disposable owner-only negative schema probe, after real physical/inspection
    /// commands produced valid records. Each attempt changes ONLY one nullable sequence,
    /// requires its named CHECK violation, rolls back, and verifies the retained original.</summary>
    private async Task VerifyReceivingRequiredSequences(Guid goodsReceiptLineId)
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        foreach (var (table, column, predicate, constraint) in new[]
        {
            ("goods_receipt_lines", "physical_sequence", "id", "ck_receiving_line_execution"),
            ("receipt_inspections", "ledger_sequence", "goods_receipt_line_id", "ck_receiving_inspection_execution"),
        })
        {
            // All SQL identifiers come only from the fixed tuple above; identities are parameters.
            var where = $"tenant_id='probe' AND legal_entity_id=@company AND {predicate}=@line";
            async Task<object?> Read()
            {
                await using var read = new NpgsqlCommand($"SELECT {column} FROM procurement.{table} WHERE {where}", owner);
                read.Parameters.AddWithValue("company", OrderCompany); read.Parameters.AddWithValue("line", goodsReceiptLineId);
                return await read.ExecuteScalarAsync();
            }
            var before = await Read();
            Require(before is long sequence && sequence > 0, "Required-sequence denial fixture must start with actual valid recorded evidence.");
            await using (var transaction = await owner.BeginTransactionAsync())
            {
                try
                {
                    await using var update = new NpgsqlCommand($"UPDATE procurement.{table} SET {column}=NULL WHERE {where}", owner, transaction);
                    update.Parameters.AddWithValue("company", OrderCompany); update.Parameters.AddWithValue("line", goodsReceiptLineId);
                    await update.ExecuteNonQueryAsync();
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Missing required receiving sequence passed PostgreSQL CHECK: " + constraint);
                }
                catch (PostgresException error) when (error.SqlState == "23514" && error.ConstraintName == constraint)
                { await transaction.RollbackAsync(); }
            }
            Require(Equals(before, await Read()), "Rejected missing sequence changed retained receiving evidence.");
        }
        Console.WriteLine("PASS receiving required sequences: real valid physical/inspection rows deny NULL sequence through named PostgreSQL checks and retain originals.");
    }
}
