using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed record InvitationDeliveryHealth(long Queued,long RetryDue,long RetryWaiting,long Accepted,long Suppressed,
    long Invalid,long Exhausted,long Unrecognized,DateTimeOffset? OldestDueAt);
public sealed record InvitationDeliveryOperatorRequest(Guid Tenant,Guid Event,Guid Key,int ExpectedAttempts,string Operator,string Reason);
public enum InvitationDeliveryOperatorStatus { Attempted,Existing,NotEligible,BusyOrMissing,Conflict }
public sealed record InvitationDeliveryOperatorResult(InvitationDeliveryOperatorStatus Status,string? State=null,int? Attempt=null);

/// <summary>Local operator authority, not a customer API. Never resets attempts or revives terminal mail.</summary>
public sealed class CompanyInvitationDeliveryOperations(HrmDbContext db,OutboxPublisherTenantAccessor tenant,TimeProvider clock)
{
    public const string AuditAction="core.invitation.delivery.operator.retry";
    public static bool Valid(InvitationDeliveryOperatorRequest request)=>request.Tenant!=Guid.Empty && request.Event!=Guid.Empty && request.Key!=Guid.Empty
        && request.ExpectedAttempts is >=1 and <8 && request.Operator is not null
        && Regex.IsMatch(request.Operator,@"\A[a-z0-9][a-z0-9_-]{7,63}\z",RegexOptions.CultureInvariant)
        && request.Reason is "relay-recovered" or "connectivity-restored" or "operator-verified";

    public async Task<InvitationDeliveryHealth> ReportAsync(CancellationToken cancellation)
    {
        RequirePostgres();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,cancellation);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",cancellation);
        var now=clock.GetUtcNow();
        var report=await db.Database.SqlQuery<InvitationDeliveryHealth>($@"
            SELECT count(*) FILTER (WHERE status='pending') AS ""Queued"",
              count(*) FILTER (WHERE status='failed' AND available_at<={now}) AS ""RetryDue"",
              count(*) FILTER (WHERE status='failed' AND available_at>{now}) AS ""RetryWaiting"",
              count(*) FILTER (WHERE status='delivery-accepted') AS ""Accepted"",
              count(*) FILTER (WHERE status='delivery-suppressed') AS ""Suppressed"",
              count(*) FILTER (WHERE status='delivery-invalid') AS ""Invalid"",
              count(*) FILTER (WHERE status='delivery-exhausted') AS ""Exhausted"",
              count(*) FILTER (WHERE event_version<>'1' OR publish_attempts<0 OR publish_attempts>8
                OR status NOT IN ('pending','failed','delivery-accepted','delivery-suppressed','delivery-invalid','delivery-exhausted')
                OR (status='pending' AND publish_attempts<>0)
                OR (status='failed' AND (publish_attempts<1 OR publish_attempts>=8))
                OR (status='delivery-accepted' AND (publish_attempts<1 OR published_at IS NULL))
                OR (status='delivery-exhausted' AND publish_attempts<>8)
                OR (status IN ('delivery-suppressed','delivery-invalid','delivery-exhausted') AND (publish_attempts<1 OR published_at IS NOT NULL))) AS ""Unrecognized"",
              min(available_at) FILTER (WHERE status IN ('pending','failed') AND available_at<={now}) AS ""OldestDueAt""
            FROM hrm.outbox_messages WHERE event_type={CompanyInvitationDeliveryStore.EventType} AND is_archived=false")
            .SingleAsync(cancellation);
        await transaction.CommitAsync(cancellation);return report;
    }

