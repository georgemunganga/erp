using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

// IDs come from the real mapped workflow. Missing evidence is a release hold,
// never an empty-table UPDATE or a fabricated replacement identity.
internal sealed record PrincipalApprovalStorageGraph(Guid RequestId, Guid CheckerSnapshotId,
    Guid AuthorityCreatedEvidenceId, Guid SubmissionId, Guid AssignmentEvidenceId,
    string TenantId, Guid LegalEntityId, long SubmittedBusinessVersion, long CurrentRequestVersion);

internal static partial class PrincipalApprovalHttpVerification
{
    internal static object? PrincipalRawStorageVerification { get; private set; }
    internal static object? PrincipalRawStorageFailure { get; private set; }
    private const string OriginalCheck = "ck_pr_original_principal_identity";
    private const string DownSha = "1b298d0a0fb8ccd42ab1f0b93260ef02d89896c1a21b3209728286e75492ba1c";
    private const string DownMessage = "Retained Core-principal request approval evidence prevents destructive downgrade.";
    private const string Zero = "'00000000-0000-0000-0000-000000000000'::uuid";
    private sealed record RawFault(string Name, string Table, Guid Id, string Assignment,
        string SqlState, string? Constraint = null, string? Column = null, string? OtherPrincipal = null);

    // Runnable with the first create/submit slice: no checker/assignment IDs needed.
    internal static async Task VerifyRequestRawStorageAsync(NpgsqlConnection owner, Guid requestId,
        string tenantId, Guid companyId, long expectedVersion)
    {
        PrincipalRawStorageVerification = null;
        await RequirePrivateRawOwnerAsync(owner, tenantId, companyId);
        await RequireOriginalRequestAsync(owner, requestId, tenantId, companyId, expectedVersion);
        var faults = RequestFaults(requestId);
        await VerifyFaultsAsync(owner, tenantId, companyId, faults);
        PrincipalRawStorageVerification = new
        {
            status = "PASS_OWNER_ORIGINAL_REQUEST_STORAGE_ONLY", cases = faults.Length,
            actualNamedCheckDenials = faults.Length, nonVacuousPositiveBaselineEveryCase = true,
            fullFootprintUnchangedEveryCase = true, fiveFamilyGraphVerified = false,
            finiteRoleVerified = false, workflowOrFinanceVerifiedByRawChecks = false,
        };
    }

