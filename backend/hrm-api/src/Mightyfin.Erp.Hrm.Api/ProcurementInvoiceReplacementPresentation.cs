using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record InvoiceReplacementContext(Guid SourceInvoiceId, long SourceVersion, Guid PurchaseOrderId);

public static partial class ProcurementInvoiceIntakeRoutes
{
    internal static bool ReplacementReadActor(ProcurementRequestScope scope,
        ProcurementPrincipalPeople.Person? actor)
        => actor is not null && actor.PrincipalId == scope.SubjectId
            && scope.PrincipalId?.ToString("D") == scope.SubjectId
            && actor.Permissions.Contains("procurement-invoice-read");

    internal static bool ReplacementMakerContext(SupplierInvoice invoice, ProcurementRequestScope scope,
        HttpRequest request, ProcurementPrincipalPeople.Person? actor)
        => ReplacementReadActor(scope, actor) && IntakeWriteContextAllowed(scope, request)
            && invoice.TenantId == scope.TenantId && invoice.LegalEntityId == scope.LegalEntityId
            && invoice.CreatedBy == scope.SubjectId && invoice.BranchRef == scope.WorkLocationId
            && actor!.Permissions.Contains("procurement-invoice-capture")
            && (actor.OrganisationWide || actor.AllowedBranches.Contains(scope.WorkLocationId!.Value));

    internal static bool ReplacementSourceCandidate(SupplierInvoice invoice)
        => invoice.Id != Guid.Empty && invoice.Version > 0 && invoice.Status == "Received"
            && invoice.MatchingState == "ComparisonBlocked" && invoice.CurrentMappingVersionId is not null
            && invoice.LastMatchResultId is not null && invoice.ActiveMatchClaimResultId is null
            && invoice.FinancePostingRef is null && invoice.CreditForInvoiceRef is null;

    // This formats an already verified owning-server witness. It cannot create
    // eligibility from a client status, a header PO, or an unverified object ID.
    internal static InvoiceReplacementContext? ReplacementDisplayedContext(SupplierInvoice displayed,
        InvoiceReplacementSourceWitness witness)
        => ReplacementSourceCandidate(displayed) && ReplacementSourceCandidate(witness.Invoice)
            && displayed.Id == witness.Invoice.Id
            && displayed.Version == witness.Invoice.Version
            && displayed.TenantId == witness.Invoice.TenantId
            && displayed.LegalEntityId == witness.Invoice.LegalEntityId
            && displayed.BranchRef == witness.Invoice.BranchRef
            && displayed.SupplierId == witness.Invoice.SupplierId
            && displayed.CurrencyCode == witness.Invoice.CurrencyCode
            && displayed.CreatedBy == witness.Invoice.CreatedBy
            && witness.Mapping.SourceSchemaVersion == 3
            && witness.BlockedResult.SourceSchemaVersion == 3 && witness.BlockedResult.ExecutionVersion == 2
            && witness.BlockedResult.Outcome == "Blocked"
            && displayed.CurrentMappingVersionId == witness.Mapping.Id
            && displayed.LastMatchResultId == witness.BlockedResult.Id
            && witness.Mapping.PurchaseOrderId != Guid.Empty
            && witness.BlockedResult.PurchaseOrderId == witness.Mapping.PurchaseOrderId
            && witness.BlockedResult.IssuedVersionId == witness.Mapping.IssuedVersionId
                ? new(displayed.Id, displayed.Version, witness.Mapping.PurchaseOrderId) : null;

    internal static string? ReplacementReviewIssue(InvoiceReplacementSourceWitness witness, DateTimeOffset now)
    {
        if (InvoiceUnitReviewStorage.Read(witness.ReviewSource).SourceSchemaVersion != 4)
            return "invoice-replacement-review-mode-unsupported";
        return witness.BlockedResult.MatchedAt == default || witness.ReviewOutcome.CompletedAt == default
            || witness.BlockedResult.MatchedAt < witness.ReviewOutcome.CompletedAt
            || witness.BlockedResult.MatchedAt > now ? "invoice-replacement-review-time-invalid" : null;
    }

