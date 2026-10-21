using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Finite configuration/evaluation only; no operational publication handler or state effect.</summary>
public sealed class SourcingPublicationAuthorityConfigurationTests
{
    private static readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), Department = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T10:00:00Z");
    private static SpendingAuthorityConfigurationActor Actor(string principal, string permission) => new("tenant", Company, principal, true, new HashSet<string>([permission]));
    private static SpendingAuthorityConfigurationActor Maker => Actor("maker", SpendingAuthorityConfiguration.ConfigurePermission);
    private static SpendingAuthoritySubject Subject => new("tenant", Company, "subject", true, true);
    private static SpendingAuthorityDraft Draft => new("ZMW", "ApprovedLineBase", 12.3456m, Now.AddDays(-1), Now.AddDays(1),
        BranchRef: Branch, DepartmentRef: Department, CategoryCodes: ["IT"]);
    private static ProcurementSpendingAuthorityGrant Create(SpendingAuthorityDraft? input = null) =>
        SpendingAuthorityConfiguration.CreateDraft("RFQ-FINITE", "subject", input ?? Draft, Maker, Now, [], "SourcingEvent.Publish");

    [Fact]
    public void Exact_configured_lifecycle_covers_only_its_own_tuple_and_limit_without_other_transaction_authority()
    {
        var grant = Create();
        Assert.Equal("SourcingEvent.Publish", grant.Action); Assert.Equal("procurement-sourcing-publish", grant.Permission);
        SpendingAuthorityConfiguration.Submit(grant, grant.Version, Maker, Subject, Now);
        SpendingAuthorityConfiguration.Activate(grant, grant.Version, Actor("reviewer", SpendingAuthorityConfiguration.ReviewPermission), Subject, "Reviewed explicit limit", Now, [grant]);
        var context = new ProcurementSpendContext(Guid.NewGuid(), "tenant", Company, "ZMW", "ApprovedLineBase", 12.3456m, Branch, [Department], ["IT"]);
        var before = SpendingAuthorityConfiguration.Fingerprint(grant);
        Assert.True(ProcurementSpendingAuthority.Evaluate(context, "subject", grant.Permission, grant.Action, Now, [grant]).Eligible);
        Assert.False(ProcurementSpendingAuthority.Evaluate(context with { Amount = 12.34560001m }, "subject", grant.Permission, grant.Action, Now, [grant]).Eligible);
        Assert.False(ProcurementSpendingAuthority.Evaluate(context with { CurrencyCode = "USD" }, "subject", grant.Permission, grant.Action, Now, [grant]).Eligible);
        Assert.False(ProcurementSpendingAuthority.Evaluate(context with { BranchRef = Guid.NewGuid() }, "subject", grant.Permission, grant.Action, Now, [grant]).Eligible);
        Assert.False(ProcurementSpendingAuthority.Evaluate(context, "subject", "procurement-po-approve", "PurchaseOrder.Approve", Now, [grant]).Eligible);
        Assert.False(ProcurementSpendingAuthority.Evaluate(context with { AmountBasis = "EstimatedLineBase" }, "subject", "procurement-approval-decide", "PurchaseRequest.Approve", Now, [grant]).Eligible);
        Assert.Equal(before, SpendingAuthorityConfiguration.Fingerprint(grant));
    }

    [Theory]
    [InlineData("maker")][InlineData("subject")]
    public void Maker_and_subject_cannot_review_their_own_sourcing_grant(string principal)
    {
        var grant = Create(); SpendingAuthorityConfiguration.Submit(grant, grant.Version, Maker, Subject, Now);
        var version = grant.Version;
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Activate(grant, grant.Version,
            Actor(principal, SpendingAuthorityConfiguration.ReviewPermission), Subject, "Not independent", Now, [grant]));
        Assert.Equal("PendingReview", grant.State); Assert.Equal(version, grant.Version); Assert.Null(grant.PublishedBusinessFingerprint);
    }

    [Theory]
    [InlineData("EstimatedLineBase")][InlineData("Gross")]
    public void Another_amount_basis_cannot_submit_as_publication_authority(string basis)
    {
        var grant = Create(Draft with { AmountBasis = basis });
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.Submit(grant, grant.Version, Maker, Subject, Now));
        Assert.Equal("Draft", grant.State); Assert.Null(grant.SubmittedBusinessFingerprint);
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("permission")]
    public void Wrong_owner_scope_or_permission_cannot_edit_sourcing_authority(string invalid)
    {
        var grant = Create(); var actor = invalid switch {
            "tenant" => Maker with { TenantId = "foreign" },
            "company" => Maker with { LegalEntityId = Guid.NewGuid() },
            _ => Maker with { Permissions = new HashSet<string>() } };
        Assert.Throws<InvalidOperationException>(() => SpendingAuthorityConfiguration.EditDraft(grant, grant.Version, Draft with { MaximumAmount = 999 }, actor, Now));
        Assert.Equal(12.3456m, grant.MaximumAmount); Assert.Equal("Draft", grant.State);
    }
}
