using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundContractTests
{
    [Fact]
    public void Submitter_policy_pin_transport_contains_only_reference_and_version()
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new ProcurementSourcingExecutionRoutes.RoundPolicyPin(id, 3),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.EnumerateObject().Count());
        Assert.Equal(id, parsed.RootElement.GetProperty("policyVersionId").GetGuid());
        Assert.Equal(3, parsed.RootElement.GetProperty("policyVersionNumber").GetInt32());
        Assert.DoesNotContain("rules", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Round_policy_picker_requires_companywide_current_round_review_grant()
    {
        var right = new HashSet<string> { CommercialRoundReviewCoreWorkflow.ReviewPermission };
        Assert.True(ProcurementPolicyDraftRoutes.RoundPlanReviewerEligible(true, right));
        Assert.False(ProcurementPolicyDraftRoutes.RoundPlanReviewerEligible(false, right));
        Assert.False(ProcurementPolicyDraftRoutes.RoundPlanReviewerEligible(true,
            new HashSet<string> { "procurement-sourcing-publish", "procurement-plan-review" }));
    }

    [Theory]
    [InlineData("Create", true)]
    [InlineData("Revise", true)]
    [InlineData("Cancel", true)]
    [InlineData("Submit", false)]
    [InlineData("Approve", false)]
    [InlineData("Return", false)]
    [InlineData("Reject", false)]
    [InlineData("FutureOperation", false)]
    public void Legacy_actor_key_recovery_never_returns_review_purpose_or_unknown_receipts(string operation, bool expected)
        => Assert.Equal(expected, ProcurementSourcingExecutionRoutes.LegacyRoundRecoveryOperation(operation));

    [Fact]
    public void Round_review_sql_uses_the_real_quoted_Core_approval_columns()
    {
        var sql = Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.Install;
        var down = Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.RetainedDownGuard;
        foreach (var name in new[] { "TenantId", "LegalEntityId", "Id", "SourceModule", "SourceType",
                     "SourceId", "SourceVersion", "MakerSubjectId", "PolicyVersionId",
                     "PolicyVersionNumber", "State" })
            Assert.Contains("c.\"" + name + "\"", sql);
        foreach (var name in new[] { "TenantId", "LegalEntityId", "Id", "CaseId", "StageId",
                     "AssignmentId", "ActorSubjectId", "Action", "Reason", "DecidedAt" })
            Assert.Contains("d.\"" + name + "\"", sql);
        Assert.Contains("\"SourceModule\"='procurement'", down);
        Assert.Contains("\"SourceType\"='commercial_round_plan'", down);
        Assert.DoesNotContain("c.tenant_id", sql);
        Assert.DoesNotContain("d.case_id", sql);
        Assert.DoesNotContain("WHERE source_module=", down);
        Assert.Contains("JOIN core_approval.stages st", sql);
        Assert.Contains("JOIN core_approval.assignments a", sql);
        Assert.Contains("st.\"CaseId\"=c.\"Id\"", sql);
        Assert.Contains("a.\"CaseId\"=c.\"Id\"", sql);
        Assert.Contains("a.\"StageId\"=st.\"Id\"", sql);
        Assert.Contains("a.\"PermissionKey\"='procurement-round-plan-review'", sql);
        Assert.Contains("a.\"AssigneeSubjectId\"=d.\"ActorSubjectId\"", sql);
        Assert.Contains("jsonb_array_length(p.rules_json::jsonb->'assigneePrincipalIds')", sql);
    }

    [Fact]
    public void Round_review_Down_locks_every_retention_source_before_the_veto()
    {
        var guard = Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.RetainedDownGuard;
        var check = guard.IndexOf("DO $$ BEGIN IF EXISTS", StringComparison.Ordinal);
        var locks = guard[..check];
        Assert.StartsWith("LOCK TABLE ", locks);
        Assert.EndsWith(" IN ACCESS EXCLUSIVE MODE;\n", locks);
        foreach (var table in new[] { "commercial_round_plans", "commercial_round_plan_revisions",
                     "commercial_round_plan_receipts", "commercial_round_review_submissions",
                     "commercial_round_review_outcomes", "policy_versions",
                     "operational_policy_identity_evidence", "audit_events", "outbox_events" })
            Assert.Contains("procurement." + table, locks);
        Assert.Contains("core_approval.cases", locks);
        Assert.Equal(10, System.Text.RegularExpressions.Regex.Matches(locks,
            @"(?:procurement|core_approval)\.[a-z_]+").Count);
        Assert.Contains("USING ERRCODE='55000'", guard);
    }

    [Fact]
    public void Round_review_readiness_seals_the_deferred_completeness_function()
    {
        var capture = ProcurementCommercialRoundReviewReadiness.FingerprintSql;
        Assert.Contains("'commercial_round_review_complete'", capture);
        Assert.Contains("pg_get_functiondef(p.oid)", capture);
        foreach (var mode in new[] { false, true })
        {
            var privilege = ProcurementCommercialRoundReviewReadiness.PrivilegeSql("bounded_round_role", mode);
            Assert.Contains("'commercial_round_review_complete'", privilege);
            Assert.Contains("pg_has_role(current_user,p.proowner,'SET')", privilege);
            Assert.Contains("pg_has_role(current_user,p.proowner,'MEMBER')", privilege);
        }
    }

    [Fact]
    public void Round_review_requires_future_opening_and_unconflicted_required_team()
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var rules = new CommercialRoundReviewPolicyRules.Definition("Any", 1, [Guid.NewGuid()],
            [Guid.NewGuid()], ["*"], ["ZMW"], 1, ["Technical"], ["Planner"], false, false, false);
        var criterion = new RoundPlanCriterionInput(Guid.NewGuid(), 1, "TECH", "Technical", "PassFail", null, null, null, null, null);
        var planner = new RoundPlanParticipantInput(Guid.NewGuid(), 1, Guid.NewGuid().ToString("D"), "Planner", null);
        var draft = new RoundPlanDraftInput("Goods needed", "Policy", null, null,
            now.AddMinutes(1), now.AddHours(1), null, null, [criterion], [planner]);
        Assert.True(ProcurementSourcingExecutionRoutes.RoundReviewComplete(draft, rules, now));
        Assert.False(ProcurementSourcingExecutionRoutes.RoundReviewComplete(draft, rules, now.AddMinutes(1)));
        Assert.False(ProcurementSourcingExecutionRoutes.RoundReviewComplete(draft with
            { Participants = [planner with { ConflictNote = "Declared conflict" }] }, rules, now));
        Assert.False(ProcurementSourcingExecutionRoutes.RoundReviewComplete(draft with
            { Participants = [planner with { Duty = "Reviewer" }] }, rules, now));
    }

    [Theory]
    [InlineData("read")][InlineData("manage")][InlineData("submit")][InlineData("review")][InlineData("cancel")]
    public void Round_plan_actions_are_explicit_Core_grants_not_legacy_admin_inheritance(string suffix)
    {
        var action="procurement-round-plan-"+suffix;
        Assert.True(ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
        Assert.Empty(ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.Contains(ProcurementActionCatalog.AdminReadCatalog(),x=>x.Key==action&&x.Group=="Sourcing and RFQs");
        Assert.False(ProcurementPermissions.Allows(action,[new Claim(ClaimTypes.Role,"erp_admin")]));
        Assert.True(ProcurementPermissions.Allows(action,[new Claim(ProcurementPermissions.ConfiguredClaim,"true"),new Claim(ProcurementPermissions.ActionClaim,"procurement-access"),new Claim(ProcurementPermissions.ActionClaim,action)]));
    }
    [Theory]
    [InlineData("/api/procurement/v1/sourcing/round-plans","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/history","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/1","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/commands/22222222-2222-2222-2222-222222222222","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/round-plan/review-tasks","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/reviews/22222222-2222-2222-2222-222222222222","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/review-commands/22222222-2222-2222-2222-222222222222","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/submit-context","GET",false)]
    [InlineData("/api/procurement/v1/sourcing/round-plans","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/revise","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/decisions","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/0","GET",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/1/extra","GET",false)]
    [InlineData("/api/procurement/v1/sourcing/00000000-0000-0000-0000-000000000000/round-plan","GET",false)]
    public void Company_All_middleware_only_admits_exact_read_paths(string path,string method,bool expected)
        => Assert.Equal(expected,ProcurementSourcingExecutionRoutes.RoundPlanAllReadPath(new PathString(path),method));
    [Fact]
    public void Flags_default_off_after_native_capture_without_enabling_new_enrollment()
    {
        var options=new ProcurementCommercialRoundOptions();Assert.False(options.ReadEnabled);Assert.False(options.RecordEnabled);
        Assert.True(new ProcurementCommercialRoundReadiness(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).HasCapturedContract);
    }
    [Fact]
    public void Uncaptured_or_partial_native_contract_still_fails_closed()
    {
        Assert.False(CommercialRoundNativeContract.IsCaptured(null,null));
        Assert.False(CommercialRoundNativeContract.IsCaptured("",""));
        Assert.False(CommercialRoundNativeContract.IsCaptured(CommercialRoundNativeContract.MigrationId,""));
        Assert.False(CommercialRoundNativeContract.IsCaptured("",CommercialRoundNativeContract.Fingerprint));
        Assert.False(CommercialRoundNativeContract.IsCaptured(CommercialRoundNativeContract.MigrationId,new string('z',64)));
        Assert.True(CommercialRoundNativeContract.IsCaptured(CommercialRoundNativeContract.MigrationId,CommercialRoundNativeContract.Fingerprint));
    }
    [Fact]
    public void Captured_contract_binds_genuine_migration_and_actual_proposed_projection()
    {
        Assert.Equal("20261103163108_M7b1CommercialRoundDraftPlans",CommercialRoundNativeContract.MigrationId);
        Assert.Equal("24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b",CommercialRoundNativeContract.Fingerprint);
        Assert.NotEqual("0e0721e7e3313953a5d08c0894c31891283a5f09d97a79537c1b6f986a89a82d",CommercialRoundNativeContract.Fingerprint);
    }
    [Fact]
    public void Parent_key_projection_uses_ordered_column_names_not_restore_specific_ordinals_or_owners()
    {
        var sql=ProcurementCommercialRoundReadiness.FingerprintSql;
        Assert.Contains("'originalSourceKey'",sql);
        Assert.Contains("jsonb_agg(a.attname ORDER BY k.ord)",sql);
        Assert.Contains("c.convalidated,c.condeferrable,c.condeferred",sql);
        Assert.Contains("i.indisvalid,i.indisready,i.indislive,i.indisunique,i.indimmediate",sql);
        Assert.DoesNotContain("c.conname,c.contype,c.conkey",sql);
        Assert.DoesNotContain("pg_get_userbyid(pc.relowner)",sql);
        Assert.DoesNotContain("pg_get_userbyid(ic.relowner)",sql);
    }
    [Fact]
    public void Rollback_critical_trigger_and_function_security_states_are_part_of_captured_admission()
    {
        var sql=ProcurementCommercialRoundReadiness.FingerprintSql;
        Assert.Contains("c.relname,t.tgname,t.tgenabled,pg_get_triggerdef(t.oid)",sql);
        Assert.Contains("t.tgname='commercial_round_event_barrier'",sql);
        Assert.Contains("p.prosecdef,pg_get_functiondef(p.oid)",sql);
        Assert.Contains("i.indisvalid,i.indisready,i.indislive,i.indimmediate",sql);
    }
    [Fact]
    public void Record_requires_all_referenced_table_owners_schema_usage_but_retained_read_does_not()
    {
        var prerequisite=ProcurementCommercialRoundReadiness.ReferencedOwnersSchemaUsageSql;
        Assert.Contains("count(*)=6",prerequisite);
        Assert.Contains("has_schema_privilege(c.relowner,n.oid,'USAGE')",prerequisite);
        Assert.Contains("c.relkind='r'",prerequisite);
        foreach(var table in Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanSchema.Tables)
            Assert.Contains("'"+table+"'",prerequisite);
        Assert.Contains(prerequisite,ProcurementCommercialRoundReadiness.PrivilegeSql(true,"bounded_round_role"));
        Assert.DoesNotContain(prerequisite,ProcurementCommercialRoundReadiness.PrivilegeSql(false,"bounded_round_role"));
        Assert.DoesNotContain("'CREATE'",prerequisite);
    }

}
