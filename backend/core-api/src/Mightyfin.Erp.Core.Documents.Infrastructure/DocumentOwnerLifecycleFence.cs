using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Persistence.Infrastructure;
using Npgsql;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>
/// Cooperative owner lifecycle lease, not document/purpose authorization or a registered use-store.
/// Acquire on the actual owning EF session before BEGIN, all authority reads and any document-domain locks.
/// If combined with existing fences: Core branch -> Procurement catalog -> document owner keys -> transaction/domain.
/// No caller may open an untracked raw transaction or use the connection concurrently during acquisition.
/// Hold through commit/rollback, release before closing the session. Writers must take exclusive and pin/read
/// commands shared on the SAME owner keys; this primitive alone cannot protect unpaired lifecycle writers.
/// </summary>
public sealed class DocumentOwnerLifecycleFence : IAsyncDisposable
{
    internal const int KeyVersion = 1;
    private const string KeyPrefix = "greenwebb.core.document-owner-lifecycle.v1\0";
    private readonly IReadOnlyList<PostgresSessionAdvisoryLease> leases;
    private int disposed;
    private DocumentOwnerLifecycleFence(IReadOnlyList<PostgresSessionAdvisoryLease> leases) => this.leases = leases;

    public static async Task<DocumentOwnerLifecycleFence> AcquireAsync(DocumentDbContext db, IDocumentScope actor,
        IReadOnlyList<DocumentOwner> owners, bool exclusive, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(actor);
        db.RequireDocumentUseScope(actor);
        var keys = OrderedKeys(actor, owners);
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("Document lifecycle fencing requires the owning PostgreSQL context.");
        if (db.Database.CurrentTransaction is not null || db.Database.GetEnlistedTransaction() is not null
            || System.Transactions.Transaction.Current is not null)
            throw new InvalidOperationException("Acquire document lifecycle fencing before beginning or enlisting a transaction.");
        if (db.Database.GetDbConnection() is not NpgsqlConnection connection || connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Open the actual owning PostgreSQL session before acquiring document lifecycle fencing.");
        var held = new List<PostgresSessionAdvisoryLease>();
        try
        {
            foreach (var key in keys)
                held.Add(await PostgresSessionAdvisoryLease.AcquireAsync(connection, key, exclusive, ct));
            return new(held.AsReadOnly());
        }
        catch
        {
            // The shared primitive closes/clears an indeterminate acquisition session. Still unwind every
            // earlier lease, preserving the originating error instead of masking it with cleanup failures.
            await ReleaseAll(held, suppressErrors: true);
            throw;
        }
    }

    internal static IReadOnlyList<long> OrderedKeys(IDocumentScope actor, IReadOnlyList<DocumentOwner> owners)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(owners);
        if (owners.Count is < 1 or > 100) throw new ArgumentException("Choose between one and 100 document owners.", nameof(owners));
        var copy = owners.ToArray();
        if (copy.Any(x => x is null) || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("Document lifecycle owners must be exact and duplicate-free.", nameof(owners));
        // Distinct hash collisions safely coarsen serialization; never acquire one session key twice.
        return Array.AsReadOnly(copy.Select(owner => Key(actor, owner)).Distinct().Order().ToArray());
    }

    internal static long Key(IDocumentScope actor, DocumentOwner owner)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(owner);
        if (!CanonicalText(actor.TenantId, 150) || !CanonicalText(actor.SubjectId, 150) || actor.LegalEntityId == Guid.Empty)
            throw new UnauthorizedAccessException("A canonical owning document scope is required.");
        if (owner.Id == Guid.Empty || !CanonicalToken(owner.Module, 60) || !CanonicalToken(owner.Type, 80))
            throw new ArgumentException("A canonical document owner is required.", nameof(owner));
        var identity = KeyPrefix + actor.TenantId + "\0" + actor.LegalEntityId.ToString("D") + "\0"
            + owner.Module + "\0" + owner.Type + "\0" + owner.Id.ToString("D");
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static bool CanonicalText(string? value, int length) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= length && value == value.Trim() && !value.Contains('\0');
    private static bool CanonicalToken(string? value, int length) => CanonicalText(value, length)
        && value!.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await ReleaseAll(leases, suppressErrors: false);
    }

    private static async Task ReleaseAll(IReadOnlyList<PostgresSessionAdvisoryLease> held, bool suppressErrors)
    {
        List<Exception>? failures = null;
        for (var index = held.Count - 1; index >= 0; index--)
            try { await held[index].DisposeAsync(); }
            catch (Exception error) { (failures ??= []).Add(error); }
        if (!suppressErrors && failures is { Count: > 0 })
            throw new AggregateException("Document lifecycle fence cleanup failed; the shared lease invalidates uncertain sessions.", failures);
    }
}
