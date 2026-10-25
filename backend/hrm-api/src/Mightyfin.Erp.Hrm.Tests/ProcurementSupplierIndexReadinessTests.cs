using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementSupplierIndexReadinessTests
{
    [Fact]
    public void Actual_Supplier_unique_indexes_resolve_exact_registration_capture_and_unfiltered_number()
    {
        using var db=Owner();var supplier=db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Supplier))!;
        var unique=supplier.GetIndexes().Where(x=>x.IsUnique).ToArray();
        Assert.Equal(2,unique.Length);
        Assert.Single(unique.Where(x=>x.GetFilter() is not null));
        foreach(var index in unique)
        {
            Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
                typeof(Supplier),index.GetDatabaseName(),index.GetFilter(),out var actual));
            if(index.GetFilter() is null) Assert.Null(actual);
            else
            {
                Assert.Equal(ProcurementSupplierIndexCapture.RegistrationIndex,index.GetDatabaseName());
                Assert.Equal("normalized_registration IS NOT NULL",index.GetFilter());
                Assert.Equal("(normalized_registration IS NOT NULL)",actual);
            }
        }
        Assert.Equal(System.Data.ConnectionState.Closed,db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData("normalized_registration IS NULL")]
    [InlineData("normalized_registration is not null")]
    [InlineData("(normalized_registration IS NOT NULL)")]
    [InlineData(null)]
    public void Known_Supplier_index_rejects_changed_model_filter_instead_of_normalizing(string? filter)
        => Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(Supplier),ProcurementSupplierIndexCapture.RegistrationIndex,filter,out _));

    [Fact]
    public void Unknown_partial_and_wrong_owner_type_cannot_adopt_Supplier_capture()
    {
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(Supplier),"unreviewed_supplier_filter","normalized_registration IS NOT NULL",out _));
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(SupplierContract),ProcurementSupplierIndexCapture.RegistrationIndex,"normalized_registration IS NOT NULL",out _));
    }

    [Fact]
    public void All_actual_P35_inspected_unique_indexes_have_bounded_owner_predicates()
    {
        using var db=Owner();var model=db.GetService<IDesignTimeModel>().Model;
        var partials=new List<string>();
        foreach(var type in ProcurementContractReviewSchemaCapture.Types.Concat(ProcurementContractReviewStoreReadiness.Dependencies).Distinct())
        foreach(var index in model.FindEntityType(type)!.GetIndexes().Where(x=>x.IsUnique))
        {
            Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(type,
                index.GetDatabaseName(),index.GetFilter(),out _),type.Name+"."+index.GetDatabaseName());
            if(index.GetFilter() is not null)partials.Add(index.GetDatabaseName()!);
        }
        Assert.True(partials.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{
            ProcurementSupplierIndexCapture.RegistrationIndex,"ux_p35_contract_policy_created","ux_p35_contract_policy_published"}));
        Assert.Equal(3,partials.Count);
    }

    [Fact]
    public void All_P32_owned_and_dependency_partial_indexes_are_accounted_for_by_their_actual_owner()
    {
        using var db=Owner();var model=db.GetService<IDesignTimeModel>().Model;
        // P32 inspects these owned types through the generic inspector. The
        // operational identity dependency has its own mandatory physical owner
        // readiness, not an invented generic-inspector capability.
        foreach(var type in ProcurementRfqCommercialSchemaCapture.Types)
        foreach(var index in model.FindEntityType(type)!.GetIndexes().Where(x=>x.IsUnique))
            Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(type,
                index.GetDatabaseName(),index.GetFilter(),out _));
        var partials=new List<string>();
        foreach(var type in ProcurementRfqCommercialStoreReadiness.Dependencies)
        foreach(var index in model.FindEntityType(type)!.GetIndexes().Where(x=>x.IsUnique && x.GetFilter() is not null))
        {
            var name=index.GetDatabaseName()!;partials.Add(name);
            if(type==typeof(OperationalPolicyIdentityRecord))
            {
                var stage=name switch {"ux_operational_policy_identity_created"=>"Created",
                    "ux_operational_policy_identity_published"=>"Published",_=>throw new InvalidOperationException("Unknown operational owner index")};
                Assert.Equal($"stage = '{stage}'",index.GetFilter());
                Assert.Equal($"((stage)::text = '{stage}'::text)",ProcurementOperationalPolicyIdentitySchemaReadiness.PartialPredicates[name]);
            }
            else
            {
                Assert.Equal(typeof(Supplier),type);
                Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(type,name,index.GetFilter(),out _));
            }
        }
        Assert.True(partials.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{
            ProcurementSupplierIndexCapture.RegistrationIndex,"ux_operational_policy_identity_created","ux_operational_policy_identity_published"}));
        Assert.Equal(3,partials.Count);
    }
    private static ProcurementDbContext Owner()=>new(new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseNpgsql("Host=never-used.invalid;Database=supplier-index-model-only").Options,new Scope());
    private sealed class Scope:IProcurementScope
    {
        public string TenantId=>"supplier-index-test";
        public Guid LegalEntityId=>Guid.Parse("11111111-1111-4111-8111-111111111111");
        public string SubjectId=>"22222222-2222-4222-8222-222222222222";
        public Guid? WorkLocationId=>null;public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;public string? CorrelationId=>null;
    }
}
