using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Hrm.Tests;

// Source contract/DI tests. Fake transaction objects never establish PostgreSQL
// admission, and synthetic definition strings are not physical capture evidence.
public sealed class PrincipalGoodsReturnReadinessTests
{
    private sealed class ClaimingReadiness : IProcurementPrincipalGoodsReturnReadiness
    {
        public Task<bool> IsConfiguredAsync(CancellationToken ct)=>Task.FromResult(true);
        public Task<bool> IsReadyAsync(ProcurementDbContext db,bool write,CancellationToken ct)=>Task.FromResult(true);
    }

    private sealed class SourceReadiness : IProcurementPrincipalReceivingReadiness
    {
        internal bool? Write;
        internal ProcurementDbContext? Owner;
        public Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(false);
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
        { Owner=owner;Write=write;return Task.FromResult(false); }
    }

    [Fact]
    public async Task Return_write_foundation_requests_only_source_read_on_supplied_owner()
    {
        var source=new SourceReadiness();
        Assert.False(await ProcurementPrincipalGoodsReturnStoreReadiness.SourceReadReadyAsync(source,null!,default));
        Assert.False(source.Write);Assert.Null(source.Owner);
    }

    [Fact]
    public async Task Held_composition_defaults_off_and_never_trusts_configured_connection_boolean()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["Procurement:PrincipalGoodsReturnDrafts:ExecutionDatabaseConfigured"]="true" }).Build();
        var services=new ServiceCollection();services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IProcurementPrincipalReceivingReadiness>(new SourceReadiness());
        ProcurementPrincipalGoodsReturnComposition.AddFoundation(services,configuration);
        using var provider=services.BuildServiceProvider();
        var options=provider.GetRequiredService<ProcurementPrincipalGoodsReturnOptions>();
        Assert.False(options.Enabled);Assert.False(options.ExecutionDatabaseConfigured);
        var ready=provider.GetRequiredService<IProcurementPrincipalGoodsReturnReadiness>();
        Assert.IsType<ProcurementPrincipalGoodsReturnStoreReadiness>(ready);
        Assert.False(await ready.IsConfiguredAsync(default));
        Assert.False(await ready.IsReadyAsync(null!,true,default));
    }

    [Fact]
    public async Task Captured_feature_and_connection_still_deny_without_execution_foundation()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Procurement:PrincipalGoodsReturnDrafts:Enabled"]="true",
            ["ConnectionStrings:ProcurementApprovalExecution"]="Host=never-used.invalid;Database=private;Username=finite",
        }).Build();
        var services=new ServiceCollection();services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IProcurementPrincipalReceivingReadiness>(new SourceReadiness());
        ProcurementPrincipalGoodsReturnComposition.AddFoundation(services,configuration);
        using var provider=services.BuildServiceProvider();
        Assert.True(ProcurementPrincipalGoodsReturnSchemaCapture.Populated);
        Assert.False(await provider.GetRequiredService<IProcurementPrincipalGoodsReturnReadiness>().IsReadyAsync(null!,false,default));
        Assert.False(await provider.GetRequiredService<IProcurementPrincipalGoodsReturnReadiness>().IsConfiguredAsync(default));
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public async Task Normal_host_capability_stays_closed_without_explicit_execution_authority(
        bool enabled,bool executionConnection)
    {
        var values=new Dictionary<string,string?>
        {
            ["Procurement:PrincipalGoodsReturnDrafts:Enabled"]=enabled.ToString(),
            ["Procurement:Approvals:Enabled"]="true",
            ["Procurement:Approvals:PrincipalExecutionEnabled"]="false"
        };
        if(executionConnection)
            values["ConnectionStrings:ProcurementApprovalExecution"]="Host=never-used.invalid;Database=private;Username=finite";
        var config=new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services=new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        ProcurementHostIntegration.AddServices(services,"Host=never-used.invalid;Database=record",config);
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
        var options=scope.ServiceProvider.GetRequiredService<ProcurementPrincipalGoodsReturnOptions>();
        Assert.Equal(enabled,options.Enabled);
        Assert.Equal(executionConnection,options.ExecutionDatabaseConfigured);
        Assert.IsType<ProcurementPrincipalGoodsReturnStoreReadiness>(
            scope.ServiceProvider.GetRequiredService<IProcurementPrincipalGoodsReturnReadiness>());
        // The third case has an execution string but no principal execution
        // authority; no network connection is attempted by this test.
        Assert.False(await ProcurementPrincipalGoodsReturnComposition.ReadyAsync(scope.ServiceProvider,false,default));
    }

    [Fact]
    public async Task Capability_rejects_substituted_ready_provider_and_foreign_execution_database()
    {
        var services=new ServiceCollection();
        services.AddSingleton(new ProcurementPrincipalGoodsReturnOptions { Enabled=true,ExecutionDatabaseConfigured=true });
        services.AddSingleton(new ProcurementApprovalExecutionOptions
            { Enabled=true,PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true });
        services.AddSingleton<IProcurementPrincipalGoodsReturnReadiness>(new ClaimingReadiness());
        using var provider=services.BuildServiceProvider();
        Assert.False(await ProcurementPrincipalGoodsReturnComposition.ReadyAsync(provider,false,default));
        Assert.False(await ProcurementPrincipalGoodsReturnComposition.ReadyAsync(provider,true,default));
        Assert.False(InvoiceDocumentStoreBinding.SamePhysicalDatabase(
            "Host=private-socket;Database=return-source;Username=finite",
            "Host=private-socket;Database=other-tenant;Username=finite"));
    }

    [Fact]
    public void Own_write_matrix_does_not_require_source_counter_or_Core_workflow_writes()
    {
        var sql=ProcurementPrincipalGoodsReturnStoreReadiness.PrivilegeSql(true);
        foreach(var table in ProcurementPrincipalGoodsReturnSchemaCapture.Tables)
            Assert.Contains("has_table_privilege(current_user,'"+table+"','INSERT')",sql);
        Assert.Contains("has_column_privilege(current_user,'procurement.principal_goods_return_drafts','current_revision_id','UPDATE')",sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.principal_goods_return_revisions','UPDATE')",sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.principal_goods_return_receipts','UPDATE')",sql);
        Assert.Contains("NOT(a.attname::text=ANY(",sql);
        Assert.Contains("rolbypassrls",sql);Assert.Contains("pg_has_role",sql);
        Assert.DoesNotContain("principal_receiving_",sql);
        Assert.DoesNotContain("purchase_order_lines",sql);Assert.DoesNotContain("purchase_request_lines",sql);
        Assert.DoesNotContain("core_approval",sql);
        var read=ProcurementPrincipalGoodsReturnStoreReadiness.PrivilegeSql(false);
        Assert.DoesNotContain(",'INSERT')",read);
        Assert.DoesNotContain("has_column_privilege(current_user,'procurement.principal_goods_return_drafts'",read);
    }

    [Fact]
    public void Complete_capture_requires_exact_all_owned_and_overlay_objects()
    {
        var checks=ProcurementPrincipalGoodsReturnSchemaCapture.Checks.ToDictionary(x=>x.Key,x=>x.Value);
        var foreign=ProcurementPrincipalGoodsReturnSchemaCapture.ForeignDefinitions.ToDictionary(x=>x.Key,x=>x.Value);
        var keys=ProcurementPrincipalGoodsReturnSchemaCapture.OverlayKeys.ToDictionary(x=>x.Key,x=>x.Value);
        var indexes=ProcurementPrincipalGoodsReturnSchemaCapture.IndexDefinitions.ToDictionary(x=>x.Key,x=>x.Value);
        Assert.Equal(3,checks.Count);Assert.Equal(12,foreign.Count);Assert.Equal(4,keys.Count);Assert.Equal(19,indexes.Count);
        Assert.True(ProcurementPrincipalGoodsReturnSchemaCapture.Complete(checks,foreign,keys,indexes));
        indexes.Remove(indexes.Keys.First());
        Assert.False(ProcurementPrincipalGoodsReturnSchemaCapture.Complete(checks,foreign,keys,indexes));
        indexes=ProcurementPrincipalGoodsReturnSchemaCapture.IndexDefinitions.ToDictionary(x=>x.Key,x=>x.Value);foreign[foreign.Keys.First()]="";
        Assert.False(ProcurementPrincipalGoodsReturnSchemaCapture.Complete(checks,foreign,keys,indexes));
        Assert.True(ProcurementPrincipalGoodsReturnSchemaCapture.Populated);
    }

    [Fact]
    public void Capture_names_follow_actual_genuine_operations_and_deferred_current_owner_contract()
    {
        var ops=new P37PrincipalGoodsReturnDrafts().UpOperations;
        var tables=ops.OfType<CreateTableOperation>().ToArray();
        var generated=tables.SelectMany(t=>t.ForeignKeys.Select(f=>"procurement."+t.Name+"."+f.Name));
        Assert.All(generated,n=>Assert.Contains(n,ProcurementPrincipalGoodsReturnSchemaCapture.ForeignNames));
        var indexes=ops.OfType<CreateIndexOperation>().Select(i=>i.Name)
            .Concat(tables.Select(t=>t.PrimaryKey!.Name))
            .Concat(tables.SelectMany(t=>t.UniqueConstraints.Select(k=>k.Name)))
            .Append("ak_pgret_current_revision").ToHashSet(StringComparer.Ordinal);
        Assert.True(indexes.SetEquals(ProcurementPrincipalGoodsReturnSchemaCapture.IndexNames));
        var manifest=ProcurementPrincipalGoodsReturnStoreReadiness.OwnershipManifestSql;
        Assert.Contains("'fk_pgret_current_revision'",manifest);Assert.Contains("c.condeferrable=e.deferred",manifest);
        Assert.Contains("c.condeferred=e.deferred",manifest);Assert.Contains("c.confdeltype='a'",manifest);
    }

    private sealed class Connection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; }="";
        public override string Database=>"never-opened";public override string DataSource=>"never-opened";
        public override string ServerVersion=>"none";public override ConnectionState State=>ConnectionState.Closed;
        public override void ChangeDatabase(string value)=>throw new InvalidOperationException("Uncalled");
        public override void Close() { }
        public override void Open()=>throw new InvalidOperationException("Uncalled");
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)=>throw new InvalidOperationException("Uncalled");
        protected override DbCommand CreateDbCommand()=>throw new InvalidOperationException("Uncalled");
    }
    private sealed class Transaction(DbConnection connection,IsolationLevel level) : DbTransaction
    {
        public override IsolationLevel IsolationLevel=>level;
        protected override DbConnection DbConnection=>connection;
        public override void Commit()=>throw new InvalidOperationException("Uncalled");
        public override void Rollback()=>throw new InvalidOperationException("Uncalled");
    }
    [Fact]
    public void Every_purpose_requires_the_actual_same_connection_serializable_transaction()
    {
        using var first=new Connection();using var second=new Connection();
        using var serial=new Transaction(first,IsolationLevel.Serializable);
        using var weak=new Transaction(first,IsolationLevel.ReadCommitted);
        Assert.True(ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(serial,first));
        Assert.False(ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(serial,second));
        Assert.False(ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(weak,first));
        Assert.False(ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(null,first));
    }
}
