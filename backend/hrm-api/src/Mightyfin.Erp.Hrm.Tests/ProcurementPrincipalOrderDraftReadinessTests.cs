using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Source guard contracts. Actual PG catalog and finite-role checks
/// require the separate migration rehearsal and private HTTP cohort.</summary>
public sealed class ProcurementPrincipalOrderDraftReadinessTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Uncaptured_physical_checks_never_accept_an_owning_context(bool write)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("m3b-readiness", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "test");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, scope);
        var reader = new ProcurementPrincipalOrderDraftSchemaReadiness(
            new ConfigurationBuilder().Build(), scope, null!, null!);
        Assert.False(await reader.IsReadyAsync(owner, write, default));
        Assert.False(owner.ChangeTracker.HasChanges());
    }

    [Fact]
    public void Write_contract_is_the_distinct_finite_execution_role_and_append_only_evidence()
    {
        var write = ProcurementPrincipalOrderDraftSchemaReadiness.PrivilegeSql(true, "p30-execute");
        var read = ProcurementPrincipalOrderDraftSchemaReadiness.PrivilegeSql(false, "p30-execute");
        Assert.Contains("session_user=current_user", write);
        Assert.Contains("current_user='p30-execute'", write);
        Assert.Contains("rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.principal_order_source_bindings','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.principal_order_draft_receipts','INSERT')", write);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.principal_order_draft_receipts','UPDATE')", write);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.audit_events','DELETE')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.purchase_request_lines','UPDATE')", write);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.principal_order_draft_receipts','INSERT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.purchase_request_lines','UPDATE')", read);
        Assert.DoesNotContain("'SELECT,INSERT'", write); // PostgreSQL's comma privilege form means ANY.
    }
}
