using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CoreDocumentScannerProbe <private-clamd-unix-socket>");
    return 2;
}
var scanner = new ClamdDocumentScanner(new(args[0], TimeZoneInfo.Utc,
    TimeSpan.FromSeconds(30), TimeSpan.FromHours(48)));
// Synthetic fixtures only. EICAR is the harmless antivirus test string, never an ERP file.
var fixtures = new[]
{
    ("clean", "%PDF-1.7\nSynthetic scanner verification document.\n%%EOF", ScannerVerdict.Clean),
    ("eicar", @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*", ScannerVerdict.Infected),
};
foreach (var (name, text, expected) in fixtures)
{
    var bytes = Encoding.ASCII.GetBytes(text);
    var result = await scanner.ScanAsync(new MemoryStream(bytes), bytes.Length,
        Convert.ToHexStringLower(SHA256.HashData(bytes)));
    Console.WriteLine($"{name}: {result.Verdict}; engine={result.Engine}; definitions={result.Definitions}; failure={result.FailureCode}");
    if (result.Verdict != expected) return 1;
}
return 0;
