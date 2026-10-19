namespace Mightyfin.Erp.Core.Approvals.Domain;

/// <summary>Shared ERP approval data. Source modules own the business action and its guards.</summary>
public abstract class ApprovalRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApprovalCase : ApprovalRecord
{
    public string SourceModule { get; set; } = null!;
    public string SourceType { get; set; } = null!;
    public Guid SourceId { get; set; }
    public long SourceVersion { get; set; }
    public Guid PolicyVersionId { get; set; }
    public int PolicyVersionNumber { get; set; }
    public string MakerSubjectId { get; set; } = null!;
    public string State { get; set; } = "Pending";
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletionKey { get; set; }
    public ICollection<ApprovalStage> Stages { get; set; } = new List<ApprovalStage>();
}

public sealed class ApprovalStage : ApprovalRecord
{
    public Guid CaseId { get; set; }
    public int Sequence { get; set; }
    public string Code { get; set; } = null!;
    public string Mode { get; set; } = "Quorum";
    public int RequiredApprovals { get; set; } = 1;
    public string State { get; set; } = "Waiting";
    public DateTimeOffset? DueAt { get; set; }
    public ICollection<ApprovalAssignment> Assignments { get; set; } = new List<ApprovalAssignment>();
}

public sealed class ApprovalAssignment : ApprovalRecord
{
    public Guid CaseId { get; set; }
    public Guid StageId { get; set; }
    public string AssigneeSubjectId { get; set; } = null!;
    public string? DelegatedFromSubjectId { get; set; }
    public string PermissionKey { get; set; } = null!;
    public string State { get; set; } = "Pending";
}

public sealed class ApprovalDecision : ApprovalRecord
{
    public Guid CaseId { get; set; }
    public Guid StageId { get; set; }
    public Guid AssignmentId { get; set; }
    public string ActorSubjectId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? Reason { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApprovalDelegation : ApprovalRecord
{
    public string DelegatorSubjectId { get; set; } = null!;
    public string DelegateSubjectId { get; set; } = null!;
    public string PermissionKey { get; set; } = null!;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset EffectiveTo { get; set; }
    public bool Active { get; set; } = true;
    public string Reason { get; set; } = null!;
}
