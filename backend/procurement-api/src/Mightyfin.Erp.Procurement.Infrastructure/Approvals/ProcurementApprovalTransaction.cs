using System.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Infrastructure;

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
            || procurementScope.SubjectId != approvalScope.SubjectId
            || procurementScope.IsExplicitAllBranches
                && (procurementScope.WorkLocationId is not null || procurementScope.OrgUnitId is not null))
            throw new UnauthorizedAccessException("A matching authenticated Procurement and Core scope is required.");
        this.connectionString = connectionString;
        scope = new(procurementScope.TenantId, procurementScope.LegalEntityId, procurementScope.SubjectId,
            procurementScope.WorkLocationId, procurementScope.OrgUnitId, procurementScope.IsConfined,
            procurementScope.CorrelationId, procurementScope.WorkerId, procurementScope.CoreLinkedWorkerId,
            procurementScope.IsExplicitAllBranches);
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
        => await ExecuteCommandAsync(command, manualPayments: false, cancellationToken);

    /// <summary>Same Core transaction/fences/retry semantics, with mandatory manual
    /// payment workflow and company-date companions. Does not enable an HTTP route.</summary>
    public async Task<TResult> ExecuteManualPaymentAsync<TResult>(
        Func<ProcurementDbContext, ApprovalDbContext, CancellationToken, Task<TResult>> command,
        CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync(command, manualPayments: true, cancellationToken);

    private async Task<TResult> ExecuteCommandAsync<TResult>(
        Func<ProcurementDbContext, ApprovalDbContext, CancellationToken, Task<TResult>> command,
        bool manualPayments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ExecuteAttemptAsync((db, cases, documents, fence, token) => command(db, cases, token), null, false, cancellationToken, manualPayments);
            }
            catch (Exception exception) when (attempt < 3 && IsConfirmedTransactionAbort(exception))
            {
                // ExecuteAttemptAsync has already disposed both contexts/transaction/connection.
                await Task.Delay(TimeSpan.FromMilliseconds(40 * attempt), cancellationToken);
            }
        }
    }

    /// <summary>Exact invoice-owner lifecycle fence before BEGIN. Invoice scope/current
    /// authority is revalidated inside the callback; a route UUID itself grants no access.
    /// Same-key retry always reacquires every lease on a fresh physical session.</summary>
    public async Task<TResult> ExecuteWithInvoiceDocumentsAsync<TResult>(Guid invoiceId,
        Func<ProcurementDbContext, ApprovalDbContext, DocumentDbContext, DocumentOwnerLifecycleFence, CancellationToken, Task<TResult>> command,
        CancellationToken cancellationToken = default, bool exclusive = false)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("An exact invoice owner is required.", nameof(invoiceId));
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ExecuteAttemptAsync((db, cases, documents, fence, token) =>
                    command(db, cases, documents!, fence!, token), new("procurement", "supplier_invoice", invoiceId), exclusive, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsConfirmedTransactionAbort(exception))
            { await Task.Delay(TimeSpan.FromMilliseconds(40 * attempt), cancellationToken); }
        }
    }

    /// <summary>Exact RFQ document owner on the existing physical session and
    /// pre-BEGIN lifecycle fence; no route, authority or provider registration.</summary>
    public async Task<TResult> ExecuteWithSourcingDocumentsAsync<TResult>(Guid eventId,
        Func<ProcurementDbContext,ApprovalDbContext,DocumentDbContext,DocumentOwnerLifecycleFence,CancellationToken,Task<TResult>> command,
        CancellationToken cancellationToken=default)
    {
        if(eventId==Guid.Empty)throw new ArgumentException("An exact RFQ owner is required.",nameof(eventId));
        ArgumentNullException.ThrowIfNull(command);
        for(var attempt=1;;attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{return await ExecuteAttemptAsync((db,cases,documents,fence,token)=>command(db,cases,documents!,fence!,token),
                new("procurement","sourcing_event",eventId),false,cancellationToken);}
            catch(Exception error) when(attempt<3&&IsConfirmedTransactionAbort(error))
            {await Task.Delay(TimeSpan.FromMilliseconds(40*attempt),cancellationToken);}
        }
    }

    private async Task<TResult> ExecuteAttemptAsync<TResult>(
        Func<ProcurementDbContext, ApprovalDbContext, DocumentDbContext?, DocumentOwnerLifecycleFence?, CancellationToken, Task<TResult>> command,
        DocumentOwner? documentOwner, bool exclusiveDocuments, CancellationToken ct, bool manualPayments = false)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // Session fence must precede BEGIN/snapshot acquisition, including on retries/replays.
        // Declaration order releases the transaction before unlocking the pooled session.
        await using var branchFence = await CoreBranchAuthorizationFence.AcquireAsync(connection,
            scope.TenantId, exclusive: false, ct);
        await using var catalogFence = await ProcurementCatalogAuthorityFence.AcquireAsync(connection,
            scope.TenantId, scope.LegalEntityId, exclusive: false, ct);
        await using var documents = documentOwner is null ? null : new DocumentDbContext(
            new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(connection,
                provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_document")).Options, scope);
        await using var documentFence = documents is null ? null : await DocumentOwnerLifecycleFence.AcquireAsync(
            documents, scope, [documentOwner!], exclusive: exclusiveDocuments, ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (documents is not null) await documents.Database.UseTransactionAsync(transaction, ct);
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
        procurement.BindSharedCoreApprovalContext(approvals);
        if (manualPayments) procurement.RequireManualPaymentGovernance();

        var outcome = await command(procurement, approvals, documents, documentFence, ct);
        if (documents is not null) await documents.SaveChangesAsync(ct);
        // Payment provenance requires actual Core inserts observed in this transaction,
        // so the coordinator owns the safe order instead of relying on every callback.
        if (manualPayments) await approvals.SaveChangesAsync(ct);
        await procurement.SaveChangesAsync(ct);
        if (!manualPayments) await approvals.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return outcome;
    }

    private static bool IsConfirmedTransactionAbort(Exception exception)
        // Npgsql's execution strategy can wrap DbUpdateException in an
        // InvalidOperationException even when automatic provider retries are off.
        => exception.GetBaseException() is PostgresException
            { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };

    private sealed record TransactionScope(string TenantId, Guid LegalEntityId, string SubjectId,
        Guid? WorkLocationId, Guid? OrgUnitId, bool IsConfined, string? CorrelationId, Guid? WorkerId,
        Guid? CoreLinkedWorkerId, bool IsExplicitAllBranches)
        : IProcurementScope, IApprovalScope, IDocumentScope;
}
