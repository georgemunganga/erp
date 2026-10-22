using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Synthetic pure source evidence only. No installed review policy/provider or Finance authority.</summary>
public sealed class InvoiceRetainedUnitReviewTests
{
    [Fact]
    public void Another_current_matcher_can_consume_exact_retained_independent_review_without_resigning_it()
    {
        var f = new Fixture();
        Assert.NotEqual(f.Envelope.ReviewerPrincipalId, f.Consumer.PrincipalId);
        Assert.NotEqual(f.Envelope.ReviewerWorkerId, f.Consumer.WorkerId);
        Assert.Equal("EACH", Assert.Single(f.Verify()).Value);
        var original = f.Authority.EvidenceJson;
        f.Invoice.Version++; // Operational comparisons do not rewrite the original or reviewer.
        Assert.Equal("EACH", Assert.Single(f.Verify()).Value);
        f.Consumer = f.Consumer with { PrincipalId = f.Mapping.ActorPrincipalId, WorkerId = f.Mapping.ActorWorkerId };
        Assert.Equal("EACH", Assert.Single(f.Verify()).Value); // A maker may consume another person's review, not self-review.
        Assert.Equal(original, f.Authority.EvidenceJson);
        Assert.Null(f.Invoice.ActiveMatchClaimResultId); Assert.Null(f.Invoice.FinancePostingRef);
        Assert.Equal("NotCompared", f.Invoice.MatchingState);
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("branch")][InlineData("identity")]
    [InlineData("worker")][InlineData("permission")][InlineData("access")][InlineData("time")]
    [InlineData("revokedBranch")][InlineData("selectedBranch")]
    public void Current_consumer_scope_identity_or_permission_cannot_be_replaced_by_historical_signer(string changed)
    {
        var f = new Fixture();
        f.Consumer = changed switch
        {
            "tenant" => f.Consumer with { TenantId = "foreign" },
            "company" => f.Consumer with { LegalEntityId = Guid.NewGuid() },
            "branch" => f.Consumer with { BranchId = Guid.NewGuid() },
            "identity" => f.Consumer with { PrincipalId = "" },
            "worker" => f.Consumer with { WorkerId = Guid.Empty },
            "revokedBranch" => f.Consumer with { AllowedBranches = new HashSet<Guid>() },
            "selectedBranch" => f.Consumer with { SelectedBranchId = Guid.NewGuid() },
            "permission" => f.Consumer with { Permissions = new HashSet<string> { "procurement-access" } },
            "access" => f.Consumer with { Permissions = new HashSet<string> { "procurement-invoice-match" } },
            _ => f.Consumer with { At = f.Envelope.ReviewedAt.AddTicks(-1) },
        };
        f.Denied();
    }

    [Theory]
    [InlineData("invoice")][InlineData("original")][InlineData("mapping")][InlineData("revision")]
    [InlineData("issued")][InlineData("order")][InlineData("mappingHash")][InlineData("maker")]
    [InlineData("reviewId")][InlineData("sourceVersion")][InlineData("schema1")][InlineData("schema3")]
    [InlineData("invoiceVersion")][InlineData("matchingPolicy")][InlineData("reviewPolicy")]
    [InlineData("matchingHash")][InlineData("reviewHash")][InlineData("line")][InlineData("unit")]
    [InlineData("document")][InlineData("documentHash")][InlineData("documentLine")][InlineData("missingLine")]
    public void Rehashed_foreign_or_altered_v2_fields_still_fail_exact_owned_binding(string changed)
    {
        var f = new Fixture(); var e = f.Envelope;
        e = changed switch
        {
            "invoice" => e with { InvoiceId = Guid.NewGuid() },
            "original" => e with { InvoiceOriginalSha256 = new string('b', 64) },
            "mapping" => e with { MappingVersionId = Guid.NewGuid() },
            "revision" => e with { MappingRevision = e.MappingRevision + 1 },
            "issued" => e with { IssuedVersionId = Guid.NewGuid() },
            "order" => e with { PurchaseOrderId = Guid.NewGuid() },
            "mappingHash" => e with { MappingSha256 = new string('b', 64) },
            "maker" => e with { MakerWorkerId = Guid.NewGuid() },
            "reviewId" => e with { ReviewId = Guid.NewGuid() },
            "sourceVersion" => e with { ReviewSourceVersion = 2 },
            "schema1" => e with { SchemaVersion = 1 },
            "schema3" => e with { SchemaVersion = 3 },
            "invoiceVersion" => e with { InvoiceVersionAtReview = f.Invoice.Version + 1 },
            "matchingPolicy" => e with { MatchingPolicyId = e.ReviewPolicyId },
            "reviewPolicy" => e with { ReviewPolicyId = e.MatchingPolicyId },
            "matchingHash" => e with { MatchingPolicySha256 = new string('b', 64) },
            "reviewHash" => e with { ReviewPolicySha256 = new string('b', 64) },
            "line" => e with { Lines = [e.Lines[0] with { InvoiceLineId = Guid.NewGuid() }] },
            "unit" => e with { Lines = [e.Lines[0] with { UnitCode = "BOX" }] },
            "document" => e with { Lines = [e.Lines[0] with { DocumentVersionId = Guid.NewGuid() }] },
            "documentHash" => e with { Lines = [e.Lines[0] with { DocumentSha256 = new string('b', 64) }] },
            "documentLine" => e with { Lines = [e.Lines[0] with { DocumentLineReference = "page2-line1" }] },
            _ => e with { Lines = [] },
        };
        f.Replace(e); f.Denied();
    }

