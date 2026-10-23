using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Actual SaveChanges guard tests over InMemory; no claim of Core authentication,
/// PostgreSQL constraints, finite-role permissions or transactional concurrency.</summary>
public sealed class PrincipalRequestIdentityPersistenceTests
{
    private sealed record Scope(Guid LegalEntityId,string SubjectId,Guid? CoreLinkedWorkerId):IProcurementScope
    {
        public string TenantId=>"principal-original";
        public Guid? WorkLocationId=>null;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;
        public string? CorrelationId=>"original-provenance";
    }
    private static ProcurementDbContext Store(string database,Scope scope)=>new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options,scope);
    private static PurchaseRequest Request(Scope scope)=>new(){TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
        CreatedBy=scope.SubjectId,RequesterPrincipalId=Guid.Parse(scope.SubjectId),SourceChannel="ERP",Status="Draft",
        Number="PR-"+Guid.NewGuid().ToString("N"),Purpose="Synthetic original",CurrencyCode="ZMW"};

    [Fact]
    public async Task New_original_optional_alias_roundtrips_and_rehashed_mutation_cannot_forge_OriginalValues()
    {
        var scope=new Scope(Guid.NewGuid(),Guid.NewGuid().ToString("D"),Guid.NewGuid());var database=Guid.NewGuid().ToString();Guid id;
        await using(var create=Store(database,scope))
        {
            var request=Request(scope);PurchaseRequestPrincipalIdentity.CaptureNew(request,scope.SubjectId,scope.CoreLinkedWorkerId);
            create.AddScoped(request);await create.SaveChangesAsync();id=request.Id;
        }
        await using(var edit=Store(database,scope))
        {
            var request=await edit.PurchaseRequests.SingleAsync(x=>x.Id==id);
            Assert.True(PurchaseRequestPrincipalIdentity.Valid(request));Assert.Equal(scope.CoreLinkedWorkerId,request.RequesterCoreWorkerId);
            request.RequesterCoreWorkerId=Guid.NewGuid();request.RequesterIdentitySha256=PurchaseRequestPrincipalIdentity.Digest(request);
            edit.Entry(request).Property(x=>x.RequesterCoreWorkerId).OriginalValue=request.RequesterCoreWorkerId;
            edit.Entry(request).Property(x=>x.RequesterIdentitySha256).OriginalValue=request.RequesterIdentitySha256;
            request.Purpose="Attempted rewritten original";
            await Assert.ThrowsAsync<InvalidOperationException>(()=>edit.SaveChangesAsync());
        }
        await using var retained=Store(database,scope);var original=await retained.PurchaseRequests.SingleAsync(x=>x.Id==id);
        Assert.True(PurchaseRequestPrincipalIdentity.Valid(original));Assert.Equal(scope.CoreLinkedWorkerId,original.RequesterCoreWorkerId);
        Assert.Equal("Synthetic original",original.Purpose);
    }

    [Fact]
    public async Task Existing_basic_principal_request_cannot_be_adopted_into_new_operational_original_provenance()
    {
        var scope=new Scope(Guid.NewGuid(),Guid.NewGuid().ToString("D"),null);var database=Guid.NewGuid().ToString();Guid id;
        await using(var create=Store(database,scope))
        {
            var request=Request(scope);create.AddScoped(request);await create.SaveChangesAsync();id=request.Id;
        }
        await using(var adopt=Store(database,scope))
        {
            var request=await adopt.PurchaseRequests.SingleAsync(x=>x.Id==id);
            request.RequesterIdentitySchemaVersion=1;request.RequesterIdentitySha256=PurchaseRequestPrincipalIdentity.Digest(request);
            adopt.Entry(request).Property(x=>x.RequesterIdentitySchemaVersion).OriginalValue=1;
            adopt.Entry(request).Property(x=>x.RequesterIdentitySha256).OriginalValue=request.RequesterIdentitySha256;
            request.Purpose="Attempted adoption";
            await Assert.ThrowsAsync<InvalidOperationException>(()=>adopt.SaveChangesAsync());
        }
        await using var retained=Store(database,scope);var original=await retained.PurchaseRequests.SingleAsync(x=>x.Id==id);
        Assert.Equal(0,original.RequesterIdentitySchemaVersion);Assert.Null(original.RequesterIdentitySha256);
        Assert.Equal("Synthetic original",original.Purpose);
    }

    [Fact]
    public async Task New_original_alias_must_equal_actual_supplied_current_scope_not_an_arbitrary_worker()
    {
        var scope=new Scope(Guid.NewGuid(),Guid.NewGuid().ToString("D"),null);
        await using var create=Store(Guid.NewGuid().ToString(),scope);var request=Request(scope);
        PurchaseRequestPrincipalIdentity.CaptureNew(request,scope.SubjectId,Guid.NewGuid());create.AddScoped(request);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>create.SaveChangesAsync());
        Assert.Empty(await create.PurchaseRequests.AsNoTracking().ToListAsync());Assert.Empty(await create.AuditEvents.AsNoTracking().ToListAsync());
    }
}
