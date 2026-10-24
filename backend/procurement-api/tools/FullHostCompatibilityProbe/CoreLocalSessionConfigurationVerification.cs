using Microsoft.Extensions.Configuration;
using Npgsql;

/// <summary>Pure existing-guard regression; no host startup, SQL, roles or actor data.</summary>
internal static class CoreLocalSessionConfigurationVerification
{
    internal static void Verify()
    {
        var previous=Environment.GetEnvironmentVariable(RestoredCoreSessionConnections.OptIn);
        try
        {
            string Connection(string kind)=>new NpgsqlConnectionStringBuilder
            { Host="/private-pg-socket",Database="proc_p30_core_session_20261024_01",
                Username="p30_"+kind+"_login_01",Password=new string('a',64),Pooling=true,
                Multiplexing=false,NoResetOnClose=false,IncludeErrorDetail=false,LogParameters=false }.ConnectionString;
            var core=new RestoredCoreSessionConnections(Connection("core_session"),"p30-core-session-01");
            var record=Connection("record");var execution=Connection("execute");
            Environment.SetEnvironmentVariable(RestoredCoreSessionConnections.OptIn,"reviewed-private-only");
            using(var selected=new PrincipalApprovalHost(new PrincipalApprovalFixture("",false),"",record,execution,
                actualSchema:true,normalLocalAuthentication:true,principalExecutionEnabled:false,restoredCore:core))
            {
                var baseline=selected.Settings();
                core.RequireConfiguration(new ConfigurationBuilder().AddInMemoryCollection(baseline).Build(),record,execution);
                foreach(var key in new[]{"Procurement:InvoiceUnitReview:Enabled","Procurement:ManualPayments:Enabled"})
                {
                    if(!baseline.TryGetValue(key,out var value)||value!="false")
                        throw new InvalidOperationException("SelectedCoreMustOverrideEveryOperationalFeature");
                    // An inherited/late provider leaving either effective value true
                    // must fail through the ACTUAL guard before any host can start.
                    var configuration=new ConfigurationBuilder().AddInMemoryCollection(baseline)
                        .AddInMemoryCollection(new Dictionary<string,string?> {[key]="true"}).Build();
                    var denied=false;
                    try {core.RequireConfiguration(configuration,record,execution);}
                    catch(InvalidOperationException error) when(error.Message=="CoreSessionPrivateNonExecutingHostRequired") {denied=true;}
                    if(!denied)throw new InvalidOperationException("InheritedOperationalFeatureWasAdmitted");
                }
            }
            Environment.SetEnvironmentVariable(RestoredCoreSessionConnections.OptIn,null);
            const string legacyOwner="Host=private-unused-example;Database=legacy_private_example;Username=legacy_private_owner";
            using var legacy=new PrincipalApprovalHost(new PrincipalApprovalFixture(legacyOwner,false),legacyOwner,record,execution,actualSchema:true);
            var legacySettings=legacy.Settings();
            if(legacySettings.ContainsKey("Procurement:InvoiceUnitReview:Enabled")||legacySettings.ContainsKey("Procurement:ManualPayments:Enabled")
                ||new NpgsqlConnectionStringBuilder(legacySettings["ConnectionStrings:Hrm"]!).Username!="legacy_private_owner")
                throw new InvalidOperationException("UnselectedPrivateModeChanged");
        }
        finally {Environment.SetEnvironmentVariable(RestoredCoreSessionConnections.OptIn,previous);}
    }
}
