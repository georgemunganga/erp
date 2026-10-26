using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Tests;
public sealed partial class FinanceCheckerIdentityEvidenceTests
{
    [Fact]
    public void Principal_checker_without_employee_retains_exact_finance_tuple_and_cannot_relabel_as_legacy()
    {
        var f=Snapshot();f.CheckedBySubjectId=Guid.NewGuid().ToString("D");var legacy=FinanceSnapshotCodec.Legacy(f);
        var binding=FinanceCheckerIdentityEvidence.CreatePrincipalForNewSnapshot(f,f.CheckedBySubjectId,null);
        f.CheckerIdentitySchemaVersion=binding.SchemaVersion;f.CheckedByWorkerId=binding.WorkerId;f.CheckerIdentitySha256=binding.Sha256;
        Assert.Equal(2,binding.SchemaVersion);Assert.Null(binding.WorkerId);Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(f));
        Assert.Equal(legacy,FinanceSnapshotCodec.Legacy(f));
        Assert.True(FinanceCheckerIdentityEvidence.MatchesCurrentCanonical(f,f.TenantId,f.LegalEntityId,f.PurchaseRequestId,f.RequestVersion,f.CheckedBySubjectId,null));
        var exact=FinanceSnapshotCodec.Encode(f,2);Assert.True(FinanceSnapshotCodec.Matches(f,2,exact,ProcurementEvidenceEncoding.Digest(exact)));
        Assert.Throws<ArgumentException>(()=>FinanceSnapshotCodec.Encode(f,1));Assert.Throws<ArgumentException>(()=>FinanceSnapshotCodec.Encode(f,0));
        f.CheckerIdentitySchemaVersion=1;Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
    }
    [Theory]
    [InlineData("principal")][InlineData("alias")][InlineData("amount")][InlineData("businessVersion")]
    public void Principal_checker_digest_binds_original_identity_and_complete_exact_tuple(string fault)
    {
        var f=Snapshot();f.CheckedBySubjectId=Guid.NewGuid().ToString("D");var b=FinanceCheckerIdentityEvidence.CreatePrincipalForNewSnapshot(f,f.CheckedBySubjectId,Guid.NewGuid());
        f.CheckerIdentitySchemaVersion=2;f.CheckedByWorkerId=b.WorkerId;f.CheckerIdentitySha256=b.Sha256;
        if(fault=="principal")f.CheckedBySubjectId=Guid.NewGuid().ToString("D");if(fault=="alias")f.CheckedByWorkerId=null;
        if(fault=="amount")f.CheckedAmount+=0.0001m;if(fault=="businessVersion")f.RequestVersion++;
        Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(f));
    }
}
public sealed partial class SpendingAuthorityIdentityTests
{
    private static (ProcurementSpendingAuthorityGrant Grant,SpendingAuthorityIdentityEvidence Created,SpendingAuthorityIdentityEvidence Submitted) PrincipalPending(Guid? makerAlias=null)
    {
        var grant=SpendingAuthorityConfiguration.CreateDraft("AUTH-PRINCIPAL",Recipient,
            new("ZMW","EstimatedLineBase",100,Now.AddDays(-1),Now.AddDays(1),AllBranches:true,AllDepartments:true,AllCategories:true),
            Actor(Maker,SpendingAuthorityConfiguration.ConfigurePermission),Now,[]);
        var created=SpendingAuthorityIdentity.CaptureCreation(grant,makerAlias,null,2);
        SpendingAuthorityConfiguration.Submit(grant,grant.Version,Actor(Maker,SpendingAuthorityConfiguration.ConfigurePermission),Subject(),Now.AddMinutes(1));
        var submitted=SpendingAuthorityIdentity.CaptureSubmission(grant,created,Maker,null,null);
        return(grant,created,submitted);
    }
    [Fact]
    public void Workerless_independent_Core_principals_preserve_full_authority_lifecycle_and_business_terms()
    {
        var(g,c,s)=PrincipalPending();var fingerprint=SpendingAuthorityConfiguration.Fingerprint(g);
        SpendingAuthorityIdentity.RequireIndependentReviewer(g,c,s,Reviewer,null);Activate(g);
        var a=SpendingAuthorityIdentity.CaptureActivation(g,c,s,Reviewer,null,null);SpendingAuthorityIdentity.VerifyActivation(g,c,s,a);
        Assert.Equal(2,a.SchemaVersion);Assert.Null(a.ActorWorkerId);Assert.Null(a.RecipientWorkerId);
        Assert.Equal(fingerprint,g.PublishedBusinessFingerprint);Assert.Equal(a,SpendingAuthorityIdentityStorage.Read(SpendingAuthorityIdentityStorage.ToRecord(a)));
    }
    [Theory]
    [InlineData("maker")][InlineData("recipient")][InlineData("knownAlias")][InlineData("zeroAlias")]
    public void Principal_authority_denies_self_or_retained_known_alias_even_after_current_maker_alias_is_absent(string fault)
    {
        var(g,c,s)=PrincipalPending(MakerWorker);
        var principal=fault=="maker"?Maker:fault=="recipient"?Recipient:Reviewer;
        Guid? alias=fault=="knownAlias"?MakerWorker:fault=="zeroAlias"?Guid.Empty:null;
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.RequireIndependentReviewer(g,c,s,principal,alias));
    }
    [Theory]
    [InlineData("PurchaseOrder.Approve","procurement-order-approve")]
    [InlineData("SourcingEvent.Publish","procurement-approval-decide")]
    public void Principal_authority_successor_is_not_silently_reused_by_other_operational_families(string action,string permission)
    {
        var(_,created,_)=PrincipalPending();var changed=created with {Action=action,Permission=permission};
        changed=changed with {EvidenceSha256=SpendingAuthorityIdentity.Digest(changed)};
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.Verify(changed));
    }

    [Fact]
    public void Rfq_publication_authority_requires_its_exact_sourcing_action_and_permission_pair()
    {
        var (_, created, _) = PrincipalPending();
        var sourcing = created with
        {
            Action = "SourcingEvent.Publish", Permission = "procurement-sourcing-publish",
        };
        sourcing = sourcing with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(sourcing) };
        SpendingAuthorityIdentity.Verify(sourcing);
        var wrongPermission = sourcing with { Permission = "procurement-approval-decide" };
        wrongPermission = wrongPermission with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(wrongPermission) };
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.Verify(wrongPermission));
    }

    [Fact]
    public void Authority_lineage_cannot_downgrade_or_mix_worker_and_principal_versions()
    {
        var(g,c,s)=PrincipalPending();var bad=s with {SchemaVersion=1,ActorWorkerId=MakerWorker,RecipientWorkerId=RecipientWorker};
        bad=bad with {EvidenceSha256=SpendingAuthorityIdentity.Digest(bad)};
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.VerifySubmission(g,c,bad));
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.CaptureCreation(
            SpendingAuthorityConfiguration.CreateDraft("AUTH-OLD",Recipient,new("ZMW","EstimatedLineBase",100,Now.AddDays(-1),Now.AddDays(1),AllBranches:true,AllDepartments:true,AllCategories:true),Actor(Maker,SpendingAuthorityConfiguration.ConfigurePermission),Now,[]),null,null));
    }
}
public sealed partial class PurchaseRequestApprovalPinningTests
{
    private static Fixture PrincipalFixture()
    {
        var f=new Fixture();var principal=Guid.NewGuid();f.Request.Status="Draft";f.Request.Version=1;
        f.Request.CreatedBy=principal.ToString("D");f.Request.RequesterPrincipalId=principal;f.Request.RequesterWorkerId=null;f.Request.SourceChannel="ERP";
        PurchaseRequestPrincipalIdentity.CaptureNew(f.Request,principal.ToString("D"),null);
        f.Request.Status="Submitted";f.Request.Version=4;
        f.Finance.CheckedBySubjectId=Guid.NewGuid().ToString("D");var b=FinanceCheckerIdentityEvidence.CreatePrincipalForNewSnapshot(f.Finance,f.Finance.CheckedBySubjectId,null);
        f.Finance.CheckerIdentitySchemaVersion=2;f.Finance.CheckedByWorkerId=b.WorkerId;f.Finance.CheckerIdentitySha256=b.Sha256;
        return f;
    }
    private static PurchaseRequestApprovalSubmission PrincipalPin(Fixture f)=>PurchaseRequestApprovalPinning.PinPrincipal(f.Request,f.Evaluation,f.Policy,f.Finance,
        Fixture.Tenant,f.Company,Guid.NewGuid(),Guid.NewGuid(),PurchaseRequestApprovalPinning.ApprovedManualEvidence,"Synthetic policy reference; not external Finance proof",f.Now.AddMinutes(1),null);
    [Fact]
    public void Principal_request_pin_without_employees_binds_immutable_original_and_real_business_evidence()
    {
        var f=PrincipalFixture();var pin=PrincipalPin(f);f.Link(pin);
        Assert.Equal(3,pin.SnapshotSchemaVersion);Assert.True(PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(pin));
        Assert.False(PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin));Assert.Null(pin.MakerWorkerId);Assert.Null(pin.RequesterWorkerId);
        Assert.True(f.Verify(pin).IsValid,string.Join(',',f.Verify(pin).Blockers));
        Assert.Throws<ArgumentException>(()=>f.Pin());
    }
    [Fact]
    public void Principal_readiness_uses_explicit_successor_finance_and_never_relabels_a_legacy_worker_path()
    {
        var f=PrincipalFixture();
        var readiness=RequestApprovalReadiness.CheckPrincipal(f.Request,f.Evaluation,f.Policy,f.Finance);
        Assert.DoesNotContain("PrincipalApprovalOriginalIdentityRequired",readiness.Blockers);
        Assert.DoesNotContain("PrincipalFinanceCheckerEvidenceRequired",readiness.Blockers);
        Assert.Contains("PrincipalApprovalEvidenceRequired",RequestApprovalReadiness.Check(f.Request,f.Evaluation,f.Policy,f.Finance).Blockers);
        f.Finance.CheckerIdentitySchemaVersion=0;
        Assert.Contains("PrincipalFinanceCheckerEvidenceRequired",RequestApprovalReadiness.CheckPrincipal(f.Request,f.Evaluation,f.Policy,f.Finance).Blockers);
    }

    [Theory]
    [InlineData("originalAlias")][InlineData("principal")][InlineData("pinDowngrade")][InlineData("financeDowngrade")]
    public void Principal_pin_never_infers_missing_original_identity_or_accepts_a_version_downgrade(string fault)
    {
        var f=PrincipalFixture();var pin=PrincipalPin(f);f.Link(pin);
        if(fault=="originalAlias")f.Request.RequesterCoreWorkerId=Guid.NewGuid();if(fault=="principal")f.Request.RequesterPrincipalId=Guid.NewGuid();
        if(fault=="financeDowngrade")f.Finance.CheckerIdentitySchemaVersion=1;
        if(fault=="pinDowngrade") {var node=JsonNode.Parse(JsonSerializer.Serialize(pin))!;node["SnapshotSchemaVersion"]=2;pin=node.Deserialize<PurchaseRequestApprovalSubmission>()!;}
        Assert.False(f.Verify(pin).IsValid);
    }
}
