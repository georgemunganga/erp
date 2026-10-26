using System.Text;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementMasterImportContractTests
{
    [Fact]
    public void Native_and_bulk_vendor_builder_uses_unique_16_hex_numbers_at_max_batch_size()
    {
        var actor = Guid.NewGuid();
        var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", Guid.NewGuid(), actor.ToString("D"), null, null, false, "test");
        scope.AssignPrincipal(actor);
        var mapped = new SortedDictionary<string, string>(StringComparer.Ordinal)
        { ["legalName"] = "Example supplier", ["displayName"] = "Example supplier",
            ["countryCode"] = "ZM", ["registrationNumber"] = "" };
        var input = ProcurementMasterDraftRules.ImportVendor(mapped, "ZMW");
        Assert.Null(ProcurementMasterDraftRules.ValidateVendor(input));
        var vendors = Enumerable.Range(0, 1000)
            .Select(_ => ProcurementMasterDraftRules.NewVendor(input, scope)).ToArray();
        Assert.Equal(1000, vendors.Select(x => x.Number).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1000, vendors.Select(x => x.Id).Distinct().Count());
        Assert.All(vendors, vendor =>
        {
            Assert.Matches("^VEN-[0-9A-F]{16}$", vendor.Number);
            Assert.Equal('7', vendor.Id.ToString("N")[12]);
            Assert.Equal("Draft", vendor.Status);
            Assert.False(vendor.IsOrderable);
        });
    }

    [Fact]
    public void Preview_validates_trimmed_headers_explicit_mapping_and_native_draft_shape()
    {
        var csv = Encoding.UTF8.GetBytes(" code ,name,kind,categoryCode,unitCode\r\n"
            + " sku-1 ,\"Bolt, zinc\",Goods,FAST,EA\r\n");
        var request = new MasterImportPreviewRequest(1, "items.csv", Convert.ToBase64String(csv),
            new Dictionary<string, string> { ["code"] = "code", ["name"] = "name",
                ["kind"] = "kind", ["categoryCode"] = "categoryCode", ["unitCode"] = "unitCode" },
            "reject", []);
        var (document, original, code) = ProcurementMasterImportContracts.Parse(request, "Item");
        Assert.Null(code);
        Assert.Equal(csv, original);
        var row = Assert.Single(document!.Rows);
        Assert.Equal((2, 2), (row.StartLine, row.EndLine));
        var mapped = ProcurementMasterImportContracts.MapRow(document, row, request.Mapping);
        var draft = ProcurementMasterDraftRules.ImportItem(mapped);
        Assert.Null(ProcurementMasterDraftRules.ValidateItem(draft));
        var item = ProcurementMasterDraftRules.NewItem(draft);
        Assert.Equal("SKU-1", item.Code);
        Assert.Equal("Inactive", item.Status);
        Assert.False(item.IsPurchasable);
    }

    [Fact]
    public void Preview_rejects_null_mapping_target_before_row_access_and_unacknowledged_extra_mapping()
    {
        var csv = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            "legalName,displayName,countryCode,notes\nA,A,ZM,x\n"));
        var mapping = new Dictionary<string, string> { ["legalName"] = "legalName",
            ["displayName"] = "displayName", ["countryCode"] = "countryCode", ["notes"] = null! };
        var request = new MasterImportPreviewRequest(1, "vendors.csv", csv, mapping, "reject", []);
        Assert.Equal("validation-failed", ProcurementMasterImportContracts.Parse(request, "Vendor").Code);
        Assert.Null(ProcurementMasterImportContracts.ReadSnapshot("{}"));
        Assert.Null(ProcurementMasterImportContracts.ReadSnapshot("{\"rows\":[null]}"));
        mapping["notes"] = "";
        Assert.Null(ProcurementMasterImportContracts.Parse(request, "Vendor").Code);
        mapping["notes"] = "not-an-import-field";
        Assert.Equal("validation-failed", ProcurementMasterImportContracts.Parse(request, "Vendor").Code);
    }

    [Fact]
    public void Natural_duplicates_skip_only_under_skip_policy_and_name_similarity_needs_current_acknowledgement()
    {
        static ProcurementMasterImportRoutes.MappedRow Vendor(int number, string name, string registration) =>
            new(number, number, number, new SortedDictionary<string, string>(StringComparer.Ordinal)
            { ["legalName"] = name, ["displayName"] = name, ["countryCode"] = "ZM",
                ["registrationNumber"] = registration });
        var existing = Guid.NewGuid();
        var candidates = new ProcurementMasterImportRoutes.CandidateSet(
            [new(existing, "ZM", "REG001", "Existing", "Existing")], []);
        var source = new[] { Vendor(2, "First", "REG-001"), Vendor(3, "Existing", "REG-002") };
        var reject = ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates(
            "Vendor", source, "reject", [], "ZMW", candidates);
        Assert.Equal(new[] { "error", "error" }, reject.Select(x => x.Disposition));
        Assert.Equal(new[] { "duplicate-master", "similar-vendor" }, reject.Select(x => x.Code));
        var acknowledged = ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates(
            "Vendor", source, "skip", [3], "ZMW", candidates);
        Assert.Equal(new[] { "skip", "create" }, acknowledged.Select(x => x.Disposition));
        Assert.Throws<InvalidOperationException>(() =>
            ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates("Vendor", source, "skip", [2], "ZMW", candidates));
        var changed = new ProcurementMasterImportRoutes.CandidateSet(
            [new(existing, "ZM", "REG001", "Changed", "Changed")], []);
        Assert.Throws<InvalidOperationException>(() =>
            ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates("Vendor", source, "skip", [3], "ZMW", changed));
    }

    [Fact]
    public void Same_file_vendor_similarity_checks_legal_and_display_names_independently()
    {
        static ProcurementMasterImportRoutes.MappedRow Row(int number, string legal, string display) =>
            new(number, number, number, new SortedDictionary<string, string>(StringComparer.Ordinal)
            { ["legalName"] = legal, ["displayName"] = display,
                ["countryCode"] = "ZM", ["registrationNumber"] = "" });
        var source = new[] { Row(2, "Legal A", "Display A"), Row(3, "Legal A", "Display B"),
            Row(4, "Legal C", "Display A") };
        var candidates = new ProcurementMasterImportRoutes.CandidateSet([], []);
        var unacknowledged = ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates(
            "Vendor", source, "reject", [], "ZMW", candidates);
        Assert.Equal(new[] { "create", "error", "error" }, unacknowledged.Select(x => x.Disposition));
        Assert.Equal("similar-vendor", unacknowledged[1].Code);
        Assert.Equal("similar-vendor", unacknowledged[2].Code);
        var acknowledged = ProcurementMasterImportRoutes.EvaluateRowsAgainstCandidates(
            "Vendor", source, "reject", [3, 4], "ZMW", candidates);
        Assert.All(acknowledged, row => Assert.Equal("create", row.Disposition));
    }

    [Fact]
    public async Task Known_length_and_chunked_oversized_json_are_rejected_before_deserialization()
    {
        var known = new DefaultHttpContext();
        known.Request.ContentType = "application/json";
        known.Request.ContentLength = ProcurementMasterImportContracts.MaxJsonBytes + 1;
        known.Request.Body = Stream.Null;
        var (_, knownError) = await ProcurementMasterImportRoutes.ReadJson<MasterImportPreviewRequest>(known,
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, ((IStatusCodeHttpResult)knownError!).StatusCode);

        var chunked = new DefaultHttpContext();
        chunked.Request.ContentType = "application/json";
        chunked.Request.Body = new MemoryStream(new byte[ProcurementMasterImportContracts.MaxJsonBytes + 1]);
        var (_, chunkedError) = await ProcurementMasterImportRoutes.ReadJson<MasterImportPreviewRequest>(chunked,
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, ((IStatusCodeHttpResult)chunkedError!).StatusCode);
    }

    [Fact]
    public void Company_master_All_admits_only_reviewed_import_subroutes_and_methods()
    {
        foreach (var kind in new[] { "vendors", "items" })
        {
            var root = $"/api/procurement/v1/{kind}/import";
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/schema", "GET"));
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/SCHEMA", "GET"));
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/template", "GET"));
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/" + Guid.NewGuid(), "GET"));
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/preview", "POST"));
            Assert.True(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/commit", "POST"));
            Assert.False(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/commit", "GET"));
            Assert.False(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/preview/other", "POST"));
            Assert.False(ProcurementHostIntegration.AllCompanyMasterImportPath(root + "/other", "POST"));
        }
    }
}
