using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Real SaveChanges guards with synthetic result linkage over InMemory,
/// plus real Npgsql design metadata. No Core authentication, PostgreSQL constraint,
/// migration, finite-permission, transaction-race or full RequestDetail proof.</summary>
public sealed class PurchaseRequestDraftReceiptPersistenceTests
{
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId,Guid? WorkLocationId=null,Guid? OrgUnitId=null):IProcurementScope
    {public bool IsConfined=>false;public Guid? WorkerId=>null;public string? CorrelationId=>"pr-draft-receipt-tests";}
    private static Scope NewScope()=>new("pr-draft-receipts",Guid.NewGuid(),Guid.NewGuid().ToString("D"));
    private static DbContextOptions<ProcurementDbContext> Options()=>new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static PurchaseRequest Request(Scope scope)=>new() {RequesterPrincipalId=Guid.Parse(scope.SubjectId),
        Number="PR-"+Guid.NewGuid().ToString("N"),Purpose="Synthetic persisted draft",CurrencyCode="ZMW",SourceChannel="ERP",
        BranchRef=scope.WorkLocationId,DepartmentRef=scope.OrgUnitId,Status="Draft"};
    private static PurchaseRequestDraftCommandReceipt Receipt(Scope scope,PurchaseRequest request,string operation="Create",long? expected=null,
        Guid? actor=null,Guid? command=null)
    {
        var principal=actor??Guid.Parse(scope.SubjectId);var version=expected.HasValue?checked(expected.Value+1):1;
        var metadata=new PurchaseRequestDraftCommandMetadata(scope.TenantId,scope.LegalEntityId,operation,command??Guid.NewGuid(),principal,
            request.Id,scope.WorkLocationId,scope.OrgUnitId,request.BranchRef,request.DepartmentRef,expected,version,
            PurchaseRequestDraftCommandReceipts.Fingerprint("{\"syntheticIntentVersion\":1}"),DateTimeOffset.UtcNow);
        // Linkage-only synthetic result; existing owning API validates its complete DTO.
        return PurchaseRequestDraftCommandReceipts.Complete(metadata,JsonSerializer.Serialize(new {id=request.Id,
            requesterPrincipalId=request.RequesterPrincipalId is null?(Guid?)null:principal,requesterWorkerId=request.RequesterWorkerId,
            branchRef=request.BranchRef,departmentRef=request.DepartmentRef,version,requestEditVersion=1,
            status=request.Status,approvalState=request.ApprovalState,currencyCode=request.CurrencyCode}));
    }
    private static async Task<string> Footprint(DbContextOptions<ProcurementDbContext> options,Scope scope)
    {
        await using var db=new ProcurementDbContext(options,scope);
        return JsonSerializer.Serialize(new {requests=await db.PurchaseRequests.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            lines=await db.PurchaseRequestLines.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            receipts=await db.PurchaseRequestDraftCommandReceipts.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            audit=await db.AuditEvents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),outbox=await db.OutboxEvents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()});
    }

    [Fact]
    public async Task Workerless_Create_Update_Submit_save_original_bytes_and_normal_stamps_without_duplicate_receipt_audit()
    {
        var scope=NewScope();var options=Options();var request=Request(scope);
        request.Lines.Add(new PurchaseRequestLine {PurchaseRequestId=request.Id,LineNumber=1,Description="Owned line",UnitCode="each",RequestedQuantity=2,EstimatedUnitPrice=5.1234m});
        await using(var db=new ProcurementDbContext(options,scope))
        {
            db.AddScoped(request);var create=Receipt(scope,request);db.AddScoped(create);await db.SaveChangesAsync();
            Assert.Equal(1,request.Version);Assert.Equal(scope.SubjectId,create.CreatedBy);Assert.Equal(1,create.Version);
            Assert.True(create.CreatedAt>=create.CompletedAt);Assert.Null(create.UpdatedAt);Assert.Null(create.UpdatedBy);
            request.Purpose="Exact changed draft";var update=Receipt(scope,request,"Update",request.Version);
            db.AddScoped(update);await db.SaveChangesAsync();Assert.Equal(2,request.Version);
            request.Status="Submitted";request.ApprovalState="PendingPolicyAndBudget";
            var submit=Receipt(scope,request,"Submit",request.Version);db.AddScoped(submit);
            db.AddScoped(new AuditEvent {RecordType="PurchaseRequest",RecordId=request.Id,Action="Submitted",ActorSubjectId=scope.SubjectId,ChangedFieldsJson="{}"});
            await db.SaveChangesAsync();Assert.Equal(3,request.Version);
            var beforeVersion=request.Version;var auditCount=await db.AuditEvents.CountAsync();
            await db.SaveChangesAsync();Assert.Equal(beforeVersion,request.Version);Assert.Equal(auditCount,await db.AuditEvents.CountAsync());
            Assert.All(new[]{create,update,submit},receipt=>Assert.Equal(receipt.ResponseJson,
                db.PurchaseRequestDraftCommandReceipts.AsNoTracking().Single(x=>x.Id==receipt.Id).ResponseJson));
        }
        await using var read=new ProcurementDbContext(options,scope);
        var receipts=await read.PurchaseRequestDraftCommandReceipts.OrderBy(x=>x.ResultVersion).ToArrayAsync();
        Assert.Equal(new[]{"Create","Update","Submit"},receipts.Select(x=>x.Operation));
        Assert.Equal(new long[]{1,2,3},receipts.Select(x=>x.ResultVersion));
        Assert.All(receipts,PurchaseRequestDraftCommandReceipts.ValidateCompleted);
        Assert.Equal(3,(await read.PurchaseRequests.SingleAsync()).Version);Assert.Single(await read.PurchaseRequestLines.ToArrayAsync());
        var audit=await read.AuditEvents.ToArrayAsync();
        Assert.Equal(3,audit.Count(x=>x.RecordType==nameof(PurchaseRequest)&&x.Action!="Submitted"));
        Assert.Single(audit.Where(x=>x.RecordType==nameof(PurchaseRequestLine)));
        Assert.Single(audit.Where(x=>x.Action=="Submitted"));
        Assert.DoesNotContain(audit,x=>x.RecordType==nameof(PurchaseRequestDraftCommandReceipt));Assert.Empty(await read.OutboxEvents.ToArrayAsync());
    }

    [Fact]
    public async Task Receipt_only_save_preserves_parent_version_and_companywide_context_can_retain_original_branch()
    {
        var branch=Guid.NewGuid();var original=NewScope() with {WorkLocationId=branch};var company=original with {WorkLocationId=null};var options=Options();var request=Request(original);
        await using(var create=new ProcurementDbContext(options,original)){create.AddScoped(request);await create.SaveChangesAsync();}
        await using(var edit=new ProcurementDbContext(options,company))
        {
            var current=await edit.PurchaseRequests.SingleAsync();current.Purpose="Companywide owner edit";
            await edit.SaveChangesAsync();Assert.Equal(2,current.Version);
            var audit=await edit.AuditEvents.CountAsync();var receipt=Receipt(company,current,"Update",1);
            edit.AddScoped(receipt);await edit.SaveChangesAsync();Assert.Equal(2,current.Version);Assert.Equal(audit,await edit.AuditEvents.CountAsync());
            Assert.Null(receipt.ContextBranchRef);Assert.Equal(branch,receipt.OriginalBranchRef);
        }
    }

    [Theory]
    [InlineData("otherActor","RequestDraftCommandReceiptActorInvalid")]
    [InlineData("noncanonicalActor","RequestDraftCommandReceiptActorInvalid")]
    [InlineData("tenant","RequestDraftCommandReceiptScopeInvalid")]
    [InlineData("company","RequestDraftCommandReceiptScopeInvalid")]
    [InlineData("context","RequestDraftCommandReceiptScopeInvalid")]
    [InlineData("metadata","RequestDraftCommandReceiptMetadataInvalid")]
    [InlineData("version","RequestDraftCommandReceiptOwnerVersionInvalid")]
    [InlineData("responseHash","RequestDraftCommandResponseDigestInvalid")]
    [InlineData("responseCurrency","RequestDraftCommandReceiptOwnerResultInvalid")]
    public async Task Added_receipt_actor_scope_metadata_version_and_result_faults_do_not_write_any_record(string fault,string error)
    {
        var scope=NewScope();var options=Options();var request=Request(scope);
        await using(var seed=new ProcurementDbContext(options,scope)){seed.AddScoped(request);await seed.SaveChangesAsync();}
        var baseline=await Footprint(options,scope);
        var activeScope=fault switch {"noncanonicalActor"=>scope with {SubjectId="not-a-canonical-Core-principal"},
            "context"=>scope with {WorkLocationId=Guid.NewGuid()},_=>scope};
        await using(var db=new ProcurementDbContext(options,activeScope))
        {
            var receipt=fault=="otherActor"?Receipt(scope,request,actor:Guid.NewGuid()):Receipt(scope,request);
            if(fault=="tenant")receipt.TenantId="foreign-tenant";
            if(fault=="company")receipt.LegalEntityId=Guid.NewGuid();
            if(fault=="metadata")receipt.Version=2;
            if(fault=="version")receipt=Receipt(scope,request,"Update",5);
            if(fault=="responseCurrency")
            {
                var wrong=Request(scope);wrong.Id=request.Id;wrong.CurrencyCode="USD";receipt=Receipt(scope,wrong);
            }
            db.PurchaseRequestDraftCommandReceipts.Add(receipt); // bypass AddScoped to exercise guard, never relabel evidence
            if(fault=="responseHash")db.Entry(receipt).Property(x=>x.ResponseSha256).CurrentValue=new string('0',64);
            Assert.Equal(error,(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        }
        Assert.Equal(baseline,await Footprint(options,scope));
    }

    [Fact]
    public async Task New_receipt_cannot_replace_actual_parent_branch_or_worker_link_with_a_self_hashed_response()
    {
        var scope=NewScope() with {WorkLocationId=Guid.NewGuid()};var company=scope with {WorkLocationId=null};
        var options=Options();var request=Request(scope);request.RequesterWorkerId=Guid.NewGuid();
        await using(var seed=new ProcurementDbContext(options,scope)){seed.AddScoped(request);await seed.SaveChangesAsync();request.Purpose="Later saved edit";await seed.SaveChangesAsync();}
        var baseline=await Footprint(options,scope);
        foreach(var wrongBranch in new[]{true,false})
        {
            await using var db=new ProcurementDbContext(options,company);
            var supplied=Request(company);supplied.Id=request.Id;supplied.BranchRef=wrongBranch?Guid.NewGuid():request.BranchRef;
            supplied.RequesterWorkerId=wrongBranch?request.RequesterWorkerId:Guid.NewGuid();
            db.AddScoped(Receipt(company,supplied,"Update",1));
            Assert.Equal(wrongBranch?"RequestDraftCommandReceiptOwnerVersionInvalid":"RequestDraftCommandReceiptOwnerResultInvalid",
                (await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
            Assert.Equal(baseline,await Footprint(options,scope));
        }
    }

    [Fact]
    public void AddScoped_does_not_relabel_a_constructed_foreign_receipt()
    {
        var scope=NewScope();using var db=new ProcurementDbContext(Options(),scope);var request=Request(scope);
        var foreign=scope with {TenantId="different"};var receipt=Receipt(foreign,request);
        Assert.Throws<InvalidOperationException>(()=>db.AddScoped(receipt));Assert.Equal("different",receipt.TenantId);
        Assert.Empty(db.ChangeTracker.Entries<PurchaseRequestDraftCommandReceipt>());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreignTenant")]
    [InlineData("foreignCompany")]
    [InlineData("differentOwner")]
    public async Task Scoped_parent_FK_owner_guard_denies_missing_foreign_or_other_principal_record(string mode)
    {
        var scope=NewScope();var options=Options();var parentScope=mode switch {"foreignTenant"=>scope with {TenantId="foreign"},
            "foreignCompany"=>scope with {LegalEntityId=Guid.NewGuid()},"differentOwner"=>scope with {SubjectId=Guid.NewGuid().ToString("D")},_=>scope};
        var request=Request(parentScope);
        if(mode!="missing")await using(var seed=new ProcurementDbContext(options,parentScope)){seed.AddScoped(request);await seed.SaveChangesAsync();}
        var baseline=await Footprint(options,scope);
        await using(var db=new ProcurementDbContext(options,scope))
        {
            db.AddScoped(Receipt(scope,request));
            Assert.Equal("RequestDraftCommandReceiptOwnerInvalid",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        }
        Assert.Equal(baseline,await Footprint(options,scope));
    }

    [Fact]
    public async Task Exact_legacy_CreatedBy_ownership_remains_usable_without_principal_or_worker_backfill()
    {
        var scope=NewScope();var options=Options();var request=Request(scope);request.RequesterPrincipalId=null;request.RequesterWorkerId=Guid.NewGuid();
        await using(var seed=new ProcurementDbContext(options,scope)){seed.AddScoped(request);await seed.SaveChangesAsync();}
        await using(var update=new ProcurementDbContext(options,scope))
        {
            var owner=await update.PurchaseRequests.SingleAsync();owner.Purpose="Legacy owner edit";
            update.AddScoped(Receipt(scope,owner,"Update",1));await update.SaveChangesAsync();
            Assert.Null(owner.RequesterPrincipalId);Assert.Equal(request.RequesterWorkerId,owner.RequesterWorkerId);
        }
    }

    [Theory]
    [InlineData("loaded")]
    [InlineData("detached")]
    [InlineData("delete")]
    public async Task Loaded_detached_updates_and_delete_of_receipt_are_denied_without_new_audit(string mode)
    {
        var scope=NewScope();var options=Options();var request=Request(scope);Guid receiptId;
        await using(var seed=new ProcurementDbContext(options,scope))
        {seed.AddScoped(request);var receipt=Receipt(scope,request);receiptId=receipt.Id;seed.AddScoped(receipt);await seed.SaveChangesAsync();}
        var baseline=await Footprint(options,scope);
        await using(var db=new ProcurementDbContext(options,scope))
        {
            var receipt=mode=="detached"
                ?await db.PurchaseRequestDraftCommandReceipts.AsNoTracking().SingleAsync(x=>x.Id==receiptId)
                :await db.PurchaseRequestDraftCommandReceipts.SingleAsync(x=>x.Id==receiptId);
            if(mode=="detached")db.Attach(receipt);
            if(mode=="delete")db.Remove(receipt);
            else if(mode=="detached")db.Entry(receipt).State=EntityState.Modified;
            else db.Entry(receipt).Property(x=>x.ResponseJson).CurrentValue="{}";
            Assert.Equal("RequestDraftCommandReceiptAppendOnly",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        }
        Assert.Equal(baseline,await Footprint(options,scope));
    }

    [Fact]
    public void Model_has_one_actor_independent_scoped_key_restrict_parent_text_hash_and_NULL_safe_overflow_safe_checks()
    {
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=pr_draft_design_only;Username=invalid").Options,NewScope());
        var entity=db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PurchaseRequestDraftCommandReceipt))!;
        Assert.Equal("purchase_request_draft_command_receipts",entity.GetTableName());Assert.Equal("procurement",entity.GetSchema());
        var unique=Assert.Single(entity.GetIndexes().Where(x=>x.IsUnique));Assert.True(unique.IsUnique);Assert.Equal("ux_pr_draft_receipt_command",unique.GetDatabaseName());
        Assert.Equal(new[]{"TenantId","LegalEntityId","Operation","CommandKey"},unique.Properties.Select(x=>x.Name));
        Assert.DoesNotContain(unique.Properties,x=>x.Name=="ActorPrincipalId");
        var lookup=Assert.Single(entity.GetIndexes().Where(x=>!x.IsUnique));Assert.Equal("ix_pr_draft_receipt_request",lookup.GetDatabaseName());
        Assert.Equal(new[]{"TenantId","LegalEntityId","PurchaseRequestId"},lookup.Properties.Select(x=>x.Name));
        var fk=Assert.Single(entity.GetForeignKeys());Assert.Equal(typeof(PurchaseRequest),fk.PrincipalEntityType.ClrType);Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior);
        Assert.Equal(new[]{"TenantId","LegalEntityId","PurchaseRequestId"},fk.Properties.Select(x=>x.Name));
        Assert.Equal("text",entity.FindProperty("ResponseJson")!.GetColumnType());
        foreach(var name in new[]{"Id","TenantId","LegalEntityId","ActorPrincipalId","CommandKey","Operation","CommandFingerprint","ResponseJson","ResponseSha256","CompletedAt","ResultVersion","Version"})
            Assert.False(entity.FindProperty(name)!.IsNullable);
        var checks=entity.GetCheckConstraints().ToDictionary(x=>x.Name!,x=>x.Sql);Assert.Equal(4,checks.Count);
        Assert.Contains("expected_version IS NOT NULL",checks["ck_pr_draft_receipt_version"]);
        Assert.Contains("result_version::numeric=expected_version::numeric+1",checks["ck_pr_draft_receipt_version"]);
        Assert.DoesNotContain("expected_version+1",checks["ck_pr_draft_receipt_version"]);
        Assert.Contains("original_branch_ref IS NOT NULL",checks["ck_pr_draft_receipt_scope"]);
        Assert.Contains("IS NOT DISTINCT FROM",checks["ck_pr_draft_receipt_scope"]);
        Assert.Contains("sha256(convert_to(response_json,'UTF8'))",checks["ck_pr_draft_receipt_completion"]);
        Assert.Contains("version=1 AND updated_at IS NULL AND updated_by IS NULL",checks["ck_pr_draft_receipt_completion"]);
    }
}