    [Theory]
    [InlineData("case")][InlineData("caseScope")][InlineData("source")][InlineData("sourceType")]
    [InlineData("casePolicy")][InlineData("state")][InlineData("completion")][InlineData("decision")]
    [InlineData("assignment")][InlineData("decisionScope")][InlineData("permission")]
    [InlineData("aliasWorker")][InlineData("workerPinMissing")][InlineData("duplicateWorker")]
    [InlineData("duplicateDecision")][InlineData("futureVote")][InlineData("openedBeforeMapping")]
    public void Authentic_Core_case_decision_assignment_and_retained_worker_independence_are_required(string changed)
    {
        var f = new Fixture();
        switch (changed)
        {
            case "case": f.Case.Id = Guid.NewGuid(); break;
            case "caseScope": f.Case.LegalEntityId = Guid.NewGuid(); break;
            case "source": f.Case.SourceId = Guid.NewGuid(); break;
            case "sourceType": f.Case.SourceType = "purchase_request"; break;
            case "casePolicy": f.Case.PolicyVersionId = f.MatchingPolicy.Id; break;
            case "state": f.Case.State = "Pending"; break;
            case "completion": f.Case.CompletionKey = "other"; break;
            case "decision": f.Decision.Action = "Reject"; break;
            case "assignment": f.Decision.AssignmentId = Guid.NewGuid(); break;
            case "decisionScope": f.Decision.TenantId = "foreign"; break;
            case "permission": f.Assignment.PermissionKey = "procurement-approval-decide"; break;
            case "aliasWorker":
                f.Authority = f.Authority with { Workers = [new(f.Assignment.Id, f.Assignment.AssigneeSubjectId, f.Mapping.ActorWorkerId)] };
                f.Replace(f.Envelope with { ReviewerWorkerId = f.Mapping.ActorWorkerId, AssignmentWorkers = f.Authority.Workers }); break;
            case "workerPinMissing": f.Authority = f.Authority with { Workers = [] }; break;
            case "duplicateWorker": f.Authority = f.Authority with { Workers = [f.Authority.Workers[0], f.Authority.Workers[0]] }; break;
            case "duplicateDecision": f.Authority = f.Authority with { Decisions = [f.Decision, f.Decision] }; break;
            case "futureVote": f.Decision.DecidedAt = f.Consumer.At.AddMinutes(1); break;
            default: f.Case.OpenedAt = f.Mapping.ReviewedAt.AddTicks(-1); break;
        }
        f.Denied();
    }

