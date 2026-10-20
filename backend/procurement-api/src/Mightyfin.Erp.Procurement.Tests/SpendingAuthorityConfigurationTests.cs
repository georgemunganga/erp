using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SpendingAuthorityConfigurationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static SpendingAuthorityConfigurationActor Owner => Actor("owner", SpendingAuthorityConfiguration.ConfigurePermission);
    private static SpendingAuthorityConfigurationActor Reviewer => Actor("reviewer", SpendingAuthorityConfiguration.ReviewPermission);
    private static SpendingAuthoritySubject Subject => new("tenant", Company, "subject", true, true);
    private static SpendingAuthorityDraft Complete => new("ZMW", "EstimatedLineBase", 100,
        Now.AddDays(-1), Now.AddDays(1), BranchRef: Branch, DepartmentRef: Department, CategoryCodes: ["IT"]);

    [Fact]
    public void Empty_draft_can_be_created_and_edited_but_has_no_implicit_financial_authority()
    {
        var draft = Create(new());
        Assert.Equal("Draft", draft.State);
        Assert.Equal(0, draft.MaximumAmount);
        Assert.Equal("owner", draft.ConfigurationOwnerPrincipalId);
        Assert.Equal("owner", draft.CreatedBy);
        Assert.Equal(1, draft.AuthorityVersion);
        Assert.Empty(draft.Categories);
        Assert.Throws<InvalidOperationException>(() => Submit(draft));
        SpendingAuthorityConfiguration.EditDraft(draft, draft.Version, Complete, Owner, Now);
        Assert.Equal(2, draft.Version);
        Submit(draft);
        Assert.Equal("PendingReview", draft.State);
        Assert.Null(draft.PublishedBusinessFingerprint);
        Activate(draft, [draft]);
        Assert.Equal("Active", draft.State);
        Assert.Equal("reviewer", draft.ActivatedByPrincipalId);
        Assert.Equal("Checked the declared scope", draft.ActivationReason);
        Assert.NotNull(draft.PublishedBusinessFingerprint);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("subject")]
    public void Owner_and_authority_subject_cannot_activate_even_with_review_permission(string principal)
    {
        var grant = Create(Complete);
        Submit(grant);
        var actor = Actor(principal, SpendingAuthorityConfiguration.ReviewPermission);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Activate(grant,
            grant.Version, actor, Subject, "Reviewed", Now, [grant]));
        Assert.Equal("PendingReview", grant.State);
    }

    [Fact]
    public void Draft_edit_requires_owner_scope_current_revision_and_configure_permission()
    {
        var draft = Create(Complete);
        foreach (var actor in new[] { Actor("other", SpendingAuthorityConfiguration.ConfigurePermission),
            Owner with { TenantId = "foreign" }, Owner with { LegalEntityId = Guid.NewGuid() },
            Owner with { IsActive = false }, Actor("owner") })
            Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.EditDraft(
                draft, draft.Version, Complete, actor, Now));
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.EditDraft(
            draft, draft.Version + 1, Complete, Owner, Now));
        Submit(draft);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.EditDraft(
            draft, draft.Version, Complete, Owner, Now));
    }

    [Fact]
    public void Inactive_foreign_or_unmapped_subject_blocks_submission_and_activation()
    {
        foreach (var invalid in new[] { Subject with { IsActive = false },
            Subject with { IsCompanyMember = false }, Subject with { PrincipalId = "worker-not-principal" },
            Subject with { TenantId = "foreign" }, Subject with { LegalEntityId = Guid.NewGuid() } })
        {
            var draft = Create(Complete);
            Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Submit(
                draft, draft.Version, Owner, invalid, Now));
            Submit(draft);
            Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Activate(
                draft, draft.Version, Reviewer, invalid, "Reviewed", Now, [draft]));
        }
    }

    [Fact]
    public void Invalid_financial_terms_or_undeclared_scope_never_enter_review()
    {
        foreach (var input in new[] { Complete with { MaximumAmount = 0 },
            Complete with { AmountBasis = "TaxInclusive" }, Complete with { CurrencyCode = "" },
            Complete with { EffectiveUntil = Now }, Complete with { EffectiveUntil = Now.AddDays(-2) },
            Complete with { BranchRef = null }, Complete with { DepartmentRef = null },
            Complete with { CategoryCodes = [] }, Complete with { AllBranches = true },
            Complete with { AllDepartments = true }, Complete with { AllCategories = true } })
        {
            var draft = Create(input);
            Assert.Throws<InvalidOperationException>(() => Submit(draft));
            Assert.Equal("Draft", draft.State);
        }
    }

    [Fact]
    public void Explicit_all_scopes_are_supported_without_sample_limits()
    {
        var draft = Create(Complete with { AllBranches = true, BranchRef = null,
            AllDepartments = true, DepartmentRef = null, AllCategories = true, CategoryCodes = [] });
        Submit(draft);
        Activate(draft, [draft]);
        Assert.True(draft.AllCategories);
        Assert.Equal(100, draft.MaximumAmount);
    }

    [Fact]
    public void Review_return_retains_reason_then_requires_new_submission_after_edit()
    {
        var draft = Create(Complete);
        Submit(draft);
        SpendingAuthorityConfiguration.ReturnForChanges(draft, draft.Version, Reviewer,
            "Confirm the department", Now);
        Assert.Equal("Draft", draft.State);
        Assert.Equal("Confirm the department", draft.ReviewReturnReason);
        Assert.Null(draft.SubmittedBusinessFingerprint);
        SpendingAuthorityConfiguration.EditDraft(draft, draft.Version, Complete with { MaximumAmount = 80 }, Owner, Now);
        Assert.Throws<InvalidOperationException>(() => Activate(draft, [draft]));
        Submit(draft);
        Activate(draft, [draft]);
        Assert.Equal(80, draft.MaximumAmount);
    }

    [Fact]
    public void Submitted_business_fields_cannot_change_silently_before_activation()
    {
        var draft = Create(Complete);
        Submit(draft);
        draft.MaximumAmount = 1000;
        Assert.Throws<InvalidOperationException>(() => Activate(draft, [draft]));
        Assert.Equal("PendingReview", draft.State);
    }

    [Fact]
    public void Supersession_requires_explicit_atomic_retirement_of_predecessor()
    {
        var old = Create(Complete with { MaximumAmount = 1000 });
        Submit(old); Activate(old, [old]);
        var next = Create(Complete with { MaximumAmount = 100 }, [old]);
        Assert.Equal(2, next.AuthorityVersion);
        Submit(next);
        Assert.Throws<InvalidOperationException>(() => Activate(next, [old, next]));
        SpendingAuthorityConfiguration.Supersede(next, next.Version, old, old.Version, Reviewer,
            Subject, "Lower approved limit", Now, [old, next]);
        Assert.Equal("Superseded", old.State);
        Assert.Equal(next.Id, old.SupersededByGrantId);
        Assert.Equal("Active", next.State);
        Assert.Equal(100, next.MaximumAmount);
        Assert.Throws<InvalidOperationException>(() => Activate(old, [old, next]));
    }

    [Fact]
    public void Revocation_is_reasoned_terminal_and_does_not_revive_old_higher_grant()
    {
        var old = Create(Complete with { MaximumAmount = 1000 });
        Submit(old); Activate(old, [old]);
        var next = Create(Complete, [old]); Submit(next);
        SpendingAuthorityConfiguration.Supersede(next, next.Version, old, old.Version, Reviewer,
            Subject, "New limit", Now, [old, next]);
        var revoker = Actor("security-admin", SpendingAuthorityConfiguration.RevokePermission);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Revoke(next,
            next.Version, revoker, " ", Now));
        SpendingAuthorityConfiguration.Revoke(next, next.Version, revoker, "Delegated authority withdrawn", Now);
        Assert.Equal("Revoked", next.State);
        Assert.Equal("security-admin", next.RevokedByPrincipalId);
        Assert.Equal("Delegated authority withdrawn", next.RevocationReason);
        Assert.Equal("Superseded", old.State);
        var request = new PurchaseRequest { TenantId = "tenant", LegalEntityId = Company,
            BranchRef = Branch, DepartmentRef = Department, CurrencyCode = "ZMW" };
        request.Lines.Add(new PurchaseRequestLine { TenantId = "tenant", LegalEntityId = Company,
            PurchaseRequestId = request.Id, CategoryCode = "IT", RequestedQuantity = 1, EstimatedUnitPrice = 500 });
        var eligibility = PurchaseRequestSpendingAuthority.Evaluate(request, "subject",
            SpendingAuthorityConfiguration.ApprovalPermission, SpendingAuthorityConfiguration.ApprovalAction,
            "EstimatedLineBase", Now, [old, next]);
        Assert.False(eligibility.Eligible);
        Assert.Null(eligibility.Evidence);
        Assert.Throws<InvalidOperationException>(() => Activate(next, [old, next]));
        Assert.Throws<InvalidOperationException>(() => Activate(old, [old, next]));
    }

    [Fact]
    public void Older_pending_version_cannot_activate_after_newer_version_is_revoked()
    {
        var previous = Create(Complete);
        Submit(previous);
        var later = Create(Complete with { MaximumAmount = 50 }, [previous]);
        Submit(later); Activate(later, [previous, later]);
        SpendingAuthorityConfiguration.Revoke(later, later.Version,
            Actor("revoker", SpendingAuthorityConfiguration.RevokePermission), "Withdrawn", Now);
        Assert.Throws<InvalidOperationException>(() => Activate(previous, [previous, later]));
        Assert.Equal("PendingReview", previous.State);
    }

    [Fact]
    public void Missing_family_or_duplicate_version_and_future_supersession_fail_without_partial_changes()
    {
        var old = Create(Complete); Submit(old); Activate(old, [old]);
        var next = Create(Complete with { EffectiveFrom = Now.AddHours(1) }, [old]); Submit(next);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Supersede(next,
            next.Version, old, old.Version, Reviewer, Subject, "New limit", Now, [old, next]));
        Assert.Equal("Active", old.State);
        Assert.Equal("PendingReview", next.State);
        Assert.Throws<InvalidOperationException>(() => Activate(next, []));
        var conflicting = JsonSerializer.Deserialize<ProcurementSpendingAuthorityGrant>(JsonSerializer.Serialize(old))!;
        conflicting.Id = Guid.NewGuid();
        var duplicate = Assert.Throws<InvalidOperationException>(() => Create(Complete, [old, conflicting]));
        Assert.Equal("AuthorityFamilyVersionsAmbiguous", duplicate.Message);
    }

    [Fact]
    public void Published_business_fingerprint_rejects_mutation_and_terminal_revival()
    {
        var grant = Create(Complete); Submit(grant); Activate(grant, [grant]);
        grant.MaximumAmount += 1;
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.AssertPublishedBusinessUnchanged(grant, grant));
        grant.MaximumAmount -= 1;
        SpendingAuthorityConfiguration.Revoke(grant, grant.Version,
            Actor("revoker", SpendingAuthorityConfiguration.RevokePermission), "Withdrawn", Now);
        var proposed = JsonSerializer.Deserialize<ProcurementSpendingAuthorityGrant>(JsonSerializer.Serialize(grant))!;
        proposed.State = "Active";
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.AssertPublishedBusinessUnchanged(grant, proposed));
    }

    [Fact]
    public void Fingerprint_is_stable_across_database_decimal_scale_and_timestamp_offset_precision()
    {
        var grant = Create(Complete);
        grant.CreatedAt = Now.AddTicks(7);
        var before = SpendingAuthorityConfiguration.Fingerprint(grant);
        grant.MaximumAmount = 100.0000m;
        grant.CreatedAt = Now.ToOffset(TimeSpan.FromHours(2));
        Assert.Equal(before, SpendingAuthorityConfiguration.Fingerprint(grant));
    }

    [Fact]
    public void Revoke_requires_current_server_permission_scope_and_revision()
    {
        var grant = Create(Complete); Submit(grant); Activate(grant, [grant]);
        foreach (var actor in new[] { Actor("reviewer"),
            Actor("reviewer", SpendingAuthorityConfiguration.RevokePermission) with { TenantId = "foreign" },
            Actor("reviewer", SpendingAuthorityConfiguration.RevokePermission) with { IsActive = false } })
            Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Revoke(grant,
                grant.Version, actor, "Withdrawn", Now));
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Revoke(grant,
            grant.Version + 1, Actor("reviewer", SpendingAuthorityConfiguration.RevokePermission), "Withdrawn", Now));
        Assert.Equal("Active", grant.State);
    }

    [Fact]
    public void Activation_requires_review_reason_and_the_actual_loaded_family_instance()
    {
        var grant = Create(Complete); Submit(grant);
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Activate(grant,
            grant.Version, Reviewer, Subject, " ", Now, [grant]));
        var detached = JsonSerializer.Deserialize<ProcurementSpendingAuthorityGrant>(JsonSerializer.Serialize(grant))!;
        Assert.Throws<InvalidOperationException>(() => Activate(grant, [detached]));
        Assert.Equal("PendingReview", grant.State);
    }

    [Theory]
    [InlineData("0.00005")]
    [InlineData("100000000000000")]
    public void Draft_rejects_values_that_PostgreSQL_numeric_cannot_store_exactly(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<InvalidOperationException>(() => Create(Complete with { MaximumAmount = amount }));
        var draft = Create(new());
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.EditDraft(draft,
            draft.Version, Complete with { MaximumAmount = amount }, Owner, Now));
        Assert.Equal(0, draft.MaximumAmount);
        Assert.Equal("Draft", draft.State);
    }

    private static ProcurementSpendingAuthorityGrant Create(SpendingAuthorityDraft input,
        IReadOnlyCollection<ProcurementSpendingAuthorityGrant>? family = null)
        => SpendingAuthorityConfiguration.CreateDraft("AUTH-001", "subject", input, Owner, Now, family ?? []);
    private static void Submit(ProcurementSpendingAuthorityGrant grant)
        => SpendingAuthorityConfiguration.Submit(grant, grant.Version, Owner, Subject, Now);
    private static void Activate(ProcurementSpendingAuthorityGrant grant,
        IReadOnlyCollection<ProcurementSpendingAuthorityGrant> family)
        => SpendingAuthorityConfiguration.Activate(grant, grant.Version, Reviewer, Subject,
            "Checked the declared scope", Now, family);
    private static SpendingAuthorityConfigurationActor Actor(string principal, params string[] permissions)
        => new("tenant", Company, principal, true, permissions.ToHashSet(StringComparer.Ordinal));
}