    // Call only after the complete real current graph exists. Do not pass empty IDs
    // during the earlier Finance-unavailable/create-submit acceptance slice.
    internal static async Task VerifyRawStorageAsync(NpgsqlConnection owner, PrincipalApprovalStorageGraph graph)
    {
        PrincipalRawStorageVerification = null;
        await RequirePrivateRawOwnerAsync(owner, graph.TenantId, graph.LegalEntityId);
        RawNeed(new[] { graph.RequestId, graph.CheckerSnapshotId, graph.AuthorityCreatedEvidenceId,
            graph.SubmissionId, graph.AssignmentEvidenceId }.All(x => x != Guid.Empty)
            && graph.SubmittedBusinessVersion > 0 && graph.CurrentRequestVersion > 0,
            "The complete actual graph and both retained business/current CAS versions are required.");
        await RequireOriginalRequestAsync(owner, graph.RequestId, graph.TenantId, graph.LegalEntityId, graph.CurrentRequestVersion);
        var checker = await ReadRawRowAsync(owner, "budget_check_snapshots", graph.CheckerSnapshotId, graph.TenantId, graph.LegalEntityId);
        var authority = await ReadRawRowAsync(owner, "spending_authority_identity_evidence", graph.AuthorityCreatedEvidenceId, graph.TenantId, graph.LegalEntityId);
        var pin = await ReadRawRowAsync(owner, "purchase_request_approval_submissions", graph.SubmissionId, graph.TenantId, graph.LegalEntityId);
        var assignment = await ReadRawRowAsync(owner, "assignment_authority_evidence", graph.AssignmentEvidenceId, graph.TenantId, graph.LegalEntityId);
        RawNeed(checker.GetProperty("checker_identity_schema_version").GetInt32() == 2
            && checker.GetProperty("checked_by_worker_id").ValueKind == JsonValueKind.Null
            && checker.GetProperty("purchase_request_id").GetGuid() == graph.RequestId
            && checker.GetProperty("request_version").GetInt64() == graph.SubmittedBusinessVersion,
            "Actual workerless principal checker tuple required.");
        var checkerPrincipal = checker.GetProperty("checked_by_subject_id").GetString();
        RawNeed(Guid.TryParseExact(checkerPrincipal, "D", out var checkerId) && checkerId != Guid.Empty,
            "Actual captured checker principal required; no invented alternate identity.");
        RawNeed(authority.GetProperty("schema_version").GetInt32() == 2
            && authority.GetProperty("stage").GetString() == "Created"
            && authority.GetProperty("actor_worker_id").ValueKind == JsonValueKind.Null
            && authority.GetProperty("recipient_worker_id").ValueKind == JsonValueKind.Null,
            "Actual workerless Created authority evidence required.");
        RawNeed(pin.GetProperty("snapshot_schema_version").GetInt32() == 3
            && pin.GetProperty("finance_identity_schema_version").GetInt32() == 2
            && pin.GetProperty("purchase_request_id").GetGuid() == graph.RequestId
            && pin.GetProperty("finance_snapshot_id").GetGuid() == graph.CheckerSnapshotId
            && pin.GetProperty("submitted_business_version").GetInt64() == graph.SubmittedBusinessVersion
            && pin.GetProperty("maker_worker_id").ValueKind == JsonValueKind.Null
            && pin.GetProperty("requester_worker_id").ValueKind == JsonValueKind.Null
            && pin.GetProperty("worker_identity_sha256").ValueKind == JsonValueKind.Null,
            "Actual current principal submission and exact retained FK/version tuple required.");
        RawNeed(assignment.GetProperty("identity_schema_version").GetInt32() == 2
            && assignment.GetProperty("assignee_worker_id").ValueKind == JsonValueKind.Null
            && assignment.GetProperty("submission_id").GetGuid() == graph.SubmissionId
            && assignment.GetProperty("core_approval_case_id").GetGuid() == pin.GetProperty("core_approval_case_id").GetGuid()
            && assignment.GetProperty("grant_id").GetGuid() == authority.GetProperty("grant_id").GetGuid(),
            "Actual current assignment must refer to this retained submission/case/grant.");
        RawNeed(pin.GetProperty("maker_subject_id").GetString() != checkerPrincipal,
            "The observed checker must be distinct from the real maker.");

        const string budget = "budget_check_snapshots", grant = "spending_authority_identity_evidence";
        const string submission = "purchase_request_approval_submissions", assignee = "assignment_authority_evidence";
        var faults = new List<RawFault>(RequestFaults(graph.RequestId))
        {
            new("original-creator-mismatch", "purchase_requests", graph.RequestId, "created_by=@otherPrincipal", "23514", OriginalCheck, OtherPrincipal: checkerPrincipal),
            new("checker-missing-hash", budget, graph.CheckerSnapshotId, "checker_identity_sha256=NULL", "23514", "ck_budget_checker_identity"),
            // The existing column is NOT NULL. Empty text targets the successor
            // canonical-principal CHECK instead of the earlier column constraint.
            new("checker-missing-principal", budget, graph.CheckerSnapshotId, "checked_by_subject_id=''", "23514", "ck_budget_checker_identity"),
            new("checker-zero-alias", budget, graph.CheckerSnapshotId, "checked_by_worker_id=" + Zero, "23514", "ck_budget_checker_identity"),
            new("checker-unsupported-version", budget, graph.CheckerSnapshotId, "checker_identity_schema_version=99", "23514", "ck_budget_checker_identity"),
            new("checker-false-worker-version", budget, graph.CheckerSnapshotId, "checker_identity_schema_version=1", "23514", "ck_budget_checker_identity"),
            new("authority-unsupported-version", grant, graph.AuthorityCreatedEvidenceId, "schema_version=99", "23514", "ck_authority_identity_evidence"),
            new("authority-false-worker-version", grant, graph.AuthorityCreatedEvidenceId, "schema_version=1", "23514", "ck_authority_identity_evidence"),
            new("authority-zero-actor-alias", grant, graph.AuthorityCreatedEvidenceId, "actor_worker_id=" + Zero, "23514", "ck_authority_identity_evidence"),
            new("authority-zero-recipient-alias", grant, graph.AuthorityCreatedEvidenceId, "recipient_worker_id=" + Zero, "23514", "ck_authority_identity_evidence"),
            new("authority-other-operational-family", grant, graph.AuthorityCreatedEvidenceId, "action='PurchaseOrder.Approve'", "23514", "ck_authority_identity_evidence"),
            new("authority-malformed-digest", grant, graph.AuthorityCreatedEvidenceId, "evidence_sha256=repeat('z',64)", "23514", "ck_authority_identity_evidence"),
            new("authority-required-principal-null", grant, graph.AuthorityCreatedEvidenceId, "actor_principal_id=NULL", "23502", Column: "actor_principal_id"),
            new("submission-missing-principal", submission, graph.SubmissionId, "requester_principal_id=NULL", "23514", "ck_pr_approval_principal_identity"),
            new("submission-missing-original-digest", submission, graph.SubmissionId, "requester_identity_sha256=NULL", "23514", "ck_pr_approval_principal_identity"),
            new("submission-missing-principal-digest", submission, graph.SubmissionId, "principal_identity_sha256=NULL", "23514", "ck_pr_approval_principal_identity"),
            new("submission-maker-mismatch", submission, graph.SubmissionId, "maker_subject_id=@otherPrincipal", "23514", "ck_pr_approval_principal_identity", OtherPrincipal: checkerPrincipal),
            new("submission-forbidden-worker-digest", submission, graph.SubmissionId, "worker_identity_sha256=principal_identity_sha256", "23514", "ck_pr_approval_worker_identity"),
            new("submission-zero-maker-alias", submission, graph.SubmissionId, "maker_worker_id=" + Zero, "23514", "ck_pr_approval_worker_identity"),
            new("submission-zero-requester-alias", submission, graph.SubmissionId, "requester_worker_id=" + Zero, "23514", "ck_pr_approval_worker_identity"),
            new("submission-zero-business-version", submission, graph.SubmissionId, "submitted_business_version=0", "23514", "ck_pr_approval_submission_identity"),
            new("submission-zero-opening-key", submission, graph.SubmissionId, "opening_command_key=" + Zero, "23514", "ck_pr_approval_submission_identity"),
            new("submission-unbound-finance-version", submission, graph.SubmissionId, "finance_identity_schema_version=0", "23514", "ck_pr_finance_identity_version"),
            new("submission-legacy-finance-version", submission, graph.SubmissionId, "finance_identity_schema_version=1", "23514", "ck_pr_finance_identity_version"),
            new("submission-unsupported-finance-version", submission, graph.SubmissionId, "finance_identity_schema_version=99", "23514", "ck_pr_finance_identity_version"),
            new("assignment-unsupported-version", assignee, graph.AssignmentEvidenceId, "identity_schema_version=99", "23514", "ck_pr_assignment_authority_identity"),
            new("assignment-malformed-principal", assignee, graph.AssignmentEvidenceId, "principal_id='not-a-canonical-principal'", "23514", "ck_pr_assignment_authority_identity"),
            new("assignment-zero-alias", assignee, graph.AssignmentEvidenceId, "assignee_worker_id=" + Zero, "23514", "ck_pr_assignment_authority_identity"),
            new("assignment-zero-stage", assignee, graph.AssignmentEvidenceId, "core_stage_id=" + Zero, "23514", "ck_pr_assignment_authority_identity"),
            new("assignment-zero-grant-revision", assignee, graph.AssignmentEvidenceId, "grant_revision=0", "23514", "ck_pr_assignment_authority_identity"),
            new("assignment-zero-authority-version", assignee, graph.AssignmentEvidenceId, "authority_version=0", "23514", "ck_pr_assignment_authority_identity"),
        };
        await VerifyFaultsAsync(owner, graph.TenantId, graph.LegalEntityId, faults);
        await VerifyCurrentGraphDownRefusalAsync(owner);
        PrincipalRawStorageVerification = new
        {
            status = "PASS_OWNER_CURRENT_GRAPH_STORAGE_ONLY", cases = faults.Count,
            actualNamedCheckDenials = faults.Count(x => x.SqlState == "23514"),
            actualNotNullColumnDenials = faults.Count(x => x.SqlState == "23502"),
            nonVacuousPositiveBaselineEveryCase = true, fullFootprintUnchangedEveryCase = true,
            currentGraphDowngradeRefused = true, currentGraphFamiliesAreDependent = true,
            independentlyIsolatedRowFamilyBranches = 0,
            legacyPositiveCompatibilityVerified = false, finiteRoleVerified = false,
            workflowOrFinanceVerifiedByRawChecks = false,
        };
    }

