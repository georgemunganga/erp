using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

/// <summary>Source-only pure evidence/factory boundaries. No synthetic lease is accepted as PostgreSQL proof.</summary>
public sealed class DocumentUseOwnerAdapterTests
{
    private static readonly Scope Actor=new("tenant-a",Guid.NewGuid(),"maker");
    private static readonly DateTimeOffset At=new(2026,10,21,12,0,0,TimeSpan.Zero);

    [Fact]
    public void Restored_original_evidence_is_independent_of_current_retirement_and_actor()
    {
        var f=Fixture();
        f.Document.Retire(Actor,"Later owner lifecycle fixture",At.AddDays(1));
        var restored=EfDocumentUsePinStore.Restore(f.Stored).Single();
        Assert.Equal(f.Pin.Id,restored.Id);
        Assert.Equal(f.Pin.Source,restored.Source);
        Assert.Equal(f.Pin.Scan,restored.Scan);
        Assert.Equal(f.Pin.CreatedBy,restored.CreatedBy);
        Assert.Equal(f.Pin.CreatedAt,restored.CreatedAt);
        Assert.Equal(f.Pin.VersionRevision,restored.VersionRevision);
        Assert.Equal(f.Pin.Sha256,restored.Sha256);
        // This does not authorize a new use of the retired current document.
        Assert.Throws<InvalidOperationException>(()=>DocumentUsePin.Create(Actor,f.Pin.Source,
            f.Stored.Pins.Single().Selection(),f.Document,f.Version,f.Pin.Scan,At.AddDays(2)));
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("LegalEntityId")]
    [InlineData("UseSetId")]
    [InlineData("OwnerId")]
    [InlineData("CreatedBy")]
    [InlineData("CreatedAt")]
    public void Cross_set_scope_owner_original_actor_or_time_cannot_be_rehydrated(string field)
    {
        var f=Fixture(); var row=f.Stored.Pins.Single();
        object changed=field switch
        {
            "TenantId"=>"foreign", "CreatedBy"=>"other-current-reader",
            "CreatedAt"=>At.AddSeconds(1), _=>Guid.NewGuid(),
        };
        Set(row,field,changed);
        // Even a recomputed selection checksum does not repair foreign set ownership/original identity.
        Set(f.Stored.Set,nameof(DocumentUseSetRow.SelectionDigest),DocumentUsePinRow.Digest(f.Stored.Pins));
        Assert.Throws<InvalidOperationException>(()=>EfDocumentUsePinStore.Restore(f.Stored));
    }

    [Theory]
    [InlineData("ScanVerdict")]
    [InlineData("ScanEvidenceId")]
    [InlineData("ScanAttempt")]
    [InlineData("ScanFinishedAt")]
    [InlineData("Sha256")]
    [InlineData("FileName")]
    [InlineData("DocumentRevision")]
    public void Hash_valid_but_malformed_retained_scan_or_content_is_denied(string field)
    {
        var f=Fixture(); var row=f.Stored.Pins.Single();
        object changed=field switch
        {
            "ScanVerdict"=>ScannerVerdict.Infected, "ScanEvidenceId"=>Guid.Empty,
            "ScanAttempt"=>0, "ScanFinishedAt"=>At.AddSeconds(5),
            "Sha256"=>new string('A',64), "FileName"=>"../secret.pdf", _=>0L,
        };
        Set(row,field,changed);
        Set(f.Stored.Set,nameof(DocumentUseSetRow.SelectionDigest),DocumentUsePinRow.Digest(f.Stored.Pins));
        Assert.Throws<InvalidOperationException>(()=>EfDocumentUsePinStore.Restore(f.Stored));
    }

    [Fact]
    public void Incomplete_duplicate_and_tampered_complete_sets_are_denied()
    {
        var f=Fixture();
        Assert.Throws<InvalidOperationException>(()=>EfDocumentUsePinStore.Restore(new(f.Stored.Set,[])));
        Assert.Throws<InvalidOperationException>(()=>EfDocumentUsePinStore.Restore(new(f.Stored.Set,[f.Stored.Pins[0],f.Stored.Pins[0]])));
        Set(f.Stored.Pins[0],nameof(DocumentUsePinRow.MediaType),"image/png");
        Assert.Throws<InvalidOperationException>(()=>EfDocumentUsePinStore.Restore(f.Stored));
    }

