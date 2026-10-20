using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestSpendingAuthorityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private const string Principal = "canonical-erp-principal";
    private const string Permission = "procurement-approval-decide";
    private const string Action = "PurchaseRequest.Approve";

    [Fact]
    public void Exact_limit_is_eligible_and_retains_detached_versioned_evidence()
    {
        var request = Request();
        var grant = Grant(request);
        var result = Check(request, grant);
        Assert.True(result.Eligible);
        var evidence = Assert.IsType<PurchaseRequestAuthorityEvidence>(result.Evidence);
        Assert.Equal(grant.Id, evidence.GrantId);
        Assert.Equal(grant.Version, evidence.GrantRevision);
        Assert.Equal(grant.AuthorityVersion, evidence.AuthorityVersion);
        Assert.Equal(grant.AuthorityReference, evidence.AuthorityReference);
        Assert.Equal(100m, evidence.RequestAmount);
        Assert.Equal(100m, evidence.MaximumAmount);
        Assert.Equal("EstimatedLineBase", evidence.AmountBasis);
        Assert.Equal(Principal, evidence.PrincipalId);
        Assert.Equal(Permission, evidence.Permission);
        Assert.Equal(Action, evidence.Action);
        Assert.Equal(request.TenantId, evidence.TenantId);
        Assert.Equal(request.LegalEntityId, evidence.LegalEntityId);
        Assert.Equal(request.BranchRef, evidence.BranchRef);
        Assert.Equal(request.DepartmentRef, evidence.DepartmentRef);
        Assert.Equal(Now, evidence.EvaluatedAt);
        grant.Categories.Clear();
        grant.MaximumAmount = 1;
        Assert.Equal("IT", Assert.Single(evidence.CategoryCodes));
        Assert.Equal(100m, evidence.MaximumAmount);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void One_cent_over_limit_is_denied_without_rounding_down()
    {
        var request = Request();
        var grant = Grant(request);
        request.Lines.Single().EstimatedUnitPrice += 0.01m;
        Denied(Check(request, grant));
    }

    [Theory]
    [InlineData("USD", "EstimatedLineBase")]
    [InlineData("ZMW", "TaxInclusive")]
    [InlineData("", "EstimatedLineBase")]
    public void Currency_and_basis_are_not_converted_or_assumed(string currency, string basis)
    {
        var request = Request();
        var grant = Grant(request);
        grant.CurrencyCode = currency;
        grant.AmountBasis = basis;
        Denied(Check(request, grant));
    }

    [Fact]
    public void Unknown_requested_basis_cannot_match_even_when_grant_uses_it()
    {
        var request = Request();
        var grant = Grant(request);
        grant.AmountBasis = "TaxInclusive";
        var result = PurchaseRequestSpendingAuthority.Evaluate(request, Principal, Permission,
            Action, "TaxInclusive", Now, [grant]);
        Denied(result);
        Assert.Contains("AuthorityAmountBasisUnsupported", result.Blockers);
    }

    [Fact]
    public void Currency_case_is_normalized_but_line_currency_conflict_is_denied()
    {
        var request = Request();
        var grant = Grant(request);
        grant.CurrencyCode = "zmw";
        Assert.Equal("ZMW", Check(request, grant).Evidence!.CurrencyCode);
        request.Lines.Single().EstimatedCurrencyCode = "USD";
        Denied(Check(request, grant));
    }

    [Fact]
    public void Effective_interval_includes_start_and_excludes_end()
    {
        var request = Request();
        var grant = Grant(request);
        grant.EffectiveFrom = Now;
        grant.EffectiveUntil = Now.AddHours(1);
        Assert.True(Check(request, grant).Eligible);
        grant.EffectiveFrom = Now.AddTicks(1);
        Denied(Check(request, grant));
        grant.EffectiveFrom = Now.AddDays(-1);
        grant.EffectiveUntil = Now;
        Denied(Check(request, grant));
        grant.EffectiveUntil = grant.EffectiveFrom;
        Denied(Check(request, grant));
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Revoked")]
    [InlineData("Suspended")]
    [InlineData("Expired")]
    [InlineData("active")]
    public void Nonactive_grant_cannot_authorize(string state)
    {
        var request = Request();
        var grant = Grant(request);
        grant.State = state;
        Denied(Check(request, grant));
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("Company")]
    [InlineData("Branch")]
    [InlineData("Department")]
    [InlineData("Category")]
    [InlineData("Principal")]
    [InlineData("Permission")]
    [InlineData("Action")]
    public void Every_scope_identity_and_action_dimension_must_match(string mismatch)
    {
        var request = Request();
        var grant = Grant(request);
        switch (mismatch)
        {
            case "Tenant": grant.TenantId = "another-tenant"; break;
            case "Company": grant.LegalEntityId = Guid.NewGuid(); break;
            case "Branch": grant.BranchRef = Guid.NewGuid(); break;
            case "Department": grant.DepartmentRef = Guid.NewGuid(); break;
            case "Category": grant.Categories.Single().CategoryCode = "OFFICE"; break;
            case "Principal": grant.PrincipalId = "worker-id-is-not-canonical-principal"; break;
            case "Permission": grant.Permission = "procurement-request-read"; break;
            case "Action": grant.Action = "PurchaseOrder.Approve"; break;
        }
        Denied(Check(request, grant));
    }

    [Fact]
    public void Missing_scope_does_not_implicitly_grant_all_records()
    {
        var request = Request();
        var grant = Grant(request);
        grant.BranchRef = null;
        Denied(Check(request, grant));
        grant.AllBranches = true;
        Assert.True(Check(request, grant).Eligible);
        grant.DepartmentRef = null;
        Denied(Check(request, grant));
        grant.AllDepartments = true;
        Assert.True(Check(request, grant).Eligible);
        grant.Categories.Clear();
        Denied(Check(request, grant));
        grant.AllCategories = true;
        Assert.True(Check(request, grant).Eligible);
    }

    [Fact]
    public void Unrestricted_and_restricted_scope_cannot_be_declared_simultaneously()
    {
        var request = Request();
        var grant = Grant(request);
        grant.AllBranches = true;
        Denied(Check(request, grant));
        grant.AllBranches = false;
        grant.AllDepartments = true;
        Denied(Check(request, grant));
        grant.AllDepartments = false;
        grant.AllCategories = true;
        Denied(Check(request, grant));
    }

    [Fact]
    public void Missing_and_zero_or_unversioned_authority_do_not_create_default_limits()
    {
        var request = Request();
        Denied(Check(request));
        var grant = Grant(request);
        grant.MaximumAmount = 0;
        Denied(Check(request, grant));
        grant.MaximumAmount = 100;
        grant.AuthorityVersion = 0;
        Denied(Check(request, grant));
        grant.AuthorityVersion = 1;
        grant.Version = 0;
        Denied(Check(request, grant));
        grant.Version = 1;
        grant.AuthorityReference = "";
        Denied(Check(request, grant));
    }

    [Fact]
    public void Small_grants_cannot_be_added_together_or_used_per_line_to_bypass_full_amount()
    {
        var request = Request();
        request.Lines.Single().EstimatedUnitPrice = 60;
        request.Lines.Add(Line(request, "IT", 60));
        var first = Grant(request);
        var second = Grant(request);
        first.MaximumAmount = second.MaximumAmount = 100;
        Denied(Check(request, first, second));
        first.MaximumAmount = 120;
        Assert.Equal(120m, Check(request, first, second).Evidence!.RequestAmount);
    }

    [Fact]
    public void Different_category_grants_cannot_be_assembled_into_authority_for_one_request()
    {
        var request = Request();
        request.Lines.Single().EstimatedUnitPrice = 50;
        request.Lines.Add(Line(request, "OFFICE", 50));
        var first = Grant(request);
        var second = Grant(request);
        second.Categories.Single().CategoryCode = "OFFICE";
        Denied(Check(request, first, second));
        first.Categories.Add(Category(first, "OFFICE"));
        Assert.True(Check(request, first, second).Eligible);
        request.Lines.Last().CategoryCode = null;
        Denied(Check(request, first, second));
    }

    [Fact]
    public void Foreign_or_duplicate_category_evidence_is_denied()
    {
        var request = Request();
        var grant = Grant(request);
        grant.Categories.Single().LegalEntityId = Guid.NewGuid();
        Denied(Check(request, grant));
        grant.Categories.Clear();
        grant.Categories.Add(Category(grant, "IT"));
        grant.Categories.Add(Category(grant, "IT"));
        Denied(Check(request, grant));
    }

    [Fact]
    public void Cancelled_lines_are_excluded_but_invalid_active_lines_are_denied()
    {
        var request = Request();
        var grant = Grant(request);
        var extra = Line(request, "OFFICE", 1000);
        extra.ApprovalStatus = "Cancelled";
        request.Lines.Add(extra);
        Assert.Equal(100m, Check(request, grant).Evidence!.RequestAmount);
        extra.ApprovalStatus = "Pending";
        extra.TenantId = "foreign";
        Denied(Check(request, grant));
        request.Lines.Clear();
        Denied(Check(request, grant));
    }

    [Fact]
    public void Negative_zero_and_overflow_amounts_fail_closed()
    {
        var request = Request();
        var grant = Grant(request);
        var line = request.Lines.Single();
        line.EstimatedUnitPrice = 0;
        Denied(Check(request, grant));
        line.EstimatedUnitPrice = -1;
        Denied(Check(request, grant));
        line.EstimatedUnitPrice = decimal.MaxValue;
        line.RequestedQuantity = 2;
        var result = Check(request, grant);
        Denied(result);
        Assert.Contains("AuthorityAmountOverflow", result.Blockers);
    }

    [Theory]
    [InlineData("", Permission, Action)]
    [InlineData(Principal, "", Action)]
    [InlineData(Principal, Permission, "")]
    public void Missing_principal_permission_or_action_is_denied(string principal, string permission, string action)
    {
        var request = Request();
        var grant = Grant(request);
        var result = PurchaseRequestSpendingAuthority.Evaluate(request, principal, permission,
            action, "EstimatedLineBase", Now, [grant]);
        Denied(result);
    }

    [Fact]
    public void Unrestricted_category_grant_does_not_repair_missing_source_categorization()
    {
        var request = Request();
        var grant = Grant(request);
        grant.AllCategories = true;
        grant.Categories.Clear();
        request.Lines.Single().CategoryCode = null;
        Denied(Check(request, grant));
    }

    [Fact]
    public void Duplicate_grant_identity_cannot_supply_contradictory_evidence()
    {
        var request = Request();
        var grant = Grant(request);
        var revoked = Grant(request);
        revoked.Id = grant.Id;
        revoked.State = "Revoked";
        var result = Check(request, grant, revoked);
        Denied(result);
        Assert.Contains("SpendingAuthorityEvidenceAmbiguous", result.Blockers);
    }

    private static void Denied(PurchaseRequestAuthorityResult result)
    {
        Assert.False(result.Eligible);
        Assert.Null(result.Evidence);
        Assert.NotEmpty(result.Blockers);
    }

    private static PurchaseRequestAuthorityResult Check(PurchaseRequest request,
        params ProcurementSpendingAuthorityGrant[] grants)
        => PurchaseRequestSpendingAuthority.Evaluate(request, Principal, Permission, Action,
            "EstimatedLineBase", Now, grants);

    private static PurchaseRequest Request()
    {
        var request = new PurchaseRequest
        {
            TenantId = "tenant-a", LegalEntityId = Guid.NewGuid(), BranchRef = Guid.NewGuid(),
            DepartmentRef = Guid.NewGuid(), CurrencyCode = "ZMW",
        };
        request.Lines.Add(Line(request, "IT", 100));
        return request;
    }

    private static PurchaseRequestLine Line(PurchaseRequest request, string category, decimal price)
        => new()
        {
            TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            PurchaseRequestId = request.Id, CategoryCode = category,
            RequestedQuantity = 1, EstimatedUnitPrice = price,
        };

    private static ProcurementSpendingAuthorityGrant Grant(PurchaseRequest request)
    {
        var grant = new ProcurementSpendingAuthorityGrant
        {
            TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            AuthorityReference = "AUTH-001", AuthorityVersion = 3, Version = 4,
            PrincipalId = Principal, Permission = Permission, Action = Action, State = "Active",
            CurrencyCode = "ZMW", AmountBasis = "EstimatedLineBase", MaximumAmount = 100,
            EffectiveFrom = Now.AddDays(-1), EffectiveUntil = Now.AddDays(1),
            BranchRef = request.BranchRef, DepartmentRef = request.DepartmentRef,
        };
        grant.Categories.Add(Category(grant, "IT"));
        return grant;
    }

    private static ProcurementSpendingAuthorityCategory Category(ProcurementSpendingAuthorityGrant grant,
        string category) => new()
        {
            TenantId = grant.TenantId, LegalEntityId = grant.LegalEntityId,
            GrantId = grant.Id, CategoryCode = category,
        };
}