    private static RawFault[] RequestFaults(Guid id) =>
    [
        new("original-missing-hash", "purchase_requests", id, "requester_identity_sha256=NULL", "23514", OriginalCheck),
        new("original-zero-optional-alias", "purchase_requests", id, "requester_core_worker_id=" + Zero, "23514", OriginalCheck),
        new("original-unsupported-version", "purchase_requests", id, "requester_identity_schema_version=99", "23514", OriginalCheck),
        new("original-false-legacy-version", "purchase_requests", id, "requester_identity_schema_version=0", "23514", OriginalCheck),
    ];

    private static async Task VerifyFaultsAsync(NpgsqlConnection owner, string tenant, Guid company, IReadOnlyCollection<RawFault> faults)
    {
        var before = await PrincipalFootprintAsync(owner);
        foreach (var fault in faults)
        {
            RawNeed(Regex.IsMatch(fault.Table, "^[a-z_]+$") && fault.Id != Guid.Empty, "Fixed scoped row required.");
            await using (var tx = await owner.BeginTransactionAsync(IsolationLevel.RepeatableRead))
            {
                try
                {
                    // PostgreSQL must first accept this exact baseline row. Zero rows is failure.
                    await using var positive = RawCommand(owner, tx, fault.Table, "version=version", fault.Id, tenant, company);
                    RawNeed(await positive.ExecuteNonQueryAsync() == 1, "Raw positive baseline must affect exactly one row.");
                    try
                    {
                        await using var negative = RawCommand(owner, tx, fault.Table, fault.Assignment, fault.Id, tenant, company);
                        if (fault.OtherPrincipal is not null) negative.Parameters.AddWithValue("otherPrincipal", fault.OtherPrincipal);
                        await negative.ExecuteNonQueryAsync();
                        throw new InvalidOperationException("Expected actual raw denial: " + fault.Name);
                    }
                    catch (PostgresException error)
                    {
                        if(error.SqlState != fault.SqlState || fault.Constraint is not null && error.ConstraintName != fault.Constraint
                            || fault.Column is not null && error.ColumnName != fault.Column)
                            PrincipalRawStorageFailure=new {
                                fault=fault.Name,expectedSqlState=fault.SqlState,
                                actualSqlState=Regex.IsMatch(error.SqlState,"^[0-9A-Z]{5}$")?error.SqlState:"Unrecognized",
                                actualConstraint=SafeRawIdentifier(error.ConstraintName),actualColumn=SafeRawIdentifier(error.ColumnName)};
                        RawNeed(error.SqlState == fault.SqlState && (fault.Constraint is null || error.ConstraintName == fault.Constraint)
                            && (fault.Column is null || error.ColumnName == fault.Column), "Unexpected raw constraint/column denial: " + fault.Name);
                    }
                }
                finally { await tx.RollbackAsync(); }
            }
            RawNeed(before == await PrincipalFootprintAsync(owner), "Raw rollback changed owning facts: " + fault.Name);
        }
    }

