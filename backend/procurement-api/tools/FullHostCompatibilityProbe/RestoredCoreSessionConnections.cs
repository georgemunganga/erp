using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

/// <summary>Only the explicitly selected private restored cohort may use this contract.
/// Owner credentials never enter this object or the application's DI/configuration.</summary>
internal sealed record RestoredCoreSessionConnections(string Core,string Tenant)
{
    internal static readonly string[] DisabledFeatures=
    ["Procurement:Approvals:Enabled","Procurement:Approvals:PrincipalExecutionEnabled","Procurement:Orders:Enabled",
     "Procurement:Orders:PrincipalExecutionEnabled","Procurement:PrincipalDirectOrderDrafts:Enabled",
     "Procurement:SourcingDrafts:Enabled","Procurement:SourcingDrafts:PrincipalSourceEnabled","Procurement:SourcingPublication:Enabled",
     "Procurement:PrincipalReceiving:Enabled","Procurement:Receiving:DraftsEnabled","Procurement:Receiving:PositiveExecutionEnabled",
     "Procurement:InvoiceMatching:Enabled","Procurement:InvoiceUnitReview:Enabled","Procurement:ManualPayments:Enabled"];
    internal const string OptIn="P30_RESTORED_CORE_SESSION";
    internal static bool Selected=>Environment.GetEnvironmentVariable(OptIn)=="reviewed-private-only";

    internal void RequireSelection(string owner,string record,string execution,bool normalAuth,bool actualSchema,
        bool privateFinance,bool principalExecution,bool specialisedFixture)
    {
        if(!Selected||!normalAuth||!actualSchema||privateFinance||principalExecution||specialisedFixture||owner.Length!=0
            ||!Regex.IsMatch(Tenant,"^p30-core-session-[0-9]{2}$"))
            throw new InvalidOperationException("ExplicitCoreFiniteNormalAuthenticationRequired");
        var core=Require(Core,"core_session");
        var suffix=core.Database![^2..];
        if(Tenant!="p30-core-session-"+suffix)
            throw new InvalidOperationException("CoreSessionTenantSuffixMismatch");
        foreach(var (value,kind) in new[]{(record,"record"),(execution,"execute")})
        {
            var other=Require(value,kind);
            if(other.Host!=core.Host||other.Port!=core.Port||other.Database!=core.Database)
                throw new InvalidOperationException("CoreSessionSamePhysicalStoreRequired");
        }
    }

    internal static NpgsqlConnectionStringBuilder Require(string value,string kind,bool requirePassword=true)
    {
        var b=new NpgsqlConnectionStringBuilder(value);
        if(b.Database is not {} db||!Regex.IsMatch(db,"^proc_p30_core_session_20261024_[0-9]{2}$")
            ||b.Host!="/private-pg-socket"||b.Port!=5432||b.Username!="p30_"+kind+"_login_"+db[^2..]
            ||requirePassword&&(b.Password is null||!Regex.IsMatch(b.Password,"^[0-9a-f]{64}$"))
            ||!b.Pooling||b.Multiplexing||b.NoResetOnClose||b.IncludeErrorDetail||b.LogParameters
            ||!string.IsNullOrWhiteSpace(b.Options)||!string.IsNullOrWhiteSpace(b.SearchPath)
            ||!string.IsNullOrWhiteSpace(b.Passfile))
            throw new InvalidOperationException("ExplicitFiniteCoreRecordExecutionConnectionRequired");
        return b;
    }

    internal void RequireConfiguration(IConfiguration configuration,string record,string execution)
    {
        var expected=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        { ["Hrm"]=Core,["Procurement"]=record,["ProcurementApprovalExecution"]=execution };
        foreach(var child in configuration.GetSection("ConnectionStrings").AsEnumerable(makePathsRelative:true))
            if(!string.IsNullOrWhiteSpace(child.Value)&&(!expected.TryGetValue(child.Key,out var value)
                ||new NpgsqlConnectionStringBuilder(child.Value).ConnectionString!=new NpgsqlConnectionStringBuilder(value).ConnectionString))
                throw new InvalidOperationException("UndeclaredOrOwnerApplicationConnectionForbidden");
        foreach(var (name,value) in expected)
            if(new NpgsqlConnectionStringBuilder(configuration.GetConnectionString(name)??"").ConnectionString
                !=new NpgsqlConnectionStringBuilder(value).ConnectionString)
                throw new InvalidOperationException("ExplicitApplicationConnectionMissing");
        if(configuration["ERP:AuthMode"]!="local"||configuration["HRM:AuthMode"]!="local"
            ||configuration.GetValue<bool>("ERP:Database:MigrateOnStartup")
            ||configuration.GetValue<bool>("HRM:MonthlyLeaveAccrualEnabled")
            ||!string.IsNullOrWhiteSpace(configuration["HRM:LocalAdminEmail"])
            ||!string.IsNullOrWhiteSpace(configuration["HRM:LocalAdminPassword"])
            ||configuration.GetValue<bool>("CoreDocuments:Enabled")
            ||DisabledFeatures.Any(key=>configuration.GetValue<bool>(key)))
            throw new InvalidOperationException("CoreSessionPrivateNonExecutingHostRequired");
    }
}
