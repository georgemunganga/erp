using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>SaveChanges/model counterexamples only; InMemory cannot prove PostgreSQL FK/CHECK enforcement.</summary>
public sealed class OperationalPolicyIdentityPersistenceTests
{
    private const string Maker = "11111111-1111-4111-8111-111111111111";
    private const string Reviewer = "22222222-2222-4222-8222-222222222222";
    private static readonly Guid MakerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ReviewerWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid Company = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly InMemoryDatabaseRoot Store = new();
    private sealed class Scope(string actor, Guid? worker, Guid? coreAlias = null) : IProcurementScope
    {
        public string TenantId => "operational-policy-test";
        public Guid LegalEntityId => Company;
        public string SubjectId => actor;
        public Guid? WorkerId => worker;
        public Guid? CoreLinkedWorkerId => coreAlias ?? worker;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "operational-policy-test";
    }
    private static ProcurementDbContext Context(string name, string actor = Maker, Guid? worker = null)
        => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name, Store).Options, new Scope(actor, worker));
    private static PolicyVersion Policy(string key = "RECEIVING", int number = 1) => new()
    {
        TenantId = "operational-policy-test", LegalEntityId = Company, PolicyKey = key,
        PolicyVersionNumber = number, State = "Draft", RulesJson = "{\"original\":true}",
        CreatedBy = Maker, CreatedAt = At, EffectiveFrom = At.AddDays(number), Version = 1,
    };
    private static OperationalPolicyIdentityRecord Capture(PolicyVersion policy, string stage, string actor, Guid worker,
        long revision, DateTimeOffset at, OperationalPolicyWorkerIdentityEvidence? origin = null, OperationalPolicyWorkerIdentityEvidence? submitted = null)
        => OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, stage, actor, worker, revision, at, origin, submitted));
    private static async Task<OperationalPolicyWorkerIdentityEvidence> Origin(ProcurementDbContext db, Guid id)
        => OperationalPolicyIdentityStorage.Read(await db.OperationalPolicyIdentityRecords.AsNoTracking().SingleAsync(x => x.PolicyVersionId == id && x.Stage == "Created"));

    [Theory] [InlineData("RECEIVING")] [InlineData("INVOICE_MATCHING")] [InlineData("INVOICE_UNIT_REVIEW")]
    public async Task Owning_create_submit_publish_roundtrip_retains_exact_people_rules_revision_and_metadata(string key)
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name, key); await Submit(name, id); await Publish(name, id);
        await using var db = Context(name, Reviewer, ReviewerWorker);
        var policy = await db.PolicyVersions.SingleAsync();
        var rows = await db.OperationalPolicyIdentityRecords.ToArrayAsync();
        Assert.Equal(3, policy.Version); Assert.Equal(At, policy.CreatedAt);
        OperationalPolicyWorkerIdentity.VerifyPublication(policy,
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Created")),
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted")),
            OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Published")));
        Assert.All(rows, row => { Assert.Equal(1, row.Version); Assert.Equal(row.ActorPrincipalId, row.CreatedBy); Assert.Equal(row.ActionAt, row.CreatedAt); });
        Assert.Equal(6, await db.AuditEvents.CountAsync());
    }

    [Theory] [InlineData("missing-worker")] [InlineData("rebound-worker")] [InlineData("wrong-key")]
    [InlineData("wrong-digest")] [InlineData("metadata")]
    public async Task Created_proof_must_match_real_scope_initial_rules_and_inherited_metadata(string fault)
    {
        var name = Guid.NewGuid().ToString(); var policy = Policy();
        var row = Capture(policy, "Created", Maker, MakerWorker, 1, At);
        if (fault == "wrong-key")
        {
            var value = OperationalPolicyIdentityStorage.Read(row) with { PolicyKey = "INVOICE_MATCHING" };
            row = OperationalPolicyIdentityStorage.ToRecord(value with { EvidenceSha256 = OperationalPolicyWorkerIdentity.Digest(value) });
        }
        if (fault == "wrong-digest") policy.RulesJson = "{\"changedAfterCapture\":true}";
        if (fault == "metadata") row.CreatedBy = Reviewer;
        await using (var command = Context(name, worker: fault == "missing-worker" ? null : fault == "rebound-worker" ? ReviewerWorker : MakerWorker))
        {
            command.Add(policy); command.Add(row);
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync());
        }
        await using var verify = Context(name); Assert.Empty(await verify.PolicyVersions.ToArrayAsync()); Assert.Empty(await verify.AuditEvents.ToArrayAsync());
    }

    [Fact]
    public async Task Ordinary_runtime_added_operational_policy_cannot_bypass_original_proof()
    {
        var name = Guid.NewGuid().ToString(); await using var db = Context(name, worker: MakerWorker);
        db.Add(Policy());
        Assert.Equal("OperationalPolicyOriginalMakerRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public async Task Raw_pre_migration_legacy_policy_stays_readable_but_cannot_be_adopted_or_edited()
    {
        var name = Guid.NewGuid().ToString(); var legacy = Policy(); await RawLegacy(name, legacy);
        await using (var command = Context(name, worker: MakerWorker))
        {
            var stored = await command.PolicyVersions.SingleAsync(); stored.RulesJson = "{\"attemptedAdoption\":true}";
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync());
        }
        await using var verify = Context(name); Assert.Equal(legacy.RulesJson, (await verify.PolicyVersions.SingleAsync()).RulesJson);
        Assert.Empty(await verify.OperationalPolicyIdentityRecords.ToArrayAsync()); Assert.Empty(await verify.AuditEvents.ToArrayAsync());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Evidence_and_inherited_metadata_cannot_be_updated_or_deleted(bool delete)
    {
        var name = Guid.NewGuid().ToString(); await Create(name);
        await using var db = Context(name, worker: MakerWorker); var proof = await db.OperationalPolicyIdentityRecords.SingleAsync();
        if (delete) db.Remove(proof); else proof.CreatedAt = proof.CreatedAt.AddMinutes(1);
        Assert.Equal("OperationalPolicyIdentityAppendOnly", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public async Task Attached_unchanged_fake_origin_is_not_persisted_lineage()
    {
        var name = Guid.NewGuid().ToString(); var legacy = Policy(); await RawLegacy(name, legacy);
        await using var db = Context(name, worker: MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
        var origin = Capture(policy, "Created", Maker, MakerWorker, 1, At); db.Attach(origin);
        policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = At.AddMinutes(1); policy.ReviewReason = "Review";
        db.Add(Capture(policy, "Submitted", Maker, MakerWorker, 2, policy.ReviewRequestedAt.Value, OperationalPolicyIdentityStorage.Read(origin)));
        Assert.Equal("OperationalPolicyIdentityRetainedLineageRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        await using var verify = Context(name); Assert.Equal("Draft", (await verify.PolicyVersions.SingleAsync()).State); Assert.Empty(await verify.AuditEvents.ToArrayAsync());
    }

    [Fact]
    public async Task Original_account_rebound_to_another_person_cannot_edit_or_submit()
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name);
        await using var db = Context(name, worker: ReviewerWorker); var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
        policy.RulesJson = "{\"newPerson\":true}";
        Assert.Equal("OperationalPolicyOriginalAuthorRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public async Task Rehashed_submission_with_stale_intended_revision_does_not_advance_policy()
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name);
        await using var db = Context(name, worker: MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
        var origin = await Origin(db, id); policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = At.AddMinutes(1); policy.ReviewReason = "Review";
        db.Add(Capture(policy, "Submitted", Maker, MakerWorker, 3, policy.ReviewRequestedAt.Value, origin)); // Actual next revision is 2.
        Assert.Equal("OperationalPolicyIdentityActionMismatch", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Theory] [InlineData("maker-alias")] [InlineData("changed-rules")]
    public async Task Publication_rejects_original_maker_alias_and_rule_change_after_submission(string fault)
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name); await Submit(name, id);
        var worker = fault == "maker-alias" ? MakerWorker : ReviewerWorker;
        await using var db = Context(name, Reviewer, worker); var policy = await db.PolicyVersions.SingleAsync();
        var origin = await Origin(db, id);
        var submitted = OperationalPolicyIdentityStorage.Read(await db.OperationalPolicyIdentityRecords.SingleAsync(x => x.Stage == "Submitted"));
        if (fault == "changed-rules") policy.RulesJson = "{\"switchedAfterReview\":true}";
        policy.State = "Published"; policy.PublishedBy = Reviewer; policy.PublishedAt = At.AddMinutes(2); policy.PublicationReason = "Reviewed";
        db.Add(Capture(policy, "Published", Reviewer, worker, 3, policy.PublishedAt.Value, origin, submitted));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var verify = Context(name); Assert.Equal("PendingReview", (await verify.PolicyVersions.SingleAsync()).State);
        Assert.Equal(2, await verify.OperationalPolicyIdentityRecords.CountAsync()); Assert.Equal(4, await verify.AuditEvents.CountAsync());
    }

    [Theory] [InlineData("rules")] [InlineData("unlinked-shortening")] [InlineData("extension")] [InlineData("metadata-only")]
    public async Task Published_business_is_immutable_without_exact_same_save_replacement(string fault)
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name, effectiveTo: fault == "extension" ? At.AddDays(5) : null); await Submit(name, id); await Publish(name, id);
        await using var db = Context(name, Reviewer, ReviewerWorker); var policy = await db.PolicyVersions.SingleAsync();
        switch (fault)
        {
            case "rules": policy.RulesJson = "{\"changed\":true}"; break;
            case "unlinked-shortening": policy.EffectiveTo = policy.EffectiveFrom.AddDays(1); break;
            case "extension": policy.EffectiveTo = At.AddDays(6); break;
            default: db.Entry(policy).State = EntityState.Modified; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Only_same_key_genuine_published_replacement_can_shorten_retained_period(bool foreignKey)
    {
        var name = Guid.NewGuid().ToString(); var originalId = await Create(name); await Submit(name, originalId); await Publish(name, originalId);
        var replacementId = await Create(name, foreignKey ? "INVOICE_MATCHING" : "RECEIVING", 2); await Submit(name, replacementId);
        await using var db = Context(name, Reviewer, ReviewerWorker);
        var previous = await db.PolicyVersions.SingleAsync(x => x.Id == originalId);
        var replacement = await db.PolicyVersions.SingleAsync(x => x.Id == replacementId);
        previous.EffectiveTo = replacement.EffectiveFrom;
        var origin = await Origin(db, replacementId);
        var submitted = OperationalPolicyIdentityStorage.Read(await db.OperationalPolicyIdentityRecords.SingleAsync(x => x.PolicyVersionId == replacementId && x.Stage == "Submitted"));
        replacement.State = "Published"; replacement.PublishedAt = At.AddMinutes(2); replacement.PublishedBy = Reviewer; replacement.PublicationReason = "Independent replacement";
        db.Add(Capture(replacement, "Published", Reviewer, ReviewerWorker, 3, replacement.PublishedAt.Value, origin, submitted));
        if (foreignKey) await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        else
        {
            await db.SaveChangesAsync(); Assert.Equal(4, previous.Version);
            var retained = await db.OperationalPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == originalId).ToArrayAsync();
            OperationalPolicyWorkerIdentity.VerifyPublication(previous,
                OperationalPolicyIdentityStorage.Read(retained.Single(x => x.Stage == "Created")),
                OperationalPolicyIdentityStorage.Read(retained.Single(x => x.Stage == "Submitted")),
                OperationalPolicyIdentityStorage.Read(retained.Single(x => x.Stage == "Published")), OperationalPolicyPublicationMode.RetainedHistorical);
        }
    }

    [Fact]
    public async Task Return_requires_independent_original_person_and_resubmission_retains_a_new_round()
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name); await Submit(name, id);
        await using (var deny = Context(name, Reviewer, MakerWorker))
        {
            var pending = await deny.PolicyVersions.SingleAsync(); pending.State = "Draft";
            Assert.Equal("OperationalPolicyIndependentReviewerRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => deny.SaveChangesAsync())).Message);
        }
        await using (var review = Context(name, Reviewer, ReviewerWorker))
        {
            var pending = await review.PolicyVersions.SingleAsync(); pending.State = "Draft"; await review.SaveChangesAsync();
        }
        await Submit(name, id, At.AddMinutes(3));
        await using var verify = Context(name); Assert.Equal(4, (await verify.PolicyVersions.SingleAsync()).Version);
        Assert.Equal(2, await verify.OperationalPolicyIdentityRecords.CountAsync(x => x.Stage == "Submitted"));
    }

    [Fact]
    public async Task Owner_record_revision_cannot_be_manually_advanced_to_forge_a_same_save_action()
    {
        var name = Guid.NewGuid().ToString(); var id = await Create(name);
        await using var command = Context(name, worker: MakerWorker);
        var policy = await command.PolicyVersions.SingleAsync(); var origin = await Origin(command, id);
        policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = At.AddMinutes(1); policy.ReviewReason = "Review";
        policy.Version = 7;
        command.Add(Capture(policy, "Submitted", Maker, MakerWorker, 8, policy.ReviewRequestedAt.Value, origin));
        Assert.Equal("OperationalPolicyOwnerVersionManagedByStore", (await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync())).Message);
        await using var verify = Context(name); Assert.Equal(1, (await verify.PolicyVersions.SingleAsync()).Version);
        Assert.Single(await verify.OperationalPolicyIdentityRecords.ToArrayAsync());
    }

    [Fact]
    public void Npgsql_model_has_typed_stable_owner_and_stage_keys_without_changing_P29_owner_key()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=invalid;Database=design_only;Username=design_only;Password=unused").Options, new Scope(Maker, MakerWorker));
        var model = db.GetService<IDesignTimeModel>().Model;
        var policy = model.FindEntityType(typeof(PolicyVersion))!;
        Assert.Contains(policy.GetKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id", "PolicyVersionNumber" }));
        var identity = model.FindEntityType(typeof(OperationalPolicyIdentityRecord))!;
        var owner = identity.GetForeignKeys().Single(x => x.PrincipalEntityType == policy);
        Assert.Equal("ak_operational_policy_identity_owner", owner.PrincipalKey.GetName());
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "Id", "PolicyKey", "PolicyVersionNumber" }, owner.PrincipalKey.Properties.Select(x => x.Name));
        Assert.DoesNotContain(owner.PrincipalKey.Properties, x => x.Name == "Version");
        Assert.All(identity.GetForeignKeys().Where(x => x.PrincipalEntityType == identity), fk => Assert.Contains(fk.Properties, p => p.Name == "PolicyKey"));
        Assert.Equal(3, identity.GetIndexes().Count(x => x.IsUnique));
        var check = identity.GetCheckConstraints().Single().Sql;
        Assert.Contains("created_evidence_stage IS NOT NULL", check); Assert.Contains("submitted_evidence_stage IS NOT NULL", check);
        Assert.Contains("rules_sha256 IS NOT NULL", check); Assert.Contains("version=1", check);
    }

    [Theory] [InlineData(1)][InlineData(2)]
    public async Task Only_new_principal_evidence_emits_durable_downgrade_marker_without_rewriting_legacy_audit(int version)
    {
        var name = Guid.NewGuid().ToString(); await using var db = Context(name, worker: version == 1 ? MakerWorker : null);
        var policy = Policy();
        var proof = OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy,
            "Created", Maker, version == 1 ? MakerWorker : null, 1, At, schemaVersion: version));
        db.Add(policy); db.Add(proof); await db.SaveChangesAsync();
        var audit = await db.AuditEvents.SingleAsync(x => x.RecordType == nameof(OperationalPolicyIdentityRecord));
        using var json = System.Text.Json.JsonDocument.Parse(audit.ChangedFieldsJson);
        if (version == 1) Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
        else
        {
            Assert.Equal(2, json.RootElement.GetProperty("principalEvidenceVersion").GetInt32());
            Assert.Equal("OperationalPolicyIdentity", json.RootElement.GetProperty("principalEvidenceKind").GetString());
            Assert.Contains(json.RootElement.GetProperty("fields").EnumerateArray(), x => x.GetString() == nameof(OperationalPolicyIdentityRecord.SchemaVersion));
        }
    }
    [Fact]
    public async Task Principal_policy_retains_inactive_Core_alias_after_author_link_clear_and_blocks_old_alias_reviewer_without_effects()
    {
        var name = Guid.NewGuid().ToString(); Guid id;
        ProcurementDbContext AliasContext(string actor, Guid? alias) => new(
            new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name, Store).Options, new Scope(actor, null, alias));
        await using (var create = AliasContext(Maker, MakerWorker))
        {
            var policy = Policy(); id = policy.Id;
            create.Add(policy); create.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(
                policy, "Created", Maker, MakerWorker, 1, At, schemaVersion: 2)));
            await create.SaveChangesAsync();
        }
        await using (var submit = AliasContext(Maker, null))
        {
            var policy = await submit.PolicyVersions.SingleAsync(); var created = await Origin(submit, id);
            policy.State = "PendingReview"; policy.ReviewRequestedAt = At.AddMinutes(1); policy.ReviewRequestedBy = Maker; policy.ReviewReason = "Review principal policy";
            submit.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Submitted", Maker,
                null, 2, policy.ReviewRequestedAt.Value, created, schemaVersion: 2))); await submit.SaveChangesAsync();
        }
        await using (var publish = AliasContext(Reviewer, MakerWorker))
        {
            var policy = await publish.PolicyVersions.SingleAsync(); var created = await Origin(publish, id);
            var submitted = OperationalPolicyIdentityStorage.Read(await publish.OperationalPolicyIdentityRecords.SingleAsync(x => x.Stage == "Submitted"));
            policy.State = "Published"; policy.PublishedAt = At.AddMinutes(2); policy.PublishedBy = Reviewer; policy.PublicationReason = "Known alias must deny";
            publish.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Published", Reviewer,
                MakerWorker, 3, policy.PublishedAt.Value, created, submitted, 2)));
            Assert.Equal("OperationalPolicyIndependentReviewerRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => publish.SaveChangesAsync())).Message);
        }
        await using var verify = Context(name);
        var saved = await verify.PolicyVersions.SingleAsync(); Assert.Equal("PendingReview", saved.State); Assert.Equal(2, saved.Version);
        Assert.Equal(2, await verify.OperationalPolicyIdentityRecords.CountAsync()); Assert.Equal(4, await verify.AuditEvents.CountAsync());
        Assert.Equal(MakerWorker, (await verify.OperationalPolicyIdentityRecords.SingleAsync(x => x.Stage == "Created")).ActorWorkerId);
    }
    [Fact]
    public async Task Known_Core_alias_is_not_active_worker_authority_for_legacy_identity_one()
    {
        var name = Guid.NewGuid().ToString();
        await using (var command = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(name, Store).Options, new Scope(Maker, null, MakerWorker)))
        {
            var policy = Policy(); command.Add(policy); command.Add(Capture(policy, "Created", Maker, MakerWorker, 1, At));
            Assert.Equal("OperationalPolicyIdentityActorMismatch", (await Assert.ThrowsAsync<InvalidOperationException>(() => command.SaveChangesAsync())).Message);
        }
        await using var verify = Context(name);
        Assert.Empty(await verify.PolicyVersions.ToArrayAsync()); Assert.Empty(await verify.OperationalPolicyIdentityRecords.ToArrayAsync());
        Assert.Empty(await verify.AuditEvents.ToArrayAsync());
    }
    private static async Task<Guid> Create(string name, string key = "RECEIVING", int number = 1, DateTimeOffset? effectiveTo = null)
    {
        await using var db = Context(name, worker: MakerWorker); var policy = Policy(key, number); policy.EffectiveTo = effectiveTo;
        db.Add(policy); db.Add(Capture(policy, "Created", Maker, MakerWorker, 1, At));
        await db.SaveChangesAsync(); return policy.Id;
    }
    private static async Task Submit(string name, Guid id, DateTimeOffset? at = null)
    {
        await using var db = Context(name, worker: MakerWorker); var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
        var origin = await Origin(db, id); policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker;
        policy.ReviewRequestedAt = at ?? At.AddMinutes(1); policy.ReviewReason = "Review exact rules";
        db.Add(Capture(policy, "Submitted", Maker, MakerWorker, policy.Version + 1, policy.ReviewRequestedAt.Value, origin));
        await db.SaveChangesAsync();
    }
    private static async Task Publish(string name, Guid id)
    {
        await using var db = Context(name, Reviewer, ReviewerWorker); var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
        var origin = await Origin(db, id);
        var submitted = OperationalPolicyIdentityStorage.Read(await db.OperationalPolicyIdentityRecords.SingleAsync(x => x.PolicyVersionId == id && x.Stage == "Submitted"));
        policy.State = "Published"; policy.PublishedAt = At.AddMinutes(2); policy.PublishedBy = Reviewer; policy.PublicationReason = "Reviewed independently";
        db.Add(Capture(policy, "Published", Reviewer, ReviewerWorker, policy.Version + 1, policy.PublishedAt.Value, origin, submitted));
        await db.SaveChangesAsync();
    }
    private static async Task RawLegacy(string name, PolicyVersion policy)
    {
        // Owner-only pre-migration fixture. The ordinary Procurement SaveChanges path must reject this write.
        await using var modelOwner = Context(name);
        await using var raw = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(name, Store).UseModel(modelOwner.Model).Options);
        raw.Add(policy); await raw.SaveChangesAsync();
    }
}
