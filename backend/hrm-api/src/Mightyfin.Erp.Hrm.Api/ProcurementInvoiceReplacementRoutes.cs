using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record InvoiceReplacementInput(long SourceVersion, string? Reason,
    InvoiceIntakeInput? CorrectedInvoice);

public static partial class ProcurementInvoiceIntakeRoutes
{
    private static bool ReplacementWritesEnabled(IConfiguration config)
        => config.GetValue<bool>("Procurement:InvoiceReplacement:Enabled");

    internal static bool ExplicitReplacementCurrency(string? currency)
        => !string.IsNullOrWhiteSpace(currency);

    internal static bool SameReplacementCurrency(string sourceCurrency, string correctedCurrency)
        => correctedCurrency.Trim().ToUpperInvariant() == sourceCurrency;

    private static async Task<bool> ReplacementReadSchemaReady(HttpRequest request, CancellationToken ct)
        => request.HttpContext.RequestServices.GetService<IProcurementInvoiceReplacementSchemaReadiness>()
            is { } readiness && await readiness.IsReadyAsync(ct);

    private static IResult ReplacementSchemaUnavailable() => Results.Json(new {
        code = "invoice-replacement-schema-unavailable",
        message = "Invoice replacement history is not available in this database."
    }, statusCode: 503);

