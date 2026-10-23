using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingPolicyIdentityTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "policy-identity"; }
    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly string Reviewer = Guid.NewGuid().ToString("D");
    private static readonly Guid MakerWorker = Guid.NewGuid(), ReviewerWorker = Guid.NewGuid();
    private static readonly Scope Owner = new("policy-identity", Guid.NewGuid(), Maker);
    private static PolicyVersion Policy() => new() { TenantId = Owner.TenantId, LegalEntityId = Owner.LegalEntityId,
        CreatedBy = Maker, PolicyKey = "SOURCING", PolicyVersionNumber = 1, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1), RulesJson = "{\"explicit\":true}" };
    private static (PolicyVersion Policy, SourcingPolicyIdentityEvidence Origin, SourcingPolicyIdentityEvidence Submitted) Pending()
    {
        var policy = Policy(); var origin = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, DateTimeOffset.UtcNow);
        policy.State = "PendingReview"; policy.Version = 2; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = DateTimeOffset.UtcNow;
        var submitted = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Submitted", Maker, MakerWorker, 2, policy.ReviewRequestedAt.Value, origin);
        return (policy, origin, submitted);
    }
    [Fact]
    public void Workerless_core_accounts_can_author_and_independently_publish_schema_two_policy()
    {
        var at = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var policy = Policy();
        var origin = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
            "Created", Maker, null, 1, at, schemaVersion: 2);
        SourcingPolicyIdentity.RequireAuthor(policy, origin, Maker, null);
        policy.Version = 2; policy.State = "PendingReview";
        policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = at;
        var submitted = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
            "Submitted", Maker, null, 2, at, origin, schemaVersion: 2);
        SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted);
        SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, Reviewer, null);
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.RequireIndependent(policy,
            origin, submitted, Maker, null));
        policy.Version = 3; policy.State = "Published";
        policy.PublishedBy = Reviewer; policy.PublishedAt = at;
        var published = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
            "Published", Reviewer, null, 3, at, origin, submitted, 2);
        SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
        Assert.Equal(2, published.SchemaVersion); Assert.Null(published.ActorWorkerId);
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.Capture(policy,
            Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, Guid.Empty, 3,
            at, origin, submitted, 2));
    }
    [Fact]
    public void Schema_one_still_requires_a_retained_worker_and_cannot_mix_evidence_versions()
    {
        var policy = Policy(); var at = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.Capture(policy,
            Owner.TenantId, Owner.LegalEntityId, "Created", Maker, null, 1, at));
        var created = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
            "Created", Maker, MakerWorker, 1, at);
        policy.Version = 2; policy.State = "PendingReview";
        policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = at;
        var mixed = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
            "Submitted", Maker, null, 2, at, created, schemaVersion: 2);
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.VerifySubmission(policy, created, mixed));
    }
    [Fact]
    public async Task Owning_change_tracker_keeps_workerless_schema_two_policy_lineage_append_only()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var at = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        Guid policyId;
        await using (var db = new ProcurementDbContext(options, Owner))
        {
            var policy = Policy(); policyId = policy.Id;
            db.AddScoped(policy);
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
                "Created", Maker, null, 1, at, schemaVersion: 2));
            await db.SaveChangesAsync();
            var origin = await db.SourcingPolicyIdentityEvidenceRecords.SingleAsync();
            policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker;
            policy.ReviewRequestedAt = at; policy.ReviewReason = "Independent review";
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
                "Submitted", Maker, null, 2, at, origin, schemaVersion: 2));
            await db.SaveChangesAsync();
        }
        await using (var db = new ProcurementDbContext(options, Owner with { SubjectId = Reviewer }))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            var rows = await db.SourcingPolicyIdentityEvidenceRecords.ToArrayAsync();
            var origin = rows.Single(x => x.Stage == "Created");
            var submitted = rows.Single(x => x.Stage == "Submitted");
            policy.State = "Published"; policy.PublishedBy = Reviewer;
            policy.PublishedAt = at; policy.PublicationReason = "Independent decision";
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId,
                "Published", Reviewer, null, 3, at, origin, submitted, 2));
            await db.SaveChangesAsync();
            Assert.Equal(3, await db.SourcingPolicyIdentityEvidenceRecords.CountAsync());
            var published = await db.SourcingPolicyIdentityEvidenceRecords.SingleAsync(x => x.Stage == "Published");
            published.EvidenceSha256 = new string('0', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
    [Fact]
    public void Original_worker_not_current_historical_account_mapping_controls_review_and_edits()
    {
        var (policy, origin, submitted) = Pending();
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, Reviewer, MakerWorker));
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.RequireAuthor(policy, origin, Maker, ReviewerWorker));
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.RequireAuthor(policy, origin, Reviewer, MakerWorker));
        // No active historical-account input exists: offboarding the maker cannot erase its original worker.
        SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, Reviewer, ReviewerWorker);
    }
    [Theory] [InlineData("rules")] [InlineData("version")] [InlineData("time")] [InlineData("subject")] [InlineData("company")]
    public void Pending_identity_requires_exact_current_document_revision_and_scope(string field)
    {
        var (policy, origin, submitted) = Pending();
        if (field == "rules") policy.RulesJson = "{}";
        if (field == "version") policy.Version++;
        if (field == "time") policy.ReviewRequestedAt = policy.ReviewRequestedAt!.Value.AddSeconds(1);
        if (field == "subject") policy.ReviewRequestedBy = Reviewer;
        if (field == "company") policy.LegalEntityId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, Reviewer, ReviewerWorker));
    }
    [Theory] [InlineData("submission-time")] [InlineData("submission-revision")] [InlineData("publication-time")]
    public void Otherwise_valid_hash_bound_lineage_rejects_backdating_or_nonadvancing_submission(string fault)
    {
        var policy = Policy(); var at = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var origin = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, at);
        policy.RulesJson = "{\"editedByOriginalAuthor\":true}";
        policy.State = "PendingReview"; policy.Version = fault == "submission-revision" ? 1 : 4;
        policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = fault == "submission-time" ? at.AddTicks(-10) : at;
        var submitted = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Submitted", Maker, MakerWorker,
            policy.Version, policy.ReviewRequestedAt.Value, origin);
        SourcingPolicyIdentity.Verify(origin); SourcingPolicyIdentity.Verify(submitted);
        if (fault != "publication-time")
            Assert.Equal("SourcingPolicySubmittedIdentityMismatch", Assert.Throws<InvalidOperationException>(() =>
                SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted)).Message);
        else SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted);
        policy.State = "Published"; policy.PublishedBy = Reviewer;
        policy.PublishedAt = fault == "publication-time" ? at.AddTicks(-10) : at;
        policy.Version = submitted.PolicyRecordVersionAtAction + 1;
        var published = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, ReviewerWorker,
            policy.Version, policy.PublishedAt.Value, origin, submitted);
        SourcingPolicyIdentity.Verify(published);
        Assert.Equal("SourcingPolicyPublicationIdentityMismatch", Assert.Throws<InvalidOperationException>(() =>
            SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published)).Message);
    }
    [Theory] [InlineData(2)] [InlineData(5)]
    public void Equal_canonical_times_and_edited_draft_revisions_are_supported(long submittedRevision)
    {
        var policy = Policy(); var at = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var origin = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, at);
        policy.RulesJson = "{\"editedByOriginalAuthor\":true}";
        policy.State = "PendingReview"; policy.Version = submittedRevision; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = at.AddTicks(9);
        var submitted = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Submitted", Maker, MakerWorker,
            policy.Version, policy.ReviewRequestedAt.Value, origin);
        SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted);
        Assert.Equal(origin.ActionAt, submitted.ActionAt); Assert.NotEqual(origin.RulesSha256, submitted.RulesSha256);
        policy.State = "Published"; policy.PublishedBy = Reviewer; policy.PublishedAt = at.AddTicks(1); policy.Version++;
        var published = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, ReviewerWorker,
            policy.Version, policy.PublishedAt.Value, origin, submitted);
        SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
    }
    [Theory] [InlineData("submission-time")] [InlineData("submission-revision")] [InlineData("publication-time")]
    public async Task Save_parent_guards_reject_otherwise_valid_bad_order_before_any_persistence(string fault)
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var at = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero); Guid id; int audits;
        await using (var db = new ProcurementDbContext(options, Owner))
        {
            var policy = Policy(); id = policy.Id; db.AddScoped(policy);
            var origin = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, at);
            db.AddScoped(origin); await db.SaveChangesAsync(); audits = await db.AuditEvents.CountAsync();
            if (fault == "submission-revision")
            {
                // Denial-only metadata regression: the proposed revision agrees with this corrupted
                // tracked CAS base, but cannot precede/equal the actual retained Created revision.
                db.Entry(policy).OriginalValues[nameof(PolicyVersion.Version)] = 0L; policy.Version = 0;
            }
            policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker;
            policy.ReviewRequestedAt = fault == "submission-time" ? at.AddTicks(-10) : at; policy.ReviewReason = "Explicit review";
            var submitted = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Submitted", Maker, MakerWorker,
                policy.Version + 1, policy.ReviewRequestedAt.Value, origin);
            SourcingPolicyIdentity.Verify(submitted); db.AddScoped(submitted);
            if (fault != "publication-time") Assert.Equal("SourcingPolicySubmittedIdentityMismatch",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
            else { await db.SaveChangesAsync(); audits = await db.AuditEvents.CountAsync(); }
        }
        if (fault == "publication-time")
        {
            await using var db = new ProcurementDbContext(options, Owner with { SubjectId = Reviewer });
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
            var origin = await db.SourcingPolicyIdentityEvidenceRecords.SingleAsync(x => x.Stage == "Created");
            var submitted = await db.SourcingPolicyIdentityEvidenceRecords.SingleAsync(x => x.Stage == "Submitted");
            policy.State = "Published"; policy.PublishedBy = Reviewer; policy.PublishedAt = at.AddTicks(-10); policy.PublicationReason = "Independent review";
            var published = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, ReviewerWorker,
                policy.Version + 1, policy.PublishedAt.Value, origin, submitted);
            SourcingPolicyIdentity.Verify(published); db.AddScoped(published);
            Assert.Equal("SourcingPolicyIndependentReviewerRequired", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        }
        await using var read = new ProcurementDbContext(options, Owner);
        var retained = await read.PolicyVersions.SingleAsync(x => x.Id == id);
        Assert.Equal(fault == "publication-time" ? "PendingReview" : "Draft", retained.State);
        Assert.Equal(fault == "publication-time" ? 2 : 1, retained.Version);
        Assert.Equal(fault == "publication-time" ? 2 : 1, await read.SourcingPolicyIdentityEvidenceRecords.CountAsync());
        Assert.Equal(audits, await read.AuditEvents.CountAsync());
    }

    [Fact]
    public void Retained_hash_detects_tamper_and_publication_retains_independent_worker_after_supersession()
    {
        var (policy, origin, submitted) = Pending();
        policy.State = "Published"; policy.PublishedAt = DateTimeOffset.UtcNow; policy.PublishedBy = Reviewer;
        var published = SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, ReviewerWorker, 3,
            policy.PublishedAt.Value, origin, submitted);
        policy.Version = 3; SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
        policy.Version = 4; policy.EffectiveTo = DateTimeOffset.UtcNow.AddDays(2);
        SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
        published.EvidenceSha256 = new string('a', 64);
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published));
    }
    [Fact]
    public async Task Real_change_tracker_saves_created_submitted_published_evidence_atomically_and_denies_mutation()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Guid id;
        await using (var db = new ProcurementDbContext(options, Owner))
        {
            var policy = Policy(); id = policy.Id; db.AddScoped(policy);
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            var origin = await db.SourcingPolicyIdentityEvidenceRecords.SingleAsync();
            policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = DateTimeOffset.UtcNow; policy.ReviewReason = "Explicit review";
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Submitted", Maker, MakerWorker, 2, policy.ReviewRequestedAt.Value, origin));
            await db.SaveChangesAsync(); Assert.Equal(2, policy.Version);
        }
        await using (var db = new ProcurementDbContext(options, Owner with { SubjectId = Reviewer }))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == id);
            var rows = await db.SourcingPolicyIdentityEvidenceRecords.ToArrayAsync();
            var origin = rows.Single(x => x.Stage == "Created"); var submitted = rows.Single(x => x.Stage == "Submitted");
            SourcingPolicyIdentity.RequireIndependent(policy, origin, submitted, Reviewer, ReviewerWorker);
            policy.State = "Published"; policy.PublishedAt = DateTimeOffset.UtcNow; policy.PublishedBy = Reviewer; policy.PublicationReason = "Independent review";
            db.AddScoped(SourcingPolicyIdentity.Capture(policy, Owner.TenantId, Owner.LegalEntityId, "Published", Reviewer, ReviewerWorker, 3, policy.PublishedAt.Value, origin, submitted));
            await db.SaveChangesAsync(); Assert.Equal(3, policy.Version);
        }
        await using (var db = new ProcurementDbContext(options, Owner))
        {
            var row = await db.SourcingPolicyIdentityEvidenceRecords.FirstAsync(); row.EvidenceSha256 = new string('a', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = new ProcurementDbContext(options, Owner))
        {
            db.Remove(await db.SourcingPolicyIdentityEvidenceRecords.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
    [Fact]
    public async Task Legacy_unbound_sourcing_and_orphan_added_evidence_cannot_gain_maker_identity()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, Owner);
        db.AddScoped(Policy());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Reconstructed_legacy_row_cannot_be_backfilled_and_added_origin_requires_same_save_new_parent()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, Owner);
        var legacy = Policy(); db.Attach(legacy); // Denial-only reconstruction, NOT persisted historical authority.
        db.AddScoped(SourcingPolicyIdentity.Capture(legacy, Owner.TenantId, Owner.LegalEntityId, "Created", Maker, MakerWorker, 1, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public void Mapping_uses_only_stable_owned_keys_and_distinct_created_published_unique_indexes()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql("Host=invalid;Database=design_only;Username=invalid").Options, Owner);
        var model = db.GetService<IDesignTimeModel>().Model;
        var evidence = model.FindEntityType(typeof(SourcingPolicyIdentityEvidence))!;
        Assert.Equal(2, evidence.GetIndexes().Count(x => x.GetDatabaseName() is "ux_source_policy_identity_created" or "ux_source_policy_identity_published"));
        Assert.DoesNotContain(evidence.GetKeys().SelectMany(x => x.Properties), x => x.Name == nameof(SourcingPolicyIdentityEvidence.PolicyRecordVersionAtAction));
        Assert.DoesNotContain(model.FindEntityType(typeof(PolicyVersion))!.GetKeys().SelectMany(x => x.Properties), x => x.Name == nameof(PolicyVersion.Version));
        Assert.All(evidence.GetForeignKeys(), x => Assert.Contains(x.Properties, p => p.Name == nameof(SourcingPolicyIdentityEvidence.TenantId)));
        Assert.Equal(new[] { "fk_source_policy_identity_created", "fk_source_policy_identity_owner", "fk_source_policy_identity_submitted" },
            evidence.GetForeignKeys().Select(x => x.GetConstraintName()).OrderBy(x => x, StringComparer.Ordinal));
        var check = Assert.Single(evidence.GetCheckConstraints()); Assert.Contains("created_evidence_stage IS NOT NULL", check.Sql);
        Assert.Contains("rules_sha256 IS NOT NULL", check.Sql); Assert.Contains("evidence_sha256 IS NOT NULL", check.Sql);
        Assert.Contains("schema_version IN (1,2)", check.Sql);
        Assert.True(evidence.FindProperty(nameof(SourcingPolicyIdentityEvidence.ActorWorkerId))!.IsNullable);
    }
}
