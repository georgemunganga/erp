using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqCommercialReviewDocumentsTests
{
    private sealed class FinderOnly : IDocumentRecords
    {
        public int FindCalls { get; private set; }
        public int BroadListCalls { get; private set; }
        public Task<RegisteredDocument?> FindAsync(DocumentOwner owner, Guid versionId,
            CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult<RegisteredDocument?>(null);
        }
        public Task<IReadOnlyList<RegisteredDocument>> ListAsync(DocumentOwner owner,
            CancellationToken ct)
        {
            BroadListCalls++;
            throw new InvalidOperationException("Broad owner enumeration is forbidden.");
        }
        public Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document,
            CoreDocumentVersion version, int maxFiles, CancellationToken ct)
            => throw new UnauthorizedAccessException();
        public Task SaveStateAsync(RegisteredDocument record, string action,
            DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct)
            => throw new UnauthorizedAccessException();
    }

    [Fact]
    public async Task Assigned_purpose_enumerates_only_pinned_versions_and_never_calls_broad_owner_list()
    {
        var ev = Guid.NewGuid();
        var version = Guid.NewGuid();
        var inner = new FinderOnly();
        var records = new ProcurementSourcingExecutionRoutes.CommercialPinnedRecords(
            inner, ev, new Dictionary<Guid, string> { [version] = new string('a', 64) });
        var owner = new DocumentOwner("procurement", "sourcing_event", ev);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            records.ListAsync(owner, CancellationToken.None));
        Assert.Equal(1, inner.FindCalls);
        Assert.Equal(0, inner.BroadListCalls);
        Assert.Null(await records.FindAsync(owner, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(1, inner.FindCalls);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            records.ListAsync(new("procurement", "sourcing_event", Guid.NewGuid()),
                CancellationToken.None));
        Assert.Equal(0, inner.BroadListCalls);
    }
}
