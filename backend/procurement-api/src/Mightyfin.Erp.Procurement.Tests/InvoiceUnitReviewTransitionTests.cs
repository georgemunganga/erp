using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Rules = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewPolicyRules;
using Flow = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewTransitions;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceUnitReviewTransitionTests
{
    [Fact]
    public void Plan_uses_real_Core_One_then_All_without_verifying_or_mutating_invoice_or_financial_claims()
    {
        var f = new Fixture(); var before = JsonSerializer.Serialize(f.Invoice); var plan = f.Open();
        Assert.Equal(plan.Case.Id, plan.Source.CoreCaseId); Assert.Equal(1, plan.Source.SourceVersion);
        Assert.Equal(4, plan.AssignmentWorkers.Count); Assert.Equal("Unverified", f.Mapping.VerificationState);
        var decisions = new List<ApprovalDecision>();
        foreach (var stage in plan.Case.Stages.OrderBy(x => x.Sequence))
        {
            var assignments = stage.Mode == "Any" ? stage.Assignments.Take(1).ToArray() : stage.Assignments.ToArray();
            foreach (var assignment in assignments)
            {
                var person = f.Policy.People.Single(x => x.PrincipalId == assignment.AssigneeSubjectId);
                Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching,
                    f.Policy.Policy, person, f.Hashes, assignment.Id,
                    new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version, assignment.Version, "Approve", null, f.Policy.Now.AddMinutes(1)), decisions);
            }
        }
        Assert.Equal("Approved", plan.Case.State); Assert.Equal(3, decisions.Count);
        Assert.Equal(before, JsonSerializer.Serialize(f.Invoice)); Assert.Equal("Unverified", f.Mapping.VerificationState);
        Assert.False(InvoiceHistoricalExposure.Unavailable().Complete); Assert.Null(f.Invoice.ActiveMatchClaimResultId);
        Assert.Null(f.Invoice.FinancePostingRef); // Core completion alone creates no v2 outcome/provider/AP claim.
    }

    [Theory]
    [InlineData("mapping")][InlineData("original")][InlineData("document")][InlineData("case")]
    [InlineData("policy")][InlineData("actor-alias")][InlineData("version")][InlineData("issued")]
    [InlineData("no-return-reason")]
    public void Stale_or_unbound_new_decision_is_denied_before_Core_mutation(string changed)
    {
        var f = new Fixture(); var plan = f.Open(); var stage = plan.Case.Stages.First(); var task = stage.Assignments.First();
        var actor = f.Policy.People.Single(x => x.PrincipalId == task.AssigneeSubjectId);
        var command = new Flow.DecisionCommand(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version, task.Version,
            "Approve", null, f.Policy.Now.AddMinutes(1));
        switch (changed)
        {
            case "mapping": f.Invoice.CurrentMappingVersionId = Guid.NewGuid(); break;
            case "original": f.Line.Quantity = 2; break;
            case "document": f.Hashes[f.Document] = new string('b', 64); break;
            case "case": plan.Case.Id = Guid.NewGuid(); break;
            case "policy": f.Policy.Policy.RulesJson = f.Policy.Policy.RulesJson.Replace("\"All\"", "\"One\""); break;
            case "actor-alias": actor = actor with { WorkerId = f.Policy.MakerWorker }; break;
            case "version": command = command with { ExpectedInvoiceVersion = f.Invoice.Version + 1 }; break;
            case "issued": f.Issued = f.Issued with { SupplierId = Guid.NewGuid() }; break;
            case "no-return-reason": command = command with { Action = "Return", Reason = "  " }; break;
        }
        var before = JsonSerializer.Serialize(plan.Case); var decisions = new List<ApprovalDecision>();
        Assert.ThrowsAny<InvalidOperationException>(() => Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping,
            f.Issued, f.Matching, f.Policy.Policy, actor, f.Hashes, task.Id, command, decisions));
        Assert.Equal(before, JsonSerializer.Serialize(plan.Case)); Assert.Empty(decisions);
        Assert.Null(f.Invoice.ActiveMatchClaimResultId); Assert.Null(f.Invoice.FinancePostingRef);
    }

    [Fact]
    public void Return_uses_existing_Core_nonverified_state_and_keeps_original_proposal()
    {
        var f = new Fixture(); var plan = f.Open(); var task = plan.Case.Stages.First().Assignments.First();
        var actor = f.Policy.People.Single(x => x.PrincipalId == task.AssigneeSubjectId); var decisions = new List<ApprovalDecision>();
        var result = Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy,
            actor, f.Hashes, task.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version,
                task.Version, "Return", "  Please inspect\n the line  ", f.Policy.Now.AddMinutes(1)), decisions);
        Assert.Equal("Returned", result.State); Assert.Equal("Please inspect the line", Assert.Single(decisions).Reason);
        Assert.Equal("Unverified", f.Mapping.VerificationState); Assert.Equal(f.Mapping.Id, f.Invoice.CurrentMappingVersionId);
    }

    [Fact]
    public void Operational_invoice_and_order_versions_can_advance_without_rewriting_immutable_issued_source()
    {
        var f = new Fixture(); var plan = f.Open(); f.Invoice.Version++; f.Issued = f.Issued with { Version = f.Issued.Version + 1 };
        var task = plan.Case.Stages.First().Assignments.First(); var actor = f.Policy.People.Single(x => x.PrincipalId == task.AssigneeSubjectId);
        Assert.True(Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy, actor,
            f.Hashes, task.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version, task.Version,
                "Approve", null, f.Policy.Now.AddMinutes(1)), new List<ApprovalDecision>()).Recorded);
    }

    [Fact]
    public void Completed_stage_signer_may_offboard_while_current_next_stage_worker_remains_authorized()
    {
        var f = new Fixture();
        f.Policy.Policy.RulesJson = JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "InvoiceUnitReview", stages = new[] {
            new { code = "FIRST", mode = "One", principals = new[] { f.Policy.First } },
            new { code = "SECOND", mode = "All", principals = new[] { f.Policy.Second } } } });
        var plan = f.Open(); var decisions = new List<ApprovalDecision>();
        var first = plan.Case.Stages.First().Assignments.Single();
        var firstActor = f.Policy.People.Single(x => x.PrincipalId == first.AssigneeSubjectId);
        Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy,
            firstActor, f.Hashes, first.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version,
                first.Version, "Approve", null, f.Policy.Now.AddSeconds(1)), decisions);
        // Current directory projection now says historical signer inactive/no grants. It is
        // deliberately not supplied as current authority to the next worker's new decision.
        var index = Array.FindIndex(f.Policy.People, x => x.PrincipalId == firstActor.PrincipalId);
        f.Policy.People[index] = firstActor with { Active = false, Permissions = new HashSet<string>() };
        var next = plan.Case.Stages.Last().Assignments.Single();
        var nextActor = f.Policy.People.Single(x => x.PrincipalId == next.AssigneeSubjectId);
        Flow.ApplyNewDecision(plan, f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, f.Policy.Policy,
            nextActor, f.Hashes, next.Id, new(Guid.NewGuid(), f.Invoice.Version, plan.Case.Version,
                next.Version, "Approve", null, f.Policy.Now.AddSeconds(2)), decisions);
        Assert.Equal("Approved", plan.Case.State); Assert.Equal(2, decisions.Count);
        Assert.Contains(plan.AssignmentWorkers, x => x.PrincipalId == firstActor.PrincipalId && x.WorkerId == firstActor.WorkerId);
        Assert.Equal("Unverified", f.Mapping.VerificationState); Assert.Null(f.Invoice.ActiveMatchClaimResultId);
    }

    [Fact]
    public void Expired_pinned_policy_blocks_new_effects_without_mutation_and_successor_requires_fresh_review_source()
    {
        var f = new Fixture(); var old = f.Open(); var task = old.Case.Stages.First().Assignments.First();
        var actor = f.Policy.People.Single(x => x.PrincipalId == task.AssigneeSubjectId);
        f.Policy.Policy.EffectiveTo = f.Policy.Now.AddSeconds(30);
        var before = JsonSerializer.Serialize(old.Case); var decisions = new List<ApprovalDecision>();
        Assert.Throws<InvalidOperationException>(() => Flow.ApplyNewDecision(old, f.Invoice, [f.Line], f.Mapping,
            f.Issued, f.Matching, f.Policy.Policy, actor, f.Hashes, task.Id,
            new(Guid.NewGuid(), f.Invoice.Version, old.Case.Version, task.Version, "Approve", null, f.Policy.Now.AddMinutes(1)), decisions));
        Assert.Equal(before, JsonSerializer.Serialize(old.Case)); Assert.Empty(decisions);
        var successor = new PolicyVersion { TenantId = f.Policy.Policy.TenantId, LegalEntityId = f.Policy.Company,
            PolicyKey = f.Policy.Policy.PolicyKey, PolicyVersionNumber = 2, State = "Published",
            EffectiveFrom = f.Policy.Now.AddSeconds(30), PublishedAt = f.Policy.Now.AddSeconds(30),
            CreatedBy = f.Policy.Policy.CreatedBy, PublishedBy = f.Policy.Policy.PublishedBy,
            PublicationReason = "Synthetic explicitly adopted successor", RulesJson = f.Policy.Policy.RulesJson };
        var fresh = Flow.PrepareOpen(f.Invoice, [f.Line], f.Mapping, f.Issued, f.Matching, successor, f.Maker,
            f.Policy.People, f.Hashes, new(Guid.NewGuid(), f.Invoice.Version, f.Mapping.MappingRevision,
                f.Policy.Now.AddMinutes(1), "New policy requires fresh review"));
        Assert.NotEqual(old.Source.ReviewId, fresh.Source.ReviewId); Assert.Equal(2, fresh.Source.ReviewPolicy.PolicyVersion);
        Assert.Equal(before, JsonSerializer.Serialize(old.Case)); // No fake cancellation/rewriting of original Core case.
    }

    private sealed class Fixture
    {
        public InvoiceUnitReviewPolicyRulesTests.Fixture Policy = new();
        public SupplierInvoice Invoice; public SupplierInvoiceLine Line; public InvoiceMatchMappingVersion Mapping;
        public InvoiceIssuedOrder Issued; public PolicyVersion Matching; public Guid Document = Guid.NewGuid();
        public Dictionary<Guid, string> Hashes; public Rules.Person Maker;
        public Fixture()
        {
            Invoice = new() { TenantId = "test", LegalEntityId = Policy.Company, BranchRef = Policy.Branch,
                SupplierId = Guid.NewGuid(), InvoiceNumber = "SYNTHETIC-REVIEW", NormalizedInvoiceNumber = "SYNTHETIC-REVIEW",
                CurrencyCode = "ZMW", NetAmount = 20, GrossAmount = 20, InvoiceDate = DateOnly.FromDateTime(Policy.Now.UtcDateTime) };
            Line = new() { TenantId = "test", LegalEntityId = Policy.Company, SupplierInvoiceId = Invoice.Id,
                LineNumber = 1, Description = "Review original", Quantity = 1, UnitPrice = 20 };
            var orderId = Guid.NewGuid(); var issuedId = Guid.NewGuid(); var orderLineId = Guid.NewGuid();
            Mapping = InvoiceMappingEvidence.Create(Invoice, [Line], orderId, issuedId,
                [new(Line.Id, orderLineId, null, null, "EACH", Document, "Page1 Line1")], Policy.Maker, Policy.MakerWorker, Policy.Now);
            Invoice.CurrentMappingVersionId = Mapping.Id; Invoice.MappingRevision = Mapping.MappingRevision;
            Issued = new(orderId, issuedId, 1, 4, "test", Policy.Company, Policy.Branch, Invoice.SupplierId, "ZMW",
                "Issued", "SYNTHETIC-ISSUED", Policy.Now.AddHours(-1), [new(orderLineId, 1, "IT", "EACH", 1, 20, 0)]);
            Matching = new() { TenantId = "test", LegalEntityId = Policy.Company, PolicyKey = "INVOICE_MATCHING",
                PolicyVersionNumber = 1, State = "Published", EffectiveFrom = Policy.Now.AddDays(-1), PublishedAt = Policy.Now.AddHours(-1),
                CreatedBy = Guid.NewGuid().ToString("D"), PublishedBy = Guid.NewGuid().ToString("D"), PublicationReason = "Synthetic rule owner",
                RulesJson = """
                {"schemaVersion":1,"transactionType":"InvoiceMatching","currencyCode":"ZMW","moneyScale":2,"rounding":"ToEven","rules":[
                {"categoryCode":"IT","method":"TwoWay","acceptanceKind":null,
                "quantity":{"absolute":"0","percent":"0","combination":"AllLimits"},
                "unitPrice":{"absolute":"0","percent":"0","combination":"AllLimits"},
                "tax":{"absolute":"0","percent":"0","combination":"AllLimits"},
                "total":{"absolute":"0","percent":"0","combination":"AllLimits"}}]}
                """ };
            Hashes = new() { [Document] = new string('a', 64) };
            Maker = new("test", Policy.Company, Policy.Maker, Policy.MakerWorker, true,
                new HashSet<string> { "procurement-access", Flow.RequestPermission }, false, new HashSet<Guid> { Policy.Branch });
        }
        public Flow.OpeningPlan Open() => Flow.PrepareOpen(Invoice, [Line], Mapping, Issued, Matching, Policy.Policy,
            Maker, Policy.People, Hashes, new(Guid.NewGuid(), Invoice.Version, Mapping.MappingRevision, Policy.Now, "Check units"));
    }
}
