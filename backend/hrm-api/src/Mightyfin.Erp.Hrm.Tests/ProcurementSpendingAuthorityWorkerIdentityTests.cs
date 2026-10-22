using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Owning-store/current Core identity bridge tests. InMemory is not HTTP/PostgreSQL concurrency proof.</summary>
public sealed class ProcurementSpendingAuthorityWorkerIdentityTests
{
    [Fact]
    public async Task Creation_resolves_unique_canonical_people_and_rejects_missing_or_stale_actor_worker()
    {
        await using var f = await Fixture.Start();
        var scope = f.Scope(f.Maker, f.MakerWorker);
        var actors = await ProcurementSpendingAuthorityWorkerIdentity.ResolveCreationActorsAsync(f.Core, scope, f.Recipient, default);
        Assert.Equal(f.MakerWorker, actors.Actor.WorkerId); Assert.Equal(f.RecipientWorker, actors.Recipient.WorkerId);
        foreach (var worker in new Guid?[] { null, f.ReviewerWorker })
            await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.ResolveCreationActorsAsync(
                f.Core, f.Scope(f.Maker, worker), f.Recipient, default));
        await f.AddAlias(f.RecipientWorker);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.ResolveCreationActorsAsync(
            f.Core, scope, f.Recipient, default));
    }

    [Fact]
    public async Task Original_author_is_not_transferred_when_the_account_worker_changes()
    {
        await using var f = await Fixture.Start();
        await using var db = f.Db(f.Maker, f.MakerWorker); var grant = await f.Grant(db);
        var proof = await ProcurementSpendingAuthorityWorkerIdentity.RequireAuthorAsync(f.Core, db, f.Scope(f.Maker, f.MakerWorker), grant, default);
        Assert.Equal(f.MakerWorker, proof.Created.ActorWorkerId);
        await f.SwapMakerReviewer();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireAuthorAsync(
            f.Core, db, f.Scope(f.Maker, f.ReviewerWorker), grant, default));
    }

    [Fact]
    public async Task Original_maker_cannot_review_through_a_different_unique_current_account_after_swap()
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.SwapMakerReviewer();
        await using var db = f.Db(f.Reviewer, f.MakerWorker); var grant = await f.Grant(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(
            f.Core, db, f.Scope(f.Reviewer, f.MakerWorker), grant, default));
        Assert.Equal("AuthorityIndependentWorkerRequired", error.Message);
        Assert.Equal("PendingReview", grant.State); Assert.Equal(2, await db.SpendingAuthorityIdentityRecords.CountAsync());
    }

    [Fact]
    public async Task Independent_current_reviewer_can_review_with_original_maker_offboarded()
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.OffboardMaker();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var grant = await f.Grant(db);
        var before = await db.AuditEvents.CountAsync();
        var proof = await ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(f.Core, db,
            f.Scope(f.Reviewer, f.ReviewerWorker), grant, default);
        Assert.Equal(f.MakerWorker, proof.Created.ActorWorkerId);
        Assert.Equal(f.ReviewerWorker, proof.Actor.WorkerId);
        Assert.Equal(before, await db.AuditEvents.CountAsync()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Eligibility_checks_recipient_binding_even_for_new_case_with_no_assignment_pin()
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.Activate();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var grant = await f.Grant(db);
        await ProcurementSpendingAuthorityWorkerIdentity.RequireEligibleRecipientAsync(f.Core, db,
            f.Scope(f.Reviewer, f.ReviewerWorker), grant, f.Recipient, f.RecipientWorker, default);
        // Another existing canonical person replaces the recipient; reviewer remains independently active.
        var replacement = new Worker { EmployeeNo = "replacement", FirstName = "Replacement", LastName = "Person", Status = "active", LocationId = f.Branch };
        f.Core.Workers.Add(replacement); await f.Core.SaveChangesAsync();
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Recipient))).WorkerId = replacement.Id;
        await f.Core.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireEligibleRecipientAsync(
            f.Core, db, f.Scope(f.Reviewer, f.ReviewerWorker), grant, f.Recipient, replacement.Id, default));
        Assert.Equal("AuthorityRecipientIdentityChanged", error.Message);
        // Retiring a proved predecessor does not demand an active historical recipient.
        await ProcurementSpendingAuthorityWorkerIdentity.RequireRetainedActivationAsync(db, f.Scope(f.Reviewer, f.ReviewerWorker), grant, default);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("alias")]
    [InlineData("stale-expected-worker")]
    public async Task Current_recipient_must_be_active_unique_and_match_server_candidate(string fault)
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.Activate();
        if (fault == "inactive") { (await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Recipient))).IsActive = false; await f.Core.SaveChangesAsync(); }
        if (fault == "alias") await f.AddAlias(f.RecipientWorker);
        await using var db = f.Db(f.Maker, f.MakerWorker); var grant = await f.Grant(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireEligibleRecipientAsync(
            f.Core, db, f.Scope(f.Maker, f.MakerWorker), grant, f.Recipient,
            fault == "stale-expected-worker" ? f.ReviewerWorker : f.RecipientWorker, default));
    }

    [Fact]
    public async Task Missing_legacy_evidence_is_not_reconstructed_from_active_Core_or_attached_envelope()
    {
        await using var f = await Fixture.Start(legacy: true);
        await using var db = f.Db(f.Maker, f.MakerWorker); var grant = await f.Grant(db);
        var invented = SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureCreation(grant, f.MakerWorker, f.RecipientWorker));
        db.Attach(invented);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(db,
            f.Scope(f.Maker, f.MakerWorker), grant, default));
    }

    [Fact]
    public async Task Current_pending_round_never_falls_back_to_a_previous_submission()
    {
        await using var f = await Fixture.Start(); await f.Submit();
        await using (var revise = f.Db(f.Reviewer, f.ReviewerWorker))
        {
            var grant = await f.Grant(revise);
            SpendingAuthorityConfiguration.ReturnForChanges(grant, grant.Version,
                f.Actor(f.Reviewer, SpendingAuthorityConfiguration.ReviewPermission), "Revise", f.At.AddMinutes(2));
            await revise.SaveChangesAsync();
        }
        await using (var submit = f.Db(f.Maker, f.MakerWorker))
        {
            var grant = await f.Grant(submit);
            // Owner-only malformed fixture: existing A2 permits explicit legacy grant writes;
            // deliberately omit the new round's identity evidence to exercise fail-closed loading.
            SpendingAuthorityConfiguration.Submit(grant, grant.Version,
                f.Actor(f.Maker, SpendingAuthorityConfiguration.ConfigurePermission), f.Subject(), f.At.AddMinutes(3));
            await submit.SaveChangesAsync();
        }
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var pending = await f.Grant(db);
        Assert.Equal(4, pending.Version);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(
            f.Core, db, f.Scope(f.Reviewer, f.ReviewerWorker), pending, default));
        Assert.Equal("AuthorityHistoricalIdentityMissing", error.Message);
    }

    [Theory]
    [InlineData("foreign-company")]
    [InlineData("fingerprint")]
    [InlineData("version")]
    public async Task Retained_activation_cannot_be_used_with_changed_owner_scope_values_or_revision(string fault)
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.Activate();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var grant = await f.Grant(db);
        if (fault == "foreign-company") grant.LegalEntityId = Guid.NewGuid();
        if (fault == "fingerprint") grant.MaximumAmount += 1;
        if (fault == "version") grant.Version += 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementSpendingAuthorityWorkerIdentity.RequireRetainedActivationAsync(
            db, f.Scope(f.Reviewer, f.ReviewerWorker), grant, default));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string database = Guid.NewGuid().ToString();
        internal string Maker { get; } = Guid.NewGuid().ToString("D");
        internal string Recipient { get; } = Guid.NewGuid().ToString("D");
        internal string Reviewer { get; } = Guid.NewGuid().ToString("D");
        internal Guid MakerWorker { get; } = Guid.NewGuid();
        internal Guid RecipientWorker { get; } = Guid.NewGuid();
        internal Guid ReviewerWorker { get; } = Guid.NewGuid();
        internal Guid Company { get; } = Guid.NewGuid();
        internal Guid Branch { get; } = Guid.NewGuid();
        internal DateTimeOffset At { get; } = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        internal HrmDbContext Core { get; }
        private Fixture() => Core = new(new DbContextOptionsBuilder<HrmDbContext>()
            .UseInMemoryDatabase("authority-worker-core-" + database).Options, new FixedTenantAccessor("authority-worker-test"));
        internal ProcurementRequestScope Scope(string actor, Guid? worker)
        {
            var scope = new ProcurementRequestScope(); scope.Set("authority-worker-test", Company, actor, null, null, false, "authority-worker-test");
            if (worker.HasValue) scope.AssignWorker(worker.Value); return scope;
        }
        internal ProcurementDbContext Db(string actor, Guid? worker) => new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase("authority-worker-proc-" + database).Options, Scope(actor, worker));
        internal SpendingAuthorityConfigurationActor Actor(string actor, string permission)
            => new("authority-worker-test", Company, actor, true, new HashSet<string> { permission });
        internal SpendingAuthoritySubject Subject() => new("authority-worker-test", Company, Recipient, true, true);
        internal async Task<ProcurementSpendingAuthorityGrant> Grant(ProcurementDbContext db)
            => await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
        internal static async Task<Fixture> Start(bool legacy = false)
        {
            var f = new Fixture();
            f.Core.LegalEntities.Add(new LegalEntity { Id = f.Company, Code = "AUTH", RegisteredName = "Authority", Currency = "ZMW" });
            f.Core.WorkLocations.Add(new WorkLocation { Id = f.Branch, Code = "HOME", Name = "Home", LegalEntityId = f.Company });
            foreach (var (principal, worker, label) in new[] { (f.Maker, f.MakerWorker, "Maker"), (f.Recipient, f.RecipientWorker, "Recipient"), (f.Reviewer, f.ReviewerWorker, "Reviewer") })
            {
                f.Core.Workers.Add(new Worker { Id = worker, EmployeeNo = label, FirstName = label, LastName = "Person", Status = "active", LocationId = f.Branch });
                f.Core.LocalUsers.Add(new LocalUser { Id = Guid.Parse(principal), WorkerId = worker,
                    Email = label + "@example.test", NormalizedEmail = label.ToUpperInvariant() + "@EXAMPLE.TEST",
                    DisplayName = label, PasswordHash = "synthetic-not-a-credential", RolesCsv = "employee", IsActive = true });
            }
            await f.Core.SaveChangesAsync();
            await using var db = f.Db(f.Maker, f.MakerWorker);
            var grant = SpendingAuthorityConfiguration.CreateDraft("WORKER-TEST", f.Recipient,
                new("ZMW", "EstimatedLineBase", 100, f.At.AddDays(-1), f.At.AddDays(1), AllBranches: true, AllDepartments: true, AllCategories: true),
                f.Actor(f.Maker, SpendingAuthorityConfiguration.ConfigurePermission), f.At, []);
            db.Add(grant);
            if (!legacy) db.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureCreation(grant, f.MakerWorker, f.RecipientWorker)));
            await db.SaveChangesAsync(); return f;
        }
        internal async Task Submit()
        {
            await using var db = Db(Maker, MakerWorker); var grant = await Grant(db);
            var created = await ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(db, Scope(Maker, MakerWorker), grant, default);
            SpendingAuthorityConfiguration.Submit(grant, grant.Version, Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), At.AddMinutes(1));
            db.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureSubmission(grant, created, Maker, MakerWorker, RecipientWorker)));
            await db.SaveChangesAsync();
        }
        internal async Task Activate()
        {
            await using var db = Db(Reviewer, ReviewerWorker); var grant = await Grant(db);
            var proof = await ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(Core, db, Scope(Reviewer, ReviewerWorker), grant, default);
            SpendingAuthorityConfiguration.Activate(grant, grant.Version, Actor(Reviewer, SpendingAuthorityConfiguration.ReviewPermission), Subject(), "Independent review", At.AddMinutes(2), [grant]);
            db.Add(SpendingAuthorityIdentityStorage.ToRecord(SpendingAuthorityIdentity.CaptureActivation(grant, proof.Created, proof.Submitted, Reviewer, ReviewerWorker, RecipientWorker)));
            await db.SaveChangesAsync();
        }
        internal async Task SwapMakerReviewer()
        {
            (await Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(Maker))).WorkerId = ReviewerWorker;
            (await Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(Reviewer))).WorkerId = MakerWorker;
            await Core.SaveChangesAsync();
        }
        internal async Task OffboardMaker()
        {
            (await Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(Maker))).IsActive = false;
            (await Core.Workers.SingleAsync(x => x.Id == MakerWorker)).Status = "inactive";
            await Core.SaveChangesAsync();
        }
        internal async Task AddAlias(Guid worker)
        {
            Core.LocalUsers.Add(new LocalUser { WorkerId = worker, Email = "alias@example.test", NormalizedEmail = "ALIAS@EXAMPLE.TEST", DisplayName = "Alias", PasswordHash = "synthetic-not-a-credential", IsActive = true });
            await Core.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() => await Core.DisposeAsync();
    }
}
