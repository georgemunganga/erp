using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

public static class HrmEventTypes
{
    public const string PayslipReleased = "hrm.payslip.released";
    public const string RequestDecided = "hrm.request.decided";
    public const string LeaveRequested = "hrm.leave.requested";
    public const string LeaveDecided = "hrm.leave.decided";
    public const string LeaveCancelled = "hrm.leave.cancelled";
    public const string OvertimeDecided = "hrm.overtime.decided";
    public const string IntegrationReady = "hrm.integration.ready";
    public const string AccountAccessLink = "hrm.account.access-link";
}

/// <summary>Adds an event to the current scoped DbContext. The surrounding
/// unit of work decides when the transaction commits.</summary>
public interface IOutboxWriter
{
    Task<OutboxMessage> EnqueueAsync(
        string eventType,
        string subjectId,
        object privacySafePayload,
        CancellationToken ct);
}

/// <summary>Runs all repository saves made by an operation under one database
/// transaction. Repositories share the same scoped DbContext.</summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct);
}

/// <summary>Operational outbox metadata safe for HR administrators. Event
/// payloads and recipient addresses are deliberately excluded.</summary>
public sealed record NotificationDeliveryDto(
    Guid Id,
    string PublicId,
    string EventType,
    string Status,
    int PublishAttempts,
    string? LastTransport,
    string? LastError,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset AvailableAt,
    DateTimeOffset? PublishedAt);

public sealed record NotificationDeliverySummaryDto(
    int Pending,
    int Publishing,
    int Published,
    int Failed,
    int FallbackDelivered,
    List<NotificationDeliveryDto> Items);

public interface INotificationDeliveryService
{
    Task<NotificationDeliverySummaryDto> ListAsync(
        string? eventType, string? status, int limit, CancellationToken ct);
    Task<NotificationDeliveryDto> RetryAsync(Guid id, CancellationToken ct);
}

public sealed record EmployeeNotificationDto(
    Guid Id,
    string EventType,
    string Title,
    string Status,
    string ActionUrl,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record EmployeeNotificationInboxDto(
    int UnreadCount,
    List<EmployeeNotificationDto> Items);

public interface IEmployeeNotificationService
{
    Task<EmployeeNotificationInboxDto> ListAsync(string subjectId, CancellationToken ct);
    Task<EmployeeNotificationDto> MarkReadAsync(Guid id, string subjectId, CancellationToken ct);
    Task<int> MarkAllReadAsync(string subjectId, CancellationToken ct);
}

/// <summary>Minimum recipient and payslip facts needed to build a privacy-safe
/// notification event after payslips are finalized.</summary>
public sealed record PayslipNotificationTarget(
    Guid PayslipId,
    string PayslipNo,
    string PeriodLabel,
    Guid WorkerId,
    string? SubjectId,
    string? Email,
    string? PersonalEmail,
    string FirstName,
    string LastName);
