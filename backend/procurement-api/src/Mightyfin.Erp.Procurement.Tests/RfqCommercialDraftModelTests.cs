using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class RfqCommercialDraftModelTests
{
    private sealed class Scope:IProcurementScope
    {
        public string TenantId=>"model-only";public Guid LegalEntityId{get;}=Guid.NewGuid();public string SubjectId{get;}=Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId=>null;public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string? CorrelationId=>null;
    }
    [Fact]
    public void Npgsql_model_keeps_only_stable_scoped_identity_keys_and_short_explicit_names()
    {
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only_not_opened;Username=none;Password=none").Options,new Scope());
        var model=db.GetService<IDesignTimeModel>().Model;
        foreach(var type in new[]{typeof(PrincipalRfqCommercialDraftHead),typeof(PrincipalRfqCommercialDraftRevision),typeof(PrincipalRfqCommercialDraftReceipt),typeof(PrincipalRfqPreparedPlan),typeof(PrincipalRfqPreparationReceipt)})
        {
            var entity=model.FindEntityType(type)!;
            var ordinaryKeys=new[]{"Id","TenantId,LegalEntityId,Id"};
            var preparedOwnershipKeys=new[]{
                "TenantId,LegalEntityId,Id,SourcingEventId,BranchId,SnapshotSha256",
                "TenantId,LegalEntityId,Id,SourcingEventId,BranchId,DraftRevisionId,EvaluationRulesSha256"};
            Assert.All(entity.GetKeys(),key=>Assert.Contains(string.Join(",",key.Properties.Select(p=>p.Name)),
                type==typeof(PrincipalRfqPreparedPlan)?ordinaryKeys.Concat(preparedOwnershipKeys):ordinaryKeys));
            if(type==typeof(PrincipalRfqPreparedPlan))
                Assert.All(preparedOwnershipKeys,tuple=>Assert.Contains(entity.GetKeys(),key=>
                    string.Join(",",key.Properties.Select(p=>p.Name))==tuple));
            Assert.Equal(ValueGenerated.Never,entity.FindProperty("Id")!.ValueGenerated);
            Assert.All(entity.GetKeys(),key=>Assert.True(key.GetName()!.Length<=63));
            Assert.All(entity.GetIndexes(),index=>Assert.True(index.GetDatabaseName()!.Length<=63));
            Assert.All(entity.GetForeignKeys(),key=>Assert.Equal(new[]{"TenantId","LegalEntityId","Id"},
                key.PrincipalKey.Properties.Select(x=>x.Name)));
        }
        var original=model.FindEntityType(typeof(SourcingDraftEvidence))!;
        Assert.True(original.FindProperty(nameof(SourcingDraftEvidence.BranchRef))!.IsNullable);
        Assert.DoesNotContain(original.GetKeys(),key=>key.Properties.Any(x=>x.Name==nameof(SourcingDraftEvidence.BranchRef)));
    }
    [Fact]
    public void Exact_ownership_proposal_binds_source_actor_command_and_original_snapshot_without_CAS_EF_keys()
    {
        var sql=RfqCommercialDraftOwnershipConstraintSql.Install;
        Assert.Equal(4,sql.Split("ADD CONSTRAINT",StringSplitOptions.None).Length-1);
        Assert.Equal(4,sql.Split("DEFERRABLE INITIALLY DEFERRED",StringSplitOptions.None).Length-1);
        Assert.Contains("branch_ref,original_draft_fingerprint,payload_sha256",sql);
        Assert.Contains("branch_id,command_key,actor_principal_id,snapshot_sha256",sql);
        Assert.Contains("draft_id,revision,snapshot_sha256",sql);Assert.DoesNotContain("CASCADE",sql);
        var preparation=RfqPreparationOwnershipConstraintSql.Install;
        Assert.Equal(4,preparation.Split("ADD CONSTRAINT",StringSplitOptions.None).Length-1);
        Assert.Equal(4,preparation.Split("DEFERRABLE INITIALLY DEFERRED",StringSplitOptions.None).Length-1);
        Assert.Contains("published_stage,policy_rules_sha256,published_identity_sha256",preparation);
        Assert.Contains("actor_principal_id,command_key,request_sha256",preparation);
    }
    [Fact]
    public void Scope_and_append_only_guards_reject_changes_without_querying_unknown_source()
    {
        var scope=new Scope();using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,scope);
        var row=new PrincipalRfqCommercialDraftRevision{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,SnapshotJson="original",SnapshotSha256=new string('a',64),ActorPrincipalId=scope.SubjectId};
        db.Attach(row);db.Entry(row).Property(x=>x.SnapshotJson).CurrentValue="changed";
        var error=Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());Assert.Equal("RfqCommercialDraftEvidenceAppendOnly",error.Message);
        db.ChangeTracker.Clear();db.Add(new PrincipalRfqCommercialDraftHead{TenantId="foreign",LegalEntityId=scope.LegalEntityId});
        error=Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());Assert.Equal("RfqCommercialDraftScopeMismatch",error.Message);
        db.ChangeTracker.Clear();var prepared=new PrincipalRfqPreparedPlan{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            SnapshotSha256=new string('a',64),EvaluationRulesSha256=new string('b',64)};
        db.Attach(prepared);db.Remove(prepared);
        error=Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());Assert.Equal("RfqPreparationAppendOnly",error.Message);
    }
}
