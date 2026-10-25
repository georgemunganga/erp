using System.Text;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.Shared;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementHumanCsvExportTests
{
    [Fact]
    public void Export_filter_accepts_only_single_bounded_search_and_status_values()
    {
        var request = new DefaultHttpContext().Request;
        request.QueryString = new QueryString("?search=vendor&status=Active");
        Assert.True(ProcurementHumanCsvExport.ValidFilters(request));
        foreach (var invalid in new[]
        {
            "?search=one&search=two", "?page=1", "?selectedId=abc",
            "?search=%0A", "?status=%09", $"?search={new string('x', 501)}",
            $"?status={new string('x', 81)}",
        })
        {
            request.QueryString = new QueryString(invalid);
            Assert.False(ProcurementHumanCsvExport.ValidFilters(request));
        }
    }

    [Fact]
    public void Spreadsheet_export_has_fixed_width_bom_crlf_quoted_cells_and_formula_protection()
    {
        var content = ProcurementHumanCsvExport.Encode(["Code", "Name"],
            [["A,1", "  =SUM(1,2)"], ["B\"2", "Second\nline"], ["C3", "Δelta"]]);
        Assert.Equal(Encoding.UTF8.GetPreamble(), content[..3]);
        var decoded = Encoding.UTF8.GetString(content, 3, content.Length - 3);
        Assert.Equal("\"Code\",\"Name\"\r\n\"A,1\",\"\t  =SUM(1,2)\"\r\n\"B\"\"2\",\"Second\nline\"\r\n\"C3\",\"Δelta\"\r\n", decoded);
        Assert.Equal("\"Code\",\"Name\"\r\n", Encoding.UTF8.GetString(
            ProcurementHumanCsvExport.Encode(["Code", "Name"], []), 3,
            ProcurementHumanCsvExport.Encode(["Code", "Name"], []).Length - 3));
        Assert.Throws<InvalidOperationException>(() => ProcurementHumanCsvExport.Encode(["Code", "Name"], [["only one"]]));
    }

    [Theory]
    [InlineData("=1+1")][InlineData("+1")][InlineData("-1")][InlineData("@user")]
    [InlineData("\uFEFF =1")][InlineData("\uFF1D1")]
    public void Formula_leaders_are_neutralized_in_human_view(string value)
    {
        Assert.StartsWith("\t", ProcurementHumanCsvExport.Protect(value));
    }

    [Fact]
    public void Limit_and_original_HRM_csv_quote_bytes_remain_exact()
    {
        Assert.False(ProcurementHumanCsvExport.ExceedsLimit(10_000));
        Assert.True(ProcurementHumanCsvExport.ExceedsLimit(10_001));
        Assert.Equal("plain", SharedCsv.Quote("plain"));
        Assert.Equal("\"a,b\"", SharedCsv.Quote("a,b"));
        Assert.Equal("\"a\"\"b\"", SharedCsv.Quote("a\"b"));
        Assert.Equal("\"a\nb\"", SharedCsv.Quote("a\nb"));
    }
}
