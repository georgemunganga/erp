using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceIssuedParentReadinessTests
{
    [Fact]
    public async Task Unsupported_store_cannot_admit_B2_or_stage_any_changes()
    {
        var scope=new ProcurementRequestScope();scope.Set("b2",Guid.NewGuid(),"reader",null,null,false,"test");
        await using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,scope);
        Assert.False(await ProcurementPrincipalInvoiceIssuedParentReadiness.RequireIssuedParentsAsync(db,default));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Only_the_two_named_issued_references_can_be_generated_columns()
    {
        var table=StoreObjectIdentifier.Table("invoice_match_results","procurement");
        var ordinary=ProcurementPrincipalInvoiceIssuedParentReadiness.ColumnSql(
            table,"issued_version_id","uuid",true,null);
        var generated=ProcurementPrincipalInvoiceIssuedParentReadiness.ColumnSql(
            table,"principal_issued_version_ref_id","uuid",true,
            "CASE WHEN source_schema_version = 3 THEN issued_version_id ELSE NULL END");
        Assert.Contains("a.attgenerated=''",ordinary);
        Assert.DoesNotContain("pg_get_expr",ordinary);
        Assert.Contains("a.attgenerated='s'",generated);
        Assert.Contains("pg_get_expr(d.adbin,d.adrelid)=",generated);
        Assert.Contains("source_schema_version = 3",generated);
        Assert.DoesNotContain("a.attgenerated='s'",ordinary);
    }

    [Fact]
    public void Native_B2_capture_slots_are_specific_to_all_three_matching_tables()
    {
        Assert.Equal(new[]{"invoice_match_exposure_states","invoice_match_mapping_versions",
            "invoice_match_results"},
            ProcurementPrincipalInvoiceIssuedParentReadiness.Captured.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(3,ProcurementPrincipalInvoiceIssuedParentReadiness.Captured.Count);
    }
}
