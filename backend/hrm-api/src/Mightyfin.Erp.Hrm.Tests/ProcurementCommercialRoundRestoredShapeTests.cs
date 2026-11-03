using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundRestoredShapeTests
{
    private const string RestoredFingerprint = "f7d9d1254a04586578b8853da7c5152c8f3eb9a6ec3b42c07fa81d2da10d0032";
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Fact]
    public void Saved_catalog_projection_reproduces_both_actual_PostgreSQL_fingerprints()
    {
        Assert.Equal(CommercialRoundNativeContract.Fingerprint, Fingerprint(Load("Direct")));
        Assert.Equal(RestoredFingerprint, Fingerprint(Load("Restored")));
        Assert.NotEqual(CommercialRoundNativeContract.Fingerprint, RestoredFingerprint);
    }

    [Fact]
    public void Only_five_observed_CHECK_spellings_differ_in_the_complete_restored_contract()
    {
        var direct = Load("Direct");
        var restored = Load("Restored");
        Assert.Equal(5, CommercialRoundRestoredCheckForms.Observed.Count);
        Assert.True(JsonNode.DeepEquals(direct, Canonicalize(restored)));
        Assert.True(JsonNode.DeepEquals(direct, Canonicalize(direct)));
        Assert.Equal(CommercialRoundNativeContract.Fingerprint, Fingerprint(Canonicalize(restored)));
        foreach (var pair in CommercialRoundRestoredCheckForms.Observed)
        {
            Assert.Equal(pair.Direct, Constraint(direct, pair.Table, pair.Name)[6]!.GetValue<string>());
            Assert.Equal(pair.Restored, Constraint(restored, pair.Table, pair.Name)[6]!.GetValue<string>());
        }
    }

    public static IEnumerable<object[]> ObservedChecks => CommercialRoundRestoredCheckForms.Observed
        .Select(x => new object[] { x.Table, x.Name });

    [Theory]
    [MemberData(nameof(ObservedChecks))]
    public void Each_known_spelling_requires_the_exact_table_constraint_and_entire_definition(string table, string name)
    {
        var pair = CommercialRoundRestoredCheckForms.Observed.Single(x => x.Table == table && x.Name == name);
        Assert.Equal(pair.Direct, CommercialRoundRestoredCheckForms.CanonicalDefinition(table, name, pair.Restored));
        Assert.Equal(pair.Direct, CommercialRoundRestoredCheckForms.CanonicalDefinition(table, name, pair.Direct));
        Assert.Equal(pair.Restored, CommercialRoundRestoredCheckForms.CanonicalDefinition("unknown_table", name, pair.Restored));
        Assert.Equal(pair.Restored, CommercialRoundRestoredCheckForms.CanonicalDefinition(table, "unknown_check", pair.Restored));
        Assert.Equal(pair.Restored + " ", CommercialRoundRestoredCheckForms.CanonicalDefinition(table, name, pair.Restored + " "));
    }

    [Theory]
    [MemberData(nameof(ObservedChecks))]
    public void Weaker_version_or_result_guard_is_not_mapped_even_with_a_known_constraint_name(string table, string name)
    {
        var pair = CommercialRoundRestoredCheckForms.Observed.Single(x => x.Table == table && x.Name == name);
        var altered = table == "commercial_round_plans"
            ? pair.Restored.Replace("version > 0", "version >= 0", StringComparison.Ordinal)
            : pair.Restored.Replace("version = 1", "version >= 1", StringComparison.Ordinal);
        Assert.NotEqual(pair.Restored, altered);
        Assert.Equal(altered, CommercialRoundRestoredCheckForms.CanonicalDefinition(table, name, altered));
        var actual = Load("Restored");
        Constraint(actual, table, name)[6] = altered;
        Assert.NotEqual(CommercialRoundNativeContract.Fingerprint, Fingerprint(Canonicalize(actual)));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("column")]
    [InlineData("validated")]
    [InlineData("index")]
    [InlineData("trigger")]
    [InlineData("function")]
    [InlineData("source-key")]
    public void Restore_CHECK_support_never_masks_other_catalog_or_security_changes(string change)
    {
        var actual = Load("Restored");
        switch (change)
        {
            case "owner": actual["relations"]![0]![4] = "unexpected_owner"; break;
            case "column": actual["columns"]![0]![2] = "text"; break;
            case "validated": actual["constraints"]![0]![3] = false; break;
            case "index": actual["indexes"]![0]![1] = false; break;
            case "trigger": actual["triggers"]![0]![2] = "D"; break;
            case "function": actual["functions"]![0]![2] = true; break;
            case "source-key": actual["originalSourceKey"]![0]![2]![0] = "wrong_column"; break;
        }
        Assert.NotEqual(CommercialRoundNativeContract.Fingerprint, Fingerprint(Canonicalize(actual)));
    }

    [Fact]
    public void Production_SQL_maps_only_whole_observed_definitions_inside_the_existing_full_fingerprint()
    {
        var sql = ProcurementCommercialRoundReadiness.FingerprintSql;
        Assert.Contains("c.condeferred," + CommercialRoundRestoredCheckForms.Sql + ") ORDER BY r.relname,c.conname", sql);
        Assert.Contains("ELSE pg_get_constraintdef(c.oid) END", sql);
        Assert.Contains("c.contype='c' AND pg_get_constraintdef(c.oid)=", sql);
        Assert.DoesNotContain("regexp_replace", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replace(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'originalSourceKey'", sql);
        Assert.Contains("t.tgenabled", sql);
        Assert.Contains("p.prosecdef", sql);
        Assert.Contains("i.indisvalid,i.indisready,i.indislive,i.indimmediate", sql);
        Assert.Equal("24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b", CommercialRoundNativeContract.Fingerprint);
    }

    private static JsonNode Load(string form)
    {
        var assembly = typeof(ProcurementCommercialRoundRestoredShapeTests).Assembly;
        using var stream = assembly.GetManifestResourceStream("Mightyfin.Erp.Hrm.Tests.Fixtures.B1" + form + "CapturedShape.json")!;
        return JsonNode.Parse(stream)!;
    }

    private static JsonArray Constraint(JsonNode shape, string table, string name)
        => shape["constraints"]!.AsArray().Select(x => x!.AsArray())
            .Single(x => x[0]!.GetValue<string>() == table && x[1]!.GetValue<string>() == name);

    private static JsonNode Canonicalize(JsonNode shape)
    {
        var actual = shape.DeepClone();
        foreach (var row in actual["constraints"]!.AsArray().Select(x => x!.AsArray()))
            if (row[2]!.GetValue<string>() == "c")
                row[6] = CommercialRoundRestoredCheckForms.CanonicalDefinition(row[0]!.GetValue<string>(),
                    row[1]!.GetValue<string>(), row[6]!.GetValue<string>());
        return actual;
    }

    private static string Fingerprint(JsonNode node)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(PostgresJsonbText(node))));

    // Reproduce the actual PG jsonb::text encoding for these schema-only fixtures:
    // object keys by UTF8 byte length then bytes, JSON array order unchanged.
    private static string PostgresJsonbText(JsonNode? node) => node switch
    {
        JsonObject obj => "{" + string.Join(", ", obj.OrderBy(x => Encoding.UTF8.GetByteCount(x.Key))
            .ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => JsonSerializer.Serialize(x.Key, JsonOptions)
                + ": " + PostgresJsonbText(x.Value))) + "}",
        JsonArray arr => "[" + string.Join(", ", arr.Select(PostgresJsonbText)) + "]",
        null => "null",
        _ => node.ToJsonString(JsonOptions),
    };
}