    public async Task<InvitationDeliveryOperatorResult> RetryAsync(InvitationDeliveryOperatorRequest request,
        Func<OutboxMessage,CancellationToken,Task<InvitationMailOutcome>> deliver,CancellationToken cancellation)
    {
        if(!Valid(request))throw new InvalidOperationException("Reviewed invitation operator arguments are required.");
        RequirePostgres();
        await using var transaction=await db.Database.BeginTransactionAsync(cancellation);
        var scopeId=request.Tenant.ToString("D");var eventId=request.Event.ToString("D");
        var rows=await db.OutboxMessages.FromSqlInterpolated($@"
            SELECT * FROM hrm.outbox_messages WHERE tenant_id={scopeId} AND public_id={eventId}
              AND event_type={CompanyInvitationDeliveryStore.EventType} AND is_archived=false
            FOR UPDATE SKIP LOCKED LIMIT 1").IgnoreQueryFilters().ToArrayAsync(cancellation);
        if(rows.Length!=1)return new(InvitationDeliveryOperatorStatus.BusyOrMissing);
        var row=rows[0];using var binding=tenant.BindAuthoritativeRow(row);
        var correlation=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.Operator+"\n"+request.Key.ToString("D"))));
        // Keys are scoped to this operator + tenant + event. The row lock also
        // serializes receipt lookup/creation with automatic and manual workers.
        var receipts=await db.AuditEntries.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==row.TenantId
            && x.EntityType=="CoreCompanyInvitationDelivery" && x.EntityId==row.Id.ToString()
            && x.Action==AuditAction && x.CorrelationId==correlation).Take(2).ToArrayAsync(cancellation);
        if(receipts.Length>0) {
            if(receipts.Length!=1 || receipts[0].ActorSubjectId!="core-invitation-operator#"+request.Operator)return new(InvitationDeliveryOperatorStatus.Conflict);
            try {
                using var packet=JsonDocument.Parse(receipts[0].AfterJson!,new JsonDocumentOptions{MaxDepth=2});var root=packet.RootElement;
                var names=root.EnumerateObject().Select(x=>x.Name).ToArray();
                if(names.Length!=5 || names.Distinct(StringComparer.Ordinal).Count()!=5
                    || root.GetProperty("event_id").GetString()!=eventId || root.GetProperty("reason").GetString()!=request.Reason
                    || root.GetProperty("expected_attempts").GetInt32()!=request.ExpectedAttempts)return new(InvitationDeliveryOperatorStatus.Conflict);
                var state=root.GetProperty("state").GetString();var attempt=root.GetProperty("attempt").GetInt32();
                if(attempt!=request.ExpectedAttempts+1 || state is not ("failed" or "delivery-accepted" or "delivery-suppressed" or "delivery-invalid" or "delivery-exhausted"))
                    return new(InvitationDeliveryOperatorStatus.Conflict);
                return new(InvitationDeliveryOperatorStatus.Existing,state,attempt);
            }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return new(InvitationDeliveryOperatorStatus.Conflict);}
        }
        var now=clock.GetUtcNow();
        if(row.Status!="failed" || row.PublishAttempts!=request.ExpectedAttempts || row.AvailableAt>now || row.PublishedAt is not null
            || row.LastTransport!="kuubu-smtp" || row.LastError!="invitation-delivery-unconfirmed" || row.EventVersion!="1"
            || !Guid.TryParseExact(row.SubjectId,"D",out var invitationId)
            || !await db.CompanyInvitations.IgnoreQueryFilters().AnyAsync(x=>x.TenantId==row.TenantId && x.Id==invitationId
                && !x.IsArchived && x.State=="pending" && x.ExpiresAt>now,cancellation))return new(InvitationDeliveryOperatorStatus.NotEligible);
        await CompanyInvitationDeliveryStore.ApplyAttemptAsync(db,row,deliver,clock,cancellation);
        db.AuditEntries.Add(new AuditEntry {TenantId=row.TenantId,EntityType="CoreCompanyInvitationDelivery",EntityId=row.Id.ToString("D"),
            Action=AuditAction,ActorSubjectId="core-invitation-operator#"+request.Operator,CreatedBy="core-invitation-operator#"+request.Operator,
            CorrelationId=correlation,AfterJson=JsonSerializer.Serialize(new{event_id=eventId,reason=request.Reason,
                expected_attempts=request.ExpectedAttempts,state=row.Status,attempt=row.PublishAttempts})});
        await db.SaveChangesAsync(cancellation);await transaction.CommitAsync(cancellation);
        return new(InvitationDeliveryOperatorStatus.Attempted,row.Status,row.PublishAttempts);
    }
    private void RequirePostgres(){if(!db.Database.IsNpgsql())throw new InvalidOperationException("Invitation operations require qualified PostgreSQL locking.");}
}