    [Fact]
    public void Two_accounts_for_one_reviewer_worker_cannot_supply_two_quorum_votes()
    {
        var f = new Fixture(); var stage = Assert.Single(f.Case.Stages);
        var alias = new ApprovalAssignment { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            CaseId = f.Case.Id, StageId = stage.Id, AssigneeSubjectId = "reviewer-alias",
            PermissionKey = InvoiceRetainedUnitReview.ReviewPermission, State = "Approved" };
        stage.Assignments.Add(alias); stage.RequiredApprovals = 2;
        var vote = new ApprovalDecision { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            CaseId = f.Case.Id, StageId = stage.Id, AssignmentId = alias.Id, ActorSubjectId = alias.AssigneeSubjectId,
            Action = "Approve", IdempotencyKey = "other-vote", DecidedAt = f.Envelope.ReviewedAt };
        var workers = f.Authority.Workers.Concat(new[] { new InvoiceUnitReviewAssignmentWorker(alias.Id, alias.AssigneeSubjectId, f.Envelope.ReviewerWorkerId) }).ToArray();
        f.Authority = f.Authority with { Workers = workers, Decisions = f.Authority.Decisions.Concat(new[] { vote }).ToArray() };
        f.Replace(f.Envelope with { AssignmentWorkers = workers });
        f.Denied();
    }

    [Theory]
    [InlineData("changedOriginal")][InlineData("currentMapping")][InlineData("missingDocument")]
    [InlineData("changedDocumentHash")][InlineData("expiredReviewPolicy")][InlineData("revokedReviewPolicy")]
    [InlineData("changedReviewRules")][InlineData("changedMatchingRules")][InlineData("matchingAsReview")]
    public void Current_owned_source_document_or_policy_change_invalidates_new_consumption(string changed)
    {
        var f = new Fixture();
        switch (changed)
        {
            case "changedOriginal": f.Line.Quantity = 2; break;
            case "currentMapping": f.Invoice.CurrentMappingVersionId = Guid.NewGuid(); break;
            case "missingDocument": f.Consumer = f.Consumer with { DocumentVersionHashes = new Dictionary<Guid, string>() }; break;
            case "changedDocumentHash": f.Consumer = f.Consumer with { DocumentVersionHashes = new Dictionary<Guid, string> { [f.DocumentId] = new string('b', 64) } }; break;
            case "expiredReviewPolicy": f.ReviewPolicy.EffectiveTo = f.Consumer.At; break;
            case "revokedReviewPolicy": f.ReviewPolicy.State = "Superseded"; break;
            case "changedReviewRules": f.ReviewPolicy.RulesJson = "{\"changed\":true}"; break;
            case "changedMatchingRules": f.MatchingPolicy.RulesJson = "{\"changed\":true}"; break;
            default: f.Authority = f.Authority with { ReviewPolicy = f.MatchingPolicy }; break;
        }
        f.Denied();
    }

    [Theory]
    [InlineData("malformed")][InlineData("duplicate")][InlineData("unknown")][InlineData("missing")]
    [InlineData("hash")][InlineData("duplicateLine")]
    public void Unsupported_JSON_or_incomplete_evidence_is_not_promoted_to_verified(string changed)
    {
        var f = new Fixture(); var json = f.Authority.EvidenceJson;
        if (changed == "duplicateLine") { f.Replace(f.Envelope with { Lines = [f.Envelope.Lines[0], f.Envelope.Lines[0]] }); f.Denied(); return; }
        json = changed switch
        {
            "malformed" => "{", "duplicate" => json.Insert(1, "\"schemaVersion\":2,"),
            "unknown" => json.Insert(1, "\"unowned\":true,"), "missing" => "{}", _ => json,
        };
        f.Authority = f.Authority with { EvidenceJson = json, EvidenceSha256 = changed == "hash" ? new string('b', 64) : ProcurementEvidenceEncoding.Digest(json) };
        f.Denied();
    }

