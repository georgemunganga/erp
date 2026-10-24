using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class FinanceCheckerIdentityPersistenceTests
{
    [Fact]
    public void Successor_model_keeps_explicit_NULL_safe_versions_and_never_promotes_optional_aliases_to_principal_keys()
    {
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=principal_approval_design_only;Username=invalid").Options,new Scope(Guid.NewGuid()));
        var model=db.GetService<IDesignTimeModel>().Model;
        var request=model.FindEntityType(typeof(PurchaseRequest))!;
        var original=request.GetCheckConstraints().Single(x=>x.Name=="ck_pr_original_principal_identity").Sql;
        Assert.Contains("requester_identity_schema_version=0 AND requester_core_worker_id IS NULL AND requester_identity_sha256 IS NULL",original);
        Assert.Contains("requester_identity_schema_version=1 AND requester_principal_id IS NOT NULL",original);
        Assert.Contains("requester_identity_sha256 IS NOT NULL",original);
        Assert.Contains("created_by IS NOT NULL AND created_by=requester_principal_id::text",original);
        Assert.Contains("source_channel IS NOT NULL AND source_channel='ERP'",original);
        Assert.Equal(0,request.FindProperty(nameof(PurchaseRequest.RequesterIdentitySchemaVersion))!.GetDefaultValue());
        Assert.Equal(64,request.FindProperty(nameof(PurchaseRequest.RequesterIdentitySha256))!.GetMaxLength());
        Assert.DoesNotContain(request.GetKeys().SelectMany(x=>x.Properties),x=>x.Name==nameof(PurchaseRequest.RequesterCoreWorkerId));
        var pin=model.FindEntityType(typeof(PurchaseRequestApprovalSubmission))!;
        var sql=pin.GetCheckConstraints().Single(x=>x.Name=="ck_pr_approval_principal_identity").Sql;
        Assert.Contains("snapshot_schema_version IN (1,2) AND requester_principal_id IS NULL",sql);
        Assert.Contains("snapshot_schema_version=3 AND requester_principal_id IS NOT NULL",sql);
        Assert.Contains("maker_subject_id IS NOT NULL AND maker_subject_id=requester_principal_id::text",sql);
        Assert.Contains("requester_identity_sha256 IS NOT NULL",sql);Assert.Contains("principal_identity_sha256 IS NOT NULL",sql);
        var worker=pin.GetCheckConstraints().Single(x=>x.Name=="ck_pr_approval_worker_identity").Sql;
        Assert.Contains("snapshot_schema_version = 2 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL",worker);
        Assert.Contains("snapshot_schema_version=3 AND worker_identity_sha256 IS NULL",worker);
        var identity=model.FindEntityType(typeof(SpendingAuthorityIdentityRecord))!;
        Assert.True(identity.FindProperty(nameof(SpendingAuthorityIdentityRecord.ActorWorkerId))!.IsNullable);
        Assert.True(identity.FindProperty(nameof(SpendingAuthorityIdentityRecord.RecipientWorkerId))!.IsNullable);
        var authority=identity.GetCheckConstraints().Single(x=>x.Name=="ck_authority_identity_evidence").Sql;
        Assert.Contains("schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)",authority);
        Assert.Contains("schema_version=1 OR ((action='PurchaseRequest.Approve' AND permission='procurement-approval-decide')",authority);
        Assert.Contains("(action='PurchaseOrder.Approve' AND permission='procurement-po-approve')",authority);
        Assert.Contains("(action='SourcingEvent.Publish' AND permission='procurement-sourcing-publish')",authority);
        Assert.DoesNotContain(identity.GetKeys().SelectMany(x=>x.Properties),x=>x.Name is "ActorWorkerId" or "RecipientWorkerId");
        var assignment=model.FindEntityType(typeof(PurchaseRequestAssignmentAuthorityEvidence))!;
        Assert.Equal(1,assignment.FindProperty(nameof(PurchaseRequestAssignmentAuthorityEvidence.IdentitySchemaVersion))!.GetDefaultValue());
        Assert.DoesNotContain(assignment.GetKeys().SelectMany(x=>x.Properties),x=>x.Name is "AssigneeWorkerId" or "IdentitySchemaVersion");
        Assert.DoesNotContain(assignment.GetForeignKeys(),x=>x.PrincipalEntityType.GetSchema() is "hrm" or "core_approval");
    }
}
