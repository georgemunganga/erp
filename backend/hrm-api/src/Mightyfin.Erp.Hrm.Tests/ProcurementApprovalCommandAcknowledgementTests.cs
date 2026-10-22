using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real mapped route/current Core grants, synthetic owner projections and read-schema
/// status only. Not an executed approval, finite PostgreSQL, migration or Finance proof.</summary>
public sealed class ProcurementApprovalCommandAcknowledgementTests
{
    [Theory][InlineData("Open")][InlineData("Decide")]
    public async Task Historical_actor_owned_ack_is_read_only_after_new_round_and_without_execution_or_Finance_authority(string operation)
    {
        await using var f=await Fixture.Start(operation);var before=await f.Footprint();
        var response=await f.Get();Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("no-store",response.Headers.CacheControl?.ToString());
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1,body.GetProperty("acknowledgementSchemaVersion").GetInt32());Assert.Equal("Completed",body.GetProperty("acknowledgement").GetString());
        Assert.Equal(operation,body.GetProperty("operation").GetString());Assert.Equal(f.Key,body.GetProperty("commandKey").GetGuid());
        Assert.Equal(f.Request,body.GetProperty("requestId").GetGuid());Assert.Equal(2,body.GetProperty("result").GetProperty("submittedVersion").GetInt64());
        Assert.Equal(operation=="Open"?3:4,body.GetProperty("result").GetProperty("requestVersion").GetInt64());
        Assert.Equal(operation=="Open"?"Pending":"Approved",body.GetProperty("result").GetProperty("state").GetString());
        Assert.DoesNotContain("ResponseJson",await response.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
        Assert.False(f.Options.ExecutionDatabaseConfigured);Assert.Empty(f.Options.ManualFinancePolicies);
        Assert.Equal(before,await f.Footprint());Assert.True(f.Readiness.ReadCalls>0);Assert.Equal(0,f.Readiness.WriteCalls);
    }

    [Fact]
    public async Task Original_independently_granted_opener_can_ack_without_becoming_request_maker()
    {
        await using var f=await Fixture.Start("Open","separate-opener");var before=await f.Footprint();
        Assert.Equal(HttpStatusCode.OK,(await f.Get()).StatusCode);Assert.Equal(before,await f.Footprint());
    }

