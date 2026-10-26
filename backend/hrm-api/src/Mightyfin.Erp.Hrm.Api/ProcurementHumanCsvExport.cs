using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Bounded human spreadsheet export; never a round-trip import format.</summary>
internal static class ProcurementHumanCsvExport
{
    internal const int MaxRows = 10_000;
    internal static bool ExceedsLimit(int count) => count > MaxRows;

    internal static bool ValidFilters(HttpRequest request)
    {
        if (request.Query.Any(pair => pair.Key is not ("search" or "status") || pair.Value.Count != 1))
            return false;
        return ValidValue(request.Query["search"].ToString(), 500)
            && ValidValue(request.Query["status"].ToString(), 80);
    }

    private static bool ValidValue(string value, int maximum) => value.Length <= maximum
        && !value.Any(char.IsControl);

    internal static byte[] Encode(string[] headers, IEnumerable<string?[]> rows)
    {
        var output = new StringBuilder();
        AppendRow(output, headers);
        foreach (var row in rows)
        {
            if (row.Length != headers.Length) throw new InvalidOperationException("CSV projection width changed.");
            AppendRow(output, row);
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(output.ToString())).ToArray();
    }

    private static void AppendRow(StringBuilder output, IEnumerable<string?> values)
    {
        output.Append(string.Join(",", values.Select(value => ForceQuote(Protect(value ?? ""))))).Append("\r\n");
    }

    private static string ForceQuote(string value)
    {
        var quoted = SharedCsv.Quote(value);
        return quoted.StartsWith('"') && quoted.EndsWith('"') ? quoted : $"\"{quoted}\"";
    }

    // A spreadsheet may skip leading whitespace/control characters. The tab is
    // inside the quoted cell and is specific to this human-view Procurement file.
    internal static string Protect(string value)
    {
        var index = 0;
        while (index < value.Length && (char.IsWhiteSpace(value[index]) || char.IsControl(value[index])
            || value[index] == '\uFEFF')) index++;
        if (index < value.Length && value[index] is ('=' or '+' or '-' or '@'
            or '\uFF1D' or '\uFF0B' or '\uFF0D' or '\uFF20'))
            return "\t" + value;
        return value;
    }

    internal static async Task<IResult> CompleteAsync(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http,
        string recordType, string normalizedFilter, int rowCount, byte[] content, string fileName,
        CancellationToken ct)
    {
        var exportId = Guid.CreateVersion7();
        db.AddScoped(new AuditEvent
        {
            RecordType = recordType,
            RecordId = exportId,
            Action = "ExportGenerated",
            ActorSubjectId = scope.SubjectId,
            CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                filterSha256 = Sha256(Encoding.UTF8.GetBytes(normalizedFilter)),
                rowCount,
                payloadSha256 = Sha256(content),
                branchId = scope.WorkLocationId,
                allPermittedBranches = scope.IsExplicitAllBranches,
            }),
        });
        await db.SaveChangesAsync(ct);
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Export-Id"] = exportId.ToString("D");
        return Results.File(content, "text/csv; charset=utf-8", fileName);
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    // Same native-reader admission used by /meta: live connection, immutable
    // model parity and no unapplied Procurement migration. This adds no flag.
    internal static async Task<bool> NativeReaderReadyAsync(ProcurementDbContext db,
        IConfiguration configuration, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
        && !ProcurementModelParityDiagnostics.HasPendingModelChanges(db, configuration, "export.model-parity")
        && !(await db.Database.GetPendingMigrationsAsync(ct)).Any();

    internal static IResult Unavailable(HttpContext http, ILogger logger, string code)
    {
        logger.LogWarning("Procurement CSV export unavailable: {Code} {TraceId}", code, http.TraceIdentifier);
        return Results.Json(new { code, traceId = http.TraceIdentifier }, statusCode: 503);
    }
}
