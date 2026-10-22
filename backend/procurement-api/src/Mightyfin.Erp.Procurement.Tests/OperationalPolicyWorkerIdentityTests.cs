using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure retained-identity contract only; no persisted provenance or current Core authorization is claimed.</summary>
public sealed class OperationalPolicyWorkerIdentityTests
{
    private const string Maker = "11111111-1111-4111-8111-111111111111";
    private const string Reviewer = "22222222-2222-4222-8222-222222222222";
    private static readonly Guid MakerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ReviewerWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    [Theory] [InlineData("RECEIVING")] [InlineData("INVOICE_MATCHING")] [InlineData("INVOICE_UNIT_REVIEW")]
    public void Exact_original_author_submission_and_independent_publication_are_verified(string key)
    {
        var f = new Fixture(key); f.Submit();
        OperationalPolicyWorkerIdentity.RequireAuthor(f.Policy, f.Created, Maker, MakerWorker);
        OperationalPolicyWorkerIdentity.RequireIndependent(f.Policy, f.Created, f.Submitted!, Reviewer, ReviewerWorker);
        f.Publish();
        OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, f.Published!);
        Assert.Equal(key, f.Published!.PolicyKey);
        var roundtrip = JsonSerializer.Deserialize<OperationalPolicyWorkerIdentityEvidence>(JsonSerializer.Serialize(f.Published))!;
        Assert.Equal(f.Published, roundtrip); OperationalPolicyWorkerIdentity.Verify(roundtrip);
    }

    [Theory] [InlineData("SOURCING")] [InlineData("PURCHASE_REQUEST")] [InlineData("PURCHASE_ORDER")]
    [InlineData("receiving")] [InlineData("")]
    public void Contract_never_adopts_other_policy_types(string key)
        => Assert.Throws<InvalidOperationException>(() => new Fixture(key));

    [Fact]
    public void Policy_key_is_part_of_digest_and_exact_owned_boundary()
    {
        var f = new Fixture();
        var changed = f.Created with { PolicyKey = "INVOICE_MATCHING" };
        Assert.NotEqual(f.Created.EvidenceSha256, OperationalPolicyWorkerIdentity.Digest(changed));
        changed = Rehash(changed); OperationalPolicyWorkerIdentity.Verify(changed);
        Assert.Equal("OperationalPolicyIdentityScopeMismatch", Assert.Throws<InvalidOperationException>(() =>
            OperationalPolicyWorkerIdentity.VerifyOrigin(f.Policy, changed)).Message);
    }

    [Theory] [InlineData("principal")] [InlineData("worker")]
    public void Author_principal_and_original_worker_are_both_required(string changed)
    {
        var f = new Fixture();
        Assert.Equal("OperationalPolicyOriginalAuthorRequired", Assert.Throws<InvalidOperationException>(() =>
            OperationalPolicyWorkerIdentity.RequireAuthor(f.Policy, f.Created,
                changed == "principal" ? Reviewer : Maker, changed == "worker" ? ReviewerWorker : MakerWorker)).Message);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void Review_cannot_be_performed_by_rebound_original_account_or_alias_of_original_person(bool changedAccount)
    {
        var f = new Fixture(); f.Submit();
        Assert.Equal("OperationalPolicyIndependentReviewerRequired", Assert.Throws<InvalidOperationException>(() =>
            OperationalPolicyWorkerIdentity.RequireIndependent(f.Policy, f.Created, f.Submitted!,
                changedAccount ? Maker : Reviewer, changedAccount ? ReviewerWorker : MakerWorker)).Message);
    }

    [Fact]
    public void Independent_reviewer_needs_no_current_lookup_of_offboarded_maker()
    {
        var f = new Fixture(); f.Submit();
        // Current Core's author/worker records are deliberately not inputs to retained verification.
        OperationalPolicyWorkerIdentity.RequireIndependent(f.Policy, f.Created, f.Submitted!, Reviewer, ReviewerWorker);
        Assert.Equal(MakerWorker, f.Created.ActorWorkerId);
    }

    [Theory] [InlineData("scope")] [InlineData("owner")] [InlineData("number")]
    public void A_valid_hash_cannot_reassign_retained_identity_to_another_policy_owner(string fault)
    {
        var f = new Fixture();
        var changed = fault switch
        {
            "scope" => f.Created with { LegalEntityId = Guid.NewGuid() },
            "owner" => f.Created with { PolicyVersionId = Guid.NewGuid() },
            _ => f.Created with { PolicyVersionNumber = 2 },
        };
        changed = Rehash(changed);
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifyOrigin(f.Policy, changed));
    }

    [Fact]
    public void Legacy_origin_is_never_inferred_from_current_policy_author()
    {
        var f = new Fixture();
        Assert.Equal("OperationalPolicyHistoricalIdentityMissing", Assert.Throws<InvalidOperationException>(() =>
            OperationalPolicyWorkerIdentity.RequireAuthor(f.Policy, null, Maker, MakerWorker)).Message);
    }

    [Fact]
    public void Creation_requires_the_real_normalized_initial_author_and_time()
    {
        var f = new Fixture();
        f.Policy.CreatedBy = Reviewer;
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.Capture(f.Policy, "Created", Maker, MakerWorker, 1, At));
        f.Policy.CreatedBy = Maker; f.Policy.CreatedAt = At.AddTicks(1);
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.Capture(f.Policy, "Created", Maker, MakerWorker, 1, At));
        f.Policy.CreatedAt = At; f.Policy.Version = 2;
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.Capture(f.Policy, "Created", Maker, MakerWorker, 1, At));
    }

    [Theory] [InlineData("rules")] [InlineData("round")] [InlineData("time")] [InlineData("lineage")]
    public void Submission_requires_exact_current_rules_round_timestamp_and_origin(string fault)
    {
        var f = new Fixture(); f.Submit();
        var submitted = f.Submitted!;
        if (fault == "rules") f.Policy.RulesJson = "{\"changed\":true}";
        if (fault == "round") f.Policy.Version++;
        if (fault == "time") f.Policy.ReviewRequestedAt = At.AddMinutes(2);
        if (fault == "lineage") submitted = Rehash(submitted with { CreatedEvidenceId = Guid.NewGuid() });
        Assert.Equal("OperationalPolicySubmittedIdentityMismatch", Assert.Throws<InvalidOperationException>(() =>
            OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, submitted)).Message);
    }

    [Fact]
    public void Edited_drafts_and_equal_microsecond_chronology_are_supported_without_rewriting_origin()
    {
        var f = new Fixture(); var originalHash = f.Created.EvidenceSha256;
        f.Policy.RulesJson = "{\"legitimateOriginalAuthorEdit\":true}"; f.Policy.Version = 4;
        f.Submit(At); f.Publish(At);
        OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, f.Published!);
        Assert.NotEqual(f.Created.RulesSha256, f.Published!.RulesSha256);
        Assert.Equal(originalHash, f.Created.EvidenceSha256);
    }

    [Theory] [InlineData("submission")] [InlineData("publication")]
    public void Otherwise_valid_hashes_do_not_allow_backdated_actions(string fault)
    {
        var f = new Fixture(); f.Submit();
        if (fault == "submission")
        {
            f.Policy.ReviewRequestedAt = At.AddTicks(-10);
            var changed = Rehash(f.Submitted! with { ActionAt = f.Policy.ReviewRequestedAt.Value });
            Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, changed));
        }
        else
        {
            f.Publish(); f.Policy.PublishedAt = At;
            var changed = Rehash(f.Published! with { ActionAt = At });
            Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, changed));
        }
    }

    [Fact]
    public void Returned_draft_resubmission_uses_new_exact_revision_and_keeps_old_history()
    {
        var f = new Fixture(); f.Submit(); var old = f.Submitted!;
        f.Policy.State = "Draft"; f.Policy.Version++;
        f.Submit(At.AddMinutes(3));
        OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, f.Submitted!);
        Assert.NotEqual(old.Id, f.Submitted!.Id);
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, old));
        OperationalPolicyWorkerIdentity.Verify(old); // Still valid history, not the current submission round.
    }

    [Theory] [InlineData("Published")] [InlineData("Superseded")]
    public void Later_supersession_revision_and_closed_period_do_not_invalidate_retained_publication(string state)
    {
        var f = new Fixture(); f.Submit(); f.Publish();
        var original = f.Published!.EvidenceSha256;
        f.Policy.State = state; f.Policy.Version += 2; f.Policy.EffectiveTo = At.AddDays(-1);
        f.Policy.UpdatedAt = At.AddDays(1); f.Policy.UpdatedBy = Reviewer;
        OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, f.Published,
            OperationalPolicyPublicationMode.RetainedHistorical);
        Assert.Equal(original, f.Published.EvidenceSha256);
        if (state == "Superseded")
            Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, f.Published));
        // No current effective-time authorization is inferred from either proof mode.
    }

    [Theory] [InlineData("Draft")] [InlineData("PendingReview")] [InlineData("Unknown")]
    public void Historical_mode_is_not_a_blanket_bypass_for_invalid_current_lifecycle(string state)
    {
        var f = new Fixture(); f.Submit(); f.Publish(); f.Policy.State = state;
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, f.Published!,
            OperationalPolicyPublicationMode.RetainedHistorical));
    }

    [Theory] [InlineData("reviewer-worker")] [InlineData("publication-version")] [InlineData("submitted-id")]
    public void Publication_requires_independent_person_and_exact_completion_lineage(string fault)
    {
        var f = new Fixture(); f.Submit(); f.Publish();
        var changed = fault switch
        {
            "reviewer-worker" => f.Published! with { ActorWorkerId = MakerWorker },
            "publication-version" => f.Published! with { PolicyRecordVersionAtAction = f.Policy.Version + 1 },
            _ => f.Published! with { SubmittedEvidenceId = Guid.NewGuid() },
        };
        changed = Rehash(changed);
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifyPublication(f.Policy, f.Created, f.Submitted!, changed));
    }

    [Fact]
    public void Intended_revision_is_explicit_for_same_save_validation_not_guessed_from_current_version()
    {
        var f = new Fixture(); f.Submit(); var intended = f.Policy.Version;
        f.Policy.Version--;
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, f.Submitted!));
        OperationalPolicyWorkerIdentity.VerifySubmission(f.Policy, f.Created, f.Submitted!, intended);
    }

    [Theory] [InlineData("worker")] [InlineData("stage")] [InlineData("schema")] [InlineData("submicrosecond")]
    public void Malformed_even_rehashed_envelopes_are_rejected(string fault)
    {
        var f = new Fixture();
        var changed = fault switch
        {
            "worker" => f.Created with { ActorWorkerId = Guid.Empty },
            "stage" => f.Created with { Stage = "Reviewed" },
            "schema" => f.Created with { SchemaVersion = 2 },
            _ => f.Created with { ActionAt = At.AddTicks(1) },
        };
        changed = Rehash(changed);
        Assert.Throws<InvalidOperationException>(() => OperationalPolicyWorkerIdentity.Verify(changed));
    }

    private static OperationalPolicyWorkerIdentityEvidence Rehash(OperationalPolicyWorkerIdentityEvidence row)
        => row with { EvidenceSha256 = OperationalPolicyWorkerIdentity.Digest(row) };
    private sealed class Fixture
    {
        internal PolicyVersion Policy { get; }
        internal OperationalPolicyWorkerIdentityEvidence Created { get; }
        internal OperationalPolicyWorkerIdentityEvidence? Submitted { get; private set; }
        internal OperationalPolicyWorkerIdentityEvidence? Published { get; private set; }
        internal Fixture(string key = "RECEIVING")
        {
            Policy = new() { TenantId = "tenant", LegalEntityId = Guid.NewGuid(), PolicyKey = key,
                PolicyVersionNumber = 1, RulesJson = "{\"schemaVersion\":1}", CreatedBy = Maker, CreatedAt = At,
                EffectiveFrom = At.AddDays(-2), State = "Draft", Version = 1 };
            Created = OperationalPolicyWorkerIdentity.Capture(Policy, "Created", Maker, MakerWorker, 1, At);
        }
        internal void Submit(DateTimeOffset? at = null)
        {
            Policy.State = "PendingReview"; Policy.Version++;
            Policy.ReviewRequestedBy = Maker; Policy.ReviewRequestedAt = at ?? At.AddMinutes(1);
            Submitted = OperationalPolicyWorkerIdentity.Capture(Policy, "Submitted", Maker, MakerWorker,
                Policy.Version, Policy.ReviewRequestedAt.Value, Created);
        }
        internal void Publish(DateTimeOffset? at = null)
        {
            Policy.State = "Published"; Policy.Version++;
            Policy.PublishedBy = Reviewer; Policy.PublishedAt = at ?? At.AddMinutes(2);
            Published = OperationalPolicyWorkerIdentity.Capture(Policy, "Published", Reviewer, ReviewerWorker,
                Policy.Version, Policy.PublishedAt.Value, Created, Submitted);
        }
    }
}
