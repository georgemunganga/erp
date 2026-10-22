using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

/// <summary>Read-only catalog facts. No default/seed/row data is selected or serialized.</summary>
internal sealed class HrmModelCatalog
{
    private sealed record Column(string Name, string Type, bool Nullable);
    private sealed record Key(string Name, string Kind, string[] Columns, string? PrincipalSchema, string? PrincipalTable, string[]? PrincipalColumns, string DeleteAction, bool Validated, bool Deferrable, bool Deferred);
    private sealed record Index(string Name, string[] Columns, bool Unique, bool Valid, string? Filter);
    private readonly HashSet<string> schemas = [];
    private readonly Dictionary<(string Schema, string Table), List<Column>> columns = [];
    private readonly Dictionary<(string Schema, string Table), List<Key>> keys = [];
    private readonly Dictionary<(string Schema, string Table), List<Index>> indexes = [];

    internal static async Task<HrmModelCatalog> ReadAsync(HrmDbContext owner)
    {
        var result = new HrmModelCatalog();
        await using var connection = new NpgsqlConnection(owner.Database.GetDbConnection().ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) await readOnly.ExecuteNonQueryAsync();
        await using (var command = new NpgsqlCommand("SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg_%' AND nspname<>'information_schema'", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) result.schemas.Add(reader.GetString(0));
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname,c.relname,a.attname,format_type(a.atttypid,a.atttypmod),NOT a.attnotnull
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
            WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
            ORDER BY n.nspname,c.relname,a.attnum
            """, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync())
        {
            var table = (reader.GetString(0), reader.GetString(1));
            if (!result.columns.TryGetValue(table, out var list)) result.columns[table] = list = [];
            list.Add(new(reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
        }
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname,c.relname,k.conname,k.contype::text,
              ARRAY(SELECT a.attname::text FROM unnest(k.conkey) WITH ORDINALITY x(id,ord) JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=x.id ORDER BY x.ord),
              pn.nspname,p.relname,
              CASE WHEN k.contype='f' THEN ARRAY(SELECT a.attname::text FROM unnest(k.confkey) WITH ORDINALITY x(id,ord) JOIN pg_attribute a ON a.attrelid=p.oid AND a.attnum=x.id ORDER BY x.ord) END,
              k.confdeltype::text,k.convalidated,k.condeferrable,k.condeferred
            FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            LEFT JOIN pg_class p ON p.oid=k.confrelid LEFT JOIN pg_namespace pn ON pn.oid=p.relnamespace
            WHERE k.contype IN ('p','u','f') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
            """, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync())
        {
            var table = (reader.GetString(0), reader.GetString(1));
            if (!result.keys.TryGetValue(table, out var list)) result.keys[table] = list = [];
            list.Add(new(reader.GetString(2), reader.GetString(3), reader.GetFieldValue<string[]>(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<string[]>(7), reader.GetString(8), reader.GetBoolean(9), reader.GetBoolean(10), reader.GetBoolean(11)));
        }
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname,c.relname,i.relname,
              ARRAY(SELECT a.attname::text FROM unnest(x.indkey) WITH ORDINALITY k(id,ord) JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.id ORDER BY k.ord),
              x.indisunique,x.indisvalid AND x.indisready,pg_get_expr(x.indpred,x.indrelid)
            FROM pg_index x JOIN pg_class c ON c.oid=x.indrelid JOIN pg_class i ON i.oid=x.indexrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
            """, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync())
        {
            var table = (reader.GetString(0), reader.GetString(1));
            if (!result.indexes.TryGetValue(table, out var list)) result.indexes[table] = list = [];
            list.Add(new(reader.GetString(2), reader.GetFieldValue<string[]>(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        await transaction.RollbackAsync(); return result;
    }

    internal object Compare(MigrationOperation operation, IRelationalModel target)
    {
        string? Text(string name) => operation.GetType().GetProperty(name)?.GetValue(operation) as string;
        if (operation is EnsureSchemaOperation ensure) return new { supported = true, existingSchemaMatches = schemas.Contains(ensure.Name) };
        var schema = Text("NewSchema") ?? Text("Schema") ?? "hrm";
        var table = operation is RenameTableOperation ? Text("NewName") : Text("Table") ?? (operation is TableOperation ? Text("Name") : null);
        // MigrationOperation subtypes such as DropTableOperation need their explicit table name.
        if (operation is DropTableOperation drop) return new { supported = true, targetAbsent = !columns.ContainsKey((drop.Schema ?? "hrm", drop.Name)) };
        if (table is null) return new { supported = false, reason = "NonTableOperationNeedsOwningReview" };
        var expected = target.Tables.SingleOrDefault(x => (x.Schema ?? "hrm") == schema && x.Name == table);
        var present = columns.TryGetValue((schema, table), out var actualColumns);
        if (expected is null || !present) return new { supported = true, tableExists = present, currentModelTableExists = expected is not null, matchesCurrentModel = false };
        var actualKeys = keys.GetValueOrDefault((schema, table)) ?? [];
        var actualIndexes = indexes.GetValueOrDefault((schema, table)) ?? [];
        bool Key(string name, string kind, IEnumerable<string> names) => actualKeys.Any(x => x.Name == name && x.Kind == kind && x.Validated && !x.Deferrable && !x.Deferred && x.Columns.SequenceEqual(names));
        var requiredColumns = expected.Columns.Select(x => new { column = x.Name,
            exists = actualColumns!.Any(a => a.Name == x.Name),
            typeMatches = actualColumns!.Any(a => a.Name == x.Name && string.Equals(a.Type, x.StoreType, StringComparison.OrdinalIgnoreCase)),
            nullabilityMatches = actualColumns!.Any(a => a.Name == x.Name && a.Nullable == x.IsNullable) }).ToArray();
        var requiredKeys = expected.UniqueConstraints.Select(x => new { name = x.Name,
            matches = Key(x.Name, ReferenceEquals(x, expected.PrimaryKey) ? "p" : "u", x.Columns.Select(c => c.Name)) }).ToArray();
        var requiredForeignKeys = expected.ForeignKeyConstraints.Select(x => new { name = x.Name,
            matches = actualKeys.Any(a => a.Name == x.Name && a.Kind == "f" && a.Validated && !a.Deferrable && !a.Deferred && a.Columns.SequenceEqual(x.Columns.Select(c => c.Name))
                && a.PrincipalSchema == (x.PrincipalTable.Schema ?? "hrm") && a.PrincipalTable == x.PrincipalTable.Name
                && a.PrincipalColumns is not null && a.PrincipalColumns.SequenceEqual(x.PrincipalColumns.Select(c => c.Name))
                && a.DeleteAction == (x.OnDeleteAction.ToString() switch { "Cascade" => "c", "Restrict" => "r", "SetNull" => "n", "SetDefault" => "d", _ => "a" })) }).ToArray();
        var requiredIndexes = expected.Indexes.Select(x => new { name = x.Name,
            matches = actualIndexes.Any(a => a.Name == x.Name && a.Valid && a.Unique == x.IsUnique && a.Columns.SequenceEqual(x.Columns.Select(c => c.Name))
                && a.Filter == x.Filter) }).ToArray();
        return new { supported = true, schema, table, tableExists = true, currentModelTableExists = true,
            columns = requiredColumns, keys = requiredKeys, foreignKeys = requiredForeignKeys, indexes = requiredIndexes,
            modelColumnCount = expected.Columns.Count(), actualColumnCount = actualColumns!.Count,
            requiredModelShapeMatches = requiredColumns.All(x => x.exists && x.typeMatches && x.nullabilityMatches)
                && requiredKeys.All(x => x.matches) && requiredForeignKeys.All(x => x.matches) && requiredIndexes.All(x => x.matches),
            extraColumns = actualColumns.Count(a => !expected.Columns.Any(x => x.Name == a.Name)),
            manualChecksDefaultsTriggersAndSequenceStateNotCompared = true };
    }
}
