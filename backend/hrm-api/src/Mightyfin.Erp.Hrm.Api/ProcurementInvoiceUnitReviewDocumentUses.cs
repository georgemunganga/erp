using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static DocumentOwner ReviewDocumentOwner(InvoiceUnitReviewSource source) => new("procurement", "supplier_invoice", source.SupplierInvoiceId);
    private static DocumentUseReference ReviewDocumentReference(InvoiceUnitReviewSource source) => new(source.Id,
        "procurement", "invoice_unit_review", source.Id, 1, DocumentUseKind.Review);

    // Constructed only after the route's current canonical actor/action/record proof.
    // The exact immutable source constrains this adapter; it cannot authorize another
    // invoice, arbitrary versions, publication or retirement.
    internal sealed class ReviewDocumentUseAccess(ProcurementRequestScope scope, InvoiceUnitReviewSource source,
        string principal, bool mayAcquire) : IDocumentUseAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentUseReference reference, DocumentUseOperation operation,
            IReadOnlyList<Guid> versionIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var typed = InvoiceUnitReviewStorage.Read(source);
            var expected = typed.Lines.Select(x => x.DocumentVersionId).Distinct().Order().ToArray();
            if (scope.TenantId != source.TenantId || scope.LegalEntityId != source.LegalEntityId || scope.SubjectId != principal
                || !ProcurementPrincipalEvidence.Canonical(principal) || owner != ReviewDocumentOwner(source)
                || reference != ReviewDocumentReference(source) || versionIds.Distinct().Count() != versionIds.Count
                || (versionIds.Count != 0 && !versionIds.Order().SequenceEqual(expected))
                || operation == DocumentUseOperation.Acquire && (!mayAcquire || principal != source.MakerPrincipalId || versionIds.Count == 0)
                || operation is not (DocumentUseOperation.Read or DocumentUseOperation.Acquire))
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }

    private static async Task RetainOriginalDocuments(UnitReviewDocumentSession session, ProcurementRequestScope scope,
        InvoiceUnitReviewSource source, SupplierInvoice invoice, InvoiceUnitReviewPolicyRules.Person actor,
        IReadOnlyDictionary<Guid, string> hashes, CancellationToken ct)
    {
        if (source.SupplierInvoiceId != invoice.Id || source.MakerPrincipalId != actor.PrincipalId
            || invoice.CurrentMappingVersionId != source.MappingVersionId || invoice.MappingRevision != source.MappingRevision)
            throw new UnauthorizedAccessException();
        var owner = ReviewDocumentOwner(source); var expected = InvoiceUnitReviewStorage.Read(source).Lines
            .GroupBy(x => x.DocumentVersionId).ToDictionary(x => x.Key, x => x.First().DocumentSha256);
        if (expected.Count is < 1 or > 100 || expected.Count != hashes.Count || expected.Any(x => !hashes.TryGetValue(x.Key, out var hash) || hash != x.Value))
            throw new ReviewProblem("InvoiceOriginalDocumentChanged", 409);
        var rows = await (from version in session.Owner.Versions join document in session.Owner.Documents on version.DocumentId equals document.Id
            where expected.Keys.Contains(version.Id) && document.OwnerModule == owner.Module && document.OwnerType == owner.Type && document.OwnerId == owner.Id
            select new { document, version }).ToArrayAsync(ct);
        if (rows.Length != expected.Count) throw new ReviewProblem("InvoiceOriginalDocumentChanged", 409);
        var selections = rows.Select(x => new DocumentUseSelection(owner.Module, owner.Type, owner.Id, x.document.Id,
            x.version.Id, x.document.Version, x.version.Version, expected[x.version.Id])).ToArray();
        var store = await EfDocumentUsePinStore.CreateAsync(session.Owner, scope,
            new ReviewDocumentUseAccess(scope, source, actor.PrincipalId, true), session.Fence, [owner], ct);
        _ = await store.AcquireAsync(owner, ReviewDocumentReference(source), selections, ct);
        // Save inside the SAME caller transaction. An ensuing Core/Procurement failure
        // rolls these rows back too; the coordinator alone commits all three schemas.
        await session.Owner.SaveChangesAsync(ct);
    }

    private static async Task RequireRetainedOriginalDocuments(ProcurementDbContext transaction, ProcurementRequestScope scope,
        InvoiceUnitReviewSource source, CancellationToken ct)
    {
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(transaction.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(transaction.Database.CurrentTransaction!.GetDbTransaction(), ct);
        var pins = await EfDocumentUsePinStore.ReadRetainedAsync(documents, scope,
            new ReviewDocumentUseAccess(scope, source, scope.SubjectId, false), ReviewDocumentOwner(source), ReviewDocumentReference(source), ct);
        var expected = InvoiceUnitReviewStorage.Read(source).Lines.GroupBy(x => x.DocumentVersionId)
            .ToDictionary(x => x.Key, x => x.First().DocumentSha256);
        if (pins.Count != expected.Count || pins.Any(x => !expected.TryGetValue(x.VersionId, out var hash) || x.Sha256 != hash))
            throw new ReviewProblem("InvoiceUnitReviewDocumentRetentionUnavailable", 409);
    }
}
