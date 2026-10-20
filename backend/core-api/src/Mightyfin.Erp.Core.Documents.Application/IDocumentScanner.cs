using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Application;

public sealed record DocumentScanResult(ScannerVerdict Verdict, string Sha256, string Engine,
    string? Definitions, string? FailureCode = null);

public interface IDocumentScanner
{
    Task<DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256,
        CancellationToken cancellationToken = default);
}
