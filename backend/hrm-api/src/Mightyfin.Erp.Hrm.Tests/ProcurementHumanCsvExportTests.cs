using System.Text;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.Shared;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementHumanCsvExportTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("comma,here", "\"comma,here\"")]
    [InlineData("say \"yes\"", "\"say \"\"yes\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    public void Extracted_HRM_quote_has_exact_old_bytes(string value, string expected) =>
        Assert.Equal(expected, SharedCsv.Quote(value));

    [Theory]
    [InlineData("=1+1")]
    [InlineData(" +SUM(A1:A2)")]
    [InlineData("\r\n@cmd")]
    [InlineData("\uFF1D1+1")]
    [InlineData("\uFEFF-1")]
    public void Spreadsheet_formula_leading_text_is_tab_prefixed_inside_quoted_cell(string unsafeValue)
    {
        var csv = Encoding.UTF8.GetString(ProcurementHumanCsvExport.Encode(["Name"], [[unsafeValue]]));
        Assert.StartsWith("\uFEFF\"Name\"\r\n\"\t", csv);
        Assert.Contains(unsafeValue.Replace("\"", "\"\""), csv);
    }

    [Fact]
    public void Human_csv_has_fixed_width_header_only_case_and_technical_bound()
    {
        Assert.Equal("\uFEFF\"Number\",\"Name\"\r\n", Encoding.UTF8.GetString(
            ProcurementHumanCsvExport.Encode(["Number", "Name"], [])));
        Assert.False(ProcurementHumanCsvExport.ExceedsLimit(10_000));
        Assert.True(ProcurementHumanCsvExport.ExceedsLimit(10_001));
        Assert.Throws<InvalidOperationException>(() => ProcurementHumanCsvExport.Encode(["One", "Two"], [["one"]]));
    }

    [Fact]
    public void Export_filter_accepts_only_one_bounded_server_search_and_status()
    {
        static bool Valid(string query)
        {
            var request = new DefaultHttpContext().Request;
            request.QueryString = new QueryString(query);
            return ProcurementHumanCsvExport.ValidFilters(request);
        }
        Assert.True(Valid("?search=existing&status=Active"));
        Assert.False(Valid("?search=a&page=2"));
        Assert.False(Valid("?search=a&search=b"));
        Assert.True(Valid("?search=" + new string('a', 500)));
        Assert.False(Valid("?search=" + new string('a', 501)));
    }
}