    [Theory]
    [InlineData("rootSame")][InlineData("rootConflicting")][InlineData("rootCaseAlias")]
    [InlineData("lineSame")][InlineData("lineConflicting")][InlineData("lineCaseAlias")]
    [InlineData("workerSame")][InlineData("workerConflicting")][InlineData("workerCaseAlias")]
    [InlineData("depth")][InlineData("size")][InlineData("utf8Size")][InlineData("nodes")]
    [InlineData("quotedSchema")][InlineData("quotedSourceVersion")][InlineData("quotedMappingRevision")]
    public void Rehashed_duplicate_properties_and_unsupported_JSON_work_are_rejected_before_deserialization(string changed)
    {
        var f = new Fixture(); var json = f.Authority.EvidenceJson;
        json = changed switch
        {
            "rootSame" => json.Insert(1, "\"schemaVersion\":2,"),
            "rootConflicting" => json.Insert(1, "\"schemaVersion\":1,"), // Last property would otherwise win.
            "rootCaseAlias" => json.Insert(1, "\"SchemaVersion\":2,"),
            "quotedSchema" => json.Replace("\"schemaVersion\":2", "\"schemaVersion\":\"2\""),
            "quotedSourceVersion" => json.Replace("\"reviewSourceVersion\":1", "\"reviewSourceVersion\":\"1\""),
            "quotedMappingRevision" => json.Replace("\"mappingRevision\":1", "\"mappingRevision\":\"1\""),
            "lineSame" => json.Replace("\"unitCode\":\"EACH\"", "\"unitCode\":\"EACH\",\"unitCode\":\"EACH\""),
            "lineConflicting" => json.Replace("\"unitCode\":\"EACH\"", "\"unitCode\":\"BOX\",\"unitCode\":\"EACH\""),
            "lineCaseAlias" => json.Replace("\"unitCode\":\"EACH\"", "\"UnitCode\":\"EACH\",\"unitCode\":\"EACH\""),
            "workerCaseAlias" => json.Replace("\"assignmentWorkers\":[{", "\"assignmentWorkers\":[{\"PrincipalId\":\"reviewer\","),
            "workerSame" => json.Replace("\"assignmentWorkers\":[{", "\"assignmentWorkers\":[{\"principalId\":\"reviewer\","),
            "workerConflicting" => json.Replace("\"assignmentWorkers\":[{", "\"assignmentWorkers\":[{\"principalId\":\"maker\","),
            "depth" => "{\"nested\":" + new string('[', 17) + "0" + new string(']', 17) + "}",
            "size" => json.Insert(1, "\"oversized\":\"" + new string('a', 256 * 1024) + "\","),
            "utf8Size" => json.Insert(1, "\"oversized\":\"" + new string('界', 100_000) + "\","),
            _ => "{\"nodes\":[" + string.Join(',', Enumerable.Repeat("0", 10_001)) + "]}",
        };
        Assert.NotEqual(f.Authority.EvidenceJson, json);
        f.Authority = f.Authority with { EvidenceJson = json, EvidenceSha256 = ProcurementEvidenceEncoding.Digest(json) };
        f.Denied();
    }

    [Fact]
    public void Genuine_multi_stage_Core_quorum_with_equal_timestamps_has_valid_final_proof()
    {
        var f = MultiStageFixture(); Assert.Single(f.Verify());
        // Equal persisted PostgreSQL timestamps do not invent a strict temporal ordering.
        foreach (var vote in f.Authority.Decisions) vote.DecidedAt = f.Envelope.ReviewedAt;
        Assert.Single(f.Verify());
    }

