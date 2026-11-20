using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed class CoreCompanyInvitationDeliveryWorker(
    Func<OutboxPublisherTenantAccessor,HrmDbContext> contextFactory,CoreCompanyInvitationMail mail,
    ILogger<CoreCompanyInvitationDeliveryWorker> logger):BackgroundService
{
    internal static bool Enabled(IConfiguration config)=>ProductCompanyInvitationsFlow.Enabled(config)
        && ProductInvitationAcceptanceFlow.Enabled(config)
        && config.GetValue<bool>("ERP:CompanyInvitationDelivery:Enabled");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested) {
            try {
                var tenant=new OutboxPublisherTenantAccessor();
                await using var db=contextFactory(tenant);
                using var budget=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(45));
                var store=new CompanyInvitationDeliveryStore(db,tenant,TimeProvider.System);
                await store.ProcessNextAsync(mail.DeliverAsync,budget.Token);
            }catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception){logger.LogWarning("Kuubu invitation delivery cycle is unconfirmed; durable state will be retried.");}
            try {await Task.Delay(TimeSpan.FromSeconds(1),stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
        }
    }
}
