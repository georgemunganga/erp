using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Preserves the pre-M6B row_to_json bytes for old allocations after
/// PostgreSQL adds the two nullable principal receiving columns. This is a
/// named, allocation-only projection; nonnull principal links remain evidence.</summary>
public static class InvoiceProcurementAllocationCanonicalRow
{
    private const string Source = "principal_receiving_source_state_id";
    private const string Fact = "principal_receiving_fact_id";

    public static InvoiceProcurementInventoryCapture ProjectCapture(
        InvoiceProcurementInventoryCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (capture.Tables is null) throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
        return capture with
        {
            Tables = capture.Tables.Select(table => table.TableName != "invoice_match_allocations"
                ? table
                : table with
                {
                    Rows = table.Rows.Select(row =>
                    {
                        if (ProcurementEvidenceEncoding.PolicyJsonDigest(row.RowJson)
                            != row.CanonicalRowSha256)
                            throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
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
        if (tableName != "invoice_match_allocations") return raw;
        if (string.IsNullOrEmpty(raw) || Encoding.UTF8.GetByteCount(raw) > 1_048_576)
            throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(raw);
            var reader = new Utf8JsonReader(bytes);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
            var properties = new List<(string Name, int Start, int End, JsonTokenType Kind)>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
                var name = reader.GetString()!;
                var start = checked((int)reader.TokenStartIndex);
                if (!reader.Read())
                    throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
                var kind = reader.TokenType;
                if ((name == Source || name == Fact) && kind != JsonTokenType.Null
                    && (kind != JsonTokenType.String || !Guid.TryParse(reader.GetString(), out var id)
                        || id == Guid.Empty))
                    throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
                reader.Skip();
                properties.Add((name, start, checked((int)reader.BytesConsumed), kind));
            }
            if (reader.TokenType != JsonTokenType.EndObject || reader.Read()
                || properties.Count(x => x.Name == Source) > 1
                || properties.Count(x => x.Name == Fact) > 1)
                throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
            var source = properties.SingleOrDefault(x => x.Name == Source);
            var fact = properties.SingleOrDefault(x => x.Name == Fact);
            var sourcePresent = source.Name is not null;
            var factPresent = fact.Name is not null;
            var sourceHasValue = sourcePresent && source.Kind != JsonTokenType.Null;
            var factHasValue = factPresent && fact.Kind != JsonTokenType.Null;
            if (sourcePresent != factPresent || sourceHasValue != factHasValue)
                throw new InvalidOperationException("InvoiceOwnerAllocationPrincipalPairIncomplete");
            if (sourceHasValue || !sourcePresent && !factPresent) return raw;

            var ranges = new List<(int Start, int End)>();
            for (var index = 0; index < properties.Count;)
            {
                if (properties[index].Name is not (Source or Fact))
                {
                    index++;
                    continue;
                }
                var first = index;
                while (index < properties.Count && properties[index].Name is (Source or Fact))
                    index++;
                var last = index - 1;
                if (first > 0)
                {
                    var comma = Comma(bytes, properties[first - 1].End, properties[first].Start);
                    ranges.Add((comma, properties[last].End));
                }
                else if (index < properties.Count)
                {
                    _ = Comma(bytes, properties[last].End, properties[index].Start);
                    ranges.Add((properties[first].Start, properties[index].Start));
                }
                else ranges.Add((properties[first].Start, properties[last].End));
            }
            var ordered = ranges.OrderBy(x => x.Start).ToArray();
            var output = new List<byte>(bytes.Length);
            var cursor = 0;
            foreach (var range in ordered)
            {
                if (range.Start > cursor)
                    output.AddRange(bytes.AsSpan(cursor, range.Start - cursor).ToArray());
                cursor = Math.Max(cursor, range.End);
            }
            output.AddRange(bytes.AsSpan(cursor).ToArray());
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (Exception error) when (error is JsonException or OverflowException or InvalidOperationException)
        {
            if (error is InvalidOperationException operation
                && operation.Message == "InvoiceOwnerAllocationPrincipalPairIncomplete") throw;
            throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid", error);
        }
    }

    private static int Comma(byte[] bytes, int from, int to)
    {
        var comma = -1;
        for (var index = from; index < to; index++)
        {
            if (bytes[index] == ',')
            {
                if (comma >= 0) throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
                comma = index;
            }
            else if (bytes[index] != (byte)' ' && bytes[index] != (byte)'\n'
                && bytes[index] != (byte)'\r' && bytes[index] != (byte)'\t')
                throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
        }
        if (comma < 0) throw new InvalidOperationException("InvoiceOwnerAllocationProjectionInvalid");
        return comma;
    }
}