    [Theory]
    [InlineData("earlierStageFinal")][InlineData("stageOrder")][InlineData("extraVote")]
    public void Impossible_Core_final_stage_chronology_or_extra_quorum_vote_is_rejected(string changed)
    {
        var f = MultiStageFixture(); var first = f.Authority.Decisions.OrderBy(x => x.DecidedAt).First();
        if (changed == "earlierStageFinal")
        {
            foreach (var vote in f.Authority.Decisions) vote.DecidedAt = f.Envelope.ReviewedAt;
            f.Case.CompletionKey = first.IdempotencyKey;
            var worker = f.Authority.Workers.Single(x => x.AssignmentId == first.AssignmentId);
            f.Replace(f.Envelope with { FinalDecisionId = first.Id, ReviewerPrincipalId = first.ActorSubjectId, ReviewerWorkerId = worker.WorkerId });
        }
        else if (changed == "stageOrder") first.DecidedAt = f.Envelope.ReviewedAt.AddSeconds(-1);
        else
        {
            var stage = f.Case.Stages.MaxBy(x => x.Sequence)!;
            var extra = new ApprovalAssignment { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
                CaseId = f.Case.Id, StageId = stage.Id, AssigneeSubjectId = "extra-reviewer",
                PermissionKey = InvoiceRetainedUnitReview.ReviewPermission, State = "Approved" };
            stage.Assignments.Add(extra);
            var vote = new ApprovalDecision { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
                CaseId = f.Case.Id, StageId = stage.Id, AssignmentId = extra.Id, ActorSubjectId = extra.AssigneeSubjectId,
                Action = "Approve", IdempotencyKey = "extra-vote", DecidedAt = f.Envelope.ReviewedAt };
            var workers = f.Authority.Workers.Concat(new[] { new InvoiceUnitReviewAssignmentWorker(extra.Id, extra.AssigneeSubjectId, Guid.NewGuid()) }).ToArray();
            f.Authority = f.Authority with { Workers = workers, Decisions = f.Authority.Decisions.Concat(new[] { vote }).ToArray() };
            f.Replace(f.Envelope with { AssignmentWorkers = workers });
        }
        f.Denied();
    }

    private static Fixture MultiStageFixture()
    {
        var f = new Fixture(); var firstStage = Assert.Single(f.Case.Stages);
        firstStage.State = "Active"; f.Assignment.State = "Pending";
        f.Case.State = "Pending"; f.Case.CompletedAt = null; f.Case.CompletionKey = null;
        var secondStage = new ApprovalStage { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            CaseId = f.Case.Id, Sequence = 2, Code = "SECOND_REVIEW", State = "Waiting", Mode = "Quorum", RequiredApprovals = 2 };
        var second = new ApprovalAssignment { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            CaseId = f.Case.Id, StageId = secondStage.Id, AssigneeSubjectId = "second-reviewer", PermissionKey = InvoiceRetainedUnitReview.ReviewPermission };
        var final = new ApprovalAssignment { TenantId = f.Invoice.TenantId, LegalEntityId = f.Invoice.LegalEntityId,
            CaseId = f.Case.Id, StageId = secondStage.Id, AssigneeSubjectId = "final-reviewer", PermissionKey = InvoiceRetainedUnitReview.ReviewPermission };
        secondStage.Assignments.Add(second); secondStage.Assignments.Add(final); f.Case.Stages.Add(secondStage);
        ApprovalEngine.ValidateNewCase(f.Case);
        var decisions = new List<ApprovalDecision>();
        var permissions = new HashSet<string> { InvoiceRetainedUnitReview.ReviewPermission };
        foreach (var (assignment, seconds, key) in new[] { (f.Assignment, -3, "first"), (second, -2, "second"), (final, 0, "final") })
            ApprovalEngine.Decide(f.Case, assignment, new(assignment.AssigneeSubjectId, f.Invoice.TenantId, f.Invoice.LegalEntityId, permissions),
                "Approve", key, null, decisions, f.Envelope.ReviewedAt.AddSeconds(seconds));
        var workers = f.Authority.Workers.Concat(new[] { new InvoiceUnitReviewAssignmentWorker(second.Id, second.AssigneeSubjectId, Guid.NewGuid()),
            new InvoiceUnitReviewAssignmentWorker(final.Id, final.AssigneeSubjectId, Guid.NewGuid()) }).ToArray();
        f.Authority = f.Authority with { Workers = workers, Decisions = decisions };
        f.Decision = decisions.Last(); f.Assignment = final;
        f.Replace(f.Envelope with { AssignmentWorkers = workers, FinalDecisionId = f.Decision.Id,
            ReviewerPrincipalId = final.AssigneeSubjectId, ReviewerWorkerId = workers.Single(x => x.AssignmentId == final.Id).WorkerId });
        return f;
    }

