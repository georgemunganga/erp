namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only prepared CANDIDATE; not a published RFQ or award authority.</summary>
public sealed class PrincipalRfqPreparedPlan:ProcurementRecord
{
    public int SchemaVersion{get;init;}=1;
    public Guid SourcingEventId{get;init;}
    public Guid DraftId{get;init;}
    public Guid DraftRevisionId{get;init;}
    public Guid SourceEvidenceId{get;init;}
    public Guid BranchId{get;init;}
    public long DraftRevision{get;init;}
    public long EventVersion{get;init;}
    public string ActorPrincipalId{get;init;}=null!;
    public Guid? ActorWorkerAliasId{get;init;}
    public string DraftSnapshotSha256{get;init;}=null!;
    public Guid PolicyVersionId{get;init;}
    public Guid PublishedPolicyIdentityId{get;init;}
    public string PublishedStage{get;init;}="Published";
    public string PolicyRulesSha256{get;init;}=null!;
    public string PublishedIdentitySha256{get;init;}=null!;
    public bool RequirementsPackRequired{get;init;}
    public int DocumentCount{get;init;}
    public Guid DocumentUseKey{get;init;}
    public Guid? DocumentUseSetId{get;init;}
    public string? DocumentSelectionDigest{get;init;}
    public string EvaluationRulesSha256{get;init;}=null!;
    public string SnapshotJson{get;init;}=null!;
    public string SnapshotSha256{get;init;}=null!;
    public DateTimeOffset PreparedAt{get;init;}
}
public sealed class PrincipalRfqPreparationReceipt:ProcurementRecord
{
    public int SchemaVersion{get;init;}=1;
    public Guid PreparedPlanId{get;init;}
    public Guid DraftRevisionId{get;init;}
    public Guid DraftCommandReceiptId{get;init;}
    public Guid SourcingEventId{get;init;}
    public Guid BranchId{get;init;}
    public Guid CommandKey{get;init;}
    public string ActorPrincipalId{get;init;}=null!;
    public string RequestSha256{get;init;}=null!;
    public string SnapshotSha256{get;init;}=null!;
    public string ResponseJson{get;init;}=null!;
    public string ResponseSha256{get;init;}=null!;
}
