using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Application.Shared;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record MasterImportPreviewRequest(int SchemaVersion, string FileName, string CsvBase64,
    Dictionary<string, string> Mapping, string DuplicatePolicy, int[]? AcknowledgedSimilarityRows);

public sealed record MasterImportCommitRequest(Guid BatchId, string PreviewSha256, Guid CommandKey);

internal sealed record MasterImportRow(int RowNumber, int StartLine, int EndLine,
    SortedDictionary<string, string> Fields, string Disposition, string? Code,
    string? Message, string? MatchSetSha256);

internal sealed record MasterImportSnapshot(int SchemaVersion, string TenantId, Guid CompanyId,
    string ActorSubjectId, string Kind, string FileSha256, string FileName, string DuplicatePolicy,
    SortedDictionary<string, string> Mapping, int[] AcknowledgedSimilarityRows, string? CompanyCurrencyCode,
    MasterImportRow[] Rows);

/// <summary>Finite wire and snapshot rules. No authorization, persistence or business mutation.</summary>
internal static class ProcurementMasterImportContracts
{
    internal const int SchemaVersion = 1;
    internal const int MaxJsonBytes = 8 * 1024 * 1024; // Base64 expansion of the 5 MiB CSV plus finite metadata.
    internal const int MaxCsvBytes = 5 * 1024 * 1024;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static readonly BoundedCsvLimits CsvLimits = BoundedCsvLimits.Default;
    private static readonly string[] VendorFields = ["legalName", "displayName", "countryCode", "registrationNumber"];
    private static readonly string[] VendorRequired = ["legalName", "displayName", "countryCode"];
    private static readonly string[] ItemFields = ["code", "name", "kind", "categoryCode", "unitCode", "description"];
    private static readonly string[] ItemRequired = ["code", "name", "kind", "categoryCode", "unitCode"];

    internal static string[] Fields(string kind) => kind == "Vendor" ? VendorFields : ItemFields;
    internal static string[] Required(string kind) => kind == "Vendor" ? VendorRequired : ItemRequired;
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    internal static bool IsHash(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static (BoundedCsvDocument? Document, byte[]? Csv, string? Code) Parse(MasterImportPreviewRequest? request,
        string kind)
    {
        if (request is null || request.SchemaVersion != SchemaVersion || request.Mapping is null
            || request.CsvBase64 is null || request.DuplicatePolicy is not ("reject" or "skip")
            || request.Mapping.Values.Any(value => value is null)
            || string.IsNullOrWhiteSpace(request.FileName)
            || Encoding.UTF8.GetByteCount(request.FileName) > 128
            || request.FileName.Any(x => char.IsControl(x) || x is '/' or '\\')
            || !request.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            || request.AcknowledgedSimilarityRows is { Length: > 1000 })
            return (null, null, "validation-failed");
        if (request.CsvBase64.Length > ((MaxCsvBytes + 2) / 3) * 4 + 4)
            return (null, null, "import-limit-exceeded");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.CsvBase64); }
        catch (FormatException) { return (null, null, "validation-failed"); }
        if (bytes.Length > MaxCsvBytes) return (null, null, "import-limit-exceeded");
        try
        {
            var document = BoundedCsvRecordReader.Parse(bytes, CsvLimits);
            if (document.Rows.Count == 0) return (null, null, "validation-failed");
            BoundedCsvRecordReader.ValidateMapping(document, request.Mapping, Fields(kind), Required(kind));
            var acknowledgements = request.AcknowledgedSimilarityRows ?? [];
            if (acknowledgements.Distinct().Count() != acknowledgements.Length
                || acknowledgements.Any(x => x < 2)) return (null, null, "validation-failed");
            return (document, bytes, null);
        }
        catch (BoundedCsvException error)
        {
            return (null, null, error.Code is "byte-limit" or "row-limit" or "column-limit" or "cell-limit"
                ? "import-limit-exceeded" : "validation-failed");
        }
    }

    internal static SortedDictionary<string, string> MapRow(BoundedCsvDocument document,
        BoundedCsvRecord record, IReadOnlyDictionary<string, string> mapping)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < document.Headers.Count; i++)
            if (mapping.TryGetValue(document.Headers[i], out var target) && target.Length > 0)
                fields.Add(target, record.Cells[i].Trim());
        return fields;
    }

    internal static SortedDictionary<string, string> SortMapping(IReadOnlyDictionary<string, string> mapping) =>
        new(mapping.OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    internal static string SnapshotText(MasterImportSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Json);
    internal static MasterImportSnapshot? ReadSnapshot(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try { return JsonSerializer.Deserialize<MasterImportSnapshot>(text, Json); }
        catch (JsonException) { return null; }
    }

    internal static object PreviewView(Guid batchId, string digest, DateTimeOffset expiresAt,
        MasterImportRow[] rows) => new
        {
            state = "Previewed", batchId, previewSha256 = digest, expiresAt,
            rows = rows.Select(x => new { x.RowNumber, x.StartLine, x.EndLine,
                x.Disposition, x.Code, x.Message }).ToArray(),
            willCreate = rows.Count(x => x.Disposition == "create"),
            willSkip = rows.Count(x => x.Disposition == "skip"),
            willError = rows.Count(x => x.Disposition == "error"),
        };
}
