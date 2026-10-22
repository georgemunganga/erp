using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Owning SaveChanges/model tests. InMemory is not proof of PostgreSQL FK/CHECK execution.</summary>
public sealed class SpendingAuthorityIdentityPersistenceTests
{
    private const string Maker = "11111111-1111-4111-8111-111111111111";
    private const string Recipient = "22222222-2222-4222-8222-222222222222";
    private const string Reviewer = "33333333-3333-4333-8333-333333333333";
    private static readonly Guid Company = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid MakerWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid RecipientWorker = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid ReviewerWorker = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly DateTimeOffset At = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Scope(string actor, Guid? worker, Guid company) : IProcurementScope
    {
        public string TenantId => "authority-identity-test";
        public Guid LegalEntityId => company;
        public string SubjectId => actor;
        public Guid? WorkerId => worker;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "authority-identity-test";
    }
    private static ProcurementDbContext Context(string name, string actor = Maker, Guid? worker = null, Guid? company = null)
        => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name).Options,
            new Scope(actor, worker, company ?? Company));
    private static SpendingAuthorityConfigurationActor Actor(string actor, string permission)
        => new("authority-identity-test", Company, actor, true, new HashSet<string> { permission });
    private static SpendingAuthoritySubject Subject() => new("authority-identity-test", Company, Recipient, true, true);
    private static ProcurementSpendingAuthorityGrant Draft()
        => SpendingAuthorityConfiguration.CreateDraft("IDENTITY-PERSISTENCE", Recipient,
            new("ZMW", "EstimatedLineBase", 12.3456m, At.AddDays(-1), At.AddDays(1),
                AllBranches: true, AllDepartments: true, AllCategories: true),
            Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), At, []);
    private static SpendingAuthorityIdentityRecord Origin(ProcurementSpendingAuthorityGrant grant)
        => SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureCreation(grant, MakerWorker, RecipientWorker));

    [Fact]
    public async Task Real_SaveChanges_sequence_preserves_creation_bytes_and_retains_each_action_once()
    {
        var name = Guid.NewGuid().ToString(); var grant = Draft(); var created = Origin(grant);
        var creationHash = created.BusinessFingerprint;
        await using (var db = Context(name, worker: MakerWorker))
        {
            db.SpendingAuthorityGrants.Add(grant); db.SpendingAuthorityIdentityRecords.Add(created);
            await db.SaveChangesAsync();
            Assert.Equal(At, grant.CreatedAt); Assert.Equal(creationHash, SpendingAuthorityConfiguration.Fingerprint(grant));
        }
        await using (var db = Context(name, worker: MakerWorker))
        {
            var stored = await db.SpendingAuthorityGrants.SingleAsync();
            var origin = SpendingAuthorityIdentityStorage.Read(await db.SpendingAuthorityIdentityRecords.SingleAsync());
            SpendingAuthorityConfiguration.Submit(stored, stored.Version,
                Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), At.AddMinutes(1));
            db.SpendingAuthorityIdentityRecords.Add(SpendingAuthorityIdentityStorage.ToRecord(
                SpendingAuthorityIdentity.CaptureSubmission(stored, origin, Maker, MakerWorker, RecipientWorker)));
            await db.SaveChangesAsync(); Assert.Equal(2, stored.Version);
        }
        await using (var db = Context(name, Reviewer, ReviewerWorker))
        {
            var stored = await db.SpendingAuthorityGrants.SingleAsync();
            var rows = await db.SpendingAuthorityIdentityRecords.ToArrayAsync();
            var origin = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Created"));
            var submission = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted"));
            SpendingAuthorityConfiguration.Activate(stored, stored.Version,
                Actor(Reviewer, SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Independent review", At.AddMinutes(2), [stored]);
            db.SpendingAuthorityIdentityRecords.Add(SpendingAuthorityIdentityStorage.ToRecord(
                SpendingAuthorityIdentity.CaptureActivation(stored, origin, submission, Reviewer, ReviewerWorker, RecipientWorker)));
            await db.SaveChangesAsync(); Assert.Equal(3, stored.Version);
        }
        await using var verify = Context(name, Reviewer, ReviewerWorker);
        var finalGrant = await verify.SpendingAuthorityGrants.SingleAsync();
        var retained = await verify.SpendingAuthorityIdentityRecords.ToArrayAsync();
        SpendingAuthorityIdentity.VerifyActivation(finalGrant,
            SpendingAuthorityIdentityStorage.Read(retained.Single(x => x.Stage == "Created")),
            SpendingAuthorityIdentityStorage.Read(retained.Single(x => x.Stage == "Submitted")),
            SpendingAuthorityIdentityStorage.Read(retained.Single(x => x.Stage == "Activated")));
        Assert.All(retained, row => { Assert.Equal(row.ActionAt, row.CreatedAt); Assert.Equal(row.ActorPrincipalId, row.CreatedBy); Assert.Equal(1, row.Version); });
        Assert.Equal(3, await verify.AuditEvents.CountAsync(x => x.RecordType == nameof(SpendingAuthorityIdentityRecord)));
    }

    [Theory]
    [InlineData("missing-worker")]
    [InlineData("wrong-worker")]
    [InlineData("wrong-actor")]
    [InlineData("changed-fingerprint")]
    [InlineData("metadata")]
    [InlineData("noncanonical-created-time")]
    public async Task Otherwise_valid_creation_requires_actual_scope_and_exact_initial_owner(string fault)
    {
        var name = Guid.NewGuid().ToString(); var grant = Draft(); var origin = Origin(grant);
        if (fault == "changed-fingerprint") grant.MaximumAmount += 1;
        if (fault == "metadata") origin.CreatedBy = Reviewer;
        if (fault == "noncanonical-created-time") grant.CreatedAt = grant.CreatedAt.AddTicks(1);
        await using (var db = Context(name, fault == "wrong-actor" ? Reviewer : Maker,
            fault == "missing-worker" ? null : fault == "wrong-worker" ? ReviewerWorker : MakerWorker))
        {
            db.Add(grant); db.Add(origin);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using var verify = Context(name);
        Assert.Empty(await verify.SpendingAuthorityGrants.ToListAsync());
        Assert.Empty(await verify.SpendingAuthorityIdentityRecords.ToListAsync()); Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public void Attaching_original_identity_never_relabels_it_as_another_company()
    {
        var grant = Draft(); var origin = Origin(grant);
        var originalTenant = origin.TenantId; var originalCompany = origin.LegalEntityId;
        var originalDigest = origin.EvidenceSha256;
        using var db = Context(Guid.NewGuid().ToString(), worker: MakerWorker, company: Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => db.AddScoped(origin));
        Assert.Equal(originalTenant, origin.TenantId); Assert.Equal(originalCompany, origin.LegalEntityId);
        Assert.Equal(originalDigest, origin.EvidenceSha256); Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Legacy_grant_remains_seedable_but_cannot_adopt_a_new_creation_envelope()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = Context(name)) { db.Add(Draft()); await db.SaveChangesAsync(); }
        await using (var db = Context(name, worker: MakerWorker))
        {
            var grant = await db.SpendingAuthorityGrants.SingleAsync();
            db.Add(Origin(grant));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using var verify = Context(name);
        Assert.Single(await verify.SpendingAuthorityGrants.ToArrayAsync()); Assert.Empty(await verify.SpendingAuthorityIdentityRecords.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_evidence_field_and_inherited_metadata_is_append_only(bool delete)
    {
        var name = Guid.NewGuid().ToString(); await SeedOrigin(name);
        await using var db = Context(name, worker: MakerWorker);
        var row = await db.SpendingAuthorityIdentityRecords.SingleAsync();
        if (delete) db.Remove(row); else row.CreatedAt = row.CreatedAt.AddMinutes(1);
        Assert.Equal("AuthorityIdentityAppendOnly", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        await using var verify = Context(name);
        Assert.Equal(At, (await verify.SpendingAuthorityIdentityRecords.SingleAsync()).ActionAt);
        Assert.Equal(2, await verify.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Foreign_company_evidence_is_hidden_and_cannot_be_adopted_as_lineage()
    {
        var name = Guid.NewGuid().ToString(); await SeedOrigin(name);
        await using var foreign = Context(name, worker: MakerWorker, company: Guid.NewGuid());
        Assert.Empty(await foreign.SpendingAuthorityIdentityRecords.ToListAsync());
        Assert.Empty(await foreign.SpendingAuthorityGrants.ToListAsync());
    }

    [Fact]
    public async Task Unchanged_attached_historical_envelope_is_not_trusted_without_persisted_lineage()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = Context(name)) { db.Add(Draft()); await db.SaveChangesAsync(); }
        await using var command = Context(name, worker: MakerWorker);
        var grant = await command.SpendingAuthorityGrants.SingleAsync();
        var inventedOrigin = Origin(grant);
        command.Attach(inventedOrigin); // Deliberately not persisted; a hash is not provenance.
        SpendingAuthorityConfiguration.Submit(grant, grant.Version,
            Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), DateTimeOffset.UtcNow.AddMinutes(1));
        var envelope = SpendingAuthorityIdentity.CaptureSubmission(grant,
            SpendingAuthorityIdentityStorage.Read(inventedOrigin), Maker, MakerWorker, RecipientWorker);
        command.Add(SpendingAuthorityIdentityStorage.ToRecord(envelope));
        Assert.Equal("AuthorityIdentityRetainedLineageRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync())).Message);
        await using var verify = Context(name);
        Assert.Equal("Draft", (await verify.SpendingAuthorityGrants.SingleAsync()).State);
        Assert.Empty(await verify.SpendingAuthorityIdentityRecords.ToArrayAsync());
    }

    [Theory]
    [InlineData("maker-alias")]
    [InlineData("recipient-alias")]
    [InlineData("wrong-submission")]
    public async Task Activation_storage_denies_rehashed_alias_or_foreign_lineage_before_any_effect(string fault)
    {
        var name = Guid.NewGuid().ToString(); await SeedOrigin(name);
        await using (var submit = Context(name, worker: MakerWorker))
        {
            var grant = await submit.SpendingAuthorityGrants.SingleAsync();
            var created = SpendingAuthorityIdentityStorage.Read(await submit.SpendingAuthorityIdentityRecords.SingleAsync());
            SpendingAuthorityConfiguration.Submit(grant, grant.Version,
                Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), At.AddMinutes(1));
            submit.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureSubmission(
                grant, created, Maker, MakerWorker, RecipientWorker)));
            await submit.SaveChangesAsync();
        }
        var actingWorker = fault == "maker-alias" ? MakerWorker
            : fault == "recipient-alias" ? RecipientWorker : ReviewerWorker;
        await using (var command = Context(name, Reviewer, actingWorker))
        {
            var grant = await command.SpendingAuthorityGrants.SingleAsync();
            var rows = await command.SpendingAuthorityIdentityRecords.ToArrayAsync();
            var created = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Created"));
            var submitted = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted"));
            SpendingAuthorityConfiguration.Activate(grant, grant.Version,
                Actor(Reviewer, SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Independent review", At.AddMinutes(2), [grant]);
            var valid = SpendingAuthorityIdentity.CaptureActivation(grant, created, submitted, Reviewer, ReviewerWorker, RecipientWorker);
            // Deliberately malformed but internally hashed; storage must verify the actual retained lineage/person.
            var malformed = valid with { ActorWorkerId = actingWorker,
                SubmittedEvidenceId = fault == "wrong-submission" ? Guid.NewGuid() : submitted.Id };
            malformed = malformed with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(malformed) };
            command.Add(SpendingAuthorityIdentityStorage.ToRecord(malformed));
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync());
        }
        await using var verify = Context(name);
        Assert.Equal("PendingReview", (await verify.SpendingAuthorityGrants.SingleAsync()).State);
        Assert.Equal(2, await verify.SpendingAuthorityIdentityRecords.CountAsync());
        Assert.Equal(4, await verify.AuditEvents.CountAsync());
    }

    [Fact]
    public void Npgsql_model_uses_stable_scoped_typed_FKs_and_explicit_lineage_stage_checks()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=invalid;Database=design_only;Username=design_only;Password=unused").Options,
            new Scope(Maker, MakerWorker, Company)); // Design model only; never opens a connection.
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(SpendingAuthorityIdentityRecord))!;
        var owner = entity.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(ProcurementSpendingAuthorityGrant));
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "GrantId" }, owner.Properties.Select(x => x.Name));
        Assert.DoesNotContain(owner.PrincipalKey.Properties, x => x.Name == "Version");
        var lineage = entity.GetForeignKeys().Where(x => x.PrincipalEntityType == entity).ToArray(); Assert.Equal(2, lineage.Length);
        Assert.All(lineage, fk => { Assert.Contains(fk.Properties, x => x.Name == "GrantId"); Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior); });
        var check = entity.GetCheckConstraints().Single(x => x.Name == "ck_authority_identity_evidence").Sql;
        Assert.Contains("created_evidence_stage IS NOT NULL", check); Assert.Contains("submitted_evidence_stage IS NOT NULL", check);
        Assert.Contains("evidence_sha256 IS NOT NULL", check); Assert.Contains("version=1", check);
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.GetFilter() == "stage = 'Created'");
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.GetFilter() == "stage = 'Activated'");
    }

    private static async Task SeedOrigin(string name)
    {
        await using var db = Context(name, worker: MakerWorker); var grant = Draft();
        db.Add(grant); db.Add(Origin(grant)); await db.SaveChangesAsync();
    }
}
