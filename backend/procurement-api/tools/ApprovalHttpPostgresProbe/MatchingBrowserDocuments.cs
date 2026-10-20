using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private string? matchingDocumentDirectory;
    private const string MatchingDocumentName = "matching-original.pdf";
    // Deliberately tiny generated PDF-signature fixture. It carries no customer data.
    private const string MatchingDocumentText = "%PDF-1.7\n% Synthetic P26 original invoice: one goods line, quantity1 at100 ZMW.\n%%EOF\n";
    private static byte[] MatchingDocumentBytes => Encoding.UTF8.GetBytes(MatchingDocumentText);
    private static string MatchingDocumentSha => Convert.ToHexStringLower(SHA256.HashData(MatchingDocumentBytes));

    private static string ConfigureMatchingBrowserDocuments(WebApplicationBuilder builder,string connection)
    {
        var requested=Environment.GetEnvironmentVariable("PROCUREMENT_MATCHING_BROWSER_DOCUMENT_DIRECTORY");
        var directory=requested??Path.Combine(Path.GetTempPath(),"erp-matching-browser-documents-"+Guid.NewGuid().ToString("N"));
        if(!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("A private absolute synthetic document directory is required.");
        directory=Path.GetFullPath(directory);
        if(Directory.Exists(directory))
        {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0 || Directory.EnumerateFileSystemEntries(directory).Any()
                || !OperatingSystem.IsWindows() && (File.GetUnixFileMode(directory)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))!=0)
                throw new InvalidOperationException("Matching browser documents require a fresh empty owner-private directory, never production storage.");
        }
        if(OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["CoreDocuments:Enabled"]="true",["ConnectionStrings:CoreDocuments"]=connection,
            ["CoreDocuments:PrivateDirectory"]=directory,
            // Registration requires an endpoint; the tools-only exact-byte scanner replaces Clamd below.
            ["CoreDocuments:ScannerSocket"]=Path.Combine(directory,"unused-test-only-scanner.sock"),
            ["CoreDocuments:MaxFileBytes"]="4096",["CoreDocuments:MaxOwnerFiles"]="1",
        });
        return directory;
    }
    private static void RegisterMatchingBrowserScanner(IServiceCollection services)
    {
        services.RemoveAll<IDocumentScanner>();
        services.AddSingleton<IDocumentScanner,KnownFixtureOnlyDocumentScanner>();
    }
    private static async Task VerifyMatchingScannerBoundary()
    {
        var scanner=new KnownFixtureOnlyDocumentScanner();
        async Task<DocumentScanResult> Read(byte[] bytes,string? declaredSha=null,long? declaredSize=null)
        {
            await using var input=new MemoryStream(bytes,false);
            return await scanner.ScanAsync(input,declaredSize??bytes.Length,
                declaredSha??Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        Require((await Read(MatchingDocumentBytes)).Verdict==ScannerVerdict.Clean,"Known benign PDF scanner fixture failed.");
        var altered=MatchingDocumentBytes; altered[^2]^=1;
        Require((await Read(altered)).Verdict!=ScannerVerdict.Clean,"Altered fixture bytes were trusted.");
        Require((await Read([..MatchingDocumentBytes,(byte)'x'])).Verdict!=ScannerVerdict.Clean,"Appended bytes were trusted.");
        Require((await Read(MatchingDocumentBytes,new string('0',64))).Verdict!=ScannerVerdict.Clean,"Wrong declared hash was trusted.");
        Require((await Read(MatchingDocumentBytes,declaredSize:1)).Verdict!=ScannerVerdict.Clean,"Wrong declared size was trusted.");
    }
    /// <summary>TOOLS ONLY. Exact fixture-byte acceptance is not antivirus, original-unit review,
    /// a document approval, or a production scanner. Every different input is rejected.</summary>
    private sealed class KnownFixtureOnlyDocumentScanner : IDocumentScanner
    {
        public async Task<DocumentScanResult> ScanAsync(Stream content,long expectedBytes,string expectedSha256,CancellationToken ct=default)
        {
            var expected=MatchingDocumentBytes;
            var bytes=new byte[4097]; var length=0;
            while(length<bytes.Length)
            {
                var read=await content.ReadAsync(bytes.AsMemory(length),ct);
                if(read==0) break; length+=read;
            }
            var observed=Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(0,length)));
            var allowed=expectedBytes==expected.Length && expectedSha256==MatchingDocumentSha && length==expected.Length
                && bytes.AsSpan(0,length).SequenceEqual(expected);
            return new(allowed?ScannerVerdict.Clean:ScannerVerdict.Infected,observed,
                "TEST-ONLY KnownFixtureOnlyDocumentScanner","synthetic-exact-benign-bytes-only",allowed?null:"test-fixture-content-rejected");
        }
    }
}

