using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementInventoryEvidenceTests
{
    private static InvoiceProcurementInventoryCapture Capture(Guid? wrongCompany=null)
    {
        var company=Guid.NewGuid();var id=Guid.NewGuid();
        var row=JsonSerializer.Serialize(new{id,version=1,tenant_id="tenant-a",legal_entity_id=wrongCompany??company,
            supplier_id=Guid.NewGuid(),invoice_number="INV-1",normalized_invoice_number="INV1",
            currency_code="ZMW",net_amount=10m,tax_amount=0m,gross_amount=10m,
            invoice_date="2026-10-28",status="Draft"});
        var tables=InvoiceProcurementOwnerTables.Names.Select(name=>new InvoiceProcurementCapturedTable(name,
            name=="supplier_invoices"
                ?new InvoiceProcurementCapturedRow[]{new(id,1,ProcurementEvidenceEncoding.PolicyJsonDigest(row),row)}
                :Array.Empty<InvoiceProcurementCapturedRow>())).ToArray();
        return new("tenant-a",company,tables);
    }

    [Fact]
    public void Server_read_inventory_requires_exact_twenty_table_manifest_and_row_scope()
    {
        var capture=Capture();var summary=InvoiceProcurementInventoryEvidence.Summarize(capture);
        Assert.Equal(20,summary.Tables.Count);
        Assert.Equal(1,summary.RowCount);
        Assert.Equal(0,summary.Tables.Single(x=>x.TableName=="invoice_match_allocations").RowCount);
        Assert.Equal(1,summary.Tables.Single(x=>x.TableName=="supplier_invoices").RowCount);
        Assert.Equal(64,summary.ManifestSha256.Length);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementInventoryEvidence.Summarize(capture with{Tables=capture.Tables.Skip(1).ToArray()}));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementInventoryEvidence.Summarize(Capture(Guid.NewGuid())));
        var invoice=capture.Tables.Last();var row=Assert.Single(invoice.Rows);
        var replaced=invoice with{Rows=[row with{CanonicalRowSha256=new string('a',64)}]};
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementInventoryEvidence.Summarize(capture with
        {Tables=capture.Tables.Take(capture.Tables.Count-1).Append(replaced).ToArray()}));
    }

    [Fact]
    public void Immutable_capture_and_Core_final_decision_are_both_required()
    {
        var capture=Capture();
        // Rebind a genuine captured scope, including the invoice row, for this graph.
        var company=capture.LegalEntityId;
        var summary=InvoiceProcurementInventoryEvidence.Summarize(capture);
        var maker=Guid.NewGuid().ToString("D");var reviewer=Guid.NewGuid().ToString("D");
        var at=new DateTimeOffset(2026,10,28,10,0,0,TimeSpan.Zero);
        var baseline=new InvoiceProcurementOwnerBaseline
        {
            TenantId="tenant-a",LegalEntityId=company,CoreCaseId=Guid.NewGuid(),
            MakerPrincipalId=maker,MakerWorkerAliasId=null,CapturedAtUtc=at,
            CatalogManifestJson=JsonSerializer.Serialize(new{schemaVersion=1,tableNames=InvoiceProcurementOwnerTables.Names},
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CatalogManifestSha256=summary.ManifestSha256,InventorySetSha256=summary.CompleteSetSha256,
            InventoryRowCount=summary.RowCount,InitialActiveClaimsSha256=new string('a',64)
        };
        var invoice=Assert.Single(capture.Tables.Last().Rows);
        var mutable=JsonNode.Parse(invoice.RowJson)!.AsObject();
        mutable["status"]="Received";
        mutable["current_mapping_version_id"]=Guid.NewGuid().ToString("D");
        Assert.Equal(InvoiceProcurementImmutableFields.Digest("supplier_invoices",invoice.RowJson),
            InvoiceProcurementImmutableFields.Digest("supplier_invoices",mutable.ToJsonString()));
        var refreshedJson=mutable.ToJsonString();
        var refreshed=invoice with{RowJson=refreshedJson,
            CanonicalRowSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(refreshedJson)};
        var refreshedTable=capture.Tables.Last() with{Rows=[refreshed]};
        var refreshedCapture=capture with{Tables=capture.Tables.Take(19).Append(refreshedTable).ToArray()};
        mutable["net_amount"]=11m;
        Assert.NotEqual(InvoiceProcurementImmutableFields.Digest("supplier_invoices",invoice.RowJson),
            InvoiceProcurementImmutableFields.Digest("supplier_invoices",mutable.ToJsonString()));
        var row=new InvoiceProcurementOwnerBaselineRow
        {
            TenantId="tenant-a",LegalEntityId=company,BaselineId=baseline.Id,
            TableName="supplier_invoices",SourceRowId=invoice.Id,SourceRowVersion=invoice.Version,
            OriginalRowSha256=invoice.CanonicalRowSha256,
            ImmutableFieldsSha256=InvoiceProcurementImmutableFields.Digest("supplier_invoices",invoice.RowJson),
            InitialClassification="BlockedAmbiguity"
        };
        InvoiceProcurementBaselineEvidence.VerifyCapture(baseline,[row],capture);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCapture(
            baseline,[row],refreshedCapture));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.RequireResolvedClassification(baseline,[row]));
        var tampered=new InvoiceProcurementOwnerBaselineRow
        {
            TenantId=row.TenantId,LegalEntityId=row.LegalEntityId,BaselineId=row.BaselineId,
            TableName=row.TableName,SourceRowId=row.SourceRowId,SourceRowVersion=row.SourceRowVersion,
            OriginalRowSha256=new string('c',64),ImmutableFieldsSha256=row.ImmutableFieldsSha256,
            InitialClassification=row.InitialClassification
        };
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCapture(baseline,[tampered],capture));

        var stage=new ApprovalStage{TenantId="tenant-a",LegalEntityId=company,CaseId=baseline.CoreCaseId,
            Sequence=1,Code="IndependentReview",Mode="Any",RequiredApprovals=1,State="Approved"};
        var assignment=new ApprovalAssignment{TenantId="tenant-a",LegalEntityId=company,CaseId=baseline.CoreCaseId,
            StageId=stage.Id,AssigneeSubjectId=reviewer,
            PermissionKey=InvoiceProcurementBaselineEvidence.ReviewPermission,State="Approved"};
        stage.Assignments.Add(assignment);
        var decided=at.AddMinutes(1);var key=Guid.NewGuid().ToString("D");
        var decision=new ApprovalDecision{TenantId="tenant-a",LegalEntityId=company,CaseId=baseline.CoreCaseId,
            StageId=stage.Id,AssignmentId=assignment.Id,ActorSubjectId=reviewer,Action="Approve",
            IdempotencyKey=key,DecidedAt=decided};
        var workflow=new ApprovalCase{Id=baseline.CoreCaseId,TenantId="tenant-a",LegalEntityId=company,
            SourceModule="procurement",SourceType=InvoiceProcurementBaselineEvidence.SourceType,
            SourceId=baseline.Id,SourceVersion=1,MakerSubjectId=maker,State="Approved",
            OpenedAt=at,CompletedAt=decided,CompletionKey=key,
            PolicyVersionId=Guid.NewGuid(),PolicyVersionNumber=1};
        workflow.Stages.Add(stage);
        var approvalAudit=new AuditEvent{TenantId="tenant-a",LegalEntityId=company,
            RecordType=InvoiceProcurementBaselineEvidence.SourceType,RecordId=baseline.Id,
            Action="InvoiceOwnerBaselineApproved",ActorSubjectId=reviewer,
            ChangedFieldsJson=InvoiceProcurementBaselineEvidence.DecisionAuditJson(baseline,decision)};
        var pendingStage=new ApprovalStage{TenantId="tenant-a",LegalEntityId=company,CaseId=baseline.CoreCaseId,
            Sequence=1,Code="IndependentReview",Mode="Any",RequiredApprovals=1,State="Active"};
        pendingStage.Assignments.Add(new ApprovalAssignment{TenantId="tenant-a",LegalEntityId=company,
            CaseId=baseline.CoreCaseId,StageId=pendingStage.Id,AssigneeSubjectId=reviewer,
            PermissionKey=InvoiceProcurementBaselineEvidence.ReviewPermission,State="Pending"});
        var pending=new ApprovalCase{Id=baseline.CoreCaseId,TenantId="tenant-a",LegalEntityId=company,
            SourceModule="procurement",SourceType=InvoiceProcurementBaselineEvidence.SourceType,
            SourceId=baseline.Id,SourceVersion=1,MakerSubjectId=maker,State="Pending",
            OpenedAt=at,PolicyVersionId=workflow.PolicyVersionId,PolicyVersionNumber=1};
        pending.Stages.Add(pendingStage);
        InvoiceProcurementBaselineEvidence.VerifyPendingCore(baseline,pending,
            new Dictionary<string,Guid?>{{reviewer,null}});
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyPendingCore(
            baseline,pending,new Dictionary<string,Guid?>{{maker,null}}));
        InvoiceProcurementBaselineEvidence.VerifyCoreReview(baseline,workflow,[decision],approvalAudit,reviewer,null,decided);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCoreReview(
            baseline,workflow,[decision],approvalAudit,maker,null,decided));
        approvalAudit.ActorSubjectId=maker;
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCoreReview(
            baseline,workflow,[decision],approvalAudit,reviewer,null,decided));
        approvalAudit.ActorSubjectId=reviewer;
        decision.IdempotencyKey=Guid.NewGuid().ToString("D");
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCoreReview(
            baseline,workflow,[decision],approvalAudit,reviewer,null,decided));
        decision.IdempotencyKey=key;
        workflow.SourceId=Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementBaselineEvidence.VerifyCoreReview(
            baseline,workflow,[decision],approvalAudit,reviewer,null,decided));
    }
}
