using System.Data;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Core.Persistence.Infrastructure;
using Npgsql;

namespace Mightyfin.Erp.Core.Documents.Tests;

/// <summary>Key/input/precondition contracts only. No network, physical-lock or race proof.</summary>
public sealed class DocumentOwnerLifecycleFenceTests
{
    private static readonly Scope Actor = new("tenant-a", Guid.Parse("11111111-1111-4111-8111-111111111111"), "reader");
    private static readonly DocumentOwner Owner = new("procurement", "sourcing_event", Guid.Parse("22222222-2222-4222-8222-222222222222"));

    [Fact]
    public void Versioned_key_is_frozen_and_owner_scope_is_exact_not_actor_or_use_purpose()
    {
        Assert.Equal(1, DocumentOwnerLifecycleFence.KeyVersion);
        Assert.Equal(3078790645253077403L, DocumentOwnerLifecycleFence.Key(Actor, Owner));
        Assert.Equal(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor with { SubjectId = "other-authorized-person" }, Owner));
        Assert.NotEqual(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor with { TenantId = "tenant-b" }, Owner));
        Assert.NotEqual(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor with { LegalEntityId = Guid.NewGuid() }, Owner));
        Assert.NotEqual(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor, Owner with { Module = "other" }));
        Assert.NotEqual(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor, Owner with { Type = "supplier_invoice" }));
        Assert.NotEqual(DocumentOwnerLifecycleFence.Key(Actor, Owner), DocumentOwnerLifecycleFence.Key(Actor, Owner with { Id = Guid.NewGuid() }));
    }

    [Fact]
    public void Multiple_owner_keys_sort_identically_regardless_of_selection_order()
    {
        var other = Owner with { Id = Guid.NewGuid() };
        var first = DocumentOwnerLifecycleFence.OrderedKeys(Actor, [Owner, other]);
        Assert.Equal(first, DocumentOwnerLifecycleFence.OrderedKeys(Actor, [other, Owner]));
        Assert.Equal(first.Order(), first);
        Assert.Equal(2, first.Count);
        Assert.Throws<ArgumentException>(() => DocumentOwnerLifecycleFence.OrderedKeys(Actor, []));
        Assert.Throws<ArgumentException>(() => DocumentOwnerLifecycleFence.OrderedKeys(Actor, [Owner, Owner]));
        Assert.Throws<ArgumentException>(() => DocumentOwnerLifecycleFence.OrderedKeys(Actor, [null!]));
        Assert.Throws<ArgumentException>(() => DocumentOwnerLifecycleFence.OrderedKeys(Actor,
            Enumerable.Range(0, 101).Select(_ => Owner with { Id = Guid.NewGuid() }).ToArray()));
    }

    [Fact]
    public void Invalid_scope_and_owner_cannot_be_normalized_onto_another_lock_identity()
    {
        foreach (var scope in new[] { Actor with { TenantId = " tenant-a" }, Actor with { TenantId = "a\0b" },
            Actor with { TenantId = new string('a',151) }, Actor with { SubjectId = " " }, Actor with { LegalEntityId = Guid.Empty } })
            Assert.Throws<UnauthorizedAccessException>(() => DocumentOwnerLifecycleFence.Key(scope, Owner));
        foreach (var owner in new[] { Owner with { Id = Guid.Empty }, Owner with { Module = " procurement" },
            Owner with { Type = "sourcing/event" }, Owner with { Module = "a\0b" }, Owner with { Type = new string('x',81) } })
            Assert.Throws<ArgumentException>(() => DocumentOwnerLifecycleFence.Key(Actor, owner));
    }

    [Fact]
    public async Task Nonrelational_and_mismatched_context_scope_never_acquire_a_lease()
    {
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, Actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor, [Owner], false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor with { SubjectId = "alias" }, [Owner], false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor with { TenantId = "other" }, [Owner], true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor with { LegalEntityId = Guid.NewGuid() }, [Owner], true));
    }

    [Fact]
    public async Task Closed_actual_connection_and_ambient_transaction_fail_without_network_or_side_effects()
    {
        await using var connection = new NpgsqlConnection("Host=localhost;Database=never_opened;Username=never_opened");
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(connection).Options, Actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor, [Owner], false));
        Assert.Equal(ConnectionState.Closed, connection.State);
        using (var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            await Assert.ThrowsAsync<InvalidOperationException>(() => DocumentOwnerLifecycleFence.AcquireAsync(db, Actor, [Owner], true));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Shared_lease_still_rejects_unsafe_session_settings_without_opening(bool multiplexing, bool noReset)
    {
        var settings = new NpgsqlConnectionStringBuilder("Host=localhost;Database=never_opened;Username=never_opened")
            { Multiplexing = multiplexing, NoResetOnClose = noReset };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PostgresSessionAdvisoryLease.AcquireAsync(connection, 1, exclusive: false));
        Assert.Contains("pinned sessions and connection reset", error.Message);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
}