    [Fact]
    public void Mapping_projection_consumes_typed_v2_without_fabricating_a_current_signer_envelope()
    {
        var f = new Fixture();
        var units = f.Verify();
        var review = new InvoiceMappingReview(true, "Verified", f.Authority.EvidenceJson, units)
            { RetainedAuthority = f.Authority, CurrentConsumer = f.Consumer };
        var context = new InvoiceMappingReviewContext(f.Consumer.TenantId, f.Consumer.LegalEntityId, f.Consumer.BranchId,
            f.Consumer.PrincipalId, f.Consumer.WorkerId, f.Authority.MatchingPolicy.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(f.Authority.MatchingPolicy.RulesJson), f.Consumer.At, f.Consumer.DocumentVersionHashes);
        var projected = InvoiceMappingEvidence.Project(f.Mapping, f.Invoice, [f.Line], review, context);
        Assert.Equal("EACH", Assert.Single(projected.Lines).UnitCode);
        Assert.Equal(f.Authority.EvidenceJson, review.EvidenceJson);
        Assert.NotEqual(f.Envelope.ReviewerPrincipalId, context.PrincipalId);
    }

    [Theory]
    [InlineData("actor")][InlineData("worker")][InlineData("policy")][InlineData("time")][InlineData("units")][InlineData("documents")]
    public void Typed_v2_projection_rechecks_current_consumer_and_actual_retained_units(string changed)
    {
        var f = new Fixture();
        var review = new InvoiceMappingReview(true, "Verified", f.Authority.EvidenceJson, f.Verify())
            { RetainedAuthority = f.Authority, CurrentConsumer = f.Consumer };
        var context = new InvoiceMappingReviewContext(f.Consumer.TenantId, f.Consumer.LegalEntityId, f.Consumer.BranchId,
            f.Consumer.PrincipalId, f.Consumer.WorkerId, f.Authority.MatchingPolicy.Id,
            ProcurementEvidenceEncoding.PolicyJsonDigest(f.Authority.MatchingPolicy.RulesJson), f.Consumer.At, f.Consumer.DocumentVersionHashes);
        context = changed switch {
            "actor" => context with { PrincipalId = "other" },
            "worker" => context with { WorkerId = Guid.NewGuid() },
            "policy" => context with { PolicyId = Guid.NewGuid() },
            "time" => context with { At = context.At.AddTicks(10) },
            "documents" => context with { DocumentVersionHashes = new Dictionary<Guid,string>() },
            _ => context };
        if (changed == "units") review = review with { VerifiedUnits = new Dictionary<Guid,string> { [f.Line.Id] = "BOX" } };
        Assert.Throws<InvalidOperationException>(() => InvoiceMappingEvidence.Project(f.Mapping, f.Invoice, [f.Line], review, context));
    }

