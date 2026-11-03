using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CommercialRoundPlanDraftTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Actor = Guid.NewGuid().ToString("D");
    private static readonly RoundPlanSource Source = new("round-tests", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, new string('a', 64), Guid.NewGuid().ToString("D"));
    private static RoundPlanDraftInput Empty() => new(null, null, null, null, null, null, null, null, [], []);
    private static RoundPlanCriterionInput Criterion(int order = 1) => new(Guid.NewGuid(), order, "C" + order, "Technical", "Weighted", null, null, 1m, 10m, 0m);
    private static RoundPlanParticipantInput Participant(int order = 1) => new(Guid.NewGuid(), order, Actor, "Planner", null);
    private static RoundPlanChange Create(RoundPlanDraftInput? input = null) => CommercialRoundPlanDraft.Create(Source,
        new(Actor, null), input ?? Empty(), "UTC", Guid.NewGuid(), new string('b', 64), At);

    [Fact]
    public void Incomplete_draft_can_be_created_by_current_non_original_workerless_principal_without_policy()
    {
        var change = Create();
        Assert.NotEqual(Source.OriginalMakerPrincipalId, change.Plan.MakerPrincipalId);
        Assert.Equal(Actor, change.Enrollment!.EnrolledByPrincipalId); Assert.Null(change.Enrollment.EnrolledByWorkerAlias);
        Assert.Null(change.Revision.ProposedPolicyReference); Assert.Empty(change.Criteria); Assert.Empty(change.Participants);
        Assert.Equal(1, change.Revision.ResultVersion); Assert.Equal("Draft", change.Plan.Status);
        Assert.Equal(201, change.Receipt.ResponseStatusCode);
        CommercialRoundPlanDraft.VerifyReceipt(change.Receipt, change.Plan, change.Revision);
    }

    [Theory]
    [InlineData("null-criteria")][InlineData("null-participants")][InlineData("null-criterion")][InlineData("null-participant")]
    [InlineData("duplicate-logical")][InlineData("duplicate-order")][InlineData("duplicate-code")][InlineData("duplicate-duty")]
    [InlineData("zero-id")][InlineData("foreign-principal")][InlineData("weighted-passfail")][InlineData("negative")]
    [InlineData("fraction")][InlineData("overflow")][InlineData("range")][InlineData("unknown-duty")][InlineData("unknown-stage")]
    [InlineData("blank-text")][InlineData("unknown-kind")][InlineData("long-text")]
    public void Invalid_or_contradictory_drafts_are_rejected_before_graph_is_returned(string fault)
    {
        var c = Criterion(); var p = Participant(); var input = Empty() with { Criteria = [c], Participants = [p] };
        input = fault switch {
            "null-criteria" => input with { Criteria = null! }, "null-participants" => input with { Participants = null! },
            "null-criterion" => input with { Criteria = [null!] }, "null-participant" => input with { Participants = [null!] },
            "duplicate-logical" => input with { Criteria = [c, c with { Order = 2, Code = "C2" }] },
            "duplicate-order" => input with { Criteria = [c, Criterion() with { Code = "C2" }] },
            "duplicate-code" => input with { Criteria = [c, Criterion(2) with { Code = "c1" }] },
            "duplicate-duty" => input with { Participants = [p, p with { LogicalId = Guid.NewGuid(), Order = 2 }] },
            "zero-id" => input with { Criteria = [c with { LogicalId = Guid.Empty }] },
            "foreign-principal" => input with { Participants = [p with { PrincipalId = "not-an-account" }] },
            "weighted-passfail" => input with { Criteria = [c with { Kind = "PassFail" }] },
            "negative" => input with { Criteria = [c with { Weight = -1m }] },
            "fraction" => input with { Criteria = [c with { Weight = .00001m }] },
            "overflow" => input with { Criteria = [c with { Weight = 100000000000000m }] },
            "range" => input with { Criteria = [c with { MinimumScore = 11m }] },
            "unknown-duty" => input with { Participants = [p with { Duty = "Approver" }] },
            "unknown-stage" => input with { Criteria = [c with { Stage = "Award" }] },
            "unknown-kind" => input with { Criteria = [c with { Kind = "Automatic" }] },
            "blank-text" => input with { Requirements = " " }, "long-text" => input with { ProposedPolicyReference = new string('a',201) },
            _ => throw new Exception("Unknown test") };
        Assert.Throws<InvalidOperationException>(() => Create(input));
    }

    [Theory]
    [InlineData("offset")][InlineData("submicrosecond")][InlineData("close-before-open")][InlineData("release-before-close")]
    public void Draft_schedule_rejects_ambiguous_or_contradictory_instants(string fault)
    {
        var input = Empty() with { OpensAt = At, ClosesAt = At.AddDays(1) };
        input = fault switch { "offset" => input with { OpensAt = At.ToOffset(TimeSpan.FromHours(2)) },
            "submicrosecond" => input with { OpensAt = At.AddTicks(1) },
            "close-before-open" => input with { ClosesAt = At.AddDays(-1) },
            "release-before-close" => input with { TechnicalReleaseAt = At }, _ => input };
        Assert.Throws<InvalidOperationException>(() => Create(input));
    }

    [Fact]
    public void Canonical_content_orders_logical_children_and_exact_decimals_without_mutating_input()
    {
        var a = Criterion(1); var b = Criterion(2); var first = Empty() with { Criteria = [b, a] };
        var before = JsonSerializer.Serialize(first); var change = Create(first);
        Assert.Equal(before, JsonSerializer.Serialize(first));
        Assert.Equal(change.Revision.ContentJson, CommercialRoundPlanDraft.Content(first with { Criteria = [a with { Weight = 1.0000m }, b] }, "UTC"));
        Assert.Equal(change.Revision.ContentJson, CommercialRoundPlanDraft.Content(CommercialRoundPlanDraft.Restore(change.Revision, change.Criteria, change.Participants), "UTC"));
    }

    [Fact]
    public void Revisions_preserve_logical_identity_but_append_new_physical_rows_and_support_removal()
    {
        var first = Create(Empty() with { Criteria = [Criterion()] }); var old = JsonSerializer.Serialize(first.Revision);
        var input = CommercialRoundPlanDraft.Restore(first.Revision, first.Criteria, first.Participants);
        var next = CommercialRoundPlanDraft.Revise(first.Plan, first.Revision, 1, new(Actor,null), input,
            "UTC", "Revise", null, Guid.NewGuid(), new string('c',64), At.AddHours(1));
        Assert.Equal(first.Criteria[0].LogicalId, next.Criteria[0].LogicalId); Assert.NotEqual(first.Criteria[0].Id, next.Criteria[0].Id);
        Assert.Equal(first.Revision.Id, next.Revision.PreviousRevisionId); Assert.Equal(2,next.Revision.ResultVersion);
        Assert.Equal(first.Revision.ContentSha256,next.Revision.ContentSha256); Assert.Equal(old,JsonSerializer.Serialize(first.Revision));
        Assert.Empty(CommercialRoundPlanDraft.Revise(first.Plan,first.Revision,1,new(Actor,null),Empty(),"UTC","Revise",null,Guid.NewGuid(),new string('c',64),At).Criteria);
    }

    [Theory]
    [InlineData("stale")][InlineData("cancel-no-reason")][InlineData("cancel-content-change")][InlineData("cancelled-plan")][InlineData("backdate")]
    public void Revision_CAS_cancel_and_time_rules_are_explicit(string fault)
    {
        var first = Create(); if (fault == "cancelled-plan") first.Plan.Status = "Cancelled";
        Assert.Throws<InvalidOperationException>(() => CommercialRoundPlanDraft.Revise(first.Plan,first.Revision,
            fault == "stale" ? 2 : 1,new(Actor,null),fault == "cancel-content-change" ? Empty() with { Requirements="changed" } : Empty(),"UTC",
            fault.StartsWith("cancel-") ? "Cancel" : "Revise",fault == "cancel-content-change" ? "No longer needed" : null,
            Guid.NewGuid(),new string('c',64),fault == "backdate" ? At.AddSeconds(-1) : At));
    }

    [Fact]
    public void Cancel_retains_exact_content_and_records_reason_without_releasing_source_holds()
    {
        var first = Create(); var next = CommercialRoundPlanDraft.Revise(first.Plan,first.Revision,1,new(Actor,null),Empty(),"UTC","Cancel","No longer needed",Guid.NewGuid(),new string('c',64),At);
        Assert.Equal("Cancelled",next.Revision.ResultStatus); Assert.Equal("Draft",first.Plan.Status);
        Assert.Equal(first.Revision.ContentJson,next.Revision.ContentJson); Assert.Null(next.Enrollment);
        CommercialRoundPlanDraft.VerifyReceipt(next.Receipt,next.Plan,next.Revision);
    }

    [Fact]
    public void Rehashed_receipt_with_forged_action_cannot_claim_success()
    {
        var first=Create(); var node=JsonNode.Parse(JsonSerializer.Serialize(first.Receipt))!;
        var response=JsonNode.Parse(first.Receipt.ResponseJson)!;response["publicationAvailable"]=true;
        node["ResponseJson"]=response.ToJsonString();node["ResponseSha256"]=ProcurementEvidenceEncoding.Digest(response.ToJsonString());
        var forged=node.Deserialize<CommercialRoundPlanReceipt>()!;
        Assert.Throws<InvalidOperationException>(()=>CommercialRoundPlanDraft.VerifyReceipt(forged,first.Plan,first.Revision));
    }

    [Fact]
    public async Task Owning_stage_saves_workerless_graph_and_unstaged_edits_are_rejected()
    {
        var first=Create(); await using var db=Context();db.StageCommercialRoundPlan(first);await db.SaveChangesAsync();
        Assert.Equal(1,await db.CommercialRoundEnrollments.CountAsync());Assert.Equal(1,first.Plan.Version);
        first.Plan.Status="Cancelled";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
    }

    [Theory][InlineData("tenant")][InlineData("company")]
    public void AddScoped_does_not_relabel_foreign_plan_scope(string fault)
    {
        var first=Create();using var db=Context();if(fault=="tenant") first.Plan.TenantId="other";else first.Plan.LegalEntityId=Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(()=>db.AddScoped(first.Plan));Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Actual_EF_model_uses_scoped_previous_revision_identity_and_separate_logical_keys()
    {
        using var db=Context();var revision=db.Model.FindEntityType(typeof(CommercialRoundPlanRevision))!;
        var previous=Assert.Single(revision.GetForeignKeys(),x=>x.PrincipalEntityType.ClrType==typeof(CommercialRoundPlanRevision));
        Assert.Equal(new[]{"TenantId","LegalEntityId","PlanId","PreviousRevisionId","ExpectedVersion"},previous.Properties.Select(x=>x.Name));
        Assert.Equal(DeleteBehavior.Restrict,previous.DeleteBehavior);
        var criterion=db.Model.FindEntityType(typeof(CommercialRoundCriterion))!;
        Assert.Equal(18,criterion.FindProperty("Weight")!.GetPrecision());Assert.Equal(4,criterion.FindProperty("Weight")!.GetScale());
        Assert.Contains(criterion.GetIndexes(),x=>x.IsUnique&&x.Properties.Any(p=>p.Name=="LogicalId"));
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(SourcingDraftEvidence))!.GetKeys().SelectMany(x=>x.Properties),x=>x.Name=="BranchRef");
    }
    [Theory]
    [InlineData("scope")][InlineData("count")][InlineData("logical")][InlineData("snapshot")]
    public void Typed_children_and_retained_content_must_reconstruct_the_same_revision(string fault)
    {
        var first=Create(Empty() with { Criteria=[Criterion()] });
        var revision=first.Revision;var child=first.Criteria[0];
        if(fault=="scope") child.TenantId="foreign";
        else
        {
            var node=JsonNode.Parse(JsonSerializer.Serialize(revision))!;
            if(fault=="count") node["CriterionCount"]=0;
            if(fault=="snapshot") { node["ContentJson"]="{}";node["ContentSha256"]=ProcurementEvidenceEncoding.Digest("{}"); }
            if(fault=="logical")
            {
                var item=JsonNode.Parse(JsonSerializer.Serialize(child))!;item["LogicalId"]=Guid.NewGuid();
                child=item.Deserialize<CommercialRoundCriterion>()!;
            }
            revision=node.Deserialize<CommercialRoundPlanRevision>()!;
        }
        Assert.Throws<InvalidOperationException>(()=>CommercialRoundPlanDraft.Restore(revision,[child],[]));
    }

    [Fact]
    public async Task Staged_create_cannot_be_downgraded_by_removing_its_exact_receipt_before_save()
    {
        var first=Create();await using var db=Context();db.StageCommercialRoundPlan(first);db.Entry(first.Receipt).State=EntityState.Detached;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        Assert.Empty(await db.CommercialRoundEnrollments.AsNoTracking().ToArrayAsync());
    }

    private static ProcurementDbContext Context()=>new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new Scope());
    private sealed class Scope:IProcurementScope
    {
        public string TenantId=>Source.TenantId; public Guid LegalEntityId=>Source.CompanyId; public Guid? WorkLocationId=>Source.BranchId;
        public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string SubjectId=>Actor;public string? CorrelationId=>"round-test";
    }
}
