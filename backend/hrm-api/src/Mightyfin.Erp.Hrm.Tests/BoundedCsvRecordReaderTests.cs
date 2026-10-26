using System.Text;
using Mightyfin.Erp.Hrm.Application.Shared;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class BoundedCsvRecordReaderTests
{
    private static readonly BoundedCsvLimits Small = new(1024, 2, 3, 32);
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static void Fails(string source, string code, int line, BoundedCsvLimits? limits = null)
    {
        var error = Assert.Throws<BoundedCsvException>(() =>
            BoundedCsvRecordReader.Parse(Bytes(source), limits ?? Small));
        Assert.Equal(code, error.Code);
        Assert.Equal(line, error.Line);
        Assert.DoesNotContain(source, error.Message);
    }

    [Fact]
    public void QuotedMultilineEscapedQuoteAndCrLfKeepPhysicalRecordLines()
    {
        var input = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(
            "Name,Code\r\n\"North, \"\"Works\"\"\r\nZambia\",NW\r\nPlain,P\n")).ToArray();
        var result = BoundedCsvRecordReader.Parse(input, new BoundedCsvLimits(1024, 2, 3, 64));

        Assert.Equal(input.Length, result.InputBytes);
        Assert.Equal(new[] { "Name", "Code" }, result.Headers);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal((2, 3), (result.Rows[0].StartLine, result.Rows[0].EndLine));
        Assert.Equal("North, \"Works\"\r\nZambia", result.Rows[0].Cells[0]);
        Assert.Equal("NW", result.Rows[0].Cells[1]);
        Assert.Equal((4, 4), (result.Rows[1].StartLine, result.Rows[1].EndLine));
    }

    [Fact]
    public async Task StreamReadIsBoundedEvenWhenLengthIsUnknown()
    {
        using var stream = new MemoryStream(Bytes("Name\n" + new string('x', 1020)));
        var error = await Assert.ThrowsAsync<BoundedCsvException>(async () =>
            await BoundedCsvRecordReader.ReadAsync(stream, Small));
        Assert.Equal("byte-limit", error.Code);
    }

    [Fact]
    public void InvalidUtf8AndIncompleteBomAreDenied()
    {
        var bad = Assert.Throws<BoundedCsvException>(() => BoundedCsvRecordReader.Parse(
            new byte[] { 0xEF, 0xBB, 0xBF, (byte)'N', 0xFF }, Small));
        Assert.Equal("invalid-utf8", bad.Code);
        bad = Assert.Throws<BoundedCsvException>(() => BoundedCsvRecordReader.Parse(
            new byte[] { 0xEF, 0xBB }, Small));
        Assert.Equal("invalid-utf8", bad.Code);
    }

    [Theory]
    [InlineData("Name,Code\n\"open,foo", "unclosed-quote", 2)]
    [InlineData("Name\nfoo\"bar", "invalid-quote", 2)]
    [InlineData("Name\n\"foo\"bar", "invalid-quote", 2)]
    [InlineData("Name\n\nfoo", "blank-record", 2)]
    [InlineData("Name,Code\na", "column-count", 2)]
    [InlineData("Name,name\na,b", "duplicate-header", 1)]
    [InlineData("Name, \na,b", "empty-header", 1)]
    public void MalformedRecordsFailWithStableCodeAndLine(string source, string code, int line) =>
        Fails(source, code, line);

    [Fact]
    public void RowColumnAndUtf8CellByteLimitsAreIndependent()
    {
        Fails("Name\na\nb\nc", "row-limit", 4);
        Fails("A,B,C,D\n1,2,3,4", "column-limit", 1);
        Fails("Name\n🇿🇲", "cell-limit", 2, new BoundedCsvLimits(1024, 2, 3, 4));
        Fails("Name\n" + new string('x', 33), "cell-limit", 2);
    }

    [Fact]
    public void MappingRejectsUnknownSourceTargetDuplicateTargetAndMissingRequired()
    {
        var document = BoundedCsvRecordReader.Parse(Bytes("Name,Code,Notes\nA,01,ok"), Small);
        var allowed = new[] { "name", "code" };
        var required = new[] { "name", "code" };

        void Error(Dictionary<string, string> map, string expected)
        {
            var error = Assert.Throws<BoundedCsvException>(() =>
                BoundedCsvRecordReader.ValidateMapping(document, map, allowed, required));
            Assert.Equal(expected, error.Code);
        }

        Error(new() { ["Wrong"] = "name" }, "mapping-unknown-header");
        Error(new() { ["Name"] = "name", ["Code"] = "price" }, "mapping-unknown-target");
        Error(new() { ["Name"] = "name", ["Code"] = "name" }, "mapping-duplicate-target");
        Error(new() { ["Name"] = "name" }, "mapping-required-target");
        var valid = new Dictionary<string, string> { ["Name"] = "name", ["Code"] = "code", ["Notes"] = "" };
        Assert.Same(valid, BoundedCsvRecordReader.ValidateMapping(document, valid, allowed, required));
    }

    [Fact]
    public void UnsupportedLimitConfigurationIsDeniedBeforeReading()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedCsvRecordReader.Parse(
            Bytes("Name\nA"), new BoundedCsvLimits(int.MaxValue, 1, 1, 1)));
    }
}
