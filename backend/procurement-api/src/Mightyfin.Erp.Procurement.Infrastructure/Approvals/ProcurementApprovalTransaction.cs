using System.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Procurement.Infrastructure.Approvals;

/// <summary>
/// Composes two owning schemas without copying Core approval execution into Procurement.
/// Every attempt uses fresh contexts enlisted in ONE physical PostgreSQL transaction.
/// Callers must re-read/lock the source, case and eligibility in a consistent order inside
/// the callback. Only database work belongs here: record outbox effects, never send them.
/// </summary>
public sealed class ProcurementApprovalTransaction
{
    private readonly string connectionString;
    private readonly TransactionScope scope;

    public ProcurementApprovalTransaction(string connectionString, IProcurementScope procurementScope,
        IApprovalScope approvalScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (string.IsNullOrWhiteSpace(procurementScope.TenantId)
            || procurementScope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(procurementScope.SubjectId)
            || procurementScope.TenantId != approvalScope.TenantId
            || procurementScope.LegalEntityId != approvalScope.LegalEntityId
            || procurementScope.SubjectId != approvalScope.SubjectId)
            throw new UnauthorizedAccessException("A matching authenticated Procurement and Core scope is required.");
        this.connectionString = connectionString;
        scope = new(procurementScope.TenantId, procurementScope.LegalEntityId, procurementScope.SubjectId,
            procurementScope.WorkLocationId, procurementScope.OrgUnitId, procurementScope.IsConfined,
            procurementScope.CorrelationId, procurementScope.WorkerId);
    }

    /// <summary>
    /// The whole callback may run again ONLY after a confirmed serialization/deadlock
    /// rollback. Therefore it must use the same command key, re-read all state and avoid
    /// external effects. Unknown commit/network outcomes are NOT automatically retried;
    /// an explicit same-key command retry must recover its durable recorded outcome.
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<ProcurementDbContext, ApprovalDbContext, CancellationToken, Task<TResult>> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ExecuteAttemptAsync(command, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsConfirmedTransactionAbort(exception))
            {
                // ExecuteAttemptAsync has already disposed both contexts/transaction/connection.
                await Task.Delay(TimeSpan.FromMilliseconds(40 * attempt), cancellationToken);
            }
        }
    }

    private async Task<TResult> ExecuteAttemptAsync<TResult>(
        Func<ProcurementDbContext, ApprovalDbContext, CancellationToken, Task<TResult>> command,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // Session fence must precede BEGIN/snapshot acquisition, including on retries/replays.
        // Declaration order releases the transaction before unlocking the pooled session.
        await using var branchFence = await CoreBranchAuthorizationFence.AcquireAsync(connection,
            scope.TenantId, exclusive: false, ct);
        await using var catalogFence = await ProcurementCatalogAuthorityFence.AcquireAsync(connection,
            scope.TenantId, scope.LegalEntityId, exclusive: false, ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var procurementOptions = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__procurement_migrations", "procurement"))
            .Options;
        var approvalOptions = new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval"))
            .Options;
        await using var procurement = new ProcurementDbContext(procurementOptions, scope);
        await using var approvals = new ApprovalDbContext(approvalOptions, scope);
        await procurement.Database.UseTransactionAsync(transaction, ct);
        await approvals.Database.UseTransactionAsync(transaction, ct);
        procurement.BindInvoiceUnitReviewCoreContext(approvals);

        var outcome = await command(procurement, approvals, ct);
        await procurement.SaveChangesAsync(ct);
        await approvals.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return outcome;
    }

    private static bool IsConfirmedTransactionAbort(Exception exception)
        // Npgsql's execution strategy can wrap DbUpdateException in an
        // InvalidOperationException even when automatic provider retries are off.
        => exception.GetBaseException() is PostgresException
            { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };

    private sealed record TransactionScope(string TenantId, Guid LegalEntityId, string SubjectId,
        Guid? WorkLocationId, Guid? OrgUnitId, bool IsConfined, string? CorrelationId, Guid? WorkerId)
        : IProcurementScope, IApprovalScope;
}
