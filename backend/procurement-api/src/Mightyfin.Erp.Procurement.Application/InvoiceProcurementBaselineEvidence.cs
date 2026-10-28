using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Checks the immutable source that Core independently reviewed.
/// The caller must load the complete inventory in its current serializable
/// Procurement transaction; this pure verifier never asserts DB completeness.</summary>
public static class InvoiceProcurementBaselineEvidence
{
    public const string SourceType = "InvoiceProcurementOwnerBaseline";
    public const string ReviewPermission = "procurement-invoice-owner-baseline-review";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string CatalogManifestJson() => JsonSerializer.Serialize(new
    {schemaVersion=1,tableNames=InvoiceProcurementOwnerTables.Names},Json);

    public static string CaptureAuditJson(InvoiceProcurementOwnerBaseline baseline) =>
        JsonSerializer.Serialize(new {baselineId=baseline.Id,baseline.CoreCaseId,
            baseline.CatalogManifestSha256,baseline.InventorySetSha256,
            baseline.InventoryRowCount},Json);

    public static string DecisionAuditJson(InvoiceProcurementOwnerBaseline baseline,
        ApprovalDecision decision) => JsonSerializer.Serialize(new
        {baselineId=baseline.Id,baseline.CoreCaseId,decisionId=decision.Id,
            decision.IdempotencyKey,baseline.InventorySetSha256},Json);