internal static partial class RuntimePrivileges
{
    private static async Task GrantMatchingDocumentsAsync(NpgsqlConnection owner,string quotedRole,IServiceProvider fixture)
    {
        using var services=fixture.CreateScope(); var documents=services.ServiceProvider.GetRequiredService<DocumentDbContext>();
        await Sql(owner,"REVOKE CREATE ON SCHEMA core_document FROM PUBLIC");
        await Sql(owner,$"REVOKE ALL ON SCHEMA core_document FROM {quotedRole}");
        await Sql(owner,$"REVOKE ALL ON ALL TABLES IN SCHEMA core_document FROM {quotedRole}");
        await Sql(owner,"REVOKE ALL ON ALL TABLES IN SCHEMA core_document FROM PUBLIC");
        await Sql(owner,$"GRANT USAGE ON SCHEMA core_document TO {quotedRole}");
        await Sql(owner,$"GRANT SELECT ON core_document.\"__EFMigrationsHistory\" TO {quotedRole}");
        foreach(var type in new[] { typeof(CoreDocument),typeof(CoreDocumentVersion),typeof(DocumentScanEvidence),typeof(DocumentRecordEvent) })
        {
            var entity=documents.Model.FindEntityType(type)??throw new InvalidOperationException("Core document fixture model is missing.");
            await Sql(owner,$"GRANT SELECT,INSERT ON {Table(entity)} TO {quotedRole}");
        }
        var version=documents.Model.FindEntityType(typeof(CoreDocumentVersion))!;
        var store=StoreObjectIdentifier.Table(version.GetTableName()!,version.GetSchema());
        var names=new[] { "ScanFailureCode","ScanState","ScanAttempts","ScanLeaseId","ScanStartedAt","ScanLeaseExpiresAt","Version" };
        var columns=names.Select(name=>Quote(version.FindProperty(name)?.GetColumnName(store)??throw new InvalidOperationException("Core document scan field is unmapped.")));
        await Sql(owner,$"GRANT UPDATE ({string.Join(',',columns)}) ON {Table(version)} TO {quotedRole}");
        var procurement=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var attachment=procurement.Model.FindEntityType(typeof(Mightyfin.Erp.Procurement.Domain.ProcurementAttachmentRef))!;
        await Sql(owner,$"GRANT SELECT,INSERT ON {Table(attachment)} TO {quotedRole}");
    }
    private static async Task AssertMatchingDocumentWritesDeniedAsync(string runtime,IServiceProvider fixture)
    {
        using var services=fixture.CreateScope(); var documents=services.ServiceProvider.GetRequiredService<DocumentDbContext>();
        var statements=new List<string> { "CREATE TABLE core_document.probe_forbidden(id integer)" };
        foreach(var type in new[] { typeof(CoreDocument),typeof(CoreDocumentVersion),typeof(DocumentScanEvidence),typeof(DocumentRecordEvent) })
        {
            var entity=documents.Model.FindEntityType(type)!;
            var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
            var key=Quote(entity.FindProperty("Id")!.GetColumnName(store)!);
            statements.Add($"DELETE FROM {Table(entity)} WHERE FALSE");
            statements.Add($"UPDATE {Table(entity)} SET {key}={key} WHERE FALSE");
        }
        await using var connection=new NpgsqlConnection(runtime); await connection.OpenAsync();
        foreach(var sql in statements)
        {
            await using var transaction=await connection.BeginTransactionAsync();
            try { await using var command=new NpgsqlCommand(sql,connection,transaction); await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Runtime unexpectedly changed protected Core document evidence."); }
            catch(PostgresException error) when(error.SqlState==PostgresErrorCodes.InsufficientPrivilege) { }
            finally { await transaction.RollbackAsync(); }
        }
    }
}
