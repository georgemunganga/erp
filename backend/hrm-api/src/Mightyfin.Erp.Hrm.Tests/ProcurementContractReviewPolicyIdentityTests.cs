using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped same-save controls. Actual PostgreSQL ownership remains a later gate.</summary>
public sealed class ProcurementContractReviewPolicyIdentityTests
{
    [Fact]
    public async Task Workerless_Core_author_and_independent_publisher_are_retained_with_original_policy()
    {
        var database = "contract-policy-" + Guid.NewGuid();
        var tenant = "contract-policy-test";
        var company = Guid.NewGuid();
        var author = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        var rules = $$"""{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[{"code":"LEGAL","mode":"All","principalIds":["{{reviewer:D}}"]}]}""";
        Guid policyId;
        Guid createdId;
        await using (var db = Db(database, Scope(tenant, company, author)))
        {
            var policy = new PolicyVersion { PolicyKey = ProcurementContractReviewPolicyRules.PolicyKey,
                PolicyVersionNumber = 1, State = "Draft", RulesJson = rules,
                EffectiveFrom = now.AddDays(1), CreatedAt = now, CreatedBy = author.ToString("D") };
            db.AddScoped(policy);
            var origin = OperationalPolicyWorkerIdentity.Capture(policy, "Created", author.ToString("D"),
                null, 1, now, schemaVersion: 2);
            db.AddScoped(ContractReviewPolicyIdentityStorage.ToRecord(origin));
            await db.SaveChangesAsync();
            policyId = policy.Id;
            createdId = origin.Id;
        }
        await using (var db = Db(database, Scope(tenant, company, author)))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            var created = ContractReviewPolicyIdentityStorage.Read(await db.ContractReviewPolicyIdentityRecords
                .AsNoTracking().SingleAsync(x => x.Id == createdId));
            policy.State = "PendingReview";
            policy.ReviewRequestedBy = author.ToString("D");
            policy.ReviewRequestedAt = now.AddMinutes(1);
            policy.ReviewReason = "Ready for independent review";
            var submitted = OperationalPolicyWorkerIdentity.Capture(policy, "Submitted", author.ToString("D"),
                null, 2, policy.ReviewRequestedAt.Value, created, schemaVersion: 2);
            db.AddScoped(ContractReviewPolicyIdentityStorage.ToRecord(submitted));
            await db.SaveChangesAsync();
        }
        await using (var db = Db(database, Scope(tenant, company, reviewer)))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            var rows = await db.ContractReviewPolicyIdentityRecords.AsNoTracking().ToArrayAsync();
            var created = ContractReviewPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Created"));
            var submitted = ContractReviewPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted"));
            policy.State = "Published";
            policy.PublishedBy = reviewer.ToString("D");
            policy.PublishedAt = now.AddMinutes(2);
            policy.PublicationReason = "Reviewed terms routing";
            var published = OperationalPolicyWorkerIdentity.Capture(policy, "Published", reviewer.ToString("D"),
                null, 3, policy.PublishedAt.Value, created, submitted, schemaVersion: 2);
            db.AddScoped(ContractReviewPolicyIdentityStorage.ToRecord(published));
            await db.SaveChangesAsync();
        }
        await using (var db = Db(database, Scope(tenant, company, reviewer)))
        {
            var policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == policyId);
            Assert.Equal("Published", policy.State);
            Assert.Equal(3, await db.ContractReviewPolicyIdentityRecords.CountAsync());
            Assert.Equal(3, policy.Version);
        }
    }

    [Fact]
    public async Task Unowned_policy_creation_and_foreign_actor_draft_edit_are_denied_before_save()
    {
        var database = "contract-policy-deny-" + Guid.NewGuid();
        var tenant = "contract-policy-test";
        var company = Guid.NewGuid();
        var author = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        await using (var db = Db(database, Scope(tenant, company, author)))
        {
            db.AddScoped(new PolicyVersion { PolicyKey = ProcurementContractReviewPolicyRules.PolicyKey,
                PolicyVersionNumber = 1, State = "Draft", RulesJson = EmptyDraft, EffectiveFrom = now.AddDays(1) });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        Guid policyId;
        await using (var db = Db(database, Scope(tenant, company, author)))
        {
            var policy = new PolicyVersion { PolicyKey = ProcurementContractReviewPolicyRules.PolicyKey,
                PolicyVersionNumber = 1, State = "Draft", RulesJson = EmptyDraft, EffectiveFrom = now.AddDays(1),
                CreatedAt = now, CreatedBy = author.ToString("D") };
            db.AddScoped(policy);
            db.AddScoped(ContractReviewPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(
                policy, "Created", author.ToString("D"), null, 1, now, schemaVersion: 2)));
            await db.SaveChangesAsync();
            policyId = policy.Id;
        }
        await using (var db = Db(database, Scope(tenant, company, other)))
        {
            var policy = await db.PolicyVersions.SingleAsync(x => x.Id == policyId);
            policy.RulesJson = """{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[{"code":"LEGAL","mode":"All","principalIds":[]}]}""";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }

    private static ProcurementRequestScope Scope(string tenant, Guid company, Guid actor)
    {
        var scope = new ProcurementRequestScope();
        scope.Set(tenant, company, actor.ToString("D"), null, null, false, "contract-policy-test");
        scope.AssignPrincipal(actor); // Explicit workerless Core account in this mapped fixture.
        return scope;
    }

    private static ProcurementDbContext Db(string database, ProcurementRequestScope scope) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options, scope);

    private const string EmptyDraft = """{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[]}""";
}
