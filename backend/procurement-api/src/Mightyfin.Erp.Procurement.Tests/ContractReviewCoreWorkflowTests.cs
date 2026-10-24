using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ContractReviewCoreWorkflowTests
{
    [Fact]
    public void Named_two_stage_One_All_opens_only_a_shared_Core_case_without_signature()
    {
        var f = Fixture();
        var result = ContractReviewCoreWorkflow.Open(f.Contract, f.Version, [f.Rate], f.Policy,
            f.Branch, f.Maker, null, f.Reviewers, 1, f.Now);
        Assert.Equal("SupplierContractReview", result.Case.SourceType);
        Assert.Equal(result.Submission.Id, result.Case.SourceId);
        Assert.Equal(result.Submission.CoreCaseId, result.Case.Id);
        Assert.Equal(f.Branch, result.Submission.ReviewBranchId);
        Assert.Equal(new[] { "Any", "All" }, result.Case.Stages.OrderBy(x => x.Sequence).Select(x => x.Mode));
        Assert.Equal(new[] { 1, 2 }, result.Case.Stages.OrderBy(x => x.Sequence).Select(x => x.RequiredApprovals));
        var pins = ContractReviewAssignmentPins.Verify(result.Submission, result.Case, f.Policy);
        Assert.Equal(3, pins.Count);
        ContractReviewCoreHistory.VerifyPending(result.Submission, result.Case, pins, []);
        Assert.All(result.Case.Stages.SelectMany(x => x.Assignments), x => Assert.Equal("procurement-contract-review", x.PermissionKey));
        Assert.Equal("Draft", f.Contract.Status);
        Assert.Null(f.Version.SignedAt);
        Assert.Null(f.Version.SignedDocumentServiceId);
        result.Case.Stages.Single(x => x.Sequence == 1).State = "Approved";
        Assert.Throws<InvalidOperationException>(() => ContractReviewCoreHistory.VerifyPending(result.Submission,
            result.Case, pins, []));
        result.Case.Stages.Single(x => x.Sequence == 1).State = "Active";
        result.Submission.AssignmentPinsJson = result.Submission.AssignmentPinsJson.Replace("LEGAL", "FINANCE", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ContractReviewAssignmentPins.Verify(result.Submission, result.Case, f.Policy));
    }

    [Fact]
    public void Revoked_branch_grant_or_known_alias_blocks_opening()
    {
        var f = Fixture();
        var deniedBranch = f.Reviewers.Select(x => x.PrincipalId == f.Reviewers[0].PrincipalId
            ? x with { AllowedBranches = new HashSet<Guid>() } : x).ToArray();
        Assert.Throws<InvalidOperationException>(() => ContractReviewCoreWorkflow.Open(f.Contract,
            f.Version, [f.Rate], f.Policy, f.Branch, f.Maker, null, deniedBranch, 1, f.Now));

        var linked = Guid.CreateVersion7();
        var sameWorker = f.Reviewers.Select(x => x.PrincipalId == f.Reviewers[0].PrincipalId
            ? x with { WorkerAliasId = linked } : x).ToArray();
        Assert.Throws<InvalidOperationException>(() => ContractReviewCoreWorkflow.Open(f.Contract,
            f.Version, [f.Rate], f.Policy, f.Branch, f.Maker, linked, sameWorker, 1, f.Now));

        var peerAlias = f.Reviewers.Select((x, index) => index < 2
            ? x with { WorkerAliasId = linked } : x).ToArray();
        Assert.Throws<InvalidOperationException>(() => ContractReviewCoreWorkflow.Open(f.Contract,
            f.Version, [f.Rate], f.Policy, f.Branch, f.Maker, null, peerAlias, 1, f.Now));
    }

    private static (SupplierContract Contract, SupplierContractVersion Version, ContractRate Rate,
        PolicyVersion Policy, Guid Branch, string Maker, ContractReviewCurrentReviewer[] Reviewers,
        DateTimeOffset Now) Fixture()
    {
        const string tenant = "synthetic-contract-review";
        var company = Guid.CreateVersion7(); var branch = Guid.CreateVersion7();
        var maker = Guid.CreateVersion7().ToString("D");
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.CreateVersion7().ToString("D")).ToArray();
        var rules = JsonSerializer.Serialize(new { schemaVersion = 1,
            transactionType = "SupplierContractReview", stages = new object[]
            { new { code = "COMMERCIAL", mode = "One", principalIds = new[] { ids[0] } },
                new { code = "LEGAL", mode = "All", principalIds = new[] { ids[1], ids[2] } } } });
        var now = DateTimeOffset.UtcNow;
        var contract = new SupplierContract { TenantId = tenant, LegalEntityId = company,
            Number = "CON-SYNTHETIC", SupplierId = Guid.CreateVersion7(), Title = "Synthetic",
            ContractType = "Framework", CurrencyCode = "ZMW", CeilingAmount = 100m,
            OwnerSubjectId = maker, Status = "Draft" };
        var version = new SupplierContractVersion { TenantId = tenant, LegalEntityId = company,
            SupplierContractId = contract.Id, RevisionNumber = 1, CeilingAmount = 100m, Status = "Draft" };
        var rate = new ContractRate { TenantId = tenant, LegalEntityId = company,
            SupplierContractVersionId = version.Id, LineNumber = 1, Status = "Active",
            Description = "Synthetic rate", UnitCode = "EA", UnitPrice = 10m };
        var policy = new PolicyVersion { TenantId = tenant, LegalEntityId = company,
            PolicyKey = ProcurementContractReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1,
            State = "Published", RulesJson = rules, EffectiveFrom = now.AddDays(-1) };
        var reviewers = ids.Select(id => new ContractReviewCurrentReviewer(id, null, false,
            new HashSet<Guid> { branch }, new HashSet<string> { "procurement-access", "procurement-contract-review" })).ToArray();
        return (contract, version, rate, policy, branch, maker, reviewers, now);
    }
}