    private static async Task<InvoiceMappingReview?> ReadRetainedReplacementReview(ProcurementDbContext db,
        ApprovalDbContext cases, ProcurementRequestScope scope, HttpContext http, SupplierInvoice source,
        InvoiceReplacementSourceWitness witness, ProcurementPrincipalPeople.Person actor, CancellationToken ct)
    {
        var lines = await db.SupplierInvoiceLines.AsNoTracking()
            .Where(x => x.SupplierInvoiceId == source.Id && x.IsActive).OrderBy(x => x.LineNumber).ToArrayAsync(ct);
        var policy = await db.PolicyVersions.AsNoTracking()
            .SingleAsync(x => x.Id == witness.ReviewSource.MatchingPolicyId, ct);
        var context = new InvoiceMappingReviewContext(scope.TenantId, scope.LegalEntityId,
            source.BranchRef, scope.SubjectId, actor.CoreLinkedWorkerId, policy.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            witness.BlockedResult.MatchedAt, new Dictionary<Guid, string>());
        return await ProcurementInvoiceUnitReviewRoutes.RetainedReview(db, cases, scope, http,
            source, lines, witness.Mapping, context, ct, requireCurrentPublication: false);
    }

    // Retained-link visibility deliberately does not inspect either write flag or
    // CanRecordAsync. The current reader's Core permission/scope still applies.
    internal static async Task<bool> ReplacementReadCapability(ProcurementDbContext db,
        ProcurementRequestScope scope, HttpContext http, bool pending, CancellationToken ct)
    {
        var services = http.RequestServices;
        if (pending || services.GetService<IProcurementInvoiceReplacementSchemaReadiness>() is null
            || !ProcurementPrincipalInvoiceMatchingCapability.ReadContext(scope)) return false;
        var authorization = services.GetService<IAuthorizationService>();
        if (authorization is null || !(await authorization.AuthorizeAsync(http.User,
                "procurement-invoice-read")).Succeeded || !db.Database.IsNpgsql()) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await ReplacementReadSchemaReady(services, db, ct)) return false;
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (!ReplacementReadActor(scope, actor)) return false;
        try { _ = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, ct); }
        catch (UnauthorizedAccessException) { return false; }
        return true;
    }

    internal static async Task<InvoiceReplacementContext?> ReplacementPresentation(ProcurementDbContext db,
        SupplierInvoice displayed, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService authorization, CancellationToken ct)
    {
        var config = CalculationConfig(http);
        if (!ReplacementWritesEnabled(config) || !CalculationWritesEnabled(config)
            || http.RequestServices.GetService<IProcurementInvoiceReplacementSchemaReadiness>() is null
            || !ReplacementSourceCandidate(displayed)
            || !IntakeWriteContextAllowed(scope, http.Request) || displayed.CreatedBy != scope.SubjectId
            || displayed.BranchRef != scope.WorkLocationId
            || !(await authorization.AuthorizeAsync(http.User, "procurement-invoice-read")).Succeeded
            || !(await authorization.AuthorizeAsync(http.User, "procurement-invoice-capture")).Succeeded
            || !db.Database.IsNpgsql()) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await ReplacementReadSchemaReady(http.RequestServices, db, ct)
            || !await ReplacementWriteSchemaReady(http.RequestServices, db, ct)
            || !await CurrencyCalculationReady(db, config, ct, capture: true)) return null;
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (!ReplacementMakerContext(displayed, scope, http.Request, actor)) return null;
        HashSet<Guid> allowed;
        try {
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, displayed.BranchRef, true, ct);
            allowed = await ProcurementInvoiceMatchingRoutes.PrincipalMatchingBranches(core, scope, ct);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or KeyNotFoundException) { return null; }
        await using var cases = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await cases.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        // Narrow proof failures hide this fresh-attempt hint; database, provider,
        // cancellation and unrelated infrastructure failures are never swallowed.
        try
        {
            var witness = await db.ReadReplacementSourceAsync(displayed.Id,
                (requirement, token) => RetainedReplacementReference(db, scope, requirement, token), ct);
            var context = ReplacementDisplayedContext(displayed, witness);
            if (context is null || !ReplacementMakerContext(witness.Invoice, scope, http.Request, actor)
                || ReplacementReviewIssue(witness, CalculationNow(http)) is not null) return null;
            var issued = await ProcurementPrincipalInvoiceIssuedWitness.ReadForPresentationAsync(db, cases,
                scope, allowed, witness.Mapping.PurchaseOrderId, witness.Mapping.IssuedVersionId, ct);
            if (issued.SupplierId != witness.Invoice.SupplierId || issued.CurrencyCode != witness.Invoice.CurrencyCode
                || issued.BranchId != displayed.BranchRef || issued.PurchaseOrderId != context.PurchaseOrderId
                || issued.IssuedVersionId != witness.Mapping.IssuedVersionId) return null;
            var review = await ReadRetainedReplacementReview(db, cases, scope, http,
                witness.Invoice, witness, actor!, ct);
            return review is { Verified: true, State: "Verified", ManualSource: not null,
                RetainedAuthority: not null } ? context : null;
        }
        catch (InvalidOperationException) { return null; }
    }
}
