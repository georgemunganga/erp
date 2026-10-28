using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementIssuedCanonicalRowTests
{
    private static readonly Guid Issued = Guid.Parse("5e19f8b5-99af-4904-9cab-631e19491934");
    private static readonly Guid Other = Guid.Parse("8ba3097a-2716-4352-a4cf-d89583f2d4c1");

    [Theory]
    [InlineData("invoice_match_mapping_versions")]
    [InlineData("invoice_match_exposure_states")]
    [InlineData("invoice_match_results")]
    public void Legacy_derived_columns_preserve_original_row_bytes(string table)
    {
        var old = $"{{\"issued_version_id\":\"{Issued:D}\",\"note\":\"a, b\""
            + (table == "invoice_match_mapping_versions" ? ",\"source_schema_version\":null" : "") + "}";
        var appended = old[..^1] + $",\"legacy_issued_version_ref_id\":\"{Issued:D}\",\"principal_issued_version_ref_id\":null"
            + (table == "invoice_match_mapping_versions" ? "" : ",\"source_schema_version\":null") + "}";
        Assert.Equal(old, InvoiceProcurementIssuedCanonicalRow.Project(table, appended));
        Assert.Equal(old, InvoiceProcurementIssuedCanonicalRow.Project(table, old));
    }

    [Fact]
    public void Principal_source_and_parent_pin_remain_in_canonical_evidence()
    {
        var source = $"{{\"issued_version_id\":\"{Issued:D}\",\"source_schema_version\":3,\"legacy_issued_version_ref_id\":null,\"principal_issued_version_ref_id\":\"{Issued:D}\"}}";
        Assert.Equal(source, InvoiceProcurementIssuedCanonicalRow.Project("invoice_match_exposure_states", source));
        Assert.Throws<InvalidOperationException>(() => InvoiceProcurementIssuedCanonicalRow.Project("invoice_match_exposure_states",
            source.Replace(Issued.ToString("D") + "\"}", Other.ToString("D") + "\"}")));
    }

    [Fact]
    public void Legacy_columns_at_front_or_interleaved_are_removed_without_changing_other_bytes()
    {
        var front = $"{{\"legacy_issued_version_ref_id\":\"{Issued:D}\",\"principal_issued_version_ref_id\":null,\"source_schema_version\":null,\"issued_version_id\":\"{Issued:D}\",\"note\":\"x, y\"}}";
        Assert.Equal($"{{\"issued_version_id\":\"{Issued:D}\",\"note\":\"x, y\"}}",
            InvoiceProcurementIssuedCanonicalRow.Project("invoice_match_exposure_states", front));
        var interleaved = $"{{\"issued_version_id\":\"{Issued:D}\",\"legacy_issued_version_ref_id\":\"{Issued:D}\",\"note\":\"x, y\",\"principal_issued_version_ref_id\":null,\"source_schema_version\":null}}";
        Assert.Equal($"{{\"issued_version_id\":\"{Issued:D}\",\"note\":\"x, y\"}}",
            InvoiceProcurementIssuedCanonicalRow.Project("invoice_match_exposure_states", interleaved));
    }

    [Theory]
    [InlineData("{\"issued_version_id\":null,\"source_schema_version\":null,\"legacy_issued_version_ref_id\":null}")]
    [InlineData("{\"issued_version_id\":null,\"source_schema_version\":3,\"legacy_issued_version_ref_id\":null,\"principal_issued_version_ref_id\":null}")]
    [InlineData("{\"issued_version_id\":null,\"source_schema_version\":null,\"legacy_issued_version_ref_id\":null,\"principal_issued_version_ref_id\":\"8ba3097a-2716-4352-a4cf-d89583f2d4c1\"}")]
    public void Partial_or_wrong_discriminator_is_rejected(string raw)
    {
        Assert.Throws<InvalidOperationException>(() => InvoiceProcurementIssuedCanonicalRow.Project("invoice_match_results", raw));
    }
}
