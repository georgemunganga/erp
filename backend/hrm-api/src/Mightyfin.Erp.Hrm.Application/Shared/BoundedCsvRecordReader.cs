using System.Text;

namespace Mightyfin.Erp.Hrm.Application.Shared;

/// <summary>Finite parser limits for a server-side import preview, independent of the browser demo.</summary>
public sealed record BoundedCsvLimits(int MaxBytes, int MaxRows, int MaxColumns, int MaxCellBytes)
{
    public static BoundedCsvLimits Default { get; } = new(5 * 1024 * 1024, 1000, 64, 16 * 1024);

    internal void Validate()
    {
        if (MaxBytes is < 1 or > 20 * 1024 * 1024 || MaxRows is < 1 or > 10000
            || MaxColumns is < 1 or > 256 || MaxCellBytes is < 1 or > 64 * 1024)
            throw new ArgumentOutOfRangeException(nameof(BoundedCsvLimits), "CSV limits exceed the supported bounds.");
    }
}

public sealed record BoundedCsvRecord(int StartLine, int EndLine, IReadOnlyList<string> Cells);

public sealed record BoundedCsvDocument(
    IReadOnlyList<string> Headers, IReadOnlyList<BoundedCsvRecord> Rows, int InputBytes);

/// <summary>Stable parser code and physical line; never includes a source cell or filename.</summary>
public sealed class BoundedCsvException(string code, int line)
    : Exception($"CSV {code} at physical line {line}.")
{
    public string Code { get; } = code;
    public int Line { get; } = line;
}

/// <summary>
/// Strict UTF-8 CSV records for future typed import previews. Unlike the legacy
/// ImportRowParser.ParseCsvLine, a quoted field may span physical lines.
/// This class parses and validates shape only; it grants no import authority.
/// </summary>
public static class BoundedCsvRecordReader
{
    private enum FieldState { Start, Unquoted, Quoted, AfterQuote }
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<BoundedCsvDocument> ReadAsync(Stream source, BoundedCsvLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var bound = limits ?? BoundedCsvLimits.Default;
        bound.Validate();
        using var bytes = new MemoryStream();
        var block = new byte[8192];
        while (true)
        {
            var count = await source.ReadAsync(block, cancellationToken);
            if (count == 0) break;
            if (bytes.Length + count > bound.MaxBytes) throw new BoundedCsvException("byte-limit", 1);
            bytes.Write(block, 0, count);
        }
        return Parse(bytes.ToArray(), bound);
    }

    public static BoundedCsvDocument Parse(ReadOnlySpan<byte> source, BoundedCsvLimits? limits = null)
    {
        var bound = limits ?? BoundedCsvLimits.Default;
        bound.Validate();
        if (source.Length > bound.MaxBytes) throw new BoundedCsvException("byte-limit", 1);
        var inputBytes = source.Length;
        if (source.StartsWith([0xEF, 0xBB, 0xBF])) source = source[3..];
        string text;
        try { text = StrictUtf8.GetString(source); }
        catch (DecoderFallbackException) { throw new BoundedCsvException("invalid-utf8", 1); }

        var records = new List<BoundedCsvRecord>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var state = FieldState.Start;
        var line = 1;
        var startLine = 1;
        var touched = false;

        void Append(char value)
        {
            cell.Append(value);
            // For valid UTF-8, encoded bytes are never fewer than UTF-16 code units.
            if (cell.Length > bound.MaxCellBytes) throw new BoundedCsvException("cell-limit", startLine);
        }

        void FinishCell()
        {
            var value = cell.ToString();
            if (StrictUtf8.GetByteCount(value) > bound.MaxCellBytes)
                throw new BoundedCsvException("cell-limit", startLine);
            cells.Add(value);
            if (cells.Count > bound.MaxColumns) throw new BoundedCsvException("column-limit", startLine);
            cell.Clear();
            state = FieldState.Start;
        }

        void FinishRecord()
        {
            if (!touched && cells.Count == 0 && cell.Length == 0)
                throw new BoundedCsvException("blank-record", startLine);
            FinishCell();
            records.Add(new BoundedCsvRecord(startLine, line, cells.ToArray()));
            if (records.Count - 1 > bound.MaxRows)
                throw new BoundedCsvException("row-limit", startLine);
            cells.Clear();
            touched = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\0') throw new BoundedCsvException("invalid-character", line);
            if (ch is '\r' or '\n')
            {
                var crlf = ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
                if (state == FieldState.Quoted)
                {
                    Append(ch);
                    if (crlf) Append('\n');
                }
                else FinishRecord();
                if (crlf) i++;
                line++;
                if (state != FieldState.Quoted) startLine = line;
                continue;
            }
            if (state == FieldState.Quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { Append('"'); i++; }
                    else state = FieldState.AfterQuote;
                }
                else Append(ch);
                continue;
            }
            if (ch == ',')
            {
                FinishCell();
                touched = true;
                continue;
            }
            if (ch == '"')
            {
                if (state != FieldState.Start) throw new BoundedCsvException("invalid-quote", line);
                state = FieldState.Quoted;
                touched = true;
                continue;
            }
            if (state == FieldState.AfterQuote) throw new BoundedCsvException("invalid-quote", line);
            Append(ch);
            state = FieldState.Unquoted;
            touched = true;
        }
        if (state == FieldState.Quoted) throw new BoundedCsvException("unclosed-quote", startLine);
        if (touched || cells.Count > 0 || cell.Length > 0) FinishRecord();
        if (records.Count == 0) throw new BoundedCsvException("empty-file", 1);

        var header = records[0].Cells.Select(x => x.Trim()).ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (header.Any(x => x.Length == 0)) throw new BoundedCsvException("empty-header", records[0].StartLine);
        if (header.Any(x => !names.Add(x))) throw new BoundedCsvException("duplicate-header", records[0].StartLine);
        foreach (var record in records.Skip(1))
            if (record.Cells.Count != header.Length)
                throw new BoundedCsvException("column-count", record.StartLine);
        return new BoundedCsvDocument(header, records.Skip(1).ToArray(), inputBytes);
    }

    /// <summary>Validate a caller's explicit header-to-canonical-field mapping before any row is staged.</summary>
    public static IReadOnlyDictionary<string, string> ValidateMapping(BoundedCsvDocument document,
        IReadOnlyDictionary<string, string> mapping, IReadOnlyCollection<string> allowedTargets,
        IReadOnlyCollection<string> requiredTargets)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(allowedTargets);
        ArgumentNullException.ThrowIfNull(requiredTargets);
        var headers = document.Headers.ToHashSet(StringComparer.Ordinal);
        var allowed = allowedTargets.ToHashSet(StringComparer.Ordinal);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (source, target) in mapping)
        {
            if (!headers.Contains(source)) throw new BoundedCsvException("mapping-unknown-header", 1);
            if (string.IsNullOrEmpty(target)) continue; // explicitly unmapped source column
            if (!allowed.Contains(target)) throw new BoundedCsvException("mapping-unknown-target", 1);
            if (!assigned.Add(target)) throw new BoundedCsvException("mapping-duplicate-target", 1);
        }
        if (requiredTargets.Any(x => !assigned.Contains(x)))
            throw new BoundedCsvException("mapping-required-target", 1);
        // Do not hand an import adapter a validated view over caller-owned mutable state.
        var snapshot = mapping.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(snapshot);
    }
}
