using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Rules = Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewPolicyRules;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure synthetic owner projections. No published policy, role grants or adapter is installed.</summary>
public sealed class InvoiceUnitReviewPolicyRulesTests
{
    [Fact]
    public void Explicit_named_One_All_policy_projects_deterministically_and_matches_actual_Core_shape()
    {
        var f = new Fixture(); var first = f.Resolve();
        var reversed = Rules.Resolve(f.Policy, "test", f.Company, f.Branch, f.Maker, f.MakerWorker,
            f.People.Reverse().ToArray(), f.Now);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(reversed));
        Assert.Equal("Any", first.Stages[0].CoreMode); Assert.Equal(1, first.Stages[0].RequiredApprovals);
        Assert.Equal("All", first.Stages[1].CoreMode); Assert.Equal(2, first.Stages[1].RequiredApprovals);
        Rules.VerifyCaseGraph(first, f.Case(first), f.Workers);
    }

    [Theory]
    [InlineData("unknown")][InlineData("duplicate")][InlineData("case")][InlineData("no-stage")]
    [InlineData("quorum")][InlineData("duplicate-member")][InlineData("principal")][InlineData("huge")]
    [InlineData("nested")][InlineData("duplicate-code")]
    public void Malformed_or_unsupported_policy_never_creates_an_implicit_reviewer(string changed)
    {
        var f = new Fixture(); var raw = f.Policy.RulesJson;
        raw = changed switch
        {
            "unknown" => raw.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"minimumAmount\":0"),
            "duplicate" => raw.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
            "case" => raw.Replace("\"schemaVersion\"", "\"SchemaVersion\""),
            "no-stage" => "{\"schemaVersion\":1,\"transactionType\":\"InvoiceUnitReview\",\"stages\":[]}",
            "quorum" => raw.Replace("\"One\"", "\"Quorum\""),
            "duplicate-member" => raw.Replace(f.Second, f.First),
            "principal" => raw.Replace(f.First, "role:procurement_manager"),
            "huge" => new string(' ', 65537) + raw,
            "nested" => new string('[', 17) + "1" + new string(']', 17),
            "duplicate-code" => raw.Replace("SECOND", "FIRST"),
            _ => raw,
        };
        Assert.Equal("InvoiceUnitReviewPolicyUnavailable", Assert.Throws<InvalidOperationException>(() => Rules.Parse(raw)).Message);
    }

    [Theory]
    [InlineData("alias-maker")][InlineData("alias-quorum")][InlineData("permission")][InlineData("branch")]
    [InlineData("company")][InlineData("inactive")][InlineData("missing-person")][InlineData("unpublished")]
    [InlineData("expired")][InlineData("matching-policy")]
    public void Current_named_worker_authority_and_real_review_policy_are_required(string changed)
    {
        var f = new Fixture(); var people = f.People.ToArray();
        switch (changed)
        {
            case "alias-maker": people[0] = people[0] with { WorkerId = f.MakerWorker }; break;
            case "alias-quorum": people[1] = people[1] with { WorkerId = people[0].WorkerId }; break;
            case "permission": people[0] = people[0] with { Permissions = new HashSet<string> { "procurement-access" } }; break;
            case "branch": people[0] = people[0] with { OrganisationWide = false, AllowedBranches = new HashSet<Guid> { Guid.NewGuid() } }; break;
            case "company": people[0] = people[0] with { LegalEntityId = Guid.NewGuid() }; break;
            case "inactive": people[0] = people[0] with { Active = false }; break;
            case "missing-person": people = people.Take(1).ToArray(); break;
            case "unpublished": f.Policy.State = "Draft"; break;
            case "expired": f.Policy.EffectiveTo = f.Now; break;
            case "matching-policy": f.Policy.PolicyKey = "INVOICE_MATCHING"; break;
        }
        Assert.Throws<InvalidOperationException>(() => Rules.Resolve(f.Policy, "test", f.Company,
            f.Branch, f.Maker, f.MakerWorker, people, f.Now));
    }

    [Theory]
    [InlineData("weaker-quorum")][InlineData("mode")][InlineData("worker")][InlineData("principal")]
    [InlineData("permission")][InlineData("stage")][InlineData("policy")]
    public void Core_case_mentioning_right_policy_cannot_substitute_a_different_execution_graph(string changed)
    {
        var f = new Fixture(); var projection = f.Resolve(); var workflow = f.Case(projection);
        var stage = workflow.Stages.Last(); var assignment = stage.Assignments.First(); var workers = f.Workers.ToArray();
        switch (changed)
        {
            case "weaker-quorum": stage.RequiredApprovals = 1; break;
            case "mode": stage.Mode = "Any"; break;
            case "worker": workers[0] = workers[0] with { WorkerId = Guid.NewGuid() }; break;
            case "principal": assignment.AssigneeSubjectId = Guid.NewGuid().ToString("D"); break;
            case "permission": assignment.PermissionKey = "procurement-approval-decide"; break;
            case "stage": stage.Code = "UNDECLARED"; break;
            case "policy": workflow.PolicyVersionId = Guid.NewGuid(); break;
        }
        Assert.Throws<InvalidOperationException>(() => Rules.VerifyCaseGraph(projection, workflow, workers));
    }

    internal sealed class Fixture
    {
        public Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), MakerWorker = Guid.NewGuid();
        public string Maker = Guid.NewGuid().ToString("D"), First = Guid.NewGuid().ToString("D"), Second = Guid.NewGuid().ToString("D");
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
        public PolicyVersion Policy;
        public Rules.Person[] People;
        public List<InvoiceUnitReviewAssignmentWorker> Workers = [];
        public Fixture()
        {
            Policy = new() { TenantId = "test", LegalEntityId = Company, PolicyKey = InvoiceRetainedUnitReview.ReviewPolicyKey,
                PolicyVersionNumber = 1, State = "Published", EffectiveFrom = Now.AddDays(-1), PublishedAt = Now.AddHours(-1),
                CreatedBy = Guid.NewGuid().ToString("D"), PublishedBy = Guid.NewGuid().ToString("D"), PublicationReason = "Synthetic parser fixture",
                RulesJson = JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "InvoiceUnitReview", stages = new[] {
                    new { code = "FIRST", mode = "One", principals = new[] { First, Second } },
                    new { code = "SECOND", mode = "All", principals = new[] { Second, First } } } }) };
            People = new[] { First, Second }.Select(x => new Rules.Person("test", Company, x, Guid.NewGuid(), true,
                new HashSet<string> { "procurement-access", InvoiceRetainedUnitReview.ReviewPermission }, false, new HashSet<Guid> { Branch })).ToArray();
        }
        public Rules.Projection Resolve() => Rules.Resolve(Policy, "test", Company, Branch, Maker, MakerWorker, People, Now);
        public ApprovalCase Case(Rules.Projection projection)
        {
            Workers.Clear();
            var result = new ApprovalCase { TenantId = "test", LegalEntityId = Company, SourceModule = "procurement",
                SourceType = InvoiceRetainedUnitReview.ReviewSourceType, SourceId = Guid.NewGuid(), SourceVersion = 1,
                PolicyVersionId = Policy.Id, PolicyVersionNumber = Policy.PolicyVersionNumber, MakerSubjectId = Maker };
            foreach (var row in projection.Stages)
            {
                var stage = new ApprovalStage { TenantId = "test", LegalEntityId = Company, CaseId = result.Id,
                    Sequence = row.Sequence, Code = row.Code, Mode = row.CoreMode, RequiredApprovals = row.RequiredApprovals };
                foreach (var person in row.Assignees)
                {
                    var assignment = new ApprovalAssignment { TenantId = "test", LegalEntityId = Company, CaseId = result.Id,
                        StageId = stage.Id, AssigneeSubjectId = person.PrincipalId, PermissionKey = InvoiceRetainedUnitReview.ReviewPermission };
                    stage.Assignments.Add(assignment); Workers.Add(new(assignment.Id, person.PrincipalId, person.WorkerId));
                }
                result.Stages.Add(stage);
            }
            return result;
        }
    }
}