    private static string ReplacementRequestDigest(Guid sourceId, InvoiceReplacementInput input)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new {
            operation = InvoiceReplacementEvidence.Operation, sourceId, input.SourceVersion,
            reason = input.Reason?.Trim(), correctedInvoice = input.CorrectedInvoice is null
                ? null : NormalizedCaptureInput(input.CorrectedInvoice),
            expectedCurrencyReferenceId = input.CorrectedInvoice?.ExpectedCurrencyReferenceId,
            expectedCurrencyRevision = input.CorrectedInvoice?.ExpectedCurrencyRevision,
            expectedCalculationSha256 = input.CorrectedInvoice?.ExpectedCalculationSha256
        }, CalculationJson));

    internal static async Task<IResult> ReplaceCurrencyInvoice(Guid id, InvoiceReplacementInput input,
        HttpRequest request, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!IntakeWriteContextAllowed(scope, request)) return IntakeBranchRequired();
        if (id == Guid.Empty || input.CorrectedInvoice is null || input.SourceVersion <= 0
            || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Invalid("Supply the source version, corrected invoice and replacement reason.");
        if (!ExplicitReplacementCurrency(input.CorrectedInvoice.CurrencyCode))
            return Invalid("Enter the original invoice's currency explicitly on the corrected invoice.");
        if (!Guid.TryParse(request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
            return Invalid("A saved replacement command reference is required.");
        var requestSha = ReplacementRequestDigest(id, input);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || actor.PrincipalId != scope.SubjectId
            || !actor.Permissions.Contains("procurement-invoice-read")
            || !actor.Permissions.Contains("procurement-invoice-capture")
            || scope.WorkLocationId is not { } branch || branch == Guid.Empty
            || !actor.OrganisationWide && !actor.AllowedBranches.Contains(branch))
            return Results.Forbid();
        Guid[] allowedBranches;
        try { allowedBranches = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, ct); }
        catch (Exception e) when (e is UnauthorizedAccessException or KeyNotFoundException)
        { return Results.NotFound(); }
        var source = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (source is null || source.BranchRef != branch || source.CreatedBy != scope.SubjectId)
            return Missing();
        if (!await ReplacementReadSchemaReady(request, ct)) return ReplacementSchemaUnavailable();

        // The original actor and source are checked before key lookup. Saved bytes
        // remain recoverable after a policy or Core precision is retired.
        var prior = await db.InvoiceReplacementCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, ct);
        if (prior is not null)
        {
            if (prior.SourceInvoiceId != id || prior.RequestSha256 != requestSha)
                return Results.Conflict(new { code = "invoice-replacement-key-conflict",
                    message = "This replacement reference belongs to different details." });
            return await SavedCurrencyReplacement(prior, source, db, scope, ct);
        }
        var config = CalculationConfig(request.HttpContext);
        if (!ReplacementWritesEnabled(config) || !CalculationWritesEnabled(config)
            || !await CurrencyCalculationReady(db, config, ct, capture: true))
            return CalculationUnavailable();
        if (input.CorrectedInvoice.IntakeSchemaVersion != 2
            || input.CorrectedInvoice.ExpectedCurrencyReferenceId is null
            || input.CorrectedInvoice.ExpectedCurrencyRevision is null
            || input.CorrectedInvoice.ExpectedCalculationSha256 is not { Length: 64 })
            return Invalid("Review the corrected invoice's currency calculation before replacing the original.");
        var issue = Validate(input.CorrectedInvoice, allowDefaultCurrency: false);
        if (issue is not null) return Invalid(issue);
        if (source.Version != input.SourceVersion) return StaleInvoice();
        if (source.BranchRef != branch || source.SupplierId != input.CorrectedInvoice.SupplierId
            || !SameReplacementCurrency(source.CurrencyCode, input.CorrectedInvoice.CurrencyCode!))
            return Results.Conflict(new { code = "invoice-replacement-scope-changed",
                message = "The corrected invoice must retain the vendor, currency and branch." });
        var already = await db.InvoiceReplacementLinks.AsNoTracking()
            .AnyAsync(x => x.SourceInvoiceId == id, ct);
        if (already) return Results.Conflict(new { code = "invoice-already-replaced",
            message = "This invoice already has a recorded replacement." });

        // New work follows the established PR -> PO -> exposure -> invoice lock
        // order. The source pointer is only a prelock hint; it is checked again
        // against the locked invoice before any successor is staged.
        if (source.CurrentMappingVersionId is not { } preMappingId
            || source.LastMatchResultId is not { } preResultId)
            return Results.Conflict(new { code = "invoice-replacement-source-not-closed",
                message = "The original invoice's comparison needs review before replacement." });
        var preMapping = await db.InvoiceMatchMappingVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == preMappingId && x.SupplierInvoiceId == id, ct);
        var preResult = await db.InvoiceMatchResults.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == preResultId && x.SupplierInvoiceId == id, ct);
        if (preMapping is not { SourceSchemaVersion: 3 }
            || preResult is not { SourceSchemaVersion: 3, ExecutionVersion: 2, Outcome: "Blocked" }
            || preResult.MappingVersionId != preMapping.Id
            || preResult.PurchaseOrderId != preMapping.PurchaseOrderId
            || preResult.IssuedVersionId != preMapping.IssuedVersionId)
            return Results.Conflict(new { code = "invoice-replacement-source-not-closed",
                message = "The original invoice's comparison needs review before replacement." });
        await using var cases = new ApprovalDbContext(
            new DbContextOptionsBuilder<ApprovalDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await cases.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        PrincipalInvoiceIssuedWitness issued;
        try { issued = await ProcurementPrincipalInvoiceIssuedWitness.LoadAsync(db, cases,
            scope, allowedBranches.ToHashSet(), preMapping.PurchaseOrderId,
            preMapping.IssuedVersionId, ct); }
        catch (InvalidOperationException)
        { return Results.Conflict(new { code = "invoice-replacement-issued-owner-unavailable",
            message = "The purchase order's retained issued authority needs review before replacement." }); }
        if (issued.SupplierId != source.SupplierId || issued.CurrencyCode != source.CurrencyCode
            || issued.BranchId != branch || issued.PurchaseOrderId != preMapping.PurchaseOrderId
            || issued.IssuedVersionId != preMapping.IssuedVersionId)
            return Results.Conflict(new { code = "invoice-replacement-issued-owner-changed",
                message = "The original purchase order authority changed." });
        var preExposure = await db.InvoiceMatchExposureStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseOrderId == preMapping.PurchaseOrderId
                && x.IssuedVersionId == preMapping.IssuedVersionId, ct);
        if (preExposure is not null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.invoice_match_exposure_states WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={preExposure.Id} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
        source = await db.SupplierInvoices.SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId, ct);
        if (source is null) return Missing();
        await db.Entry(source).ReloadAsync(ct);
        if (db.Entry(source).State == EntityState.Detached || source.TenantId != scope.TenantId
            || source.LegalEntityId != scope.LegalEntityId || source.BranchRef != branch
            || source.CreatedBy != scope.SubjectId)
            return Missing();
        if (source.Version != input.SourceVersion) return StaleInvoice();
        if (source.CurrentMappingVersionId != preMapping.Id || source.LastMatchResultId != preResult.Id)
            return Results.Conflict(new { code = "invoice-replacement-source-changed",
                message = "The original mapping or comparison changed. Refresh it before replacement." });
        InvoiceReplacementSourceWitness witness;
        try { witness = await db.ReadReplacementSourceAsync(id,
            (requirement, token) => RetainedReplacementReference(db, scope, requirement, token), ct); }
        catch (InvalidOperationException)
        { return Results.Conflict(new { code = "invoice-replacement-source-not-closed",
            message = "The original invoice's comparison and financial effect must be reviewed before replacement." }); }
        if (witness.BlockedResult.PurchaseOrderId is not { } orderId
            || witness.BlockedResult.IssuedVersionId is not { } issuedId
            || input.CorrectedInvoice.PurchaseOrderId != orderId
            || source.PurchaseOrderId is { } sourceOrder && sourceOrder != orderId)
            return Results.Conflict(new { code = "invoice-replacement-order-changed",
                message = "The corrected invoice must use the original authoritative purchase order." });
        if (InvoiceUnitReviewStorage.Read(witness.ReviewSource).SourceSchemaVersion != 4)
            return Results.Conflict(new { code = "invoice-replacement-review-mode-unsupported",
                message = "This original review needs a separate retained evidence path before replacement." });
        if (witness.BlockedResult.MatchedAt == default
            || witness.ReviewOutcome.CompletedAt == default
            || witness.BlockedResult.MatchedAt < witness.ReviewOutcome.CompletedAt
            || witness.BlockedResult.MatchedAt > CalculationNow(request.HttpContext))
            return Results.Conflict(new { code = "invoice-replacement-review-time-invalid",
                message = "The original comparison and review time cannot be reconciled." });
        // A stored Verified label is not Core approval authority. Reconstruct the
        // terminal review in the same transaction before admitting replacement.
        var sourceLines = await db.SupplierInvoiceLines.AsNoTracking()
            .Where(x => x.SupplierInvoiceId == id && x.IsActive)
            .OrderBy(x => x.LineNumber).ToArrayAsync(ct);
        var policy = await db.PolicyVersions.AsNoTracking()
            .SingleAsync(x => x.Id == witness.ReviewSource.MatchingPolicyId, ct);
        var reviewContext = new InvoiceMappingReviewContext(scope.TenantId, scope.LegalEntityId,
            source.BranchRef, scope.SubjectId, actor.CoreLinkedWorkerId, policy.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            witness.BlockedResult.MatchedAt, new Dictionary<Guid, string>());
        InvoiceMappingReview? retained;
        try { retained = await ProcurementInvoiceUnitReviewRoutes.RetainedReview(db, cases,
            scope, request.HttpContext, source, sourceLines, witness.Mapping, reviewContext, ct,
            requireCurrentPublication: false); }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (InvalidOperationException)
        { return Results.Conflict(new { code = "invoice-replacement-review-unavailable",
            message = "The original invoice review must remain independently verified." }); }
        if (retained is not { Verified: true, State: "Verified", ManualSource: not null,
                RetainedAuthority: not null })
            return Results.Conflict(new { code = "invoice-replacement-review-unavailable",
                message = "The original invoice review must remain independently verified." });
        try
        {
        var precision = await ResolveCalculationPrecision(db, scope,
            input.CorrectedInvoice.CurrencyCode, CalculationNow(request.HttpContext), ct);
        var corrected = input.CorrectedInvoice with { CurrencyCode = precision.CurrencyCode };
        if (corrected.ExpectedCurrencyReferenceId != precision.ReferenceId
            || corrected.ExpectedCurrencyRevision != precision.Revision
            || corrected.ExpectedCalculationSha256 != PreviewDigest(corrected, precision))
            return Results.Conflict(new { code = "invoice-calculation-changed",
                message = "Review the updated calculation before saving the corrected invoice." });
        var amounts = CalculateInput(corrected, precision);
        var captureKey = InvoiceReplacementEvidence.CaptureCommandKey(scope.TenantId,
            scope.LegalEntityId, id, key);
        if (await db.InvoiceIntakeCommandReceipts.AsNoTracking().AnyAsync(x =>
                x.ActorPrincipalId == scope.SubjectId && x.CommandKey == captureKey, ct))
            throw new InvalidOperationException("InvoiceReplacementCaptureKeyAlreadyUsed");
        var at = CalculationNow(request.HttpContext);
        var staged = await StageCurrencyCaptureAsync(corrected, db, core, scope, precision,
            amounts, captureKey, CaptureRequestDigest(corrected), at, ct);
        var link = new InvoiceReplacementLink {
            SourceInvoiceId = id, SuccessorInvoiceId = staged.Invoice.Id,
            SourceInvoiceVersion = source.Version,
            SourceOriginalSha256 = witness.Mapping.InvoiceOriginalSha256,
            SourceMappingVersionId = witness.Mapping.Id,
            SourceMappingRevision = witness.Mapping.MappingRevision,
            BlockedResultId = witness.BlockedResult.Id,
            ReviewSourceId = witness.ReviewSource.Id,
            ReviewOutcomeId = witness.ReviewOutcome.Id,
            NoClaimProofSha256 = witness.NoClaimProofSha256,
            SupplierId = source.SupplierId, CurrencyCode = source.CurrencyCode,
            PurchaseOrderId = orderId, IssuedVersionId = issuedId,
            BranchId = branch, CalculationId = staged.Calculation.Id,
            CaptureReceiptId = staged.Receipt.Id, CaptureCommandKey = captureKey,
            MakerPrincipalId = scope.SubjectId, Reason = input.Reason.Trim(),
            CommandKey = key, RecordedAtUtc = at, CreatedAt = at, CreatedBy = scope.SubjectId
        };
        db.AddScoped(link);
        db.AddScoped(InvoiceReplacementEvidence.CreateReceipt(link, staged.Invoice, requestSha, at));
        var marker = InvoiceReplacementEvidence.MarkerJson(link);
        db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceReplacementLink), RecordId = link.Id,
            Action = InvoiceReplacementEvidence.AuditAction, ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = marker, CorrelationId = key.ToString("D") });
        db.AddScoped(new OutboxEvent { AggregateType = nameof(SupplierInvoice), AggregateId = id,
            EventType = InvoiceReplacementEvidence.EventType, EventVersion = 1,
            PayloadJson = marker, IdempotencyKey = key.ToString("D") });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Content(InvoiceReplacementEvidence.ResponseJson(link, staged.Invoice),
            "application/json", statusCode: 201);
        }
        catch (InvalidOperationException)
        { return Results.Conflict(new { code = "invoice-replacement-evidence-changed",
            message = "The corrected invoice or retained source evidence changed. Review it before replacing the invoice." }); }
    }

    internal static async Task<bool> RetainedReplacementReference(ProcurementDbContext db,
        ProcurementRequestScope scope, InvoiceAmountCoreReferenceRequirement requirement, CancellationToken ct)
    {
        if (requirement.TenantId != scope.TenantId || requirement.LegalEntityId != scope.LegalEntityId)
            return false;
        var reference = await CoreCurrencyPrecisionReader.ReadHistoricalAsync(db.Database.GetDbConnection(),
            db.Database.CurrentTransaction!.GetDbTransaction(), scope.TenantId,
            requirement.ReferenceId, requirement.ObservedAtUtc, ct);
        return reference.ReferenceId == requirement.ReferenceId && reference.Revision == requirement.Revision
            && reference.CurrencyCode == requirement.CurrencyCode && reference.MinorUnits == requirement.MinorUnits
            && reference.Rounding == requirement.Rounding.ToString()
            && reference.PayloadSha256 == requirement.SourcePayloadSha256
            && reference.PublishedAtUtc == requirement.PublishedAtUtc;
    }

    private static async Task<IResult> RecoverCurrencyReplacement(Guid id, Guid key,
        HttpRequest request, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (id == Guid.Empty || key == Guid.Empty) return Missing();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || !actor.Permissions.Contains("procurement-invoice-read")) return Results.Forbid();
        var source = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (source is null || source.CreatedBy != scope.SubjectId) return Missing();
        try { await ProcurementExecutionRecordScope.Branches(core, scope, source.BranchRef, true, ct); }
        catch (Exception e) when (e is UnauthorizedAccessException or KeyNotFoundException) { return Missing(); }
        if (!await ReplacementReadSchemaReady(request, ct)) return ReplacementSchemaUnavailable();
        var receipt = await db.InvoiceReplacementCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceInvoiceId == id
                && x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, ct);
        return receipt is null ? Missing() : await SavedCurrencyReplacement(receipt, source, db, scope, ct);
    }

    private static async Task<IResult> ReadCurrencyReplacement(Guid id, HttpRequest request,
        ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (id == Guid.Empty) return Missing();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || !actor.Permissions.Contains("procurement-invoice-read")) return Results.Forbid();
        var invoice = await Visible(db, scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null) return Missing();
        try { await ProcurementExecutionRecordScope.Branches(core, scope, invoice.BranchRef, true, ct); }
        catch (Exception e) when (e is UnauthorizedAccessException or KeyNotFoundException) { return Missing(); }
        if (!await ReplacementReadSchemaReady(request, ct)) return ReplacementSchemaUnavailable();
        var verified = await db.ReadVerifiedInvoiceReplacementAsync(id,
            (requirement, token) => RetainedReplacementReference(db, scope, requirement, token), ct);
        if (verified is null) return Missing();
        var (link, receipt) = verified.Value;
        var source = await Visible(db, scope).SingleAsync(x => x.Id == link.SourceInvoiceId, ct);
        var successor = await Visible(db, scope).SingleAsync(x => x.Id == link.SuccessorInvoiceId, ct);
        return Results.Ok(new { schemaVersion = 1, linkId = link.Id,
            link.SourceInvoiceId, link.SuccessorInvoiceId,
            sourceInvoiceNumber = source.InvoiceNumber,
            successorInvoiceNumber = successor.InvoiceNumber,
            sourceDisplayState = "SupersededByCorrection",
            successorStatus = successor.Status,
            link.CommandKey, link.RecordedAtUtc,
            originalResponseSha256 = receipt.ResponseSha256,
            canPostToFinance = false });
    }

    private static async Task<IResult> SavedCurrencyReplacement(InvoiceReplacementCommandReceipt receipt,
        SupplierInvoice source, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
    {
        var link = await db.InvoiceReplacementLinks.AsNoTracking().SingleAsync(x => x.Id == receipt.LinkId, ct);
        if (link.SourceInvoiceId != source.Id || link.MakerPrincipalId != scope.SubjectId) return Missing();
        // The inventory supplement authenticates the link, both command receipts,
        // the retained no-claim result and the exact audit/outbox marker pair.
        try { _ = await db.CaptureInvoiceOwnerInventoryAsync(ct,
            (requirement, token) => RetainedReplacementReference(db, scope, requirement, token),
            openingNewBaseline: false); }
        catch (InvalidOperationException)
        { return Results.Conflict(new { code = "invoice-replacement-history-unavailable",
            message = "The retained replacement history needs review before its original response can be shown." }); }
        return Results.Content(receipt.ResponseJson, "application/json", statusCode: receipt.ResponseStatus);
    }
}
