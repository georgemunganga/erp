using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

internal sealed class PrincipalApprovalFixture(string owner, bool browserBuyerProfile, bool legacyWorkerProfile=false,
    bool principalRfqProfile=false,bool principalPoProfile=false,bool principalReceivingProfile=false,
    bool principalInvoiceProfile=false)
{
    internal bool PrincipalRfqProfile=>principalRfqProfile;
    internal bool PrincipalPoProfile=>principalPoProfile;
    internal bool PrincipalReceivingProfile=>principalReceivingProfile;
    internal bool PrincipalInvoiceProfile=>principalInvoiceProfile;
    internal const string Tenant="p25-private-http";
    internal readonly Guid Company=Guid.NewGuid(),Branch=Guid.NewGuid();
    internal readonly Guid Maker=Guid.NewGuid(),Checker=Guid.NewGuid(),Approver=Guid.NewGuid(),ConfigMaker=Guid.NewGuid(),ConfigReviewer=Guid.NewGuid(),Ungranted=Guid.NewGuid(),Buyer=Guid.NewGuid(),PoFinance=Guid.NewGuid(),PoApprover=Guid.NewGuid(),PoAck=Guid.NewGuid();
    internal readonly Guid Receiver=Guid.NewGuid(),ReceiverPeer=Guid.NewGuid(),Inspector=Guid.NewGuid(),Acceptor=Guid.NewGuid(),ServiceOwner=Guid.NewGuid(),ReceivingOutsider=Guid.NewGuid(),OtherReceivingBranch=Guid.NewGuid();
    internal readonly Guid InvoiceMaker=Guid.NewGuid(),InvoiceReviewer=Guid.NewGuid(),InvoiceMatcher=Guid.NewGuid();
    internal Guid[] Subjects=>principalInvoiceProfile?[Maker,Checker,Approver,ConfigMaker,ConfigReviewer,Ungranted,Buyer,PoFinance,PoApprover,PoAck,Receiver,ReceiverPeer,Inspector,Acceptor,ServiceOwner,ReceivingOutsider,InvoiceMaker,InvoiceReviewer,InvoiceMatcher]
        :principalReceivingProfile?[Maker,Checker,Approver,ConfigMaker,ConfigReviewer,Ungranted,Buyer,PoFinance,PoApprover,PoAck,Receiver,ReceiverPeer,Inspector,Acceptor,ServiceOwner,ReceivingOutsider]
        :principalPoProfile?[Maker,Checker,Approver,ConfigMaker,ConfigReviewer,Ungranted,Buyer,PoFinance,PoApprover,PoAck]
        :principalRfqProfile?[Maker,Checker,Approver,ConfigMaker,ConfigReviewer,Ungranted,Buyer]
        :[Maker,Checker,Approver,ConfigMaker,ConfigReviewer,Ungranted];
    internal PrincipalScope Scope=>new(Company,Branch,Maker);
    internal HrmDbContext Core()=>new(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(owner,n=>n.MigrationsHistoryTable("__hrm_migrations","hrm")).Options,new PrincipalTenant());
    internal ProcurementDbContext Procurement(string? connection=null)=>new(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection??owner,n=>n.MigrationsHistoryTable("__procurement_migrations","procurement")).Options,Scope);
    internal ApprovalDbContext Approvals()=>new(new DbContextOptionsBuilder<ApprovalDbContext>().UseNpgsql(owner,n=>n.MigrationsHistoryTable("__EFMigrationsHistory","core_approval")).Options,Scope);
    internal async Task BootstrapAsync()
    {
        await using(var c=new NpgsqlConnection(owner))
        {
            await c.OpenAsync();
            await using(var identity=new NpgsqlCommand("SELECT system_identifier::text='7698678713374847009' AND current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')='' FROM pg_control_system()",c))
                PrincipalApprovalHttpVerification.Need(await identity.ExecuteScalarAsync() is true,"Exact private PostgreSQL17 cluster and no TCP required before migrations or role DDL.");
            await using var q=new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' AND c.relkind IN ('r','p'))",c);
            PrincipalApprovalHttpVerification.Need(await q.ExecuteScalarAsync() is true,"Fresh existing empty database required; no reset/resume.");
        }
        await using(var core=Core())
        {
            PrincipalApprovalHttpVerification.Need(!core.Database.HasPendingModelChanges(),"Core model must match genuine migrations.");
            await core.Database.MigrateAsync();
            core.LegalEntities.Add(new LegalEntity {Id=Company,Code="P25_HTTP",RegisteredName="Private P25 company",Currency="ZMW",IsDefault=true});
            core.WorkLocations.Add(new WorkLocation {Id=Branch,Code="P25_MAIN",Name="Private P25 branch",LegalEntityId=Company});
            var people=new[]{(Maker,"p25_request_maker","procurement-access,procurement-request-read,procurement-request-create,procurement-request-edit,procurement-request-submit,procurement-request-return,procurement-approval-open"
                    +(legacyWorkerProfile?",procurement-authority-read,procurement-authority-configure":"")),
                (Checker,"p25_checker","procurement-access,procurement-request-read,procurement-budget-check"
                    +(legacyWorkerProfile?",procurement-authority-read,procurement-authority-review":"")),
                (Approver,"p25_custom_approver","procurement-access,procurement-request-read,procurement-approval-read,procurement-approval-decide"),
                (ConfigMaker,"p25_config_maker","procurement-access,procurement-configuration-read,procurement-configuration-edit,procurement-authority-read,procurement-authority-configure"
                    +(principalPoProfile?",procurement-vendor-create,procurement-vendor-edit":"")
                    + (browserBuyerProfile ? ",procurement-policy-evaluate,procurement-request-read" : "")),
                (ConfigReviewer,"p25_config_reviewer","procurement-access,procurement-configuration-read,procurement-configuration-approve,procurement-authority-read,procurement-authority-review"
                    +(principalPoProfile?",procurement-vendor-approve,procurement-po-finance-confirm":"")),
                (Ungranted,"p25_ungranted","")}.Concat(principalPoProfile
                ?new[]{(Buyer,"p30_po_buyer","procurement-access,procurement-po-convert,procurement-po-read,procurement-po-edit,procurement-po-submit,procurement-po-issue"),
                    (PoFinance,"p30_po_finance","procurement-access,procurement-po-finance-confirm"),
                    (PoApprover,"p30_po_approver","procurement-access,procurement-po-approve"),
                    (PoAck,"p30_po_ack","procurement-access,procurement-po-dispatch-confirm")}
                :principalRfqProfile?new[]{(Buyer,"p29_rfq_buyer","procurement-access,procurement-sourcing-convert,procurement-sourcing-cancel")}
                :Array.Empty<(Guid,string,string)>()).Concat(principalReceivingProfile?new[]{
                    (Receiver,"p30_receiving_actor","procurement-access,procurement-receipt-view,procurement-receipt-create,procurement-receipt-edit,procurement-receipt-record,procurement-receipt-inspect,procurement-receipt-accept"),
                    (ReceiverPeer,"p30_receiving_peer","procurement-access,procurement-receipt-view,procurement-receipt-create,procurement-receipt-edit,procurement-receipt-record"),
                    (Inspector,"p30_receiving_inspector","procurement-access,procurement-receipt-inspect"),
                    (Acceptor,"p30_receiving_acceptor","procurement-access,procurement-receipt-accept"),
                    (ServiceOwner,"p30_receiving_service_owner","procurement-access,procurement-service-certify"),
                    (ReceivingOutsider,"p30_receiving_outsider","procurement-access,procurement-receipt-view,procurement-receipt-create")}
                    :Array.Empty<(Guid,string,string)>()).Concat(principalInvoiceProfile?new[]{
                    (InvoiceMaker,"p31_invoice_maker","procurement-access,procurement-invoice-capture,procurement-invoice-read,procurement-invoice-match,procurement-invoice-unit-review-request"),
                    (InvoiceReviewer,"p31_invoice_reviewer","procurement-access,procurement-invoice-unit-review"),
                    (InvoiceMatcher,"p31_invoice_matcher","procurement-access,procurement-invoice-read,procurement-invoice-match")}
                    :Array.Empty<(Guid,string,string)>());
            foreach(var (id,role,actions) in people)
            {
                core.TenantRoleAssignments.Add(new TenantRoleAssignment {RoleKey=role,RoleName="Private "+role,Category="system",PermissionsCsv=actions,Active=true});
                core.LocalUsers.Add(new LocalUser {Id=id,WorkerId=null,Email=role+"@example.test",NormalizedEmail=role.ToUpperInvariant()+"@EXAMPLE.TEST",DisplayName=role,PasswordHash="private-no-real-login",RolesCsv=role,IsActive=true});
                core.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=id,LocationId=principalReceivingProfile&&id==ReceivingOutsider?OtherReceivingBranch:Branch});
            }
            if(principalReceivingProfile)core.WorkLocations.Add(new WorkLocation{Id=OtherReceivingBranch,Code="P30_RECEIVING_OTHER",Name="Other private receiving site",LegalEntityId=Company});
            if(legacyWorkerProfile)
                core.TenantRoleAssignments.Add(new TenantRoleAssignment {RoleKey="procurement_manager",
                    RoleName="Private legacy manager category",Category="system",PermissionsCsv="procurement-access",Active=true});
            await core.SaveChangesAsync();
            PrincipalApprovalHttpVerification.Need(!await core.Workers.AnyAsync(),"This milestone must require no employee fixture.");
        }
        await using(var approval=Approvals()) {PrincipalApprovalHttpVerification.Need(!approval.Database.HasPendingModelChanges(),"Core approval model drift.");await approval.Database.MigrateAsync();}
        await using(var procurement=Procurement()) {PrincipalApprovalHttpVerification.Need(!procurement.Database.HasPendingModelChanges(),"Procurement model drift.");await procurement.Database.MigrateAsync();}
        if(principalInvoiceProfile)
        {
            await using var documents=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
                .UseNpgsql(owner,p=>p.MigrationsHistoryTable("__EFMigrationsHistory","core_document")).Options,
                new PrincipalDocumentScope(Company,InvoiceMaker));
            PrincipalApprovalHttpVerification.Need(!documents.Database.HasPendingModelChanges(),"Core document model drift.");
            await documents.Database.MigrateAsync();
        }
    }
    internal sealed class PrincipalTenant:ITenantAccessor {public string GetTenantId()=>Tenant;}
    private sealed class PrincipalDocumentScope(Guid company,Guid actor):IDocumentScope
    {public string TenantId=>Tenant;public Guid LegalEntityId=>company;public string SubjectId=>actor.ToString("D");}
    internal sealed class PrincipalScope(Guid company,Guid branch,Guid maker):Mightyfin.Erp.Procurement.Application.IProcurementScope,IApprovalScope
    {
        public string TenantId=>Tenant;public Guid LegalEntityId=>company;public string SubjectId=>maker.ToString("D");
        public Guid? WorkLocationId=>branch;public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string? CorrelationId=>"private-p25";
        public Guid? PrincipalId=>maker;public Guid? WorkerId=>null;public Guid? CoreLinkedWorkerId=>null;
    }
}

