using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class EfUnitOfWork(HrmDbContext db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await operation(ct);
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await operation(ct);
            await transaction.CommitAsync(ct);
        });
    }
}

public sealed class EfOutboxWriter(
    HrmDbContext db,
    IConfiguration configuration,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContext) : IOutboxWriter
{
    public async Task<OutboxMessage> EnqueueAsync(
        string eventType,
        string subjectId,
        object privacySafePayload,
        CancellationToken ct)
    {
        if (!eventType.StartsWith("hrm.", StringComparison.Ordinal))
            throw new ArgumentException("HRM event types must start with 'hrm.'.", nameof(eventType));

        var correlationId = httpContext.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.HttpContext?.TraceIdentifier
            ?? $"corr_{Guid.NewGuid():N}";
        var environment = configuration["HRM:Environment"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]?.ToLowerInvariant()
            ?? "production";
        var message = new OutboxMessage
        {
            PublicId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            EventVersion = "1",
            Environment = environment,
            SubjectId = subjectId,
            CorrelationId = correlationId,
            PayloadJson = JsonSerializer.Serialize(privacySafePayload),
            Status = "pending",
            AvailableAt = DateTimeOffset.UtcNow,
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }
}

public interface IOutboxPublisherStore
{
    Task<List<OutboxMessage>> ClaimAsync(int limit, CancellationToken ct);
    Task CompleteAsync(Guid id, bool success, string transport, string? error, CancellationToken ct);
}

public sealed class EfOutboxPublisherStore(HrmDbContext db) : IOutboxPublisherStore
{
    public async Task<List<OutboxMessage>> ClaimAsync(int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var rows = await db.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT * FROM hrm.outbox_messages
                WHERE available_at <= now()
                  AND (status IN ('pending', 'failed')
                       OR (status = 'publishing' AND updated_at < {staleBefore}))
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT {limit}")
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            row.Status = "publishing";
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task CompleteAsync(Guid id, bool success, string transport, string? error, CancellationToken ct)
    {
        var row = await db.OutboxMessages.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct);
        row.PublishAttempts++;
        row.LastTransport = transport;
        row.LastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, 2000)];
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (success)
        {
            row.Status = transport == "smtp" ? "fallback-delivered" : "published";
            row.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            row.Status = "failed";
            var delaySeconds = Math.Min(900, 10 * (int)Math.Pow(2, Math.Min(row.PublishAttempts - 1, 6)));
            row.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class NotificationDeliveryService(
    HrmDbContext db,
    IAuthzService authz) : INotificationDeliveryService
{
    public async Task<NotificationDeliverySummaryDto> ListAsync(
        string? eventType, string? status, int limit, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        limit = Math.Clamp(limit, 1, 200);
        var tenantRows = db.OutboxMessages.AsNoTracking();
        var pending = await tenantRows.CountAsync(x => x.Status == "pending", ct);
        var publishing = await tenantRows.CountAsync(x => x.Status == "publishing", ct);
        var published = await tenantRows.CountAsync(x => x.Status == "published", ct);
        var failed = await tenantRows.CountAsync(x => x.Status == "failed", ct);
        var fallbackDelivered = await tenantRows.CountAsync(x => x.Status == "fallback-delivered", ct);

        var query = tenantRows;
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(x => x.EventType == eventType.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim().ToLowerInvariant());
        // Entity ids are UUIDv7, so descending id preserves creation order and
        // remains portable across PostgreSQL and the SQLite test provider.
        var rows = await query.OrderByDescending(x => x.Id).Take(limit).ToListAsync(ct);
        return new NotificationDeliverySummaryDto(
            pending, publishing, published, failed, fallbackDelivered,
            rows.Select(MapDelivery).ToList());
    }

    public async Task<NotificationDeliveryDto> RetryAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var row = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("notification-not-found", $"Notification {id} does not exist.");
        if (row.Status != "failed")
            throw new DomainException("notification-not-retryable", $"Notification is {row.Status}; only failed notifications can be retried.");
        row.Status = "pending";
        row.AvailableAt = DateTimeOffset.UtcNow;
        row.LastError = null;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapDelivery(row);
    }

    private static NotificationDeliveryDto MapDelivery(OutboxMessage row) => new(
        row.Id, row.PublicId, row.EventType, row.Status, row.PublishAttempts,
        row.LastTransport, SanitizeError(row.LastError), row.CorrelationId,
        row.CreatedAt, row.AvailableAt, row.PublishedAt);

    private static string? SanitizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        var oneLine = string.Join(" ", error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return oneLine[..Math.Min(oneLine.Length, 240)];
    }
}

public sealed class EmployeeNotificationService(HrmDbContext db, IAuthzService authz) : IEmployeeNotificationService
{
    public async Task<EmployeeNotificationInboxDto> ListAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll");
        RequireSubject(subjectId);
        var rows = await db.OutboxMessages.AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .OrderByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(ct);
        return new EmployeeNotificationInboxDto(rows.Count(x => x.EmployeeReadAt is null), rows.Select(MapEmployee).ToList());
    }

    public async Task<EmployeeNotificationDto> MarkReadAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll");
        RequireSubject(subjectId);
        var row = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct)
            ?? throw new DomainException("notification-not-owned", "The notification does not belong to the signed-in worker.");
        row.EmployeeReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapEmployee(row);
    }

    public async Task<int> MarkAllReadAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll");
        RequireSubject(subjectId);
        var rows = await db.OutboxMessages.Where(x => x.SubjectId == subjectId && x.EmployeeReadAt == null).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows) row.EmployeeReadAt = now;
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }

    private static void RequireSubject(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) throw new DomainException("no-subject-claim", "The request carries no identity claim.");
    }

    private static EmployeeNotificationDto MapEmployee(OutboxMessage row)
    {
        var (title, url) = row.EventType switch
        {
            HrmEventTypes.PayslipReleased => ("Your payslip is ready", "/hrm/payslips"),
            HrmEventTypes.RequestDecided => ("Your HR request was updated", "/hrm/requests"),
            HrmEventTypes.LeaveRequested => ("Your leave request was submitted", "/hrm/leave"),
            HrmEventTypes.LeaveDecided => ("Your leave request was decided", "/hrm/leave"),
            HrmEventTypes.LeaveCancelled => ("Your leave request was cancelled", "/hrm/leave"),
            _ => ("HR update", "/hrm/self-service")
        };
        return new EmployeeNotificationDto(row.Id, row.EventType, title, row.Status, url, row.EmployeeReadAt is not null, row.CreatedAt);
    }
}

