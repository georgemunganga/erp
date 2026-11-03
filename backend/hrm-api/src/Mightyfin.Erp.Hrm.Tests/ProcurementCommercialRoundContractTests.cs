using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundContractTests
{
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
    [InlineData("/api/procurement/v1/sourcing/round-plans","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/revise","POST",false)]
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
}