    private static string? SafeRawIdentifier(string? value)=>value is null?null:
        Regex.IsMatch(value,"^[a-z0-9_]{1,120}$")?value:"Unrecognized";

    private static NpgsqlCommand RawCommand(NpgsqlConnection owner, NpgsqlTransaction tx, string table,
        string assignment, Guid id, string tenant, Guid company)
    {
        var command = new NpgsqlCommand($"UPDATE procurement.{table} SET {assignment} WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company", owner, tx);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("company", company);
        return command;
    }

    private static async Task<JsonElement> ReadRawRowAsync(NpgsqlConnection owner, string table, Guid id, string tenant, Guid company)
    {
        RawNeed(Regex.IsMatch(table, "^[a-z_]+$") && id != Guid.Empty, "Actual scoped row required.");
        await using var command = new NpgsqlCommand($"SELECT to_jsonb(t)::text FROM procurement.{table} t WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company", owner);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("company", company);
        var json = await command.ExecuteScalarAsync() as string;
        RawNeed(json is not null, "Actual scoped evidence row is missing; no synthetic replacement.");
        using var document = JsonDocument.Parse(json!); return document.RootElement.Clone();
    }

    private static async Task RequireOriginalRequestAsync(NpgsqlConnection owner, Guid id, string tenant, Guid company, long version)
    {
        var request = await ReadRawRowAsync(owner, "purchase_requests", id, tenant, company);
        RawNeed(version > 0 && request.GetProperty("version").GetInt64() == version
            && request.GetProperty("requester_identity_schema_version").GetInt32() == 1
            && request.GetProperty("requester_core_worker_id").ValueKind == JsonValueKind.Null
            && request.GetProperty("requester_principal_id").GetString() == request.GetProperty("created_by").GetString()
            && request.GetProperty("source_channel").GetString() == "ERP",
            "Actual workerless original identity/current CAS must exist before raw tests.");
    }

