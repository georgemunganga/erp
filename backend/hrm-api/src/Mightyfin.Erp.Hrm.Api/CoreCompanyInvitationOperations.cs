using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record InvitationOperationsCommand(bool Report,InvitationDeliveryOperatorRequest? Retry);

/// <summary>Default-off local service-account command. No HTTP host, schema owner or identity bootstrap.</summary>
internal static class CoreCompanyInvitationOperations
{
    internal const string Mode="--run-invitation-delivery-operations";
    internal static InvitationOperationsCommand Parse(string[] args,IConfiguration config)
    {
        if(!config.GetValue<bool>("ERP:CompanyInvitationOperations:Enabled") || args.Count(x=>x==Mode)!=1)
            throw new InvalidOperationException("Invitation operations are not enabled.");
        var fields=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var argument in args.Where(x=>x!=Mode)) {
            var parts=argument.Split('=',2);
            if(parts.Length!=2 || parts[1].Length>128 || !fields.TryAdd(parts[0],parts[1]))throw new InvalidOperationException("Invalid operator arguments.");
        }
        if(fields.Count==1 && fields.GetValueOrDefault("--operation")=="report")return new(true,null);
        if(!config.GetValue<bool>("ERP:CompanyInvitationOperations:RetryEnabled") || !CoreCompanyInvitationDeliveryWorker.Enabled(config)
            || fields.Count!=6 || fields.GetValueOrDefault("--operation")!="retry"
            || !Canonical(fields.GetValueOrDefault("--tenant"),out var tenant) || !Canonical(fields.GetValueOrDefault("--event"),out var eventId)
            || !Canonical(fields.GetValueOrDefault("--command-key"),out var key)
            || !int.TryParse(fields.GetValueOrDefault("--expected-attempts"),out var attempts))throw new InvalidOperationException("Invalid operator arguments.");
        var request=new InvitationDeliveryOperatorRequest(tenant,eventId,key,attempts,
            config["ERP:CompanyInvitationOperations:Operator"]??"",fields.GetValueOrDefault("--reason")??"");
        if(!CompanyInvitationDeliveryOperations.Valid(request))throw new InvalidOperationException("Invalid operator arguments.");
        return new(false,request);
    }
    private static bool Canonical(string? value,out Guid result)=>Guid.TryParseExact(value,"D",out result) && result!=Guid.Empty && result.ToString("D")==value;

    internal static async Task<int> RunAsync(IConfiguration config,string[] args,CancellationToken stopping=default)
    {
        InvitationOperationsCommand command;
        try {command=Parse(args,config);}
        catch(Exception){Console.WriteLine("{\"schema_version\":1,\"status\":\"invalid-or-disabled\"}");return 2;}
        try {
            var connection=config.GetConnectionString("InvitationOperations");
            if(string.IsNullOrWhiteSpace(connection)){Console.WriteLine("{\"schema_version\":1,\"status\":\"configuration-required\"}");return 2;}
            var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm")).Options;
            var tenant=new OutboxPublisherTenantAccessor();await using var db=new HrmDbContext(options,tenant);
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(stopping);budget.CancelAfter(TimeSpan.FromSeconds(45));
            var operations=new CompanyInvitationDeliveryOperations(db,tenant,TimeProvider.System);
            if(command.Report) {
                var seconds=config.GetValue<int?>("ERP:CompanyInvitationOperations:StaleAfterSeconds")??600;
                if(seconds is <30 or >86400)throw new InvalidOperationException("Invalid report threshold.");
                var report=await operations.ReportAsync(budget.Token);
                var attention=report.Exhausted>0 || report.Invalid>0 || report.Unrecognized>0
                    || report.OldestDueAt is { } oldest && oldest<TimeProvider.System.GetUtcNow().AddSeconds(-seconds);
                Console.WriteLine(JsonSerializer.Serialize(new{schema_version=1,status=attention?"attention":"ok",delivery_enabled=CoreCompanyInvitationDeliveryWorker.Enabled(config),report}));
                return attention?3:0;
            }
            // Retry uses exactly the worker's licensing, original inviter,
            // envelope, company lock and mandatory SMTP validation path.
            var services=new ServiceCollection();services.AddLogging(logging=>logging.ClearProviders());services.AddHttpContextAccessor();
            services.AddProductIdentity(config);services.AddProductCommercialLicense(config);
            services.AddCoreCompanyTeamApi(config,connection,runInvitationDelivery:false);
            services.AddDataProtection().DisableAutomaticKeyGeneration();
            await using var provider=services.BuildServiceProvider();
            var clock=TimeProvider.System;
            var mail=new CoreCompanyInvitationMail(t=>new HrmDbContext(options,new OperationsTenant(t)),provider.GetRequiredService<CoreCompanyTeamApi>(),
                provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose),
                new SmtpCompanyInvitationMail(config,clock),clock,config["ERP:ProductIdentity:ClientId"]!);
            var result=await operations.RetryAsync(command.Retry!,mail.DeliverAsync,budget.Token);
            Console.WriteLine(JsonSerializer.Serialize(new{schema_version=1,status=result.Status.ToString(),outcome_state=result.State,attempt=result.Attempt,
                historical=result.Status==InvitationDeliveryOperatorStatus.Existing}));
            return (result.Status is InvitationDeliveryOperatorStatus.Attempted or InvitationDeliveryOperatorStatus.Existing) && result.State=="delivery-accepted"?0:3;
        }catch(OperationCanceledException){Console.WriteLine("{\"schema_version\":1,\"status\":\"unconfirmed\"}");return 4;}
        catch(Exception){Console.WriteLine("{\"schema_version\":1,\"status\":\"unavailable\"}");return 4;}
    }
    private sealed class OperationsTenant(string tenant):ITenantAccessor {public string GetTenantId()=>tenant;}
}