    [Theory][InlineData("Open")][InlineData("Decide")]
    public async Task Current_operation_grant_is_required_even_for_original_actor_and_restored_grant_recovers_same_ack(string operation)
    {
        await using var f=await Fixture.Start(operation);await f.Grant(false);var before=await f.Footprint();
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get()).StatusCode);Assert.Equal(before,await f.Footprint());
        await f.Grant(true);before=await f.Footprint();Assert.Equal(HttpStatusCode.OK,(await f.Get()).StatusCode);
        Assert.Equal(before,await f.Footprint());
    }

    [Theory]
    [InlineData("actor",404)][InlineData("key",404)][InlineData("request",404)]
    [InlineData("company",403)][InlineData("branch",403)][InlineData("locked",403)]
    public async Task Other_actor_key_request_or_current_Core_scope_cannot_disclose_receipt(string variant,int expected)
    {
        await using var f=await Fixture.Start("Decide");
        if(variant=="locked")await f.LockActor();var before=await f.Footprint();
        var response=await f.Get(variant);Assert.Equal((HttpStatusCode)expected,response.StatusCode);
        Assert.DoesNotContain("acknowledgementSchemaVersion",await response.Content.ReadAsStringAsync());Assert.Equal(before,await f.Footprint());
    }

    [Theory][InlineData("off")][InlineData("principal-off")][InlineData("unavailable")][InlineData("missing-provider")]
    public async Task Default_or_unavailable_read_contract_remains_closed_without_writes(string scenario)
    {
        await using var f=await Fixture.Start("Open",scenario);var before=await f.Footprint();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Get()).StatusCode);Assert.Equal(before,await f.Footprint());
    }

    [Theory]
    [InlineData("extra")][InlineData("duplicate")][InlineData("wrong-type")][InlineData("zero-version")]
    [InlineData("unknown-state")][InlineData("wrong-result-id")][InlineData("wrong-case-source")]
    [InlineData("wrong-decision-key")][InlineData("missing-outcome")][InlineData("unsupported-pin")]
    [InlineData("bad-source-hash")][InlineData("source-branch")]
    public async Task Corrupt_or_unsupported_receipt_projection_is_never_a_successful_ack(string fault)
    {
        await using var f=await Fixture.Start("Decide",fault);var before=await f.Footprint();
        var response=await f.Get();Assert.Equal(fault=="source-branch"?HttpStatusCode.NotFound:HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal(before,await f.Footprint());
    }

    [Theory][InlineData("Release")][InlineData("open")][InlineData("zero")]
    public async Task Invalid_ack_inputs_do_not_query_or_mutate_receipts(string variant)
    {
        await using var f=await Fixture.Start("Open");var before=await f.Footprint();
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await f.Get(variant)).StatusCode);Assert.Equal(before,await f.Footprint());
    }

    private sealed class ReadStatus: IProcurementPrincipalApprovalSchemaReadiness
    {
        public int ReadCalls,WriteCalls;
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct){ReadCalls++;return Task.FromResult(true);}
        public Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct){WriteCalls++;throw new InvalidOperationException("Read acknowledgement must not require recording.");}
        public Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct){WriteCalls++;throw new InvalidOperationException("Read acknowledgement must not require execution.");}
    }
    private sealed class Fixture(WebApplication app,string operation,Guid company,Guid branch,Guid otherBranch,Guid otherCompany,
        Guid opener,Guid decider,Guid stranger,Guid request,Guid key,ReadStatus readiness,ProcurementApprovalExecutionOptions options):IAsyncDisposable
    {
        public Guid Request=>request;public Guid Key=>key;public ReadStatus Readiness=>readiness;public ProcurementApprovalExecutionOptions Options=>options;
        private Guid Actor=>operation=="Open"?opener:decider;
        private HttpClient Client {get;}=app.GetTestClient();
        public Task<HttpResponseMessage> Get(string? variant=null)
        {
            var op=variant is "Release" or "open"?variant:operation;
            var path=$"/api/procurement/v1/requests/{(variant=="request"?Guid.NewGuid():request)}/approval-commands/{op}/{(variant=="zero"?Guid.Empty:variant=="key"?Guid.NewGuid():key)}";
            var message=new HttpRequestMessage(HttpMethod.Get,path);
            message.Headers.Add("X-Test-User",(variant=="actor"?stranger:Actor).ToString("D"));
            message.Headers.Add("X-Test-Role","erp_admin"); // Deliberately stale: actual stored custom grants win.
            message.Headers.Add("X-Shell-Entity",(variant=="company"?otherCompany:company).ToString("D"));
            message.Headers.Add("X-Shell-Location",(variant=="branch"?otherBranch:branch).ToString("D"));
            return Client.SendAsync(message);
        }
        public async Task Grant(bool grant)
        {
            using var services=app.Services.CreateScope();var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role=await core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey==(operation=="Open"?"ack_opener":"ack_decider"));
            role.PermissionsCsv="procurement-access"+(grant?",procurement-approval-"+(operation=="Open"?"open":"decide"):"");await core.SaveChangesAsync();
        }
        public async Task LockActor()
        {
            using var services=app.Services.CreateScope();var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var user=await core.LocalUsers.SingleAsync(x=>x.Id==Actor);user.LockedUntil=DateTimeOffset.UtcNow.AddDays(1);await core.SaveChangesAsync();
        }
        public async Task<string> Footprint()
        {
            using var services=app.Services.CreateScope();var scope=services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            scope.Set("tenant-a",company,Actor.ToString("D"),null,null,false,"ack-footprint");scope.AssignPrincipal(Actor);
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();var workflow=services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            return JsonSerializer.Serialize(new {
                requests=await db.PurchaseRequests.AsNoTracking().ToArrayAsync(),pins=await db.ApprovalSubmissions.AsNoTracking().ToArrayAsync(),
                assignments=await db.AssignmentAuthorityEvidence.AsNoTracking().ToArrayAsync(),receipts=await db.ApprovalCommandReceipts.AsNoTracking().ToArrayAsync(),
                outcomes=await db.ApprovalOutcomes.AsNoTracking().ToArrayAsync(),audit=await db.AuditEvents.AsNoTracking().ToArrayAsync(),outbox=await db.OutboxEvents.AsNoTracking().ToArrayAsync(),
                cases=await workflow.Cases.AsNoTracking().ToArrayAsync(),stages=await workflow.Stages.AsNoTracking().ToArrayAsync(),
                coreAssignments=await workflow.Assignments.AsNoTracking().ToArrayAsync(),decisions=await workflow.Decisions.AsNoTracking().ToArrayAsync(),
                users=await core.LocalUsers.AsNoTracking().ToArrayAsync(),roles=await core.TenantRoleAssignments.AsNoTracking().ToArrayAsync(),
                branches=await core.UserBranchAssignments.AsNoTracking().ToArrayAsync()});
        }
        public static async Task<Fixture> Start(string operation,string? fault=null)
        {
            var name=Guid.NewGuid().ToString("N");var root=new InMemoryDatabaseRoot();var workflowRoot=new InMemoryDatabaseRoot();
            var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
            builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(o=>o.UseInMemoryDatabase("ack-core-"+name));
            builder.Services.AddScoped<ProcurementRequestScope>();builder.Services.AddScoped<IProcurementScope>(s=>s.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddScoped<IApprovalScope>(s=>s.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(o=>o.UseInMemoryDatabase("ack-proc-"+name,root));
            builder.Services.AddDbContext<ApprovalDbContext>(o=>o.UseInMemoryDatabase("ack-workflow-"+name,workflowRoot));
            var options=new ProcurementApprovalExecutionOptions {Enabled=fault!="off",PrincipalExecutionEnabled=fault!="principal-off",ExecutionDatabaseConfigured=false};
            var readiness=new ReadStatus();builder.Services.AddSingleton(options);
            if(fault=="unavailable")builder.Services.AddSingleton<IProcurementPrincipalApprovalSchemaReadiness,UnavailableProcurementPrincipalApprovalSchemaReadiness>();
            else if(fault!="missing-provider")builder.Services.AddSingleton<IProcurementPrincipalApprovalSchemaReadiness>(readiness);
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,ProcurementRequestHttpTests.TestAuthenticationHandler>("test",_=>{});
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseMiddleware<ProcurementCorePrincipalRefreshMiddleware>();
            app.UseAuthorization();app.UseMiddleware<ProcurementScopeMiddleware>();ProcurementApprovalExecutionRoutes.MapCommandAcknowledgements(app);
            var company=new LegalEntity {Code="ACK",RegisteredName="Synthetic ack company",Currency="ZMW"};
            var otherCompany=new LegalEntity {Code="OTHER",RegisteredName="Other company",Currency="USD"};
            var branch=new WorkLocation {Code="HERE",Name="Here",LegalEntityId=company.Id};
            var otherBranch=new WorkLocation {Code="OTHER",Name="Other branch",LegalEntityId=company.Id};
            var opener=Guid.NewGuid();var decider=Guid.NewGuid();var stranger=Guid.NewGuid();var openKey=Guid.NewGuid();var decideKey=Guid.NewGuid();
            using(var services=app.Services.CreateScope())
            {
                var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();core.LegalEntities.AddRange(company,otherCompany);core.WorkLocations.AddRange(branch,otherBranch);
                foreach(var (id,role,actions) in new[]{(opener,"ack_opener","procurement-access,procurement-approval-open"),
                    (decider,"ack_decider","procurement-access,procurement-approval-decide"),(stranger,"ack_stranger","procurement-access,procurement-approval-open,procurement-approval-decide")})
                {
                    core.LocalUsers.Add(new LocalUser {Id=id,WorkerId=null,RolesCsv=role,IsActive=true,Email=role+"@example.test",NormalizedEmail=role.ToUpperInvariant()+"@EXAMPLE.TEST",DisplayName=role,PasswordHash="not-a-real-login"});
                    core.TenantRoleAssignments.Add(new TenantRoleAssignment {RoleKey=role,RoleName=role,PermissionsCsv=actions,Active=true});
                    core.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=id,LocationId=branch.Id});
                }
                await core.SaveChangesAsync();
            }
            var r=new PurchaseRequest {TenantId="tenant-a",LegalEntityId=company.Id,CreatedBy=opener.ToString("D"),RequesterPrincipalId=opener,
                Number="ACK-ONLY",Purpose="Owner projection, not executed approval",CurrencyCode="ZMW",BranchRef=branch.Id,SourceChannel="ERP"};
            PurchaseRequestPrincipalIdentity.CaptureNew(r,opener.ToString("D"),null);
            r.Status="Submitted";r.SubmittedVersion=8;r.Version=20;r.CreatedAt=DateTimeOffset.UtcNow.AddDays(-1);
            var c=new ApprovalCase {TenantId="tenant-a",LegalEntityId=company.Id,SourceModule="procurement",SourceType="purchase_request",SourceId=r.Id,
                SourceVersion=fault=="wrong-case-source"?7:2,PolicyVersionId=Guid.NewGuid(),PolicyVersionNumber=1,MakerSubjectId=r.CreatedBy,State="Approved",Version=10,
                OpenedAt=DateTimeOffset.UtcNow.AddHours(-2),CompletedAt=DateTimeOffset.UtcNow.AddHours(-1)};
            var pinId=Guid.NewGuid();
            var sourceJson=JsonSerializer.Serialize(new {schemaVersion=3,RequesterPrincipalId=opener,RequesterCoreWorkerId=(Guid?)null,RequesterIdentitySha256=r.RequesterIdentitySha256,legacyBusiness=new {schemaVersion=1,Id=r.Id,TenantId="tenant-a",LegalEntityId=company.Id,
                SubmittedVersion=2,CreatedBy=r.CreatedBy,BranchRef=fault=="source-branch"?otherBranch.Id:branch.Id,DepartmentRef=(Guid?)null}});
            var pin=new PurchaseRequestApprovalSubmission {Id=pinId,TenantId="tenant-a",LegalEntityId=company.Id,SnapshotSchemaVersion=fault=="unsupported-pin"?2:3,
                PurchaseRequestId=r.Id,CoreApprovalCaseId=c.Id,OpeningCommandKey=openKey,SubmittedBusinessVersion=2,MakerSubjectId=r.CreatedBy,RequesterPrincipalId=opener,
                RequesterIdentitySha256=r.RequesterIdentitySha256,PrincipalIdentitySha256=PurchaseRequestApprovalPinning.PrincipalIdentityDigest("tenant-a",company.Id,r.Id,2,c.Id,r.CreatedBy,opener,null,null,r.RequesterIdentitySha256!),
                RequestSnapshotJson=sourceJson,RequestSnapshotSha256=fault=="bad-source-hash"?new string('0',64):ProcurementEvidenceEncoding.Digest(sourceJson),
                PolicyVersionId=c.PolicyVersionId,PolicyVersionNumber=1,PolicyEvaluationId=Guid.NewGuid(),PolicyDecisionSha256=new string('a',64),PolicyRulesSha256=new string('a',64),
                FinanceSnapshotId=Guid.NewGuid(),FinanceIdentitySchemaVersion=2,FinanceSnapshotJson="{}",FinanceSnapshotSha256=ProcurementEvidenceEncoding.Digest("{}"),
                FinanceEvidenceMode=PurchaseRequestApprovalPinning.ApprovedManualEvidence,ManualEvidenceAuthorityRef="SYNTHETIC-PROJECTION-NOT-FINANCE",AmountBasis="EstimatedLineBase",EstimatedAmount=1,CurrencyCode="ZMW",PinnedAt=c.OpenedAt};
            var stage=new ApprovalStage {TenantId="tenant-a",LegalEntityId=company.Id,CaseId=c.Id,Sequence=1,Code="REVIEW",State="Approved"};
            var assigned=new ApprovalAssignment {TenantId="tenant-a",LegalEntityId=company.Id,CaseId=c.Id,StageId=stage.Id,AssigneeSubjectId=decider.ToString("D"),PermissionKey=ProcurementApprovalExecutionRoutes.DecidePermission,State="Approved"};
            var decision=new ApprovalDecision {TenantId="tenant-a",LegalEntityId=company.Id,CaseId=c.Id,StageId=stage.Id,AssignmentId=assigned.Id,ActorSubjectId=decider.ToString("D"),Action="Approve",IdempotencyKey=(fault=="wrong-decision-key"?Guid.NewGuid():decideKey).ToString("D"),DecidedAt=c.CompletedAt!.Value};
            var authority=new PurchaseRequestAuthorityEvidence(Guid.NewGuid(),1,"SYNTHETIC-RETAINED-NOT-CURRENT",1,"tenant-a",company.Id,decider.ToString("D"),"procurement-approval-decide","PurchaseRequest.Approve","ZMW","EstimatedLineBase",1,1,c.OpenedAt,null,c.OpenedAt,false,branch.Id,true,null,true,[]);
            var bindingJson=JsonSerializer.Serialize(new PurchaseRequestPrincipalAssignmentEvidence(2,"tenant-a",company.Id,pinId,c.Id,stage.Id,assigned.Id,decider.ToString("D"),null,"ProcurementManager",authority));
            var binding=new PurchaseRequestAssignmentAuthorityEvidence {TenantId="tenant-a",LegalEntityId=company.Id,IdentitySchemaVersion=2,SubmissionId=pinId,CoreApprovalCaseId=c.Id,CoreStageId=stage.Id,CoreAssignmentId=assigned.Id,PrincipalId=decider.ToString("D"),AssigneeKind="ProcurementManager",GrantId=authority.GrantId,GrantRevision=1,AuthorityVersion=1,EvidenceJson=bindingJson,EvidenceSha256=ProcurementEvidenceEncoding.Digest(bindingJson)};
            var outcome=new PurchaseRequestApprovalOutcome {TenantId="tenant-a",LegalEntityId=company.Id,SubmissionId=pinId,PurchaseRequestId=r.Id,SubmittedBusinessVersion=2,CoreApprovalCaseId=c.Id,CoreDecisionId=decision.Id,DecisionCommandKey=decideKey.ToString("D"),Outcome="Approved",EffectKey="synthetic-recovery-projection",ActorSubjectId=decider.ToString("D"),AppliedAt=decision.DecidedAt};
            var original=JsonSerializer.Serialize(new {caseId=c.Id,requestId=r.Id,submittedVersion=2,requestVersion=operation=="Open"?3:4,caseVersion=operation=="Open"?1:2,state=operation=="Open"?"Pending":"Approved",requestState=operation=="Open"?"Submitted":"Approved",approvalState=operation=="Open"?"Pending":"Approved"});
            var node=JsonNode.Parse(original)!.AsObject();
            if(fault=="extra")node["unownedSecret"]="must-not-be-returned";if(fault=="wrong-type")node["requestVersion"]="4";
            if(fault=="zero-version")node["caseVersion"]=0;if(fault=="unknown-state")node["state"]="Invented";
            if(fault=="wrong-result-id")node["requestId"]=Guid.NewGuid();original=node.ToJsonString();
            if(fault=="duplicate")original=original.Insert(1,"\"caseVersion\":9,");
            var receipt=new ProcurementApprovalCommandReceipt {TenantId="tenant-a",LegalEntityId=company.Id,Operation=operation,CommandKey=operation=="Open"?openKey:decideKey,
                ActorSubjectId=(operation=="Open"?(fault=="separate-opener"?stranger:opener):decider).ToString("D"),PurchaseRequestId=r.Id,CoreApprovalCaseId=c.Id,CoreAssignmentId=operation=="Open"?null:assigned.Id,
                CommandFingerprint=new string('a',64),ResponseJson=original,CompletedAt=DateTimeOffset.UtcNow.AddMinutes(-30)};
            foreach(var fact in new ProcurementRecord[]{pin,binding,receipt,outcome})
            {fact.CreatedBy=opener.ToString("D");fact.CreatedAt=c.OpenedAt;}
            using(var services=app.Services.CreateScope())
            {
                var scope=services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();scope.Set("tenant-a",company.Id,opener.ToString("D"),null,null,false,"synthetic-read-projection");scope.AssignPrincipal(opener);
                var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();var workflow=services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
                // Explicit owner projection fixture only. Never calls a workflow command or claims
                // seeded rows prove Finance, authority activation, PostgreSQL or execution readiness.
                await using var raw=new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase("ack-proc-"+name,root).UseModel(db.Model).Options);
                raw.AddRange(r,pin,binding,receipt);if(fault!="missing-outcome")raw.Add(outcome);await raw.SaveChangesAsync();
                await using var coreRaw=new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase("ack-workflow-"+name,workflowRoot).UseModel(workflow.Model).Options);
                coreRaw.AddRange(c,stage,assigned,decision);await coreRaw.SaveChangesAsync();
            }
            await app.StartAsync();return new(app,operation,company.Id,branch.Id,otherBranch.Id,otherCompany.Id,fault=="separate-opener"?stranger:opener,decider,stranger,r.Id,receipt.CommandKey,readiness,options);
        }
        public async ValueTask DisposeAsync(){Client.Dispose();await app.DisposeAsync();}
    }
}
