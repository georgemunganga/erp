using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Npgsql;

/// <summary>Private PostgreSQL session/cleanup proof only. No pin/publication/retirement provider is activated.</summary>
internal static class DocumentLifecycleFenceVerification
{
    internal static async Task RunAsync(string connectionString, IDocumentScope actor, DocumentOwner owner)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (!(settings.Database?.StartsWith("core_doc_rehearsal_", StringComparison.Ordinal) ?? false))
            throw new InvalidOperationException("Lifecycle lease probe requires a private core_doc_rehearsal_* database.");
        if (!settings.Pooling || settings.Multiplexing || settings.NoResetOnClose)
            throw new InvalidOperationException("The private lifecycle probe requires Pooling=true, pinned sessions and connection reset.");
        await using var a = new NpgsqlConnection(settings.ConnectionString);
        await using var b = new NpgsqlConnection(settings.ConnectionString);
        await using var observer = new NpgsqlConnection(settings.ConnectionString);
        await a.OpenAsync(); await b.OpenAsync(); await observer.OpenAsync();
        DocumentDbContext Context(NpgsqlConnection connection) => new(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(connection).Options, actor);
        await using var first = Context(a); await using var second = Context(b);
        var before = await Footprint(observer);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var sharedA = await DocumentOwnerLifecycleFence.AcquireAsync(first, actor, [owner], false, deadline.Token);
        try
        {
            await using (var sharedB = await DocumentOwnerLifecycleFence.AcquireAsync(second, actor, [owner], false, deadline.Token))
            { /* Two actual shared sessions coexist on exactly the same owner. */ }
            var writer = DocumentOwnerLifecycleFence.AcquireAsync(second, actor, [owner], true, deadline.Token);
            await WaitForAdvisoryWait(observer, b.ProcessID, deadline.Token);
            Check(!writer.IsCompleted, "Exclusive owner writer did not wait for the shared session.");
            await sharedA.DisposeAsync();
            await using var exclusive = await writer;
        }
        finally { await sharedA.DisposeAsync(); }

        // Known EF transaction must be rejected before taking any owner lease.
        await using (var transaction = await first.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, deadline.Token))
        {
            try
            {
                await using var invalid = await DocumentOwnerLifecycleFence.AcquireAsync(first, actor, [owner], false, deadline.Token);
                throw new InvalidOperationException("Document owner fence accepted an existing EF transaction.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("before beginning or enlisting", StringComparison.Ordinal)) { }
            await transaction.RollbackAsync(deadline.Token);
        }

        var other = owner with { Id = Guid.NewGuid() };
        var ordered = new[] { owner, other }.OrderBy(x => DocumentOwnerLifecycleFence.Key(actor, x)).ToArray();
        var canceledContenderPid = b.ProcessID;
        // Hold only the later key, so the contender obtains the first before it blocks/cancels.
        await using (var blocker = await DocumentOwnerLifecycleFence.AcquireAsync(first, actor, [ordered[1]], true, deadline.Token))
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var pending = DocumentOwnerLifecycleFence.AcquireAsync(second, actor, ordered.Reverse().ToArray(), false, cancel.Token);
            await WaitForAdvisoryWait(observer, canceledContenderPid, deadline.Token);
            await using (var held = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE pid=@pid AND locktype='advisory' AND granted", observer))
            {
                held.Parameters.AddWithValue("pid", canceledContenderPid);
                Check((long)(await held.ExecuteScalarAsync(deadline.Token))! == 1, "Contender did not hold the first ordered owner before cancellation.");
            }
            cancel.Cancel();
            try { await using var unexpected = await pending; throw new InvalidOperationException("Canceled owner acquisition succeeded."); }
            catch (OperationCanceledException) { }
            Check(b.State == System.Data.ConnectionState.Closed, "Indeterminate canceled acquisition did not discard its session.");
        }
        // Pooled replacement must acquire BOTH keys; no first-key leak or retained exclusive lock.
        await using (var replacement = new NpgsqlConnection(settings.ConnectionString))
        {
            await replacement.OpenAsync(deadline.Token);
            Check(replacement.ProcessID != canceledContenderPid, "Canceled physical session was returned as the pooled replacement.");
            await using var replacementContext = Context(replacement);
            await using var recovered = await DocumentOwnerLifecycleFence.AcquireAsync(replacementContext, actor, ordered, true, deadline.Token);
            await using var transaction = await replacementContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, deadline.Token);
            await replacementContext.Database.ExecuteSqlRawAsync("SELECT 1", deadline.Token);
            await transaction.RollbackAsync(deadline.Token);
        }
        Check(before == await Footprint(observer), "Lifecycle fence-only probe changed document/use/scan/event rows.");
        Console.WriteLine("PASS private document owner lifecycle fence: actual shared/exclusive contention, pre-EF-BEGIN guard, sorted multi-owner canceled acquisition/unwind, pooled replacement with a different canceled-session PID, no document or pin writes. No paired production lifecycle provider claimed.");
    }

    private static async Task WaitForAdvisoryWait(NpgsqlConnection observer, int pid, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < until)
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid=@pid AND wait_event_type='Lock' AND wait_event='advisory')", observer);
            query.Parameters.AddWithValue("pid", pid);
            if (await query.ExecuteScalarAsync(ct) is true) return;
            await Task.Delay(25, ct);
        }
        throw new InvalidOperationException("Expected actual owner advisory contention was not observed.");
    }
    private static async Task<string> Footprint(NpgsqlConnection connection)
    {
        string[] tables = ["documents", "versions", "scan_evidence", "record_events", "use_sets", "use_pins"];
        var rows = tables.Select(table => $"'{table}',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.\"Id\"),'[]'::jsonb) FROM core_document.{table} r)");
        await using var query = new NpgsqlCommand("SELECT jsonb_build_object(" + string.Join(',', rows) + ")::text", connection);
        return (string)(await query.ExecuteScalarAsync())!;
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
