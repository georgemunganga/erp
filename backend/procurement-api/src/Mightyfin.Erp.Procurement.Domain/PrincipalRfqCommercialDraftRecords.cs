namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Editable commercial preparation only; never published/award authority.
/// Original scope/source/owner fields are immutable; Revision is an ordinary CAS.</summary>
public sealed class PrincipalRfqCommercialDraftHead:ProcurementRecord
{
    public int SchemaVersion{get;init;}=1;
    public Guid SourcingEventId{get;init;}
    public Guid SourceEvidenceId{get;init;}
    public int SourceIdentitySchemaVersion{get;init;}=2;
    public Guid BranchId{get;init;}
    public string OwnerPrincipalId{get;init;}=null!;
    public Guid? OwnerWorkerAliasId{get;init;}
    public string OriginalFingerprint{get;init;}=null!;
    public string SourceEnvelopeSha256{get;init;}=null!;
    public long Revision{get;set;}=1;
    public string SnapshotSha256{get;set;}=null!;
}
public sealed class PrincipalRfqCommercialDraftRevision:ProcurementRecord
{
    public int SchemaVersion{get;init;}=1;
    public Guid DraftId{get;init;}
    public Guid SourcingEventId{get;init;}
    public Guid SourceEvidenceId{get;init;}
    public Guid BranchId{get;init;}
    public long Revision{get;init;}
    public Guid CommandKey{get;init;}
    public string ActorPrincipalId{get;init;}=null!;
    public Guid? ActorWorkerAliasId{get;init;}
    public string SnapshotJson{get;init;}=null!;
    public string SnapshotSha256{get;init;}=null!;
}
public sealed class PrincipalRfqCommercialDraftReceipt:ProcurementRecord
{
    public int SchemaVersion{get;init;}=1;
    public Guid DraftId{get;init;}
    public Guid DraftRevisionId{get;init;}
    public Guid SourcingEventId{get;init;}
    public Guid SourceEvidenceId{get;init;}
    public Guid BranchId{get;init;}
    public string Operation{get;init;}=null!;
    public Guid CommandKey{get;init;}
    public string ActorPrincipalId{get;init;}=null!;
    public string RequestSha256{get;init;}=null!;
    public string SnapshotSha256{get;init;}=null!;
    public string ResponseJson{get;init;}=null!;
    public string ResponseSha256{get;init;}=null!;
    public int ResponseStatus{get;init;}
    public long ResultRevision{get;init;}
}