    private sealed class Fixture
    {
        public SupplierInvoice Invoice; public SupplierInvoiceLine Line; public InvoiceMatchMappingVersion Mapping;
        public PolicyVersion MatchingPolicy; public PolicyVersion ReviewPolicy; public ApprovalCase Case;
        public ApprovalAssignment Assignment; public ApprovalDecision Decision; public Guid DocumentId = Guid.NewGuid();
        public InvoiceRetainedUnitReviewEnvelope Envelope; public InvoiceRetainedUnitReviewAuthority Authority;
        public InvoiceUnitReviewConsumer Consumer;
        public Fixture()
        {
            var at = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero); var company = Guid.NewGuid(); var branch = Guid.NewGuid();
            Invoice = new() { TenantId = "retained-review", LegalEntityId = company, BranchRef = branch,
                SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-1", NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW",
                IntakeChannel = "Manual", InvoiceDate = new(2026,10,20), NetAmount = 1, GrossAmount = 1 };
            Line = new() { TenantId = Invoice.TenantId, LegalEntityId = company, SupplierInvoiceId = Invoice.Id,
                LineNumber = 1, Description = "Original", Quantity = 1, UnitPrice = 1 };
            Mapping = InvoiceMappingEvidence.Create(Invoice, [Line], Guid.NewGuid(), Guid.NewGuid(),
                [new(Line.Id, Guid.NewGuid(), null, null, "EACH", DocumentId, "page1-line1")], "maker", Guid.NewGuid(), at.AddMinutes(-10));
            Invoice.CurrentMappingVersionId = Mapping.Id; Invoice.MappingRevision = Mapping.MappingRevision;
            MatchingPolicy = Policy(InvoiceMatchingPolicyRules.PolicyKey, company, at);
            ReviewPolicy = Policy(InvoiceRetainedUnitReview.ReviewPolicyKey, company, at);
            var reviewId = Guid.NewGuid(); var reviewerWorker = Guid.NewGuid();
            Case = new() { TenantId = Invoice.TenantId, LegalEntityId = company, SourceModule = "procurement",
                SourceType = InvoiceRetainedUnitReview.ReviewSourceType, SourceId = reviewId, SourceVersion = 1,
                PolicyVersionId = ReviewPolicy.Id, PolicyVersionNumber = 1, MakerSubjectId = "maker", OpenedAt = at.AddMinutes(-5) };
            var stage = new ApprovalStage { TenantId = Invoice.TenantId, LegalEntityId = company, CaseId = Case.Id,
                Sequence = 1, Code = "DOCUMENT_REVIEW", State = "Active", Mode = "All", RequiredApprovals = 1 };
            Assignment = new() { TenantId = Invoice.TenantId, LegalEntityId = company, CaseId = Case.Id,
                StageId = stage.Id, AssigneeSubjectId = "reviewer", PermissionKey = InvoiceRetainedUnitReview.ReviewPermission };
            stage.Assignments.Add(Assignment); Case.Stages.Add(stage); ApprovalEngine.ValidateNewCase(Case);
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(Case, Assignment, new("reviewer", Invoice.TenantId, company,
                new HashSet<string> { InvoiceRetainedUnitReview.ReviewPermission }), "Approve", "review-decision", null, decisions, at);
            Decision = Assert.Single(decisions);
            Envelope = new(2, Invoice.TenantId, company, branch, reviewId, 1, Invoice.Id, Invoice.Version,
                Mapping.Id, Mapping.MappingRevision, Mapping.MappingSha256, Mapping.InvoiceOriginalSha256,
                Mapping.PurchaseOrderId, Mapping.IssuedVersionId, MatchingPolicy.Id, 1, Digest(MatchingPolicy.RulesJson),
                ReviewPolicy.Id, 1, Digest(ReviewPolicy.RulesJson), Case.Id, Decision.Id, "reviewer", reviewerWorker,
                "maker", Mapping.ActorWorkerId, at, [new(Line.Id, DocumentId, new string('a',64), "page1-line1", "EACH")],
                [new(Assignment.Id, "reviewer", reviewerWorker)]);
            var json = Serialize(Envelope);
            Authority = new(reviewId, 1, json, ProcurementEvidenceEncoding.Digest(json), Case, decisions,
                [new(Assignment.Id, "reviewer", reviewerWorker)], MatchingPolicy, ReviewPolicy);
            Consumer = new(Invoice.TenantId, company, branch, "different-matcher", Guid.NewGuid(),
                new HashSet<string> { "procurement-access", "procurement-invoice-match" }, at.AddMinutes(1),
                new Dictionary<Guid,string> { [DocumentId] = new string('a',64) }, branch, new HashSet<Guid> { branch }, false);
        }
        private PolicyVersion Policy(string key, Guid company, DateTimeOffset at) => new() {
            TenantId = Invoice.TenantId, LegalEntityId = company, PolicyKey = key, PolicyVersionNumber = 1,
            State = "Published", PublishedAt = at.AddDays(-1), EffectiveFrom = at.AddDays(-1), RulesJson = "{\"syntheticContractOnly\":true}" };
        public IReadOnlyDictionary<Guid,string> Verify() => InvoiceRetainedUnitReview.Verify(Authority, Invoice, [Line], Mapping, Consumer);
        public void Denied() => Assert.Equal("InvoiceRetainedUnitReviewInvalid", Assert.Throws<InvalidOperationException>(() => Verify()).Message);
        public void Replace(InvoiceRetainedUnitReviewEnvelope envelope) { Envelope = envelope; var json = Serialize(envelope);
            Authority = Authority with { EvidenceJson = json, EvidenceSha256 = ProcurementEvidenceEncoding.Digest(json) }; }
    }
    private static string Digest(string json) => ProcurementEvidenceEncoding.PolicyJsonDigest(json);
    private static string Serialize(InvoiceRetainedUnitReviewEnvelope envelope) => JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
