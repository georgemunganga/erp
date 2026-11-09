using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public enum InvitationMailOutcome { Accepted, Suppressed, Invalid, Retry }

/// <summary>Owns only invitation delivery intent rows. SMTP acceptance is not inbox receipt.</summary>
public sealed class CompanyInvitationDeliveryStore(HrmDbContext db, OutboxPublisherTenantAccessor tenant, TimeProvider clock)
{
    public const string EventType = "core.company.invitation.delivery.requested.v1";

    public async Task<bool> ProcessNextAsync(Func<OutboxMessage,CancellationToken,Task<InvitationMailOutcome>> deliver,
        CancellationToken cancellation)
    {
        if(!db.Database.IsNpgsql())throw new InvalidOperationException("Invitation delivery requires qualified PostgreSQL locking.");
        await using var transaction=await db.Database.BeginTransactionAsync(cancellation);
        // Hold the row until outcome commit. A second worker skips it; a crash
        // rolls back, permitting retry of the same delivery/invitation/message ID.
        var now=clock.GetUtcNow();
        var rows=await db.OutboxMessages.FromSqlInterpolated($@"
            SELECT * FROM hrm.outbox_messages
            WHERE event_type={EventType} AND is_archived=false
              AND status IN ('pending','failed') AND available_at <= {now}
            ORDER BY created_at, id FOR UPDATE SKIP LOCKED LIMIT 1")
            .IgnoreQueryFilters().ToArrayAsync(cancellation);
        if(rows.Length==0)return false;
        var row=rows[0];
        using var scope=tenant.BindAuthoritativeRow(row);
        InvitationMailOutcome outcome;
        try {outcome=await deliver(row,cancellation);}
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested){throw;}
        catch(Exception) {outcome=InvitationMailOutcome.Retry;} // Never persist transport exceptions/addresses/links.
        cancellation.ThrowIfCancellationRequested();
        row.PublishAttempts=Math.Min(row.PublishAttempts,1_000_000)+1;
        row.UpdatedAt=clock.GetUtcNow();row.UpdatedBy="core-invitation-delivery";
        row.LastTransport="kuubu-smtp";
        row.LastError=outcome switch {
            InvitationMailOutcome.Accepted=>null,
            InvitationMailOutcome.Suppressed=>"invitation-current-authority-denied",
            InvitationMailOutcome.Invalid=>"invitation-delivery-invalid",
            _=>"invitation-delivery-unconfirmed"
        };
        row.Status=outcome switch {
            InvitationMailOutcome.Accepted=>"delivery-accepted",
            InvitationMailOutcome.Suppressed=>"delivery-suppressed",
            InvitationMailOutcome.Invalid=>"delivery-invalid",
            _=>row.PublishAttempts>=8?"delivery-exhausted":"failed"
        };
        row.PublishedAt=outcome==InvitationMailOutcome.Accepted?clock.GetUtcNow():null;
        if(row.Status=="failed")row.AvailableAt=clock.GetUtcNow().AddSeconds(Math.Min(900,10*(1<<Math.Min(row.PublishAttempts-1,6))));
        db.AuditEntries.Add(new AuditEntry {
            TenantId=row.TenantId,EntityType="CoreCompanyInvitationDelivery",EntityId=row.Id.ToString("D"),
            Action="core.company.invitation.delivery.attempted",ActorSubjectId="core-invitation-delivery",
            CorrelationId=row.CorrelationId,CreatedBy="core-invitation-delivery",
            AfterJson=System.Text.Json.JsonSerializer.Serialize(new {event_id=row.PublicId,state=row.Status,attempt=row.PublishAttempts})
        });
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return true;
    }
}
