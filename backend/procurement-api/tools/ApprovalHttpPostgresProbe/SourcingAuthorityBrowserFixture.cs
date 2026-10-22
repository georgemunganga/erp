using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private const string SourceAuthorityReference="BROWSER-SOURCING-LIMIT";
    private const string SourceAuthorityActivationReason="Browser independent sourcing limit approved";
    private const string SourceAuthorityAction="SourcingEvent.Publish";
    private string? sourceAuthorityBrowserBefore;
    private Guid sourceAuthorityMakerWorker,sourceAuthorityReviewerWorker,sourceAuthorityPrincipalWorker;
    private DateTimeOffset sourceAuthorityEffectiveFrom;

    private async Task<string> PrepareSourcingAuthorityBrowserManifest(int port)
    {
        Require(distinctRuntime,"Sourcing authority browser requires separate finite runtime/bootstrap identities.");
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection,fixtureServices);
        using(var services=Scope(buyer))
        {
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.TenantRoleAssignments.AddRange(
                new TenantRoleAssignment {RoleKey="browser_sourcing_authority_maker",RoleName="Synthetic source authority maker",Active=true,
                    PermissionsCsv="procurement-access,procurement-configuration-read,procurement-authority-read,procurement-authority-configure"},
                new TenantRoleAssignment {RoleKey="browser_sourcing_authority_reviewer",RoleName="Synthetic source authority reviewer",Active=true,
                    PermissionsCsv="procurement-access,procurement-configuration-read,procurement-authority-read,procurement-authority-review"},
                new TenantRoleAssignment {RoleKey="browser_sourcing_publisher",RoleName="Synthetic named source publisher",Active=true,
                    PermissionsCsv="procurement-access,procurement-sourcing-publish"});
            var maker=await core.LocalUsers.SingleAsync(x=>x.Id==buyer);
            var reviewer=await core.LocalUsers.SingleAsync(x=>x.Id==manager1);
            var principal=await core.LocalUsers.SingleAsync(x=>x.Id==manager2);
            maker.RolesCsv="browser_sourcing_authority_maker";reviewer.RolesCsv="browser_sourcing_authority_reviewer";
            principal.RolesCsv="browser_sourcing_publisher";
            sourceAuthorityMakerWorker=maker.WorkerId!.Value;sourceAuthorityReviewerWorker=reviewer.WorkerId!.Value;
            sourceAuthorityPrincipalWorker=principal.WorkerId!.Value;
            Require(new[]{sourceAuthorityMakerWorker,sourceAuthorityReviewerWorker,sourceAuthorityPrincipalWorker}.Distinct().Count()==3,
                "Maker, independent reviewer and named authority subject must be distinct canonical workers.");
            foreach(var id in new[]{buyer,manager1,manager2})
                core.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=id,LocationId=branch});
            await core.SaveChangesAsync();
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.SpendingAuthorityGrants.AnyAsync() && !await db.PolicyVersions.AnyAsync()
                && !await db.PurchaseRequests.AnyAsync() && !await db.SourcingEvents.AnyAsync(),
                "Browser authority fixture must have no preseeded grant, policy, request or sourcing workflow.");
        }
        // Names and action tuple come from real scoped authority endpoints, not manifest-only eligibility.
        var list=await Get(Root+"/configuration/spending-authorities?action="+Uri.EscapeDataString(SourceAuthorityAction),buyer);
        Require(list.GetProperty("total").GetInt32()==0 && list.GetProperty("canCreate").GetBoolean()
            && list.GetProperty("createAction").GetString()==SourceAuthorityAction
            && list.GetProperty("createAmountBasis").GetString()=="ApprovedLineBase", "Normal host did not expose the exact SOURCING configuration tuple.");
        var people=await Get(Root+"/configuration/spending-authorities/lookups/people?action="+Uri.EscapeDataString(SourceAuthorityAction)+"&search=manager2",buyer);
        var person=people.GetProperty("items").EnumerateArray().Single();
        Require(person.GetProperty("principalId").GetString()==manager2.ToString("D"),"Scoped source authority people lookup lost the explicit named publisher.");
        sourceAuthorityEffectiveFrom=new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1),TimeSpan.Zero);
        var makerContext=await Get(Root+"/context",buyer);
        var reviewerContext=await Get(Root+"/context",manager1);
        var principalContext=await Get(Root+"/context",manager2);
        var meta=await Get(Root+"/meta",buyer);
        Require(meta.GetProperty("schemaReady").GetBoolean(),"Actual normal-host schema is not ready.");
        var capabilities=meta.GetProperty("capabilities");
        Require(!capabilities.GetProperty("approvals").GetBoolean() && !capabilities.GetProperty("orders").GetBoolean()
            && !capabilities.GetProperty("sourcingSourceDrafts").GetBoolean() && !capabilities.GetProperty("sourcing").GetBoolean(),
            "Authority settings fixture must not activate unrelated source workflows or publication.");
        sourceAuthorityBrowserBefore=await SourceAuthorityBrowserInvariant();
        return await WriteBrowserManifest(new {
            baseUrl=$"http://127.0.0.1:{port}",tenantId="probe",companyId=company,branchId=branch,currencyCode="ZMW",authenticationHeader="X-Probe-User",
            actors=new {
                maker=new {principalId=buyer,workerId=sourceAuthorityMakerWorker,context=makerContext},
                reviewer=new {principalId=manager1,workerId=sourceAuthorityReviewerWorker,context=reviewerContext},
                namedmanager2=new {principalId=manager2,workerId=sourceAuthorityPrincipalWorker,context=principalContext}},
            sourcingAuthority=new {authorityReference=SourceAuthorityReference,principalId=manager2,
                principalDisplayName=person.GetProperty("displayName").GetString(),maximumAmount="1.2345",currencyCode="ZMW",branchId=branch,
                effectiveFrom=sourceAuthorityEffectiveFrom.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture),effectiveUntil=(string?)null,
                allBranches=false,allDepartments=true,allCategories=true,expectedAction=SourceAuthorityAction,amountBasis="ApprovedLineBase",
                activationReason=SourceAuthorityActivationReason,noAuthorityOrWorkflowSeeded=true},
            warning="Synthetic private configuration fixture only. A named action permission is not a financial limit; operational RFQ publication is unavailable."
        });
    }

    public async Task VerifySourcingAuthorityBrowserCompletion()
    {
        Require(sourceAuthorityBrowserBefore is not null,"Untouched source authority browser fixture was not prepared.");
        // Fresh bootstrap-owned context after Kestrel shutdown, independent of browser UI success flags.
        using var services=Scope(buyer);
        var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var grants=await db.SpendingAuthorityGrants.AsNoTracking().Include(x=>x.Categories).ToArrayAsync();
        Require(grants.Length==1,"Browser must create exactly one configured authority and no default grants.");
        var grant=grants.Single();
        Require(grant.TenantId=="probe" && grant.LegalEntityId==company && grant.AuthorityReference==SourceAuthorityReference
            && grant.AuthorityVersion==1 && grant.Version==3 && grant.State=="Active"
            && grant.PrincipalId==manager2.ToString("D") && grant.Action==SourceAuthorityAction
            && grant.Permission=="procurement-sourcing-publish" && grant.AmountBasis=="ApprovedLineBase"
            && grant.MaximumAmount==1.2345m && grant.CurrencyCode=="ZMW"
            && !grant.AllBranches && grant.BranchRef==branch && grant.AllDepartments && grant.DepartmentRef is null
            && grant.AllCategories && grant.Categories.Count==0 && grant.EffectiveFrom==sourceAuthorityEffectiveFrom && grant.EffectiveUntil is null,
            "Retained active authority tuple, exact amount, scope or revision differs from browser contract.");
        Require(grant.CreatedBy==buyer.ToString("D") && grant.ConfigurationOwnerPrincipalId==buyer.ToString("D")
            && grant.SubmittedByPrincipalId==buyer.ToString("D") && grant.ActivatedByPrincipalId==manager1.ToString("D")
            && grant.UpdatedBy==manager1.ToString("D") && grant.SubmittedAt is not null && grant.ActivatedAt is not null
            && grant.CreatedAt<=grant.SubmittedAt.Value && grant.ActivatedAt.Value>=grant.SubmittedAt.Value
            && grant.ActivationReason==SourceAuthorityActivationReason
            && grant.SubmittedBusinessFingerprint==SpendingAuthorityConfiguration.Fingerprint(grant)
            && grant.PublishedBusinessFingerprint==grant.SubmittedBusinessFingerprint
            && grant.RevokedAt is null && grant.SupersededAt is null,"Independent lifecycle actors/fingerprints were not retained exactly.");
        var audits=await db.AuditEvents.AsNoTracking().Where(x=>x.RecordType==nameof(ProcurementSpendingAuthorityGrant) && x.RecordId==grant.Id).ToArrayAsync();
        Require(audits.Length==6
            && audits.Count(x=>x.Action=="create" && x.ActorSubjectId==buyer.ToString("D"))==1
            && audits.Count(x=>x.Action=="update" && x.ActorSubjectId==buyer.ToString("D"))==1
            && audits.Count(x=>x.Action=="update" && x.ActorSubjectId==manager1.ToString("D"))==1,
            "Expected exactly three lifecycle and three automatic authority audit effects.");
        foreach(var (action,state,version,actor) in new[]{
            ("authority.draft_created","Draft",1L,buyer),("authority.submit","PendingReview",2L,buyer),("authority.activate","Active",3L,manager1)})
        {
            var audit=audits.Single(x=>x.Action==action && x.ActorSubjectId==actor.ToString("D"));
            using var body=JsonDocument.Parse(audit.ChangedFieldsJson!);var after=body.RootElement.GetProperty("after");
            Require(after.GetProperty("State").GetString()==state && after.GetProperty("Version").GetInt64()==version
                && after.GetProperty("maximumAmount").GetString()=="1.2345", "Audit does not prove exact Draft→Submitted→Active browser transitions.");
        }
        Require(sourceAuthorityBrowserBefore==await SourceAuthorityBrowserInvariant(grant.Id),
            "Browser changed other Procurement/Core approval records, outbox, policies, allocations or histories.");
        Require(!await db.SourcingEvents.AnyAsync() && !await db.PolicyVersions.AnyAsync() && !await db.PurchaseRequests.AnyAsync(),
            "Configuration browser must not fabricate operational publication/source workflow.");
        Console.WriteLine("PASS: sourcing authority browser persisted exactly one finite independently activated v3 limit; unrelated source/Core state unchanged.");
    }

    private async Task<string> SourceAuthorityBrowserInvariant(Guid? authorityId=null)
    {
        await using var owner=new NpgsqlConnection(databaseConnection);await owner.OpenAsync();
        var tables=new List<(string Schema,string Name)>();
        await using(var command=new NpgsqlCommand("SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('procurement','core_approval') AND c.relkind='r' ORDER BY n.nspname,c.relname",owner))
        await using(var rows=await command.ExecuteReaderAsync())
            while(await rows.ReadAsync())tables.Add((rows.GetString(0),rows.GetString(1)));
        static string Quote(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
        var parts=tables.Select(table=>{
            var filter=table.Schema=="procurement" ? table.Name switch {
                "spending_authority_grants"=>" WHERE r.id<>@authority",
                "audit_events"=>" WHERE NOT(coalesce(r.record_type='ProcurementSpendingAuthorityGrant' AND r.record_id=@authority,false))",_=>""}:"";
            return "SELECT '"+table.Schema+"."+table.Name.Replace("'","''")+"' name,coalesce(jsonb_agg(j ORDER BY j::text),'[]'::jsonb) rows FROM (SELECT to_jsonb(r) j FROM "+Quote(table.Schema)+"."+Quote(table.Name)+" r"+filter+") source_rows";
        });
        await using var snapshot=new NpgsqlCommand("SELECT jsonb_object_agg(name,rows)::text FROM ("+string.Join(" UNION ALL ",parts)+") original",owner);
        snapshot.Parameters.AddWithValue("authority",authorityId??Guid.Empty);
        return ProcurementEvidenceEncoding.Digest((string)(await snapshot.ExecuteScalarAsync())!);
    }
}
