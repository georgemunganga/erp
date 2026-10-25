using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
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

    [Fact]
    public void Real_Core_One_then_All_votes_keep_prior_history_consistent()
    {
        var f = Fixture();
        var linkedWorker = Guid.CreateVersion7();
        var reviewers = f.Reviewers.Select((reviewer, index) => index == 0
            ? reviewer with { WorkerAliasId = linkedWorker } : reviewer).ToArray();
        var opened = ContractReviewCoreWorkflow.Open(f.Contract, f.Version, [f.Rate], f.Policy,
            f.Branch, f.Maker, null, reviewers, 1, f.Now);
        var pins = ContractReviewAssignmentPins.Verify(opened.Submission, opened.Case, f.Policy);
        var decisions = new List<ApprovalDecision>();
        var votes = new List<ContractReviewVote>();
        var receipts = new List<ContractReviewCommandReceipt>();
        ContractReviewCommandReceipt Ack(string operation, Guid? voteId, Guid key, string actor,
            DateTimeOffset at, string status)
        {
            var ack = new ContractReviewCommandReceipt { TenantId = opened.Submission.TenantId,
                LegalEntityId = opened.Submission.LegalEntityId, ContractId = opened.Submission.ContractId,
                SubmissionId = opened.Submission.Id, ReviewBranchId = opened.Submission.ReviewBranchId,
                VoteId = voteId, Operation = operation, CommandKey = key, ActorPrincipalId = actor,
                CommandSha256 = new string('a', 64), CompletedAt = at,
                ResultContractVersion = opened.Submission.ContractBusinessVersion + 1,
                ResponseStatus = operation == "Submit" ? 201 : 200 };
            ack.ResponseJson = ContractReviewReceiptProof.Response(ack, status);
            ack.ResponseSha256 = ProcurementEvidenceEncoding.Digest(ack.ResponseJson);
            return ack;
        }
        receipts.Add(Ack("Submit", null, Guid.CreateVersion7(), f.Maker,
            opened.Submission.SubmittedAt, "PendingReview"));
        var all = opened.Case.Stages.OrderBy(x => x.Sequence).ToArray();
        void Decide(ApprovalAssignment assignment, int minute)
        {
            var actor = new ApprovalActor(assignment.AssigneeSubjectId, opened.Case.TenantId,
                opened.Case.LegalEntityId, new HashSet<string> { ContractReviewCoreWorkflow.ReviewAction });
            ApprovalEngine.Decide(opened.Case, assignment, actor, "Approve", Guid.CreateVersion7().ToString("D"),
                null, decisions, ProcurementEvidenceEncoding.DatabaseTimestamp(f.Now.AddMinutes(minute))!.Value);
            var decision = decisions[^1];
            var vote = new ContractReviewVote { TenantId = opened.Submission.TenantId,
                LegalEntityId = opened.Submission.LegalEntityId, SubmissionId = opened.Submission.Id,
                ContractId = opened.Submission.ContractId, ReviewBranchId = opened.Submission.ReviewBranchId,
                CoreCaseId = opened.Case.Id, CoreStageId = decision.StageId,
                CoreAssignmentId = decision.AssignmentId, CoreDecisionId = decision.Id,
                ActorPrincipalId = decision.ActorSubjectId,
                ActorWorkerAliasId = pins.Single(x => x.AssignmentId == decision.AssignmentId).WorkerAliasId,
                Action = decision.Action,
                DecidedAt = decision.DecidedAt };
            votes.Add(vote);
            receipts.Add(Ack("Decide", vote.Id, Guid.Parse(decision.IdempotencyKey),
                decision.ActorSubjectId, decision.DecidedAt, "PendingReview"));
        }
        Decide(all[0].Assignments.Single(), 1);
        ContractReviewRetainedVotes.VerifyPending(opened.Submission, opened.Case, pins,
            decisions, votes, receipts);
        Decide(all[1].Assignments.First(), 2);
        ContractReviewRetainedVotes.VerifyPending(opened.Submission, opened.Case, pins,
            decisions, votes, receipts);
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes.Skip(1).ToArray(), receipts));
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes, receipts.Take(2).ToArray()));
        var originalVote = votes[0];
        votes[0] = ContractReviewEvidenceFixture.Replace(originalVote,
            nameof(ContractReviewVote.ActorPrincipalId), f.Maker);
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes, receipts));
        votes[0] = ContractReviewEvidenceFixture.Replace(originalVote,
            nameof(ContractReviewVote.ActorWorkerAliasId), null);
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes, receipts));
        votes[0] = ContractReviewEvidenceFixture.Replace(originalVote,
            nameof(ContractReviewVote.ActorWorkerAliasId), Guid.CreateVersion7());
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes, receipts));
        votes[0] = originalVote;
        var prior = receipts[1];
        prior.ResponseJson += " ";
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.VerifyPending(
            opened.Submission, opened.Case, pins, decisions, votes, receipts));
        prior.ResponseJson = prior.ResponseJson.TrimEnd();
        ContractReviewRetainedVotes.VerifyPending(opened.Submission, opened.Case, pins,
            decisions, votes, receipts);
        Decide(all[1].Assignments.Last(), 3);
        Assert.Equal("Approved", opened.Case.State);
        Assert.Equal(3, decisions.Count);
    }

    [Fact]
    public void Genuine_Core_Return_allows_exact_old_terms_then_correction_and_resubmit_snapshot()
    {
        var f = Fixture();
        var opened = ContractReviewCoreWorkflow.Open(f.Contract, f.Version, [f.Rate], f.Policy,
            f.Branch, f.Maker, null, f.Reviewers, 1, f.Now);
        var pins = ContractReviewAssignmentPins.Verify(opened.Submission, opened.Case, f.Policy);
        var stage = opened.Case.Stages.OrderBy(x => x.Sequence).First();
        var assignment = stage.Assignments.Single();
        var decisions = new List<ApprovalDecision>();
        ApprovalEngine.Decide(opened.Case, assignment,
            new ApprovalActor(assignment.AssigneeSubjectId, opened.Case.TenantId,
                opened.Case.LegalEntityId, new HashSet<string> { ContractReviewCoreWorkflow.ReviewAction }),
            "Return", Guid.CreateVersion7().ToString("D"), "Correct the rate", decisions,
            ProcurementEvidenceEncoding.DatabaseTimestamp(f.Now.AddMinutes(1))!.Value);
        ContractReviewCoreHistory.VerifyReturned(opened.Submission, opened.Case, pins, decisions);
        var decision = Assert.Single(decisions);
        var vote = new ContractReviewVote { TenantId = opened.Submission.TenantId,
            LegalEntityId = opened.Submission.LegalEntityId, SubmissionId = opened.Submission.Id,
            ContractId = opened.Submission.ContractId, ReviewBranchId = opened.Submission.ReviewBranchId,
            CoreCaseId = opened.Case.Id, CoreStageId = decision.StageId,
            CoreAssignmentId = decision.AssignmentId, CoreDecisionId = decision.Id,
            ActorPrincipalId = decision.ActorSubjectId, Action = decision.Action,
            DecidedAt = decision.DecidedAt };
        ContractReviewCommandReceipt Ack(string operation, Guid? voteId, Guid key, string actor,
            DateTimeOffset at, long resultVersion, int status, string contractStatus)
        {
            var ack = new ContractReviewCommandReceipt { ContractId = opened.Submission.ContractId,
                SubmissionId = opened.Submission.Id, ReviewBranchId = opened.Submission.ReviewBranchId,
                VoteId = voteId, Operation = operation, CommandKey = key,
                ActorPrincipalId = actor, CommandSha256 = new string('a', 64),
                CompletedAt = at, ResultContractVersion = resultVersion, ResponseStatus = status };
            ack.ResponseJson = ContractReviewReceiptProof.Response(ack, contractStatus);
            ack.ResponseSha256 = ProcurementEvidenceEncoding.Digest(ack.ResponseJson);
            return ack;
        }
        var submitted = Ack("Submit", null, Guid.CreateVersion7(), f.Maker,
            opened.Submission.SubmittedAt, opened.Submission.ContractBusinessVersion + 1, 201, "PendingReview");
        var returned = Ack("Decide", vote.Id, Guid.Parse(decision.IdempotencyKey), decision.ActorSubjectId,
            decision.DecidedAt, opened.Submission.ContractBusinessVersion + 2, 200, "Returned");
        ContractReviewRetainedVotes.Verify(opened.Submission, pins, decisions, [vote], [submitted, returned]);
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.Verify(
            opened.Submission, pins, decisions, [], [submitted, returned]));
        var alteredVote = ContractReviewEvidenceFixture.Replace(vote,
            nameof(ContractReviewVote.ActorWorkerAliasId), Guid.CreateVersion7());
        Assert.Throws<InvalidOperationException>(() => ContractReviewRetainedVotes.Verify(
            opened.Submission, pins, decisions, [alteredVote], [submitted, returned]));
        f.Contract.Status = "Returned"; f.Version.Status = "Returned";
        f.Contract.Version = checked(opened.Submission.ContractBusinessVersion + 2);
        f.Version.Version = checked(opened.Submission.ContractVersionBusinessVersion + 2);
        f.Version.ApprovalInstanceRef = opened.Case.Id.ToString("D");
        ContractReviewTerms.VerifyCurrentReturned(opened.Submission, f.Contract, f.Version, [f.Rate]);
        f.Rate.UnitPrice = 11m;
        Assert.Throws<InvalidOperationException>(() => ContractReviewTerms.VerifyCurrentReturned(
            opened.Submission, f.Contract, f.Version, [f.Rate]));
        f.Rate.UnitPrice = 10m;
        f.Contract.Status = "Draft"; f.Version.Status = "Draft";
        f.Version.ApprovalInstanceRef = null;
        f.Contract.Version++; f.Version.Version++;
        var next = ContractReviewCoreWorkflow.Open(f.Contract, f.Version, [f.Rate], f.Policy,
            f.Branch, f.Maker, null, f.Reviewers, 2, f.Now.AddMinutes(2));
        Assert.Equal(2, next.Submission.SubmissionNumber);
        Assert.NotEqual(opened.Submission.Id, next.Submission.Id);
        Assert.NotEqual(opened.Submission.TermsSha256, next.Submission.TermsSha256);
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
