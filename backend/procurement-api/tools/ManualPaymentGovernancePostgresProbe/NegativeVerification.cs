using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;

sealed partial class GovernanceProbe
{
    private async Task<string> CountsAsync()
    {
        await using var c = new NpgsqlConnection(connection); await c.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT jsonb_build_array(
              (SELECT count(*) FROM procurement.manual_payment_requests),
              (SELECT count(*) FROM procurement.manual_payment_decisions),
              (SELECT count(*) FROM procurement.manual_payment_receipts),
              (SELECT count(*) FROM procurement.manual_payment_workflow_cases),
              (SELECT count(*) FROM procurement.manual_payment_core_decisions),
              (SELECT count(*) FROM procurement.manual_payment_date_observations),
              (SELECT count(*) FROM procurement.audit_events),
              (SELECT count(*) FROM core_approval.cases),
              (SELECT count(*) FROM core_approval.decisions))::text
            """, c);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task DenySqlAsync(string sql, string expectedMessage, params (string Name, object Value)[] values)
    {
        var before = await CountsAsync(); var denied = false;
        await using (var c = new NpgsqlConnection(connection))
        {
            await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(sql, c, tx);
                foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
                await command.ExecuteNonQueryAsync(); await tx.CommitAsync();
            }
            catch (PostgresException error) when (error.SqlState == "23514" && error.MessageText == expectedMessage)
            { denied = true; }
        }
        Check(denied); Check(await CountsAsync() == before);
    }

    private async Task VerifySqlPolicyDigestAsync()
    {
        Phase = "typed-sql-policy-digest-and-invalid-limits";
        await using (var c = new NpgsqlConnection(connection))
        {
            await c.OpenAsync();
            foreach (var rules in new[] { Rules,
                Rules.Replace("100.0000", "NaN"), Rules.Replace("100.0000", "0"),
                Rules.Replace("100.0000", "100.00001"), Rules.Replace("100.0000", "1e2"),
                Rules.Replace("100.0000", "100\n"), Rules.Replace("\"100.0000\"", "100"),
                Rules.Replace("\"maximumByCurrency\"", "\"unsupportedLimits\"") })
            {
                await using var command = new NpgsqlCommand("SELECT procurement.manual_payment_policy_digest(@rules::jsonb)", c);
                command.Parameters.AddWithValue("rules", rules.Replace("100\n", "100\\n"));
                var digest = await command.ExecuteScalarAsync();
                Check(rules == Rules ? digest is string exact && exact == ProcurementEvidenceEncoding.PolicyJsonDigest(Rules)
                    : digest is null or DBNull);
            }
        }
    }

    private async Task NegativeChecksAsync(Guid confirmedRequest)
    {
        Phase = "raw-sql-governance-companion-rollback";
        var draft = await CreateAsync(1m);
        await DenySqlAsync("""
            UPDATE procurement.manual_payment_requests SET status='PendingApproval',version=version+1,
              updated_at=clock_timestamp(),updated_by=creator_principal_id WHERE id=@request;
            INSERT INTO procurement.manual_payment_decisions
            SELECT (jsonb_populate_record(NULL::procurement.manual_payment_decisions,
              to_jsonb(d)||jsonb_build_object('id',@decision,'manual_payment_request_id',@request,
                'from_version',1,'to_version',2))).*
            FROM procurement.manual_payment_decisions d WHERE d.action='Submit' LIMIT 1;
            INSERT INTO procurement.manual_payment_receipts
            SELECT (jsonb_populate_record(NULL::procurement.manual_payment_receipts,
              to_jsonb(r)||jsonb_build_object('id',@receipt,'manual_payment_request_id',@request,
                'command_key',@command,'result_version',2,
                'response_json',jsonb_build_object('id',@request,'version',2,'status','PendingApproval')::text))).*
            FROM procurement.manual_payment_receipts r WHERE r.action='Submit' LIMIT 1;
            """, "Manual payment transition requires its Core case link", ("request", draft),
            ("decision", Guid.NewGuid()), ("receipt", Guid.NewGuid()), ("command", Guid.NewGuid()));
        await using (var db = Procurement(creator))
            Check((await db.ManualPaymentRequests.SingleAsync(x => x.Id == draft)).Status == "Draft");

        Phase = "submitted-title-and-append-only-evidence";
        await SubmitAsync(draft);
        await DenySqlAsync("""
            UPDATE procurement.manual_payment_requests SET title='Rewritten',version=version+1,
              updated_at=clock_timestamp(),updated_by=creator_principal_id WHERE id=@request
            """, "Submitted manual payment title is immutable", ("request", draft));
        foreach (var table in new[] { "manual_payment_workflow_cases", "manual_payment_core_decisions", "manual_payment_date_observations" })
        {
            await DenySqlAsync("UPDATE procurement." + table + " SET created_by=created_by WHERE manual_payment_request_id=@request",
                "Manual payment history and receipts are append-only", ("request", confirmedRequest));
            await DenySqlAsync("DELETE FROM procurement." + table + " WHERE manual_payment_request_id=@request",
                "Manual payment history and receipts are append-only", ("request", confirmedRequest));
        }
        Phase = "retained-p28-downgrade-veto";
        var counts = await CountsAsync(); var blocked = false;
        await using (var db = Procurement(creator))
        {
            try { await db.GetService<IMigrator>().MigrateAsync("20261023125243_P27ManualPaymentRecords"); }
            catch (PostgresException error) when (error.SqlState == "23514" && error.MessageText == "Payment governance evidence is retained; downgrade is unsafe")
            { blocked = true; }
            Check((await db.Database.GetAppliedMigrationsAsync()).Last().EndsWith("P28ManualPaymentGovernance", StringComparison.Ordinal));
        }
        Check(blocked); Check(counts == await CountsAsync());
    }
}
