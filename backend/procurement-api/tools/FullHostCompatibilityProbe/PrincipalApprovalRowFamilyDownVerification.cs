using System.Security.Cryptography;
using System.Text;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record RowFamilyCounts(long RequestProvenance, long Checker2, long Authority2,
        long Submission3, long Assignment2);

    /// <summary>One invocation operates on one freshly bootstrapped private P25
    /// database. The caller must use five distinct new clones for five checkpoints.
    /// Later families retain their genuine upstream dependencies: this does not
    /// claim that the OR arms in the owning Down were independently isolated.</summary>
    private static async Task<object> VerifyRowFamilyDownAsync(NpgsqlConnection owner, string family,
        Guid company, Guid requestId, Guid? grantId = null, Guid? checkerId = null, Guid? caseId = null)
    {
        await RequirePrivateRawOwnerAsync(owner, PrincipalApprovalFixture.Tenant, company);
        Need(new[] { "RequestProvenance", "Authority2", "Checker2", "Submission3", "Assignment2" }.Contains(family),
            "Only an explicit P25 row-family checkpoint is supported.");
        Need(requestId != Guid.Empty, "A genuine route-created request is required.");
        var counts = await CountRowFamiliesAsync(owner);
        Need(counts.RequestProvenance >= 1, "Genuine request provenance must be retained.");
        await NeedOneAsync(owner, "SELECT count(*) FROM procurement.purchase_requests WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company AND requester_identity_schema_version=1", requestId, company);
        if (family is "Authority2" or "Checker2" or "Submission3" or "Assignment2")
        {
            Need(grantId is { } grant && grant != Guid.Empty && counts.Authority2 >= 3,
                "Actual independently activated authority2 evidence is required.");
            await NeedOneAsync(owner, "SELECT count(*) FROM procurement.spending_authority_grants WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company AND state='Active'", grantId!.Value, company);
        }
        if (family is "Checker2" or "Submission3" or "Assignment2")
        {
            Need(checkerId is { } checker && checker != Guid.Empty && counts.Checker2 >= 1,
                "Actual route-recorded checker2 evidence is required.");
            await NeedOneAsync(owner, "SELECT count(*) FROM procurement.budget_check_snapshots WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company AND purchase_request_id=@request AND checker_identity_schema_version=2", checkerId!.Value, company, requestId);
        }
        if (family is "Submission3" or "Assignment2")
        {
            Need(caseId is { } selectedCase && selectedCase != Guid.Empty && counts.Submission3 >= 1 && counts.Assignment2 >= 1,
                "Actual atomic Open must retain submission3 and assignment2 together.");
            await NeedOneAsync(owner, "SELECT count(*) FROM procurement.purchase_request_approval_submissions WHERE purchase_request_id=@request AND tenant_id=@tenant AND legal_entity_id=@company AND core_approval_case_id=@id AND snapshot_schema_version=3", caseId!.Value, company, requestId);
            await NeedOneAsync(owner, "SELECT count(*) FROM procurement.assignment_authority_evidence a JOIN procurement.purchase_request_approval_submissions s ON s.id=a.submission_id AND s.tenant_id=a.tenant_id AND s.legal_entity_id=a.legal_entity_id WHERE a.core_approval_case_id=@id AND s.purchase_request_id=@request AND a.tenant_id=@tenant AND a.legal_entity_id=@company AND a.identity_schema_version=2", caseId!.Value, company, requestId);
        }
        Need(family switch
        {
            "RequestProvenance" => counts.Checker2 == 0 && counts.Authority2 == 0 && counts.Submission3 == 0 && counts.Assignment2 == 0,
            "Authority2" => counts.Checker2 == 0 && counts.Submission3 == 0 && counts.Assignment2 == 0,
            "Checker2" => counts.Submission3 == 0 && counts.Assignment2 == 0,
            _ => true
        }, "Later P25 families appeared before this genuine lifecycle checkpoint.");

        var path = Environment.GetEnvironmentVariable("ERP_PRINCIPAL_P25_DOWN_SQL");
        Need(path is not null && Path.IsPathFullyQualified(path) && File.Exists(path),
            "Pinned unmodified generated P25 Down is required.");
        var bytes = await File.ReadAllBytesAsync(path!);
        Need(Convert.ToHexStringLower(SHA256.HashData(bytes)) == DownSha, "Original generated P25 Down changed.");
        var down = Encoding.UTF8.GetString(bytes).TrimStart('\ufeff');
        Need(down.StartsWith("START TRANSACTION;\nLOCK TABLE ", StringComparison.Ordinal)
            && down.Split("\nCOMMIT;", StringSplitOptions.None).Length == 2
            && down.TrimEnd().EndsWith("COMMIT;", StringComparison.Ordinal),
            "Exact owning locked Down transaction required.");
        var before = await PrincipalFootprintAsync(owner);
        Exception? outcome = null;
        try
        {
            // Execute the EXACT generated Down, including its original COMMIT.
            // A broken veto may destructively downgrade only this disposable clone;
            // it will fail the required status and complete-footprint comparison.
            await using var command = new NpgsqlCommand(down, owner);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception error) { outcome = error; }
        finally
        {
            await using var rollback = new NpgsqlCommand("ROLLBACK;", owner);
            await rollback.ExecuteNonQueryAsync();
        }
        Need(before == await PrincipalFootprintAsync(owner), "Unmodified P25 Down changed the complete private clone footprint.");
        Need(outcome is PostgresException pg && pg.SqlState == "55000" && pg.MessageText == DownMessage,
            "Unmodified P25 Down did not return the owning retained-evidence veto.");
        var targetDatabase = new NpgsqlConnectionStringBuilder(owner.ConnectionString).Database;
        Need(!string.IsNullOrWhiteSpace(targetDatabase), "Private target identity unavailable.");
        return new { family, requestProvenanceRows = counts.RequestProvenance, checker2Rows = counts.Checker2,
            authority2Rows = counts.Authority2, submission3Rows = counts.Submission3,
            assignment2Rows = counts.Assignment2, actualSqlState = "55000", completeFootprintUnchanged = true,
            unmodifiedGeneratedDown = true, independentlyIsolatedOrPredicate = false,
            genuineUpstreamDependenciesRetained = family != "RequestProvenance",
            privateTargetFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(targetDatabase!))) };
    }

    private static async Task<RowFamilyCounts> CountRowFamiliesAsync(NpgsqlConnection owner)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
             (SELECT count(*) FROM procurement.purchase_requests WHERE requester_identity_schema_version<>0 OR requester_core_worker_id IS NOT NULL OR requester_identity_sha256 IS NOT NULL),
             (SELECT count(*) FROM procurement.budget_check_snapshots WHERE checker_identity_schema_version NOT IN (0,1)),
             (SELECT count(*) FROM procurement.spending_authority_identity_evidence WHERE schema_version<>1 OR actor_worker_id IS NULL OR recipient_worker_id IS NULL),
             (SELECT count(*) FROM procurement.purchase_request_approval_submissions WHERE snapshot_schema_version NOT IN (1,2) OR finance_identity_schema_version NOT IN (0,1) OR requester_principal_id IS NOT NULL OR requester_identity_sha256 IS NOT NULL OR principal_identity_sha256 IS NOT NULL),
             (SELECT count(*) FROM procurement.assignment_authority_evidence WHERE identity_schema_version<>1)
            """, owner);
        await using var rows = await command.ExecuteReaderAsync();
        Need(await rows.ReadAsync(), "P25 family census unavailable.");
        return new(rows.GetInt64(0), rows.GetInt64(1), rows.GetInt64(2), rows.GetInt64(3), rows.GetInt64(4));
    }

    private static async Task NeedOneAsync(NpgsqlConnection owner, string sql, Guid id, Guid company,
        Guid? requestId = null)
    {
        await using var command = new NpgsqlCommand(sql, owner);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", PrincipalApprovalFixture.Tenant);
        command.Parameters.AddWithValue("company", company);
        if (requestId is not null) command.Parameters.AddWithValue("request", requestId.Value);
        Need(await command.ExecuteScalarAsync() is long count && count == 1,
            "The selected checkpoint must retain exactly one genuine scoped source row.");
    }
}