    private static async Task VerifyCurrentGraphDownRefusalAsync(NpgsqlConnection owner)
    {
        var path = Environment.GetEnvironmentVariable("ERP_PRINCIPAL_P25_DOWN_SQL");
        RawNeed(path is not null && Path.IsPathFullyQualified(path) && File.Exists(path), "Pinned generated P25 Down file required for retained-graph refusal.");
        var bytes = await File.ReadAllBytesAsync(path!);
        RawNeed(Convert.ToHexStringLower(SHA256.HashData(bytes)) == DownSha, "Original generated P25 Down changed.");
        var down = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\ufeff');
        RawNeed(down.StartsWith("START TRANSACTION;\nLOCK TABLE ", StringComparison.Ordinal)
            && down.Split("\nCOMMIT;", StringSplitOptions.None).Length == 2 && down.TrimEnd().EndsWith("COMMIT;", StringComparison.Ordinal),
            "Exact owning locked Down transaction required.");
        // Original Down statements stay in order; this extra abort means even a broken
        // veto cannot commit a downgrade. P0001 never satisfies the expected owning55000.
        var statement = down.Replace("\nCOMMIT;", "\nDO $unexpected_down_success$ BEGIN RAISE EXCEPTION USING ERRCODE='P0001', MESSAGE='ExpectedOwningP25GraphVetoDidNotFire'; END $unexpected_down_success$;\nCOMMIT;", StringComparison.Ordinal);
        var before = await PrincipalFootprintAsync(owner);
        try
        {
            try
            {
                await using var command = new NpgsqlCommand(statement, owner); await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Expected retained current graph downgrade refusal.");
            }
            catch (PostgresException error) { RawNeed(error.SqlState == "55000" && error.MessageText == DownMessage, "Wrong retained graph downgrade failure."); }
        }
        finally { await using var rollback = new NpgsqlCommand("ROLLBACK;", owner); await rollback.ExecuteNonQueryAsync(); }
        RawNeed(before == await PrincipalFootprintAsync(owner), "Retained graph Down probe changed owning facts.");
    }

    private static async Task RequirePrivateRawOwnerAsync(NpgsqlConnection owner, string tenant, Guid company)
    {
        var parsed = new NpgsqlConnectionStringBuilder(owner.ConnectionString);
        RawNeed(owner.State == ConnectionState.Open && parsed.Host is { } host && host.StartsWith('/') && !host.Contains(',')
            && parsed.Database is { } database && Regex.IsMatch(database, "^proc_p25_http_[a-z0-9_]+$")
            && parsed.Username == "postgres" && !string.IsNullOrWhiteSpace(tenant) && company != Guid.Empty,
            "Raw owner probes are private Unix-only test data, never finite/public/production authority.");
        await using var identity = new NpgsqlCommand("""
            SELECT current_user='postgres' AND session_user='postgres'
                AND current_database()=@database AND inet_server_addr() IS NULL
                AND (SELECT system_identifier::text FROM pg_control_system())='7698678713374847009'
            """, owner);
        identity.Parameters.AddWithValue("database", parsed.Database!);
        RawNeed(await identity.ExecuteScalarAsync() is true, "Exact private owning session/cluster required before raw mutations.");
    }
    private static void RawNeed(bool ok, string message) => Need(ok, message);
}
