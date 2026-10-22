using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMatchReadProjectionTests
{
    [Fact]
    public void Ordinary_invoice_read_never_exports_other_invoice_history_or_unrelated_PO_lines()
    {
        var own=Guid.NewGuid(); var other=Guid.NewGuid(); var hiddenInvoice=Guid.NewGuid();
        var evidence=JsonSerializer.Serialize(new { schemaVersion=1,discrepancies=new[] { new InvoiceMatchDiscrepancy(own,"OwnDifference"),new(other,"ForeignDifference") },
            lines=new[] { new InvoiceMatchLineComparison(own,Guid.NewGuid(),null,InvoiceMatchingMethod.TwoWay,"1",".00010001","0",".00010001"),
                new(other,Guid.NewGuid(),null,InvoiceMatchingMethod.TwoWay,"999","999","999","999") },
            source=new { consumption=new { allocations=new[] { new { invoiceId=hiddenInvoice,validationReference="PRIVATE-OTHER-INVOICE" } } },
                order=new { secret="UNRELATED-COMMERCIAL-TERMS" } } },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result=new InvoiceMatchResult { ExecutionVersion=1,Outcome="Blocked",EvidenceJson=evidence,EvidenceSha256=ProcurementEvidenceEncoding.Digest(evidence),SourceWatermarksJson="{}" };
        var summary=InvoiceMatchReadProjection.Summary(result,new HashSet<Guid> { own });
        Assert.Equal(own,Assert.Single(summary.Lines).InvoiceLineId); Assert.Equal(".00010001",summary.Lines[0].NetAmount);
        Assert.Equal("OwnDifference",Assert.Single(summary.Discrepancies).Code);
        var json=JsonSerializer.Serialize(summary);
        Assert.DoesNotContain(other.ToString("D"),json); Assert.DoesNotContain(hiddenInvoice.ToString("D"),json);
        Assert.DoesNotContain("PRIVATE-OTHER-INVOICE",json); Assert.DoesNotContain("UNRELATED-COMMERCIAL-TERMS",json);
    }
    [Fact]
    public void Corrupted_comparison_evidence_is_not_silently_projected()
        =>Assert.Throws<InvalidOperationException>(()=>InvoiceMatchReadProjection.Summary(new InvoiceMatchResult
            { ExecutionVersion=1,Outcome="Compared",EvidenceJson="{}",EvidenceSha256="changed" },new HashSet<Guid>()));
}