public interface IHrmEventPublisher : IAsyncDisposable
{
    Task EnsureStreamAsync(CancellationToken ct);
    Task PublishAsync(OutboxMessage row, CancellationToken ct);
}

public sealed class NatsHrmEventPublisher : IHrmEventPublisher
{
    private readonly NatsClient client;
    private readonly INatsJSContext jetStream;

    public NatsHrmEventPublisher(IConfiguration configuration)
    {
        var url = configuration["HRM:NatsUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("HRM:NatsUrl is required by the outbox publisher.");
        var token = configuration["HRM:NatsToken"];
        var tokenFile = configuration["HRM:NatsTokenFile"];
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile))
            token = File.ReadAllText(tokenFile).Trim();
        var opts = NatsOpts.Default with
        {
            Url = url,
            AuthOpts = NatsAuthOpts.Default with { Token = token },
        };
        client = new NatsClient(opts);
        jetStream = client.CreateJetStreamContext();
    }

    public async Task EnsureStreamAsync(CancellationToken ct)
    {
        await jetStream.CreateOrUpdateStreamAsync(
            new StreamConfig("HRM_EVENTS", ["mightyfin.hrm.>"]) { Storage = StreamConfigStorage.File }, ct);
    }

    public async Task PublishAsync(OutboxMessage row, CancellationToken ct)
    {
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = row.PublicId,
            type = row.EventType,
            version = row.EventVersion,
            occurred_at = row.CreatedAt.UtcDateTime,
            tenant_id = row.TenantId,
            environment = row.Environment,
            subject_id = row.SubjectId,
            correlation_id = row.CorrelationId,
            data = JsonDocument.Parse(row.PayloadJson).RootElement,
        });
        var ack = await jetStream.PublishAsync(
            $"mightyfin.{row.EventType}",
            envelope,
            opts: new NatsJSPubOpts { MsgId = row.PublicId },
            cancellationToken: ct);
        ack.EnsureSuccess();
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}

