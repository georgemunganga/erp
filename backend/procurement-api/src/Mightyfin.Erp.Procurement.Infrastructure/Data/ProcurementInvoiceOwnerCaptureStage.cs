using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private InvoiceProcurementOwnerBaseline? stagedInvoiceOwnerBaseline;
    private InvoiceProcurementInventoryCapture? stagedInvoiceOwnerCapture;
    private ApprovalCase? stagedInvoiceOwnerCase;
    private IReadOnlyDictionary<string,Guid?>? stagedInvoiceOwnerReviewers;
    private IReadOnlyList<InvoiceProcurementOwnerBaselineRow>? stagedInvoiceOwnerRows;

    /// <summary>Read, construct and stage the Core review source in the shared
    /// serializable transaction. This does not admit an owner or enable claims.
    /// The coordinator must persist the supplied Core case in the same commit.</summary>
    public async Task<InvoiceProcurementOwnerBaseline> CaptureAndStageInvoiceOwnerBaselineAsync(
        Guid baselineId, ApprovalCase pendingCoreCase,
        IReadOnlyDictionary<string,Guid?> reviewerWorkerAliases,
        DateTimeOffset at, CancellationToken ct)
    {
        if(!Database.IsNpgsql() || Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || stagedInvoiceOwnerBaseline is not null || baselineId==Guid.Empty
            || pendingCoreCase is null || reviewerWorkerAliases is null
            || scope.IsConfined || scope.IsExplicitAllBranches
            || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            || !ProcurementPrincipalEvidence.OptionalWorker(scope.CoreLinkedWorkerId))
            throw new InvalidOperationException("InvoiceOwnerSerializableCaptureRequired");
        var capturedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(at)
            ?? throw new InvalidOperationException("InvoiceOwnerCaptureTimeInvalid");
        var capture=await CaptureInvoiceOwnerInventoryAsync(ct);
        var summary=InvoiceProcurementInventoryEvidence.Summarize(capture);
        var classifications=InvoiceProcurementHistoricalClassification.Classify(capture)
            .ToDictionary(x=>(x.TableName,x.RowId));
        var initialClaims=await CaptureInitialInvoiceClaimsAsync(capture,ct);
        var migrations=(await Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if(migrations.Length==0) throw new InvalidOperationException("InvoiceOwnerMigrationBoundaryUnavailable");
        var baseline=new InvoiceProcurementOwnerBaseline
        {
            Id=baselineId,TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapturedThroughMigration=migrations[^1],CapturedAtUtc=capturedAt,
            CatalogManifestJson=InvoiceProcurementBaselineEvidence.CatalogManifestJson(),
            CatalogManifestSha256=summary.ManifestSha256,InventorySetSha256=summary.CompleteSetSha256,
            InventoryRowCount=summary.RowCount,CoreCaseId=pendingCoreCase.Id,
            MakerPrincipalId=scope.SubjectId,MakerWorkerAliasId=scope.CoreLinkedWorkerId,
            InitialActiveClaimsSha256=InvoiceMatchExecutionEvidence.AllocationsDigest(initialClaims),
            InitialExposureSetSha256=InvoiceProcurementInitialExposureEvidence.Digest(capture),
            CreatedAt=capturedAt,CreatedBy=scope.SubjectId
        };
        var rows=capture.Tables.SelectMany(table=>table.Rows.Select(source=>
        {
            var classified=classifications[(table.TableName,source.Id)];
            return new InvoiceProcurementOwnerBaselineRow
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                BaselineId=baseline.Id,TableName=table.TableName,SourceRowId=source.Id,
                SourceRowVersion=source.Version,OriginalRowSha256=source.CanonicalRowSha256,
                ImmutableFieldsSha256=InvoiceProcurementImmutableFields.Digest(table.TableName,source.RowJson),
                InitialClassification=classified.Classification,
                SupplierInvoiceId=classified.SupplierInvoiceId,PurchaseOrderId=classified.PurchaseOrderId,
                CreatedAt=capturedAt,CreatedBy=scope.SubjectId
            };
        })).ToArray();
        InvoiceProcurementBaselineEvidence.VerifyCapture(baseline,rows,capture);
        InvoiceProcurementBaselineEvidence.VerifyPendingCore(baseline,pendingCoreCase,reviewerWorkerAliases);
        var audit=new AuditEvent
        {
            TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            RecordType=InvoiceProcurementBaselineEvidence.SourceType,RecordId=baseline.Id,
            Action="InvoiceOwnerBaselineCaptured",ActorSubjectId=scope.SubjectId,
            ChangedFieldsJson=InvoiceProcurementBaselineEvidence.CaptureAuditJson(baseline),
            CorrelationId=scope.CorrelationId,CreatedAt=capturedAt,CreatedBy=scope.SubjectId
        };
        stagedInvoiceOwnerBaseline=baseline;stagedInvoiceOwnerRows=rows;
        stagedInvoiceOwnerCapture=capture;stagedInvoiceOwnerCase=pendingCoreCase;
        stagedInvoiceOwnerReviewers=new Dictionary<string,Guid?>(reviewerWorkerAliases,StringComparer.Ordinal);
        AddScoped(baseline);
        foreach(var row in rows) AddScoped(row);
        AddScoped(audit);
        return baseline;
    }
}
