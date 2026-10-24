using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    internal const string DocumentRetirementPermission = "procurement-invoice-document-retire";
    public sealed record RetireDocumentInput(long DocumentVersion, string Reason);
    private static Task<IResult> RetireOriginalDocument(Guid id, Guid documentId, RetireDocumentInput input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunResult(http, scope, true, async (db, cases, core, auth, documents, token) =>
        {
            var invoice = await Invoice(db, scope, id, token, true);
            var actor = await Actor(core, scope, http, auth, invoice.BranchRef, [DocumentRetirementPermission], token);
            if (!actor.Permissions.Contains("procurement-invoice-read") || !(await auth.AuthorizeAsync(http.User, null, "procurement-invoice-read")).Succeeded)
                throw new UnauthorizedAccessException();
            if (db.HasPrincipalInvoiceWorkflow(id)) throw new ReviewProblem("InvoicePrincipalWorkflowActive", 409);
            if (documents is null) throw new ReviewProblem("InvoiceUnitReviewDocumentRetentionUnavailable", 503);
            var reason = Reason(input.Reason) ?? throw new ReviewProblem("DocumentRetirementReasonRequired", 422);
            if (reason.Length > 1000 || input.DocumentVersion <= 0) throw new ReviewProblem("DocumentRetirementInputInvalid", 422);
            var owner = new DocumentOwner("procurement", "supplier_invoice", invoice.Id);
            var access = new UnusedInvoiceDocumentRetirementAccess(scope, invoice, actor.PrincipalId, documentId, input.DocumentVersion);
            var writer = new EfDocumentRetirement(documents.Owner, scope, (_, _) => Task.FromResult<IDocumentRetirementAccess>(access));
            await writer.RetireEnlistedAsync(owner, documentId, input.DocumentVersion, reason, documents.Fence, access, token);
            var saved = await documents.Owner.Documents.SingleAsync(x => x.Id == documentId, token);
            return Results.Ok(new { documentId, saved.Version, saved.IsRetired, saved.RetiredAt, canPostToFinance = false });
        }, ct, id, exclusiveDocuments: true);

    // Captured only after current Core permission and exact invoice scope above. A
    // document referenced by immutable invoice/mapping evidence cannot be retired.
    internal sealed class UnusedInvoiceDocumentRetirementAccess(ProcurementRequestScope scope, SupplierInvoice invoice,
        string principal, Guid documentId, long revision) : IDocumentRetirementAccess
    {
        public Task RequireAsync(DocumentOwner owner, Guid requestedDocument, long requestedRevision, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (scope.TenantId != invoice.TenantId || scope.LegalEntityId != invoice.LegalEntityId || scope.SubjectId != principal
                || !ProcurementPrincipalEvidence.Canonical(principal) || owner != new DocumentOwner("procurement", "supplier_invoice", invoice.Id)
                || requestedDocument != documentId || requestedRevision != revision || invoice.Status != "Received"
                || invoice.CurrentMappingVersionId is not null || invoice.FinancePostingRef is not null
                || invoice.EvidenceDocumentRefId is not null)
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }
    }
}
