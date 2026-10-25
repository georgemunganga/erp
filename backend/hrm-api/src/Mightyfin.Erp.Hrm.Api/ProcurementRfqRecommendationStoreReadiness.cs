using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

// Physical and finite-role admission only. RecommendationSource separately
// checks the current Core purpose, branch, named reader and retained offer set.
public sealed class ProcurementRfqRecommendationStoreReadiness(
    ProcurementRfqCommercialOptions options,IConfiguration configuration,
    IServiceProvider services,IProcurementRfqCommercialReadiness commercial)
    : IProcurementRfqRecommendationDraftReadiness
{
    public async Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
    {
        if(!options.Enabled || !options.OfferOpeningEnabled || !options.RecommendationDraftEnabled
            || !ProcurementRfqRecommendationSchemaCapture.Populated
            || owner is null || !owner.Database.IsNpgsql()
            || owner.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable
            || !ReferenceEquals(transaction.GetDbTransaction().Connection,owner.Database.GetDbConnection())
            || services.GetService<ProcurementApprovalExecutionOptions>() is not
                {Enabled:true,PrincipalExecutionEnabled:true,ExecutionDatabaseConfigured:true}) return false;
        try
        {
            var execution=configuration.GetConnectionString("ProcurementApprovalExecution");
            if(string.IsNullOrWhiteSpace(execution)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                    execution,owner.Database.GetDbConnection().ConnectionString)
                // P36 reads opened P32 offers. It does not need P32 publish,
                // supplier-offer or Core-case write privileges.
                || !await commercial.IsReadyAsync(owner,false,ct)) return false;
            var model=owner.GetService<IDesignTimeModel>().Model;
            var tables=new HashSet<string>(StringComparer.Ordinal);
            var checks=new HashSet<string>(StringComparer.Ordinal);
            foreach(var type in ProcurementRfqRecommendationSchemaCapture.Types)
            {
                var entity=model.FindEntityType(type);
                if(entity is null || entity.GetSchema()!="procurement"
                    || entity.GetTableName() is not { } table) return false;
                tables.Add("procurement."+table);
                foreach(var check in entity.GetCheckConstraints())
                    checks.Add("procurement."+table+"."+check.Name);
            }
            if(!MatchesModelCatalog(tables,checks)) return false;
            return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(owner,
                ProcurementRfqRecommendationSchemaCapture.Types,
                PrivilegeSql(tables.Order(StringComparer.Ordinal).ToArray(),write),
                ProcurementRfqRecommendationSchemaCapture.ForeignNames,
                ProcurementRfqRecommendationSchemaCapture.Physical,ct);
        }
        catch(Exception error) when(error is not OperationCanceledException) {return false;}
    }

    internal static bool MatchesModelCatalog(IReadOnlySet<string> tables,IReadOnlySet<string> checks)
        => tables.SetEquals(ProcurementRfqRecommendationSchemaCapture.TableNames)
            && checks.SetEquals(ProcurementRfqRecommendationSchemaCapture.CheckNames);

    internal static string PrivilegeSql(IReadOnlyList<string> owned,bool write)
    {
        var expected=ProcurementRfqRecommendationSchemaCapture.TableNames;
        if(owned.Count!=expected.Count || owned.Distinct(StringComparer.Ordinal).Count()!=expected.Count
            || owned.Any(x=>!expected.Contains(x)))
            throw new ArgumentException("P36 exact owned tables required",nameof(owned));
        var append=owned.Where(x=>x!="procurement.principal_rfq_recommendation_drafts")
            .Concat(["procurement.audit_events","procurement.outbox_events"]).ToArray();
        var clauses=owned.Select(x=>"has_table_privilege(current_user,"+Literal(x)+",'SELECT')").ToList();
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND "
            +"(rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT has_schema_privilege(current_user,'core_approval','CREATE')");
        foreach(var table in owned.Concat(["procurement.audit_events","procurement.outbox_events"]))
        {
            clauses.Add("NOT has_table_privilege(current_user,"+Literal(table)+",'DELETE,TRUNCATE')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid="+Literal(table)
                +"::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER')"
                +" OR pg_has_role(current_user,c.relowner,'SET')))");
        }
        foreach(var table in append)
            clauses.Add("NOT has_any_column_privilege(current_user,"+Literal(table)+",'UPDATE')");
        if(write)
        {
            foreach(var table in owned.Concat(["procurement.audit_events","procurement.outbox_events"]))
                clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'INSERT')");
            clauses.Add("has_table_privilege(current_user,'procurement.principal_rfq_recommendation_drafts','UPDATE')");
        }
        return "SELECT "+string.Join(" AND ",clauses);
    }
}
