using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using NpgsqlTypes;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    const string Tenant = "crud-private-probe", Root = "/api/procurement/v1";
    const string P19 = "P19PurchaseOrderBranchScope", P20 = "P20InvoiceDraftMaintenance";
    const string CanonicalP21 = "20261022052824_P21CanonicalGovernedProcurementFoundation";
    static readonly Guid Company = Guid.NewGuid(), ForeignCompany = Guid.NewGuid(), Branch = Guid.NewGuid();
    static readonly Guid Actor = Guid.NewGuid(), Other = Guid.NewGuid(), Worker = Guid.NewGuid(), OtherWorker = Guid.NewGuid();
    static readonly JsonSerializerOptions Snake = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    static string connection = "", stage = "input"; static int assertions, requests;
    static Guid approvedSupplier, legacyInvoice;
    const string BrowserSocket = "/crud-browser-socket/api.sock", BrowserManifest = "/crud-browser-socket/fixture.json";
    static bool browserMode, publishManifest, recurringCrudMode;
    public static async Task<int> Main(string[] args)
    {
        try
        {
            connection = Environment.GetEnvironmentVariable("ConnectionStrings__Procurement") ?? throw new ProbeFailure("Explicit private owner connection required.");
            var settings = new NpgsqlConnectionStringBuilder(connection);
            var recurringMode = Environment.GetEnvironmentVariable("PROCUREMENT_CRUD_RECURRING");
            Check(recurringMode is null or "1", "Recurring verification accepts only an explicit 1 or an absent flag.");
            recurringCrudMode = recurringMode == "1";
            Check(args.Length == 0 && settings.Host is "localhost" or "127.0.0.1" or "::1" or "/authority-identity-private-socket", "Private socket/loopback only; no arguments.");
            Check(settings.Database is { Length: <= 63 } name && name.StartsWith("procurement_crud_http_", StringComparison.Ordinal)
                && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'), "A fresh procurement_crud_http_ database is required.");
            var socket = Environment.GetEnvironmentVariable("PROCUREMENT_CRUD_BROWSER_SOCKET");
            var manifest = Environment.GetEnvironmentVariable("PROCUREMENT_CRUD_BROWSER_MANIFEST");
            browserMode = socket is not null; publishManifest = manifest is not null;
            Check(socket is null || socket == BrowserSocket, "Only the fixed private browser socket is allowed.");
            Check(manifest is null || browserMode && manifest == BrowserManifest, "Manifest requires the fixed private browser mode/path.");
            if (browserMode)
            {
                Check(Directory.Exists("/crud-browser-socket") && new DirectoryInfo("/crud-browser-socket").LinkTarget is null,
                    "Runner must provide a private socket mount, not a symlink.");
                Check(!File.Exists(BrowserSocket) && !Directory.Exists(BrowserSocket), "Refusing to replace an existing socket.");
                if (publishManifest) Check(!File.Exists(BrowserManifest) && !Directory.Exists(BrowserManifest), "Refusing to overwrite an existing manifest.");
            }
            await Bootstrap();
            await using var app = await Host(); using var client = Client(app);
            var context = await Get(client, "/context"); Check(context.GetProperty("workerId").GetGuid() == Worker, "Actual context did not bind the synthetic Core worker.");
            var meta = await Get(client, "/meta"); Check(meta.GetProperty("crudEnabled").GetBoolean(), "Normal host CRUD capability unavailable.");
            foreach (var capability in new[] { "approvals", "orders", "sourcingSourceDrafts", "receivingDrafts", "invoiceComparison" })
                Check(!meta.GetProperty("capabilities").GetProperty(capability).GetBoolean(), "Draft fixture must not enable an operational workflow.");
            await Vendors(client); await Items(client); await Requests(client); await ExactRequests(client); await OtherDrafts(client); await Settings(client); await ReceivingLookups(client); await PolicyDraftRetirement(client); await Invoices(client); await Credits(client);
            if (recurringCrudMode) await RecurringPurchases(client);
            stage = "independent-final-owning-context";
            await using (var proof = Db())
            {
                Check(await proof.SupplierInvoices.CountAsync() == 4, "Unexpected invoices.");
                Check(await proof.PolicyVersions.CountAsync() == 2, "Unexpected policy draft count.");
                Check(await proof.ProcurementMethods.CountAsync() == 2 + (policyRaceMethodCreated ? 1 : 0), "Unexpected method count after policy race.");
                Check(!await proof.PolicyVersions.AnyAsync(p => p.State == "Retired" && proof.ProcurementMethods.Any(m => m.PolicyVersionId == p.Id)),
                    "A retired policy has a method reference.");
                Check(!await proof.FinanceInvoiceHandoffs.AnyAsync() && !await proof.FinancePaymentStatusRefs.AnyAsync()
                    && !await proof.InvoiceMatchResults.AnyAsync() && !await proof.GoodsReceipts.AnyAsync(), "Draft CRUD caused downstream effects.");
                Check((await proof.SupplierInvoices.SingleAsync(x => x.Id == legacyInvoice)).BranchRef is null, "Legacy scope was fabricated.");
            }
            var beforeDown = await Footprint();
            await using (var db = Db())
            {
                // Exercise this released migration's veto without first downgrading P21.
                // EF commits migrations individually; MigrateAsync(P19) is not an atomic
                // rollback of the complete P21 -> P19 chain.
                var downSql = db.GetService<IMigrator>().GenerateScript(P20, P19);
                Check(!downSql.Contains(CanonicalP21, StringComparison.Ordinal)
                    && downSql.Contains("Retained invoice branch or removed-line history prevents destructive downgrade.", StringComparison.Ordinal),
                    "Expected only the released P20 invoice-history downgrade guard.");
                await using var downConnection = new NpgsqlConnection(connection);
                await downConnection.OpenAsync();
                await using var downCommand = new NpgsqlCommand(downSql, downConnection);
                try { await downCommand.ExecuteNonQueryAsync(); throw new ProbeFailure("Used invoice scope/history allowed destructive Down."); }
                catch (Exception e) when (FindPg(e) is { SqlState: "55000", MessageText: "Retained invoice branch or removed-line history prevents destructive downgrade." }) { }
            }
            Check(beforeDown == await Footprint(), "Denied used Down changed retained data/history.");
            await VerifyCanonicalSchema();
            Check(await Scalar("SELECT (NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname IN ('core_approval','core_document')))::text") == "true",
                "Bounded CRUD unexpectedly required or created shared approval/document schemas.");
            Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS", assertions, actualHttpRequests = requests,
                actualPostgres = true, genuineCoreScope = true, finiteRole = false, syntheticSupplierApprovalFixture = true,
                legacyP19Preserved = true, emptyDownUp = true, usedP20DownBlocked = true, production = false,
                policyDraftRetirement = true, policyReferenceRace = true, policyRaceMethodCreated, exactRequestDrafts = true,
                policyRaceRequestsOverlapped = policyRaceOverlapped,
                recurringPurchasePlanningCrud = recurringCrudMode,
                scope = "Basic draft CRUD only; no procurement approval, supplier approval, Finance, stock or full-module completion" }));
            if (browserMode)
            {
                if (publishManifest) await WriteManifest();
                Console.WriteLine(JsonSerializer.Serialize(new { status="PRIVATE_BROWSER_READY", socketPath=BrowserSocket,
                    manifestPath=publishManifest?BrowserManifest:null, procurementResponses="Actual mapped handlers/database", coreShell="Tool-only synthetic presentation over actual Core rows", publicTcp=false }));
                await app.WaitForShutdownAsync();
            }
            else await app.StopAsync();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "FAIL", stage, assertions, requests,
                exception = e.GetType().Name, sqlState = FindPg(e)?.SqlState, constraint = FindPg(e)?.ConstraintName,
                message = e is ProbeFailure ? e.Message : "Private stage failed; connection and SQL are not printed" })); return 1;
        }
    }
    static HrmDbContext Core() => new(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options, new ToolTenant());
    static ProcurementDbContext Db() => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
        n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, new ToolScope());
    static async Task Bootstrap()
    {
        stage = "fresh-Core-P19-P20-legacy";
        await using (var raw = new NpgsqlConnection(connection))
        {
            await raw.OpenAsync(); await using var check = new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%' AND c.relkind IN ('r','p'))", raw);
            Check(await check.ExecuteScalarAsync() is true, "Refusing nonempty database; no reset/resume/cleanup.");
        }
        await using (var core = Core())
        {
            Check(await core.Database.EnsureCreatedAsync(), "Fresh Core bootstrap failed.");
            core.LegalEntities.AddRange(new LegalEntity { Id=Company,Code="CRUD",RegisteredName="Synthetic CRUD company",Currency="ZMW",IsDefault=true },
                new LegalEntity {Id=ForeignCompany,Code="FOREIGN",RegisteredName="Foreign synthetic company",Currency="USD"});
            core.WorkLocations.Add(new WorkLocation {Id=Branch,Code="MAIN",Name="Synthetic main",LegalEntityId=Company});
            foreach(var (actor,worker,label) in new[]{(Actor,Worker,"maker"),(Other,OtherWorker,"other")})
            {
                core.Workers.Add(new Worker {Id=worker,EmployeeNo="CRUD-"+label,FirstName=label,LastName="Synthetic",SubjectId=actor.ToString(),Status="active",LocationId=Branch});
                core.LocalUsers.Add(new LocalUser {Id=actor,WorkerId=worker,Email=label+"@example.test",NormalizedEmail=label.ToUpperInvariant()+"@EXAMPLE.TEST",DisplayName=label,PasswordHash="synthetic-no-login",RolesCsv="erp_admin",IsActive=true});
                core.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=actor,LocationId=Branch});
            }
            await core.SaveChangesAsync();
        }
        await using (var db=Db())
        {
            await db.GetService<IMigrator>().MigrateAsync(P19);
            var supplier=new Supplier {Number="VEN-OWNER-FIXTURE",LegalName="Synthetic approved prerequisite",DisplayName="Synthetic approved prerequisite",CountryCode="ZM",Status="Active",IsOrderable=true};
            db.AddScoped(supplier);await db.SaveChangesAsync();approvedSupplier=supplier.Id;
        }
        // Legacy rows are owner fixtures at the actual P19 schema, never current HTTP approval evidence.
        var old=new SupplierInvoice {TenantId=Tenant,LegalEntityId=Company,SupplierId=approvedSupplier,InvoiceNumber="LEGACY-1",NormalizedInvoiceNumber="LEGACY-1",Status="Received",IntakeChannel="Manual",CurrencyCode="ZMW",NetAmount=12.3456m,GrossAmount=12.3456m,InvoiceDate=new(2026,10,1),CreatedAt=DateTimeOffset.UtcNow,CreatedBy=Actor.ToString()};
        legacyInvoice=old.Id;
        var line=new SupplierInvoiceLine {TenantId=Tenant,LegalEntityId=Company,SupplierInvoiceId=old.Id,LineNumber=1,Description="Original legacy line",Quantity=1,UnitPrice=12.3456m,CreatedAt=old.CreatedAt,CreatedBy=old.CreatedBy};
        await RawInsert("supplier_invoices",old,"branch_ref");await RawInsert("supplier_invoice_lines",line,"is_active");
        var oldValues=await Legacy();
        await using(var db=Db())
        {
            var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync(P20);await migrator.MigrateAsync(P19);await migrator.MigrateAsync(P20);
            // The current invoice CLR model also contains P21 fields; do not materialize it
            // against old P20 while proving the released two-column upgrade in isolation.
            Check(await Scalar("SELECT (branch_ref IS NULL)::text FROM procurement.supplier_invoices WHERE invoice_number='LEGACY-1'") == "true", "Upgrade backfilled branch.");
            Check(await Scalar("SELECT is_active::text FROM procurement.supplier_invoice_lines WHERE description='Original legacy line'") == "true", "Legacy line lost active default.");
        }
        Check(oldValues==await Legacy(),"Upgrade/unusedDown changed original legacy columns.");
        // Retain the released P19/P20 rehearsal above. The current application also needs
        // the genuine canonical forward delta; metadata must never fake away that pending ID.
        stage = "canonical-P21-before-current-HTTP";
        await using (var db = Db()) await db.GetService<IMigrator>().MigrateAsync(CanonicalP21);
        await VerifyCanonicalSchema();
        Check(oldValues==await Legacy(),"Canonical upgrade changed original legacy columns.");
    }
    static async Task<WebApplication> Host()
    {
        var b=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Production"});b.Logging.ClearProviders();
        if (browserMode) b.WebHost.ConfigureKestrel(options=>options.ListenUnixSocket(BrowserSocket));
        else b.WebHost.UseTestServer();
        b.Services.AddScoped<ITenantAccessor>(_=>new ToolTenant());b.Services.AddDbContext<HrmDbContext>(o=>o.UseNpgsql(connection));
        ProcurementHostIntegration.AddServices(b.Services,connection);
        b.Services.AddAuthentication("private-crud").AddScheme<AuthenticationSchemeOptions,ToolAuthentication>("private-crud",_=>{});
        b.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app=b.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<ProcurementScopeMiddleware>();
        ProcurementHostIntegration.MapRoutes(app);
        if (browserMode) MapPrivateCoreShell(app);
        await app.StartAsync();return app;
    }
    static HttpClient Client(WebApplication app)
    {
        if (!browserMode) return app.GetTestClient();
        return new HttpClient(new SocketsHttpHandler { ConnectCallback=async (_, ct)=>
        {
            var socket=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
            try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(BrowserSocket),ct);return new NetworkStream(socket,ownsSocket:true); }
            catch { socket.Dispose();throw; }
        } }) { BaseAddress=new Uri("http://localhost"),Timeout=TimeSpan.FromSeconds(60) };
    }
    static async Task WriteManifest()
    {
        await using var stream=new FileStream(BrowserManifest,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(BrowserManifest,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        await JsonSerializer.SerializeAsync(stream,new { database=new NpgsqlConnectionStringBuilder(connection).Database,
            tenantId=Tenant,companyId=Company,foreignCompanyId=ForeignCompany,
            branchId=Branch,actorId=Actor,workerId=Worker,approvedSupplierId=approvedSupplier,legacyInvoiceId=legacyInvoice,
            authenticationHeader="X-Crud-Actor",socketPath=BrowserSocket,
            actors=new {maker=new {principalId=Actor,workerId=Worker},other=new {principalId=Other,workerId=OtherWorker}},
            cliProofComplete=true,production=false,finiteRole=false,coreShellPresentationOnly=true,
            supplierApproval="Synthetic owner fixture, not supplier approval proof" },new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    // Tool-only browser presentation endpoints; no production Core implementation or permission bypass.
    // Every company/user/worker/branch response comes from the actual private Core store.
    static void MapPrivateCoreShell(WebApplication app)
    {
        app.MapGet("/api/hrm/auth/me",async (HttpContext http,HrmDbContext core)=>
        {
            var facts=await ShellFacts(http,core);
            return facts is null?(IResult)Results.Unauthorized():Results.Ok(new {authenticated=true,user=new {isActive=true,
                displayName=facts.User.DisplayName,email=facts.User.Email,roles=facts.User.RolesCsv.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)},privateSyntheticShell=true});
        }).RequireAuthorization();
        app.MapGet("/api/hrm/branding",async (HttpContext http,HrmDbContext core)=>
        {
            var facts=await ShellFacts(http,core);
            return facts is null?(IResult)Results.Unauthorized():Results.Ok(new {displayName=facts.Company.RegisteredName,privateSyntheticBranding=true});
        }).RequireAuthorization();
        app.MapGet("/api/hrm/admin/legal-entities",async (HttpContext http,HrmDbContext core)=>
        {
            var facts=await ShellFacts(http,core);
            return facts is null?(IResult)Results.Unauthorized():Results.Ok(new {items=new[]{new {id=facts.Company.Id,code=facts.Company.Code,registeredName=facts.Company.RegisteredName}},privateSyntheticShell=true});
        }).RequireAuthorization();
        app.MapGet("/api/hrm/admin/locations",async (HttpContext http,HrmDbContext core)=>
        {
            var facts=await ShellFacts(http,core);
            return facts is null?(IResult)Results.Unauthorized():Results.Ok(new {items=new[]{new {id=facts.Branch.Id,code=facts.Branch.Code,name=facts.Branch.Name,legalEntityId=facts.Company.Id}},privateSyntheticShell=true});
        }).RequireAuthorization();
        app.MapGet("/api/hrm/shell/",async (HttpContext http,HrmDbContext core)=>
        {
            var facts=await ShellFacts(http,core);
            return facts is null?(IResult)Results.Unauthorized():Results.Ok(new {assignedLocationIds=new[]{facts.Branch.Id},confined=true,privateSyntheticShell=true});
        }).RequireAuthorization();
    }
    sealed record ShellProjection(LocalUser User,Mightyfin.Erp.Hrm.Domain.Entities.Worker Worker,LegalEntity Company,WorkLocation Branch);
    static async Task<ShellProjection?> ShellFacts(HttpContext http,HrmDbContext core)
    {
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value,out var actor)||(actor!=Actor&&actor!=Other)) return null;
        var user=await core.LocalUsers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==actor&&x.IsActive);
        if (user is null || user.WorkerId is not {} workerId) return null;
        var worker=await core.Workers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==workerId&&x.Status=="active");
        if (worker is null || worker.LocationId!=Branch) return null;
        var branch=await core.WorkLocations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==Branch&&x.LegalEntityId==Company&&!x.IsArchived);
        var company=await core.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==Company);
        if (branch is null||company is null||!await core.UserBranchAssignments.AsNoTracking().AnyAsync(x=>x.UserId==actor&&x.LocationId==Branch)) return null;
        return new(user,worker,company,branch);
    }
    static async Task Vendors(HttpClient c)
    {
        var draft=new {legalName="Actual CRUD vendor",displayName="Actual CRUD vendor",countryCode="ZM",attributes=new[]{new{kind="CustomField",key="Site",value="A"}}};
        var saved=await Send(c,HttpMethod.Post,"/vendors",draft,HttpStatusCode.Created);var id=Id(saved);var path="/vendors/"+id;
        await Get(c,path);
        saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),draft=new{draft.legalName,displayName="Updated CRUD vendor",draft.countryCode,attributes=new[]{new{kind="CustomField",key="Site",value="B"}}}});
        var detail=await Get(c,path);Check(detail.GetProperty("attributes")[0].GetProperty("value").GetString()=="B","Vendor attributes ignored.");
        var contact=await Send(c,HttpMethod.Post,path+"/contacts",new{version=Version(saved),contact=new{name="Contact",email="contact@example.test"}},HttpStatusCode.Created);
        contact=await Send(c,HttpMethod.Put,path+"/contacts/"+Id(contact),new{version=Version(contact),contact=new{name="Corrected contact",email="contact@example.test"}});
        contact=await Send(c,HttpMethod.Post,path+"/contacts/"+Id(contact)+"/remove",new{version=Version(contact),reason="Synthetic duplicate contact"});
        var site=await Send(c,HttpMethod.Post,path+"/sites",new{version=Version(contact),site=new{kind="Billing",addressLine1="Synthetic address",countryCode="ZM"}},HttpStatusCode.Created);
        site=await Send(c,HttpMethod.Put,path+"/sites/"+Id(site),new{version=Version(site),site=new{kind="Billing",addressLine1="Corrected synthetic address",countryCode="ZM"}});
        site=await Send(c,HttpMethod.Post,path+"/sites/"+Id(site)+"/remove",new{version=Version(site),reason="Synthetic address no longer needed"});
        await Get(c,"/vendors");
        var before=await Footprint();await Send(c,HttpMethod.Put,path,new{version=1,draft},HttpStatusCode.Conflict);Check(before==await Footprint(),"Stale vendor changed state.");
        await Send(c,HttpMethod.Get,path,null,HttpStatusCode.Forbidden,foreign:true);
        await Send(c,HttpMethod.Post,path+"/withdraw",new{version=Version(site),reason="Synthetic draft no longer needed"});
        await using var db=Db();var owner=await db.Suppliers.AsNoTracking().SingleAsync(x=>x.Id==id);
        Check(owner.Status=="Withdrawn" && await db.SupplierAttributes.AnyAsync(x=>x.SupplierId==id&&x.FieldValue=="B"),"Vendor owning reload mismatch.");
        Check(await db.SupplierContacts.AnyAsync(x=>x.SupplierId==id&&!x.IsActive&&x.Name=="Corrected contact")
            &&await db.SupplierSites.AnyAsync(x=>x.SupplierId==id&&!x.IsActive&&x.AddressLine1=="Corrected synthetic address"),"Vendor child correction/removal history missing.");
    }
    static async Task Items(HttpClient c)
    {
        var item=new{code="CRUD-ITEM",name="Draft item",kind="Goods",categoryCode="SYNTHETIC",unitCode="each"};
        var saved=await Send(c,HttpMethod.Post,"/items",item,HttpStatusCode.Created);var id=Id(saved);var path="/items/"+id;
        saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),item=new{item.code,name="Updated item",item.kind,item.categoryCode,item.unitCode}});
        await Get(c,"/items");await Get(c,path);
        var entries=path+"/catalog-entries";
        object Price(string price,string min,string max,string tax,string audience)=>new{supplierId=approvedSupplier,currencyCode="ZMW",unitCode="each",
            unitPrice=price,minimumQuantity=min,maximumQuantity=max,taxBasis=tax,audienceCode=audience,
            effectiveFrom="2026-10-22T00:00:00+00:00",effectiveTo="2027-10-22T00:00:00+00:00"};
        var price=await Send(c,HttpMethod.Post,entries,Price("12345678901234.5678","1.2345","100.0001","Inclusive","OFFICE"),HttpStatusCode.Created);
        var entryId=Id(price);Check(price.GetProperty("catalogEditVersion").GetInt32()==1
            &&price.GetProperty("exactAmounts").GetProperty("unitPrice").GetString()=="12345678901234.5678","Catalog create lost exact wide amount.");
        price=await Send(c,HttpMethod.Put,entries+"/"+entryId,new{version=Version(price),entry=Price("98765432109876.5432","2.3456","100.9999","Exclusive","FIELD_TEAM")});
        var stored=(await Get(c,entries)).GetProperty("items").EnumerateArray().Single(x=>Id(x)==entryId);
        Check(stored.GetProperty("taxBasis").GetString()=="Exclusive"&&stored.GetProperty("audienceCode").GetString()=="FIELD_TEAM"
            &&stored.GetProperty("exactAmounts").GetProperty("unitPrice").GetString()=="98765432109876.5432"
            &&stored.GetProperty("exactAmounts").GetProperty("minimumQuantity").GetString()=="2.3456"
            &&stored.GetProperty("exactAmounts").GetProperty("maximumQuantity").GetString()=="100.9999","Catalog draft reload lost commercial scope/limits/exact amount.");
        var beforePrice=await Footprint();
        await Send(c,HttpMethod.Put,entries+"/"+entryId,new{version=1,entry=Price("1","1","2","Changed","CHANGED")},HttpStatusCode.Conflict);
        Check(beforePrice==await Footprint(),"Stale catalog correction changed owning rows/audits.");
        await Send(c,HttpMethod.Post,path+"/deactivate",new{version=Version(saved)});
        await using var db=Db();Check((await db.CatalogItems.SingleAsync(x=>x.Id==id)).Name=="Updated item","Item update not persisted.");
        var ownerPrice=await db.CatalogEntries.AsNoTracking().SingleAsync(x=>x.Id==entryId);
        Check(ownerPrice.CatalogItemId==id&&ownerPrice.SupplierId==approvedSupplier&&ownerPrice.Status=="Draft"&&ownerPrice.PublishedVersion==0
            &&ownerPrice.UnitPrice==98765432109876.5432m&&ownerPrice.MinimumQuantity==2.3456m&&ownerPrice.MaximumQuantity==100.9999m
            &&ownerPrice.TaxBasis=="Exclusive"&&ownerPrice.AudienceCode=="FIELD_TEAM","Owning PostgreSQL catalog draft differs from exact HTTP result.");
        Check(!(await db.CatalogItems.SingleAsync(x=>x.Id==id)).IsPurchasable,"Catalog draft checking activated the item.");
        // A newly created inactive item deactivation is intentionally a no-op, not an activation proof.
    }
    static object RequestDraft(string purpose)=>new{purpose,currencyCode="ZMW",lines=new[]{new{description="Synthetic request need",unitCode="each",requestedQuantity=2,estimatedUnitPrice=3}}};
    static async Task Requests(HttpClient c)
    {
        var saved=await Send(c,HttpMethod.Post,"/requests",RequestDraft("CRUD request"),HttpStatusCode.Created);var id=Id(saved);var path="/requests/"+id;
        saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),purpose="Updated request",currencyCode="ZMW",lines=new[]{new{description="Updated need",unitCode="each",requestedQuantity=3,estimatedUnitPrice=4}}});
        await Get(c,"/requests");await Get(c,path);await Send(c,HttpMethod.Post,path+"/withdraw",new{version=Version(saved),reason="Synthetic request no longer needed"});
        await using var db=Db();Check((await db.PurchaseRequests.SingleAsync(x=>x.Id==id)).Status=="Withdrawn","Request withdrawal missing.");
    }
    static async Task OtherDrafts(HttpClient c)
    {
        object Plan(string description)=>new{financialPeriodRef="FY-SYNTHETIC",currencyCode="ZMW",lines=new[]{new{description,unitCode="each",estimatedQuantity=1,estimatedAmount=10}}};
        object Rfq(string description)=>new{title=description,method="RFQ",currencyCode="ZMW",lines=new[]{new{description,unitCode="each",quantity=1}}};
        object Contract(string title)=>new{supplierId=approvedSupplier,title,contractType="Service",currencyCode="ZMW",ceilingAmount=10};
        object Order(string description)=>new{supplierId=approvedSupplier,currencyCode="ZMW",lines=new[]{new{description,unitCode="each",quantity=1,unitPrice=10,taxAmount=0}}};
        foreach(var (route,create,edit) in new[]{("/plans",Plan("Plan"),Plan("Updated plan")),("/sourcing",Rfq("RFQ"),Rfq("Updated RFQ")),("/contracts",Contract("Contract"),Contract("Updated contract")),("/orders",Order("PO"),Order("Updated PO"))})
        {
            var saved=await Send(c,HttpMethod.Post,route,create,HttpStatusCode.Created);var path=route+"/"+Id(saved);await Get(c,path);
            saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),reason="Synthetic draft correction",draft=edit});
            await Get(c,route);await Get(c,path);await Send(c,HttpMethod.Post,path+"/cancel",new{version=Version(saved),reason="Synthetic draft no longer needed"});
            Check((await Get(c,path)).GetProperty("status").GetString()=="Cancelled",route+" cancellation not persisted.");
        }
        await using var db=Db();Check(await db.ProcurementPlans.CountAsync()==1&&await db.SourcingEvents.CountAsync()==1&&await db.SupplierContracts.CountAsync()==1&&await db.PurchaseOrders.CountAsync()==1,"Draft count mismatch.");
    }
    static async Task Settings(HttpClient c)
    {
        foreach(var route in new[]{"/configuration/categories","/configuration/methods"})
        {
            var saved=await Send(c,HttpMethod.Post,route,new{code="CRUD",name="Synthetic settings draft"},HttpStatusCode.Created);var path=route+"/"+Id(saved);
            saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),draft=new{code="CRUD",name="Updated settings draft"}});
            await Get(c,route);await Get(c,path);await Send(c,HttpMethod.Post,path+"/retire",new{version=Version(saved),reason="Synthetic setting no longer needed"});
            Check((await Get(c,path)).GetProperty("status").GetString()=="Retired","Settings retirement missing.");
        }
    }
    static async Task Invoices(HttpClient c)
    {
        object Draft(bool two)=>new{supplierId=approvedSupplier,invoiceNumber="CRUD-EXACT",invoiceDate="2026-10-22",currencyCode="ZMW",
            lines=two?new[]{new{description="Wide exact",quantity="1",unitPrice="12345678901234.5678",taxAmount="0"},new{description="Remove me",quantity="1",unitPrice="0.0001",taxAmount="0"}}:new[]{new{description="Corrected wide exact",quantity="1",unitPrice="12345678901234.5678",taxAmount="0"}}};
        var saved=await Send(c,HttpMethod.Post,"/invoices",Draft(true),HttpStatusCode.Created);var id=Id(saved);var path="/invoices/"+id;
        await Get(c,"/invoices");await Get(c,path);saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),reason="Correct synthetic invoice",draft=Draft(false)});
        var before=await Footprint();await Send(c,HttpMethod.Put,path,new{version=1,reason="Stale",draft=Draft(false)},HttpStatusCode.Conflict);Check(before==await Footprint(),"Stale invoice changed state.");
        await Send(c,HttpMethod.Post,path+"/cancel",new{version=Version(saved),reason="Synthetic invoice cancelled"});
        await using var db=Db();var row=await db.SupplierInvoices.AsNoTracking().SingleAsync(x=>x.Id==id);
        var lines=await db.SupplierInvoiceLines.AsNoTracking().Where(x=>x.SupplierInvoiceId==id).ToArrayAsync();
        Check(row.Status=="Cancelled"&&row.GrossAmount==12345678901234.5678m&&row.BranchRef==Branch,"Invoice exact/header/cancel persistence mismatch.");
        Check(lines.Length==2&&lines.Count(x=>x.IsActive)==1&&lines.Single(x=>x.IsActive).UnitPrice==12345678901234.5678m,"Invoice removal destroyed or rounded history.");
    }
    static async Task Credits(HttpClient c)
    {
        stage="credit-actual-invoice-prerequisites";
        object Bill(string number)=>new{supplierId=approvedSupplier,invoiceNumber=number,invoiceDate="2026-10-22",currencyCode="ZMW",
            lines=new[]{new{description="Actual credit intake source",quantity="1",unitPrice="10",taxAmount="0"}}};
        var source=await Send(c,HttpMethod.Post,"/invoices",Bill("CREDIT-CRUD-SOURCE"),HttpStatusCode.Created);var invoice=Id(source);
        var raceSource=await Send(c,HttpMethod.Post,"/invoices",Bill("CREDIT-RACE-SOURCE"),HttpStatusCode.Created);var raceInvoice=Id(raceSource);
        var protectedBefore=await CreditProtectedState();
        var saved=await Send(c,HttpMethod.Post,"/vendor-credits",new{supplierInvoiceId=invoice,creditNumber=" cn-http-001 ",amount="1.2345",reason="Synthetic supplier credit document received"},HttpStatusCode.Created);
        var id=Id(saved);var path="/vendor-credits/"+id;
        Check(saved.GetProperty("creditCrudVersion").GetInt32()==1&&saved.GetProperty("creditNumber").GetString()=="CN-HTTP-001"
            &&saved.GetProperty("exactAmount").GetString()=="1.2345"&&saved.GetProperty("status").GetString()=="Draft","Credit capture contract or exact amount mismatch.");
        var reload=await Get(c,path);Check(Id(reload)==id&&reload.GetProperty("supplierInvoiceId").GetGuid()==invoice,"Credit source identity not retained on HTTP reload.");
        saved=await Send(c,HttpMethod.Put,path,new{version=Version(saved),draft=new{creditNumber="cn-http-002",amount="2.3456",reason="Supplier corrected commercial explanation"},reason="Correct original credit capture"});
        Check(Version(saved)==2&&saved.GetProperty("exactAmount").GetString()=="2.3456","Credit update lost exact amount/CAS.");
        var before=await Footprint();
        await Send(c,HttpMethod.Put,path,new{version=1,draft=new{creditNumber="CHANGED",amount="1",reason="Stale edit"},reason="Must not overwrite"},HttpStatusCode.Conflict);
        Check(before==await Footprint(),"Stale credit correction changed stored state.");
        await Send(c,HttpMethod.Get,path,null,HttpStatusCode.Forbidden,foreign:true);
        await Send(c,HttpMethod.Post,"/vendor-credits",new{supplierInvoiceId=invoice,creditNumber="FOREIGN",amount="1",reason="Wrong company"},HttpStatusCode.Forbidden,foreign:true);
        Check(before==await Footprint(),"Wrong-scope credit read/capture changed stored state.");
        saved=await Send(c,HttpMethod.Post,path+"/cancel",new{version=Version(saved),reason="Supplier withdrew documentary reference"});
        reload=await Get(c,path);Check(Version(reload)==3&&reload.GetProperty("status").GetString()=="Cancelled"
            &&reload.GetProperty("exactAmount").GetString()=="2.3456"&&reload.GetProperty("allowedActions").GetArrayLength()==0,"Credit cancellation did not retain exact reference/history.");
        Check(protectedBefore==await CreditProtectedState(),"Credit CRUD changed invoices, Finance, receiving or outbox records.");

        stage="credit-two-separate-HTTP-requests-concurrent-ceiling";
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(HttpStatusCode Status,JsonElement Body)> Race(string number)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,Root+"/vendor-credits");
            request.Headers.Add("X-Crud-Actor",Actor.ToString());request.Headers.Add("X-Shell-Entity",Company.ToString());request.Headers.Add("X-Shell-Location",Branch.ToString());
            request.Content=JsonContent.Create(new{supplierInvoiceId=raceInvoice,creditNumber=number,amount="6",reason="Separate actual concurrent documentary credit"});
            await gate.Task;
            using var response=await c.SendAsync(request);Interlocked.Increment(ref requests);
            var content=await response.Content.ReadAsStringAsync();
            Check(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
                "Concurrent credit returned unexpected status "+(int)response.StatusCode+"; code="+SafeCode(content));
            return(response.StatusCode,JsonDocument.Parse(content).RootElement.Clone());
        }
        var one=Race("RACE-CREDIT-A");var two=Race("RACE-CREDIT-B");gate.SetResult();
        var outcomes=await Task.WhenAll(one,two);
        Check(outcomes.Count(x=>x.Status==HttpStatusCode.Created)==1,"Exactly one competing six-of-ten credit must persist.");
        foreach(var denied in outcomes.Where(x=>x.Status!=HttpStatusCode.Created))
            Check(denied.Body.GetProperty("code").GetString() is "stale-credit" or "credit-captured-total-exceeded","Competing credit failed for an unrelated reason.");
        await using(var db=Db())
        {
            var original=await db.SupplierInvoices.AsNoTracking().SingleAsync(x=>x.Id==raceInvoice);
            var rows=await db.SupplierCreditNoteRefs.AsNoTracking().Where(x=>x.SupplierInvoiceId==raceInvoice).ToArrayAsync();
            Check(rows.Length==1&&rows[0].Amount==6m&&rows[0].Status=="Draft"&&rows[0].FinanceCreditRef is null
                &&rows.Sum(x=>x.Amount)<=original.GrossAmount,"Owning PostgreSQL reload failed concurrent captured-gross conservation.");
            var corrected=await db.SupplierCreditNoteRefs.AsNoTracking().SingleAsync(x=>x.Id==id);
            Check(corrected.Status=="Cancelled"&&corrected.Amount==2.3456m&&corrected.CreditNumber=="CN-HTTP-002"&&corrected.Version==3,"Owning reload lost corrected/cancelled credit.");
            Check(await db.SupplierCreditNoteRefs.CountAsync()==2,"A denied concurrent/wrong-scope credit persisted.");
            Check(await db.AuditEvents.CountAsync(x=>x.RecordId==id&&x.Action=="CreditIntakeCaptured")==1
                &&await db.AuditEvents.CountAsync(x=>x.RecordId==id&&x.Action=="CreditIntakeCorrected")==1
                &&await db.AuditEvents.CountAsync(x=>x.RecordId==id&&x.Action=="CreditIntakeCancelled")==1,"Credit lifecycle audit evidence incomplete.");
            var winner=rows[0].Id;
            Check(await db.AuditEvents.CountAsync(x=>x.RecordType==nameof(SupplierCreditNoteRef)&&x.Action=="CreditIntakeCaptured")==2
                &&await db.AuditEvents.CountAsync(x=>x.RecordId==winner&&x.Action=="CreditIntakeCaptured")==1,"Concurrent loser leaked credit audit effects.");
        }
        Check(protectedBefore==await CreditProtectedState(),"Concurrent credit intake changed original invoices or external-owner records.");
        await CreditUnknownLegacyStatus(c,id,invoice);
        await Get(c,"/vendor-credits?search=CN-HTTP&status=Cancelled&page=1&pageSize=1");
    }
    // Explicit bootstrap-owner anomaly fixture, not an authorized credit lifecycle or financial outcome.
    // Change ONLY the status of a genuinely API-produced cancelled row, deny a new API write,
    // then restore that one original value and independently compare the entire retained footprint.
    static async Task CreditUnknownLegacyStatus(HttpClient c,Guid id,Guid invoice)
    {
        stage="owner-only-unknown-legacy-credit-status";
        var original=await Footprint();
        async Task SetStatus(string status)
        {
            await using var owner=new NpgsqlConnection(connection);await owner.OpenAsync();
            await using var command=new NpgsqlCommand("UPDATE procurement.supplier_credit_note_refs SET status=@status WHERE tenant_id=@tenant AND legal_entity_id=@company AND id=@id",owner);
            command.Parameters.AddWithValue("status",status);command.Parameters.AddWithValue("tenant",Tenant);
            command.Parameters.AddWithValue("company",Company);command.Parameters.AddWithValue("id",id);
            Check(await command.ExecuteNonQueryAsync()==1,"Synthetic legacy status fixture target not exact.");
        }
        await SetStatus("UnknownLegacyState");
        try
        {
            var anomaly=await Footprint();
            var denied=await Send(c,HttpMethod.Post,"/vendor-credits",new{supplierInvoiceId=invoice,creditNumber="UNKNOWN-HISTORY-MUST-NOT-CAPTURE",amount="1",reason="Explicit synthetic legacy anomaly"},HttpStatusCode.Conflict);
            Check(denied.GetProperty("code").GetString()=="credit-history-needs-review","Unknown legacy credit state was not blocked by its intended guard.");
            Check(anomaly==await Footprint(),"Denied unknown-history capture changed retained credit/invoice/audit state.");
        }
        finally { await SetStatus("Cancelled"); }
        Check(original==await Footprint(),"Owner-only status fixture failed exact original-footprint restoration.");
    }
    // Independent owning-store snapshot, excluding only the credit records and their intended audit changes.
    static async Task<string> CreditProtectedState()
    {
        await using var db=Db();
        return JsonSerializer.Serialize(new { invoices=await db.SupplierInvoices.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            lines=await db.SupplierInvoiceLines.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            handoffs=await db.FinanceInvoiceHandoffs.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            payments=await db.FinancePaymentStatusRefs.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            matches=await db.InvoiceMatchResults.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            receipts=await db.GoodsReceipts.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            services=await db.ServiceAcceptances.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            returns=await db.SupplierReturns.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            outbox=await db.OutboxEvents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync() });
    }
    static Guid Id(JsonElement v)=>v.GetProperty("id").GetGuid();static long Version(JsonElement v)=>v.GetProperty("version").GetInt64();
    static Task<JsonElement> Get(HttpClient c,string path)=>Send(c,HttpMethod.Get,path,null);
    static async Task<JsonElement> Send(HttpClient c,HttpMethod method,string path,object? body,HttpStatusCode expected=HttpStatusCode.OK,bool foreign=false)
    {
        stage="HTTP "+method+" "+path;using var request=new HttpRequestMessage(method,Root+path);
        request.Headers.Add("X-Crud-Actor",Actor.ToString());request.Headers.Add("X-Shell-Entity",(foreign?ForeignCompany:Company).ToString());request.Headers.Add("X-Shell-Location",Branch.ToString());
        if(body is not null)request.Content=JsonContent.Create(body);using var response=await c.SendAsync(request);Interlocked.Increment(ref requests);
        var content=await response.Content.ReadAsStringAsync();
        Check(response.StatusCode==expected,$"{stage}: expected {(int)expected}, got {(int)response.StatusCode}; code={SafeCode(content)}");
        return string.IsNullOrWhiteSpace(content)?default:JsonDocument.Parse(content).RootElement.Clone();
    }
    static string SafeCode(string content){try{return JsonDocument.Parse(content).RootElement.TryGetProperty("code",out var code)?code.GetString()??"none":"none";}catch(JsonException){return "non-json";}}
    static async Task VerifyCanonicalSchema()
    {
        await using var db = Db();
        Check(!db.Database.HasPendingModelChanges(), "Snapshot/model mismatch.");
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Actual canonical migration is missing; no availability override.");
        Check((await db.Database.GetAppliedMigrationsAsync()).Contains(CanonicalP21), "Canonical forward marker missing.");
        await using var raw = new NpgsqlConnection(connection); await raw.OpenAsync();
        foreach (var manifest in new[] { ReceivingOwnershipConstraintSql.ManifestQuery,
            InvoiceMatchingOwnershipConstraintSql.ManifestQuery, SourcingDraftOwnershipConstraintSql.ManifestQuery })
        {
            await using var command = new NpgsqlCommand(manifest, raw);
            Check(await command.ExecuteScalarAsync() is true, "Exact canonical ownership manifest missing.");
        }
        Check(await Scalar("SELECT count(*)::text FROM pg_constraint WHERE connamespace='procurement'::regnamespace AND contype='c' AND convalidated") == "39",
            "Canonical validated CHECK inventory mismatch.");
    }

    static async Task RawInsert(string table,object value,string excluded)
    {
        var json=JsonSerializer.SerializeToNode(value,Snake)!.AsObject();json.Remove(excluded);await using var raw=new NpgsqlConnection(connection);await raw.OpenAsync();
        await using var command=new NpgsqlCommand("INSERT INTO procurement."+table+" SELECT * FROM jsonb_populate_record(NULL::procurement."+table+",@row)",raw);
        command.Parameters.Add(new NpgsqlParameter("row",NpgsqlDbType.Jsonb){Value=json.ToJsonString()});Check(await command.ExecuteNonQueryAsync()==1,"Legacy raw insert failed.");
    }
    static async Task<string> Legacy()=>await Scalar("SELECT jsonb_build_object('invoice',(SELECT to_jsonb(t)-'branch_ref'-ARRAY['active_match_claim_result_id','current_mapping_version_id','last_match_result_id','mapping_revision','match_revision','matching_state'] FROM procurement.supplier_invoices t WHERE invoice_number='LEGACY-1'),'line',(SELECT to_jsonb(t)-'is_active' FROM procurement.supplier_invoice_lines t WHERE description='Original legacy line'))::text");
    static async Task<string> Scalar(string sql){await using var raw=new NpgsqlConnection(connection);await raw.OpenAsync();await using var c=new NpgsqlCommand(sql,raw);return (string)(await c.ExecuteScalarAsync())!;}
    static async Task<string> Footprint()=>await Scalar("SELECT jsonb_build_object('vendor',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.suppliers t),'attrs',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_attributes t),'items',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.catalog_items t),'catalogEntries',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.catalog_entries t),'invoices',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_invoices t),'lines',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_invoice_lines t),'credits',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_credit_note_refs t),'audit',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.audit_events t),'outbox',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.outbox_events t),'history',(SELECT jsonb_agg(to_jsonb(t) ORDER BY \"MigrationId\") FROM procurement.__procurement_migrations t))::text");
    static PostgresException? FindPg(Exception e)=>e as PostgresException??(e.InnerException is {} inner?FindPg(inner):null);
    static void Check(bool condition,string message){if(!condition)throw new ProbeFailure(message);Interlocked.Increment(ref assertions);}
    sealed class ProbeFailure(string message):InvalidOperationException(message);
    sealed class ToolTenant:ITenantAccessor{public string GetTenantId()=>Tenant;}
    sealed class ToolScope:IProcurementScope
    {
        public string TenantId=>Tenant;public Guid LegalEntityId=>Company;public string SubjectId=>Actor.ToString();public Guid? WorkerId=>Worker;
        public Guid? WorkLocationId=>Branch;public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string? CorrelationId=>"private-crud";
    }
    sealed class ToolAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!Guid.TryParse(Request.Headers["X-Crud-Actor"],out var actor)||(actor!=Actor&&actor!=Other))return Task.FromResult(AuthenticateResult.NoResult());
            var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("tenant",Tenant),new Claim("sub",actor.ToString()),new Claim(ClaimTypes.Role,"erp_admin")},"private-crud"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,"private-crud")));
        }
    }
}
