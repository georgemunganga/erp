using System.Globalization;
using System.Security.Cryptography;
using Mightyfin.Erp.Core.Documents.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task<object> VerifyP31PersistedScan(string owner, PrincipalApprovalFixture fixture,
        Guid invoice, Guid version, byte[] bytes)
    {
        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT d."Id",d."CreatedBy",d."CreatedAt",v."CreatedBy",v."SizeBytes",v."Sha256",
              v."ScanState",v."ScanAttempts",v."ScanStartedAt",v."ScanLeaseId",v."ScanLeaseExpiresAt",
              v."ScanFailureCode",s."Id",s."Attempt",s."Verdict",s."Sha256",s."Engine",s."Definitions",
              s."StartedAt",s."FinishedAt",s."FailureCode",
              (SELECT count(*) FROM core_document.scan_evidence all_scans
                WHERE all_scans."TenantId"=v."TenantId" AND all_scans."LegalEntityId"=v."LegalEntityId"
                  AND all_scans."VersionId"=v."Id"),
              (SELECT count(*) FROM core_document.record_events e WHERE e."TenantId"=d."TenantId"
                AND e."LegalEntityId"=d."LegalEntityId" AND e."DocumentId"=d."Id"
                AND e."VersionId"=v."Id" AND e."Action"='ScanFinished'
                AND e."ActorSubjectId"=@maker AND e."FailureCode" IS NULL),
              v."CreatedAt",v."Number"
            FROM core_document.documents d
            JOIN core_document.versions v ON v."DocumentId"=d."Id"
              AND v."TenantId"=d."TenantId" AND v."LegalEntityId"=d."LegalEntityId"
            JOIN core_document.scan_evidence s ON s."VersionId"=v."Id"
              AND s."TenantId"=v."TenantId" AND s."LegalEntityId"=v."LegalEntityId"
              AND s."Attempt"=v."ScanAttempts"
            WHERE d."TenantId"=@tenant AND d."LegalEntityId"=@company
              AND d."OwnerModule"='procurement' AND d."OwnerType"='supplier_invoice'
              AND d."OwnerId"=@invoice AND NOT d."IsRetired" AND v."Id"=@version
            """,connection);
        command.Parameters.AddWithValue("tenant",PrincipalApprovalFixture.Tenant);
        command.Parameters.AddWithValue("company",fixture.Company);
        command.Parameters.AddWithValue("invoice",invoice);
        command.Parameters.AddWithValue("version",version);
        command.Parameters.AddWithValue("maker",fixture.InvoiceMaker.ToString("D"));
        await using var row = await command.ExecuteReaderAsync();
        Need(await row.ReadAsync(),"Actual Core scan evidence is missing for the uploaded owner/version.");
        var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var started=row.GetFieldValue<DateTimeOffset>(18);
        var finished=row.GetFieldValue<DateTimeOffset>(19);
        var engine=row.GetString(16);
        var definitions=row.IsDBNull(17)?string.Empty:row.GetString(17);
        var parts=definitions.Split('/');
        Need(row.GetString(1)==fixture.InvoiceMaker.ToString("D")
            && row.GetString(3)==fixture.InvoiceMaker.ToString("D")
            && row.GetInt64(4)==bytes.LongLength && row.GetString(5)==hash
            && row.GetInt32(6)==(int)DocumentScanState.Available && row.GetInt32(7)==1
            && !row.IsDBNull(8) && row.GetFieldValue<DateTimeOffset>(8)==started
            && row.IsDBNull(9) && row.IsDBNull(10) && row.IsDBNull(11)
            && row.GetInt32(13)==1 && row.GetInt32(14)==(int)ScannerVerdict.Clean
            && row.GetString(15)==hash && row.IsDBNull(20)
            && row.GetInt64(21)==1 && row.GetInt64(22)==1
            && row.GetFieldValue<DateTimeOffset>(2)<=row.GetFieldValue<DateTimeOffset>(23)
            && row.GetFieldValue<DateTimeOffset>(23)<=started && started<=finished && row.GetInt32(24)==1
            && started.Offset==TimeSpan.Zero && finished.Offset==TimeSpan.Zero
            && engine.StartsWith("ClamAV ",StringComparison.Ordinal) && engine.Length<=120,
            "Persisted Core scan content, lease, verdict, actor event or chronology differs from the actual upload.");
        Need(parts.Length==2 && uint.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out var serial)
            && serial>0 && DateTimeOffset.TryParseExact(parts[1],"O",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var definitionAt) && definitionAt.Offset==TimeSpan.Zero
            // VERSION is sampled inside this real scan interval. Do not impose
            // present-day freshness on a historically valid retained scan.
            && definitionAt>=started-TimeSpan.FromHours(48)
            && definitionAt<=finished+TimeSpan.FromMinutes(15),
            "Persisted real scanner definitions or their UTC validity interval are unavailable.");
        var evidenceId=row.GetGuid(12);
        Need(!await row.ReadAsync(),"More than one active Core scan evidence row matched the uploaded version.");
        return new { versionId=version,evidenceId,sha256=hash,sizeBytes=bytes.LongLength,
            verdict="Clean",engine,definitions,startedAt=started,finishedAt=finished };
    }

    private sealed record P31DownloadEvent(Guid DocumentId,Guid VersionId,Guid OwnerId,
        string OwnerModule,string OwnerType,string Action,string Actor,DateTimeOffset At,string? Failure);

    private static async Task<Dictionary<Guid,P31DownloadEvent>> P31DocumentEvents(string owner,
        PrincipalApprovalFixture fixture)
    {
        await using var connection=new NpgsqlConnection(owner);await connection.OpenAsync();
        await using var command=new NpgsqlCommand("""
            SELECT e."Id",e."DocumentId",e."VersionId",d."OwnerId",d."OwnerModule",d."OwnerType",
              e."Action",e."ActorSubjectId",e."At",e."FailureCode"
            FROM core_document.record_events e JOIN core_document.documents d
              ON d."Id"=e."DocumentId" AND d."TenantId"=e."TenantId"
                AND d."LegalEntityId"=e."LegalEntityId"
            WHERE e."TenantId"=@tenant AND e."LegalEntityId"=@company
            """,connection);
        command.Parameters.AddWithValue("tenant",PrincipalApprovalFixture.Tenant);
        command.Parameters.AddWithValue("company",fixture.Company);
        var rows=new Dictionary<Guid,P31DownloadEvent>();
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())
            rows.Add(reader.GetGuid(0),new(reader.GetGuid(1),reader.GetGuid(2),reader.GetGuid(3),
                reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8),reader.IsDBNull(9)?null:reader.GetString(9)));
        return rows;
    }

    private static async Task VerifyP31DownloadEvent(string owner,PrincipalApprovalFixture fixture,
        Guid invoice,Guid version,Dictionary<Guid,P31DownloadEvent> before,DateTimeOffset began)
    {
        var after=await P31DocumentEvents(owner,fixture);
        Need(before.All(x=>after.TryGetValue(x.Key,out var current)&&current==x.Value),
            "A reviewer download changed prior Core document event history.");
        var added=after.Where(x=>!before.ContainsKey(x.Key)).Select(x=>x.Value).ToArray();
        Need(added.Length==1 && added[0].VersionId==version && added[0].OwnerId==invoice
            && added[0].OwnerModule=="procurement" && added[0].OwnerType=="supplier_invoice"
            && added[0].Action=="DownloadAuthorized" && added[0].Failure is null
            && added[0].Actor==fixture.InvoiceReviewer.ToString("D")
            && added[0].At>=DateTimeOffset.FromUnixTimeMilliseconds(began.ToUnixTimeMilliseconds())
            && added[0].At<=DateTimeOffset.UtcNow,
            "Exact pinned reviewer download did not append exactly its own Core DownloadAuthorized event.");
    }
}
