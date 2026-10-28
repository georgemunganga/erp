using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class PrincipalGoodsReturnReadinessTests
{
    private sealed class SourceReadiness:IProcurementPrincipalReceivingReadiness
    {
        internal bool? Write;
        internal ProcurementDbContext? Owner;
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
        {Owner=owner;Write=write;return Task.FromResult(false);}
    }
    [Fact]
    public async Task Return_foundation_requires_source_read_on_the_actual_owner()
    {
        var source=new SourceReadiness();
        Assert.False(await ProcurementPrincipalGoodsReturnStoreReadiness.SourceReadReadyAsync(source,null!,default));
        Assert.False(source.Write);Assert.Null(source.Owner);
    }
    [Fact]
    public void Write_stop_does_not_strand_history_or_recovery_and_configuration_cannot_assert_connection()
    {
        var options=new ProcurementPrincipalGoodsReturnOptions{ExecutionDatabaseConfigured=true};
        Assert.True(ProcurementPrincipalReceivingRoutes.ReturnConfigured(options,false));
        Assert.False(ProcurementPrincipalReceivingRoutes.ReturnConfigured(options,true));
        options.Enabled=true;Assert.True(ProcurementPrincipalReceivingRoutes.ReturnConfigured(options,true));
        options.ExecutionDatabaseConfigured=false;Assert.False(ProcurementPrincipalReceivingRoutes.ReturnConfigured(options,false));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {["Procurement:PrincipalGoodsReturnDrafts:ExecutionDatabaseConfigured"]="true"}).Build();
        var services=new ServiceCollection();services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IProcurementPrincipalReceivingReadiness>(new SourceReadiness());
        ProcurementPrincipalGoodsReturnComposition.AddFoundation(services,config);
        using var provider=services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<ProcurementPrincipalGoodsReturnOptions>().ExecutionDatabaseConfigured);
    }
    [Theory]
    [InlineData(false,false,true)]
    [InlineData(true,false,false)]
    [InlineData(false,true,false)]
    public void All_write_headers_cannot_be_laundered_through_default_selected_branch(bool explicitAll,bool rawAll,bool expected)
        =>Assert.Equal(expected,ProcurementPrincipalReceivingCapability.WriteContextAllowed(Guid.NewGuid(),explicitAll,rawAll));
    [Fact]
    public void Return_write_matrix_excludes_source_counter_and_Core_workflow_writes()
    {
        var read=ProcurementPrincipalGoodsReturnStoreReadiness.PrivilegeSql(false,"finite_return");
        var write=ProcurementPrincipalGoodsReturnStoreReadiness.PrivilegeSql(true,"finite_return");
        Assert.DoesNotContain(",'INSERT')",read);
        foreach(var table in ProcurementPrincipalGoodsReturnSchemaCapture.Tables)
            Assert.Contains("has_table_privilege(current_user,'"+table+"','INSERT')",write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.principal_goods_return_receipts','UPDATE')",write);
        Assert.Contains("NOT(a.attname::text=ANY(",write);
        Assert.Contains("session_user=current_user AND current_user='finite_return'",write);
        Assert.Contains("pg_has_role(current_user,r.oid,'SET')",write);
        Assert.DoesNotContain("'procurement.purchase_order_lines','UPDATE'",write);
        Assert.DoesNotContain("'procurement.purchase_request_lines','UPDATE'",write);
        Assert.DoesNotContain("'core_approval.cases','INSERT'",write);
    }
    [Fact]
    public void M5d_expected_objects_are_complete_and_source_successor_keys_are_exact()
    {
        Assert.Equal("20261028180000_AddPrincipalGoodsReturnDrafts",ProcurementPrincipalGoodsReturnSchemaCapture.MigrationId);
        Assert.True(ProcurementPrincipalGoodsReturnSchemaCapture.Populated);
        Assert.Equal(3,ProcurementPrincipalGoodsReturnSchemaCapture.Checks.Count);
        Assert.Equal(12,ProcurementPrincipalGoodsReturnSchemaCapture.ForeignDefinitions.Count);
        Assert.Equal(19,ProcurementPrincipalGoodsReturnSchemaCapture.IndexDefinitions.Count);
        Assert.Equal(3,ProcurementPrincipalReceivingPhysicalSchema.ReturnOwnerKeys.Count);
        var expected=ProcurementPrincipalReceivingPhysicalSchema.ReturnOwnerKeys["principal_receiving_facts"];
        Assert.Equal("ak_pgret_fact_owner",expected.Name);
        Assert.Equal(new[]{"tenant_id","legal_entity_id","id","source_state_id","draft_id","branch_id"},expected.Columns);
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