    public static void VerifyPendingCore(InvoiceProcurementOwnerBaseline baseline,
        ApprovalCase workflow, IReadOnlyDictionary<string,Guid?> reviewerWorkerAliases)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(reviewerWorkerAliases);
        var assignments=workflow.Stages.SelectMany(x=>x.Assignments).ToArray();
        if(workflow.Id != baseline.CoreCaseId || workflow.TenantId != baseline.TenantId
            || workflow.LegalEntityId != baseline.LegalEntityId || workflow.SourceModule != "procurement"
            || workflow.SourceType != SourceType || workflow.SourceId != baseline.Id
            || workflow.SourceVersion != baseline.Version || workflow.MakerSubjectId != baseline.MakerPrincipalId
            || workflow.State != "Pending" || workflow.OpenedAt != baseline.CapturedAtUtc
            || workflow.CompletedAt is not null || workflow.CompletionKey is not null
            || assignments.Length != reviewerWorkerAliases.Count
            || assignments.Any(x=>x.PermissionKey!=ReviewPermission
                || !reviewerWorkerAliases.TryGetValue(x.AssigneeSubjectId,out var worker)
                || !ProcurementPrincipalEvidence.Independent(baseline.MakerPrincipalId,
                    baseline.MakerWorkerAliasId,x.AssigneeSubjectId,worker)))
            throw new InvalidOperationException("InvoiceOwnerCoreOpeningChanged");
        ApprovalEngine.ValidateNewCase(workflow);
    }

    public static void VerifyCapture(InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows,
        InvoiceProcurementInventoryCapture capture)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(rows);
        var summary = InvoiceProcurementInventoryEvidence.Summarize(capture);
        var manifest = CatalogManifestJson();
        if (baseline.SchemaVersion != 2 || baseline.Version != 1 || baseline.Id == Guid.Empty
            || baseline.CoreCaseId == Guid.Empty || baseline.TenantId != summary.TenantId
            || baseline.LegalEntityId != summary.LegalEntityId
            || !ProcurementPrincipalEvidence.Canonical(baseline.MakerPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(baseline.MakerWorkerAliasId)
            || baseline.InventoryRowCount != summary.RowCount
            || baseline.CatalogManifestJson != manifest
            || baseline.CatalogManifestSha256 != summary.ManifestSha256
            || baseline.InventorySetSha256 != summary.CompleteSetSha256
            || baseline.InitialActiveClaimsSha256 is not {Length:64}
            || !baseline.InitialActiveClaimsSha256.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')
            || baseline.InitialExposureSetSha256 != InvoiceProcurementInitialExposureEvidence.Digest(capture)
            || rows.Count != summary.RowCount
            || rows.Select(x=>(x.TableName,x.SourceRowId)).Distinct().Count()!=rows.Count)
            throw new InvalidOperationException("InvoiceOwnerBaselineCaptureChanged");
        var lookup=rows.ToDictionary(x=>(x.TableName,x.SourceRowId));
        var classes=InvoiceProcurementHistoricalClassification.Classify(capture)
            .ToDictionary(x=>(x.TableName,x.RowId));
        foreach(var table in capture.Tables)
            foreach(var source in table.Rows)
            {
                var classified=classes[(table.TableName,source.Id)];
                if (!lookup.TryGetValue((table.TableName,source.Id),out var row)
                    || row.BaselineId != baseline.Id || row.TenantId != baseline.TenantId
                    || row.LegalEntityId != baseline.LegalEntityId || row.Version != 1
                    || row.SourceRowVersion != source.Version
                    || row.OriginalRowSha256 != source.CanonicalRowSha256
                    || row.ImmutableFieldsSha256 != InvoiceProcurementImmutableFields.Digest(table.TableName,source.RowJson)
                    || row.InitialClassification != classified.Classification
                    || row.SupplierInvoiceId != classified.SupplierInvoiceId
                    || row.PurchaseOrderId != classified.PurchaseOrderId)
                    throw new InvalidOperationException("InvoiceOwnerBaselineRowChanged");
            }
    }

    public static void VerifyCoreReview(InvoiceProcurementOwnerBaseline baseline,
        ApprovalCase workflow, IReadOnlyList<ApprovalDecision> decisions,
        AuditEvent approvalAudit, string reviewerPrincipalId,
        Guid? reviewerWorkerAliasId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(approvalAudit);
        var assignments=workflow.Stages.SelectMany(x=>x.Assignments).ToArray();
        var final=decisions.SingleOrDefault(x=>x.IdempotencyKey==workflow.CompletionKey);
        var ordered=workflow.Stages.OrderBy(x=>x.Sequence).ToArray();
        var exactStages=ordered.Length>0 && ordered.Select(x=>x.Sequence).Distinct().Count()==ordered.Length
            && ordered.All(stage=>stage.CaseId==workflow.Id && stage.TenantId==workflow.TenantId
                && stage.LegalEntityId==workflow.LegalEntityId && stage.State=="Approved"
                && stage.RequiredApprovals>0 && stage.RequiredApprovals<=stage.Assignments.Count
                && stage.Assignments.Count(x=>x.State=="Approved")==stage.RequiredApprovals
                && stage.Assignments.All(x=>x.CaseId==workflow.Id && x.StageId==stage.Id
                    && x.TenantId==workflow.TenantId && x.LegalEntityId==workflow.LegalEntityId
                    && x.PermissionKey==ReviewPermission && x.State is ("Approved" or "Superseded"))
                && decisions.Count(x=>x.StageId==stage.Id)==stage.RequiredApprovals);
        var priorCompleted=workflow.OpenedAt;
        if(exactStages)
            foreach(var stage in ordered)
            {
                var votes=decisions.Where(x=>x.StageId==stage.Id).ToArray();
                if(votes.Any(x=>x.DecidedAt<priorCompleted || x.DecidedAt>workflow.CompletedAt))
                    exactStages=false;
                if(votes.Length>0) priorCompleted=votes.Max(x=>x.DecidedAt);
            }
        if(workflow.Id != baseline.CoreCaseId || workflow.TenantId != baseline.TenantId
            || workflow.LegalEntityId != baseline.LegalEntityId || workflow.SourceModule != "procurement"
            || workflow.SourceType != SourceType || workflow.SourceId != baseline.Id
            || workflow.SourceVersion != baseline.Version || workflow.MakerSubjectId != baseline.MakerPrincipalId
            || workflow.PolicyVersionId == Guid.Empty || workflow.PolicyVersionNumber < 1
            || workflow.State != "Approved" || workflow.OpenedAt != baseline.CapturedAtUtc
            || workflow.CompletedAt is null || workflow.CompletedAt > at
            || !ProcurementPrincipalEvidence.Independent(baseline.MakerPrincipalId,baseline.MakerWorkerAliasId,
                reviewerPrincipalId,reviewerWorkerAliasId)
            || string.IsNullOrWhiteSpace(workflow.CompletionKey)
            || !exactStages || assignments.Length == 0
            || assignments.Select(x=>x.Id).Distinct().Count()!=assignments.Length
            || decisions.Count == 0 || decisions.Select(x=>x.Id).Distinct().Count()!=decisions.Count
            || decisions.Select(x=>x.IdempotencyKey).Distinct(StringComparer.Ordinal).Count()!=decisions.Count
            || priorCompleted!=workflow.CompletedAt
            || final is null || final.Action != "Approve" || final.CaseId != workflow.Id
            || final.StageId != ordered[^1].Id
            || final.ActorSubjectId != reviewerPrincipalId || final.DecidedAt != workflow.CompletedAt
            || approvalAudit.TenantId!=baseline.TenantId || approvalAudit.LegalEntityId!=baseline.LegalEntityId
            || approvalAudit.RecordType!=SourceType || approvalAudit.RecordId!=baseline.Id
            || approvalAudit.Action!="InvoiceOwnerBaselineApproved"
            || approvalAudit.ActorSubjectId!=reviewerPrincipalId
            || approvalAudit.CorrelationId!=workflow.CompletionKey
            || approvalAudit.ChangedFieldsJson!=DecisionAuditJson(baseline,final)
            || !assignments.Any(x=>x.Id==final.AssignmentId && x.CaseId==workflow.Id
                && x.StageId==final.StageId && x.AssigneeSubjectId==reviewerPrincipalId
                && x.PermissionKey==ReviewPermission && x.State=="Approved")
            || decisions.Any(x=>x.CaseId!=workflow.Id || x.Action!="Approve"
                || !assignments.Any(a=>a.Id==x.AssignmentId && a.State=="Approved"
                    && a.StageId==x.StageId && a.AssigneeSubjectId==x.ActorSubjectId)))
            throw new InvalidOperationException("InvoiceOwnerCoreReviewChanged");
    }

    /// <summary>A reviewed source with unresolved old rows is retained history,
    /// never a complete owner checkpoint.</summary>
    public static void RequireResolvedClassification(
        InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(rows);
        if(rows.Count!=baseline.InventoryRowCount
            || rows.Any(x=>x.BaselineId!=baseline.Id || x.TenantId!=baseline.TenantId
                || x.LegalEntityId!=baseline.LegalEntityId
                || x.InitialClassification=="BlockedAmbiguity"))
            throw new InvalidOperationException("InvoiceOwnerHistoricalRowsUnclassified");
    }

    /// <summary>Do not turn an unrelated company's or another order's old
    /// ambiguity into a global billing-capacity denial. The caller still has
    /// to validate all live links, typed transitions and claim sequences.</summary>
    public static void RequireResolvedClassificationForOrder(
        InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows,
        Guid purchaseOrderId)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(rows);
        if(purchaseOrderId==Guid.Empty || rows.Count!=baseline.InventoryRowCount
            || rows.Any(x=>x.BaselineId!=baseline.Id || x.TenantId!=baseline.TenantId
                || x.LegalEntityId!=baseline.LegalEntityId
                || !InvoiceProcurementOwnerTables.Names.Contains(x.TableName,StringComparer.Ordinal))
            || rows.Select(x=>(x.TableName,x.SourceRowId)).Distinct().Count()!=rows.Count)
            throw new InvalidOperationException("InvoiceOwnerHistoricalRowsUnclassified");
        var knownInvoices=rows.Where(x=>x.TableName=="supplier_invoices")
            .Select(x=>x.SourceRowId).ToHashSet();
        if(rows.Any(x=>x.SupplierInvoiceId is { } invoiceId
            && !knownInvoices.Contains(invoiceId)))
            throw new InvalidOperationException("InvoiceOwnerOrphanHistoricalRow");
        var linkedInvoices=rows.Where(x=>x.PurchaseOrderId==purchaseOrderId
            && x.SupplierInvoiceId is not null).Select(x=>x.SupplierInvoiceId!.Value).ToHashSet();
        if(rows.Any(x=>x.InitialClassification=="BlockedAmbiguity"
            && (x.PurchaseOrderId==purchaseOrderId
                || x.SupplierInvoiceId is { } invoiceId && linkedInvoices.Contains(invoiceId))))
            throw new InvalidOperationException("InvoiceOwnerOrderHistoryUnclassified");
    }
}