    [Fact]
    public void Microsecond_restoration_preserves_canonical_original_values_without_current_records()
    {
        var f=Fixture(); var row=f.Stored.Pins.Single();
        var restored=row.Restore(f.Stored.Set.Reference());
        Assert.Equal(0,restored.Scan.StartedAt.UtcTicks%10);
        Assert.Equal(0,restored.CreatedAt.UtcTicks%10);
        Assert.Equal(row.Id,restored.Id);
        Assert.Equal(row.ScanEvidenceId,restored.Scan.Id);
        Assert.Throws<InvalidOperationException>(()=>DocumentUsePin.Restore(restored.Id,restored.TenantId,
            restored.LegalEntityId,restored.Source,row.Selection(),restored.VersionNumber,restored.FileName,
            restored.MediaType,restored.SizeBytes,restored.Scan,restored.CreatedBy,restored.CreatedAt.AddTicks(1)));
    }

    [Fact]
    public async Task Factory_never_accepts_a_missing_lease_even_with_an_allowing_purpose_authorizer()
    {
        await using var db=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,Actor);
        var access=new RecordingAccess();
        await Assert.ThrowsAsync<ArgumentNullException>(()=>EfDocumentUsePinStore.CreateAsync(db,Actor,access,null!,
            [new("procurement","sourcing_event",Guid.NewGuid())]));
        Assert.Equal(0,access.Calls);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await db.UseSets.ToListAsync());
    }

    [Fact]
    public void Exact_purpose_operations_distinguish_metadata_read_from_content_download()
    {
        Assert.NotEqual(DocumentUseOperation.Read,DocumentUseOperation.Download);
        Assert.NotEqual(DocumentUseOperation.Acquire,DocumentUseOperation.Read);
        Assert.DoesNotContain(typeof(EfDocumentUsePinStore).GetConstructors(),_=>true);
    }

    private static FixtureData Fixture()
    {
        var owner=new DocumentOwner("procurement","sourcing_event",Guid.NewGuid());
        var document=CoreDocument.Create(Actor,owner.Module,owner.Type,owner.Id,At);
        var version=CoreDocumentVersion.Create(document,Actor,1,"requirements.pdf","application/pdf",100,new string('a',64),At);
        var lease=Guid.NewGuid(); version.BeginScan(lease,At,TimeSpan.FromMinutes(1));
        var scan=version.FinishScan(lease,version.Sha256,ScannerVerdict.Clean,"fixture","definitions",At.AddSeconds(1));
        var source=new DocumentUseReference(Guid.NewGuid(),"procurement","sourcing_review",owner.Id,1,DocumentUseKind.Review);
        var selection=new DocumentUseSelection(owner.Module,owner.Type,owner.Id,document.Id,version.Id,document.Version,version.Version,version.Sha256);
        var pin=DocumentUsePin.Create(Actor,source,selection,document,version,scan,At.AddSeconds(2));
        var set=DocumentUseSetRow.FromPins([pin]);
        return new(document,version,pin,new(set,[DocumentUsePinRow.FromPin(set.Id,pin)]));
    }
    private static void Set(object row,string name,object value)=>row.GetType().GetProperty(name)!.SetValue(row,value);
    private sealed record FixtureData(CoreDocument Document,CoreDocumentVersion Version,DocumentUsePin Pin,StoredDocumentUse Stored);
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IDocumentScope;
    private sealed class RecordingAccess:IDocumentUseAccess
    {
        public int Calls { get; private set; }
        public Task RequireAsync(DocumentOwner owner,DocumentUseReference source,DocumentUseOperation operation,
            IReadOnlyList<Guid> versionIds,CancellationToken ct) { Calls++;return Task.CompletedTask; }
    }
}
