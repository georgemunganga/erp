using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Owning-store/Core bridge tests only; InMemory does not prove PostgreSQL authorization/concurrency.</summary>
public sealed class ProcurementOperationalPolicyWorkerIdentityTests
{
    [Theory] [InlineData("RECEIVING")] [InlineData("INVOICE_MATCHING")]
    public async Task Original_author_and_independent_current_reviewer_use_actual_persisted_lineage(string key)
    {
        await using var f = await Fixture.Start(key);
        await using var db = f.Db(f.Maker, f.MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
        var author = await ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(f.Core, db,
            f.Scope(f.Maker, f.MakerWorker), policy, ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, default);
        Assert.Equal(f.MakerWorker, author.Created.ActorWorkerId);
        await f.Submit();
        await using var read = f.Db(f.Reviewer, f.ReviewerWorker); var pending = await read.PolicyVersions.SingleAsync();
        var proof = await ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(f.Core, read,
            f.Scope(f.Reviewer, f.ReviewerWorker), pending, ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, default);
        Assert.Equal(2, proof.Submitted.PolicyRecordVersionAtAction); Assert.Equal(f.ReviewerWorker, proof.Actor.WorkerId);
        Assert.False(read.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Original_person_cannot_review_after_maker_and_reviewer_accounts_swap_workers()
    {
        await using var f = await Fixture.Start(); await f.Submit();
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Maker))).WorkerId = f.ReviewerWorker;
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Reviewer))).WorkerId = f.MakerWorker;
        await f.Core.SaveChangesAsync();
        await using var db = f.Db(f.Reviewer, f.MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(
            f.Core, db, f.Scope(f.Reviewer, f.MakerWorker), policy, ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, default));
        Assert.Equal("OperationalPolicyIndependentReviewerRequired", error.Message);
        var authorError = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.RequireAuthorAsync(
            f.Core, db, f.Scope(f.Maker, f.ReviewerWorker), policy, ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, default));
        Assert.Equal("OperationalPolicyOriginalAuthorRequired", authorError.Message);
        Assert.Equal(2, await db.OperationalPolicyIdentityRecords.CountAsync()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Offboarded_historical_author_does_not_strand_independent_review_or_retained_publication()
    {
        await using var f = await Fixture.Start(); await f.Submit();
        (await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Maker))).IsActive = false;
        (await f.Core.Workers.SingleAsync(x => x.Id == f.MakerWorker)).Status = "inactive"; await f.Core.SaveChangesAsync();
        await f.Publish();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var policy = await db.PolicyVersions.SingleAsync();
        var count = await db.AuditEvents.CountAsync();
        var proof = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, f.Scope(f.Reviewer, f.ReviewerWorker), policy, default);
        Assert.Equal(f.MakerWorker, proof.Created.ActorWorkerId); Assert.Equal(f.ReviewerWorker, proof.Published.ActorWorkerId);
        Assert.Equal(count, await db.AuditEvents.CountAsync()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory] [InlineData("revoke-action")] [InlineData("alias")]
    [InlineData("stale-worker")] [InlineData("workerless-admin")]
    public async Task Current_actor_requires_stored_permission_unique_canonical_worker_and_unchanged_request_mapping(string fault)
    {
        await using var f = await Fixture.Start();
        var scope = f.Scope(f.Reviewer, f.ReviewerWorker);
        if (fault == "revoke-action")
        {
            (await f.Core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "policy_reviewer")).PermissionsCsv = "procurement-access";
            await f.Core.SaveChangesAsync();
        }
        if (fault == "alias")
        {
            f.Core.LocalUsers.Add(new LocalUser { WorkerId = f.ReviewerWorker, IsActive = true, Email = "alias@example.test",
                NormalizedEmail = "ALIAS@EXAMPLE.TEST", DisplayName = "Alias", PasswordHash = "synthetic-not-a-credential", RolesCsv = "policy_reviewer" });
            await f.Core.SaveChangesAsync();
        }
        if (fault == "stale-worker") scope = f.Scope(f.Reviewer, f.MakerWorker);
        if (fault == "workerless-admin")
        {
            var actor = await f.Core.LocalUsers.SingleAsync(x => x.Id == Guid.Parse(f.Reviewer));
            actor.WorkerId = null; actor.RolesCsv = "erp_admin"; await f.Core.SaveChangesAsync(); scope = f.Scope(f.Reviewer, null);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.CurrentActorAsync(
            f.Core, scope, ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, default));
    }

    [Fact]
    public async Task Read_or_edit_permission_cannot_be_substituted_for_independent_review_permission()
    {
        await using var f = await Fixture.Start(); await f.Submit();
        await using var db = f.Db(f.Maker, f.MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(
            f.Core, db, f.Scope(f.Maker, f.MakerWorker), policy, ProcurementOperationalPolicyWorkerIdentity.ConfigurePermission, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.CurrentActorAsync(
            f.Core, f.Scope(f.Reviewer, f.ReviewerWorker), "procurement-configuration-read", default));
    }

    [Fact]
    public async Task Attached_origin_cannot_supply_missing_historical_provenance()
    {
        await using var f = await Fixture.Start(); await using var db = f.Db(f.Maker, f.MakerWorker);
        var old = await db.PolicyVersions.AsNoTracking().SingleAsync();
        var unknown = new PolicyVersion { Id = Guid.NewGuid(), TenantId = old.TenantId, LegalEntityId = old.LegalEntityId,
            PolicyKey = old.PolicyKey, PolicyVersionNumber = 2, State = "Draft", Version = 1,
            CreatedAt = f.At, CreatedBy = f.Maker, RulesJson = "{\"legacy\":true}" };
        // A caller-attached, mathematically valid object is deliberately not retained in the owning store.
        db.Attach(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(
            unknown, "Created", f.Maker, f.MakerWorker, 1, f.At)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.LoadCreatedAsync(
            db, f.Scope(f.Maker, f.MakerWorker), unknown, default));
    }

    [Fact]
    public async Task Pending_revision_never_uses_an_old_submission_round()
    {
        await using var f = await Fixture.Start(); await f.Submit();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var policy = await db.PolicyVersions.AsNoTracking().SingleAsync();
        policy.Version++;
        Assert.Equal("OperationalPolicyHistoricalIdentityMissing", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(f.Core, db, f.Scope(f.Reviewer, f.ReviewerWorker),
                policy, ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, default))).Message);
    }

    [Theory] [InlineData("company")] [InlineData("tenant")] [InlineData("policy-key")] [InlineData("number")] [InlineData("rules")]
    public async Task Published_proof_is_bound_to_exact_owner_and_rules_not_a_foreign_or_mutated_projection(string fault)
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.Publish();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var policy = await db.PolicyVersions.AsNoTracking().SingleAsync();
        switch (fault)
        {
            case "company": policy.LegalEntityId = Guid.NewGuid(); break;
            case "tenant": policy.TenantId = "other"; break;
            case "policy-key": policy.PolicyKey = "INVOICE_MATCHING"; break;
            case "number": policy.PolicyVersionNumber++; break;
            default: policy.RulesJson = "{\"changed\":true}"; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(
            db, f.Scope(f.Reviewer, f.ReviewerWorker), policy, default));
    }

    [Fact]
    public async Task Historical_mode_accepts_retained_supersession_without_minting_current_eligibility()
    {
        await using var f = await Fixture.Start(); await f.Submit(); await f.Publish();
        await using var db = f.Db(f.Reviewer, f.ReviewerWorker); var policy = await db.PolicyVersions.AsNoTracking().SingleAsync();
        policy.State = "Superseded"; policy.Version++; policy.EffectiveTo = f.At.AddHours(-1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(
            db, f.Scope(f.Reviewer, f.ReviewerWorker), policy, default));
        var retained = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, f.Scope(f.Reviewer, f.ReviewerWorker), policy,
            default, OperationalPolicyPublicationMode.RetainedHistorical);
        Assert.Equal(f.ReviewerWorker, retained.Published.ActorWorkerId); Assert.False(db.ChangeTracker.HasChanges());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string database = Guid.NewGuid().ToString();
        internal HrmDbContext Core { get; }
        internal string Maker { get; } = Guid.NewGuid().ToString("D");
        internal string Reviewer { get; } = Guid.NewGuid().ToString("D");
        internal Guid MakerWorker { get; } = Guid.NewGuid();
        internal Guid ReviewerWorker { get; } = Guid.NewGuid();
        internal Guid Company { get; } = Guid.NewGuid();
        internal DateTimeOffset At { get; } = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        private Fixture() => Core = new(new DbContextOptionsBuilder<HrmDbContext>().UseInMemoryDatabase("op-policy-core-" + database).Options,
            new FixedTenantAccessor("op-policy-test"));
        internal ProcurementRequestScope Scope(string actor, Guid? worker)
        {
            var scope = new ProcurementRequestScope(); scope.Set("op-policy-test", Company, actor, null, null, false, "op-policy-test");
            if (worker.HasValue) scope.AssignWorker(worker.Value); return scope;
        }
        internal ProcurementDbContext Db(string actor, Guid? worker) => new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase("op-policy-proc-" + database).Options, Scope(actor, worker));
        internal static async Task<Fixture> Start(string key = "RECEIVING")
        {
            var f = new Fixture(); var branch = new WorkLocation { Code = "HOME", Name = "Home", LegalEntityId = f.Company };
            f.Core.LegalEntities.Add(new LegalEntity { Id = f.Company, Code = "OP", RegisteredName = "Operational policies", Currency = "ZMW" });
            f.Core.WorkLocations.Add(branch);
            foreach (var (principal, worker, role) in new[] { (f.Maker, f.MakerWorker, "policy_editor"), (f.Reviewer, f.ReviewerWorker, "policy_reviewer") })
            {
                f.Core.Workers.Add(new Worker { Id = worker, EmployeeNo = role, FirstName = role, LastName = "Person", LocationId = branch.Id, Status = "active" });
                f.Core.LocalUsers.Add(new LocalUser { Id = Guid.Parse(principal), WorkerId = worker, RolesCsv = role, IsActive = true,
                    Email = role + "@example.test", NormalizedEmail = role.ToUpperInvariant() + "@EXAMPLE.TEST", DisplayName = role, PasswordHash = "synthetic-not-a-credential" });
            }
            f.Core.TenantRoleAssignments.AddRange(
                new TenantRoleAssignment { RoleKey = "policy_editor", RoleName = "Policy editor", Active = true, PermissionsCsv = "procurement-access,procurement-configuration-edit" },
                new TenantRoleAssignment { RoleKey = "policy_reviewer", RoleName = "Policy reviewer", Active = true, PermissionsCsv = "procurement-access,procurement-configuration-approve" });
            await f.Core.SaveChangesAsync();
            await using var db = f.Db(f.Maker, f.MakerWorker);
            var policy = new PolicyVersion { TenantId = "op-policy-test", LegalEntityId = f.Company, PolicyKey = key,
                PolicyVersionNumber = 1, State = "Draft", Version = 1, CreatedAt = f.At, CreatedBy = f.Maker,
                RulesJson = "{\"syntheticPersistenceOnly\":true}", EffectiveFrom = f.At.AddDays(1) };
            db.Add(policy); db.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy,
                "Created", f.Maker, f.MakerWorker, 1, f.At))); await db.SaveChangesAsync(); return f;
        }
        internal async Task Submit()
        {
            await using var db = Db(Maker, MakerWorker); var policy = await db.PolicyVersions.SingleAsync();
            var created = await ProcurementOperationalPolicyWorkerIdentity.LoadCreatedAsync(db, Scope(Maker, MakerWorker), policy, default);
            policy.State = "PendingReview"; policy.ReviewRequestedBy = Maker; policy.ReviewRequestedAt = At.AddMinutes(1); policy.ReviewReason = "Review";
            db.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Submitted", Maker,
                MakerWorker, policy.Version + 1, policy.ReviewRequestedAt.Value, created))); await db.SaveChangesAsync();
        }
        internal async Task Publish()
        {
            await using var db = Db(Reviewer, ReviewerWorker); var policy = await db.PolicyVersions.SingleAsync();
            var proof = await ProcurementOperationalPolicyWorkerIdentity.RequireReviewerAsync(Core, db, Scope(Reviewer, ReviewerWorker), policy,
                ProcurementOperationalPolicyWorkerIdentity.ReviewPermission, default);
            policy.State = "Published"; policy.PublishedBy = Reviewer; policy.PublishedAt = At.AddMinutes(2); policy.PublicationReason = "Reviewed";
            db.Add(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Published", Reviewer,
                ReviewerWorker, policy.Version + 1, policy.PublishedAt.Value, proof.Created, proof.Submitted))); await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() => await Core.DisposeAsync();
    }
}
