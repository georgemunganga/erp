using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real Npgsql design metadata, no database connection or synthetic model differ.
/// Private fresh/retained schema and actual Program startup remain separate gates.</summary>
public sealed class HrmHandwrittenModelContractTests
{
    [Fact]
    public void Model_uses_exact_published_contract_and_credential_object_names()
    {
        using var db = Context(); var model = db.GetService<IDesignTimeModel>().Model;
        var contract = model.FindEntityType(typeof(ContractType))!;
        Assert.Equal("pk_contract_types", contract.FindPrimaryKey()!.GetName());
        var contractIndex = Assert.Single(contract.GetIndexes());
        Assert.True(contractIndex.IsUnique);
        Assert.Equal("ix_contract_types_tenant_id_code", contractIndex.GetDatabaseName());
        Assert.Equal(new[] { "TenantId", "Code" }, contractIndex.Properties.Select(x => x.Name));
        var link = model.FindEntityType(typeof(LocalCredentialLink))!;
        Assert.Equal("pk_local_credential_links", link.FindPrimaryKey()!.GetName());
        var indexes = link.GetIndexes().ToDictionary(x => x.GetDatabaseName()!);
        Assert.Equal(2, indexes.Count);
        Assert.True(indexes["ix_local_credential_links_token_hash"].IsUnique);
        Assert.Equal(new[] { "TokenHash" }, indexes["ix_local_credential_links_token_hash"].Properties.Select(x => x.Name));
        Assert.False(indexes["ix_local_credential_links_tenant_id_local_user_id_expires_at"].IsUnique);
        Assert.Equal(new[] { "TenantId", "LocalUserId", "ExpiresAt" }, indexes["ix_local_credential_links_tenant_id_local_user_id_expires_at"].Properties.Select(x => x.Name));
    }

    [Fact]
    public void Salary_advance_names_do_not_change_relationship_or_published_index_semantics()
    {
        using var db = Context(); var model = db.GetService<IDesignTimeModel>().Model;
        var salary = model.FindEntityType(typeof(SalaryAdvance))!;
        Assert.Equal("pk_salary_advances", salary.FindPrimaryKey()!.GetName());
        var worker = Assert.Single(salary.GetForeignKeys());
        Assert.Equal("fk_salary_advances_workers_worker_id", worker.GetConstraintName());
        Assert.Equal(typeof(Worker), worker.PrincipalEntityType.ClrType);
        Assert.Equal(new[] { "WorkerId" }, worker.Properties.Select(x => x.Name));
        Assert.Equal(DeleteBehavior.Cascade, worker.DeleteBehavior);
        var indexes = salary.GetIndexes().ToDictionary(x => x.GetDatabaseName()!);
        Assert.Equal(3, indexes.Count);
        Assert.Equal(new[] { "WorkerId" }, indexes["ix_salary_advances_worker_id"].Properties.Select(x => x.Name));
        Assert.Equal(new[] { "TenantId", "WorkerId", "Status" }, indexes["ix_salary_advances_tenant_id_worker_id_status"].Properties.Select(x => x.Name));
        Assert.Equal(new[] { "TenantId", "DeductFromPayslip", "DeductionStartDate" }, indexes["ix_salary_advances_tenant_deduct_start"].Properties.Select(x => x.Name));
        Assert.All(indexes.Values, index => { Assert.False(index.IsUnique); Assert.Null(index.GetFilter()); });
        Assert.True(salary.FindProperty(nameof(SalaryAdvance.IssueDate))!.IsNullable);
        Assert.False(salary.FindProperty(nameof(SalaryAdvance.OpeningRecoveredAmount))!.IsNullable);
    }

    [Fact]
    public void Worker_identity_index_remains_original_unique_single_predicate_contract()
    {
        using var db = Context(); var model = db.GetService<IDesignTimeModel>().Model;
        var index = model.FindEntityType(typeof(Worker))!.GetIndexes()
            .Single(x => x.GetDatabaseName() == "IX_workers_tenant_id_subject_id");
        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "TenantId", "SubjectId" }, index.Properties.Select(x => x.Name));
        Assert.Equal("subject_id IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void Genuine_initialized_snapshot_and_current_model_have_no_pending_or_generated_operations()
    {
        using var db = Context();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        var source = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true).GetRelationalModel();
        var current = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(source, current));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static HrmDbContext Context() => new(new DbContextOptionsBuilder<HrmDbContext>()
        .UseNpgsql("Host=invalid;Database=design_model_only;Username=not_used",
            postgres => postgres.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new Tenant());
    private sealed class Tenant : ITenantAccessor { public string GetTenantId() => "design-model-only"; }
}
