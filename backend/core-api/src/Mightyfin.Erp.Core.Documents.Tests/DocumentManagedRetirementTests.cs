using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

/// <summary>Design-only Npgsql contexts never open connections. These prove managed entry denials, not runtime races.</summary>
public sealed class DocumentManagedRetirementTests
{
    private static readonly Scope Actor=new("tenant-a",Guid.NewGuid(),"retirement-officer");
    private static readonly DateTimeOffset At=new(2026,10,21,12,0,0,TimeSpan.Zero);
    private static readonly DocumentOwner Owner=new("procurement","sourcing_event",Guid.NewGuid());
    private static DocumentDbContext Context()=>new(new DbContextOptionsBuilder<DocumentDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only_never_open;Username=design_only").Options,Actor);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_relational_retire_requires_managed_writer_before_any_database_io(bool synchronous)
    {
        await using var db=Context();
        var document=CoreDocument.Create(Actor,Owner.Module,Owner.Type,Owner.Id,At);
        db.Attach(document);
        document.Retire(Actor,"Original domain transition alone is not a held fence",At.AddSeconds(1));
        if(synchronous)
            Assert.Contains("exact authorized writer",Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
        else
            Assert.Contains("exact authorized writer",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        Assert.Equal(System.Data.ConnectionState.Closed,db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Retired_history_cannot_be_reopened_by_a_direct_tracked_update()
    {
        await using var db=Context();
        var document=CoreDocument.Create(Actor,Owner.Module,Owner.Type,Owner.Id,At);
        document.Retire(Actor,"Retained original history",At.AddSeconds(1));
        db.Attach(document);
        db.Entry(document).Property(x=>x.IsRetired).CurrentValue=false;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        Assert.Contains("cannot be rewritten",error.Message);
        Assert.Equal(System.Data.ConnectionState.Closed,db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(long.MaxValue)]
    public async Task Invalid_revision_is_rejected_before_authorizer_or_connection(long revision)
    {
        await using var db=Context(); var calls=0;
        var writer=new EfDocumentRetirement(db,Actor,(_,_)=>{calls++;throw new InvalidOperationException("Not called");});
        await Assert.ThrowsAsync<ArgumentException>(()=>writer.RetireAsync(Owner,Guid.NewGuid(),revision,"Reason"));
        Assert.Equal(0,calls);
        Assert.Equal(System.Data.ConnectionState.Closed,db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Dirty_or_nonrelational_context_is_not_a_fresh_authorized_writer()
    {
        var calls=0;
        Task<IDocumentRetirementAccess> Authorize(DocumentDbContext _,CancellationToken ct)
        {calls++;throw new InvalidOperationException("Not called");}
        await using(var db=Context())
        {
            db.Attach(CoreDocument.Create(Actor,Owner.Module,Owner.Type,Owner.Id,At));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>new EfDocumentRetirement(db,Actor,Authorize)
                .RetireAsync(Owner,Guid.NewGuid(),1,"Reason"));
            Assert.Equal(System.Data.ConnectionState.Closed,db.Database.GetDbConnection().State);
        }
        await using(var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,Actor))
            await Assert.ThrowsAsync<InvalidOperationException>(()=>new EfDocumentRetirement(db,Actor,Authorize)
                .RetireAsync(Owner,Guid.NewGuid(),1,"Reason"));
        Assert.Equal(0,calls);
    }

    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IDocumentScope;
}
