using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class SpendingAuthorityIdentityTests
{
    private const string Maker = "11111111-1111-4111-8111-111111111111";
    private const string Recipient = "22222222-2222-4222-8222-222222222222";
    private const string Reviewer = "33333333-3333-4333-8333-333333333333";
    private static readonly Guid MakerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid RecipientWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid ReviewerWorker = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid Company = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero).AddTicks(7);

    [Fact]
    public void Creation_submission_and_activation_retain_the_original_people_without_changing_business_fingerprints()
    {
        var (grant, created, submitted) = Pending();
        var fingerprint = SpendingAuthorityConfiguration.Fingerprint(grant);
        SpendingAuthorityIdentity.RequireIndependentReviewer(grant, created, submitted, Reviewer, ReviewerWorker);
        Activate(grant);
        var activated = SpendingAuthorityIdentity.CaptureActivation(grant, created, submitted,
            Reviewer, ReviewerWorker, RecipientWorker);
        SpendingAuthorityIdentity.VerifyActivation(grant, created, submitted, activated);
        Assert.Equal(fingerprint, SpendingAuthorityConfiguration.Fingerprint(grant));
        Assert.Equal(fingerprint, grant.PublishedBusinessFingerprint);
        Assert.Equal(MakerWorker, created.ActorWorkerId);
        Assert.Equal(RecipientWorker, activated.RecipientWorkerId);
        Assert.Equal(ReviewerWorker, activated.ActorWorkerId);
        Assert.Equal(created.Id, activated.CreatedEvidenceId);
        Assert.Equal(submitted.Id, activated.SubmittedEvidenceId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_different_reviewer_login_cannot_represent_the_original_maker_or_recipient(bool maker)
    {
        var (grant, created, submitted) = Pending();
        var version = grant.Version;
        var fingerprint = grant.SubmittedBusinessFingerprint;
        Assert.Equal("AuthorityIndependentWorkerRequired", Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireIndependentReviewer(grant, created, submitted, Reviewer,
                maker ? MakerWorker : RecipientWorker)).Message);
        Assert.Equal("PendingReview", grant.State);
        Assert.Equal(version, grant.Version);
        Assert.Equal(fingerprint, grant.SubmittedBusinessFingerprint);
    }

    [Theory]
    [InlineData(Maker)]
    [InlineData(Recipient)]
    public void Original_participant_accounts_cannot_review_even_when_rebound_to_another_worker(string principal)
    {
        var (grant, created, submitted) = Pending();
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.RequireIndependentReviewer(
            grant, created, submitted, principal, ReviewerWorker));
    }

    [Fact]
    public void Rebinding_the_recipient_does_not_transfer_authority_to_the_new_employee()
    {
        var (grant, created, _) = Pending();
        SpendingAuthorityIdentity.RequireCurrentRecipient(grant, created, Recipient, RecipientWorker);
        Assert.Equal("AuthorityRecipientIdentityChanged", Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireCurrentRecipient(grant, created, Recipient, ReviewerWorker)).Message);
    }

    [Fact]
    public void Rebinding_the_maker_does_not_transfer_authorship()
    {
        var (grant, created, _) = Pending();
        SpendingAuthorityIdentity.RequireAuthor(grant, created, Maker, MakerWorker);
        Assert.Equal("AuthorityOriginalAuthorRequired", Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireAuthor(grant, created, Maker, ReviewerWorker)).Message);
    }

    [Fact]
    public void Historical_maker_evidence_does_not_require_an_active_historical_account()
    {
        var (grant, created, submitted) = Pending();
        // Only the current reviewer is an input. An offboarded maker is not resolved or reactivated.
        SpendingAuthorityIdentity.RequireIndependentReviewer(grant, created, submitted, Reviewer, ReviewerWorker);
        Assert.Equal(MakerWorker, created.ActorWorkerId);
    }

    [Fact]
    public void Missing_legacy_origin_is_rejected_instead_of_reconstructed_from_current_workers()
    {
        var (grant, _, _) = Pending();
        Assert.Equal("AuthorityHistoricalIdentityMissing", Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireAuthor(grant, null, Maker, MakerWorker)).Message);
        Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireCurrentRecipient(grant, null, Recipient, RecipientWorker));
    }

    [Fact]
    public void Tampered_worker_evidence_and_cross_company_or_grant_reuse_are_rejected()
    {
        var (grant, created, _) = Pending();
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.VerifyOrigin(grant,
            created with { ActorWorkerId = ReviewerWorker }));
        foreach (var row in new[] { created with { LegalEntityId = Guid.NewGuid() }, created with { GrantId = Guid.NewGuid() },
            created with { Permission = "another-permission" }, created with { RecipientPrincipalId = Reviewer } })
        {
            // Even a mathematically valid digest cannot change the owning record boundary.
            var rehashed = row with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(row) };
            Assert.Equal("AuthorityIdentityEvidenceScopeMismatch", Assert.Throws<InvalidOperationException>(() =>
                SpendingAuthorityIdentity.VerifyOrigin(grant, rehashed)).Message);
        }
    }

    [Fact]
    public void Changed_financial_values_and_stale_submissions_cannot_reuse_review_evidence()
    {
        var (grant, created, submitted) = Pending();
        grant.MaximumAmount += 1;
        Assert.Throws<InvalidOperationException>(() =>
            SpendingAuthorityIdentity.RequireIndependentReviewer(grant, created, submitted, Reviewer, ReviewerWorker));
        grant.MaximumAmount -= 1;
        grant.Version += 1;
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.VerifySubmission(grant, created, submitted));
    }

    [Fact]
    public void Activation_requires_the_exact_submission_lineage_actor_version_and_time()
    {
        var (grant, created, submitted) = Pending();
        Activate(grant);
        var activated = SpendingAuthorityIdentity.CaptureActivation(grant, created, submitted,
            Reviewer, ReviewerWorker, RecipientWorker);
        foreach (var row in new[] { activated with { SubmittedEvidenceId = Guid.NewGuid() },
            activated with { GrantRecordVersionAtAction = grant.Version + 1 },
            activated with { ActorPrincipalId = Maker }, activated with { ActionAt = Now.AddHours(1) } })
        {
            var rehashed = row with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(row) };
            Assert.Throws<InvalidOperationException>(() =>
                SpendingAuthorityIdentity.VerifyActivation(grant, created, submitted, rehashed));
        }
    }

    [Fact]
    public void Resubmission_cannot_reuse_the_previous_round_identity()
    {
        var (grant, created, submitted) = Pending();
        SpendingAuthorityConfiguration.ReturnForChanges(grant, grant.Version, Actor(Reviewer,
            SpendingAuthorityConfiguration.ReviewPermission), "Please revise", Now.AddMinutes(2));
        SpendingAuthorityConfiguration.Submit(grant, grant.Version, Actor(Maker,
            SpendingAuthorityConfiguration.ConfigurePermission), Subject(), Now.AddMinutes(3));
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.VerifySubmission(grant, created, submitted));
        var next = SpendingAuthorityIdentity.CaptureSubmission(grant, created, Maker, MakerWorker, RecipientWorker);
        SpendingAuthorityIdentity.VerifySubmission(grant, created, next);
        Assert.NotEqual(submitted.Id, next.Id);
    }

    [Fact]
    public void Capture_normalizes_database_time_and_rejects_noncanonical_identity_or_empty_workers()
    {
        var (grant, created, _) = Pending();
        Assert.Equal(0, created.ActionAt.Ticks % 10);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.RequireAuthor(
            grant, created, Maker.ToUpperInvariant(), Guid.Empty));
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.RequireCurrentRecipient(
            grant, created, Recipient, Guid.Empty));
        var malformed = created with { ActorPrincipalId = "not-an-account" };
        malformed = malformed with { EvidenceSha256 = SpendingAuthorityIdentity.Digest(malformed) };
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityIdentity.Verify(malformed));
    }

    private static (ProcurementSpendingAuthorityGrant, SpendingAuthorityIdentityEvidence,
        SpendingAuthorityIdentityEvidence) Pending()
    {
        var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH-IDENTITY", Recipient,
            new("ZMW", "EstimatedLineBase", 100, Now.AddDays(-1), Now.AddDays(1),
                AllBranches: true, AllDepartments: true, AllCategories: true),
            Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Now, []);
        var created = SpendingAuthorityIdentity.CaptureCreation(grant, MakerWorker, RecipientWorker);
        SpendingAuthorityConfiguration.Submit(grant, grant.Version,
            Actor(Maker, SpendingAuthorityConfiguration.ConfigurePermission), Subject(), Now.AddMinutes(1));
        var submitted = SpendingAuthorityIdentity.CaptureSubmission(grant, created, Maker, MakerWorker, RecipientWorker);
        return (grant, created, submitted);
    }

    private static SpendingAuthorityConfigurationActor Actor(string principal, string permission)
        => new("tenant", Company, principal, true, new HashSet<string> { permission });
    private static SpendingAuthoritySubject Subject() => new("tenant", Company, Recipient, true, true);
    private static void Activate(ProcurementSpendingAuthorityGrant grant)
        => SpendingAuthorityConfiguration.Activate(grant, grant.Version,
            Actor(Reviewer, SpendingAuthorityConfiguration.ReviewPermission), Subject(),
            "Independent review", Now.AddMinutes(2), [grant]);
}
