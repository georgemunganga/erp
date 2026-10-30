using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Named projection of B2's derived issued-parent columns. Only exact
/// legacy rows lose the new columns, retaining pre-B2 row_to_json bytes.</summary>
public static class InvoiceProcurementIssuedCanonicalRow
{
    private const string Legacy = "legacy_issued_version_ref_id";
    private const string Principal = "principal_issued_version_ref_id";
    private const string Schema = "source_schema_version";
    private const string Effective = "issued_version_id";

    public static InvoiceProcurementInventoryCapture ProjectCapture(InvoiceProcurementInventoryCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (capture.Tables is null) throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
        return capture with
        {
            Tables = capture.Tables.Select(table => table.TableName is not
                ("invoice_match_mapping_versions" or "invoice_match_exposure_states" or "invoice_match_results")
                ? table
                : table with
                {
                    Rows = table.Rows.Select(row =>
                    {
                        if (ProcurementEvidenceEncoding.PolicyJsonDigest(row.RowJson) != row.CanonicalRowSha256)
                            throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
                        var projected = Project(table.TableName, row.RowJson);
                        return projected == row.RowJson ? row : row with
                        {
                            RowJson = projected,
                            CanonicalRowSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(projected)
                        };
                    }).ToArray()
                }).ToArray()
        };
    }

    public static string Project(string tableName, string raw)
    {
        var exposureOrResult = tableName is "invoice_match_exposure_states" or "invoice_match_results";
        if (tableName != "invoice_match_mapping_versions" && !exposureOrResult) return raw;
        if (string.IsNullOrEmpty(raw) || Encoding.UTF8.GetByteCount(raw) > 1_048_576)
            throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(raw);
            var reader = new Utf8JsonReader(bytes);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
            var fields = new List<(string Name, int Start, int End, JsonTokenType Kind, string? Value)>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
                var name = reader.GetString()!;
                var start = checked((int)reader.TokenStartIndex);
                if (!reader.Read()) throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
                var kind = reader.TokenType;
                var value = kind == JsonTokenType.String ? reader.GetString()
                    : name == Schema && kind == JsonTokenType.Number ? reader.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null;
                reader.Skip();
                fields.Add((name, start, checked((int)reader.BytesConsumed), kind, value));
            }
            if (reader.TokenType != JsonTokenType.EndObject || reader.Read()
                || fields.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
            var legacy = fields.SingleOrDefault(x => x.Name == Legacy);
            var principal = fields.SingleOrDefault(x => x.Name == Principal);
            var schema = fields.SingleOrDefault(x => x.Name == Schema);
            var effective = fields.SingleOrDefault(x => x.Name == Effective);
            var newCount = (legacy.Name is null ? 0 : 1) + (principal.Name is null ? 0 : 1)
                + (exposureOrResult && schema.Name is not null ? 1 : 0);
            if (newCount == 0 && schema.Name is null) return raw; // Pre-B capture.
            if (effective.Name is null || schema.Name is null)
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
            // M6A already stored mapping.source_schema_version. A retained
            // baseline between M6A and B2 has no generated pins to remove.
            if (!exposureOrResult && legacy.Name is null && principal.Name is null)
            {
                if (schema.Kind != JsonTokenType.Null
                    && !(schema.Kind == JsonTokenType.Number && schema.Value == "3"))
                    throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
                return raw;
            }
            if (legacy.Name is null || principal.Name is null || exposureOrResult && newCount != 3)
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
            var issued = ParseOptionalId(effective);
            var legacyId = ParseOptionalId(legacy);
            var principalId = ParseOptionalId(principal);
            if (schema.Kind == JsonTokenType.Number && schema.Value == "3")
            {
                if (issued is null || principalId != issued || legacyId is not null)
                    throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
                return raw;
            }
            if (schema.Kind != JsonTokenType.Null || legacyId != issued || principalId is not null)
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");

            return Remove(bytes, fields, exposureOrResult
                ? [Legacy, Principal, Schema] : [Legacy, Principal]);
        }
        catch (Exception error) when (error is JsonException or OverflowException or FormatException or InvalidOperationException)
        {
            throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid", error);
        }
    }

    private static string Remove(byte[] bytes,
        List<(string Name, int Start, int End, JsonTokenType Kind, string? Value)> fields,
        string[] omit)
    {
            var ranges = new List<(int Start, int End)>();
            for (var index = 0; index < fields.Count;)
            {
                if (!omit.Contains(fields[index].Name)) { index++; continue; }
                var first = index;
                while (index < fields.Count && omit.Contains(fields[index].Name)) index++;
                var last = index - 1;
                if (first > 0)
                    ranges.Add((Comma(bytes, fields[first - 1].End, fields[first].Start), fields[last].End));
                else if (index < fields.Count)
                {
                    _ = Comma(bytes, fields[last].End, fields[index].Start);
                    ranges.Add((fields[first].Start, fields[index].Start));
                }
                else ranges.Add((fields[first].Start, fields[last].End));
            }
            var output = new List<byte>(bytes.Length);
            var cursor = 0;
            foreach (var range in ranges.OrderBy(x => x.Start))
            {
                if (range.Start > cursor) output.AddRange(bytes.AsSpan(cursor, range.Start - cursor).ToArray());
                cursor = Math.Max(cursor, range.End);
            }
            output.AddRange(bytes.AsSpan(cursor).ToArray());
            return Encoding.UTF8.GetString(output.ToArray());
    }

    private static Guid? ParseOptionalId((string Name, int Start, int End, JsonTokenType Kind, string? Value) field)
    {
        if (field.Kind == JsonTokenType.Null) return null;
        if (field.Kind != JsonTokenType.String || !Guid.TryParse(field.Value, out var id) || id == Guid.Empty)
            throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
        return id;
    }

    private static int Comma(byte[] bytes, int from, int to)
    {
        var comma = -1;
        for (var i = from; i < to; i++)
        {
            if (bytes[i] == ',') { if (comma >= 0) throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid"); comma = i; }
            else if (bytes[i] != (byte)' ' && bytes[i] != (byte)'\t'
                && bytes[i] != (byte)'\r' && bytes[i] != (byte)'\n')
                throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
        }
        if (comma < 0) throw new InvalidOperationException("InvoiceOwnerIssuedProjectionInvalid");
        return comma;
    }
}