public interface ISmtpNotificationFallback
{
    bool Enabled { get; }
    bool CanDeliver(OutboxMessage row);
    Task DeliverAsync(OutboxMessage row, CancellationToken ct);
}

/// <summary>Emergency-only direct delivery. It is inert unless
/// HRM:NotificationFallback is exactly "smtp" and all SMTP settings exist.</summary>
public sealed class SmtpNotificationFallback : ISmtpNotificationFallback
{
    private readonly IConfiguration configuration;
    public bool Enabled { get; }

    public SmtpNotificationFallback(IConfiguration configuration)
    {
        this.configuration = configuration;
        Enabled = string.Equals(configuration["HRM:NotificationFallback"], "smtp", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanDeliver(OutboxMessage row)
    {
        if (row.EventType is not (
            HrmEventTypes.PayslipReleased or
            HrmEventTypes.RequestDecided or
            HrmEventTypes.LeaveRequested or
            HrmEventTypes.LeaveDecided or
            HrmEventTypes.LeaveCancelled or
            HrmEventTypes.AccountAccessLink))
            return false;
        try
        {
            using var payload = JsonDocument.Parse(row.PayloadJson);
            return (payload.RootElement.TryGetProperty("emails", out var emails) && emails.ValueKind == JsonValueKind.Array &&
                    emails.EnumerateArray().Any(value => !string.IsNullOrWhiteSpace(value.GetString()))) ||
                (payload.RootElement.TryGetProperty("email", out var value) && !string.IsNullOrWhiteSpace(value.GetString()));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task DeliverAsync(OutboxMessage row, CancellationToken ct)
    {
        if (!Enabled)
            throw new InvalidOperationException("SMTP fallback is not enabled.");
        if (!CanDeliver(row))
            throw new InvalidOperationException($"No SMTP fallback template exists for {row.EventType}.");
        using var payload = JsonDocument.Parse(row.PayloadJson);
        var root = payload.RootElement;
        var recipients = root.TryGetProperty("emails", out var emails) && emails.ValueKind == JsonValueKind.Array
            ? emails.EnumerateArray().Select(value => value.GetString()?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : [Required(root, "email")];
        if (recipients.Count == 0) throw new InvalidOperationException("No email recipient was supplied.");
        var firstName = Optional(root, "first_name");
        var portalUrl = configuration["HRM:PublicUrl"]?.TrimEnd('/') ?? "https://erp.mightyfinance.co.zm";
        var (subject, plainBody, htmlBody) = row.EventType switch
        {
            HrmEventTypes.PayslipReleased => (
                "Your payslip is available",
                $"Hello {firstName},\n\nYour payslip for {Optional(root, "period_label")} is now available.\nSign in to view it: {portalUrl}/hrm/payslips/{Optional(root, "payslip_id")}\n",
                BuildHtmlEmail(
                    $"Your payslip for {Optional(root, "period_label")} is now available.",
                    firstName,
                    $"View payslip",
                    $"{portalUrl}/hrm/payslips/{Optional(root, "payslip_id")}",
                    "If you did not expect this message, contact HR.")
            ),
            HrmEventTypes.RequestDecided => (
                "Your HR request was updated",
                $"Hello {firstName},\n\nThe status of your HR request is now {Optional(root, "status")}.\nSign in to view it: {portalUrl}/hrm/requests/{Optional(root, "request_id")}\n",
                BuildHtmlEmail(
                    $"The status of your HR request is now {Optional(root, "status")}.",
                    firstName,
                    $"View request",
                    $"{portalUrl}/hrm/requests/{Optional(root, "request_id")}",
                    "If you did not expect this message, contact HR.")
            ),
            HrmEventTypes.LeaveRequested => (
                "Your leave request was submitted",
                $"Hello {firstName},\n\nYour {Optional(root, "leave_type_code")} leave request has been submitted.\nSign in to view it: {portalUrl}/hrm/leave\n",
                BuildHtmlEmail(
                    $"Your {Optional(root, "leave_type_code")} leave request has been submitted.",
                    firstName,
                    $"View leave request",
                    $"{portalUrl}/hrm/leave",
                    "If you did not expect this message, contact HR.")
            ),
            HrmEventTypes.LeaveDecided => (
                "Your leave request was updated",
                $"Hello {firstName},\n\nYour leave request is now {Optional(root, "status")}.\nSign in to view it: {portalUrl}/hrm/leave\n",
                BuildHtmlEmail(
                    $"Your leave request is now {Optional(root, "status")}.",
                    firstName,
                    $"View leave request",
                    $"{portalUrl}/hrm/leave",
                    "If you did not expect this message, contact HR.")
            ),
            HrmEventTypes.LeaveCancelled => (
                "Your leave request was cancelled",
                $"Hello {firstName},\n\nYour leave request has been cancelled.\nSign in to view it: {portalUrl}/hrm/leave\n",
                BuildHtmlEmail(
                    $"Your leave request has been cancelled.",
                    firstName,
                    $"View leave request",
                    $"{portalUrl}/hrm/leave",
                    "If you did not expect this message, contact HR.")
            ),
            HrmEventTypes.AccountAccessLink => (
                "Set up your NewWorldCargo HRM account",
                $"Hello {firstName},\n\nAn HR administrator created or reset your account. Set your password using this one-time link: {Optional(root, "account_link")}\n\nThis link expires on {Optional(root, "expires_at")}.\n",
                BuildHtmlEmail(
                    "An HR administrator created or reset your account. Use the secure link below to choose your password.",
                    firstName,
                    "Set password",
                    Required(root, "account_link"),
                    $"This one-time link expires on {Optional(root, "expires_at")}. If you did not expect this message, contact HR.")
            ),
            _ => throw new InvalidOperationException($"No SMTP fallback template exists for {row.EventType}."),
        };

        var host = RequiredConfig("HRM:Smtp:Host");
        var from = RequiredConfig("HRM:Smtp:From");
        var fromName = configuration["HRM:Smtp:FromName"] ?? "NewWorldCargo HRM";
        var port = int.TryParse(configuration["HRM:Smtp:Port"], out var parsedPort) ? parsedPort : 465;
        var timeoutMs = int.TryParse(configuration["HRM:Smtp:TimeoutMs"], out var parsedTimeout)
            ? Math.Clamp(parsedTimeout, 5000, 120000)
            : 20000;
        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = plainBody,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        foreach (var recipient in recipients) message.To.Add(recipient);
        message.ReplyToList.Add(new MailAddress(configuration["HRM:Smtp:ReplyTo"] ?? from, fromName));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
        message.Headers["X-Mailer"] = "NewWorldCargo HRM";
        message.Headers["X-Entity-Ref-ID"] = row.PublicId;
        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(configuration["HRM:Smtp:UseTls"], "false", StringComparison.OrdinalIgnoreCase),
            Timeout = timeoutMs,
        };
        var username = configuration["HRM:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            smtp.Credentials = new NetworkCredential(username, RequiredConfig("HRM:Smtp:Password"));
        await smtp.SendMailAsync(message, ct);
    }

    private string RequiredConfig(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is required when SMTP fallback is enabled.");
    private static string Required(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Outbox payload is missing {key}.");
    private static string Optional(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";

    private static string BuildHtmlEmail(string intro, string firstName, string actionText, string actionUrl, string footer)
    {
        var safeIntro = WebUtility.HtmlEncode(intro);
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(firstName) ? "there" : firstName);
        var safeAction = WebUtility.HtmlEncode(actionText);
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var safeFooter = WebUtility.HtmlEncode(footer);
        return $"""
<!doctype html>
<html>
  <body style="margin:0;padding:0;background:#f6f8fb;color:#102033;font-family:Arial,Helvetica,sans-serif;">
    <div style="max-width:640px;margin:0 auto;padding:32px 20px;">
      <div style="background:#ffffff;border:1px solid #e3e8ef;border-radius:12px;padding:28px;">
        <p style="margin:0 0 16px;font-size:16px;line-height:1.5;">Hello {safeName},</p>
        <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">{safeIntro}</p>
        <p style="margin:0 0 24px;">
          <a href="{safeUrl}" style="display:inline-block;background:#012642;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:8px;font-size:15px;">{safeAction}</a>
        </p>
        <p style="margin:0;font-size:13px;line-height:1.6;color:#516173;">{safeFooter}</p>
      </div>
    </div>
  </body>
  </html>
""";
    }
}
