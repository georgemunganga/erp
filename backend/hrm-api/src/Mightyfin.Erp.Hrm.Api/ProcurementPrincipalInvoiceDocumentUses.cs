using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Purpose-only Core document use for a retained principal original-unit
/// review. The same Core owner service/fence as P22 owns the pins and use set;
/// Procurement only binds those rows to its already verified review source.</summary>
internal static class ProcurementPrincipalInvoiceDocumentUses
{
    private static DocumentOwner Owner(PrincipalInvoiceUnitReviewSource source)
        => new("procurement", "supplier_invoice", source.SupplierInvoiceId);

    private static DocumentUseReference Reference(PrincipalInvoiceUnitReviewSource source)
        => new(source.Id, "procurement", "principal_invoice_unit_review", source.Id, 1,
            DocumentUseKind.Review);

    private sealed class Access(ProcurementRequestScope scope, PrincipalInvoiceUnitReviewSource source,
        IReadOnlyDictionary<Guid, string> hashes, bool acquire) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference reference,
            DocumentUseOperation operation, IReadOnlyList<Guid> versionIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.TenantId != source.TenantId || scope.LegalEntityId != source.LegalEntityId
                || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
                || owner != Owner(source) || reference != Reference(source)
                || versionIds.Distinct().Count() != versionIds.Count
                || versionIds.Count != 0 && !versionIds.Order().SequenceEqual(hashes.Keys.Order())
                || operation == DocumentUseOperation.Acquire
                    && (!acquire || scope.SubjectId != source.MakerPrincipalId || versionIds.Count == 0)
                || operation is not (DocumentUseOperation.Acquire or DocumentUseOperation.Read))
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }

    internal static async Task RetainAsync(DocumentDbContext documents,
        DocumentOwnerLifecycleFence fence, ProcurementRequestScope scope,
        PrincipalInvoiceUnitReviewSource source,
        PrincipalInvoiceUnitReviewTransitions.OpeningEvidence opening,
        IReadOnlyDictionary<Guid, string> currentHashes, CancellationToken ct)
    {
        var expected = opening.Lines.GroupBy(x => x.DocumentVersionId)
            .ToDictionary(x => x.Key, x => x.First().DocumentSha256);
        if (source.SupplierInvoiceId != opening.InvoiceId
            || source.MakerPrincipalId != scope.SubjectId
            || expected.Count is < 1 or > 100 || expected.Count != currentHashes.Count
            || expected.Any(x => !currentHashes.TryGetValue(x.Key, out var hash) || hash != x.Value)
            || opening.Lines.Any(x => expected[x.DocumentVersionId] != x.DocumentSha256))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
        var owner = Owner(source);
        var rows = await (from version in documents.Versions
            join document in documents.Documents on version.DocumentId equals document.Id
            where expected.Keys.Contains(version.Id) && document.OwnerModule == owner.Module
                && document.OwnerType == owner.Type && document.OwnerId == owner.Id
                && !document.IsRetired && version.ScanState == DocumentScanState.Available
            select new { document, version }).ToArrayAsync(ct);
        if (rows.Length != expected.Count || rows.Any(x => x.version.Sha256 != expected[x.version.Id]))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
        var selections = rows.Select(x => new DocumentUseSelection(owner.Module, owner.Type,
            owner.Id, x.document.Id, x.version.Id, x.document.Version, x.version.Version,
            expected[x.version.Id])).ToArray();
        var store = await EfDocumentUsePinStore.CreateAsync(documents, scope,
            new Access(scope, source, expected, true), fence, [owner], ct);
        _ = await store.AcquireAsync(owner, Reference(source), selections, ct);
        // Still inside the caller's serializable Core/Procurement transaction.
        await documents.SaveChangesAsync(ct);
    }

    internal static async Task RequireAsync(ProcurementDbContext transaction,
        ProcurementRequestScope scope, PrincipalInvoiceUnitReviewSource source,
        PrincipalInvoiceUnitReviewTransitions.OpeningEvidence opening, CancellationToken ct)
    {
        var expected = opening.Lines.GroupBy(x => x.DocumentVersionId)
            .ToDictionary(x => x.Key, x => x.First().DocumentSha256);
        if (expected.Count is < 1 or > 100 || source.SupplierInvoiceId != opening.InvoiceId
            || opening.Lines.Any(x => expected[x.DocumentVersionId] != x.DocumentSha256))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentChanged");
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(transaction.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(
            transaction.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var pins = await EfDocumentUsePinStore.ReadRetainedAsync(documents, scope,
            new Access(scope, source, expected, false), Owner(source), Reference(source), ct);
        if (pins.Count != expected.Count || pins.Any(x => !expected.TryGetValue(x.VersionId,
                out var sha) || x.Sha256 != sha))
            throw new InvalidOperationException("PrincipalInvoiceOriginalDocumentUseUnavailable");
    }
}
