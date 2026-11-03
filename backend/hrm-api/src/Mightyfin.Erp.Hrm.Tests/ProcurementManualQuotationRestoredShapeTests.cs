using System.Text.Json.Nodes;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementManualQuotationRestoredShapeTests
{
    // Schema metadata only. This fixture is an exact projection of saved restore
    // metadata, not a claimed execution of NativeShape.Sql or the runtime provider.
    private static string Observed()
    {
        using var stream = typeof(ProcurementManualQuotationRestoredShapeTests).Assembly
            .GetManifestResourceStream("Mightyfin.Erp.Hrm.Tests.Fixtures.M7aRestoredDerivedShape.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Derived_restore_observation_is_rejected_by_both_old_contracts_and_admitted_exactly()
    {
        var observed = Observed();
        var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(observed);
        Assert.NotEqual(ProcurementEvidenceEncoding.PolicyJsonDigest(ProcurementManualQuotationNativeShape.Json), digest);
        Assert.NotEqual(ProcurementEvidenceEncoding.PolicyJsonDigest(ProcurementManualQuotationNativeShape.ProductionJson), digest);
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest(ProcurementManualQuotationNativeShape.RestoredJson), digest);
        Assert.True(ProcurementManualQuotationNativeShape.Matches(observed));
        Assert.True(ProcurementManualQuotationNativeShape.Matches(ProcurementManualQuotationNativeShape.Json));
        Assert.True(ProcurementManualQuotationNativeShape.Matches(ProcurementManualQuotationNativeShape.ProductionJson));
    }

    [Fact]
    public void Database_equality_uses_the_same_three_complete_contracts_and_unchanged_catalog_query()
    {
        var contracts = new[] { ProcurementManualQuotationNativeShape.Json,
            ProcurementManualQuotationNativeShape.ProductionJson, ProcurementManualQuotationNativeShape.RestoredJson };
        var expected = "SELECT " + string.Join(" OR ", contracts.Select(value =>
            "actual='" + value.Replace("'", "''", StringComparison.Ordinal) + "'::jsonb"))
            + " FROM (" + ProcurementManualQuotationNativeShape.Sql + ") AS captured(actual)";
        Assert.Equal(expected, ProcurementManualQuotationNativeShape.ReadinessSql);
        Assert.Equal(7, JsonNode.Parse(Observed())!.AsObject().Count);
    }

    [Theory]
    [InlineData("contact-check")]
    [InlineData("version-check")]
    [InlineData("line-check")]
    [InlineData("bid-check")]
    [InlineData("literal-case")]
    [InlineData("parentheses")]
    [InlineData("missing-table")]
    [InlineData("missing-constraint")]
    [InlineData("scope-foreign-key")]
    [InlineData("scope-column")]
    [InlineData("index-definition")]
    [InlineData("index-invalid")]
    [InlineData("rls")]
    public void Restored_contract_rejects_unknown_mutations(string change)
    {
        var node = JsonNode.Parse(Observed())!;
        var contact = node["procurement.sourcing_manual_contacts"]!;
        var constraints = contact["constraints"]!;
        var check = constraints["ck_manual_contact_v2"]!;
        switch (change)
        {
            case "contact-check": check["validated"] = false; break;
            case "version-check": node["procurement.sourcing_manual_quote_versions"]!["constraints"]!
                ["ck_manual_quote_version_v3"]!["definition"] = "CHECK (true)"; break;
            case "line-check": node["procurement.supplier_bid_lines"]!["constraints"]!
                ["ck_supplier_bid_line_capture_v2"]!["definition"] = "CHECK (true)"; break;
            case "bid-check": node["procurement.supplier_bids"]!["constraints"]!
                ["ck_supplier_bid_capture_v2"]!["definition"] = "CHECK (true)"; break;
            case "literal-case": check["definition"] = check["definition"]!.GetValue<string>()
                .Replace("EmailOutsideSystem", "EMAILOUTSIDESYSTEM", StringComparison.Ordinal); break;
            case "parentheses": check["definition"] = check["definition"]!.GetValue<string>()
                .Replace("CHECK (", "CHECK ((", StringComparison.Ordinal) + ")"; break;
            case "missing-table": node.AsObject().Remove("procurement.sourcing_invitations"); break;
            case "missing-constraint": constraints.AsObject().Remove("fk_quote_contact_decision"); break;
            case "scope-foreign-key": constraints["fk_quote_contact_invitation"]!["definition"] =
                constraints["fk_quote_contact_invitation"]!["definition"]!.GetValue<string>()
                    .Replace("tenant_id, legal_entity_id, ", "", StringComparison.Ordinal); break;
            case "scope-column": contact["columns"]!.AsArray().Single(x =>
                x!["name"]!.GetValue<string>() == "tenant_id")!["notNull"] = false; break;
            case "index-definition": contact["indexes"]![0]!["definition"] = "CREATE INDEX unknown ON procurement.sourcing_manual_contacts (id)"; break;
            case "index-invalid": contact["indexes"]![0]!["valid"] = false; break;
            case "rls": contact["rls"] = true; break;
        }
        Assert.NotEqual(ProcurementEvidenceEncoding.PolicyJsonDigest(Observed()),
            ProcurementEvidenceEncoding.PolicyJsonDigest(node.ToJsonString()));
        Assert.False(ProcurementManualQuotationNativeShape.Matches(node.ToJsonString()));
    }

    [Fact]
    public void Individual_restored_checks_cannot_be_mixed_with_old_contracts()
    {
        var observed = JsonNode.Parse(Observed())!;
        var native = JsonNode.Parse(ProcurementManualQuotationNativeShape.Json)!;
        var changed = 0;
        foreach (var table in native.AsObject())
        foreach (var constraint in table.Value!["constraints"]!.AsObject())
        {
            var definition = constraint.Value!["definition"]!.GetValue<string>();
            if (definition == observed[table.Key]!["constraints"]![constraint.Key]!["definition"]!.GetValue<string>()) continue;
            changed++;
            var hybrid = JsonNode.Parse(Observed())!;
            hybrid[table.Key]!["constraints"]![constraint.Key]!["definition"] = definition;
            Assert.False(ProcurementManualQuotationNativeShape.Matches(hybrid.ToJsonString()));
        }
        Assert.Equal(4, changed);
        observed["procurement.sourcing_publication_decisions"]!["constraints"]!
            ["ck_sourcing_publication_decision_v2"]!["definition"] =
            M4PrincipalRfqPhysicalChecks.Native["ck_sourcing_publication_decision_v2"];
        Assert.False(ProcurementManualQuotationNativeShape.Matches(observed.ToJsonString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_restored_shape_does_not_remove_owner_or_privileged_role_denials(bool write)
    {
        var sql = ProcurementManualQuotationSchemaReadiness.PrivilegeSql(write, "execution");
        Assert.Contains("session_user=current_user AND current_user='execution'", sql);
        Assert.Contains("r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls", sql);
        foreach (var table in ProcurementManualQuotationSchemaReadiness.ReadTables)
            Assert.Contains("c.oid='procurement." + table + "'::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET'))", sql);
    }
}
