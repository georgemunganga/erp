using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    // Bootstrap membership writes deliberately join the same protocol as Core branch writers.
    // Runtime commands still use the separately restricted connection, never HRM write grants.
    private async Task VerifyQueuedBranchRevocation(Guid orderId, Guid sourceId, long version)
    {
        Guid firstAssignment, secondAssignment;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var other = new Mightyfin.Erp.Hrm.Domain.Entities.WorkLocation
                { Code = "PO-OTHER", Name = "Other synthetic OrderBranch", LegalEntityId = OrderCompany };
            core.WorkLocations.Add(other);
            var first = new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = OrderBuyer, LocationId = OrderBranch };
            var second = new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = OrderBuyer, LocationId = other.Id };
            core.UserBranchAssignments.AddRange(first, second); await core.SaveChangesAsync();
            firstAssignment = first.Id; secondAssignment = second.Id;
        }
        try
        {
            var detail = await Get($"{Root}/orders/{orderId}", OrderBuyer);
            var lineId = detail.GetProperty("lines")[0].GetProperty("id").GetGuid();
            object Edit(long expectedVersion) => new { expectedOrderVersion = expectedVersion,
                reason = "Synthetic branch fence ordering proof", draft = new
                { supplierId = detail.GetProperty("supplierId").GetGuid(), currencyCode = "ZMW",
                    lines = new[] { new { id = lineId, description = "Synthetic item", unitCode = "each",
                        quantity = 1, unitPrice = 100, taxAmount = 0 } } } };

            // Reader-first: the command owns a shared fence before queuing on source lock.
            // A cooperative revoker must wait; this command is legitimately ordered first.
            await using (var blocker = new NpgsqlConnection(databaseConnection))
            {
                await blocker.OpenAsync();
                await using var hold = await blocker.BeginTransactionAsync();
                await using (var command = new NpgsqlCommand("SELECT id FROM procurement.purchase_requests WHERE id=@id FOR UPDATE", blocker, hold))
                { command.Parameters.AddWithValue("id", sourceId); await command.ExecuteScalarAsync(); }
                var queued = Send(HttpMethod.Put, $"{Root}/orders/{orderId}/source-draft", OrderBuyer, Edit(version), Guid.NewGuid());
                await WaitForLock("%procurement.purchase_requests%FOR UPDATE%");
                var changed = SetArchived(firstAssignment, true);
                await WaitForLock("%pg_advisory_lock(%");
                Require(!changed.IsCompleted, "Exclusive Core writer crossed the command's shared branch fence.");
                await hold.CommitAsync();
                await Check(await queued.WaitAsync(TimeSpan.FromSeconds(15)), HttpStatusCode.OK);
                await changed.WaitAsync(TimeSpan.FromSeconds(15));
            }
            // Restore A+B, then explicitly order the A -> B writer before the next command.
            await SetArchived(firstAssignment, false);
            detail = await Get($"{Root}/orders/{orderId}", OrderBuyer);
            version = detail.GetProperty("version").GetInt64();
            await using (var writer = new NpgsqlConnection(databaseConnection))
            {
                await writer.OpenAsync();
                var fence = await CoreBranchAuthorizationFence.AcquireAsync(writer, "probe", exclusive: true);
                try
                {
                    await using var change = await writer.BeginTransactionAsync();
                    await ArchiveSql(writer, change, firstAssignment, true);
                    // Middleware can still see A while writer has not committed. The execution
                    // session must wait BEFORE taking its SERIALIZABLE snapshot.
                    var queued = Send(HttpMethod.Put, $"{Root}/orders/{orderId}/source-draft", OrderBuyer, Edit(version), Guid.NewGuid());
                    await WaitForLock("%pg_advisory_lock_shared(%");
                    await change.CommitAsync();
                    await fence.DisposeAsync();
                    await Check(await queued.WaitAsync(TimeSpan.FromSeconds(15)), HttpStatusCode.Forbidden);
                }
                finally { await fence.DisposeAsync(); }
            }
            using var services = Scope(OrderBuyer);
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require((await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Version == version,
                "Writer-first revoked-scope command changed the source order.");
            Console.WriteLine("PASS branch fence: reader-first Core writer waits; writer-first A-to-B mapped command denied before mutation");
        }
        finally
        {
            // Empty assignments means organisation-wide, preserving existing Core semantics.
            await SetArchived(firstAssignment, true);
            await SetArchived(secondAssignment, true);
        }
    }

    private async Task SetArchived(Guid assignment, bool archived)
    {
        await using var connection = new NpgsqlConnection(databaseConnection);
        await connection.OpenAsync();
        await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(connection, "probe", exclusive: true);
        await using var transaction = await connection.BeginTransactionAsync();
        await ArchiveSql(connection, transaction, assignment, archived);
        await transaction.CommitAsync();
    }

    private static async Task ArchiveSql(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid assignment, bool archived)
    {
        await using var command = new NpgsqlCommand("UPDATE hrm.hr_user_branch_assignments SET is_archived=@archived WHERE id=@id AND tenant_id='probe'", connection, transaction);
        command.Parameters.AddWithValue("id", assignment); command.Parameters.AddWithValue("archived", archived);
        Require(await command.ExecuteNonQueryAsync() == 1, "Synthetic Core membership row was missing.");
    }

    private async Task WaitForLock(string queryPattern)
    {
        await using var inspect = new NpgsqlConnection(databaseConnection);
        await inspect.OpenAsync();
        for (var attempt = 0; attempt < 150; attempt++)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE @pattern)", inspect);
            command.Parameters.AddWithValue("pattern", queryPattern);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("Expected synthetic session did not wait for its lock: " + queryPattern);
    }

    private async Task VerifyBranchFenceCleanup()
    {
        var settings = new NpgsqlConnectionStringBuilder(runtimeConnection)
            { ApplicationName = "branch-fence-cleanup-" + Guid.NewGuid().ToString("N"), MaxPoolSize = 1, MinPoolSize = 0 };
        var pooled = settings.ConnectionString;
        // Rollback while shared lock is held, then explicit lease disposal before pooling.
        await using (var connection = new NpgsqlConnection(pooled))
        {
            await connection.OpenAsync();
            await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(connection, "probe", exclusive: false);
            await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await transaction.RollbackAsync();
        }
        await using (var writer = new NpgsqlConnection(databaseConnection))
        {
            await writer.OpenAsync();
            await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(writer, "probe", exclusive: true);
            await using var cancelled = new NpgsqlConnection(pooled); await cancelled.OpenAsync();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            try
            {
                await CoreBranchAuthorizationFence.AcquireAsync(cancelled, "probe", exclusive: false, cancellation.Token);
                throw new InvalidOperationException("Cancelled fence wait unexpectedly acquired a reader lease.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            Require(cancelled.State == System.Data.ConnectionState.Closed,
                "Uncertain cancelled session was returned live to its connection pool.");
        }
        // New physical/pooled connection remains usable, and no canceled reader lock survives.
        await using (var connection = new NpgsqlConnection(pooled))
        {
            await connection.OpenAsync();
            await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(connection, "probe", exclusive: false);
        }
        await using (var writer = new NpgsqlConnection(databaseConnection))
        {
            await writer.OpenAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(writer, "probe", exclusive: true, timeout.Token);
        }
        Console.WriteLine("PASS branch fence: rollback, canceled lock acquisition, physical-session eviction and pooled reuse cleanup");
    }
}