internal sealed class PrincipalApprovalHost(PrincipalApprovalFixture fixture,string owner,string record,string execution,bool actualSchema,bool privateManualEvidence=false,bool normalLocalAuthentication=false,bool principalExecutionEnabled=true):WebApplicationFactory<MonthlyLeaveAccrualWorker>
{
    internal Dictionary<string,string?> Settings()
    {
        var settings=OfflineHost.Settings(owner,PrincipalApprovalFixture.Tenant);
        // Probe-only timing; model parity caching stays at its default off value.
        settings["Procurement:ReadinessPerformance:DiagnosticsEnabled"]="true";
        settings["Procurement:ReadinessPerformance:ModelParityCacheEnabled"]="false";
        settings["ConnectionStrings:Procurement"]=record;settings["ConnectionStrings:ProcurementApprovalExecution"]=execution;
        settings["Procurement:Approvals:Enabled"]="true";settings["Procurement:Approvals:PrincipalExecutionEnabled"]=principalExecutionEnabled?"true":"false";
        if(fixture.PrincipalRfqProfile)
        {
            settings["Procurement:SourcingDrafts:Enabled"]="true";
            settings["Procurement:SourcingDrafts:PrincipalSourceEnabled"]="true";
            if(Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RFQ_PUBLICATION")=="1")
                settings["Procurement:SourcingPublication:Enabled"]="true";
        }
        if(fixture.PrincipalPoProfile)
        {
            settings["Procurement:PrincipalDirectOrderDrafts:Enabled"]="true";
            settings["Procurement:Orders:Enabled"]="true";
            settings["Procurement:Orders:PrincipalExecutionEnabled"]="true";
        }
        if(fixture.PrincipalReceivingProfile)settings["Procurement:PrincipalReceiving:Enabled"]="true";
        if(fixture.PrincipalInvoiceProfile)
        {
            settings["Procurement:PrincipalInvoiceSources:Enabled"]="true";
            settings["Procurement:PrincipalInvoiceMatching:Enabled"]="true";
            settings["ConnectionStrings:CoreDocuments"]=execution;
            settings["CoreDocuments:Enabled"]="true";
            settings["CoreDocuments:PrivateDirectory"]=PrincipalApprovalHttpVerification.P31DocumentDirectory();
            settings["CoreDocuments:ScannerSocket"]=PrincipalApprovalHttpVerification.P31ScannerSocket();
        }
        return settings;
    }
    internal void PrepareEntryPointEnvironment()
    {
        var key="TEST_CONTENTROOT_"+typeof(MonthlyLeaveAccrualWorker).Assembly.GetName().Name!.ToUpperInvariant().Replace('.','_');
        Environment.SetEnvironmentVariable("ASPNETCORE_"+key,AppContext.BaseDirectory);
        foreach(var item in Settings())Environment.SetEnvironmentVariable(item.Key.Replace(":","__",StringComparison.Ordinal),item.Value);
    }
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        // Factory clients exercise the private cold host; browser/network clients
        // keep their separate tighter bounds. This is not a production SLA.
        client.Timeout=TimeSpan.FromSeconds(300);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(Settings()));
        builder.ConfigureTestServices(services=>
        {
            foreach(var entry in services.Where(x=>x.ServiceType==typeof(IHostedService)&&x.ImplementationType==typeof(MonthlyLeaveAccrualWorker)).ToArray())services.Remove(entry);
            if(services.Any(x=>x.ServiceType==typeof(IHostedService)&&x.ImplementationType?.Assembly==typeof(MonthlyLeaveAccrualWorker).Assembly))throw new InvalidOperationException("Unexpected application worker.");
            services.RemoveAll<ILoggerProvider>();
            var feature=services.Last(x=>x.ServiceType==typeof(ProcurementApprovalExecutionOptions)).ImplementationInstance as ProcurementApprovalExecutionOptions
                ?? throw new InvalidOperationException("Expected normal composed approval options instance.");
            // Explicit PRIVATE negative prerequisite, independent of inherited template policy arrays.
            feature.ManualFinancePolicies=privateManualEvidence
                ? [new ProcurementManualFinancePolicy {Enabled=true,LegalEntityId=fixture.Company,
                    ApprovedPolicyReference=PrincipalApprovalHttpVerification.PrivateManualPolicyReference,
                    EffectiveFrom=DateTimeOffset.UtcNow.AddMinutes(-1),EffectiveUntil=DateTimeOffset.UtcNow.AddHours(1),EvidenceMaximumAgeMinutes=30}]
                : [];
            if(fixture.PrincipalPoProfile)
            {
                var order=services.Last(x=>x.ServiceType==typeof(ProcurementOrderExecutionOptions)).ImplementationInstance as ProcurementOrderExecutionOptions
                    ??throw new InvalidOperationException("Expected normal composed order options instance.");
                order.ManualCommitmentPolicies=[new ProcurementManualFinancePolicy{Enabled=true,LegalEntityId=fixture.Company,
                    ApprovedPolicyReference="PRIVATE-P30-MANUAL-COMMITMENT-NOT-FINANCE-POSTING",
                    EffectiveFrom=DateTimeOffset.UtcNow.AddMinutes(-1),EffectiveUntil=DateTimeOffset.UtcNow.AddHours(1),EvidenceMaximumAgeMinutes=30}];
                order.ApprovedManualDispatchPolicyReference="PRIVATE-P30-OBSERVED-SUPPLIER-RESPONSE";
                order.AllowedDeliveryChannels=["ManualRecord"];
            }

            // Normal-cookie opt-in keeps Program's real local scheme untouched.
            // Existing callers retain the explicitly private subject substitution.
            if(!normalLocalAuthentication)
            {
                services.AddSingleton(new PrincipalApprovalSubjects(fixture.Subjects));
                services.AddAuthentication(o=>{o.DefaultAuthenticateScheme="private-p25-subject";o.DefaultChallengeScheme="private-p25-subject";})
                    .AddScheme<AuthenticationSchemeOptions,PrincipalSubjectAuthentication>("private-p25-subject",_=>{});
            }
            // Positive runs use ordinary Program registration. This explicit
            // negative control alone substitutes the unavailable provider.
            if(!actualSchema) {services.RemoveAll<IProcurementPrincipalApprovalSchemaReadiness>();services.AddSingleton<IProcurementPrincipalApprovalSchemaReadiness,UnavailableProcurementPrincipalApprovalSchemaReadiness>();}
        });
    }
}
internal sealed record PrincipalApprovalSubjects(Guid[] Values);
internal sealed class PrincipalSubjectAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,PrincipalApprovalSubjects subjects)
    :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Guid.TryParseExact(Request.Headers["X-Private-P25-Subject"],"D",out var id)||!subjects.Values.Contains(id))return Task.FromResult(AuthenticateResult.NoResult());
        // TEST-ONLY identity-provider substitution: no action/role/worker claims supplied.
        // Actual Program's Core refresh/scope/authorization still resolve current stored grants.
        var identity=new ClaimsIdentity([new Claim("sub",id.ToString("D")),new Claim("tenant",PrincipalApprovalFixture.Tenant)],Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
    }
}
