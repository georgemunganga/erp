using System.Text;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.Shared;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementHumanCsvExportTests
{
    [Fact]
    public void Selected_export_is_a_bounded_exact_unique_guid_set_and_cannot_become_all()
    {
        var request = new DefaultHttpContext().Request;
        var a = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        request.QueryString = new QueryString($"?ids={b:D},{a:D}");
        Assert.True(ProcurementHumanCsvExport.ValidFilters(request));
        Assert.True(ProcurementHumanCsvExport.TrySelectedIds(request, out var ids));
        Assert.Equal(new[] { a, b }, ids);
        var canonical = ProcurementHumanCsvExport.NormalizedFilter(null, null, ids);
        request.QueryString = new QueryString($"?ids={a:D},{b:D}");
        Assert.True(ProcurementHumanCsvExport.TrySelectedIds(request, out var reversed));
        Assert.Equal(canonical, ProcurementHumanCsvExport.NormalizedFilter(null, null, reversed));
        request.QueryString = QueryString.Empty;
        Assert.True(ProcurementHumanCsvExport.TrySelectedIds(request, out var all));
        Assert.Null(all);
        Assert.Equal("{\"search\":\"steel\",\"status\":\"Active\"}",
            ProcurementHumanCsvExport.NormalizedFilter(" steel ", "Active", null));
        Assert.NotEqual(canonical, ProcurementHumanCsvExport.NormalizedFilter(null, null, null));
    }

    [Theory]
    [InlineData("?ids=")]
    [InlineData("?ids=not-a-guid")]
    [InlineData("?ids=00000000-0000-0000-0000-000000000000")]
    [InlineData("?ids=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa,")]
    [InlineData("?ids=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa,AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
    [InlineData("?ids=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa&ids=bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb")]
    [InlineData("?ids=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa&search=")]
    [InlineData("?ids=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa&status=Active")]
    [InlineData("?ids=%20aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]
    public void Invalid_selected_export_fails_closed(string query)
    {
        var request = new DefaultHttpContext().Request;
        request.QueryString = new QueryString(query);
        Assert.False(ProcurementHumanCsvExport.ValidFilters(request));
        Assert.False(ProcurementHumanCsvExport.TrySelectedIds(request, out var ids));
        Assert.Null(ids);
    }

    [Fact]
    public void Selected_export_accepts_one_full_directory_page_but_rejects_more()
    {
        var request = new DefaultHttpContext().Request;
        var ids = Enumerable.Range(1, 101).Select(_ => Guid.NewGuid().ToString("D")).ToArray();
        request.QueryString = new QueryString("?ids=" + string.Join(',', ids.Take(100)));
        Assert.True(ProcurementHumanCsvExport.ValidFilters(request));
        request.QueryString = new QueryString("?ids=" + string.Join(',', ids));
        Assert.False(ProcurementHumanCsvExport.ValidFilters(request));
    }

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
