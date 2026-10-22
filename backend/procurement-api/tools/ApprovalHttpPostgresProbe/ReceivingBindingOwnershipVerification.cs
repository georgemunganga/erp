using Npgsql;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyReceivingOwnershipManifest()
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        async Task<bool> Ready(NpgsqlTransaction? transaction = null)
        {
            await using var command = new NpgsqlCommand(ReceivingOwnershipConstraintSql.ManifestQuery, owner, transaction);
            return await command.ExecuteScalarAsync() is true;
        }
        Require(await Ready(), "Migrated receiving schema is missing exact deferred ownership constraints.");
        foreach (var mutation in new[] {
            "ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical",
            "ALTER TABLE procurement.receipt_inspections ALTER CONSTRAINT fk_receiving_inspection_exact_physical NOT DEFERRABLE",
            "ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical; ALTER TABLE procurement.receipt_inspections ADD CONSTRAINT fk_receiving_inspection_exact_physical FOREIGN KEY (tenant_id,legal_entity_id,goods_receipt_line_id) REFERENCES procurement.goods_receipt_lines(tenant_id,legal_entity_id,id) DEFERRABLE INITIALLY DEFERRED",
            "ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical; ALTER TABLE procurement.receipt_inspections ADD CONSTRAINT fk_receiving_inspection_exact_physical FOREIGN KEY (tenant_id,legal_entity_id,goods_receipt_line_id,receiving_source_state_id,physical_observation_id) REFERENCES procurement.goods_receipt_lines(tenant_id,legal_entity_id,id,source_state_binding_id,physical_binding_id) DEFERRABLE INITIALLY DEFERRED NOT VALID",
        })
        {
            await using var transaction = await owner.BeginTransactionAsync();
            await using var alter = new NpgsqlCommand(mutation, owner, transaction); await alter.ExecuteNonQueryAsync();
            Require(!await Ready(transaction), "Receiving ownership readiness accepted missing, nondeferred, lookalike or unvalidated constraints.");
            await transaction.RollbackAsync();
            Require(await Ready(), "Rollback did not restore the exact receiving ownership manifest.");
        }
        Console.WriteLine("PASS receiving ownership readiness: exact migrated manifest required; missing/wrong-shape/nondeferred/unvalidated constraints fail closed.");
    }
    private Task VerifyReceivingInspectionOwnership(Guid line) => ReceivingBindingDenial(
        "receipt_inspections", "goods_receipt_line_id", line,
        "physical_observation_id=@replacement, physical_binding_id=@replacement",
        "fk_receiving_inspection_exact_physical");
    private Task VerifyReceivingGoodsEffectOwnership(Guid line) => ReceivingBindingDenial(
        "receiving_acceptance_effects", "goods_receipt_line_id", line,
        "physical_observation_id=@replacement, inspection_evidence_id=NULL",
        "fk_receiving_effect_exact_physical");
    private async Task VerifyReceivingServiceEffectOwnership(Guid service, Guid goodsLine)
    {
        // A real, same-company OTHER source passes the simple state FK; exact service ownership must still deny it.
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using var read = new NpgsqlCommand("SELECT receiving_source_state_id FROM procurement.goods_receipt_lines WHERE id=@id AND tenant_id='probe' AND legal_entity_id=@company", owner);
        read.Parameters.AddWithValue("id", goodsLine); read.Parameters.AddWithValue("company", OrderCompany);
        var foreignState = (Guid)(await read.ExecuteScalarAsync() ?? throw new InvalidOperationException("Real physical state is required."));
        await ReceivingBindingDenial("receiving_acceptance_effects", "service_acceptance_id", service,
            "receiving_source_state_id=@replacement", "fk_receiving_effect_exact_service", foreignState);
    }
    private async Task ReceivingBindingDenial(string table, string targetColumn, Guid target,
        string mutation, string constraint, Guid? replacement = null)
    {
        // Identifiers/SQL originate only in the three private fixed callers; no user-controlled SQL.
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using (var metadata = new NpgsqlCommand("SELECT condeferrable AND condeferred AND convalidated FROM pg_constraint WHERE conname=@name AND connamespace='procurement'::regnamespace", owner))
        {
            metadata.Parameters.AddWithValue("name", constraint);
            Require(await metadata.ExecuteScalarAsync() is true, "Required deferred/validated receiving ownership FK is missing: " + constraint);
        }
        async Task<string> Snapshot()
        {
            await using var read = new NpgsqlCommand($"SELECT row_to_json(r)::text FROM procurement.{table} r WHERE tenant_id='probe' AND legal_entity_id=@company AND {targetColumn}=@target", owner);
            read.Parameters.AddWithValue("company", OrderCompany); read.Parameters.AddWithValue("target", target);
            return (string)(await read.ExecuteScalarAsync() ?? throw new InvalidOperationException("Actual receiving-produced row required for ownership denial."));
        }
        var before = await Snapshot();
        await using (var transaction = await owner.BeginTransactionAsync())
        {
            try
            {
                await using var update = new NpgsqlCommand($"UPDATE procurement.{table} SET {mutation} WHERE tenant_id='probe' AND legal_entity_id=@company AND {targetColumn}=@target", owner, transaction);
                update.Parameters.AddWithValue("company", OrderCompany); update.Parameters.AddWithValue("target", target);
                update.Parameters.AddWithValue("replacement", replacement ?? Guid.NewGuid());
                Require(await update.ExecuteNonQueryAsync() == 1, "Ownership fixture must mutate exactly one real row.");
                await using var check = new NpgsqlCommand($"SET CONSTRAINTS procurement.{constraint} IMMEDIATE", owner, transaction);
                await check.ExecuteNonQueryAsync();
                await transaction.RollbackAsync();
                throw new InvalidOperationException("Exact receiving ownership mismatch was accepted: " + constraint);
            }
            catch (PostgresException error) when (error.SqlState == "23503" && error.ConstraintName == constraint)
            { await transaction.RollbackAsync(); }
        }
        Require(before == await Snapshot(), "Rejected ownership mismatch changed retained business evidence.");
        Console.WriteLine("PASS receiving exact ownership: " + constraint + " rejects a cross-binding change and preserves the actual row.");
    }
}
